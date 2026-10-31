# Skeleton modules

Status: (draft) 2026-10-31. Two modules in place; nothing modifies them yet.

## Goal

Matthew, 2026-10-31: every skeleton has a module (gene) that makes it susceptible to EMP stun the same way mechanoids are, and a module that gives it a penalty to social work such as trade price negotiation. Later we add ways to modify these modules, and a soft cycle facility that interacts with them.

## What a module is

A module is a core gene (an endogene from the Skeleton xenotype, shown under cores on the genetics screen) that later systems are meant to change, unlike the fixed hardware (frame, chassis, mechanical nature). They inherit from the abstract `Rimshi_SkeletonModuleBase`, which sets `SkeletonGeneExtension.modifiable` to true (kind stays Core). `SkeletonModules.IsModule` reads it.

Each rule is its own gene, not part of `Rimshi_MechanicalNature`, so a chip or facility can turn one off without touching the others. The code only checks that the gene is active (`Pawn_GeneTracker.HasActiveGene`), so suppressing it with vanilla's gene override, or removing it, turns its effect off with no other change.

Neither module costs metabolism, so the xenotype's metabolism, and the maintenance rate that follows it, is unchanged. Complexity 1 each.

## Unshielded circuits (`Rimshi_UnshieldedCircuits`)

Vanilla `StunHandler.CanBeStunnedByDamage` lets EMP stun a pawn only when its race is not flesh (mechanoids). Skeletons are humans underneath, so `Patch_EmpStunsSkeletons` (a postfix) also allows it for a pawn with this module, after vanilla's own exceptions (downed, dead, awoken corpse). The rest is vanilla:

| | |
| --- | --- |
| Stun length | EMP damage x 30 ticks (vanilla `StunDurationTicksPerDamage`), cut by the pawn's EMP resistance stat. An EMP grenade (50) stuns for about 25 seconds. |
| Adaptation | `Patch_SkeletonsAdaptToEmp` (postfix on `CanAdaptToDamage`): like mechs, after one EMP stun more EMP does nothing for the EMP def's `stunAdaptationTicks` (2200, about 37 seconds). |
| Look | Vanilla's EMP stun effect and the "Stunned by EMP" inspect line. |
| Not changed | Nerve stun (Anomaly) still works on skeletons; mechband shockwaves still do not. |

Known gap: vanilla AI picks targets for an EMP weapon only among non-flesh pawns (`AttackTargetFinder.BestAttackTarget`, inside a compiler-generated lambda), so raiders do not aim EMP grenades at skeletons on purpose. An EMP blast that catches a skeleton (aimed at a mech or turret, or thrown by the player) stuns it. A transpiler on that lambda would close the gap if Matthew wants it.

## Archaic speech (`Rimshi_ArchaicSpeech`)

Stat factors only, so they scale whatever the Social skill gives.

| Stat | Factor | What it covers |
| --- | --- | --- |
| Trade price improvement | x0.5 | Buy and sell prices when this pawn trades |
| Negotiation ability | x0.8 | Recruitment speed, gift goodwill, peace talks |
| Social impact | x0.5 | How much its social interactions affect others. Moved here from the archaic persona (`Rimshi_ArchaicMind`), which had the same 0.5, so it is not counted twice and can be changed on its own. |
| Conversion power (Ideology) | x0.8 | Ideoligion conversion attempts |

Values are Claude's call (2026-10-31); Matthew may tune them.

## Older saves

`SkeletonModules.MigrateSave` runs when a save loads: a skeleton (frame gene) whose xenotype lists a module it does not have gets it, so skeletons from before this change pick both up. Logged as `module | added on load`.

## Future hooks

- **Chip system** (skeleton-chips.md): a chip could counter a module (for example an EMP shielding chip that overrides unshielded circuits, or a diplomacy chip that overrides archaic speech) through vanilla's gene override, the same way a xenogene overrides an endogene. Modules stay endogenes and are never extracted into chips; the chip extractor should keep skipping them as core genes.
- **Soft cycle facility** (soft-cycle-facilities.md): a facility joining a cycle could add a hediff that suppresses or softens a module until the next cycle, or permanently recalibrate it. `CompProperties_Rimshi_SoftCycleFacility` would get a field naming which modules it affects; the check stays `HasActiveGene` plus whatever the hediff changes.
- To soften rather than remove (for example half the trade penalty), add a stat part or a hediff with its own stat factors rather than changing the gene's def values.

## Open questions

- Values above.
- Should raiders aim EMP at skeletons (the transpiler above)?
- Which chips and facilities modify which module, and is it suppress, soften or remove?
