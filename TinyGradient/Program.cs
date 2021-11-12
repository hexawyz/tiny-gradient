using System.Globalization;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Companding;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

(Color Color, float Position)[] stops;
string fileName = null!;
int size = 512;
bool isVertical = false;

{
	var stopList = new List<(Color Color, float? Position)>();
	bool shouldReverseStops = false;

	const int OptionSize = 1;
	const int OptionVertical = 2;
	const int OptionHorizontal = 3;
	const int OptionReverse = 4;

	var optionMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "-s", OptionSize },
		{ "--size", OptionSize },
		{ "/s", OptionSize },
		{ "/size", OptionSize },
		{ "-v", OptionVertical },
		{ "--vertical", OptionVertical },
		{ "/v", OptionVertical },
		{ "/vertical", OptionVertical },
		{ "-r", OptionReverse },
		{ "--reverse", OptionReverse },
		{ "/r", OptionReverse },
		{ "/reverse", OptionReverse },
		{ "--h", OptionHorizontal },
		{ "--horizontal", OptionHorizontal },
		{ "/h", OptionHorizontal },
		{ "/horizontal", OptionHorizontal },
	};

	// States used for parsing the command line arguments following the pattern:
	// color [position] color [position] [color [position]] [color [position]] … [option] filename
	const int StateBeforeFirstColor = 0; // Expect a (first) color value
	const int StateAfterFirstColor = 1; // Expect a (second) color or (first) position
	const int StateBeforeSecondColor = 2; // Expect a (second) color value (after first position)
	const int StateBeforeAny = 3; // Expect anything (there are at least two stops in the list)
	const int StateBeforeAnyExceptPosition = 4; // Expect a color value or an option or filename
	const int StateAfterOption = 5; // Expect another option or filename
	const int StateBeforeOptionValue = 6; // Expect option value
	const int StateEnd = 7; // Expect nothing, filename has been provided

	void PrintUsage()
	{
		Console.WriteLine("Usage:");
		Console.WriteLine("\ttg color [position] color [position] [color [position]] [color [position]] … [options] filename");
		Console.WriteLine();
		Console.WriteLine("Options:");
		Console.WriteLine("\t/s,/size        Specifies the size of the gradient. Default: 512.");
		Console.WriteLine("\t/h,/horizontal  Produces an horizontal gradient.");
		Console.WriteLine("\t/v,/vertical    Produces a vertical gradient.");
		Console.WriteLine("\t/r,/reverse     Reverses the gradient direction.");
	}

	if (args.Length == 0)
	{
		PrintUsage();
		return;
	}

	try
	{
		int state = 0;
		int lastOption = 0;
		float minStop = 0;
		for (int i = 0; i < args.Length; i++)
		{
			string? arg = args[i];

			if (i == args.Length - 1)
			{
				if (stopList.Count < 2) throw new ArgumentException("The command line should contain at least two stop definitions.");
				if (state is not (StateBeforeAny or StateBeforeAnyExceptPosition or StateAfterOption)) throw new ArgumentException($"The value for option {args[^2]} must be specified before the filename.");
				if (arg.IndexOfAny(Path.GetInvalidPathChars()) >= 0) throw new ArgumentException($"The specified path contains invalid characters: {arg}.");

				fileName = arg;
				state = StateEnd;
			}
			else if (state == StateBeforeOptionValue)
			{
				switch (lastOption)
				{
					case 0:
						throw new InvalidOperationException("This case should not have been reached. ID: 1.");
					case OptionSize:
						size = int.Parse(arg, NumberStyles.None, CultureInfo.InvariantCulture);
						if ((uint)size is < 2U or > 4096U) throw new ArgumentException("Gradient size must be comprised between 2 and 4096 included.");
						break;
					default:
						throw new InvalidOperationException($"Processing for option {args[i - 1]} has not been implemented.");
				}
				lastOption = 0;
				state = StateAfterOption;
			}
			else if (arg is { Length: > 0 } && arg[0] is '-' or '/')
			{
				if (state is not (StateBeforeAny or StateBeforeAnyExceptPosition or StateAfterOption)) throw new ArgumentException($"Unexpected option: {arg}.");
				if (!optionMapping.TryGetValue(arg, out int option)) throw new ArgumentException($"Invalid option: {arg}.");

				state = StateAfterOption;
				switch (option)
				{
					case OptionSize:
						lastOption = option;
						state = StateBeforeOptionValue;
						break;
					case OptionHorizontal:
						isVertical = false;
						break;
					case OptionVertical:
						isVertical = true;
						break;
					case OptionReverse:
						shouldReverseStops = !shouldReverseStops;
						break;
					default:
						throw new InvalidOperationException($"Processing for option {arg} has not been implemented.");
				}
			}
			else if (arg[^1] == '%')
			{
				if (state is not (StateAfterFirstColor or StateBeforeAny)) throw new ArgumentException("A percent value can only follow a color.");

				float stopValue = float.Parse(arg.AsSpan(0, arg.Length - 1), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) / 100;

				if (stopValue <= minStop || stopValue > 1 || stopValue == 1 && i < args.Length - 1) throw new ArgumentException("Stop positions can only be increasing and contained between 0 and 1.");

				CollectionsMarshal.AsSpan(stopList)[^1].Position = stopValue;

				state = state switch
				{
					StateAfterFirstColor => StateBeforeSecondColor,
					StateBeforeAny => StateBeforeAnyExceptPosition,
					_ => throw new InvalidOperationException("This case should not have been reached. ID: 2.")
				};
			}
			else
			{
				stopList.Add((Color.Parse(arg), null));
				state = state switch
				{
					StateBeforeFirstColor => StateAfterFirstColor,
					StateAfterFirstColor => StateBeforeAny,
					StateBeforeSecondColor => StateBeforeAny,
					StateBeforeAnyExceptPosition => StateBeforeAny,
					StateBeforeAny => StateBeforeAny,
					_ => throw new InvalidOperationException("This case should not have been reached. ID: 3.")
				};
			}
		}
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine("An error occured when parsing the command line:");
		Console.Error.WriteLine(ex.Message);
		Console.Error.WriteLine();
		PrintUsage();
		return;
	}

	var stopSpan = CollectionsMarshal.AsSpan(stopList);
	for (int i = 0; i < stopSpan.Length; i++)
	{
		ref var stop = ref stopSpan[i];

		if (stop.Position is null)
		{
			if (i == 0)
			{
				stop.Position = 0;
			}
			else if (i == stopSpan.Length - 1)
			{
				stop.Position = 1;
			}
			else
			{
				int j = i + 1;

				for (; j < stopSpan.Length; j++)
				{
					if (stopSpan[j].Position != null)
					{
						break;
					}
				}

				if (j == stopSpan.Length)
				{
					stopSpan[--j].Position = 1;
				}

				float from = stopSpan[i - 1].Position.GetValueOrDefault();
				float delta = stopSpan[j].Position.GetValueOrDefault() - from;
				float factor = 1f / (j - i + 1);
				float n = 1f;

				for (; i < j; i++)
				{
					stopSpan[i].Position = from + n++ * delta * factor;
				}
			}
		}
	}

	if (shouldReverseStops)
	{
		stopSpan.Reverse();
		for (int i = 0; i < stopSpan.Length; i++)
		{
			ref var stop = ref stopSpan[i];
			stop.Position = 1 - stop.Position;
		}
	}

	stops = stopList.Select(s => (s.Color, s.Position.GetValueOrDefault())).ToArray();
}

(Vector4 Color, float Position) GetStop(int index)
{
	var stop = stops[index];

	var color = (Vector4)stop.Color;
	SRgbCompanding.Expand(ref color);

	return (color, stop.Position);
}

using var image = new Image<Rgba32>(size, 1);
{
	var pixels = image.GetPixelRowSpan(0);

	var from = GetStop(0);
	var to = GetStop(1);
	int nextIndex = 2;

	for (int i = 0; i < pixels.Length; i++)
	{
		float position = i / (float)(pixels.Length - 1);

		while (position >= to.Position && nextIndex < stops.Length)
		{
			from = to;
			to = GetStop(nextIndex++);
		}

		// Before the first step or after the last step, use an uniform color.
		Vector4 color;
		if (position <= from.Position) color = from.Color;
		else if (position >= to.Position) color = to.Color;
		else color = Vector4.Lerp(from.Color, to.Color, Math.Min(1f, (position - from.Position) / (to.Position - from.Position)));

		SRgbCompanding.Compress(ref color);

		pixels[i] = new Rgba32(color);
	}

	if (isVertical)
	{
		image.Mutate(ctx => ctx.Rotate(90));
	}

	image.Save(fileName!);
}
