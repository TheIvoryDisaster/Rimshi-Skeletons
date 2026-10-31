"""Turns the passes from render.js into RimWorld textures: cartoon outlines (silhouette, part boundaries and sharp
creases), then downscaled. Usage: python3 finish.py <passDir> <outDir> [size]. Writes TuningFork_<rot>.png."""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

OUTLINE = (34, 25, 18)


def load(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.int32)


def edges(color, ids, normal):
    alpha = color[..., 3] > 8
    h, w = alpha.shape
    edge = np.zeros((h, w), bool)
    n = normal[..., :3].astype(np.float32) / 127.5 - 1.0
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-6
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        a = (slice(0, h - dy), slice(max(0, -dx), w - max(0, dx)))
        b = (slice(dy, h), slice(max(0, dx), w - max(0, -dx)))
        idd = np.any(ids[a][..., :3] != ids[b][..., :3], axis=-1)
        sil = alpha[a] != alpha[b]
        crease = (np.sum(n[a] * n[b], axis=-1) < 0.25) & alpha[a] & alpha[b]
        e = idd | sil | crease
        edge[a] |= e
        edge[b] |= e
    return edge


def finish(pass_dir, out_dir, size):
    out_dir.mkdir(parents=True, exist_ok=True)
    for rot in ("south", "east", "north", "west"):
        color = load(pass_dir / f"{rot}_color.png")
        ids = load(pass_dir / f"{rot}_id.png")
        normal = load(pass_dir / f"{rot}_normal.png")
        edge = edges(color, ids, normal)
        big = color.shape[0]
        width = max(1, round(big / size * 1.4))
        mask = Image.fromarray((edge * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(width | 1))
        m = np.asarray(mask) > 0
        out = color.copy()
        out[m, 0], out[m, 1], out[m, 2] = OUTLINE
        out[m, 3] = 255
        img = Image.fromarray(out.astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)
        img.save(out_dir / f"TuningFork_{rot}.png")
        print("wrote", out_dir / f"TuningFork_{rot}.png")


if __name__ == "__main__":
    finish(Path(sys.argv[1]), Path(sys.argv[2]), int(sys.argv[3]) if len(sys.argv) > 3 else 320)
