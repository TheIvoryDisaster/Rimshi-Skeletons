"""Turns the passes from render.js into the cable fissure's textures: cartoon outlines (silhouette, part boundaries and
sharp creases), downscaled, plus a mask per rotation marking the bricks (red) so the stone the fissure is built from
tints them while the cables keep their colours (CutoutComplex). Also writes the heave ability's icon.
Usage: python3 finish.py <passDir> <texturesDir> [size]. Writes Buildings/CableFissure_<rot>.png, _<rot>m.png and
UI/Heave.png."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "tuningfork"))
from finish import OUTLINE, edges, load  # noqa: E402


def finish(pass_dir, tex_dir, size):
    (tex_dir / "Buildings").mkdir(parents=True, exist_ok=True)
    south = None
    for rot in ("south", "east", "north", "west"):
        color = load(pass_dir / f"{rot}_color.png")
        ids = load(pass_dir / f"{rot}_id.png")
        normal = load(pass_dir / f"{rot}_normal.png")
        brick = load(pass_dir / f"{rot}_mask.png")[..., 0] > 127
        edge = edges(color, ids, normal)
        big = color.shape[0]
        width = max(1, round(big / size * 1.4))
        line = np.asarray(Image.fromarray((edge * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(width | 1))) > 0
        out = color.copy()
        out[line, 0], out[line, 1], out[line, 2] = OUTLINE
        out[line, 3] = 255
        img = Image.fromarray(out.astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)
        img.save(tex_dir / "Buildings" / f"CableFissure_{rot}.png")
        # Mask: red where the bricks (and their outlines) are, black on the cables, collars and seams.
        alpha = out[..., 3] > 8
        m = np.zeros(color.shape[:2] + (4,), np.uint8)
        m[..., 3] = 255
        m[brick & alpha, 0] = 255
        Image.fromarray(m, "RGBA").resize((size, size), Image.BILINEAR).save(tex_dir / "Buildings" / f"CableFissure_{rot}m.png")
        print("wrote", rot)
        if rot == "east":
            # The icon is never tinted by stone, so darken its bricks as granite would.
            tinted = out.copy()
            tinted[brick & alpha, :3] = (tinted[brick & alpha, :3] * np.array([0.41, 0.37, 0.38])).astype(np.int32)
            south = Image.fromarray(tinted.astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)

    # Icon: the east view (the bundle running across), cropped to what is drawn, on a warm ring.
    arr = np.asarray(south)
    ys, xs = np.nonzero(arr[..., 3] > 8)
    crop = south.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    side = int(max(crop.size) * 1.25)
    icon = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    yy, xx = np.mgrid[:side, :side]
    r = np.hypot(xx - side / 2, yy - side / 2) / (side / 2)
    band = np.clip(1 - np.abs(r - 0.86) / 0.1, 0, 1)
    ring = np.zeros((side, side, 4), np.uint8)
    ring[..., 0], ring[..., 1], ring[..., 2] = 255, 150, 60
    ring[..., 3] = (band * 200).astype(np.uint8)
    icon.alpha_composite(Image.fromarray(ring, "RGBA"))
    icon.alpha_composite(crop, ((side - crop.size[0]) // 2, (side - crop.size[1]) // 2))
    (tex_dir / "UI").mkdir(parents=True, exist_ok=True)
    icon.resize((128, 128), Image.LANCZOS).save(tex_dir / "UI" / "Heave.png")
    print("wrote", tex_dir / "UI" / "Heave.png")


if __name__ == "__main__":
    finish(Path(sys.argv[1]), Path(sys.argv[2]), int(sys.argv[3]) if len(sys.argv) > 3 else 480)
