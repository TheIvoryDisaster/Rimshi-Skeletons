# CPU soft cycle, CPU overload and rib cages

Matthew's spec, 2026-10-26, from the Kenshi wiki's skeleton lore (https://kenshi.zone/en/skeleton#The_CPU_memory_reset_madness): living forever without a way to express what they feel, or to feel and touch the world, slowly erodes a skeleton's sanity. Skeletons cope by periodically resetting their CPU and memory buffer: long-term memory and programming stay, short-term memories and some skills go. Deathrest becomes this **CPU soft cycle**, and the deathrest bar is shown as **CPU integrity**. This replaces the chemfuel deathrest from PR #23 (the "Deathrest" section of maintenance-needs.md).

## CPU integrity

Still Biotech's deathrest underneath: the `Rimshi_Deathrest` gene (defName kept for saves, now labelled "CPU soft cycle") with vanilla `Gene_Deathrest` and the `Deathrest` need, relabelled "CPU integrity" with the lore as its tooltip.

| Rule | How |
| --- | --- |
| Falls 1/30 a day (empty in 30 days) | `Patch_CpuIntegrity` replaces `Need_Deathrest.NeedInterval` for skeletons. Holds while the need is frozen, while cycling (cycles raise it themselves) and while suspended in a rib cage. |
| At 0%: CPU overload | Same patch, instead of vanilla's deathrest exhaustion. |
| Tooltip | `Patch_CpuIntegrityTip`: integrity, days until overload or cycle progress, then the lore. |
| Buttons | `Patch_SoftCycleGizmos` drops vanilla's wake and auto-wake for skeletons and adds: **Soft cycle** (go to the nearest usable repair bed now; greyed with the reason when there is none), **Begin soft reset** (target a cell or rib cage), **Stop soft cycle / Stop soft reset** (with a confirmation), and in dev mode **DEV: Set CPU integrity**. The vanilla deathrest capacity gizmo stays (for the future capacity content). |
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

For colonies without the repair bed and its AI persona core. **Begin soft reset** targets a cell (the skeleton walks there and stands still facing south) or a free rib cage (it hangs there). `Hediff_Rimshi_SoftReset` counts the time and raises integrity along `StandingCurve`:

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
| 2 to 4 | steady | none |
| 4+ | paranoid | Moving and manipulation x110%, negotiation ability and trade price improvement x110%, a mood penalty of -1 on the first day and one more each day (capped at -30), and only violent mental breaks |

The paranoid thought (`Thought_Rimshi_CpuMadness`) shows one of Matthew's four lines as its label, a new one each day: "Stobe... gave everything...", "We used to outnumber the humans...", "Flashes of the old empire come to me in dreams... I think they're dreams...", "Skeletons are always crying". `Patch_CpuMadnessBreaks` keeps only mental breaks whose state is aggro (berserk, tantrums, sadistic rage, murderous rage); if none fits the intensity it picks any violent break that can happen.

## Rib cage

`Rimshi_RibCage`: 1x2, never rotated, 80 steel and 3 components after basic mechtech. A `Building_Bed` that is always medical (the medical toggle is hidden), so it is never assigned. Only skeletons can use it.

| Rule | How |
| --- | --- |
| Stasis | `Patch_RibCageStasis` freezes every need of a pawn lying in a rib cage (`Need.IsFrozen`), so integrity, maintenance and the rest hold; the overload clock holds too. |
| Enter by choice | Right-click it with a skeleton: **Hang in rib cage** (stays until given another order) or **Begin soft reset in rib cage**. |
| Rescue | A downed skeleton is carried to the best free medical bed it can use: the repair bed (tend offset 0.12), then a rib cage (0.11), then hospital beds (0.10). A skeleton mid-cycle outside a repair bed is downed by vanilla's deathrest, so it is rescued the same way and held there. |
| Looks | Vanilla draws a pawn in a south-facing bed upright, so the skeleton hangs upright. The building texture is the frame behind it; `CompRimshi_RibCage` draws the front ribs over it. Not minifiable: a reinstalled unrotatable building would face north and hang the skeleton upside down. |

Art: placeholder (`Textures/Rimshi/Buildings/RibCage` and `RibCage_front`, 256x512 at drawSize 1.25x2.5) until Matthew picks a proposal from docs/art/proposals/ribcage/.

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
