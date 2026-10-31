# Skeleton organs

Matthew's ask, 2026-10-27: skeletons still had human organs (lungs, a stomach...) that showed up when they were hurt. Each one is now a machine part, with a craftable replacement at the standard skeleton part cost.

| Human organ | Machine part | defName |
| --- | --- | --- |
| Lungs | Cooling fans | `Rimshi_CoolingFan` |
| Kidneys | Fluid reprocessors | `Rimshi_FluidReprocessor` |
| Stomach | Capacitor | `Rimshi_Capacitor` |
| Liver | Oil filter | `Rimshi_OilFilter` |
| Heart | Fluid pump | `Rimshi_FluidPump` |
| Eyes | Optical sensors | `Rimshi_OpticalSensor` |
| Ears | Auditory arrays | `Rimshi_AudioReceiver` (defName kept from the audio receiver, for saves) |
| Nose | IR camera | `Rimshi_IRCamera` |

## How it works

This is Anatomy option B from design/xenotype-port.md (VRE Android style), with option A's renaming on top. Code: `Source/RimshiSkeletons/Skeleton/SkeletonAnatomy.cs`.

- **Fitted parts.** The list lives on `Rimshi_MechanicalNature` (`SkeletonAnatomyExtension`). Its gene class (`Gene_MechanicalNature`) fits the part hediff on every listed organ in `PostAdd`, which covers generation, xenogerms and dev mode. A `PawnGenerator.GeneratePawn` postfix does the same as a backstop. Older saves get them on load (`SkeletonAnatomy.MigrateSave`). An organ that is already missing, or already has an added part (a converted pawn's bionic eye), is left alone. Fitting a part clears what was on that organ, as any bionic install does, so a skeleton migrated from an older save loses injuries and scars on those organs once.
- **The part hediffs** (`Defs/HediffDefs/Rimshi_SkeletonOrgans.xml`) are added parts at 100% efficiency, so capacities, vital organs and death work as for the human organ (a destroyed fluid pump kills, like a heart). They are `Hediff_Rimshi_SkeletonOrgan`, which is hidden in the health tab like natural organs; dev mode's "show all hediffs" still lists them. They don't count as prosthetics for transhumanist or body purist thoughts (`countsAsAddedPartOrImplant` false) and add no pawn value (`priceImpact` false). Removing one by surgery gives back the item (`spawnThingOnRemoved`), not a human organ.
- **Names.** Wherever a skeleton's parts are named, a transpiler swaps the `BodyPartRecord` label calls for `SkeletonAnatomy`'s, which read the part's machine name for a skeleton: "left lung" becomes "left cooling fan" (the side comes from the human label), "stomach" becomes "capacitor". The pawn comes from a `[ThreadStatic]` context set around each site: the health tab rows and their tooltips (`HealthCardUtility.DrawHediffRow`, `GetTooltip`), the surgery menu (`GenerateSurgeryOption`), medical bills (`Bill_Medical.Label`), and the combat log (the four damage `BattleLogEntry` types set their recipient while `GrammarUtility.RulesForBodyPartRecord` builds the part rules). Other text that names a part (some vanilla messages) still uses the human name.
- **Replacements** (`Defs/ThingDefs/Rimshi_SkeletonParts.xml`, `Defs/RecipeDefs/Rimshi_SkeletonPartSurgery.xml`): made at the skeleton workbench with Skeleton fabrication research and Crafting 11, at the standard skeleton limb cost (2 motors, 1 robotic component, 1 skeleton muscle, 40 steel; work 65). Installing one uses a repair kit, like the other skeleton parts. Vanilla's artificial part worker only offers an install where the part isn't already fitted, so the bill shows up once the organ is gone.

## Changes to earlier parts

- The optical sensor and audio receiver (PR #22) cost 2 robotic components, 2 components and 1 advanced component. Matthew listed eyes and ears with the other organs at the standard cost, so they now cost the same as the rest. The audio receiver is now called the auditory array.

## Open

- Anything that restores a missing part without our install (healer mech serum, biosculpter, other mods' regrowth) gives back a bare human organ. It works, but isn't renamed in the health tab until the skeleton gets the machine part. A save load does not refit it, because the organ was there all along. Possible fix: refit on `Pawn_HealthTracker.RestorePart` for skeletons.
- The brain (AI core) and jaw keep their current handling; they weren't on the list.
