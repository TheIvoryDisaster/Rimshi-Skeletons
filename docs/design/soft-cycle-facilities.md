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
| Power | 50 W idle, 1000 W while engaged with a cycle (Matthew, 2026-10-31). Unpowered (cut, flicked off) it won't join a cycle, and an engaged fork stops shortening the cycle until power is back (it stays engaged, so no second component). The repair bed's power check for starting a cycle adds the extra 950 W of each fork that would join. |

Calls Claude made: the 15% stacks additively (two forks: 30% shorter, like vanilla's deathrest efficiency offsets add up); the component is burnt when the fork joins a cycle, not when the cycle finishes, so a cycle cut short still costs it; capacity 1, so it needs a refill after every cycle.

The description says only that the skeleton "comes back to itself sooner" and that each cycle burns a component (no-spoiler text rule).

### Art

`Textures/Rimshi/Buildings/TuningFork_south/east/north/west`, 384x384 at drawSize (3.2,3.2) with drawOffset 0.55 cells north (120 px a cell; the footprint sits at the bottom of the texture and the fork rises into the space above). Matthew's brief: a large brass tuning fork with gears, turned 30 degrees to the side, in RimWorld's oblique view, with rotations that fit the front view; then (2026-10-31) standing upright on techno stereo equipment, the gears wrapped around the tines, and a second cassette deck on the other side joined to the first by a band of audio tape.

Drawn as a 3D model so the four views always agree: `tools/art/tuningfork/scene.js` (three.js) builds a stereo stack filling the footprint (amplifier with VU meter, knobs and LEDs on the ends, speakers and equaliser sliders down the long sides, a tape deck at each end joined by a band of audio tape between their inner reels) and the fork standing upright in an upturned speaker cone in the middle, turned 30 degrees, with two levels of ring gears threaded on the tines, each pair meshing between them. One fixed camera draws every rotation: an oblique projection where a unit of height rises 0.8 of a unit up the screen (tall things read front-on, tops stay full size), toon shading. The first version (the fork lying on a resonator box) was replaced at Matthew's ask. `render.js` renders colour, part id and normal passes in headless Chromium; `finish.py` draws the outlines from part boundaries, silhouettes and sharp creases and downsizes. To redraw: `npm install three` somewhere, then `THREE_DIR=<that>/node_modules/three/build node tools/art/tuningfork/render.js /tmp/fork` and `python3 tools/art/tuningfork/finish.py /tmp/fork Textures/Rimshi/Buildings 384`.

## Chemical spire ("tempest reliquary")

`Rimshi_ChemicalSpire`, Matthew's spec, 2026-10-31. Code: `Source/RimshiSkeletons/Repair/ChemicalSpire.cs`.

| | |
| --- | --- |
| Name | Label "tempest reliquary" (Claude's pick; alternates offered: "abeyance spire", "stormwell of the First Empire", "sepulchre of held lightning"). The defName stays descriptive so the label can change freely. |
| Size | 3x3, not rotatable, not minifiable, impassable |
| Cost | 250 steel, 80 plasteel, 8 components, 2 advanced components; construction 8, work 12000 (Claude's call) |
| Research | Skeleton repair beds (Claude's call, same as the fork) |
| Per bed | 1 (Claude's call: it gives a single burst) |
| Battery | First filled with 100 chemfuel (haulers fill it, auto refuel toggle; it can't be ejected). Once full it charges: 100 W for 36 hours without a break. Losing power while charging (cut, flicked off, unpowered at a rare tick) starts the charge over; the chemfuel stays. A full charge never drains, powered or not, and draws nothing; empty it draws nothing either. Read "consumes 100 energy" as 100 W while charging. |
| Joining | It joins a cycle only while charged, and needs no power to join (`requiresPower` false). Joining costs nothing: the charge and all 100 chemfuel go only when that cycle FINISHES (`CompRimshi_SoftCycleFacility.Release(finished: true)` calls `CompRimshi_SpireCharge.Discharge`). A cycle cut short leaves it charged. It does not change the cycle's length or power draw. |
| Effect | The skeleton gets `Rimshi_HeldTempest`: EMP-proof, move speed x1.1, and the one-use ability `Rimshi_LooseTheTempest`. Loosing it turns the hediff into `Rimshi_TempestWake` (EMP-proof only). Both end when the skeleton's next soft cycle finishes (bed or standing; a cycle cut short does not end them), which is how "immune for their next cycle" reads here. |
| EMP immunity | `SkeletonModules.EmpSusceptible` (the unshielded circuits module) returns false while either hediff is on, so EMP neither stuns nor adapts the skeleton. Nothing else about the module changes. |
| Burst | Radius 10 around the caster, line of sight. Hits every HOSTILE mechanoid and skeleton in range (Matthew, 2026-10-31; allies, neutrals and the caster are spared). Each takes an EMP hit equal to the damage (stun 30 ticks a point through vanilla's StunHandler, so EMP adaptation, EMP resistance and other held tempests apply) and that much burn damage in hits of up to 10, armour penetration 0.3. EMP blast flecks and sound. |
| Damage | "Accumulated skill points" read as the sum of the skeleton's skill levels (`SoftCycle.SkillLevels`, the number the bed's cycle power draw already uses): 0.25 per level, at least 5, at most 40. 120 levels (10 in every skill) gives 30, Matthew's "high"; 160 or more gives the maximum 40. A typical colonist (60 to 70 levels) does about 15 to 17. Read when the burst is loosed, so skill lost to later cycles lowers it. |

Player text says none of this: the building description says it drinks chemfuel, then power "for a long time without a pause", keeps what it takes and gives it to a skeleton that finishes a soft cycle beside it; the hediffs describe the state (moves lightly, circuits sealed, something waiting to be let go).

### Art

`Textures/Rimshi/Buildings/ChemicalSpire.png`, 408x672 at drawSize (3.4,5.6), drawOffset 1.25 cells north (120 px a cell, the 3x3 footprint at the bottom of the texture, the antenna rising above), `Graphic_Single` with `drawRotated` false. Matthew's brief: an antenna jutting out of a large cylinder with an LED panel and a glass viewport showing dark liquid. Same pipeline as the fork, so the two match: `tools/art/spire/scene.js` (stepped plinth, pale metal cylinder with bronze bands and ribs, viewport with dark liquid and a sheen, LED panel, side pipes, a cone cap, ringed antenna with a glowing tip), `render.js`, and `finish.py` (reuses the fork's outline code and also writes the ability icon `Textures/Rimshi/UI/LooseTheTempest.png`). To redraw: `npm install three` somewhere, then `THREE_DIR=<that>/node_modules/three/build node tools/art/spire/render.js /tmp/spire` and `python3 tools/art/spire/finish.py /tmp/spire Textures/Rimshi 408`.

## Tests

Log lines `facility | engaged, released` and `spire | charged, charge lost, storm granted, burst, storm ended`. Self-tests `tuning fork` (link limit, line of sight, the same-room patch, fuel, the offset, the repair bed's link list, all four textures), `chemical spire` (size, link limit, chemfuel, charge, joins unpowered, bed link list, art and icon) and `spire storm` (EMP-proof while held and in the wake, speed, ability given and taken, both end, damage range). The DEV gizmo "Charge fully" on the spire skips the fill and the 36 hours.
