"""Rib cage (proposal A, the cross) redrawn in RimWorld's oblique view: seen from the south and above, so top faces
show as foreshortened strips over front faces, ribs read as hoops wrapping toward the viewer, and the frame casts a
ground shadow. Draws at 4x and scales down. Writes the back layer (frame, back halves of the ribs), the front layer
(front halves of the ribs, drawn over the hanging skeleton), a mask of the front layer and a preview.
Usage: ribcage_oblique.py OUT_DIR [SCALE]"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter

S = 4
W, H = 256 * S, 512 * S
CX = W // 2
OUT = (30, 25, 21, 255)
RUST = (122, 84, 55, 255)
RUST_HI = (168, 122, 82, 255)
RUST_TOP = (150, 108, 72, 255)
RUST_DK = (74, 50, 34, 255)
GREY = (104, 101, 96, 255)
GREY_HI = (162, 156, 144, 255)
GREY_TOP = (134, 130, 122, 255)
GREY_DK = (62, 60, 56, 255)


def s(*v):
    return tuple(int(x * S) for x in v)


def box(d, x0, y0, x1, y1, top, front, top_col, front_col, dk, hi=None):
    """A block seen obliquely: a top face `top` px deep above a front face."""
    x0, y0, x1, y1, top = (v * S for v in (x0, y0, x1, y1, top))
    d.rectangle((x0, y0, x1, y0 + top), fill=top_col, outline=OUT, width=3 * S // 2)
    d.rectangle((x0, y0 + top, x1, y1), fill=front_col, outline=OUT, width=3 * S // 2)
    d.line([(x0 + 2 * S, y1 - 2 * S), (x1 - 2 * S, y1 - 2 * S)], fill=dk, width=3 * S)
    if hi:
        d.line([(x0 + 2 * S, y0 + top + 2 * S), (x0 + 2 * S, y1 - 3 * S)], fill=hi, width=2 * S)


def rivets(d, pts, r=3):
    for x, y in pts:
        d.ellipse(s(x - r, y - r, x + r, y + r), fill=GREY_HI, outline=OUT, width=S)
        d.ellipse(s(x - r + 2, y - r + 1, x, y - 1), fill=(200, 194, 180, 255))


def hoop(d, y, half, depth, w, col, hi, dk, front):
    """Half a rib hoop: the front (lower, toward the viewer) or back (upper) half of an ellipse."""
    bb = s(CX / S - half, y - depth, CX / S + half, y + depth)
    a0, a1 = (0, 180) if front else (180, 360)
    d.arc(bb, a0, a1, fill=OUT, width=(w + 4) * S)
    d.arc(bb, a0, a1, fill=col, width=w * S)
    if front:
        # light on the upper edge of the rib, shade underneath
        d.arc((bb[0], bb[1] - 2 * S, bb[2], bb[3] - 2 * S), a0 + 12, a1 - 12, fill=hi, width=2 * S)
        d.arc((bb[0], bb[1] + 2 * S, bb[2], bb[3] + 2 * S), a0 + 20, a1 - 20, fill=dk, width=S * 3 // 2)
    else:
        d.arc((bb[0], bb[1] + S, bb[2], bb[3] + S), a0 + 15, a1 - 15, fill=dk, width=2 * S)


RIBS = [(150, 70, 22), (186, 72, 23), (222, 70, 23), (258, 66, 22), (294, 60, 20), (328, 52, 18)]


def draw():
    back = Image.new("RGBA", (W, H)); d = ImageDraw.Draw(back)
    # base plate: wide top face, front face below, rust skids
    box(d, CX / S - 76, 440, CX / S + 76, 496, 30, None, GREY_TOP, GREY, GREY_DK, GREY_HI)
    rivets(d, [(CX / S - 64, 455), (CX / S + 64, 455), (CX / S - 64, 485), (CX / S + 64, 485)])
    box(d, CX / S - 86, 484, CX / S - 54, 504, 6, None, RUST_TOP, RUST_DK, OUT)
    box(d, CX / S + 54, 484, CX / S + 86, 504, 6, None, RUST_TOP, RUST_DK, OUT)
    # spine post standing at the back of the plate, square, cap on top
    box(d, CX / S - 11, 14, CX / S + 11, 462, 8, None, RUST_TOP, RUST, RUST_DK, RUST_HI)
    rivets(d, [(CX / S, y) for y in range(46, 450, 34)], 2.5)
    # back halves of the ribs, behind the post's front face
    for y, half, depth in RIBS:
        hoop(d, y, half, depth, 7, RUST_DK, RUST, OUT, False)
    # spine stays in front of the back halves
    box(d, CX / S - 11, 130, CX / S + 11, 462, 0, None, RUST_TOP, RUST, RUST_DK, RUST_HI)
    rivets(d, [(CX / S, y) for y in range(148, 450, 34)], 2.5)
    # crossbar at the shoulders, top face showing
    box(d, CX / S - 106, 100, CX / S + 106, 124, 7, None, RUST_TOP, RUST, RUST_DK, RUST_HI)
    for x in (CX / S - 98, CX / S + 82):
        box(d, x, 94, x + 16, 132, 5, None, GREY_TOP, GREY, GREY_DK, GREY_HI)
        rivets(d, [(x + 8, 116)], 2.5)
    # side rails from the crossbar ends down to the plate, leaning in
    for sx in (-1, 1):
        x0, x1 = CX / S + sx * 74, CX / S + sx * 64
        d.line([s(x0, 128), s(x1, 452)], fill=OUT, width=11 * S)
        d.line([s(x0, 128), s(x1, 452)], fill=GREY, width=7 * S)
        d.line([s(x0 - 2, 130), s(x1 - 2, 450)], fill=GREY_HI, width=2 * S)
        box(d, x1 - 7, 446, x1 + 7, 458, 4, None, GREY_TOP, GREY_DK, OUT)
    # gibbet hook and halo arc at the top
    d.arc(s(CX / S - 58, 8, CX / S + 58, 92), 200, 340, fill=OUT, width=11 * S)
    d.arc(s(CX / S - 58, 8, CX / S + 58, 92), 200, 340, fill=RUST, width=7 * S)
    d.arc(s(CX / S - 58, 6, CX / S + 58, 90), 210, 330, fill=RUST_HI, width=2 * S)
    front = Image.new("RGBA", (W, H)); f = ImageDraw.Draw(front)
    for i, (y, half, depth) in enumerate(RIBS):
        col, hi = (GREY, GREY_HI) if i % 2 == 0 else (RUST, RUST_HI)
        hoop(f, y, half, depth, 7, col, hi, RUST_DK if i % 2 else GREY_DK, True)
    # sternum strap holding the front ribs together
    f.line([s(CX / S, 164), s(CX / S, 354)], fill=OUT, width=12 * S)
    f.line([s(CX / S, 164), s(CX / S, 354)], fill=GREY, width=8 * S)
    f.line([s(CX / S - 2, 166), s(CX / S - 2, 352)], fill=GREY_HI, width=2 * S)
    rivets(f, [(CX / S, y + depth) for y, half, depth in RIBS], 3)
    return back, front


def ground_shadow(im):
    """Soft shadow cast down and to the right, the way RimWorld's sun shadows fall."""
    a = im.split()[3].filter(ImageFilter.GaussianBlur(5 * S))
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    sh.putalpha(a.point(lambda v: int(v * 0.32)))
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.alpha_composite(sh, (5 * S, 6 * S))
    out.alpha_composite(im)
    return out


def small(im, scale):
    return im.resize((256 * scale, 512 * scale), Image.LANCZOS)


def preview(back, front, scale):
    w, h = 256 * scale, 512 * scale
    bg = Image.new("RGBA", (w, h), (98, 86, 70, 255))
    bg.alpha_composite(back)
    k = ImageDraw.Draw(bg)
    c = w // 2
    q = scale
    k.ellipse((c - 26 * q, 46 * q, c + 26 * q, 100 * q), fill=(210, 208, 198, 255), outline=OUT, width=2 * q)
    k.rectangle((c - 22 * q, 104 * q, c + 22 * q, 300 * q), fill=(198, 196, 186, 255), outline=OUT, width=2 * q)
    for sx in (-1, 1):
        k.rectangle((c + sx * 33 * q - 7 * q, 108 * q, c + sx * 33 * q + 7 * q, 262 * q), fill=(190, 188, 178, 255), outline=OUT, width=2 * q)
    bg.alpha_composite(front)
    return bg


if __name__ == "__main__":
    out = sys.argv[1]
    scale = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    os.makedirs(out, exist_ok=True)
    back, front = draw()
    back = ground_shadow(back)
    b, f = small(back, scale), small(front, scale)
    b.save(os.path.join(out, "ribcage_oblique.png"))
    f.save(os.path.join(out, "ribcage_oblique_front.png"))
    f.split()[3].save(os.path.join(out, "ribcage_oblique_front_mask.png"))
    preview(b, f, scale).save(os.path.join(out, "ribcage_oblique_preview.png"))
