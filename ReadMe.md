# TinyGradient

TinyGradient (short tg) is a command line tool to quickly generate gradient images.

It defaults to generating perceptually correct gradients, taking into account the sRGB curve.

# Usage

````
tg color [position] color [position] [color [position]] [color [position]] … [options] filename
````

At the core, the tool expects a sequence of colors, each optionally followed by a percentage value indicating the relative position of the color stop in the gradient.

Colors should be expressed using the HTML/CSS # syntax (i.e. #rgb or #rrggbb or #rrggbbaa) or by a standard color name (e.g. red or black).
Percent values can be floating point values such as `0%`, `12.5%` or `100%`.

When the position for a color stop is not specified, the tool will automatically assign a position to the color stop using linear interpolation.
Of course, first and last color stops default to 0% and 100% respectively.

After the sequence of colors, a few options can be provided if necessary:

- `/s` or `/size` To specify the pixel size of the gradient. (Defaults to 512 px)
- `/h` or `/horizontal` To specify that an horizontal gradient should be generated. (This is the default)
- `/v` or `/vertical` To specify that a vertical gradient should be generated.
- `/r` or `/reverse` To specify that the gradient (its color stops) should be reversed.

The last parameter, following the color stop sequence and options, must be the filename.

Examples:

````
tg red yellow red-to-yellow.png

tg "#f00" "#ff0" red-to-yellow-2.png

tg "#ffffff00" "#ffffffff" transparent-to-white.png

tg "#f00" "#ff0" "#0f0" "#0ff" "#00f" "#f0f" "#f00" color-wheel.png

tg black white black black-to-white-to-black.png

tg orange yellow 40% pink 60% red orange-to-red.png

tg blue 30% yellow blue 50% blue-and-yellow.png

tg white black /v vertical-white-to-black.png

tg white black /r /v vertical-black-to-white.png

tg "#ff0" "#f00" /h /s 16 tiny-yellow-to-red.png
````

PS: HTML colors may or may not need to be quoted depending on your shell.

# Thanks

This project was made using `SixLabors.ImageSharp`. Huge thanks to @JimBobSquarePants and the contributors.
