// The brass tuning fork soft cycle facility (Rimshi_TuningFork), modelled once and rendered from RimWorld's fixed
// camera for each rotation, so the four textures always agree. Run with tools/art/tuningfork/render.js.
//
// World units are cells: x east, y up, z south. The building is 1x2; facing south its long side runs north-south.
// The fork lies along that long side on a resonator box, stem to the south, prongs to the north, rolled 30 degrees
// onto its side so both its face and its edge show. Three gears sit between the prongs and mesh with each other and
// with teeth cut into the prongs' inner edges.
import * as THREE from 'three';

const K = 0.5;        // oblique: a unit of height shows as K units up the screen (RimWorld's thin front strip)
const HALF = 1.25;    // texture covers -1.25..1.25 cells (drawSize 2.5)
const DEPTH = 6;
const ROLL = THREE.MathUtils.degToRad(30);

const pal = {
  brass: 0xc9973f,
  brassDark: 0xe0b45a,
  bronze: 0xa5652f,
  box: 0x5a4a3c,
  boxTop: 0x6b5a49,
  steel: 0x5e5248,
  rivet: 0xb0a690,
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

function mat(color) {
  const m = new THREE.MeshToonMaterial({ color, gradientMap: gradient, side: THREE.DoubleSide });
  m.userData.id = nextId++;
  return m;
}

// Fork outline in the shape plane: u across, v along the prongs (v = 0 at the bottom of the U).
const PRONG_OUT = 0.31, PRONG_IN = 0.165, BEND = 0.31, LEN = 1.2, FORK_T = 0.09;

function forkShape() {
  const s = new THREE.Shape();
  const tipR = (PRONG_OUT - PRONG_IN) / 2;
  const tipC = (PRONG_OUT + PRONG_IN) / 2;
  s.moveTo(-PRONG_OUT, LEN);
  s.lineTo(-PRONG_OUT, BEND);
  s.absarc(0, BEND, PRONG_OUT, Math.PI, 2 * Math.PI, false);
  s.lineTo(PRONG_OUT, LEN);
  s.absarc(tipC, LEN, tipR, 0, Math.PI, false);
  // inner edge of the right prong, with rack teeth the gears mesh with
  rackEdge(s, PRONG_IN, LEN, BEND + 0.1, -1);
  s.lineTo(PRONG_IN, BEND);
  s.absarc(0, BEND, PRONG_IN, 0, Math.PI, true);
  s.lineTo(-PRONG_IN, BEND + 0.1);
  rackEdge(s, -PRONG_IN, BEND + 0.1, LEN, 1);
  s.lineTo(-PRONG_IN, LEN);
  s.absarc(-tipC, LEN, tipR, 0, Math.PI, false);
  return s;
}

// Little square teeth along a prong's inner edge, from v0 to v1, pointing toward the middle.
function rackEdge(s, u, v0, v1, dir) {
  const pitch = 0.07, depth = 0.022;
  const inward = u > 0 ? -1 : 1;
  const n = Math.floor(Math.abs(v1 - v0 - 0.12) / pitch);
  const start = v0 + dir * 0.06;
  s.lineTo(u, start);
  for (let i = 0; i < n; i++) {
    const a = start + dir * (i * pitch + pitch * 0.25);
    const b = start + dir * (i * pitch + pitch * 0.75);
    s.lineTo(u, a);
    s.lineTo(u + inward * depth, a);
    s.lineTo(u + inward * depth, b);
    s.lineTo(u, b);
  }
}

function gearShape(teeth, rTip, rRoot, phase) {
  const s = new THREE.Shape();
  const steps = teeth * 4;
  for (let i = 0; i <= steps; i++) {
    const a = phase + (i / steps) * Math.PI * 2;
    const r = (i % 4 === 1 || i % 4 === 2) ? rTip : rRoot;
    const x = Math.cos(a) * r, y = Math.sin(a) * r;
    if (i === 0) s.moveTo(x, y); else s.lineTo(x, y);
  }
  const hole = new THREE.Path();
  hole.absarc(0, 0, rRoot * 0.3, 0, Math.PI * 2, true);
  s.holes.push(hole);
  return s;
}

// Shape-plane geometry laid flat: v runs north (-z), thickness up (y), centred on y = 0.
function flat(shape, thickness, bevel) {
  const g = new THREE.ExtrudeGeometry(shape, {
    depth: thickness, bevelEnabled: bevel > 0, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 2, curveSegments: 24,
  });
  g.translate(0, 0, -thickness / 2);
  g.rotateX(-Math.PI / 2);
  return g;
}

export function buildModel() {
  const root = new THREE.Group();
  const m = {
    brass: mat(pal.brass), gear: mat(pal.bronze), hub: mat(pal.brassDark), box: mat(pal.box), boxTop: mat(pal.boxTop),
    steel: mat(pal.steel), rivet: mat(pal.rivet), knob: mat(pal.brass),
  };

  // Resonator box, the footprint less a margin.
  const BOX_W = 0.84, BOX_L = 1.86, BOX_H = 0.16;
  const box = new THREE.Mesh(new THREE.BoxGeometry(BOX_W, BOX_H, BOX_L), m.box);
  box.position.y = BOX_H / 2;
  root.add(box);
  const lid = new THREE.Mesh(new THREE.BoxGeometry(BOX_W - 0.1, 0.02, BOX_L - 0.1), m.boxTop);
  lid.position.y = BOX_H + 0.01;
  root.add(lid);
  // sound hole at the north end, like a tuning fork's resonance box
  const hole = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.11, 0.03, 24), m.steel);
  hole.position.set(0, BOX_H + 0.012, -0.72);
  root.add(hole);
  for (const [x, z] of [[-0.36, -0.86], [0.36, -0.86], [-0.36, 0.86], [0.36, 0.86], [-0.36, 0], [0.36, 0]]) {
    const r = new THREE.Mesh(new THREE.SphereGeometry(0.022, 10, 8), m.rivet);
    r.position.set(x, BOX_H + 0.01, z);
    root.add(r);
  }

  // The fork, built flat in its own frame then rolled onto its side.
  const fork = new THREE.Group();
  fork.add(new THREE.Mesh(flat(forkShape(), FORK_T, 0.012), m.brass));
  // stem out of the bottom of the U, south, ending in a ball
  const STEM = 0.42;
  const stem = new THREE.Mesh(new THREE.CylinderGeometry(0.055, 0.065, STEM, 20), m.brass);
  stem.rotation.x = Math.PI / 2;
  stem.position.z = STEM / 2 - 0.01;
  fork.add(stem);
  const ball = new THREE.Mesh(new THREE.SphereGeometry(0.1, 24, 16), m.knob);
  ball.position.z = STEM + 0.04;
  fork.add(ball);

  const gearT = 0.12, rTip = 0.155, rRoot = 0.122;
  const centres = [0.47, 0.745, 1.02];
  centres.forEach((v, i) => {
    const phase = i % 2 === 0 ? 0 : Math.PI / 9;
    const gear = new THREE.Mesh(flat(gearShape(9, rTip, rRoot, phase), gearT, 0.006), m.gear);
    gear.position.z = -v;
    fork.add(gear);
    const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, gearT + 0.05, 16), m.hub);
    hub.position.z = -v;
    fork.add(hub);
  });

  // Centre the fork lengthwise on the box, roll it 30 degrees, and lift it clear.
  const FORK_TOP_V = LEN + (PRONG_OUT - PRONG_IN) / 2;
  const span = FORK_TOP_V + STEM + 0.14;
  const centreV = (FORK_TOP_V - STEM - 0.14) / 2;
  fork.position.z = centreV + 0.0;
  const rolled = new THREE.Group();
  rolled.add(fork);
  rolled.rotation.z = ROLL;
  const LIFT = BOX_H + 0.25;
  rolled.position.y = LIFT;
  root.add(rolled);

  // Mounts: a clamp holding the ball and a cradle under the bend of the U.
  const ballZ = centreV + STEM + 0.04;
  const clampPost = new THREE.Mesh(new THREE.BoxGeometry(0.2, LIFT - BOX_H, 0.16), m.steel);
  clampPost.position.set(0, BOX_H + (LIFT - BOX_H) / 2, ballZ);
  root.add(clampPost);
  const clamp = new THREE.Mesh(new THREE.TorusGeometry(0.1, 0.03, 10, 24), m.hub);
  clamp.position.set(0, LIFT, ballZ);
  root.add(clamp);
  const bendZ = centreV - BEND * 0.35;
  const cradlePost = new THREE.Mesh(new THREE.BoxGeometry(0.12, LIFT - BOX_H - 0.06, 0.1), m.steel);
  cradlePost.position.set(0, BOX_H + (LIFT - BOX_H - 0.06) / 2, bendZ);
  root.add(cradlePost);

  void span;
  return root;
}

export function makeCamera() {
  const cam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 10);
  cam.matrixAutoUpdate = false;
  cam.matrixWorldAutoUpdate = false;
  cam.matrixWorld.identity();
  cam.matrixWorldInverse.identity();
  // clip x = x, clip y = north + K * up, clip z grows away from a viewer to the south and above.
  cam.projectionMatrix.set(
    1 / HALF, 0, 0, 0,
    0, K / HALF, -1 / HALF, 0,
    0, -1 / DEPTH, -K / DEPTH, 0,
    0, 0, 0, 1);
  cam.projectionMatrixInverse.copy(cam.projectionMatrix).invert();
  return cam;
}

export function makeLights(scene) {
  scene.add(new THREE.AmbientLight(0xffffff, 0.55));
  const sun = new THREE.DirectionalLight(0xffffff, 2.6);
  sun.position.set(-0.6, 1.0, 0.35);
  scene.add(sun);
}

// Rotations: south is the model as built; east turns the stem to the east, and so on.
export const ROTATIONS = { south: 0, east: Math.PI / 2, north: Math.PI, west: -Math.PI / 2 };
