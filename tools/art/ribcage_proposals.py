"""Rib cage art proposals A/B/C: back frame + front ribs, 256x512 each, Kenshi brown/grey, with simple shading."""
import os, sys
from PIL import Image, ImageDraw, ImageFilter

W, H = 256, 512
CX = W // 2
OUT = (28, 24, 20, 255)
RUST = (118, 82, 54, 255)
RUST_HI = (160, 116, 78, 255)
RUST_DK = (70, 48, 33, 255)
GREY = (98, 96, 92, 255)
GREY_HI = (156, 150, 138, 255)
GREY_DK = (60, 58, 55, 255)
YELLOW = (176, 140, 52, 255)


def beam(d, xy0, xy1, w, base, hi):
    d.line([xy0, xy1], fill=OUT, width=w + 4)
    d.line([xy0, xy1], fill=base, width=w)
    off = max(1, w // 4)
    d.line([(xy0[0] - off, xy0[1]), (xy1[0] - off, xy1[1])], fill=hi, width=max(1, w // 3))


def plate(d, box, base, hi, dk):
    d.rectangle(box, fill=base, outline=OUT, width=3)
    x0, y0, x1, y1 = box
    d.line([(x0 + 3, y0 + 3), (x1 - 3, y0 + 3)], fill=hi, width=2)
    d.line([(x0 + 3, y1 - 3), (x1 - 3, y1 - 3)], fill=dk, width=3)


def rivets(d, pts):
    for x, y in pts:
        d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=GREY_HI, outline=OUT)


def rib(d, y, half, depth, base, hi, w, a0=200, a1=340):
    box = (CX - half, y - depth, CX + half, y + depth)
    d.arc(box, a0, a1, fill=OUT, width=w + 4)
    d.arc(box, a0, a1, fill=base, width=w)
    d.arc((box[0] + 1, box[1] - 1, box[2] - 1, box[3] - 1), a0 + 8, a1 - 8, fill=hi, width=max(1, w // 3))


def shadow(im):
    a = im.split()[3].filter(ImageFilter.GaussianBlur(4))
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    sh.putalpha(a.point(lambda v: int(v * 0.35)))
    base = Image.new("RGBA", im.size, (0, 0, 0, 0))
    base.alpha_composite(sh, (4, 5))
    base.alpha_composite(im)
    return base


def base_plate(d):
    plate(d, (CX - 72, 458, CX + 72, 490), GREY, GREY_HI, GREY_DK)
    plate(d, (CX - 84, 484, CX - 52, 502), RUST_DK, RUST, OUT)
    plate(d, (CX + 52, 484, CX + 84, 502), RUST_DK, RUST, OUT)
    rivets(d, [(CX - 60, 474), (CX + 60, 474), (CX, 474)])


def a_cross():
    back = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(back)
    base_plate(d)
    beam(d, (CX, 16), (CX, 468), 20, RUST, RUST_HI)
    rivets(d, [(CX, y) for y in range(40, 460, 34)])
    beam(d, (CX - 104, 114), (CX + 104, 114), 18, RUST, RUST_HI)
    for x in (CX - 96, CX + 80):
        plate(d, (x, 98, x + 16, 132), GREY, GREY_HI, GREY_DK)
    for i, y in enumerate(range(150, 360, 40)):
        rib(d, y, 74 - i * 4, 34, RUST_DK, RUST, 7)
    beam(d, (CX - 72, 140), (CX - 64, 462), 7, GREY, GREY_HI)
    beam(d, (CX + 72, 140), (CX + 64, 462), 7, GREY, GREY_HI)
    front = Image.new("RGBA", (W, H)); f = ImageDraw.Draw(front)
    for i, y in enumerate(range(170, 380, 42)):
        rib(f, y, 78 - i * 4, 40, GREY_HI if i % 2 == 0 else RUST, GREY_HI, 6)
    plate(f, (CX - 7, 150, CX + 7, 380), GREY, GREY_HI, GREY_DK)
    rib(f, 70, 60, 50, RUST, RUST_HI, 6, 190, 350)
    return back, front


def b_gibbet():
    back = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(back)
    base_plate(d)
    # gallows post on the left, arm over the top, cage hanging from a chain
    beam(d, (CX - 84, 470), (CX - 84, 20), 16, RUST, RUST_HI)
    beam(d, (CX - 92, 26), (CX + 30, 26), 14, RUST, RUST_HI)
    beam(d, (CX - 84, 90), (CX - 40, 30), 8, RUST_DK, RUST)
    for y in range(36, 64, 9):
        d.ellipse((CX - 5, y, CX + 5, y + 10), outline=OUT, width=4)
        d.ellipse((CX - 4, y + 1, CX + 4, y + 9), outline=GREY_HI, width=2)
    # hoops behind
    rib(d, 70, 52, 14, GREY_DK, GREY, 6, 180, 360)
    for y in (180, 300, 410):
        rib(d, y, 66, 16, GREY_DK, GREY, 6, 180, 360)
    for x in (CX - 62, CX + 62):
        beam(d, (x, 74), (x, 410), 6, GREY_DK, GREY)
    front = Image.new("RGBA", (W, H)); f = ImageDraw.Draw(front)
    rib(f, 70, 52, 14, GREY, GREY_HI, 6, 0, 180)
    for y in (180, 300, 410):
        rib(f, y, 66, 16, GREY, GREY_HI, 6, 0, 180)
    for x in (CX - 30, CX + 30):
        beam(f, (x, 82), (x, 424), 5, RUST, RUST_HI)
    beam(f, (CX - 60, 64), (CX + 60, 64), 6, RUST, RUST_HI)
    plate(f, (CX - 18, 414, CX + 18, 440), RUST_DK, RUST, OUT)
    return back, front


def c_sarcophagus():
    back = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(back)
    base_plate(d)
    plate(d, (CX - 70, 30, CX + 70, 462), GREY_DK, GREY, OUT)
    for y in range(60, 450, 26):
        d.line([(CX - 62, y), (CX + 62, y)], fill=OUT, width=2)
    beam(d, (CX - 78, 24), (CX - 78, 470), 12, RUST, RUST_HI)
    beam(d, (CX + 78, 24), (CX + 78, 470), 12, RUST, RUST_HI)
    plate(d, (CX - 90, 14, CX + 90, 40), RUST, RUST_HI, RUST_DK)
    d.rectangle((CX - 16, 18, CX + 16, 36), fill=YELLOW, outline=OUT, width=2)
    front = Image.new("RGBA", (W, H)); f = ImageDraw.Draw(front)
    for i, y in enumerate(range(140, 430, 36)):
        rib(f, y, 72, 28 - (i % 2) * 6, RUST if i % 2 else GREY_HI, RUST_HI, 7)
    for x in (CX - 70, CX + 70):
        plate(f, (x - 8, 120, x + 8, 440), RUST_DK, RUST, OUT)
    return back, front


def preview(back, front, path):
    bg = Image.new("RGBA", (W, H), (96, 84, 70, 255))
    bg.alpha_composite(back)
    sk = ImageDraw.Draw(bg)
    sk.ellipse((CX - 26, 48, CX + 26, 104), fill=(208, 206, 196, 255), outline=OUT, width=2)
    sk.rectangle((CX - 22, 106, CX + 22, 290), fill=(196, 194, 184, 255), outline=OUT, width=2)
    sk.rectangle((CX - 40, 110, CX - 26, 250), fill=(190, 188, 178, 255), outline=OUT, width=2)
    sk.rectangle((CX + 26, 110, CX + 40, 250), fill=(190, 188, 178, 255), outline=OUT, width=2)
    bg.alpha_composite(front)
    bg.save(path)
    return bg


out = sys.argv[1]
os.makedirs(out, exist_ok=True)
sheet = Image.new("RGBA", (W * 3 + 40, H + 40), (60, 56, 50, 255))
for i, (name, fn) in enumerate([("a", a_cross), ("b", b_gibbet), ("c", c_sarcophagus)]):
    back, front = fn()
    back = shadow(back)
    back.save(os.path.join(out, "ribcage_%s.png" % name))
    front.save(os.path.join(out, "ribcage_%s_front.png" % name))
    pv = preview(back, front, os.path.join(out, "ribcage_%s_preview.png" % name))
    sheet.alpha_composite(pv, (10 + i * (W + 10), 20))
sheet.save(os.path.join(out, "contact_sheet.png"))
