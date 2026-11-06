# Test checklist

What has been seen working in game, read from the logs. Every test-worthy event writes a line to Player.log in a fixed shape:

    [Rimshi][T] area | key | detail

A failure writes the same line as a warning with `FAILED:` at the start of the detail. The game also logs a build line at startup (version, build number, who built it and when, which DLCs are active), so every result is tied to a build.

**How to use it:** play with Development mode on, then tell Claude to check the logs. Claude pulls Player.log and Player-prev.log from `C:\Users\Matthew\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios`, runs `tools/rimshi_testlog.py` over them and updates this table. Results add up across sessions; any exception that mentions the mod is reported too. A PR that only updates this file is merged by Claude without asking, and its description lists every test that ran and its outcome.

**Self-tests:** with Development mode on, loading or starting a game runs the in-game self-tests once per session (also on demand: Debug actions > Rimshi Skeletons > Run self-tests). They generate a skeleton, a baseliner and a skeletal imperium town guard off the map, check genes, looks, diet, thoughts, immunity, the gene lock, the prosthetics lock, skeleton parts (install bills, self-installed limbs, KLR arm armour, AI core repair), machine organs (fitted, named, hidden, replaceable), blood and ancient oil, grey wound overlays, the drug lock, skeleton remains (inedible, no rot, no butchering, the scrap recipe and its yield), backstory, faction, reproduction, the recall array and horde skeletons (gear, protocol, failsafe core, one reprogramming session), discard the pawns, and log one `selftest` line per check plus a summary.

**Unit tests:** `python3 -m unittest discover -s tests` runs the offline tests (def cross-references, textures, checklist coverage, the log reader, house style). CI runs them on every push.

**Adding tests:** when code logs a new test line, add a row to `CHECKLIST` in `tools/rimshi_testlog.py` and to the table below.

**Status:** passed, failed, passed after fail (it failed once and later worked), or not seen.

| Area | Item | Status | Build | Last line |
| --- | --- | --- | --- | --- |
| build | Build line at startup | passed | 52 | version dev, build 52 (DESKTOP-ESRRCQ3 2026-11-04T01:39Z), dll written 2026-11-04 01:39 UTC; biotech True, royalty True, ideology True, odyssey True |
| defs | Blocked thoughts linked at startup (lists any not loaded) | passed | 52 | 27 of 30 blocked; not loaded: RG_Wet, RG_ExtremelyWet, RG_HotSpringWet |
| defs | Vanilla hunger thought nullified by component maintenance | passed | 52 | NeedFood nullified by Rimshi_FuelReactor |
| defs | Immunity list linked at startup (lists any not loaded) | passed | 52 | 23 of 23 immunities |
| defs | Component tagged as the maintenance item (nutrition, days one lasts) | passed | 52 | ComponentIndustrial: nutrition 0.7, preferability MealAwful, one lasts 2 days (0.35 maintenance a day) |
| defs | Needs from other mods removed or relabelled (Dubs Bad Hygiene), or says none | passed | 52 | Rimshi_MechanicalNature HygieneRateMultiplier x0.5, Rimshi_MechanicalNature removes DBHThirst, Rimshi_MechanicalNature removes Bladder, Rimshi_MechanicalNature  |
| defs | Skeleton backstories loaded | passed | 52 | 37 skeleton backstories |
| defs | Outlander, pirate, tribe and Empire xenotype sets include skeletons | passed | 52 | OutlanderCivil 1%, OutlanderRough 1%, TribeCivil 0.5%, TribeRough 0.5%, TribeSavage 0.5%, Pirate 1%, Empire 1%, TribeCannibal 0.5%, NudistTribe 0.5%, CannibalPi |
| patch | Every patched private method found (this line only appears when one is missing) | not seen |  |  |
| gene | Null chromosome makes a pawn genderless | passed | 52 | Katsunaga: Male to None, body Thin |
| gene | Skeleton frame gives a male, thin or hulk body and a skeleton head | passed | 52 | Katsunaga: body Thin, head Rimshi_Head_Mki |
| cores | A skeleton's genetics screen opened, reading cores and chips | passed | 39 | Beom's genetics screen reads cores / chips |
| gene | Skeletons generated without family | passed | 52 | Katsunaga: relation generation skipped |
| age | New skeleton made ancient (biological 120 or more, chronological 400 to 1200) | passed | 52 | Katsunaga (generated): biological 48 to 147, chronological 48 to 1006 |
| age | Skeleton skipped the human old age conditions (frail, bad back, cataract, dementia) | passed | 52 | Katsunaga: birthday conditions skipped at biological age 48 |
| gene | Removing the null chromosome gives a gender back | passed | 52 | Aranita: now Female |
| gene | Relations a new genderless pawn got another way were removed (names them) | passed | 52 | the Evil: Parent Aaltje, Parent Spearing |
| gene | A skeleton got back core genes its xenotype lists but it had lost (names them) | passed | 52 | Aranita (self-test): Rimshi_MechanicalNature |
| gene | Skeletons in a save that had lost core genes got them back on load | not seen |  |  |
| gene | A core gene was removed from a skeleton, and by what (diagnostic; the gene removal self-test causes one) | passed | 52 | Aranita: Rimshi_NullChromosome removed by .RimWorld.TraitSet.RemoveTrait_Patch4 < TraitSet.Notify_GeneRemoved < .RimWorld.Pawn_GeneTracker.RemoveGene_Patch1 < S |
| faction | Mod setting on: old Rim-Skeletons faction replaced by ours on load | not seen |  |  |
| faction | Mod setting on: save had no old Rim-Skeletons factions | not seen |  |  |
| faction | Debug action added the skeletal imperium to the world | not seen |  |  |
| faction | Skeletal imperium present in the loaded world (or says none) | passed | 52 | Damascus City |
| migrate | Mod setting on: an old Rim-Skeletons skeleton converted to ours (lists what changed) | passed | 52 | Arrogant: race Skeleton_ to Human, 0 health conditions moved, biological age 1289 to 120, xenotype voxex to skeleton, childhood ChildhoodSkeleton3 to Rimshi_Ske |
| migrate | Health condition with no matching human body part dropped during conversion (names it) | not seen |  |  |
| migrate | Mod setting on: conversion summary (skeletons converted, conditions dropped) | not seen |  |  |
| migrate | Mod setting on: save had no old Rim-Skeletons skeletons | not seen |  |  |
| migrate | Mod setting on: Rim-Skeletons (Continued) not loaded, nothing to convert | not seen |  |  |
| render | Skeleton body texture drawn | passed | 52 | the Blunt: Rimshi/Body/Naked_Thin, head Rimshi_Head_Scout, skin RGBA(0.431, 0.431, 0.431, 1.000), gender None, xenotype skeleton |
| repair | Crafter started repairing a skeleton with repair kits | passed | 52 | Arinaga repairing Akifumi: Bruise (human) on left arm, 0.83 per second, carrying 2 kits |
| repair | Repair used up a kit | passed | 52 | Arinaga used a repair kit on Akifumi |
| repair | Repair sealed leaks or treated a tendable condition with kits | passed | 52 | Iambulus treated Worn away on the Arrogant with repair kits, quality 83% |
| repair | Skeleton fully repaired by a crafter | passed | 52 | Arinaga finished repairing Akifumi |
| bed | Non-skeleton kept out of a skeleton repair bed | passed | 52 | Rafael is not a skeleton |
| bed | Repair bed repairing a skeleton's injury | passed | 52 | the Bleak: Old gunshot (painful) on torso -0.333 per rare tick |
| bed | Skeleton fully repaired in a repair bed | not seen |  |  |
| diet | Skeleton consumed a component for maintenance (fails if it ate anything else) | passed after fail | 52 | the Brave consumed 1 ComponentIndustrial, maintenance now 24% |
| maintenance | Skeleton out of maintenance did not get malnutrition | passed | 52 | Akifumi: out of maintenance, malnutrition refused |
| maintenance | Need bar and tooltips relabel needs (this line only appears when a patch target is missing) | not seen |  |  |
| maintenance | Components allowed in the save's food policies and Skeletons policy added (once per save) | not seen |  |  |
| maintenance | Skeleton picking up a component to carry | passed | 52 | the Destitute picking up 1 ComponentIndustrial to carry, from (146, 0, 181) |
| maintenance | Skeleton a full day out of maintenance wore a random non-brain part | passed | 52 | Goichi: out of maintenance, ribcage worn by 2 (worn total 2 of 30) |
| softcycle | Skeleton went to soft cycle in a repair bed (gives the cycle's power draw) | not seen |  |  |
| softcycle | Skeleton soft cycling in a powered repair bed (48 hours, power draw, integrity) | passed | 52 | the Destitute soft cycling in skeleton repair bed (masterwork), 2 days, drawing 765 W, integrity 7% |
| softcycle | Soft cycle held outside a repair bed (carried, in a rib cage, older save) | not seen |  |  |
| softcycle | Skeleton due a soft cycle found no usable repair bed (gives the reason) | passed | 52 | the Arrogant is due a soft cycle but no repair bed is usable (wrong bed type) |
| softcycle | Soft cycle finished in a repair bed (integrity 100%, memories flushed, skill lost, +3 thought) | passed | 52 | the Destitute: integrity 100%, overload cleared False, 15 memories forgotten, skill lost: construction -3320 XP, thought Rimshi_SoftCycleHoned |
| softcycle | Bed soft cycle cut short: power lost or woken early (integrity kept, no overload) | passed | 52 | Aranita: self-test, integrity kept at 40% |
| softcycle | Skeleton began a soft reset standing or hanging in a rib cage | passed | 52 | Sumimoto began a soft reset hanging in a rib cage, integrity 2.9% |
| softcycle | Soft reset finished without a repair bed (integrity 100%, -3 thought) | passed | 52 | Nillina: integrity 100%, overload cleared False, 29 memories forgotten, skill lost: shooting -2615 XP, social -3063 XP, thought Rimshi_SoftResetForeign |
| softcycle | Soft reset cancelled: woken, downed or a mental break (integrity 0%) | passed | 52 | Aranita: self-test, integrity 0% |
| softcycle | A skeleton in a standing soft cycle refused a player order (names the job) | passed | 52 | Sumimoto refused Equip while soft resetting |
| softcycle | A skeleton in a standing soft cycle was not drafted | passed | 52 | Sumimoto was not drafted while soft resetting |
| softcycle | Soft cycle mending its injuries toward getting up, legs first (gives the amount needed and time left) | passed | 52 | the Doubtful needs 30.5 injury mended to get up, 8 seconds of cycle left, first left leg (15.251 this step) |
| softcycle | Soft cycle finished and mended just enough to get up | passed | 52 | the Doubtful mended enough to get up as the cycle ended (last 15.25), downed False |
| softcycle | Soft cycle mends nothing: no legs left, or could not get up even fully mended | passed | 52 | the Doubtful has no legs left, the cycle mends nothing |
| softcycle | Skeleton that began a soft reset downed got up and carries on with it standing | not seen |  |  |
| softcycle | Skeletons with deathrest exhaustion from an older save got CPU overload instead | not seen |  |  |
| facility | A soft cycle facility joined a soft cycle in a linked repair bed (a tuning fork burnt an advanced component, gives the cycle length factor) | not seen |  |  |
| facility | A soft cycle facility let go of a skeleton when its soft cycle ended (finished or cut short) | not seen |  |  |
| spire | A chemical spire finished charging (or was charged by the dev gizmo); it now holds the charge | not seen |  |  |
| spire | A chemical spire lost power while charging and its charge started over (chemfuel kept) | not seen |  |  |
| spire | A soft cycle a charged spire joined finished: the skeleton took the held tempest (EMP-proof, x1.1 speed, burst) | not seen |  |  |
| spire | A skeleton loosed the tempest: radius 10, hostile mechs and skeletons hit, damage from summed skill levels, stunned count | not seen |  |  |
| fissure | A cable fissure's tuning bill finished (or the dev gizmo tuned it); it now draws 0 W and can join a soft cycle | not seen |  |  |
| fissure | A soft cycle a tuned cable fissure joined finished: the skeleton is overclocked (+20 carrying capacity, heave) | not seen |  |  |
| fissure | A skeleton heaved an adjacent enemy: disarmed, thrown carrying capacity / 10 cells, a hit per cell on different parts, double per cell blocked by a wall | not seen |  |  |
| spire | A held tempest or its wake ended at the next finished soft cycle | passed | 52 | Aranita's Rimshi_TempestWake ended: self-test |
| deathrest | Skeletons from an older save got the deathrest gene | not seen |  |  |
| overload | CPU overload started (integrity ran out or a cycle was cancelled) | passed | 52 | Aranita: soft reset cancelled: self-test |
| overload | CPU overload moved to its next stage (steady at day 2, paranoid at day 4) | passed | 52 | Akifumi now vigilant after 4.0 days |
| overload | Paranoid overloaded skeleton limited to violent mental breaks | passed | 52 | Akifumi can only break violently: Berserk, BerserkShort |
| overload | CPU overload cleared by a finished soft cycle | passed | 52 | Aranita cleared after 5.5 days |
| ribcage | Skeleton hanging in a rib cage (needs frozen) | not seen |  |  |
| reproduction | A pregnancy added to a skeleton was refused | passed | 52 | Aranita: PregnantHuman refused |
| horde | Recall array inherited the mechband antenna graphic | passed | 52 | Rimshi_RecallArray uses Things/Building/Misc/MechbandAntenna, size (2, 1), research Rimshi_SkeletonRecall |
| horde | Recall research on the research tab, overlapping nothing (gives its position) | failed | 52 | FAILED: Rimshi_SkeletonRecall at (12, 5.3) on Main, cost 2000, needs BasicMechtech at (9, 5.4) on Main; overlaps Seg_AutoWeapons |
| horde | Bladeguard melee weapon tags (adds medieval weapons without Royalty) | passed | 52 | bladeguard weapon tags UltratechMelee |
| horde | Colonist broadcast the recall codes; array burned out | not seen |  |  |
| horde | Skeleton horde dropped in (counts, kinds, weapons, shield belts, failsafe cores) | not seen |  |  |
| horde | Horde called to a map that is gone was dropped | not seen |  |  |
| horde | Horde skeleton went berserk | not seen |  |  |
| horde | Failsafe core set the death on downed chance | passed | 52 | Halsit: death on downed chance 10% (was -100%) |
| horde | A failsafe skeleton died anyway (says how; not a failure) | not seen |  |  |
| reprogram | Captured horde skeleton set to interrogate and reprogram | not seen |  |  |
| reprogram | Warden reprogramming session lowered the corruption | passed | 52 | Barr reprogrammed the Smiling: -22%, 78% corruption left |
| reprogram | Reprogrammed skeleton joined the colony | not seen |  |  |
| geneproof | Xenogerm implant on a skeleton refused | not seen |  |  |
| geneproof | Xenogerm reimplant ability on a skeleton refused | not seen |  |  |
| geneproof | Skeleton refused the absorb xenogerm job | not seen |  |  |
| geneproof | No xenogerm reached a skeleton past the checks (this line only appears when one did) | not seen |  |  |
| geneproof | No reimplant involving a skeleton got past the checks (this line only appears when one did) | not seen |  |  |
| geneproof | Skeleton genes kept out of a genepack (fails if a pack held only skeleton genes) | passed | 52 | 1 skeleton gene(s) kept out of a genepack |
| prosthetics | Generated skeleton got no prosthetics, bionics or implants | passed | 52 | Aranita (Colonist): no tech hediffs generated for a skeleton |
| prosthetics | Skeleton refused a self-installed implant (neuroformer or another mod's; mechlinks still allowed) | not seen |  |  |
| prosthetics | A psylink was refused on a skeleton (royal title, bestowing, anima tree, dev mode) | passed | 52 | Aranita: PsychicAmplifier refused |
| prosthetics | No install surgery reached a skeleton past the checks (this line only appears when one did) | not seen |  |  |
| parts | Skeleton fabrication and advanced parts research placed on the research tab without overlaps | passed | 52 | Rimshi_AdvancedSkeletonParts at (11, 7.2) on Main |
| parts | A skeleton part was installed on a skeleton (names the part) | passed | 52 | Aranita: Rimshi_AICore on Brain |
| parts | A KLR or industrial part took reduced damage (first hit each session) | passed | 52 | Aranita: Shoulder hit for 9.5, Rimshi_KLRArm made it 7.6 |
| parts | A steady arm knocked a weapon out of an enemy's hands in melee | not seen |  |  |
| parts | A second AI core repaired a damaged one (names what it removed) | passed | 52 | Aranita: removed Gunshot |
| parts | A skeleton fitted a limb to itself from the item, no surgery (names the limb, the part and whether the side was picked) | passed | 52 | Aranita: Rimshi_StandardLeg on Leg (auto) |
| anatomy | A skeleton got its machine organs (generation, gene added or older save; names the organs) | passed | 52 | Katsunaga (gene added): Stomach |
| anatomy | Skeletons in an older save were given machine organs on load | not seen |  |  |
| powerdown | A temporarily power down operation finished on a skeleton (says if it is in maintenance mode) | not seen |  |  |
| powerdown | A skeleton got maintenance mode where it would have got anesthetic (first time each session) | passed | 52 | Aranita got maintenance mode instead of anesthetic |
| blood | A hit skeleton leaked ancient oil (black filth) instead of blood | passed | 52 | the Arrogant leaked ancient oil at (171, 0, 188) |
| module | An EMP hit stunned a skeleton the way it stuns a mechanoid (unshielded circuits) | passed | 52 | Aranita stunned by EMP like a mechanoid |
| module | Skeletons from an older save got the module genes their xenotype now lists | passed | 43 | 24 modules added to skeletons from an older save |
| blood | A hit skeleton showed the black oil spray instead of the red blood spray | passed | 52 | Old: black hit spray instead of blood |
| blood | A sanguophage bit a skeleton and got no hemogen (bloodfeed ability or another mod) | not seen |  |  |
| wounds | A wounded skeleton on the map showed grey and black wound marks instead of red ones | passed | 52 | Old: Cut drawn as a grey and black wound instead of a red one |
| drugs | A skeleton was refused a drug (ingest menu, drug policy, recreation, AI job, binge or generation; one line per route) | passed | 52 | generation GiveCombatEnhancingDrugs: Halsit refused |
| addiction | An addiction or chemical dependency was refused on a skeleton (one line per hediff) | passed | 52 | Aranita: VREH_GeneticDrugNeed_Luci not added |
| addiction | A skeleton lost an addiction it already had (older save or gene added) | passed | 52 | Aranita (self-test): AlcoholAddiction |
| addiction | Loading a save removed addictions from skeletons (only appears when there were some) | not seen |  |  |
| drugs | No drug reached a skeleton past the checks (this line only appears when one did; the drug did nothing) | not seen |  |  |
| remains | A skeleton corpse was scrapped for steel, plasteel, uranium and at most one component (names the yield) | passed | 52 | Aranita yielded 2 Steel, 1 Plasteel, 1 Uranium |
| remains | A butchery bill skipped a skeleton corpse (butcher spot, butcher table or another mod's) | passed | 52 | ButcherCorpseFlesh skipped the corpse of Aranita |
| remains | A pawn or animal that went to eat a skeleton corpse (a predator after a hunt) stopped instead | not seen |  |  |
| remains | No skeleton corpse was eaten past the checks (this line only appears when one got that far; nothing was eaten) | not seen |  |  |
| remains | A skeleton corpse that had rotted or desiccated before rot was disabled became fresh again (on load or spawn) | not seen |  |  |
| remains | A scrap skeleton remains bill found nothing while a skeleton corpse lay on the map; names why (forbidden, filter, radius, reserved, unreachable) | not seen |  |  |
| selftest | Self-tests: all passed (summary line) | passed after fail | 52 | 150 passed, 0 failed |
| selftest | Self-test: skeleton and baseliner generate | passed | 52 | skeleton Aranita, baseliner Barr |
| selftest | Self-test: no exceptions while testing | not seen |  |  |
| selftest | Self-test: skeleton has the Skeleton xenotype | passed | 52 | xenotype skeleton |
| selftest | Self-test: skeleton is genderless | passed | 52 | gender None |
| selftest | Self-test: skeleton is biologically 120 or more and chronologically 400 to 1200 | passed | 52 | biological 152, chronological 516, life stage HumanlikeAdult |
| selftest | Self-test: baseliner age left alone | passed | 52 | baseliner biological 28, chronological 3061 |
| selftest | Self-test: skeleton gets no human old age conditions | passed | 52 | 9 birthday conditions checked; rolled: none; has: none |
| selftest | Self-test: skeleton is Asexual | passed | 52 | traits ShootingAccuracy, RT_TalentPhysician, Asexual |
| selftest | Self-test: skeleton body is male, thin or hulk | passed | 52 | body Thin |
| selftest | Self-test: skeleton has a skeleton head | passed | 52 | head Rimshi_Head_Mki |
| selftest | Self-test: skeleton has a plating colour | passed | 52 | skin RGBA(0.294, 0.294, 0.294, 1.000) |
| selftest | Self-test: skeleton is bald | passed | 52 | hair Bald |
| selftest | Self-test: skeleton body texture found | passed after fail | 52 | path Rimshi/Body/Naked_Thin |
| selftest | Self-test: skeleton head texture found | passed after fail | 52 | path Rimshi/Head/Mki |
| selftest | Self-test: skeleton has no rest need | passed | 52 | rest need absent |
| selftest | Self-test: skeleton generated without family | passed after fail | 52 | direct relations 0 |
| selftest | Self-test: skeleton fertility is 0 | passed | 52 | fertility 0 |
| selftest | Self-test: skeleton toxic resistance 100% | passed | 52 | toxic resistance 1 |
| selftest | Self-test: skeleton psychic sensitivity 0% | passed | 52 | psychic sensitivity 0 |
| selftest | Self-test: skeleton immune to flu | passed | 52 | immune to flu |
| selftest | Self-test: skeleton can consume components | passed | 52 | component suitable True |
| selftest | Self-test: skeleton refuses meals | passed | 52 | simple meal suitable False |
| selftest | Self-test: one component lasts a skeleton 2 days | passed | 52 | one component lasts 2.00 days |
| selftest | Self-test: skeleton consumes one component at a time | passed | 52 | takes 1 at once |
| selftest | Self-test: skeleton food bar shown as Maintenance with the Second Empire tooltip (human keeps Food) | passed | 52 | skeleton Maintenance, human Food |
| selftest | Self-test: skeleton cannot get malnutrition | passed | 52 | malnutrition False |
| selftest | Self-test: vanilla hunger thought nullified for skeleton | passed | 52 | NeedFood nullified |
| selftest | Self-test: maintenance thought can apply to skeleton | passed | 52 | Rimshi_MaintenanceNeed can apply |
| selftest | Self-test: with Dubs Bad Hygiene, no thirst or bladder and self-cleaning at half rate (passes when not loaded) | passed | 52 | thirst and bladder removed True, hygiene shown as self-cleaning, hygiene rate 0.5 |
| selftest | Self-test: skeleton has deathrest (gene, need, icon); human does not | passed | 52 | skeleton deathrests True, need True, human deathrests False |
| selftest | Self-test: deathrest need shown as CPU integrity with the soft cycle tooltip | passed | 52 | need shown as CPU integrity |
| selftest | Self-test: repair bed draws 15 W per skill level for a cycle and has no chemfuel tank | passed | 52 | cycle draw 15 W per skill level (480 W for the test skeleton), chemfuel tank False |
| selftest | Self-test: a bed soft cycle takes 48 hours | passed | 52 | bed cycle 2 days |
| selftest | Self-test: soft reset is slow for 3 days and speeds up to 100% at 5 days | passed | 52 | after 3 days 16%, after 4 40% |
| selftest | Self-test: a cycle raises integrity in step and ends at 100% | passed | 52 | 50% integrity, half a cycle 75%, whole cycle 100% |
| selftest | Self-test: a bed soft cycle cut short keeps its integrity and causes no overload | passed | 52 | bed cycle cut short: integrity 40%, overload False |
| selftest | Self-test: a cancelled standing cycle leaves 0% integrity and a CPU overload | passed | 52 | standing cycle cut short: integrity 0%, overload True |
| selftest | Self-test: needs other than CPU integrity are frozen during a soft cycle | passed | 52 | while soft cycling: maintenance frozen True, CPU integrity frozen False |
| selftest | Self-test: a skeleton at 0% CPU integrity gets no vanilla deathrest exhaustion thought | passed | 52 | vanilla thought DeathrestExhaustion active False at integrity 0% |
| selftest | Self-test: skeleton shows the CPU integrity gizmo and one soft cycle button instead of vanilla's deathrest gizmo | passed | 52 | gizmos GeneGizmo_Rimshi_CpuIntegrity |
| selftest | Self-test: orders, drafting and the draft button are guarded during a standing soft cycle | passed | 52 | orders, drafting and the draft button are guarded during a standing soft cycle |
| selftest | Self-test: the CPU integrity tooltip is skeleton lore with no vanilla deathrest text | passed after fail | 52 | integrity tooltip: <color=#E6E64CFF>CPU integrity: 0%</color> / <color=#FF3333FF>Overloaded.</color> /  / A skeleton's mind runs on a CPU and a memory buffer th |
| selftest | Self-test: the rib cage texture is drawn unrotated and without the medical sheet tint | passed | 52 | rib cage drawRotated False, sheet colour False |
| selftest | Self-test: overload stages (sluggish x0.8, recalibrating at day 2, vigilant x1.1 at day 4, -1 mood a day) | passed | 52 | moving x0.8 sluggish, stage 1 at day 3, paranoid True moving x1.1, penalty -2 on day 5.5 |
| selftest | Self-test: vigilant (paranoid) thought has the four lines and applies to the skeleton only | passed | 52 | paranoid thought active True |
| selftest | Self-test: violent break filter patched (berserk counts, sad wander does not) | passed | 52 | berserk violent True, sad wander violent False |
| selftest | Self-test: a finished cycle clears the overload, fills integrity and flushes memories | passed | 52 | overload False, integrity 100%, memories Rimshi_SoftCycleHoned |
| selftest | Self-test: rib cage is a 1x2 unrotatable medical bed, 80 steel + 3 components, basic mechtech, skeletons only | passed | 52 | 1x2 unrotatable medical bed, 80 steel + 3 components, basic mechtech; skeleton can use True, human False |
| selftest | Self-test: rib cage back and front textures load | passed | 52 | Rimshi/Buildings/RibCage and Rimshi/Buildings/RibCage_front |
| selftest | Self-test: tuning fork links to the repair bed (2 max, same room), takes advanced components, 50 W idle and 1000 W ringing, art loads | passed | 52 | max per bed 2, repair bed links it True, same-room patch True, art True, power 50 W idle, 1000 W ringing, fuel 1 advanced component a cycle, cycle with one fork |
| selftest | Self-test: chemical spire is 3x3 unrotatable, links to the repair bed (1 max, same room), 100 chemfuel, 100 W for 36 h, joins unpowered, art and icon load | passed | 52 | max per bed 1, repair bed links it True, art and icon True, chemfuel 100, charge 100 W for 1.5 days, 3x3 unrotatable True |
| selftest | Self-test: cable fissure is 2x3 rotatable, links to the repair bed (1 max), costs 100 plasteel + 2 advanced components + 60 stone, 250 W untuned, tuning bill 4 components/100 steel/40 blocks/250 research points, art, masks and icon load | passed | 52 | max per bed 1, repair bed links it True, art, masks and icon True, cost (100 plasteel, 2 advanced components, 60 stone) True, untuned draw 250 W, tuning: 4 comp |
| selftest | Self-test: overclocked musculature gives +20 carrying capacity and the heave ability, heave distance is carrying capacity / 10, ability goes with the hediff | passed after fail | 52 | offset 20, carrying capacity 76.5 -> 96.9, heave ability True (gone after True), heave distance 9 cells |
| selftest | Self-test: held tempest makes a skeleton EMP-proof with x1.1 speed and the burst ability; its wake stays EMP-proof; both end, burst damage 5 to 40 | passed | 52 | EMP-proof while held True and in its wake True, burst ability True (gone after True), move speed x1.1, stunnable again after the next cycle True, burst damage 8 |
| selftest | Self-test: rib cage freezes needs and ranks between the repair bed and hospital bed for rescue | passed | 52 | need freeze patched, rescue order repair bed 5, rib cage 6, hospital bed 7 |
| selftest | Self-test: component packing patch applied | passed | 52 | packing patch applied True |
| selftest | Self-test: Skeletons food policy allows only components | passed | 52 | Skeletons policy allows ComponentIndustrial |
| selftest | Self-test: naked thought nullified for skeleton | passed | 52 | Naked nullified |
| selftest | Self-test: human refuses components | passed | 52 | component suitable False |
| selftest | Self-test: human still eats meals | passed | 52 | simple meal suitable True |
| selftest | Self-test: human keeps gender and hunger thought | passed | 52 | gender Female, hunger thought nullified False |
| selftest | Self-test: both repair research projects need Basic mechtech | passed | 52 | Rimshi_SkeletonRepairKits and Rimshi_SkeletonRepairBeds need BasicMechtech |
| selftest | Self-test: repair kit recipe needs the repair kit research | passed | 52 | recipe Make_Rimshi_RepairKit needs Rimshi_SkeletonRepairKits, Rimshi_SkeletonFabrication |
| selftest | Self-test: repair bed needs the repair bed research and an AI persona core | passed | 52 | bed needs Rimshi_SkeletonRepairBeds |
| selftest | Self-test: repair kit is not medicine | passed | 52 | repair kit is medicine False; vanilla tending never uses it |
| selftest | Self-test: skeleton injury healing is 0%, human's is not | passed | 52 | injury healing: skeleton 0%, human 100% |
| selftest | Self-test: skeleton repair is a Smith job using Crafting | passed | 52 | work giver in Smithing |
| selftest | Self-test: a cut skeleton needs repair, a cut human does not | passed | 52 | cut skeleton needs repair True (1 kit), cut human False |
| selftest | Self-test: nothing on a skeleton is tendable with medicine; a human cut still is | passed | 52 | skeleton cut tendable False, skeleton needs tending False, human cut tendable True |
| selftest | Self-test: a cut skeleton does not count as healing on its own (no fully healed message spam); a cut human does | passed | 52 | skeleton cut heals naturally False, human cut True (a skeleton that counts as healing gets a "fully healed" message every 10 s) |
| selftest | Self-test: only skeletons can use a repair bed; skeletons can still use hospital beds | passed | 52 | skeleton True, human False, skeleton in hospital bed True |
| selftest | Self-test: removing the null chromosome restores a gender | passed | 52 | gender after removing null chromosome Female |
| selftest | Self-test: no skeleton gene can roll in gene sets | passed | 52 | 14 skeleton genes, none can roll in gene sets |
| selftest | Self-test: every skeleton gene is marked core or chip, chip category loaded | passed | 52 | 14 core genes, 0 chips, chip category loaded |
| selftest | Self-test: skeleton xenotype carries the unshielded circuits and archaic speech modules, marked core and modifiable | passed | 52 | xenotype modules: Rimshi_UnshieldedCircuits, Rimshi_ArchaicSpeech |
| selftest | Self-test: EMP stuns a skeleton (and it adapts like a mech), not a baseliner | passed | 52 | after an EMP hit: skeleton stunned True (5.0 seconds), baseliner stunned False, skeleton adapts like a mech True |
| selftest | Self-test: archaic speech lowers trade price, negotiation and social impact; archaic persona no longer does | passed | 52 | archaic speech: trade price x0.5, negotiation x0.8, social impact x0.5; archaic persona social impact x1 |
| selftest | Self-test: a skeleton's genetics screen says cores and chips, a baseliner's still says genes | passed | 52 | skeleton screen reads cores / chips, baseliner screen reads germline genes |
| selftest | Self-test: character card tooltip says view cores for a skeleton only, patches applied | passed | 52 | skeleton: Click to view Aranita's cores. / baseliner: Click to view Barr's genes. |
| selftest | Self-test: 200 random gene sets hold no skeleton gene | passed | 52 | 0 of 200 random gene sets held a skeleton gene |
| selftest | Self-test: a genepack drops skeleton genes and keeps the rest | passed | 52 | genepack holds Neversleep |
| selftest | Self-test: gene buildings refuse the skeleton, not the baseliner | passed | 52 | skeleton refused True, baseliner refused False |
| selftest | Self-test: implant xenogerm bill unavailable on a skeleton | passed | 52 | implant xenogerm bill on skeleton available False |
| selftest | Self-test: every gene lock patch applied | passed | 52 | 8 gene lock patches applied |
| selftest | Self-test: skeleton has a skeleton adulthood backstory | passed | 52 | backstories Rimshi_SkeletonChildhood1, Rimshi_SkeletonAdulthood1 |
| selftest | Self-test: skeletal imperium and skeleton player faction loaded | passed | 52 | Rimshi_SkeletalImperium with 5 pawn groups, player faction Rimshi_PlayerSkeletons |
| selftest | Self-test: a skeletal imperium pawn kind generates a skeleton | passed | 52 | Rimshi_TownGuard: xenotype skeleton, name Amuro the Reserved |
| selftest | Self-test: skeleton cannot have children or be implanted | passed | 52 | child with baseliner Aranita is a skeleton and cannot have children., pregnancy chance 0 |
| selftest | Self-test: pregnancy cannot be added to a skeleton | passed | 52 | pregnancy hediff refused |
| selftest | Self-test: recall array and its research loaded (2000, after basic mechtech, no mechanitor needed) | passed | 52 | Rimshi_RecallArray needs Rimshi_SkeletonRecall (2000, after BasicMechtech, needs mechanitor False) |
| selftest | Self-test: horde skeletons have no faction | passed | 52 | Rimshi_HordeLegionary faction none skeleton, Rimshi_HordeBladeguard faction none skeleton, Rimshi_HordeCenturion faction none skeleton |
| selftest | Self-test: horde skeletons have the corrupted protocol and cannot be recruited | passed | 52 | Halsit corrupted True recruitable False, the Smiling corrupted True recruitable False, Mochida corrupted True recruitable False |
| selftest | Self-test: horde gunners carry spacer guns, bladeguards melee weapons and shield belts | passed | 52 | Rimshi_HordeLegionary: VWE_Gun_ChargeSMG, Rimshi_HordeBladeguard: MeleeWeapon_PlasmaSword + shield belt, Rimshi_HordeCenturion: VWE_Gun_ChargeShotgun |
| selftest | Self-test: horde skeletons are not hostile to each other | passed | 52 | horde skeletons not hostile to each other |
| selftest | Self-test: failsafe core sets the death on downed chance | passed | 52 | death on downed chance 10% |
| selftest | Self-test: a reprogramming session lowers the corruption | passed | 52 | corruption 100% to 78% (warden Intellectual 4) |
| selftest | Self-test: a bionics-rolling kind gives the skeleton none and the baseliner some | passed | 52 | bionics generated: skeleton 0, baseliner 2 |
| selftest | Self-test: no install surgery is offered on a skeleton (bionic arm and natural heart still offered on a baseliner) | passed | 52 | 477 install surgeries, 0 offered on the skeleton; bionic arm and natural heart offered on the baseliner True |
| selftest | Self-test: removing a body part is still offered on a skeleton | passed | 52 | remove body part offered on the skeleton True |
| selftest | Self-test: mechlink allowed and psylink neuroformer refused for skeletons | passed | 52 | mechlink allowed, psylink neuroformer refused |
| selftest | Self-test: giving a skeleton a psylink adds none and no psycast, a baseliner gets one (Royalty only) | passed | 52 | skeleton psylink False, new abilities 0; baseliner psylink True |
| selftest | Self-test: the prosthetics lock patches are applied | passed | 52 | 8 prosthetics lock patches applied |
| selftest | Self-test: every Rimshi bench with bills (skeleton workbench, cable fissure) has a work giver that takes its bills | passed | 52 | 2 Rimshi benches with bills, every one has a bill work giver |
| selftest | Self-test: every skeleton part install is offered on a skeleton and not on a baseliner | passed | 52 | 19 skeleton part installs, skeleton only |
| selftest | Self-test: a new skeleton has a machine part on all 8 organ types | passed | 52 | 8 machine organs on every organ |
| selftest | Self-test: a skeleton's organs are named after their machine parts (left lung reads left cooling fan), a baseliner's are not | passed | 52 | left lung reads left cooling fan on the skeleton, left lung on the baseliner |
| selftest | Self-test: machine organs are hidden in the health tab like natural organs | passed | 52 | machine organs hidden in the health tab |
| selftest | Self-test: the health tab, surgery menu, medical bills and combat log name parts through the machine names | passed after fail | 52 | HealthCardUtility.DrawHediffRow 2, HealthCardUtility.GetTooltip 1, HealthCardUtility.GenerateSurgeryOption 1, Bill_Medical.get_Label 1, <RulesForBodyPartRecord> |
| selftest | Self-test: a cooling fan install is offered only once the lung is gone, and puts the cooling fan back | passed | 52 | offered on a fitted lung False, on a missing lung True, cooling fan back True, alive True |
| selftest | Self-test: a fitted lung can be removed (as its cooling fan), a bare one cannot, and a restored one gets its cooling fan | passed | 52 | removal offered on a fitted lung True, on a bare lung False, cooling fan refitted on restore True |
| selftest | Self-test: a KLR arm installs on a skeleton's shoulder | passed | 52 | KLR arm on left shoulder: True |
| selftest | Self-test: all 10 skeleton limbs install themselves from the item, organs and the AI core do not | passed | 52 | 10 self-installed limbs, surgery only: Rimshi_AICore, Rimshi_CoolingFan, Rimshi_FluidReprocessor, Rimshi_Capacitor, Rimshi_OilFilter, Rimshi_FluidPump, Rimshi_O |
| selftest | Self-test: a skeleton leg is refused for a baseliner, goes on the skeleton's missing leg first, and takes | passed | 52 | baseliner refused True (only fits a skeleton), offered right leg, left leg, missing right leg first True, installed there True |
| selftest | Self-test: a KLR arm takes 20% off the damage that reaches it | passed | 52 | 10 bullet damage on the KLR arm arrived as 9.5, reduced to 7.6 (expected 7.6), injury 7.6 |
| selftest | Self-test: an AI core installs, and a second one repairs a damaged core | passed | 52 | core installed True, repair offered when damaged True, injuries left 0, alive True |
| selftest | Self-test: skeleton cannot bleed, baseliner can | passed | 52 | skeleton can bleed False, baseliner True |
| selftest | Self-test: a cut skeleton has bleed rate 0, a cut baseliner bleeds | passed | 52 | bleed rate: cut skeleton 0%, cut baseliner 36% |
| selftest | Self-test: ancient oil filth and black hit effect loaded | passed | 52 | filth Rimshi_Filth_AncientOil colour RGBA(0.078, 0.071, 0.063, 0.784), hit effect Rimshi_Damage_HitAncientOil replaces Damage_HitFlesh |
| selftest | Self-test: a cut skeleton gets a grey and black mech wound overlay, a cut baseliner a flesh one | passed | 52 | cut overlay: skeleton Things/Pawn/Wounds/WoundMechA, baseliner Things/Pawn/Wounds/WoundFleshB; mech wound textures found True |
| selftest | Self-test: the blood patches are applied | passed | 52 | 8 blood patches applied |
| selftest | Self-test: a skeleton cannot take beer or penoxycyline, a baseliner can | passed | 52 | beer: skeleton False, baseliner True (teetotaler False); penoxycyline suitable: skeleton False, baseliner True |
| selftest | Self-test: a policy allowing every drug schedules and carries none for the skeleton, some for a baseliner | passed | 52 | policy allowing every drug: skeleton schedules or carries 0, baseliner 97 |
| selftest | Self-test: no administer drug bill is offered on a skeleton, some are on a baseliner | passed | 52 | 97 administer drug bills, 0 offered on the skeleton, 97 on the baseliner |
| selftest | Self-test: no drug binge mental state can start on a skeleton | passed | 52 | 2 drug binge states, possible on the skeleton: none |
| selftest | Self-test: the drug lock patches are applied | passed | 52 | 16 drug lock patches applied |
| selftest | Self-test: an addiction cannot be added to a skeleton, it can to a baseliner | passed | 52 | AlcoholAddiction: skeleton False, baseliner True |
| selftest | Self-test: a chemical dependency gene on a skeleton adds no dependency hediff | passed | 52 | VREH_ChemicalDependency_Luciferium: gene added True, dependency hediff False |
| selftest | Self-test: an addiction already on a skeleton is removed | passed | 52 | removed 1, still there False |
| selftest | Self-test: the addiction lock patches are applied | passed | 52 | 3 addiction lock patches applied |
| selftest | Self-test: every old skeleton body part maps to its own human part (old mod loaded) | passed | 52 | 30 old parts, unmapped: none, shared targets 0 |
| selftest | Self-test: converted old skeleton is race Human with the human body | passed | 52 | race Human, body Human |
| selftest | Self-test: converted old skeleton has the Skeleton xenotype and no gender | passed | 52 | xenotype skeleton, gender None |
| selftest | Self-test: converted old skeleton has our head and backstories | passed | 52 | head Rimshi_Head_Screamer, backstories Rimshi_SkeletonChildhood3, Rimshi_SkeletonAdulthood11 |
| selftest | Self-test: converted old skeleton has our pawn kind | passed | 52 | kind Rimshi_Colonist |
| selftest | Self-test: converted old skeleton has no old hediff and only human body parts | passed | 52 | 12 health conditions, lost 0 |
| selftest | Self-test: a converted skeleton is not converted again | passed | 52 | converted pawn no longer matches |
| selftest | Self-test: a converted skeleton is biologically 120 or more | passed | 52 | biological 120, chronological 1289 |
| selftest | Self-test: a skeleton corpse cannot be eaten, a baseliner corpse can | passed | 52 | ingestible: skeleton corpse False, baseliner corpse True |
| selftest | Self-test: a butchery bill refuses a skeleton corpse and takes a baseliner corpse | passed | 52 | butcher bill takes: skeleton corpse False, baseliner corpse True |
| selftest | Self-test: the scrap skeleton remains bill takes only skeleton corpses, at the crafting spot and machining bench | passed | 52 | scrap bill takes: skeleton corpse True, baseliner corpse False; benches CraftingSpot, TableMachining, VFE_TableMachiningLarge, VGE_CompactMachiningTable |
| selftest | Self-test: a skeleton corpse's rot is inactive and stays fresh when told to desiccate; a baseliner corpse still rots | passed | 52 | rot active: skeleton corpse False (stage Fresh after a desiccate attempt), baseliner corpse True |
| selftest | Self-test: a skeleton corpse yields 1-5 each of steel, plasteel and uranium and at most one component | passed | 52 | skeleton corpse yields 2 Steel, 1 Plasteel, 1 Uranium |
| selftest | Self-test: the remains patches are applied | passed | 52 | 10 remains patches applied |
| selftest | Self-test: a skeleton that lost mechanical nature gets it back from its xenotype | passed | 52 | removed True, restored True (Rimshi_MechanicalNature) |
| selftest | Self-test: temporarily power down is offered on skeletons only, Anesthetize on baseliners only, and anesthetic becomes maintenance mode | passed | 52 | 'temporarily power down' on the skeleton True, on the baseliner False; Anesthetize on the skeleton False, on the baseliner True; anaesthetic became maintenance  |
| selftest | Self-test: the maintenance wear patch is applied | passed | 52 | Need_Food.NeedInterval: prefixes [], postfixes [Matthew.RimshiSkeletons Patch_MaintenanceWear (400)], transpilers [] |
| selftest | Self-test: the wear clock holds while frozen, resets once maintained, and waits a full day | passed | 52 | frozen holds the clock True, maintained resets it True, no wear before a full day True (a day is 60000 ticks) |
| selftest | Self-test: a full day out of maintenance wears one random part that is not the brain | passed | 52 | a full day out wore right hand (worn 2, dealt 2), brain never chosen True, clock restarted True, alive True |
| selftest | Self-test: soft cycle mending is paced over the time left and leaves the skeleton able to get up | passed | 52 | needed 30.5 to get up, half the time left mended 15.25, after the finish still needed 0, injuries 38.5 -> 8 |
| selftest | Self-test: soft cycle mending starts with the legs and leaves the torso when the legs are enough | passed | 52 | first mended left leg, torso cut 3 of 8 left |
| selftest | Self-test: soft cycle mending does nothing for a skeleton with no legs | passed | 52 | no legs True, mended 0 |

## Manual checks

Things the log cannot show; tick them off by hand after looking in game.

- [ ] A skeleton spawned in dev mode (xenotype Skeleton) looks right: skeleton body and head, steel or rust colour, bald, no beard, no tattoos.
- [ ] A skeleton is genderless in its info card and has the Asexual trait.
- [ ] A skeleton's needs tab shows "Maintenance" instead of "Food"; its tooltip tells of the fall of the Second Empire. A human still shows "Food".
- [ ] When maintenance drops to about 30% (about two days after a full bar), a skeleton walks to a component and strips it ("Stripping component for maintenance"), one at a time, and the bar fills back up.
- [ ] In a save from before components were edible, the food policies now allow components and there is a Skeletons policy allowing only components; a hungry skeleton walks to a component, and idle skeletons pick up one component to carry and strip it when hungry. Disallowing components in a skeleton's policy stops it eating them, and the change sticks after a reload.
- [ ] A skeleton refuses ordinary meals (right-click shows "not suitable"), and a human or animal refuses components.
- [ ] Without components, a skeleton gets "maintenance due", then "maintenance overdue", then "worn parts" at 0%, getting one stage worse per day (down to "falling apart", -22); the vanilla hunger thought shows as nullified, and it never gets malnutrition.
- [ ] A skeleton left at 0% maintenance for a full day gets one "worn" injury (2 damage) on a random part, never the brain, with a message for colonists; one more each further day. No blood, it does not heal on its own, a repair kit or the repair bed fixes it, and once it has a component the wear stops. While it soft cycles the wear waits.
- [ ] CPU integrity: a skeleton's needs tab shows "CPU integrity" with a lore line for its current state (no countdown). The gizmo by the draft button reads "CPU integrity" with the bar and the CPU's state (Running, Soft cycling, Overloaded...), never "Deathrest" or a deathrest building count. Hovering the bar shows a lore tooltip about the CPU and soft cycling, with no mention of deathrest or a deathrest gene. The one "Soft cycle" button targets a repair bed, a rib cage or any standable spot, and is not greyed out when the repair bed is taken; vanilla's wake buttons are gone. A skeleton at 0% integrity has no "Deathrest exhaustion" (-12) mood, only "CPU overloaded" (-8).
- [ ] Bed soft cycle: a repair bed draws 50 W idle, 400 W more while repairing, and 15 W per skill level of a cycling skeleton (inspect the bed and the power grid). The cycle takes 48 hours, the bar climbs to 100%, the skeleton gets "Honing my memories like an edgewalker." (+3, 5 days), its other memories are gone and one skill lost a little XP. Flicking the bed off, cutting power, a solar flare or "Wake up" cuts it short: integrity stays where the cycle got it, with no overload and no memory flush, skill loss or thought. Other needs (maintenance, hygiene, recreation, mood) do not move while it cycles.
- [ ] Standing soft reset: "Soft cycle" on an empty cell; the skeleton walks there, stands facing south for 5 days (slow for 3 days, fast on the last), then gets "It's like these memories aren't mine..." (-3, 2 days). Once it stands still, right-click orders are refused ("will not respond until woken") and the draft button is disabled; only "Wake up" (with a warning tooltip and a confirmation) cancels it, with 0% integrity and an overload. Other needs do not move while it stands there. Targeting a rib cage makes it hang there instead.
- [ ] Ancient gene: a skeleton's genes list shows "ancient" (not "null chromosome pattern"), described as being so old it predates the constructs of gender.
- [ ] CPU overload: sluggish for 2 days (moving and manipulation x80%, "CPU overloaded" -8), then recalibrating with no effects for 2 days, then vigilant (paranoid) (x110% moving and manipulation, negotiation and trade price improvement x110%), a mood line that changes daily through the four quotes and gets 1 worse a day, and only violent mental breaks. A finished cycle clears it. Each stage's health tab tooltip describes only that state (no mention of later stages or of violent breaks), and the stage messages say only what just happened.
- [ ] Rib cage: 1x2, cannot be rotated, 80 steel and 3 components after basic mechtech; no medical toggle or owner assignment; only skeletons can use it. Right-click with a skeleton: "Hang in rib cage" (it stays there, needs frozen, integrity and overload hold) and "Soft cycle in rib cage". A downed skeleton is rescued to a repair bed first, then a rib cage. The skeleton draws upright behind the front ribs. The cage's head is at the top (north) and it shows its painted brown and grey, not tinted blue.
- [ ] With Dubs Bad Hygiene: a skeleton has no thirst or bladder bar, its hygiene bar reads "Self-cleaning" with the motor debris tooltip, and it drops about half as fast as a human's. The mechanical nature gene lists the changes and "hygiene rate factor x50%".
- [ ] Naked, dark, wet and pain thoughts show as nullified by mechanical nature.
- [ ] Skeleton remains: a butcher spot or butcher table bill (allowing human corpses) never takes a skeleton corpse, but still takes a human one. The crafting spot and machining table have "scrap skeleton remains"; a colonist hauls a skeleton corpse there and it yields 1-5 each of steel, plasteel and uranium and sometimes one component, with no meat, leather, blood or "butchered humanlike" mood. The corpse's apparel and weapon drop on the floor. A human corpse is never taken by the scrap bill. The bill text says nothing about the yield.
- [ ] Skeleton corpse rot: a skeleton corpse left out for days never rots or turns into a desiccated organic skeleton, shows no rot line on its inspect pane and gives off no rot stink; outdoors it still loses hit points to deterioration. A skeleton corpse from an older save that had rotted loads fresh (`remains | rot cleared`). Dead skeleton raiders outside the home area start forbidden, as every hostile corpse does: unforbid them and the scrap bill takes them. If a scrap bill sits idle while a skeleton corpse lies on the map, `remains | scrap skipped` names why.
- [ ] Nothing eats a skeleton corpse: a cannibal or starving colonist has no eat option on it, and animals (tame or wild) ignore it as food. A predator may still hunt a skeleton, but walks away from the body once it is down, with no error in the log.
- [ ] A skeleton never needs sleep (no rest bar).
- [ ] A new world lists the skeletal imperium in the faction setup (one by default) and it appears on the world map.
- [ ] Skeletal imperium visitors, traders and raids are mostly skeletons (with some human villagers and guards), with skeleton names and backstories.
- [ ] The "Embarking 'Skeleton'" scenario starts with one skeleton in the Skeleton Foundation faction.
- [ ] Mod settings show "Convert Rim-Skeletons (Continued) factions and skeletons when a save loads"; with it on and the old mod loaded, an old save's skeletal imperium shows our faction icon and keeps its name and goodwill.
- [ ] With the setting on, a "Skeletons converted" letter names each old skeleton; they look like our skeletons (body, head, plating, bald), keep their names, skills, traits, gear and injuries, and show as xenotype Skeleton. After saving, removing Rim-Skeletons (Continued) and loading again, they are still there and the log has no "Null key" or "Could not resolve reference" errors for them.
- [ ] In a world made before this mod, Debug actions > Rimshi Skeletons > Add the skeletal imperium adds the faction with one settlement.
- [ ] Research tab: "skeleton repair kits" and "skeleton repair beds" sit right of Basic mechtech and need it; neither needs a mechanitor.
- [ ] After the kit research, the machining table has "make skeleton repair kit"; kits show the steel repair case with the wrench.
- [ ] Repair kits are not offered as medicine (medical care settings, doctors never carry them to tend anyone).
- [ ] A hurt skeleton does not heal on its own (health tab shows injury healing 0%). A colonist with Smith work fetches repair kits, kneels by it and its wounds close while the work goes on; kits are used up (about one per 20 damage) and leftovers are dropped.
- [ ] No one tends a hurt skeleton (colonist, prisoner, slave or guest) with herbal or any other medicine: no doctor job, no self-tend, no drafted "Tend" option, no "needs tending" alert. The repair job, with kits, is the only treatment; with no kits it shows "No skeleton repair kits".
- [ ] While repairing, the repairer shows the mechanitor repair sparks and sound over the skeleton, and the skeleton's wounds close.
- [ ] Repair bed heal rate: a skeleton in a powered repair bed heals about twice as fast as before (80 severity a day, scaled by body size).
- [ ] A hurt skeleton keeps working unless downed or a powered repair bed is free, then goes to it; with only scars it goes during its sleep hours.
- [ ] After the bed research, the skeleton repair bed is in the Biotech tab and costs an AI persona core; deconstructing it gives the core back.
- [ ] The Biotech architect tab holds every skeleton building (repair bed, tuning fork, tempest reliquary, cable fissure, rib cage, skeleton workbench, recall array); none of them show in Furniture or Production.
- [ ] The repair bed shows the repair machine art, facing the right way in all four rotations (west mirrors east), and the skeleton lies on the deck.
- [ ] The repair bed is medical by default and a hurt skeleton picks it over a hospital bed. A human cannot be assigned to or rest in it; a hurt skeleton resting in a powered one is repaired, scars included, and gets a "fully repaired" message. Unpowered, the bed says so and does nothing.
- [ ] Right-clicking a skeleton limb with a skeleton colonist selected offers "Install ..."; with two places to fit it, a second menu names the sides. The skeleton walks to the limb, works on it for about 10 seconds, the mechlink sound plays, the limb is on the chosen side and the item is gone (an old skeleton part it replaced drops). No bill, doctor or repair kit.
- [ ] The same option is greyed out for a baseliner ("only fits a skeleton") and when every limb already has that part ("nowhere to fit it"). Machine organs and the AI core have no install option and still need the surgery bill.
- [ ] A gene extractor will not take a skeleton (the menu option is greyed out with "Skeletons have no genes to extract").
- [ ] A xenogerm's "Implant" gizmo will not target a skeleton, and the skeleton's health tab has no "Implant xenogerm" bill.
- [ ] A sanguophage's xenogerm reimplant ability will not target a skeleton.
- [ ] Skeleton genes never show up in traders' or quest genepacks, so the gene assembler never offers them. The custom xenotype editor at game start still does (on purpose).
