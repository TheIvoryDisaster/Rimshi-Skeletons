# Art generation catalog

How every piece of Rimshi Skeletons art was made, so a style that worked can be reused and an asset can be redone.
Every new art job adds its entry here (see "Adding an entry" at the end). The full prompt of every variant ever
generated, picked or not, is in [prompt-log.md](prompt-log.md).

## Quick pick: which recipe for which job

| Job | Use | Why |
| --- | --- | --- |
| A new item icon (64x64) | Recipe A: Flux Kontext, blockout or reference + vanilla style sheet | Reads well at 64 px, matches vanilla items, 3 variants in about 4 minutes. |
| Rotatable furniture with a simple shape (bed, bench) | Recipe A, one blockout per facing, vanilla building of the same facing as style image | Keeps the three views consistent because every view starts from a blockout of the same layout. |
| A shape the models cannot draw from text (rib cage cross, gibbet) | Recipe B: code-drawn line art, painted by SDXL img2img at denoise 0.5 to 0.55 | Text-to-image never produced usable ribs; the line art fixes the shape and the perspective. |
| A complex machine that needs exact matching rotations | Recipe C: three.js model, toon shading, fixed RimWorld camera, outline pass | All four rotations come from one model, so they always agree. No ComfyUI needed. |
| A placeholder | Recipe D: recolour a vanilla texture | Fast and always in style. |
| Art Matthew supplies on a plain background | Recipe E: cut-out script | Keeps his drawing as is; only the background, crop and padding change. |

## The target: what RimWorld art looks like

- **Perspective.** RimWorld furniture is seen almost straight from above, with only a thin strip of each south-facing
  front face showing; tall parts reach up the screen. Matthew corrected this twice (the rib cage "oblique" redraw read
  as a front view), so check any new building against `Textures/Rimshi/Buildings/RepairBed_south.png` before showing
  it. Items are the same: mostly the top, a sliver of the front. Calling it "oblique" or "three-quarter" in a prompt
  makes models tilt too far; say "seen from above, a thin strip of the front face".
- **Style.** Thick smooth dark outline around the silhouette, flat muted colours, soft simple shading with light from
  the top left, no ground shadow (the game draws its own), transparent background.
- **Palettes used so far.** Kenshi rust brown and dull grey steel (bed, rib cage, workbench); matte black with
  caution yellow (motor, robotic component, skeleton muscle, placeholder intermediates); brown with grey trim (limb
  placeholders); brass and dark stereo equipment (tuning fork); pale First Empire metal, bronze and teal LEDs (spire).
- **Facings.** `Graphic_Multi` needs `_south`, `_east`, `_north` (west mirrors east unless a `_west` is given). For a
  workbench in its default rotation the side the crafter works from is `_north` in RimWorld's naming, which tripped up
  the workbench prompts (see below).

### Sizes in the game today

| Texture | File size (px) | drawSize | Graphic |
| --- | --- | --- | --- |
| Items (repair kit, motor, robotic component, skeleton muscle, placeholders) | 64x64 | 0.8 to 0.85 | Single |
| RepairBed_south/east/north | 256x256 (bed drawn 128x256 inside, padded) | (2.4,2.4) | Multi |
| SkeletonWorkbench_south/east/north | 256x256 | (2,2) | Multi |
| RibCage, RibCage_front | 256x512 each (front ribs drawn over the pawn) | (1.25,2.5) | Single |
| TuningFork_south/east/north/west | 384x384 | (3.2,3.2), drawOffset 0.55 north | Multi |
| ChemicalSpire | 408x672 | (3.4,5.6), drawOffset 1.25 north | Single |
| CableFissure_south/east/north/west (+ `_<rot>m` masks) | 480x480 | (4,4), drawOffset 0.5 north | Multi, CutoutComplex |

## The machine

All generation runs locally on Matthew's PC (DESKTOP-ESRRCQ3). No paid partner or API models have been used.

- **GPU:** NVIDIA GeForce RTX 4070 Ti SUPER, 16 GB VRAM; 64 GB RAM.
- **ComfyUI:** core 0.34.5 at `D:\StableDiffusion\ComfyUI\ComfyUI\ComfyUI`, driven by comfy-cli 1.20.0 through the
  comfy-mcp tools. Custom node packs installed: comfyui-manager, comfyui-inspire-pack, comfyui_controlnet_aux,
  comfyui_instantid, comfyui_ipadapter_plus (none of them are used by the recipes below; all nodes used are core).
- **Models** (file names exactly as the workflows reference them):

| Model | Type | Used by |
| --- | --- | --- |
| `flux1-dev-kontext_fp8_scaled.safetensors` | Flux Kontext dev, fp8 (diffusion_models, UNETLoader) | Recipe A |
| `clip_l.safetensors` + `t5xxl_fp8_e4m3fn_scaled.safetensors` | Flux text encoders (DualCLIPLoader, type flux) | Recipe A |
| `ae.safetensors` | Flux VAE | Recipe A |
| `waiIllustriousSDXL_v150.safetensors` | SDXL anime/illustration checkpoint | Recipe B (picked) |
| `sd_xl_base_1.0.safetensors` | SDXL base | Recipe B (tried, worse) |
| `rimworld_items_20261013_0803.safetensors` | SDXL LoRA trained on RimWorld item art on 2026-10-13 (about 1200 steps; the training output is `output\loras\rimworld_items_20261013_0803_1200_steps_00001_.safetensors`) | Recipe B, strength 0.8 model and clip |

### Driving ComfyUI from a cloud thread

1. Start ComfyUI with `launch_comfyui` if `server_info` says it is not running (the rib cage's first attempt failed
   because it was not up; the retry worked).
2. Write the API-format workflow JSON into `<save data folder>\RimshiArt\` with `device_commit_files` (stage it under
   `/mnt/project-files/` first).
3. Put every input image (blockout, line art, style reference) into ComfyUI with `upload_file`; `LoadImage` reads
   from ComfyUI's input folder by bare file name.
4. `run_workflow` with `wait=false`, then poll `job` with a wait of 50 s or less (longer calls time out), then
   `fetch_outputs` into a `RimshiArt\<job>` folder and `device_stage_files` the PNGs back to the container.
5. Every PNG ComfyUI saves carries its full API workflow in the `prompt` text chunk, so the raw outputs in
   `output\rimworld\` and `output\` are the record of what was run. Keep them; this catalog was rebuilt from them.

Rounds 1 and 2 of the kit and bed, the workbench and the intermediates were run by a Remote Control session on the
PC instead, with the same models; their blockout and post-processing scripts stayed on the PC session and were not
saved, so only the ComfyUI side of those is reproducible exactly (see Lessons).

## Recipe A: Flux Kontext, blockout plus style reference

Used for: repair kit, repair bed, skeleton workbench, motor, robotic component, skeleton muscle.

**Idea.** Flux Kontext is an image editing model. Give it two images: image 1 is what to draw (a flat-colour blockout
of the exact layout, or a reference picture of the object), image 2 is a vanilla RimWorld texture whose style to copy.
The prompt says "redraw the first image in the art style of the second". The sampler starts from image 1's latent,
so with denoise below 1 the layout and camera of the blockout survive.

**Graph** (core nodes only; saved copies in `tools/art/comfy/kontext_*.json`):

```
UNETLoader(flux1-dev-kontext_fp8_scaled) ............................ model
DualCLIPLoader(clip_l, t5xxl_fp8_e4m3fn_scaled, flux) -> CLIPTextEncode(prompt) = C
VAELoader(ae)
LoadImage(image 1) -> FluxKontextImageScale -> VAEEncode = L1
LoadImage(image 2) -> FluxKontextImageScale -> VAEEncode = L2
ReferenceLatent(C, L1) -> ReferenceLatent(.., L2) -> FluxGuidance(2.5) = positive
ConditioningZeroOut(C) = negative
RepeatLatentBatch(L1, 2) = latent (two candidates per run)
KSampler(seed, steps 20, cfg 1.0, euler, simple, denoise) -> VAEDecode -> SaveImage
```

There is no negative prompt in Flux (cfg 1.0, zeroed conditioning); everything unwanted goes into the positive prompt
as "no text, no person, ..." clauses.

**Settings that worked.** steps 20, cfg 1.0, sampler `euler`, scheduler `simple`, FluxGuidance 2.5. Output size is
whatever FluxKontextImageScale picks for the input aspect: 1024x1024 for square, 720x1456 for a 1x2 bed upright,
1456x720 for its side view. Seeds were set per variant (7100+, 9100+, 12100+, 4197 to 4199, 4300+).

**Denoise.**
- 1.0: free redraw. Fine for round 1 proposals and for items from a Kenshi reference picture, but the model changes
  the camera and proportions, and the three bed views stopped agreeing.
- 0.9 to 0.93: keeps the blockout's layout, camera and parts exactly while still repainting it in the vanilla style.
  This is the setting for multi-view furniture (workbench 0.9, final bed pass 0.93).
- 0.6 to 0.85: tested on the bed and kit (`rw2t_*`); too much of the flat blockout shows through.

**Prompt template.** Build the prompt from these blocks, in this order (wording copied from the prompts that worked):

1. Task: `Redraw the first image, a flat blockout of <object>, as a finished RimWorld <item | furniture | building>
   texture in the art style of the second image (<a vanilla RimWorld X seen from the same side>):`
   For an item from a photo-like reference: `Redraw the first image, <object>, as a hand-painted 2D game sprite, not a
   3D render, a RimWorld item icon in the art style of the second image (vanilla RimWorld item textures):`
2. Style: `thick smooth black outline, flat muted colours, soft simple shading with light from the top left, clean
   vector-like painted look.` For items add `clean cartoon vector look like a sticker with a heavy black outline around
   the whole silhouette` (without it the first motor came out as a 3D render).
3. Layout for this view, in words: where each part sits in the frame, for example `The machine with three gauges and
   a green status bar is at the top, the roller deck fills the rest, and a thin strip of the foot end's face is at the
   bottom.`
4. Lock: `Keep exactly the blockout's layout, proportions, colours and viewpoint: orthographic, every edge straight
   and axis-aligned, not rotated, no perspective distortion, nothing moved or added.` (For items: `the case is
   perfectly level and symmetrical, its front edge horizontal, not rotated, not tilted sideways`.)
5. What may be added: `Add only soft shading, small highlights, bolts and panel lines.`
6. Exclusions: `One object only, plain flat light grey background, no shadow on the ground, no text` plus per asset
   `no red cross`, `no legs, no feet, no mattress, no blanket, no pillow, no person`, `no hands, no other objects`.
7. Colour scheme, when it is not already in the blockout: `Colour scheme: matte black and dark charcoal metal with
   caution-yellow accents.`

**Style reference images** (all in ComfyUI's input folder on the PC; the vanilla ones were extracted from the game's
textures):

| Input name | What it is | Used for |
| --- | --- | --- |
| `rwref_medind.png` | vanilla industrial medicine | repair kit rounds 1 and 2 |
| `rwref_hospbed.png` | vanilla hospital bed, south | repair bed round 1 |
| `rwref_hospbed_south/east/north.png` | vanilla hospital bed, each facing | repair bed round 2 (match the facing!) |
| `wbref_smithy_south/east/north.png` | vanilla electric smithy, each facing | workbench |
| `itref_style.png` | a sheet of vanilla item textures | motor, robotic component, skeleton muscle |
| `itref_motor.png`, `itref_robotic.png`, `itref_muscle.png` | the Kenshi item icons from kenshi.fandom.com/wiki/Robotics_Components (motor, robotics components, skeleton muscle) | image 1 for the three intermediates |
| `rw2ref_kit1.png`, `rw2ref_bed2.png` | Matthew's round 1 picks, used as style image | round 2 first pass (worse than the vanilla refs) |

The Kenshi skeleton repair bed itself came from the Kenshi wiki icon (the only picture found); it informed the round 1
bed blockouts.

**Post-processing** (done on the PC; the scripts were not saved): remove the light grey background to transparency,
check for white fringes by compositing on magenta and on black, make the opening inside the kit's carry handle
transparent, downscale with Lanczos (items to 64x64, beds to 128x256 then padded to 256x256 for drawSize (2.4,2.4),
workbench to 256x256), and keep a 4x preview of each. Contact sheets on mid grey for picking.

## Recipe B: SDXL img2img over code-drawn line art

Used for: the rib cage. Workflows: `tools/art/comfy/ribcage_img2img.json` (final, top-down) and
`ribcage_img2img_oblique.json` (the rejected oblique pass); the failed text-to-image attempt is kept as
`ribcage_txt2img_failed.json`.

1. Draw the line art in code with PIL at 4x (`tools/art/ribcage_topdown.py OUT_DIR`; `ribcage_oblique.py` is the
   rejected angle and `ribcage_proposals.py` the first three flat proposals A/B/C). It writes a back layer, a front
   layer and the masks, on a plain light background `(214,210,202)`.
2. Upload the composite as `ribcage_td_input.png` and run img2img:
   - Checkpoint `waiIllustriousSDXL_v150` + LoRA `rimworld_items_20261013_0803` at 0.8 / 0.8.
   - KSampler: seed 6606, steps 30, cfg 6.0, `dpmpp_2m`, `karras`, **denoise 0.55** (0.5 for the oblique pass);
     batch of 2 via RepeatLatentBatch. A sweep of 0.35 / 0.5 / 0.65 (oblique) and 0.4 / 0.55 (top-down) is in
     `docs/art/proposals/ribcage/*/comfy_denoise_sweep.png`: below 0.4 it stays line art, above 0.6 the ribs drift.
   - Positive: `rimworld item, game sprite, top-down view from directly above, a metal restraint frame lying flat on
     the floor like a bed, rusty cross-shaped board on a riveted steel floor plate, curved steel rib bands across the
     chest area, side rails, weathered rusty brown and dull grey scrap metal, Kenshi, hand painted, flat colors, soft
     shading, thick dark outline, clean, plain light background`
   - Negative: `person, skeleton, human, body, face, text, watermark, scene, floor, photo, realistic, blurry, noise,
     extra objects`
3. Cut it back into textures with the line art's own masks:
   `tools/art/ribcage_cutout.py PAINTED.png OUT_DIR ribcage_topdown` writes `RibCage.png`, `RibCage_front.png` and a
   preview with a skeleton. Because img2img at 0.55 keeps the layout, the code masks still fit.

**What failed.** Text-to-image (SDXL base and waiIllustrious, 704x1408, batch 4, cfg 6.5, 30 steps, seeds 1101, 2202,
3303, 4404, even with `(ribcage:1.4)` weighting) never produced a usable rib cage: it drew people, skeletons or
generic cages, and never the requested camera. Code line art plus img2img is the fix for any shape-critical object.

## Recipe C: three.js model rendered from RimWorld's camera

Used for: the brass tuning fork (all four rotations) and the chemical spire (tempest reliquary). No ComfyUI.

- Scene: `tools/art/<asset>/scene.js` builds the model in cell units (x east, y up, z south) from boxes, cylinders
  and lathes with `MeshToonMaterial` and a 4-step gradient map (70/140/205/255), ambient 0.55 + one directional light
  2.6 from the top left (-0.6, 1.0, 0.6).
- Camera: a custom orthographic projection where a unit of height shows as K = 0.8 units up the screen (the
  "thin front strip" look), with the texture's half size and drawOffset baked in (fork: HALF 1.6, OFF 0.55; spire:
  HALF_X 1.7, HALF_Y 2.8, OFF 1.25). The model rotates, the camera never moves, so rotations always match.
- Render: `npm install three` into `/tmp/forkart` (or set `THREE_DIR`), then
  `node tools/art/tuningfork/render.js /tmp/forkart/out` (headless Chromium with SwiftShader WebGL, 1536 px). It saves
  colour, id, normal and depth passes per rotation.
- Finish: `python3 tools/art/tuningfork/finish.py /tmp/forkart/out Textures/Rimshi/Buildings 384` draws cartoon
  outlines (colour `(34,25,18)`) where the silhouette, part id or normal (dot < 0.25) changes, thickens them to about
  1.4 px at final size and downsizes with Lanczos. The spire uses `tools/art/spire/finish.py` the same way and also
  writes the burst ability icon `UI/LooseTheTempest.png`.
- Stuff tint (cable fissure): a building made from stuff is tinted by multiplying its texture by the stuff colour. To
  tint only part of it, use `shaderType CutoutComplex` and ship a mask per rotation (`<tex>_<rot>m.png`, red = tinted,
  black = untouched). `tools/art/fissure/page.html` adds a `mask` pass (white on materials with `userData.brick`),
  and `finish.py` writes it at the same size. Paint tinted parts LIGHT (the fissure's bricks are 0x5c5c64 with
  0x8e8e98 tops): stone colours are 0.27 (slate) to 0.6 (limestone), so the multiply takes them to near black.
  `finish.py` darkens the icon's bricks by a granite tint, since icons are never stuff-tinted.
- Fissure render: `npm install three` into `/tmp/fissureart`, then `node tools/art/fissure/render.js /tmp/fissureart/out`
  (1920 px) and `python3 tools/art/fissure/finish.py /tmp/fissureart/out Textures/Rimshi 480` (writes the four
  views, four masks and `UI/Heave.png`). Its render.js serves every `three.*` file from `THREE_DIR`: three r170+
  splits the module into `three.module.js` and `three.core.js`, and serving only the first hangs the page.

Good for machines with many parts and exact rotations. It looks cleaner and more "toy-like" than the painted assets;
a Recipe B img2img pass over the colour render (denoise about 0.4) is the untested next step if it needs to look more
hand painted.

## Recipe D: recolours of vanilla art

- `Placeholder_Intermediate.png`: vanilla advanced component recoloured black with caution-yellow trim.
- `Placeholder_Part.png`: vanilla bionic body part item recoloured brown with grey trim.
- Both were made by the PC session from the vanilla textures read out of the game's asset files (it was asked to use
  UnityPy); the exact recolour script was not saved.
- Skeleton wound overlays reuse vanilla's grey and black mechanoid wound textures in code (PR #36); no new art.
- Skeleton heads and bodies are the original Rim-Skeletons (Continued) art by VexedTrees and Zaljerem, used with credit.

## Recipe E: supplied art, cut out

For a finished drawing Matthew hands over (no ComfyUI run of ours):

- `tools/art/recallarray/cutout.py SOURCE.png OUT.png [PREVIEW.png]` (numpy, scipy, Pillow). Background is near-white
  connected to the border, plus enclosed near-white pockets of 150 px or more (gaps seen through railings). The
  anti-aliased rim beside the background is un-mixed from white, so the dark outline keeps a soft edge and no halo.
- It crops to the content, pads to the footprint's ratio with a 3% margin and the base on the bottom edge, and scales to
  128 px per cell. The preview draws the result over the footprint grid to check the base sits on it.
- Change `CELLS` for another footprint.

## Asset catalog

| Asset | Shipped file(s) | Recipe | Picked | Workflow file(s) | Notes |
| --- | --- | --- | --- | --- | --- |
| Repair kit | `Items/RepairKit.png` | A | Round 1 kit 1 (steel case, wrench emblem, yellow latches), then round 2 kit A (lid plus a little front face) | `kontext_repair_kit_round1.json`, `kontext_repair_kit_round2.json` | Round 2 second pass (`rw2b_kit_*`, vanilla medicine as style) is the closest match. Matthew asked for the white inside the handle to be transparent. |
| Repair bed | `Buildings/RepairBed_south/east/north.png` | A | Round 1 bed 2 (gauge machine head, roller deck, yellow rail clamps), then round 2 bed B (medium tilt) | `kontext_repair_bed_round1.json`, `kontext_repair_bed_south/east/north.json` | Shipped views match the `rw4` pass (denoise 0.93, hospital bed of the same facing as style) for south and east and the `rw2` first pass for north, by image comparison; the PC session's own record was not saved. No blanket, so the skeleton shows. |
| Skeleton workbench | `Buildings/SkeletonWorkbench_south/east/north.png` | A, blockouts from a small 3D scene per version | C (brown frame, grey steel top, dark mat, two boxy gantry arms on a hazard rail) | `kontext_workbench_south/east/north.json` | Denoise 0.9, electric smithy of the same facing as style. A (slim arms, oak top) and B (hydraulic arm, chevrons) are in the prompt log. |
| Motor | `Items/Motor.png` | A from the Kenshi icon | A | `kontext_motor.json` | `it1` without the "2D sprite, not a 3D render" clause came out 3D; `it2` fixed it. |
| Robotic component | `Items/RoboticComponent.png` | A from the Kenshi icon | A | `kontext_robotic_component.json` | B's antenna became a 1 px line at 64 px. |
| Skeleton muscle | `Items/SkeletonMuscle.png` | A from the Kenshi icon (a hydraulic piston) | C (twin cylinders) | `kontext_skeleton_muscle.json` | A was faithful but a thin stick at item size. |
| Rib cage | `Buildings/RibCage.png`, `RibCage_front.png` | B | Proposal A (the cross), redrawn top-down | `ribcage_img2img.json` | Two layers so the ribs draw over the suspended skeleton. Oblique version rejected as too front-on. |
| Brass tuning fork | `Buildings/TuningFork_*.png` (4) | C | Upright fork in a speaker cone on a stereo stack with a tape deck at each end and a tape band, ring gears on both tines | `tools/art/tuningfork/` | First version lay on its side; Matthew wanted it upright. |
| Chemical spire | `Buildings/ChemicalSpire.png`, `UI/LooseTheTempest.png` | C | Single design | `tools/art/spire/` | Not rotatable, one view. |
| Cable fissure | `Buildings/CableFissure_*.png` (4 views + 4 masks), `UI/Heave.png` | C | Single design (first version, not yet reviewed by Matthew) | `tools/art/fissure/` | Black bricks of uneven height, tall block north, low block south (worker side), a bundle of seven coloured cables bound with bronze bands runs along a sunk channel between collars with glowing seams; hot seams cracked across the tops. Bricks take the stone's tint through the mask, cables keep their colours. Light from (-0.4, 1.0, 0.3), ambient 0.6, so tops read lighter than fronts. |
| Recall array | `Buildings/RecallArray.png` | E | Matthew's own painting of a rusted bell-shaped relay tower (antenna cross, two dishes, gear collar, torn red pennant), supplied 2026-11-18 | `tools/art/recallarray/cutout.py` | Source 1024x1536 on white, kept in the project's shared `art-proposals/recall-array/`. Cut out, cropped, padded to 3:5 with the base on the bottom edge, 384x640 (128 px per cell). Not rotatable, one view. |
| Placeholders | `Items/Placeholder_Intermediate.png`, `Placeholder_Part.png` | D | | | Limbs, sensors and the AI core still use the part placeholder. |

Proposal images for every round are in `docs/art/proposals/` (round 1 at the top level, `round2/`, `workbench/`,
`intermediates/`, `ribcage/`) and in the project's shared `art-proposals/` folder.

## Lessons

- Start shape-critical or multi-view art from a blockout or line art, never from text alone.
- For multi-view furniture use one blockout per facing, the vanilla building of the same facing as the style image,
  and denoise about 0.9. A single style image for all facings, or denoise 1.0, makes the views disagree.
- Vanilla textures make better style references than our own earlier picks (`rw2b` beat `rw2`).
- Describe the per-view layout in words in the prompt even though the blockout shows it; Kontext follows both.
- Say "hand-painted 2D game sprite, not a 3D render" for items, or Flux renders them in 3D.
- Read every proposal at in-game size before offering it: thin antennas, rods and outlines vanish at 64 px.
- `ImageDraw.floodfill` is pure Python and takes minutes on a 1.5 MP image; label with `scipy.ndimage` instead.
- Save the blockout and post-processing scripts in `tools/art/` with the workflow. The Remote Control rounds did not,
  so their blockouts exist only as files in ComfyUI's input folder on the PC.

## Adding an entry

Every art job, ComfyUI or not, adds to this file in the same PR as the art:

1. A row in the asset catalog table: asset, shipped files, recipe, what Matthew picked, workflow file, notes.
2. The API workflow JSON of the picked variant in `tools/art/comfy/` (or the scene and finish scripts in
   `tools/art/<asset>/`), plus any blockout or line art script.
3. Every variant's prompt, input images, seed and denoise in [prompt-log.md](prompt-log.md).
4. Anything new about what worked or failed in Lessons, and a new recipe section if the method is new.
