# Rimshi Skeletons: Kenshi lore, repair kits and repair bed

Status: (draft) 2026-10-24. Nothing here is built yet. This sits beside [xenotype-port.md](xenotype-port.md) and adds three things Matthew asked for: Kenshi lore to theme the mod, **skeleton repair kits**, and a **skeleton repair bed** that costs an AI persona core.

Code hooks below were checked against the decompiled 1.6 source on 2026-10-24 (`TendUtility`, `Toils_Tend`, `JobDriver_TendPatient`, `HealthAIUtility`, `MedicalCareUtility`, `Pawn_HealthTracker`, `Hediff_Injury`, `RestUtility`, `Building_Bed`, `MechRepairUtility`, `JobGiver_PatientGoToBed`, `Toils_LayDown`). Items marked **verify** were not.

## 1. Kenshi lore

Kenshi (Lo-Fi Games) is where the original mod's skeletons come from: the head shapes (`Catlon`, `Screamer`, `Scout`...), the "Second Empire" faction name maker, the "hydraulic knight", the Kenshi Armory compat. The lore below is from memory. The broad strokes are solid; anything marked *(unsure)* should be checked on the Kenshi wiki before a name or detail goes into game text.

**What skeletons are.** Robots built long ago by the First Empire, the high-tech human civilisation that existed before "the Fall" that left the world a wasteland. They were made as servants, labourers and soldiers. Many are thousands of years old, and most have damaged or wiped memory banks, so they remember little of what they were built for. Their ancient makers are gone and nobody alive knows how to build new ones.

**How they live.** Skeletons do not eat, sleep or bleed. They are strong, tough and good at labour, but slow to learn some things and poor at others. Importantly for us: **they do not heal on their own.** Damage stays until it is fixed by:

- **Skeleton Repair Kits**: the skeleton version of a first aid kit. Using one draws on the **Robotics** skill, not first aid. Robotics is also the skill for crafting robotic limbs (which skeletons and humans both use).
- **Skeleton Repair Beds**: a building that slowly repairs a skeleton lying in it, all the way back to full health. It is the skeleton equivalent of a hospital bed and the main way skeletons recover from serious damage. It needs research to build.

**AI cores.** In Kenshi, AI cores are rare ancient artifacts found in ruins and labs, needed to research the highest tiers of technology (traded by and sought by the **Tech Hunters**, a faction of explorers and scholars). This is a natural match for RimWorld's AI persona core as the price of the repair bed.

**The Second Empire.** After the First Empire fell, skeletons ruled the world as the Second Empire, with humans as their subjects. Its emperor was the skeleton **Cat-Lon**. The empire collapsed long ago; Kenshi presents its end as a fall into atrocity and madness, and Cat-Lon survives as a dangerous, broken figure *(unsure: where he is found and exactly what he did)*. Scattered skeleton remnants still hold out in the harshest regions, such as the Ashlands *(unsure)*. The **Skeleton Legion** is one such group of old imperial soldiers *(unsure: details)*.

**The Holy Nation.** A human theocracy (the faith of Okran) that came out of the rebellion against skeleton rule. It sees skeletons as abominations, and its paladins attack them on sight. This is the main reason a skeleton in Kenshi is hated: the history of the Second Empire.

**Other skeleton places.** Black Desert City, a mostly skeleton town in a hostile, radioactive desert. The **Skin Bandits**, skeletons driven mad who wear human skin in an attempt to feel human *(unsure: region)*. Lone skeletons who drift between towns, trade their labour and keep to themselves.

**Tone.** Ancient, tired and melancholy. Skeletons are relics outliving their purpose, mistrusted for an empire most of them never ruled, and quietly competent.

### How the lore themes the mod

The original mod already leans this way (its 60 backstories include *Skeleton-Empire Citizen*, *Shunned Robot*, *Technophobic-Sect Survivor*, *Ancient Danger*, *Machinist Machine*, *Rebellious Unit*), so this is mostly naming and a few additions, not new systems.

| Kenshi idea | In Rimshi Skeletons | Notes |
| --- | --- | --- |
| Skeletons don't heal on their own | Skeleton injuries do not heal naturally; they heal once repaired with a kit (tended), and fully in a repair bed. | Mod setting, on by default. See 2. |
| Robotics skill | Crafting skill is used to repair skeletons and to make kits. | **Decision needed**, see 2. |
| Skeleton Repair Kit | `Rimshi_SkeletonRepairKit` items, the only "medicine" that works on skeletons. | See 2. |
| Skeleton Repair Bed | `Rimshi_SkeletonRepairBed`, repairs skeletons in it, used overnight. | See 3. |
| AI cores as rare research artifacts | AI persona core in the bed's build cost. | Vanilla `AIPersonaCore`, no dependency. |
| Second Empire | The original's NPC faction "skeletal imperium" becomes **remnants of the Second Empire** (label and description reworded; same roster, already using the `NamerFactionSkeletalEmpire` names). Faction description: a shadow of a once vast skeleton empire, still holding advanced technology. | The faction name maker already produces "The Fourth Iron Citadel"-style names. |
| Cat-Lon, Skeleton Legion | Light nods only: "Cat-Lon" as a rare name in the skeleton name list, and the faction's leader title "emperor" kept. Optionally a backstory *Legionnaire of the Second Empire*. | Keep proper nouns from Kenshi rare and in flavour text, not def names. |
| Holy Nation hostility | Already covered by the original's Royalty patch (Empire permanently hostile to the skeleton player faction) and the *Technophobic-Sect Survivor* backstory. Optional extra: with Ideology, tribal and outlander factions with the Human Primacy or Body Purist style memes start a little more hostile to a skeleton player faction. | Optional, low priority. Ideology under `MayRequire`. |
| Memory loss, ancient relics | New backstories (below). Optional later: a dormant skeleton as a rare reward from ancient shrine caskets. | Casket contents: **verify** how vanilla picks them. |
| Tech Hunters | Backstory *Tech Hunter's Companion*. | Flavour only. |
| Black Desert, Skin Bandits | Backstories *Black Desert Miner*, *Skin-Wearer, Recovered*. | Flavour only. |

**New backstories** to add to the 60 converted ones (category `Rimshi_Skeleton`, written in the original's voice with `[PAWN_nameDef]`):

- *Memory-Wiped Relic* (childhood): woke in a ruin with nothing but a serial number and a mechanical habit of saluting. Bonus to construction, minus to social.
- *Second Empire Legionnaire* (childhood): served in the last skeleton legions as the empire collapsed; still marches in step. Melee and shooting.
- *Black Desert Miner* (adulthood): dug ore in a desert no human could survive. Mining.
- *Tech Hunter's Companion* (adulthood): carried packs and opened doors for scholars hunting ancient tech. Intellectual and mining.
- *Hunted by the Faithful* (adulthood): spent decades fleeing zealots who called it a demon; trusts no one. Shooting, minus social.
- *Skin-Wearer, Recovered* (adulthood): once wore human skin to feel alive, until a machinist repaired the fault. Crafting, minus social; rare and grim.
- *Self-Taught Repairer* (adulthood): with no one left to fix it, learned to fix itself and others. Crafting, the natural fit for repair kits.

## 2. Skeleton repair kits

### Items

Three tiers, mirroring herbal, industrial and glitterworld medicine so the vanilla medical care setting (no meds / herbal or worse / normal or worse / best) keeps working: `MedicalCareCategory.AllowsMedicine` only compares `MedicalPotency` against herbal and industrial medicine.

| defName | Label | MedicalPotency | MedicalQualityMax | Cost | Bench | Research |
| --- | --- | --- | --- | --- | --- | --- |
| `Rimshi_RepairKitCrude` | scrap repair kit | 0.6 (herbal) | 0.7 | Steel 4, wood 2 | Crafting spot or machining table | none |
| `Rimshi_RepairKit` | skeleton repair kit | 1.0 (industrial) | 1.0 | Steel 5, component 1 | Machining table | Machining (`Machining`, **verify**) |
| `Rimshi_RepairKitAdvanced` | advanced repair kit | 1.6 (glitter) | 1.3 | Plasteel 3, advanced component 1 | Fabrication bench | Fabrication (**verify**) |

Each is a `ThingDef` with `thingClass` `Medicine` (vanilla casts the tend target to `Medicine`), `MedicalPotency` and `MedicalQualityMax` stats, `MedicineTendXpGainFactor`, and our own thing category under Medicine so stockpiles can hold them separately. Crafted with the Crafting skill (`workSkill` Crafting, `skillRequirements` Crafting 4 / 8 for the higher tiers). Traders: added to the outlander and orbital trader stock lists under `MayRequire` where needed.

Because they are in the Medicine group (`ThingRequestGroup.Medicine` is any def with `MedicalPotency`), vanilla would happily use them on humans too. So:

- **Kits only on skeletons, medicine only on non-skeletons.** Harmony postfix on `HealthAIUtility.FindBestMedicine(healer, patient, onlyUseInventory)`: if the result does not match the patient, search again with our own filter. Simplest robust version: a prefix that, for a skeleton patient, runs the same search over skeleton kits only, and for everyone else excludes kits. The method's validator is a local lambda, so a prefix replacing the search is cleaner than a transpiler.
- Other callers that pick medicine: caravan tending and drafted "tend" orders (**verify** they all go through `FindBestMedicine`; caravan code may have its own picker). The drop-pod and inventory "take medicine" logic (`JobGiver_TakeCombatEnhancingDrug` is unrelated; pawns carrying medicine is `InventoryStockGroupDef` medicine, **verify**) should also carry kits for skeleton colonists.
- If no kit is available, a skeleton can still be "tended" without medicine (vanilla no-medicine tend, potency 0.3, max 0.7). That reads as "patched up with scrap", which fits.

### Where the Crafting skill comes in

The note "make the crafting skill" can mean two things, so here are the options. **This is the one open choice for Matthew.**

**A. Kits are crafted with Crafting; repairing still uses Medicine.** XML only (recipes) plus the medicine filter above. Doctors repair skeletons like any patient.

**B. Kits are crafted with Crafting, and repairing a skeleton also uses Crafting instead of Medicine** (Kenshi's Robotics skill). Recommended. On top of A:

- A new stat `Rimshi_SkeletonRepairQuality`, a copy of vanilla `MedicalTendQuality` with `skillNeedFactors` on Crafting instead of Medicine (same Manipulation and Sight capacity factors).
- Harmony prefix on `TendUtility.CalculateBaseTendQuality(Pawn doctor, Pawn patient, float medicinePotency, float medicineQualityMax)`: for a skeleton patient, compute the same formula with our stat in place of `StatDefOf.MedicalTendQuality`. All tend quality goes through this overload (checked).
- XP: `Toils_Tend.FinalizeTend` calls `actor.skills.Learn(SkillDefOf.Medicine, ...)` inside its init lambda. A transpiler on that lambda swaps the `SkillDefOf.Medicine` load for a helper `RimshiRepair.TendSkillFor(patient)` (the patient is captured in the closure). Same for `JobDriver_TendPatient` (`waitToil.activeSkill = () => SkillDefOf.Medicine`, which drives the skill-learning tick and the "learning" UI).
- Who does it: still the **Doctor** work type (so it is scheduled and urgent like tending). Downside: the Doctor column ranks pawns by Medicine, so the work tab's skill hint is off for skeleton repair. Acceptable for a first version.

**C. As B, but repairing skeletons is its own work.** A new `WorkGiver_Rimshi_RepairSkeleton` (a `WorkGiver_Tend` subclass that only accepts skeleton patients) placed in the **Crafting** work type, and vanilla `WorkGiver_Tend` / `WorkGiver_TendOther` skip skeletons. More faithful, but injured skeletons then wait behind whatever priority crafting has, which can get one killed. Not recommended for the default; could be a mod setting later.

Optional follow-up with B or C: surgery on skeletons (installing bionics) also uses Crafting, as the original mod's surgery recipes did (`workSkill` Crafting). That is a postfix on the surgery success chance (`StatDefOf.MedicalSurgerySuccessChance`, **verify** where `Recipe_Surgery` reads it) and the same XP swap. Not part of this first step.

### No natural healing (Kenshi rule)

`Pawn_HealthTracker.HealthTickInterval` heals injuries every 600 ticks for flesh pawns (our skeletons are the Human race, so they count as flesh): first natural healing for injuries where `Hediff_Injury.CanHealNaturally()` is true (untended and not permanent), then faster healing for tended ones (`CanHealFromTending()`, scaled by tend quality). A Harmony postfix making `CanHealNaturally()` false for skeletons gives the Kenshi rule: an untended skeleton stays damaged, a repaired one heals at the tended rate, and the repair bed fixes the rest. Mod setting **Skeletons only heal when repaired** (default on).

## 3. Skeleton repair bed

### The building

`Rimshi_SkeletonRepairBed`: a `ThingDef` with `thingClass` `Building_Bed`, styled like the vanilla hospital bed with our own texture (a frame with tools and a cable; to draw, or recolour the original mod's assets).

- `building`: `bed_humanlike` true, `bed_canBeMedical` true, `bed_defaultMedical` false (so it can be a skeleton's own bed; the player can flip it to medical), one sleeping slot, size 1x2.
- Stats: `MedicalTendQualityOffset` 0.12 (repairing a skeleton in it is better, like the hospital bed), `WorkToBuild` 9000, `MaxHitPoints` 200, `Beauty` 0, `Flammability` 0.
- **Cost:** AI persona core 1, plasteel 50, steel 80, component 6. AI persona core is the vanilla `AIPersonaCore` (comes from ancient shrines, quests and exotic traders), so no dependency.
- **Power:** `CompPowerTrader` 400 W. Without power it is just a bed.
- **Research:** `Rimshi_SkeletonRepairBed` project, prerequisites `MicroelectronicsBasics` and `Fabrication` (**verify** defNames), in its own research tab group or the main tab.
- **Deconstructing** returns the AI persona core: vanilla returns half the cost on deconstruct, which would round a single core away. Set `resourcesFractionWhenDeconstructed` 1, or a small comp that always drops the core (**verify** how `GenLeaving` rounds a count of 1).

### Who can use it

Only skeletons. `RestUtility.CanUseBedEver(Pawn p, ThingDef bedDef)` is where vanilla already keeps non-deathresters out of the deathrest casket; a Harmony postfix there returns false when the bed def has our comp and the pawn is not a skeleton. Skeletons can still use normal beds and hospital beds.

### Repairing

`CompRimshi_SkeletonRepairBed` (properties: `repairPerDay`, `repairsScars`, `restoresMissingParts`, `powerRequired`). Every 250 ticks (`CompTickRare`), for each occupant of the bed that is a skeleton, while powered:

1. Heal the worst `Hediff_Injury` that is not permanent by `repairPerDay * 250 / 60000 * pawn.HealthScale` severity. This is the same idea as Biotech's `MechRepairUtility.RepairTick` (heal one injury, then move on to missing parts), and it works whether or not the injury was tended.
2. When no fresh injuries are left, and `repairsScars` is on: heal permanent injuries (scars, old gunshots) the same way. Kenshi's bed brings a skeleton to full health, so this is on by default.
3. `restoresMissingParts`: off by default. In Kenshi a lost limb needs a robotic limb, so the bed does not regrow parts; the player installs a prosthetic or bionic as usual. Can be a mod setting.
4. When nothing is left, a "fully repaired" message (vanilla `MessageFullyHealed` style) and the skeleton gets up.

Suggested numbers: `repairPerDay` 40 (a skeleton with a 15-point gunshot and a few cuts is fixed in one night; a near-death skeleton in two or three). Tuned in testing.

The bed also gets an inspect line: "Repairing: [pawn], about N hours left" and "Unpowered" when off.

### Overnight

Skeletons have no rest need (Never sleep), so they never go to bed on their own, and `RestUtility.TimetablePreventsLayDown` never blocks them (it only applies when `needs.rest` exists). Two ways a skeleton ends up in its repair bed:

- **During the day when injured:** vanilla already sends pawns with tended, healing injuries to bed (`JobGiver_PatientGoToBed` via `HealthAIUtility.ShouldSeekMedicalRest`). With the bed set to medical or owned by the skeleton, it goes there.
- **Overnight:** a new `JobGiver_Rimshi_RepairOvernight` added to the humanlike think tree (patch on the colonist behaviour tree, next to the vanilla rest giver, **verify** the ThinkTreeDef name and insertion point). During the pawn's **Sleep** schedule hours, if the pawn is a skeleton with anything the bed can repair and it owns (or can find) a powered repair bed, it gets a `Rimshi_RepairInBed` job: a `JobDriver_LayDown` subclass that lies down without sleeping and ends when repair is done or the schedule leaves Sleep. Uninjured skeletons keep working through the night as now.

So in practice: give a skeleton a repair bed, leave the default night schedule, and it walks into the bed at night when damaged and walks out repaired.

## Soft dependencies

None new. AI persona core, components, plasteel, research projects and the medical care setting are all Core. Ideology content in 1 is under `MayRequire`. Kenshi Armory and Kenshi itself are only inspiration; nothing depends on them.

## Test log lines

New `[Rimshi][T]` lines, each with a row in the test checklist:

- `kit | craft | <kit> by <pawn> (Crafting <n>)`
- `kit | tend | <doctor> -> <skeleton> with <kit>, quality <q>, skill <skill>`
- `kit | filter | <doctor> skipped <medicine> for <skeleton>` and the reverse for a human patient
- `heal | natural-blocked | <skeleton>` (once per pawn per day)
- `bed | refused | <non-skeleton>` from `CanUseBedEver`
- `bed | repair | <skeleton> <injury> -<amount>` (throttled) and `bed | done | <skeleton>`
- `bed | overnight | <skeleton> went to bed at <hour>`

## Decisions

Open:

1. Crafting skill: **A** (craft kits with Crafting only), **B** (also repair skeletons with Crafting, under Doctor work; recommended) or **C** (repair as Crafting work).

Defaults chosen unless Matthew says otherwise: skeletons only heal when repaired (setting, on); bed repairs scars but does not regrow parts; Kenshi proper nouns only in flavour text.

## Build order (proposed)

After the core xenotype (xenotype-port.md build steps 1 to 3):

1. Repair kits: items, recipes, medicine filter.
2. Crafting-based repair (option B) and no natural healing.
3. Repair bed: def, research, comp, bed restriction.
4. Overnight job giver.
5. Lore: faction rename, new backstories, name list additions.
