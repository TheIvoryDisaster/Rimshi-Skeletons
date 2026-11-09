# Recall array and skeleton horde

Where skeletons come from: the colony can rewire Biotech's mechband antenna (the building that calls the war queen) into a **skeleton recall array**. Broadcasting the old empire's recall codes wakes a dormant skeleton legion that drops in around the array, hostile, as a raid of a hidden legion faction. A few go down intact and can be captured, interrogated and reprogrammed to join the colony. Also in this change: skeletons are blocked from reproducing by every route, not only through fertility.

Requested by Matthew on 2026-10-24. Values below are first-pass numbers for playtesting.

## Research

| Def | Value |
| --- | --- |
| `Rimshi_SkeletonRecall` "skeleton recall codes" | 2000 base cost, Industrial (no tech-level multiplier for Industrial colonies), after Biotech's `BasicMechtech`. No mechanitor needed: the array only reuses the antenna's art (Matthew, 2026-10-25). |
| Position | (12, 5.3) on the Main tab, a first guess. Startup logs its position, Basic mechtech's, and any project it overlaps (`horde | research`), so it can be moved after one log scan. |

## Recall array

- `Rimshi_RecallArray` copies the vanilla mechband antenna (defName `BurnoutMechlinkBooster`, label "mechband antenna"): its graphic, size, build work and placement, with its own comps, cost (120 steel, 6 components, 25 plasteel) and research. It used to inherit from the antenna through a Name patch, but the patch named the wrong defName, the array lost its thingClass, and saves stopped loading (2026-10-25), so it no longer depends on any patch. Startup checks the graphic still matches the antenna (`horde | array graphic`).
- Use: `CompUsable` (the vanilla `UseItem` job, 10 seconds) plus `CompUseEffect_RecallArray`. Any colonist can use it (no mechanitor needed, unlike the antenna's boss call), but not while a horde is already on its way.
- Effect: plays the antenna's own call effecter (read from its `CompProperties_Useable_CallBossgroup`), sends a small threat letter, burns out into 2 steel slag, and queues a horde on `RimshiGameComponent` for 1 to 6 in-game hours later. The queue is saved with the game.

## The horde

- Size: the storyteller's threat points (at least 1000) times 0.8, plus 20% per earlier broadcast in this game (up to +100%), spent on pawn combat power; at least 5 and at most 32 skeletons. The 0.8 and the limits (from 6 and 40) cut every horde by a fifth after the first real one overran Matthew's colony (2026-11-09). Drop pods land around the array (a random drop spot if that cell is gone).
- Kinds (`Defs/PawnKindDefs/Rimshi_HordeKinds.xml`), generated with no faction:

| Kind | Weight | Combat power | Gear |
| --- | --- | --- | --- |
| Ancient legionary | 50% | 130 | `SpacerGun` (charge rifle or charge lance) |
| Ancient bladeguard | 35% | 150 | `UltratechMelee` (Royalty plasmasword, monosword, zeushammer) and a shield belt |
| Ancient centurion | 15% | 210 | `SpacerGun`, Excellent gear, more armour money |

  All wear `IndustrialMilitaryAdvanced` and `SpacerMilitary` apparel (marine and power armour) with good quality gear and a chance of advanced bionics. Without Royalty, startup adds `MedievalMeleeAdvanced` to the bladeguard so it still gets the best medieval weapons (`horde | melee`); shield belts and charge weapons are core.
- **Recalled legion** (`Rimshi_RecallLegion`, revised 2026-11-06 at Matthew's request after the first real horde): the horde used to be factionless and was made berserk by its protocol once it had landed, so it arrived peaceful and missed the raid mechanics (raid AI, alerts, "enemies present" checks), and then wandered or walked off the map. Now every horde skeleton is generated in a hidden, permanently hostile faction (one per world, created on first use in older saves: `horde | legion added`) and drops in under a `LordJob_AssaultColony` lord (no kidnapping, stealing or fleeing), so it is hostile in its pod and fights like any raid. The faction has no pawn group makers, so the storyteller never sends it on its own. Factionless horde skeletons left on a map from an older save join the legion with a new assault lord on load (`horde | migrated`).
- **Corrupted recall protocol** (`Rimshi_CorruptedProtocol`, about two in three horde skeletons; Matthew: at least a one in three chance per skeleton to be recruitable, and every horde has at least one): sets `Pawn_GuestTracker.Recruitable` to false, so normal recruiting is hidden and does not work, and captured skeletons can only be won over by reprogramming. Skeletons without it are recruitable the normal way and are never unwaveringly loyal. The protocol no longer touches mental states.
- **Failsafe core** (`Rimshi_FailsafeCore`, 10% of each horde, at least one): sets vanilla's `Pawn_HealthTracker.overrideDeathOnDownedChance` to 10%, so a failsafe skeleton that goes down is almost never finished off (vanilla's chance for non-colonists is usually much higher). Pain x1.5 brings it down a little earlier, and bleeding x0.4 keeps it alive while it waits to be rescued. A headshot or destroyed core part still kills it; `horde | failsafe died` logs those so the odds can be tuned.

## Reprogramming

- Captured horde skeletons are switched once to the **interrogate and reprogram** prisoner interaction (`Rimshi_Reprogram`). The option only shows for corrupted skeletons. Escaped or released prisoners behave like any hostile prisoner.
- Wardens do it on vanilla's prisoner chat schedule (`ScheduledForInteraction`, about twice a day); a player order skips the wait. Each session: 10 seconds next to the prisoner, then the corruption drops by 12% + 2.5% per Intellectual level (about 4 sessions at Intellectual 10, 2 at 20) and the warden gains Intellectual xp.
- At zero: protocol and failsafe core removed, mental state ended, recruitable again, `RecruitUtility.Recruit` into the player faction, positive letter.

## Skeletons cannot reproduce

Fertility 0, Asexual and no gender already stop Biotech pregnancy. Added for any pawn with the null chromosome gene: `PregnancyUtility.CanEverProduceChild` refuses, `PregnancyChanceForPawn` and `PregnancyChanceImplantEmbryo` return 0, and `Pawn_HealthTracker.AddHediff` refuses a `Hediff_Pregnant` (dev mode or another mod).

## Breaking point: the legion turns on itself

Added 2026-11-09 at Matthew's request, with the size cut above. Vanilla has no "the colony is losing" signal to reuse: the nearest is the game-over check, which only fires once every colonist is down or dead, and raid retreat triggers are about the raiders' own losses. So the rule is ours (`HordeInfighting`):

- Every 250 ticks, on each map with free legion skeletons (spawned, not prisoners): if at least half of the map's spawned free colonists are downed (and at least one), every free legion skeleton there gets the **recall frenzy** (`Rimshi_HordeFrenzy`). Colonists who are dead no longer count, so losses push the colony over the line sooner. The first time on a map a positive letter says the legion is turning on itself (`horde | infighting`).
- It is latched: the frenzy stays when the colonists get back up. It is removed if the skeleton is captured or joins the colony. A horde arriving while the colony is already losing gets it straight away.
- The frenzy keeps the skeleton berserk (restarted every 250 ticks if it recovers; `horde | frenzy`). Vanilla berserk (`MentalState_Berserk.ForceHostileTo` is always true) makes a pawn hostile to everyone, its own faction too, and the mental state branch of the think tree runs ahead of its raid duty.
- A `JobGiver_Berserk.TryGiveJob` prefix (a virtual override, so 1.7 cannot inline it away) gives frenzied skeletons the closest reachable other legion skeleton that is still up, shooting with a gun (moving to a firing spot if needed) and charging with a melee weapon (`horde | infight`, once per skeleton per in-game hour). With no legion skeleton left in reach it falls back to vanilla berserk, which only melees whoever is within 40 cells and can still hit colonists.
- The 50% is `HordeInfighting.DownedFraction`, a first guess.

## Tests

Self-tests: `cannot reproduce`, `pregnancy refused`, `recall array`, `horde faction`, `horde protocol`, `horde recruitable`, `horde gear`, `horde peace`, `horde size`, `colony losing`, `horde frenzy`, `failsafe`, `reprogram session`. Debug actions > Rimshi Skeletons > "Summon skeleton horde now" drops a horde at the mouse. In-play lines: `horde | array used`, `arrived` (faction, lord, recruitable count), `legion added`, `migrated`, `infighting`, `frenzy`, `infight`, `failsafe`, `failsafe died`; `reprogram | captured`, `session`, `joined`; `reproduction | pregnancy blocked`.

## Checked and not checked

Vanilla signatures were checked against the 1.6 reference assemblies (Krafs.Rimworld.Ref 1.6.4871) and the mod compiles against them. Method bodies were not available in this session, so these are assumptions to confirm in play or against the decompiled source on Matthew's PC: `UseItem` drives a building's `CompUsable`; an override of -1 means "no override".

## Open questions

- Should the array cost an AI persona core or a signal chip instead of plasteel?
- Should reprogrammed skeletons keep a trace (a thought or a trait), or come over clean?
