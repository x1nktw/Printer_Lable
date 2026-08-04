#!/usr/bin/env python3
"""Generate B&W silhouette icons for marking labels (128x128, transparent BG)."""
from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(r"d:\Cursor\Printer_Lable\src\LabelPrint.Infrastructure.Printing\Assets\icons")
SIZE = 128
PAD = 10


def blank() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def save(name: str, img: Image.Image) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{name}.png"
    img.save(path)
    print(f"wrote {path}")


def meat() -> None:
    img, d = blank()
    # drumstick silhouette
    d.ellipse((38, 18, 78, 58), fill=(0, 0, 0, 255))
    d.polygon([(55, 50), (78, 100), (98, 92), (72, 48)], fill=(0, 0, 0, 255))
    d.ellipse((88, 88, 112, 112), fill=(0, 0, 0, 255))
    d.ellipse((78, 98, 98, 118), fill=(0, 0, 0, 255))
    save("meat", img)


def vegetables() -> None:
    img, d = blank()
    # leaf
    d.ellipse((28, 24, 100, 108), fill=(0, 0, 0, 255))
    d.polygon([(34, 70), (18, 40), (48, 48)], fill=(0, 0, 0, 255))
    # cut vein (transparent-ish by overdrawing lighter? use subtract via hole)
    # draw white-ish vein as erase: recreate with mask
    mask = Image.new("L", (SIZE, SIZE), 0)
    md = ImageDraw.Draw(mask)
    md.line([(44, 88), (82, 36)], fill=255, width=4)
    md.line([(54, 78), (70, 62)], fill=255, width=3)
    md.line([(58, 86), (78, 74)], fill=255, width=3)
    # convert black leaf with transparent veins
    px = img.load()
    mp = mask.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if mp[x, y] > 128 and px[x, y][3] > 0:
                px[x, y] = (0, 0, 0, 0)
    save("vegetables", img)


def sauce() -> None:
    img, d = blank()
    # bottle
    d.rounded_rectangle((48, 18, 80, 38), radius=4, fill=(0, 0, 0, 255))
    d.rectangle((54, 36, 74, 48), fill=(0, 0, 0, 255))
    d.rounded_rectangle((40, 46, 88, 114), radius=12, fill=(0, 0, 0, 255))
    d.ellipse((52, 58, 76, 78), fill=(0, 0, 0, 0))
    # label band erase
    px = img.load()
    for y in range(62, 76):
        for x in range(48, 80):
            if px[x, y][3] > 0 and 52 <= x <= 76:
                # keep solid bottle; skip erase for thermal solid look
                pass
    save("sauce", img)


def beef() -> None:
    img, d = blank()
    # simplified cow head
    d.ellipse((30, 40, 98, 108), fill=(0, 0, 0, 255))
    d.ellipse((18, 28, 48, 58), fill=(0, 0, 0, 255))
    d.ellipse((80, 28, 110, 58), fill=(0, 0, 0, 255))
    d.ellipse((42, 72, 56, 86), outline=(0, 0, 0, 255), width=3)
    d.ellipse((72, 72, 86, 86), outline=(0, 0, 0, 255), width=3)
    # make eyes as holes
    px = img.load()
    for y in range(74, 84):
        for x in list(range(44, 54)) + list(range(74, 84)):
            px[x, y] = (0, 0, 0, 0)
    d.ellipse((54, 88, 74, 102), fill=(0, 0, 0, 255))
    save("beef", img)


def pork() -> None:
    img, d = blank()
    d.ellipse((28, 42, 100, 112), fill=(0, 0, 0, 255))
    d.ellipse((20, 34, 46, 60), fill=(0, 0, 0, 255))
    d.ellipse((82, 34, 108, 60), fill=(0, 0, 0, 255))
    d.rounded_rectangle((52, 86, 76, 108), radius=8, fill=(0, 0, 0, 255))
    # snout holes
    px = img.load()
    for y in range(92, 102):
        for x in list(range(56, 62)) + list(range(66, 72)):
            px[x, y] = (0, 0, 0, 0)
    # eyes holes
    for y in range(66, 74):
        for x in list(range(48, 56)) + list(range(72, 80)):
            px[x, y] = (0, 0, 0, 0)
    save("pork", img)


def fish() -> None:
    img, d = blank()
    d.ellipse((18, 40, 96, 96), fill=(0, 0, 0, 255))
    d.polygon([(90, 68), (118, 40), (118, 96)], fill=(0, 0, 0, 255))
    px = img.load()
    for y in range(56, 66):
        for x in range(40, 50):
            px[x, y] = (0, 0, 0, 0)
    save("fish", img)


def prepared() -> None:
    img, d = blank()
    # fries cup
    d.polygon([(36, 56), (46, 114), (82, 114), (92, 56)], fill=(0, 0, 0, 255))
    for x0, x1 in [(40, 50), (52, 62), (64, 74), (76, 86)]:
        d.rounded_rectangle((x0, 22, x1, 70), radius=4, fill=(0, 0, 0, 255))
    save("prepared", img)


def frozen() -> None:
    img, d = blank()
    cx = cy = 64
    # snowflake arms
    for ang in range(0, 360, 60):
        import math
        rad = math.radians(ang)
        x2 = cx + int(46 * math.cos(rad))
        y2 = cy + int(46 * math.sin(rad))
        d.line([(cx, cy), (x2, y2)], fill=(0, 0, 0, 255), width=8)
        # side branches
        mx = cx + int(26 * math.cos(rad))
        my = cy + int(26 * math.sin(rad))
        for side in (-1, 1):
            br = rad + side * math.radians(40)
            bx = mx + int(14 * math.cos(br))
            by = my + int(14 * math.sin(br))
            d.line([(mx, my), (bx, by)], fill=(0, 0, 0, 255), width=6)
    d.ellipse((54, 54, 74, 74), fill=(0, 0, 0, 255))
    save("frozen", img)


def icecream() -> None:
    img, d = blank()
    d.ellipse((34, 18, 94, 78), fill=(0, 0, 0, 255))
    d.polygon([(44, 68), (64, 118), (84, 68)], fill=(0, 0, 0, 255))
    save("icecream", img)


def thermometer() -> None:
    img, d = blank()
    d.rounded_rectangle((54, 14, 74, 90), radius=10, fill=(0, 0, 0, 255))
    d.ellipse((42, 78, 86, 122), fill=(0, 0, 0, 255))
    # inner tube hole
    px = img.load()
    for y in range(24, 84):
        for x in range(60, 68):
            if px[x, y][3] > 0:
                px[x, y] = (0, 0, 0, 0)
    # bulb keep solid center partially
    for y in range(90, 110):
        for x in range(54, 74):
            px[x, y] = (0, 0, 0, 255)
    save("thermometer", img)


def chicken() -> None:
    # alias-style drumstick already as meat; add chicken same as meat variant
    meat()
    img = Image.open(OUT / "meat.png")
    save("chicken", img)


def leaf() -> None:
    vegetables()
    img = Image.open(OUT / "vegetables.png")
    save("leaf", img)


def fries() -> None:
    prepared()
    img = Image.open(OUT / "prepared.png")
    save("fries", img)


def snowflake() -> None:
    frozen()
    img = Image.open(OUT / "frozen.png")
    save("snowflake", img)


def bottle() -> None:
    sauce()
    img = Image.open(OUT / "sauce.png")
    save("bottle", img)


def main() -> None:
    meat()
    vegetables()
    sauce()
    beef()
    pork()
    fish()
    prepared()
    frozen()
    icecream()
    thermometer()
    # aliases used in UI
    chicken()
    leaf()
    fries()
    snowflake()
    bottle()


if __name__ == "__main__":
    main()
