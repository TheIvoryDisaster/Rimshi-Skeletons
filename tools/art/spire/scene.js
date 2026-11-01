// The chemical spire soft cycle facility (Rimshi_ChemicalSpire, "tempest reliquary"), modelled once and rendered from
// RimWorld's fixed camera. It is not rotatable, so there is a single view. Run with tools/art/spire/render.js.
//
// World units are cells: x east, y up, z south. The building is 3x3. A stepped plinth fills the footprint; on it stands
// a great cylinder of pale First Empire metal ringed with bronze, with a glass viewport on its south face showing dark
// liquid, and an LED panel beside it. A capped cone tops the cylinder and a tall ringed antenna rises from it to a
// glowing tip.
import * as THREE from 'three';

const K = 0.8;          // oblique: a unit of height shows as K units up the screen
export const HALF_X = 1.7;   // texture is 3.4 cells wide...
export const HALF_Y = 2.8;   // ...and 5.6 tall (drawSize (3.4,5.6))...
const OFF = 1.25;       // ...centred 1.25 cells north of the building's centre (drawOffset), room for the antenna
const DEPTH = 10;

const pal = {
  plinth: 0x6e7276,
  plinthTop: 0x858a8e,
  body: 0xb9beb8,
  bodyDark: 0x8d938f,
  bronze: 0x9a7240,
  bronzeDark: 0x6a4c2a,
  trim: 0x3a3f43,
  glassRim: 0x2a2d31,
  liquid: 0x17121f,
  sheen: 0x4a3a6a,
  glint: 0xc8d8e8,
  ledTeal: 0x40e8d0,
  ledAmber: 0xf0a030,
  ledRed: 0xe04838,
  panel: 0x22262a,
  orb: 0x7ad8ff,
  pipe: 0x5a5f63,
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

function mat(color, glow) {
  const m = new THREE.MeshToonMaterial({ color, gradientMap: gradient, side: THREE.DoubleSide });
  if (glow) {
    m.emissive = new THREE.Color(color);
    m.emissiveIntensity = glow;
  }
  m.userData.id = nextId++;
  return m;
}

function box(w, h, d, material, x, y, z) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  m.position.set(x, y, z);
  return m;
}

function cyl(rTop, rBottom, h, material, x, y, z, seg = 40) {
  const m = new THREE.Mesh(new THREE.CylinderGeometry(rTop, rBottom, h, seg), material);
  m.position.set(x, y, z);
  return m;
}

// A curved strip on the cylinder wall: angle 0 faces south (+z), positive angles turn toward east (+x).
function wallPatch(r, centre, width, h, y, material) {
  // three.js cylinder angles start at +z and run toward +x, matching the convention above
  const g = new THREE.CylinderGeometry(r, r, h, 24, 1, true, centre - width / 2, width);
  const m = new THREE.Mesh(g, material);
  m.position.y = y;
  return m;
}

function onWall(r, angle, y, obj) {
  obj.position.set(Math.sin(angle) * r, y, Math.cos(angle) * r);
  obj.rotation.y = angle;
  return obj;
}

export function buildModel() {
  const root = new THREE.Group();
  const m = {};
  for (const [k, v] of Object.entries(pal)) m[k] = mat(v);
  m.liquid = mat(pal.liquid, 0.15);
  m.sheen = mat(pal.sheen, 0.35);
  m.glint = mat(pal.glint, 0.4);
  m.ledTeal = mat(pal.ledTeal, 0.9);
  m.ledAmber = mat(pal.ledAmber, 0.9);
  m.ledRed = mat(pal.ledRed, 0.9);
  m.orb = mat(pal.orb, 1.0);

  // Stepped plinth filling the 3x3 footprint.
  root.add(box(2.92, 0.12, 2.92, m.plinth, 0, 0.06, 0));
  root.add(box(2.6, 0.1, 2.6, m.plinthTop, 0, 0.17, 0));
  for (const [x, z] of [[-1.3, -1.3], [1.3, -1.3], [-1.3, 1.3], [1.3, 1.3]]) {
    root.add(box(0.26, 0.2, 0.26, m.bronzeDark, x, 0.16, z));
  }

  // The cylinder.
  const R = 1.02, BASE = 0.22, H = 1.85;
  root.add(cyl(R + 0.08, R + 0.12, 0.14, m.bronze, 0, BASE + 0.07, 0));
  root.add(cyl(R, R, H, m.body, 0, BASE + H / 2, 0));
  root.add(cyl(R + 0.06, R + 0.06, 0.1, m.bronze, 0, BASE + H - 0.05, 0));
  root.add(cyl(R + 0.04, R + 0.04, 0.06, m.bronzeDark, 0, BASE + 0.55, 0));
  // vertical ribs, leaving the viewport and panel clear
  for (let i = 0; i < 12; i++) {
    const a = (i / 12) * Math.PI * 2 + Math.PI / 12;
    if (Math.abs(Math.atan2(Math.sin(a), Math.cos(a))) < 0.5) continue;
    root.add(onWall(R + 0.025, a, BASE + H / 2, box(0.07, H - 0.1, 0.05, m.bodyDark, 0, 0, 0)));
  }

  // Viewport on the south face: bronze frame, dark rim, the liquid, a lighter sheen near its surface, a glint.
  const VY = BASE + 1.0, VH = 0.95, VW = 0.85;
  root.add(wallPatch(R + 0.03, 0, VW + 0.16, VH + 0.16, VY, m.bronze));
  root.add(wallPatch(R + 0.04, 0, VW + 0.06, VH + 0.06, VY, m.glassRim));
  root.add(wallPatch(R + 0.05, 0, VW, VH, VY, m.liquid));
  root.add(wallPatch(R + 0.055, -0.05, VW * 0.8, 0.05, VY + VH * 0.22, m.sheen));
  root.add(wallPatch(R + 0.055, 0.12, VW * 0.5, 0.03, VY - VH * 0.12, m.sheen));
  root.add(wallPatch(R + 0.06, -0.22, 0.05, VH * 0.75, VY + 0.02, m.glint));
  for (const a of [-VW / 2 - 0.05, VW / 2 + 0.05]) {
    for (const dy of [-VH / 2, VH / 2]) {
      const rivet = new THREE.Mesh(new THREE.SphereGeometry(0.035, 10, 8), m.bronzeDark);
      onWall(R + 0.06, a, VY + dy, rivet);
      root.add(rivet);
    }
  }

  // LED panel to the south-east of the viewport.
  const panel = new THREE.Group();
  panel.add(box(0.44, 0.5, 0.05, m.trim, 0, 0, 0));
  panel.add(box(0.38, 0.44, 0.02, m.panel, 0, 0, 0.03));
  const leds = [m.ledTeal, m.ledTeal, m.ledAmber, m.ledTeal, m.ledRed, m.ledTeal];
  for (let row = 0; row < 4; row++) {
    for (let col = 0; col < 3; col++) {
      panel.add(box(0.07, 0.05, 0.02, leds[(row * 3 + col * 2) % leds.length], -0.11 + col * 0.11, 0.14 - row * 0.1, 0.045));
    }
  }
  panel.add(box(0.3, 0.04, 0.02, m.ledTeal, 0, -0.19, 0.045));
  root.add(onWall(R + 0.06, 0.75, BASE + 1.0, panel));

  // Pipes from the plinth up into the cylinder's west and east sides.
  for (const side of [-1, 1]) {
    const a = side * 1.9;
    const px = Math.sin(a) * (R + 0.18), pz = Math.cos(a) * (R + 0.18);
    root.add(cyl(0.07, 0.07, 0.75, m.pipe, px, BASE + 0.37, pz, 16));
    root.add(cyl(0.1, 0.1, 0.06, m.bronze, px, BASE + 0.76, pz, 16));
    const elbow = box(0.2, 0.12, 0.12, m.pipe, Math.sin(a) * (R + 0.09), BASE + 0.8, Math.cos(a) * (R + 0.09));
    elbow.rotation.y = a + Math.PI / 2;
    root.add(elbow);
  }

  // Cap: a cone narrowing to the antenna's collar.
  const TOP = BASE + H;
  root.add(cyl(0.4, R + 0.02, 0.38, m.bodyDark, 0, TOP + 0.19, 0));
  root.add(cyl(0.42, 0.42, 0.08, m.bronze, 0, TOP + 0.4, 0));
  root.add(cyl(0.22, 0.3, 0.2, m.trim, 0, TOP + 0.54, 0));

  // The antenna: a tapering mast with rings and two crossbars, and a glowing tip.
  const MB = TOP + 0.64, MH = 1.75;
  root.add(cyl(0.05, 0.13, MH, m.bodyDark, 0, MB + MH / 2, 0, 20));
  [0.3, 0.75, 1.15].forEach((v, i) => {
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.2 - i * 0.04, 0.025, 8, 28), m.bronze);
    ring.rotation.x = Math.PI / 2;
    ring.position.set(0, MB + v, 0);
    root.add(ring);
    root.add(cyl(0.09 - i * 0.015, 0.09 - i * 0.015, 0.05, m.bronzeDark, 0, MB + v, 0, 16));
  });
  for (const [v, w] of [[0.52, 0.7], [0.95, 0.46]]) {
    const bar = box(w, 0.03, 0.03, m.trim, 0, MB + v, 0);
    bar.rotation.y = Math.PI / 4;
    root.add(bar);
    for (const s of [-1, 1]) {
      const tip = new THREE.Mesh(new THREE.SphereGeometry(0.035, 10, 8), m.ledTeal);
      tip.position.set(s * Math.cos(Math.PI / 4) * w / 2, MB + v, -s * Math.sin(Math.PI / 4) * w / 2);
      root.add(tip);
    }
  }
  root.add(cyl(0.05, 0.05, 0.06, m.bronze, 0, MB + MH, 0, 16));
  const orb = new THREE.Mesh(new THREE.SphereGeometry(0.13, 20, 14), m.orb);
  orb.position.set(0, MB + MH + 0.13, 0);
  root.add(orb);

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
    1 / HALF_X, 0, 0, 0,
    0, K / HALF_Y, -1 / HALF_Y, -OFF / HALF_Y,
    0, -1 / DEPTH, -K / DEPTH, 0,
    0, 0, 0, 1);
  cam.projectionMatrixInverse.copy(cam.projectionMatrix).invert();
  return cam;
}

export function makeLights(scene) {
  scene.add(new THREE.AmbientLight(0xffffff, 0.55));
  const sun = new THREE.DirectionalLight(0xffffff, 2.4);
  sun.position.set(-0.6, 1.0, 0.6);
  scene.add(sun);
}

// Not rotatable: one view.
export const ROTATIONS = { south: 0 };
