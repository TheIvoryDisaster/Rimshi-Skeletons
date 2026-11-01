// The brass tuning fork soft cycle facility (Rimshi_TuningFork), modelled once and rendered from RimWorld's fixed
// camera for each rotation, so the four textures always agree. Run with tools/art/tuningfork/render.js.
//
// World units are cells: x east, y up, z south. The building is 1x2; facing south its long side runs north-south.
// A stack of old stereo equipment (speaker cones, VU meters, equaliser sliders, a tape deck at each end joined by a
// band of tape) fills the footprint; the fork stands upright on it in a speaker-cone socket in the middle,
// turned 30 degrees, with ring gears threaded onto both tines that mesh with each other between them.
import * as THREE from 'three';

const K = 0.8;        // oblique: a unit of height shows as K units up the screen (upright things read front-on)
const HALF = 1.6;     // texture is 3.2 cells square (drawSize 3.2)...
const OFF = 0.55;     // ...centred 0.55 cells north of the building's centre (drawOffset), room for the tall fork
const DEPTH = 8;
const YAW = THREE.MathUtils.degToRad(30);

const pal = {
  brass: 0xc9973f,
  gear: 0xa5652f,
  hub: 0xe0b45a,
  case: 0x3d3a37,
  caseTop: 0x4b4743,
  panel: 0x2a2826,
  cone: 0x1d1c1b,
  coneRim: 0x6b655d,
  knob: 0xb8b0a0,
  meter: 0xf0b040,
  led: 0x70e070,
  ledRed: 0xe05040,
  slider: 0xd8d0c0,
  tape: 0x4a2e1c,
  tapeBand: 0x8a5a30,
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
    m.emissiveIntensity = 0.6;
  }
  m.userData.id = nextId++;
  return m;
}

// Fork outline in its own plane: u across, v up (v = 0 at the bottom of the U).
const PRONG_OUT = 0.25, PRONG_IN = 0.1, BEND = 0.25, LEN = 1.35, FORK_T = 0.1;
const TINE_U = (PRONG_OUT + PRONG_IN) / 2;

function forkShape() {
  const s = new THREE.Shape();
  const tipR = (PRONG_OUT - PRONG_IN) / 2;
  s.moveTo(-PRONG_OUT, LEN);
  s.lineTo(-PRONG_OUT, BEND);
  s.absarc(0, BEND, PRONG_OUT, Math.PI, 2 * Math.PI, false);
  s.lineTo(PRONG_OUT, LEN);
  s.absarc(TINE_U, LEN, tipR, 0, Math.PI, false);
  s.lineTo(PRONG_IN, BEND);
  s.absarc(0, BEND, PRONG_IN, 0, Math.PI, true);
  s.lineTo(-PRONG_IN, LEN);
  s.absarc(-TINE_U, LEN, tipR, 0, Math.PI, false);
  return s;
}

// A ring gear seen along its axle: teeth outside, a bore the tine passes through.
function ringGearShape(teeth, rTip, rRoot, bore, phase) {
  const s = new THREE.Shape();
  const steps = teeth * 4;
  for (let i = 0; i <= steps; i++) {
    const a = phase + (i / steps) * Math.PI * 2;
    const r = (i % 4 === 1 || i % 4 === 2) ? rTip : rRoot;
    const x = Math.cos(a) * r, y = Math.sin(a) * r;
    if (i === 0) s.moveTo(x, y); else s.lineTo(x, y);
  }
  const hole = new THREE.Path();
  hole.absarc(0, 0, bore, 0, Math.PI * 2, true);
  s.holes.push(hole);
  return s;
}

function extrude(shape, depth, bevel) {
  const g = new THREE.ExtrudeGeometry(shape, {
    depth, bevelEnabled: bevel > 0, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 2, curveSegments: 24,
  });
  g.translate(0, 0, -depth / 2);
  return g;
}

function box(w, h, d, material, x, y, z) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  m.position.set(x, y, z);
  return m;
}

// A speaker cone facing +z, centred at the origin.
function speaker(r, m) {
  const g = new THREE.Group();
  const rim = new THREE.Mesh(new THREE.CylinderGeometry(r, r, 0.02, 28), m.coneRim);
  rim.rotation.x = Math.PI / 2;
  g.add(rim);
  const cone = new THREE.Mesh(new THREE.CylinderGeometry(r * 0.85, r * 0.3, 0.03, 28), m.cone);
  cone.rotation.x = Math.PI / 2;
  cone.position.z = 0.005;
  g.add(cone);
  const cap = new THREE.Mesh(new THREE.SphereGeometry(r * 0.25, 16, 10), m.coneRim);
  cap.scale.z = 0.5;
  cap.position.z = 0.015;
  g.add(cap);
  return g;
}

// The details of one face of the stereo stack, laid out on a panel facing +z, `width` wide and `height` tall.
function faceplate(width, height, m, long) {
  const g = new THREE.Group();
  g.add(box(width - 0.04, height - 0.04, 0.01, m.panel, 0, 0, 0));
  if (!long) {
    // short (front) face: amplifier with a VU meter and two knobs
    g.add(box(0.26, 0.11, 0.012, m.meter, -0.18, 0.06, 0.006));
    for (const x of [0.12, 0.28]) {
      const k = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.045, 0.04, 16), m.knob);
      k.rotation.x = Math.PI / 2;
      k.position.set(x, 0.06, 0.02);
      g.add(k);
    }
    for (let i = 0; i < 6; i++) g.add(box(0.025, 0.025, 0.012, i < 4 ? m.led : m.ledRed, -0.3 + i * 0.05, -0.08, 0.006));
    return g;
  }
  // long faces: two speakers either end, an equaliser in the middle
  for (const x of [-width / 2 + 0.24, width / 2 - 0.24]) {
    const sp = speaker(Math.min(0.15, height / 2 - 0.04), m);
    sp.position.set(x, 0, 0.006);
    g.add(sp);
  }
  for (let i = 0; i < 7; i++) {
    const x = -0.36 + i * 0.12;
    g.add(box(0.012, height - 0.1, 0.01, m.cone, x, 0, 0.006));
    g.add(box(0.05, 0.03, 0.02, m.slider, x, ((i * 37) % 7 - 3) * 0.025, 0.012));
  }
  return g;
}

export function buildModel() {
  const root = new THREE.Group();
  const m = {
    brass: mat(pal.brass), gear: mat(pal.gear), hub: mat(pal.hub), case: mat(pal.case), caseTop: mat(pal.caseTop),
    panel: mat(pal.panel), cone: mat(pal.cone), coneRim: mat(pal.coneRim), knob: mat(pal.knob),
    meter: mat(pal.meter, true), led: mat(pal.led, true), ledRed: mat(pal.ledRed, true), slider: mat(pal.slider), tape: mat(pal.tape), tapeBand: mat(pal.tapeBand),
  };

  // The stereo stack: a lower amplifier the full footprint and a slimmer deck on top of its north half.
  const W = 0.9, L = 1.88, H = 0.38;
  root.add(box(W, H, L, m.case, 0, H / 2, 0));
  root.add(box(W - 0.06, 0.02, L - 0.06, m.caseTop, 0, H + 0.01, 0));
  const front = faceplate(W, H, m, false);
  front.position.set(0, H / 2, L / 2 + 0.002);
  root.add(front);
  for (const side of [1, -1]) {
    const f = faceplate(L, H, m, true);
    f.rotation.y = side * Math.PI / 2;
    f.position.set(side * (W / 2 + 0.002), H / 2, 0);
    root.add(f);
  }
  const back = faceplate(W, H, m, false);
  back.rotation.y = Math.PI;
  back.position.set(0, H / 2, -L / 2 - 0.002);
  root.add(back);

  // Two tape decks, one each end of the stack, joined by a band of audio tape that runs between their inner reels,
  // past the fork's socket in the middle.
  const DH = 0.16, DL = 0.6, REEL_X = -0.24, REEL_R = 0.1;
  for (const DZ of [-0.6, 0.6]) {
    root.add(box(W - 0.12, DH, DL, m.case, 0, H + DH / 2, DZ));
    root.add(box(W - 0.18, 0.015, DL - 0.06, m.panel, 0, H + DH + 0.008, DZ));
    for (const z of [DZ - 0.13, DZ + 0.13]) {
      const reel = new THREE.Mesh(new THREE.CylinderGeometry(REEL_R, REEL_R, 0.02, 24), m.coneRim);
      reel.position.set(REEL_X, H + DH + 0.02, z);
      root.add(reel);
      const pack = new THREE.Mesh(new THREE.CylinderGeometry(REEL_R * 0.8, REEL_R * 0.8, 0.04, 24), m.tape);
      pack.position.set(REEL_X, H + DH + 0.035, z);
      root.add(pack);
      const spindle = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.07, 12), m.knob);
      spindle.position.set(REEL_X, H + DH + 0.04, z);
      root.add(spindle);
    }
    for (let i = 0; i < 4; i++) root.add(box(0.05, 0.03, 0.08, m.slider, 0.24, H + DH + 0.02, DZ - 0.18 + i * 0.12));
  }
  const tapeFrom = -0.6 + 0.13, tapeTo = 0.6 - 0.13;
  for (const x of [REEL_X + REEL_R * 0.8, REEL_X - REEL_R * 0.8]) {
    // laid flat rather than on edge, so the band reads from above
    root.add(box(0.05, 0.008, tapeTo - tapeFrom, m.tapeBand, x, H + DH + 0.06, 0));
  }

  // The fork's socket in the middle: an upturned speaker cone in a brass collar.
  const SZ = 0;
  const up = speaker(0.26, m);
  up.rotation.x = -Math.PI / 2;
  up.position.set(0, H + 0.02, SZ);
  root.add(up);
  const collar = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.1, 0.12, 20), m.hub);
  collar.position.set(0, H + 0.08, SZ);
  root.add(collar);

  // The fork, upright, in its own frame: plane x-y, stem down into the collar.
  const fork = new THREE.Group();
  const STEM = 0.3;
  fork.add(new THREE.Mesh(extrude(forkShape(), FORK_T, 0.012), m.brass));
  const stem = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.06, STEM, 20), m.brass);
  stem.position.y = -STEM / 2 + 0.02;
  fork.add(stem);
  // Ring gears threaded on each tine, two levels; at each level the pair meshes between the tines.
  const rTip = TINE_U, rRoot = TINE_U - 0.03;
  const teeth = 10;
  [0.6, 1.05].forEach((v, level) => {
    for (const side of [-1, 1]) {
      const phase = side > 0 ? Math.PI / teeth : 0;
      const g = new THREE.Mesh(extrude(ringGearShape(teeth, rTip, rRoot, 0.075, phase + level * 0.2), 0.06, 0.006), m.gear);
      g.rotation.x = Math.PI / 2;
      g.position.set(side * TINE_U, v, 0);
      fork.add(g);
      const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.09, 0.09, 20), m.hub);
      hub.position.set(side * TINE_U, v, 0);
      fork.add(hub);
    }
  });
  fork.rotation.y = YAW;
  fork.position.set(0, H + 0.14 + STEM, SZ);
  root.add(fork);

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
  scene.add(new THREE.AmbientLight(0xffffff, 0.55));
  const sun = new THREE.DirectionalLight(0xffffff, 2.6);
  sun.position.set(-0.6, 1.0, 0.6);
  scene.add(sun);
}

// Rotations: south is the model as built; east turns the socket end to the east, and so on.
export const ROTATIONS = { south: 0, east: Math.PI / 2, north: Math.PI, west: -Math.PI / 2 };
