"""Cuts Matthew's recall array tower painting (white background, thick dark outline) into the game texture.
Background = near-white pixels connected to the border, plus enclosed near-white pockets bigger than POCKET pixels (gaps
seen through the railings). The anti-aliased rim next to the background is un-mixed from white, so the outline keeps a
soft edge with no white halo. The cut-out is cropped to its content, padded to the footprint's 3:5 ratio with the base
on the bottom edge, and scaled to 128 px per cell.
Needs numpy, scipy and Pillow.
Usage: cutout.py SOURCE.png OUT.png [PREVIEW.png]"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

WHITE = 232        # min channel for "background white"
POCKET = 150       # enclosed white areas at least this big are see-through gaps
CELLS = (3, 5)     # footprint, matches drawSize
PX_PER_CELL = 128
MARGIN = 0.03      # fraction of the canvas left clear around the tower


def background(rgb):
    px = np.asarray(rgb, dtype=int)
    white = (px.min(axis=2) >= WHITE) & (px.max(axis=2) - px.min(axis=2) < 18)
    labels, n = ndimage.label(white)
    sizes = ndimage.sum(white, labels, index=np.arange(n + 1))
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
    keep = np.array([i in border or (i > 0 and sizes[i] >= POCKET) for i in range(n + 1)])
    return keep[labels]


def cut(src):
    rgb = src.convert("RGB")
    bg = background(rgb)
    px = np.asarray(rgb, dtype=float)
    alpha = np.where(bg, 0.0, 255.0)
    # rim: opaque pixels within 2 px of the background are a blend of outline and white; un-mix them
    near = np.asarray(Image.fromarray((bg * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
    rim = near & ~bg
    lum = px.mean(axis=2)
    a_rim = np.clip((255.0 - lum) / (255.0 - 30.0), 0, 1)
    alpha[rim] = a_rim[rim] * 255
    out = px.copy()
    unmixed = (px - (1 - a_rim[..., None]) * 255.0) / np.maximum(a_rim[..., None], 1e-3)
    out[rim] = np.clip(unmixed[rim], 0, 255)
    return Image.fromarray(np.dstack([out, alpha]).astype(np.uint8), "RGBA")


def fit(img):
    box = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    img = img.crop(box)
    cw, ch = img.size
    # canvas in source pixels: tower height fills the canvas minus margins, width padded to 3:5
    H = int(round(ch / (1 - 2 * MARGIN)))
    W = int(round(H * CELLS[0] / CELLS[1]))
    if cw > W * (1 - 2 * MARGIN):
        W = int(round(cw / (1 - 2 * MARGIN)))
        H = int(round(W * CELLS[1] / CELLS[0]))
    canvas = Image.new("RGBA", (W, H))
    bottom = int(round(H * MARGIN))
    canvas.alpha_composite(img, ((W - cw) // 2, H - bottom - ch))
    return canvas.resize((CELLS[0] * PX_PER_CELL, CELLS[1] * PX_PER_CELL), Image.LANCZOS)


def preview(tex):
    """The texture over a 3x5 grid of RimWorld-ish dirt, scaled 2x, to check the base sits on the footprint."""
    w, h = tex.size
    pad = PX_PER_CELL
    bgimg = Image.new("RGBA", (w + 2 * pad, h + 2 * pad), (120, 104, 84, 255))
    d = ImageDraw.Draw(bgimg)
    for i in range(0, bgimg.size[0], PX_PER_CELL):
        d.line([(i, 0), (i, bgimg.size[1])], fill=(100, 86, 70, 255))
    for j in range(0, bgimg.size[1], PX_PER_CELL):
        d.line([(0, j), (bgimg.size[0], j)], fill=(100, 86, 70, 255))
    d.rectangle([pad, pad, pad + w - 1, pad + h - 1], outline=(255, 255, 255, 160), width=2)
    bgimg.alpha_composite(tex, (pad, pad))
    return bgimg


if __name__ == "__main__":
    tex = fit(cut(Image.open(sys.argv[1])))
    tex.save(sys.argv[2], optimize=True)
    if len(sys.argv) > 3:
        preview(tex).save(sys.argv[3])
