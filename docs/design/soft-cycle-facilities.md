# Soft cycle facilities

Buildings that link to the skeleton repair bed and change the CPU soft cycle run in it (see cpu-soft-cycle.md), the way vanilla's deathrest buildings link to a deathrest casket. Code: `Source/RimshiSkeletons/Repair/SoftCycleFacility.cs`.

## Linking

| Rule | How |
| --- | --- |
| Same range as vanilla deathrest buildings | Vanilla binds a deathrest building to a deathrester in the same room with a line of sight to it, at any distance (`Gene_Deathrest.CanUseBindableNow`). Ours use vanilla's facility link (`CompProperties_Facility`, so the build menu and selection draw the link lines): `requiresLOS` true, `maxDistance` 999, and `Patch_SoftCycleFacilitySameRoom` (a postfix on `CompAffectedByFacilities.CanPotentiallyLinkTo_Static`) refuses a link to a bed in another room. Rooms are checked again each time a facility joins a cycle, since walls and doors change after linking. |
| How many per bed | The facility's `maxSimultaneous` (vanilla's own count of one facility def per linked building). |
| Which beds | The repair bed lists the facility in its `linkableFacilities`. The rib cage does not. |

## Joining a cycle

`CompRimshi_SoftCycleFacility` joins (engages with) the soft cycle of a skeleton lying in a linked repair bed on the cycle's first powered tick, or as soon as it is fuelled and linked if that happens mid-cycle. Joining burns `fuelPerCycle` from its `CompRefuelable`; without that much fuel it stays out. It stays with that skeleton until the cycle ends, finished or cut short (`Patch_SoftCycleWake` releases it), or the skeleton is no longer cycling in a bed it serves (its rare tick checks). One skeleton at a time, so a fork linked to two beds serves whichever cycle reached it first. The fuel is spent even if the cycle is cut short.

While engaged its `cycleTimeOffset` is added to the cycle's length factor (`SoftCycleFacilities.TimeFactor`, never below 0.25): the bed cycle's tick adds extra deathrest ticks at `BedCycleSpeed / factor` (`Patch_SoftCycleTick`, carrying fractions of a tick over). The power check for starting a cycle counts the fuelled, free facilities that would join. `cyclePowerFactor` (multiplies the cycle's power draw) is still there for later facilities; the tuning fork leaves it at 1.

## Brass tuning fork

`Rimshi_TuningFork`, Matthew's spec, 2026-10-31.

| | |
| --- | --- |
| Size | 1x2, rotatable, minifiable (like vanilla's deathrest buildings) |
| Cost | 2 advanced components, 3 motors (`Rimshi_Motor`), 120 steel, 40 plasteel, 8 components; construction 6, work 6000 |
| Research | Skeleton repair beds |
| Per bed | 2 |
| Fuel | 1 advanced component each cycle it joins; holds 1, haulers refill it (auto refuel toggle) |
| Effect | Cycle length x0.85 each: one fork 40.8 hours, two 33.6 hours (from 48) |
| Power | None (Matthew didn't ask for any) |

Calls Claude made: the 15% stacks additively (two forks: 30% shorter, like vanilla's deathrest efficiency offsets add up); the component is burnt when the fork joins a cycle, not when the cycle finishes, so a cycle cut short still costs it; capacity 1, so it needs a refill after every cycle; no power draw.

The description says only that the skeleton "comes back to itself sooner" and that each cycle burns a component (no-spoiler text rule).

### Art

`Textures/Rimshi/Buildings/TuningFork_south/east/north/west`, 320x320 at drawSize (2.5,2.5) (128 px a cell, the footprint centred, room above for the raised fork). Matthew's brief: a large brass tuning fork whose prongs are joined by gear segments, turned 30 degrees to the side, in RimWorld's oblique view, with rotations that fit the front view.

Drawn as a 3D model so the four views always agree: `tools/art/tuningfork/scene.js` (three.js) builds the fork lying on a resonator box, stem south and prongs north, rolled 30 degrees about its long axis so both its face and its edge show, three gears meshing with each other and with rack teeth on the prongs' inner edges. One fixed camera draws every rotation: an oblique projection where a unit of height rises half a unit up the screen (the thin front strip of RimWorld furniture), toon shading. `render.js` renders colour, part id and normal passes in headless Chromium; `finish.py` draws the outlines from part boundaries, silhouettes and sharp creases and downsizes. To redraw: `npm install three` somewhere, then `THREE_DIR=<that>/node_modules/three/build node tools/art/tuningfork/render.js /tmp/fork` and `python3 tools/art/tuningfork/finish.py /tmp/fork Textures/Rimshi/Buildings`.

## Tests

Log lines `facility | engaged, released`. Self-test `tuning fork` (link limit, line of sight, the same-room patch, fuel, the offset, the repair bed's link list, all four textures).
