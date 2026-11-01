"""Rib cage (proposal A, the cross) in RimWorld's furniture view: looked down on almost straight from above, like a
bed, with only a thin strip of each front face showing. The skeleton lies on its back on a cross-shaped board; the
ribs are steel bands across its chest, so from above they read as bowed bars across the width. Draws at 4x and scales
down. Writes the back layer (plate, cross, rails, rib ends), the front layer (rib bands and sternum strap, drawn over
the skeleton) and a preview. Usage: ribcage_topdown.py OUT_DIR [SCALE]"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter

S = 4
W, H = 256 * S, 512 * S
CX = 128
OUT = (30, 25, 21, 255)
RUST = (122, 84, 55, 255)
RUST_HI = (168, 122, 82, 255)
RUST_DK = (74, 50, 34, 255)
GREY = (112, 109, 103, 255)
GREY_HI = (164, 158, 146, 255)
GREY_DK = (66, 63, 59, 255)
PLATE = (88, 85, 80, 255)
PLATE_HI = (118, 114, 107, 255)


def s(*v):
    return tuple(int(round(x * S)) for x in v)


def slab(d, x0, y0, x1, y1, col, hi, dk, edge=3):
    """A flat part seen from above: its top face, a lit top-left rim and a thin dark front edge `edge` px tall."""
    d.rectangle(s(x0, y0, x1, y1 + edge), fill=OUT)
    d.rectangle(s(x0 + 1, y1 - 0.5, x1 - 1, y1 + edge - 1), fill=dk)
    d.rectangle(s(x0 + 1, y0 + 1, x1 - 1, y1 - 1), fill=col)
    d.line([s(x0 + 2, y0 + 2), s(x1 - 2, y0 + 2)], fill=hi, width=S * 2)
    d.line([s(x0 + 2, y0 + 2), s(x0 + 2, y1 - 2)], fill=hi, width=S)


def rivets(d, pts, r=2.6):
    for x, y in pts:
        d.ellipse(s(x - r, y - r, x + r, y + r), fill=GREY_HI, outline=OUT, width=S)
        d.ellipse(s(x - r + 1.2, y - r + 0.8, x, y), fill=(206, 200, 186, 255))


def band(d, y, half, bow, w, col, hi, dk):
    """A rib band across the chest seen from above: a shallow arc bowing toward the feet, thin dark front edge."""
    bb = s(CX - half, y - bow, CX + half, y + bow)
    d.arc((bb[0], bb[1] + 2 * S, bb[2], bb[3] + 2 * S), 0, 180, fill=OUT, width=(w + 4) * S)
    d.arc(bb, 0, 180, fill=OUT, width=(w + 3) * S)
    d.arc((bb[0], bb[1] + S, bb[2], bb[3] + S), 0, 180, fill=dk, width=w * S)
    d.arc(bb, 0, 180, fill=col, width=(w - 1) * S)
    d.arc((bb[0], bb[1] - S, bb[2], bb[3] - S), 25, 155, fill=hi, width=S * 3 // 2)


# (y, half width, bow): from the shoulders down the chest
RIBS = [(132, 74, 16), (160, 76, 17), (188, 75, 17), (216, 71, 16), (244, 64, 14), (270, 55, 12)]


def draw():
    back = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(back)
    # base plate filling the 1x2 footprint
    slab(d, CX - 92, 30, CX + 92, 468, PLATE, PLATE_HI, GREY_DK, 6)
    for x in (CX - 84, CX + 84):
        rivets(d, [(x, y) for y in range(46, 460, 46)])
    # floor grating between the rails
    for y in range(300, 452, 10):
        d.line([s(CX - 70, y), s(CX + 70, y)], fill=GREY_DK, width=S * 2)
    # the cross the skeleton lies on: upright board head to foot, crossbar at the shoulders
    slab(d, CX - 16, 22, CX + 16, 452, RUST, RUST_HI, RUST_DK, 4)
    slab(d, CX - 100, 100, CX + 100, 128, RUST, RUST_HI, RUST_DK, 4)
    for x in (CX - 98, CX + 82):
        slab(d, x, 96, x + 16, 132, GREY, GREY_HI, GREY_DK, 3)
        rivets(d, [(x + 8, 113)])
    rivets(d, [(CX, y) for y in range(40, 446, 30)])
    # halo arc around the head end
    d.arc(s(CX - 62, 8, CX + 62, 112), 195, 345, fill=OUT, width=12 * S)
    d.arc(s(CX - 62, 8, CX + 62, 112), 195, 345, fill=RUST, width=8 * S)
    d.arc(s(CX - 62, 6, CX + 62, 110), 205, 335, fill=RUST_HI, width=2 * S)
    # side rails along the long edges, clamping the rib ends
    for sx in (-1, 1):
        x = CX + sx * 78
        slab(d, x - 6, 120, x + 6, 300, GREY, GREY_HI, GREY_DK, 3)
        for y, half, bow in RIBS:
            rivets(d, [(x, y)], 3)
    front = Image.new("RGBA", (W, H)); f = ImageDraw.Draw(front)
    for i, (y, half, bow) in enumerate(RIBS):
        col, hi, dk = (GREY, GREY_HI, GREY_DK) if i % 2 == 0 else (RUST, RUST_HI, RUST_DK)
        band(f, y, half, bow, 8, col, hi, dk)
    # sternum strap down the middle of the bands
    slab(f, CX - 6, 140, CX + 6, 282, GREY, GREY_HI, GREY_DK, 3)
    rivets(f, [(CX, y + bow) for y, half, bow in RIBS], 2.6)
    return back, front


def ground_shadow(im):
    """Soft shadow down and to the right, like RimWorld's furniture shadows; short, since the frame is low."""
    a = im.split()[3].filter(ImageFilter.GaussianBlur(4 * S))
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    sh.putalpha(a.point(lambda v: int(v * 0.3)))
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.alpha_composite(sh, (3 * S, 4 * S))
    out.alpha_composite(im)
    return out


def small(im, scale):
    return im.resize((256 * scale, 512 * scale), Image.LANCZOS)


def preview(back, front, scale):
    """Back, a stand-in skeleton lying on its back (head up, as RimWorld draws a pawn in a south-facing bed), front."""
    w, h = 256 * scale, 512 * scale
    q = scale
    c = w // 2
    bg = Image.new("RGBA", (w, h), (98, 86, 70, 255))
    bg.alpha_composite(back)
    k = ImageDraw.Draw(bg)
    bone, edge = (212, 210, 200, 255), OUT
    k.ellipse((c - 24 * q, 54 * q, c + 24 * q, 104 * q), fill=bone, outline=edge, width=2 * q)
    k.rounded_rectangle((c - 34 * q, 108 * q, c + 34 * q, 290 * q), 14 * q, fill=(200, 198, 188, 255), outline=edge, width=2 * q)
    for sx in (-1, 1):
        k.rounded_rectangle((c + sx * 44 * q - 8 * q, 116 * q, c + sx * 44 * q + 8 * q, 270 * q), 6 * q, fill=bone, outline=edge, width=2 * q)
    bg.alpha_composite(front)
    return bg


if __name__ == "__main__":
    out = sys.argv[1]
    scale = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    os.makedirs(out, exist_ok=True)
    back, front = draw()
    back = ground_shadow(back)
    b, f = small(back, scale), small(front, scale)
    b.save(os.path.join(out, "ribcage_topdown.png"))
    f.save(os.path.join(out, "ribcage_topdown_front.png"))
    preview(b, f, scale).save(os.path.join(out, "ribcage_topdown_preview.png"))
