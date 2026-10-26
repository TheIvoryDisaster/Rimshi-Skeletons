# CPU soft cycle, CPU overload and rib cages

Matthew's spec, 2026-10-26, from the Kenshi wiki's skeleton lore (https://kenshi.zone/en/skeleton#The_CPU_memory_reset_madness): living forever without a way to express what they feel, or to feel and touch the world, slowly erodes a skeleton's sanity. Skeletons cope by periodically resetting their CPU and memory buffer: long-term memory and programming stay, short-term memories and some skills go. Deathrest becomes this **CPU soft cycle**, and the deathrest bar is shown as **CPU integrity**. This replaces the chemfuel deathrest from PR #23 (the "Deathrest" section of maintenance-needs.md).

## CPU integrity

Still Biotech's deathrest underneath: the `Rimshi_Deathrest` gene (defName kept for saves, now labelled "CPU soft cycle") with vanilla `Gene_Deathrest` and the `Deathrest` need, relabelled "CPU integrity" with the lore as its tooltip.

| Rule | How |
| --- | --- |
| Falls 1/30 a day (empty in 30 days) | `Patch_CpuIntegrity` replaces `Need_Deathrest.NeedInterval` for skeletons. Holds while the need is frozen, while cycling (cycles raise it themselves) and while suspended in a rib cage. |
| At 0%: CPU overload | Same patch, instead of vanilla's deathrest exhaustion hediff. Vanilla's "deathrest exhaustion" mood (-12) reads the need directly (`ThoughtWorker_DeathrestExhaustion`: active at 0%), so `Patch_NoDeathrestExhaustionThought` switches it off for skeletons; they get the overload's own moods. |
| Tooltip | `Patch_CpuIntegrityTip`: integrity, one lore line on the CPU's current state (`SoftCycle.StateLine`: running clean, memories piling up, crowded buffer, overloaded, or cycle/reset progress), then the lore. No countdown. |
| Buttons | `Patch_SoftCycleGizmos` drops vanilla's wake and auto-wake for skeletons and adds: **Soft cycle** (`SoftCycle.CycleGizmo`, a targeting button: a usable repair bed starts the bed cycle, a free rib cage or any spot the skeleton can stand on starts the slow soft reset; Matthew 2026-10-26: it must never be greyed out just because the repair bed is taken), **Stop soft cycle / Stop soft reset** (with a confirmation), and in dev mode **DEV: Set CPU integrity**. |
| Gizmo by the draft button | Vanilla's deathrest gizmo ("Deathrest", the need bar, deathrest building count) becomes `GeneGizmo_Rimshi_CpuIntegrity`: "CPU integrity", the bar with the integrity tooltip, and the CPU's state (Running, Soft cycling, Soft resetting, Suspended, Overloaded) with the state line as its tooltip. |
| Labels | The deathrest hediff reads "CPU soft cycle", the job report "soft cycling: 40%". |

## Bed soft cycle

| Rule | How |
| --- | --- |
| 48 hours in a powered repair bed | Vanilla deathrest is 4 days; `Patch_SoftCycleTick` adds an extra tick per tick (`BedCycleSpeed` 2) instead of patching the tiny `MinDeathrestTicks` getter, which the JIT may inline. Deathrest speed buildings would still count. |
| Integrity rises with progress | Linear from wherever it started to 100% at the end (`SoftCycle.Raise`). |
| No chemfuel | The bed's chemfuel tank and `CompRimshi_DeathrestFuel` are gone. Fuel left in a tank in an older save is lost. |
| Power draw | `CompRimshi_RepairBed` sets the bed's draw every rare tick: 50 W standby, +400 W while repairing someone, +15 W per skill level (all skills added up) for each skeleton cycling in it. `CompProperties_Rimshi_SoftCycleFacility.cyclePowerFactor` on a linked facility multiplies the cycle draw: the hook for the connected buildings to come (none use it yet). |
| Won't start without the power | The AI and the Soft cycle button only use a bed whose grid has spare generation for the cycle draw, or batteries holding enough to cover the shortfall for 48 hours. The bed must still be roofed (vanilla wakes deathresters in sunlight). |
| Power lost or stopped: cancelled | Any tick in an unpowered repair bed (power cut, flicked off, solar flare, breakdown, EMP) or Stop soft cycle before it finishes: `Patch_SoftCycleWake` turns vanilla's interrupted deathrest into a cancel: integrity 0% and a CPU overload. |
| Held, not cancelled, outside the bed | A cycling skeleton that is carried, rescued into a rib cage, or lying in an older save's deathrest on the floor neither progresses nor cancels. Put back in a powered repair bed, it carries on. |
| Finish | Always ends by itself (auto-wake forced on). See "Finishing a cycle". |

The AI still starts a cycle on its own at 5% integrity (vanilla's threshold), in the nearest usable repair bed; with none it never does, and the skeleton overloads unless the player orders a standing reset.

## Soft reset without a repair bed

For colonies without the repair bed and its AI persona core. **Soft cycle** on a cell (or a rib cage) instead of a repair bed: (the skeleton walks there and stands still facing south) or a free rib cage (it hangs there). `Hediff_Rimshi_SoftReset` counts the time and raises integrity along `StandingCurve`:

| Day | 1 | 2 | 3 | 4 | 5 |
| --- | --- | --- | --- | --- | --- |
| Done | 4% | 9% | 16% | 40% | 100% |

The job (`Rimshi_SoftReset`) is player-ordered, so damage doesn't interrupt it, but anything that ends it early (Stop soft reset, drafting, being downed, a mental break) cancels the reset like a bed cycle: 0% and an overload.

## Finishing a cycle

Both kinds: integrity to 100%, CPU overload removed, and the memory buffer flushed:

* every mood memory is removed, good and bad (short-term memories);
* 3 to 6% of the total XP is taken from one skill (bed) or two skills (reset), picked weighted by level (vanilla's deathrest damage numbers).
* Bed: "Honing my memories like an edgewalker." +3 for 5 days. Reset: "It's like these memories aren't mine..." -3 for 2 days.

The memory flush and skill loss are Claude's reading of "short term memories and some skills disappear"; Matthew's numbered list didn't ask for them. They give Cat-Lon's choice (skip resets to keep skills) a real cost. Easy to remove or tune.

## CPU overload

`Rimshi_CpuOverload`, from integrity reaching 0% or a cancelled cycle. Severity counts days overloaded and stops while cycling or suspended in a rib cage. Only a finished cycle removes it; a new cancel during an overload keeps its clock.

| Days | Stage | Effects |
| --- | --- | --- |
| 0 to 2 | sluggish | Moving and manipulation x80%, "CPU overloaded" -8 mood |
| 2 to 4 | recalibrating | none |
| 4+ | vigilant (paranoid) | Moving and manipulation x110%, negotiation ability and trade price improvement x110%, a mood penalty of -1 on the first day and one more each day (capped at -30), and only violent mental breaks |

The paranoid thought (`Thought_Rimshi_CpuMadness`) shows one of Matthew's four lines as its label, a new one each day: "Stobe... gave everything...", "We used to outnumber the humans...", "Flashes of the old empire come to me in dreams... I think they're dreams...", "Skeletons are always crying". `Patch_CpuMadnessBreaks` keeps only mental breaks whose state is aggro (berserk, tantrums, sadistic rage, murderous rage); if none fits the intensity it picks any violent break that can happen.

### Player text (no spoilers)

Matthew, 2026-10-26: player-facing text never explains the mechanics or warns what comes next; Kenshi is about not knowing and finding out. Each overload stage's tooltip (`overrideTooltip` on the stage) describes only that state and its in-world cause:

* sluggish: centuries of memory backed up through the CPU, motors lagging; nothing has flushed the buffer in a long time (a nudge toward the soft cycle);
* recalibrating: the CPU has stopped fighting and is rebalancing around the load as the new normal;
* vigilant: running hot is passively waking old defensive protocols; sharper and quicker, "perhaps this is for the better", but it watches everyone a little too closely.

Messages say only what just happened ("X's CPU has overloaded.", "X's CPU is recalibrating to the overload.", "Something old is stirring in X's CPU."). Gizmo, gene, building and hediff descriptions drop durations, percentages and consequences (the numbers above stay in this doc and the test checklist). The repair bed's inspect line still shows the cycle draw in W, since the player has to plan the grid for it.

The CPU integrity tooltip (gizmo and needs tab) is `SoftCycle.TipString`: "CPU integrity: N%", the state line, then the gene's relabel description, a lore account of the CPU, the memory buffer and the soft cycle. It never falls back to vanilla's deathrest description. The gizmo draws that tooltip itself (the bar's own tooltip is off), and `GetTipString` is swapped both on `Need_Deathrest` (where it overrides) and on base `Need` (where a game version does not override it and would add vanilla's "comes from gene" line).

Art flags: the rib cage has `drawRotated` false (it is unrotatable but placed facing south, so `Graphic_Single` would turn it 180 degrees) and `bed_UseSheetColor` false (as a medical bed it would otherwise take the blue sheet colour). The repair bed is drawn at (2.4,2.4), 20% over the hospital bed's size.

## Rib cage

`Rimshi_RibCage`: 1x2, never rotated, 80 steel and 3 components after basic mechtech. A `Building_Bed` that is always medical (the medical toggle is hidden), so it is never assigned. Only skeletons can use it.

| Rule | How |
| --- | --- |
| Stasis | `Patch_RibCageStasis` freezes every need of a pawn lying in a rib cage (`Need.IsFrozen`), so integrity, maintenance and the rest hold; the overload clock holds too. |
| Enter by choice | Right-click it with a skeleton: **Hang in rib cage** (stays until given another order) or **Soft cycle in rib cage** (the slow soft reset). |
| Rescue | A downed skeleton is carried to the best free medical bed it can use: the repair bed (tend offset 0.12), then a rib cage (0.11), then hospital beds (0.10). A skeleton mid-cycle outside a repair bed is downed by vanilla's deathrest, so it is rescued the same way and held there. |
| Looks | Vanilla draws a pawn in a south-facing bed upright, so the skeleton hangs upright. The building texture is the frame behind it; `CompRimshi_RibCage` draws the front ribs over it. Not minifiable: a reinstalled unrotatable building would face north and hang the skeleton upside down. |

Art: `Textures/Rimshi/Buildings/RibCage` (frame) and `RibCage_front` (ribs over the skeleton), 256x512 at drawSize 1.25x2.5. Three proposals in docs/art/proposals/ribcage/ (A cross, B gibbet, C ribbed sarcophagus; `_preview` shows each with a stand-in skeleton), drawn by tools/art/ribcage_proposals.py because ComfyUI on Matthew's PC did not respond. Matthew picked A. A first redraw (docs/art/proposals/ribcage/oblique/, tools/art/ribcage_oblique.py) was seen too much from the front; Matthew wanted it like RimWorld furniture, looked down on almost straight from above with only a thin strip of front faces showing, so the skeleton lies on the cross and the ribs are bands across its chest. tools/art/ribcage_topdown.py draws that line art and its masks; ComfyUI on Matthew's PC paints it (img2img, waiIllustriousSDXL + the rimworld_items LoRA, denoise 0.55, workflow tools/art/comfy/ribcage_img2img.json); tools/art/ribcage_cutout.py cuts the painted image back into the two textures with the line art's masks. Sweep and previews in docs/art/proposals/ribcage/topdown/.

## Calls Claude made (for Matthew to confirm)

* Power units: 15 W per skill level on top of the bed's 50 W standby and 400 W repair draw. A skeleton with 80 skill levels draws 1,200 W while cycling.
* Overload clock starts when integrity hits 0% (or at a cancel), not at the last cycle.
* The standing reset is cancelled by any interruption, like the bed cycle.
* Overload: the -1 a day caps at -30; "trade prices +10%" is a x1.1 factor on trade price improvement.
* Needs freeze entirely in a rib cage (maintenance included), not just integrity.
* The memory flush and skill loss on finishing (above).

## Tests

Log lines `softcycle | start, cycling, held, no bed, complete bed, cancelled bed, start standing, complete standing, cancelled standing, migrated`, `overload | begin, stage, violent breaks, end`, `ribcage | hang`. Self-tests: soft cycle bed, soft cycle length, standing curve, integrity raise, cancel overloads, overload stages, madness thought, violent breaks, cycle complete, rib cage, rib cage art, rib cage stasis. Older saves: deathrest exhaustion on a skeleton becomes a CPU overload (`SoftCycle.MigrateSave`).

## Not done

* Vanilla's low deathrest alert still says "needs deathrest" (and doesn't know about resets or rib cages).
* The connected soft cycle buildings and capacity content (future milestone).
* A skeleton standing in a soft reset can't be carried into a rib cage mid-reset; carrying it ends the reset.
