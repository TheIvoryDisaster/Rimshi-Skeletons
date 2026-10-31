// The cable fissure soft cycle facility (Rimshi_CableFissure), modelled once and rendered from RimWorld's fixed camera
// for each rotation, so the four textures always agree. Run with tools/art/fissure/render.js.
//
// World units are cells: x east, y up, z south. The building is 2x3; facing south its long side runs north-south.
// Matthew's brief: a series of black bricks of varying heights with a channel of bundled cables passing between them,
// running from one large brick to another. A tall block at the north end and a lower one at the south end (the worker's
// side), rows of smaller bricks of uneven height down both sides, and the bundle running along the channel between
// them from the south block's face to the north block's, held by bands, entering each through a collar with a hot seam.
import * as THREE from 'three';

const K = 0.8;        // oblique: a unit of height shows as K units up the screen (as the tuning fork)
const HALF = 2;       // texture is 4 cells square (drawSize 4)...
const OFF = 0.5;      // ...centred 0.5 cells north of the building's centre (drawOffset), room for the tall north block
const DEPTH = 8;

const pal = {
  brick: 0x5c5c64,   // light on purpose: the stone tint multiplies it down to near black (granite x0.41, slate x0.27)
  brickTop: 0x8e8e98,
  plinth: 0x4a4a50,
  channel: 0x141416,
  collar: 0x5a5650,
  band: 0x9a7240,
  seam: 0xff8a30,
  cables: [0x8a2a20, 0xd06a20, 0xb87333, 0x2f8f8a, 0xc8a040, 0x5a4a8a, 0x3a3a3a],
};

function toonGradient() {
  const data = new Uint8Array([70, 70, 70, 255, 140, 140, 140, 255, 205, 205, 205, 255, 255, 255, 255, 255]);
  const tex = new THREE.DataTexture(data, 4, 1, THREE.RGBAFormat);
  tex.minFilter = tex.magFilter = THREE.NearestFilter;
  tex.needsUpdate = true;
  return tex;
}

const gradient = toonGradient();
let nextId = 1;

// brick: true marks the surfaces the stone the fissure is built from tints (written to the mask pass).
function mat(color, { glow = false, brick = false } = {}) {
  const m = new THREE.MeshToonMaterial({ color, gradientMap: gradient, side: THREE.DoubleSide });
  if (glow) {
    m.emissive = new THREE.Color(color);
    m.emissiveIntensity = 0.9;
  }
  m.userData.id = nextId++;
  m.userData.brick = brick;
  return m;
}

function box(w, h, d, material, x, y, z) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  m.position.set(x, y, z);
  return m;
}

// A brick standing on the plinth: x0..x1 across, z0..z1 deep, h tall, with a slightly smaller cap so the top edge reads.
function brick(group, x0, x1, z0, z1, h, m, base) {
  const w = x1 - x0, d = z1 - z0;
  group.add(box(w, h, d, m.brick, (x0 + x1) / 2, base + h / 2, (z0 + z1) / 2));
  group.add(box(w - 0.06, 0.02, d - 0.06, m.brickTop, (x0 + x1) / 2, base + h + 0.01, (z0 + z1) / 2));
}

export function buildModel() {
  nextId = 1;
  const root = new THREE.Group();
  const m = {
    brick: mat(pal.brick, { brick: true }), brickTop: mat(pal.brickTop, { brick: true }), plinth: mat(pal.plinth, { brick: true }),
    channel: mat(pal.channel), collar: mat(pal.collar), band: mat(pal.band), seam: mat(pal.seam, { glow: true }),
    cables: pal.cables.map(c => mat(c)),
  };

  // Plinth under the whole footprint and the channel sunk down its middle.
  const P = 0.06, CW = 0.24;
  root.add(box(1.94, P, 2.94, m.plinth, 0, P / 2, 0));
  root.add(box(CW * 2, 0.012, 1.7, m.channel, 0, P + 0.006, 0));

  // The two large blocks the cables run between: tall at the north end, lower at the south (worker's) end.
  const NZ0 = -1.45, NZ1 = -0.78, SZ0 = 0.86, SZ1 = 1.45, NH = 1.15, SH = 0.5;
  brick(root, -0.95, 0.95, NZ0, NZ1, NH, m, P);
  brick(root, -0.92, 0.92, SZ0, SZ1, SH, m, P);
  // A step bricked onto the north block's shoulders, so its outline isn't a plain box.
  brick(root, -0.95, -0.45, NZ1, NZ1 + 0.12, 0.7, m, P);
  brick(root, 0.45, 0.95, NZ1, NZ1 + 0.12, 0.82, m, P);

  // Smaller bricks of uneven height down both sides of the channel.
  const rows = {
    [-1]: [[-0.74, -0.36, 0.42], [-0.33, 0.02, 0.24], [0.05, 0.47, 0.62], [0.5, 0.83, 0.3]],
    [1]: [[-0.74, -0.44, 0.3], [-0.41, -0.02, 0.56], [0.01, 0.35, 0.2], [0.38, 0.83, 0.4]],
  };
  for (const side of [-1, 1]) {
    for (const [z0, z1, h] of rows[side]) {
      const xi = CW + 0.02, xo = 0.94;
      brick(root, side > 0 ? xi : -xo, side > 0 ? xo : -xi, z0, z1, h, m, P);
    }
  }
  // Two loose bricks stacked on the rows, half across the gaps.
  brick(root, 0.4, 0.85, -0.2, 0.18, 0.16, m, P + 0.56);
  brick(root, -0.88, -0.5, 0.25, 0.6, 0.14, m, P + 0.62);

  // Hot seams cracking across the big blocks' tops and the tallest bricks.
  root.add(box(0.9, 0.012, 0.035, m.seam, -0.1, P + NH + 0.02, -1.12));
  root.add(box(0.035, 0.012, 0.3, m.seam, 0.33, P + NH + 0.02, -1.25));
  root.add(box(0.5, 0.012, 0.03, m.seam, 0.2, P + SH + 0.02, 1.18));
  root.add(box(0.03, 0.012, 0.25, m.seam, -0.7, P + 0.62 + 0.02, 0.25));

  // Collars where the bundle enters each large block, with a glowing slit around it.
  const NY = P + 0.5, SY = P + 0.28;
  root.add(box(0.44, 0.34, 0.06, m.collar, 0, NY, NZ1 + 0.03));
  root.add(box(0.36, 0.26, 0.02, m.seam, 0, NY, NZ1 + 0.065));
  root.add(box(0.44, 0.3, 0.06, m.collar, 0, SY, SZ0 - 0.03));
  root.add(box(0.36, 0.22, 0.02, m.seam, 0, SY, SZ0 - 0.065));

  // The bundle: strands fanned around a shared path that leaves the south block, sags into the channel and climbs into
  // the north block, each strand wandering a little about its place in the bundle.
  const spine = [
    [0, SY, SZ0 - 0.04], [0, SY - 0.06, SZ0 - 0.3], [0, P + 0.12, 0.35], [0, P + 0.11, -0.1], [0, P + 0.16, -0.5],
    [0, NY - 0.06, NZ1 + 0.18], [0, NY, NZ1 + 0.04],
  ];
  const strands = [[-0.08, 0.03], [0, 0.07], [0.08, 0.03], [-0.05, -0.04], [0.05, -0.04], [0, -0.01], [-0.1, -0.02]];
  strands.forEach(([dx, dy], i) => {
    const pts = spine.map(([x, y, z], j) => {
      const wobble = (j > 0 && j < spine.length - 1) ? Math.sin(i * 1.7 + j * 2.3) * 0.025 : 0;
      return new THREE.Vector3(x + dx + wobble, y + dy, z);
    });
    const curve = new THREE.CatmullRomCurve3(pts);
    const r = i === 5 ? 0.05 : 0.032;
    root.add(new THREE.Mesh(new THREE.TubeGeometry(curve, 80, r, 10, false), m.cables[i]));
  });
  // Bands binding the bundle.
  const path = new THREE.CatmullRomCurve3(spine.map(([x, y, z]) => new THREE.Vector3(x, y, z)));
  for (const t of [0.18, 0.42, 0.62, 0.84]) {
    const p = path.getPointAt(t), tangent = path.getTangentAt(t);
    const band = new THREE.Mesh(new THREE.TorusGeometry(0.1, 0.018, 8, 20), m.band);
    band.position.copy(p);
    band.lookAt(p.clone().add(tangent));
    root.add(band);
  }

  return root;
}

export function makeCamera() {
  const cam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 10);
  cam.matrixAutoUpdate = false;
  cam.matrixWorldAutoUpdate = false;
  cam.matrixWorld.identity();
  cam.matrixWorldInverse.identity();
  // clip x = x, clip y = north + K * up - OFF, clip z grows away from a viewer to the south and above.
  cam.projectionMatrix.set(
    1 / HALF, 0, 0, 0,
    0, K / HALF, -1 / HALF, -OFF / HALF,
    0, -1 / DEPTH, -K / DEPTH, 0,
    0, 0, 0, 1);
  cam.projectionMatrixInverse.copy(cam.projectionMatrix).invert();
  return cam;
}

export function makeLights(scene) {
  scene.add(new THREE.AmbientLight(0xffffff, 0.6));
  const sun = new THREE.DirectionalLight(0xffffff, 2.6);
  sun.position.set(-0.4, 1.0, 0.3);
  scene.add(sun);
}

// Rotations: south is the model as built (the low block and the worker's side to the south); east turns it to the east.
export const ROTATIONS = { south: 0, east: Math.PI / 2, north: Math.PI, west: -Math.PI / 2 };
