"""Turns the passes from render.js into the chemical spire's texture: cartoon outlines (silhouette, part boundaries
and sharp creases), then downscaled. Usage: python3 finish.py <passDir> <texturesDir> [width]. Writes
Buildings/ChemicalSpire.png (one view, the building is not rotatable) and UI/LooseTheTempest.png (the burst's icon)."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "tuningfork"))
from finish import OUTLINE, edges, load  # noqa: E402


def finish(pass_dir, tex_dir, width):
    color = load(pass_dir / "south_color.png")
    ids = load(pass_dir / "south_id.png")
    normal = load(pass_dir / "south_normal.png")
    edge = edges(color, ids, normal)
    h, w = color.shape[:2]
    line = max(1, round(w / width * 1.4))
    mask = np.asarray(Image.fromarray((edge * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(line | 1))) > 0
    out = color.copy()
    out[mask, 0], out[mask, 1], out[mask, 2] = OUTLINE
    out[mask, 3] = 255
    img = Image.fromarray(out.astype(np.uint8), "RGBA")
    height = round(h * width / w)
    (tex_dir / "Buildings").mkdir(parents=True, exist_ok=True)
    img.resize((width, height), Image.LANCZOS).save(tex_dir / "Buildings" / "ChemicalSpire.png")
    print("wrote", tex_dir / "Buildings" / "ChemicalSpire.png", width, "x", height)

    # Icon: the whole spire, cropped to what is drawn, centred on a square, with a pale electric ring behind it.
    ys, xs = np.nonzero(out[..., 3] > 8)
    crop = img.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    side = max(crop.size) + 40
    icon = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    ring = np.zeros((side, side, 4), np.uint8)
    yy, xx = np.mgrid[:side, :side]
    r = np.hypot(xx - side / 2, yy - side / 2) / (side / 2)
    band = np.clip(1 - np.abs(r - 0.82) / 0.1, 0, 1)
    ring[..., 0], ring[..., 1], ring[..., 2] = 120, 216, 255
    ring[..., 3] = (band * 200).astype(np.uint8)
    icon.alpha_composite(Image.fromarray(ring, "RGBA"))
    icon.alpha_composite(crop, ((side - crop.size[0]) // 2, (side - crop.size[1]) // 2))
    (tex_dir / "UI").mkdir(parents=True, exist_ok=True)
    icon.resize((128, 128), Image.LANCZOS).save(tex_dir / "UI" / "LooseTheTempest.png")
    print("wrote", tex_dir / "UI" / "LooseTheTempest.png")


if __name__ == "__main__":
    finish(Path(sys.argv[1]), Path(sys.argv[2]), int(sys.argv[3]) if len(sys.argv) > 3 else 408)
