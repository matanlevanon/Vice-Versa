#!/usr/bin/env python3
"""Generates src/ViceVersa/Resources/app.ico.

Design: a rounded blue tile split by a diagonal, with a Latin "A" on one side
and a Hebrew alef on the other, plus a swap arrow. Rendered at 256px and
downsampled into a multi-resolution .ico so it stays legible in the tray.
"""

import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(__file__), "..", "src", "ViceVersa", "Resources", "app.ico")
FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

SIZE = 512
BG_TOP = (37, 99, 235)      # blue 600
BG_BOTTOM = (30, 64, 175)   # blue 800
FG = (255, 255, 255)
ACCENT = (147, 197, 253)    # blue 300


def rounded_gradient(size, radius):
    grad = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / max(size - 1, 1)
        grad.putpixel((0, y), tuple(
            int(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(3)
        ))
    grad = grad.resize((size, size))

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)

    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def main():
    img = rounded_gradient(SIZE, radius=int(SIZE * 0.22))
    draw = ImageDraw.Draw(img)

    big = ImageFont.truetype(FONT_BOLD, int(SIZE * 0.46))

    # Latin A, upper left
    draw.text((SIZE * 0.29, SIZE * 0.30), "A", font=big, fill=FG, anchor="mm")

    # Hebrew alef, lower right
    draw.text((SIZE * 0.71, SIZE * 0.70), "א", font=big, fill=FG, anchor="mm")

    # Diagonal divider
    draw.line(
        [(SIZE * 0.86, SIZE * 0.14), (SIZE * 0.14, SIZE * 0.86)],
        fill=ACCENT,
        width=int(SIZE * 0.035),
    )

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(
        OUT,
        format="ICO",
        sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)],
    )
    print("wrote", os.path.normpath(OUT), os.path.getsize(OUT), "bytes")


if __name__ == "__main__":
    main()
