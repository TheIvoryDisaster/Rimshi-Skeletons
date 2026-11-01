"""Cuts a ComfyUI img2img pass of the rib cage line art back out into game textures.
The img2img run (denoise 0.5 or so) keeps the line art script's layout, so that script's own alpha masks still fit: the
building texture is the painted image inside the frame's silhouette (with the ground shadow redrawn), and the front
texture is the painted image inside the front ribs' mask.
Usage: ribcage_cutout.py PAINTED.png OUT_DIR [LINE_ART_MODULE, default ribcage_topdown]"""
import os, sys
import numpy as np
from PIL import Image, ImageFilter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib

BG = np.array([214, 210, 202], dtype=float)


def mask(alpha, size, grow):
    m = alpha.resize(size, Image.LANCZOS)
    return m.filter(ImageFilter.MaxFilter(grow)) if grow > 1 else m


def cut(painted, alpha_tight, alpha_wide):
    px = np.asarray(painted, dtype=float)
    # pixels in the grown margin keep only what differs from the plain background (the painted outline)
    dist = np.clip((np.linalg.norm(px - BG, axis=2) - 18) * 6, 0, 255)
    a = np.maximum(np.asarray(alpha_tight, dtype=float), np.minimum(np.asarray(alpha_wide, dtype=float), dist))
    rgba = np.dstack([px, a]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


if __name__ == "__main__":
    src, out = sys.argv[1], sys.argv[2]
    r = importlib.import_module(sys.argv[3] if len(sys.argv) > 3 else "ribcage_topdown")
    os.makedirs(out, exist_ok=True)
    painted = Image.open(src).convert("RGB")
    back, front = r.draw()
    whole = Image.new("RGBA", back.size); whole.alpha_composite(back); whole.alpha_composite(front)
    size = painted.size
    building = cut(painted, mask(whole.split()[3], size, 1), mask(whole.split()[3], size, 5))
    ribs = cut(painted, mask(front.split()[3], size, 1), mask(front.split()[3], size, 5))
    final = (256, 512)
    building = r.ground_shadow(building.resize((256 * r.S, 512 * r.S), Image.LANCZOS)).resize(final, Image.LANCZOS)
    ribs = ribs.resize(final, Image.LANCZOS)
    building.save(os.path.join(out, "RibCage.png"))
    ribs.save(os.path.join(out, "RibCage_front.png"))
    r.preview(building, ribs, 1).save(os.path.join(out, "RibCage_preview.png"))
