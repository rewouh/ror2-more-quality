"""Draws icon.png (256x256, required by Thunderstore): four quality diamonds around a star.

Run:  python tools/make_icon.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

SIZE = 256
SCALE = 4   # drawn large, then downsampled for smooth edges
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")

# Quality's tier colours: Uncommon, Rare, Epic, Legendary
TIERS = [(110, 220, 120), (90, 160, 255), (190, 110, 255), (255, 175, 50)]


def diamond(draw, cx, cy, r, fill, outline):
    points = [(cx, cy - r), (cx + r * 0.72, cy), (cx, cy + r), (cx - r * 0.72, cy)]
    draw.polygon(points, fill=fill, outline=outline, width=int(r * 0.12))


def star(cx, cy, outer, inner, points=5):
    result = []
    for i in range(points * 2):
        r = outer if i % 2 == 0 else inner
        a = math.pi / points * i - math.pi / 2
        result.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return result


def main():
    s = SIZE * SCALE
    image = Image.new("RGBA", (s, s))
    draw = ImageDraw.Draw(image)

    # Night-sky background with a soft centre glow.
    for y in range(s):
        t = y / s
        draw.line([(0, y), (s, y)], fill=(int(18 + 14 * t), int(14 + 8 * t), int(40 + 22 * t)))
    glow = Image.new("RGBA", (s, s))
    ImageDraw.Draw(glow).ellipse([s * 0.18, s * 0.18, s * 0.82, s * 0.82], fill=(120, 90, 255, 110))
    image = Image.alpha_composite(image, glow.filter(ImageFilter.GaussianBlur(s * 0.08)))
    draw = ImageDraw.Draw(image)

    c = s / 2
    # Four tier diamonds on the diagonals.
    for i, colour in enumerate(TIERS):
        a = math.pi / 4 + math.pi / 2 * i + math.pi
        x, y = c + math.cos(a) * s * 0.31, c + math.sin(a) * s * 0.31
        darker = tuple(int(v * 0.45) for v in colour)
        diamond(draw, x, y, s * 0.13, colour, darker)

    # Central star.
    draw.polygon(star(c, c + s * 0.01, s * 0.21, s * 0.09), fill=(255, 236, 170), outline=(150, 100, 30), width=int(s * 0.012))

    image = image.resize((SIZE, SIZE), Image.LANCZOS)
    image.save(os.path.join(ROOT, "icon.png"))
    print("icon.png written")


if __name__ == "__main__":
    main()
