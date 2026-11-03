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
| build | Build line at startup | passed | 43 | version dev, build 43 (DESKTOP-ESRRCQ3 2026-11-01T04:15Z), dll written 2026-11-01 04:15 UTC; biotech True, royalty True, ideology True, odyssey True |
| defs | Blocked thoughts linked at startup (lists any not loaded) | passed | 43 | 27 of 30 blocked; not loaded: RG_Wet, RG_ExtremelyWet, RG_HotSpringWet |
| defs | Vanilla hunger thought nullified by component maintenance | passed | 43 | NeedFood nullified by Rimshi_FuelReactor |
| defs | Immunity list linked at startup (lists any not loaded) | passed | 43 | 23 of 23 immunities |
| defs | Component tagged as the maintenance item (nutrition, days one lasts) | passed | 43 | ComponentIndustrial: nutrition 0.7, preferability MealAwful, one lasts 2 days (0.35 maintenance a day) |
| defs | Needs from other mods removed or relabelled (Dubs Bad Hygiene), or says none | passed | 43 | Rimshi_MechanicalNature HygieneRateMultiplier x0.5, Rimshi_MechanicalNature removes DBHThirst, Rimshi_MechanicalNature removes Bladder, Rimshi_MechanicalNature  |
| defs | Skeleton backstories loaded | passed | 43 | 37 skeleton backstories |
| defs | Outlander, pirate, tribe and Empire xenotype sets include skeletons | passed | 43 | OutlanderCivil 1%, OutlanderRough 1%, TribeCivil 0.5%, TribeRough 0.5%, TribeSavage 0.5%, Pirate 1%, Empire 1%, TribeCannibal 0.5%, NudistTribe 0.5%, CannibalPi |
| patch | Every patched private method found (this line only appears when one is missing) | not seen |  |  |
| gene | Null chromosome makes a pawn genderless | passed | 43 | Jae-Sung: None to None, body Male |
| gene | Skeleton frame gives a male, thin or hulk body and a skeleton head | passed | 43 | Jae-Sung: body Male, head Rimshi_Head_Mki |
| cores | A skeleton's genetics screen opened, reading cores and chips | passed | 39 | Beom's genetics screen reads cores / chips |
| gene | Skeletons generated without family | passed | 43 | Blanka: relation generation skipped |
| age | New skeleton made ancient (biological 120 or more, chronological 400 to 1200) | not seen |  |  |
| age | Skeleton skipped the human old age conditions (frail, bad back, cataract, dementia) | not seen |  |  |
| gene | Removing the null chromosome gives a gender back | passed | 43 | Arhaan: now Male |
| gene | Relations a new genderless pawn got another way were removed (names them) | passed | 43 | Blanka: Parent Pesnosdos, Parent Fenix |
| gene | A skeleton got back core genes its xenotype lists but it had lost (names them) | not seen |  |  |
| gene | Skeletons in a save that had lost core genes got them back on load | not seen |  |  |
| gene | A core gene was removed from a skeleton, and by what (diagnostic; the gene removal self-test causes one) | not seen |  |  |
| faction | Mod setting on: old Rim-Skeletons faction replaced by ours on load | not seen |  |  |
| faction | Mod setting on: save had no old Rim-Skeletons factions | not seen |  |  |
| faction | Debug action added the skeletal imperium to the world | not seen |  |  |
| faction | Skeletal imperium present in the loaded world (or says none) | passed | 43 | Damascus City |
| migrate | Mod setting on: an old Rim-Skeletons skeleton converted to ours (lists what changed) | passed | 43 | Jae-Sung: race Skeleton_ to Human, 0 health conditions moved, biological age 1480 to 47, xenotype voxex to skeleton, childhood ChildhoodSkeleton5 to Rimshi_Skel |
| migrate | Health condition with no matching human body part dropped during conversion (names it) | not seen |  |  |
| migrate | Mod setting on: conversion summary (skeletons converted, conditions dropped) | not seen |  |  |
| migrate | Mod setting on: save had no old Rim-Skeletons skeletons | not seen |  |  |
| migrate | Mod setting on: Rim-Skeletons (Continued) not loaded, nothing to convert | not seen |  |  |
| render | Skeleton body texture drawn | passed | 43 | Kouzou: Rimshi/Body/Naked_Male, head Rimshi_Head_Mki, skin RGBA(0.431, 0.431, 0.431, 1.000), gender None, xenotype skeleton |
| repair | Crafter started repairing a skeleton with repair kits | not seen |  |  |
| repair | Repair used up a kit | not seen |  |  |
| repair | Repair sealed leaks or treated a tendable condition with kits | not seen |  |  |
| repair | Skeleton fully repaired by a crafter | not seen |  |  |
| bed | Non-skeleton kept out of a skeleton repair bed | passed | 43 | Niskavaara is not a skeleton |
| bed | Repair bed repairing a skeleton's injury | not seen |  |  |
| bed | Skeleton fully repaired in a repair bed | not seen |  |  |
| diet | Skeleton consumed a component for maintenance (fails if it ate anything else) | failed | 43 | FAILED: the Blunt ate DBH_WaterBottle, which is not a maintenance item |
| maintenance | Skeleton out of maintenance did not get malnutrition | passed | 43 | Arhaan: out of maintenance, malnutrition refused |
| maintenance | Need bar and tooltips relabel needs (this line only appears when a patch target is missing) | not seen |  |  |
| maintenance | Components allowed in the save's food policies and Skeletons policy added (once per save) | not seen |  |  |
| maintenance | Skeleton picking up a component to carry | passed | 43 | the Blunt picking up 1 ComponentIndustrial to carry, from (146, 0, 181) |
| maintenance | Skeleton a full day out of maintenance wore a random non-brain part | not seen |  |  |
| softcycle | Skeleton went to soft cycle in a repair bed (gives the cycle's power draw) | not seen |  |  |
| softcycle | Skeleton soft cycling in a powered repair bed (48 hours, power draw, integrity) | not seen |  |  |
| softcycle | Soft cycle held outside a repair bed (carried, in a rib cage, older save) | not seen |  |  |
| softcycle | Skeleton due a soft cycle found no usable repair bed (gives the reason) | not seen |  |  |
| softcycle | Soft cycle finished in a repair bed (integrity 100%, memories flushed, skill lost, +3 thought) | passed | 43 | Arhaan: integrity 100%, overload cleared True, 1 memories forgotten, skill lost: construction -3772 XP, thought Rimshi_SoftCycleHoned |
| softcycle | Bed soft cycle cut short: power lost or woken early (integrity kept, no overload) | passed | 43 | Arhaan: self-test, integrity kept at 40% |
| softcycle | Skeleton began a soft reset standing or hanging in a rib cage | not seen |  |  |
| softcycle | Soft reset finished without a repair bed (integrity 100%, -3 thought) | not seen |  |  |
| softcycle | Soft reset cancelled: woken, downed or a mental break (integrity 0%) | passed | 43 | Arhaan: self-test, integrity 0% |
| softcycle | A skeleton in a standing soft cycle refused a player order (names the job) | not seen |  |  |
| softcycle | A skeleton in a standing soft cycle was not drafted | not seen |  |  |
| softcycle | Soft cycle mending its injuries toward getting up, legs first (gives the amount needed and time left) | not seen |  |  |
| softcycle | Soft cycle finished and mended just enough to get up | not seen |  |  |
| softcycle | Soft cycle mends nothing: no legs left, or could not get up even fully mended | not seen |  |  |
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
| spire | A held tempest or its wake ended at the next finished soft cycle | passed | 43 | Arhaan's Rimshi_TempestWake ended: self-test |
| deathrest | Skeletons from an older save got the deathrest gene | not seen |  |  |
| overload | CPU overload started (integrity ran out or a cycle was cancelled) | passed | 43 | Arhaan: soft reset cancelled: self-test |
| overload | CPU overload moved to its next stage (steady at day 2, paranoid at day 4) | not seen |  |  |
| overload | Paranoid overloaded skeleton limited to violent mental breaks | not seen |  |  |
| overload | CPU overload cleared by a finished soft cycle | passed | 43 | Arhaan cleared after 5.5 days |
| ribcage | Skeleton hanging in a rib cage (needs frozen) | not seen |  |  |
| reproduction | A pregnancy added to a skeleton was refused | passed | 43 | Arhaan: PregnantHuman refused |
| horde | Recall array inherited the mechband antenna graphic | passed | 43 | Rimshi_RecallArray uses Things/Building/Misc/MechbandAntenna, size (2, 1), research Rimshi_SkeletonRecall |
| horde | Recall research on the research tab, overlapping nothing (gives its position) | failed | 43 | FAILED: Rimshi_SkeletonRecall at (12, 5.3) on Main, cost 2000, needs BasicMechtech at (9, 5.4) on Main; overlaps Seg_AutoWeapons |
| horde | Bladeguard melee weapon tags (adds medieval weapons without Royalty) | passed | 43 | bladeguard weapon tags UltratechMelee |
| horde | Colonist broadcast the recall codes; array burned out | not seen |  |  |
| horde | Skeleton horde dropped in (counts, kinds, weapons, shield belts, failsafe cores) | not seen |  |  |
| horde | Horde called to a map that is gone was dropped | not seen |  |  |
| horde | Horde skeleton went berserk | not seen |  |  |
| horde | Failsafe core set the death on downed chance | passed | 43 | the Doubtful: death on downed chance 10% (was -100%) |
| horde | A failsafe skeleton died anyway (says how; not a failure) | not seen |  |  |
| reprogram | Captured horde skeleton set to interrogate and reprogram | not seen |  |  |
| reprogram | Warden reprogramming session lowered the corruption | passed | 43 | Cummings reprogrammed the Droning: -35%, 66% corruption left |
| reprogram | Reprogrammed skeleton joined the colony | not seen |  |  |
| geneproof | Xenogerm implant on a skeleton refused | not seen |  |  |
| geneproof | Xenogerm reimplant ability on a skeleton refused | not seen |  |  |
| geneproof | Skeleton refused the absorb xenogerm job | not seen |  |  |
| geneproof | No xenogerm reached a skeleton past the checks (this line only appears when one did) | not seen |  |  |
| geneproof | No reimplant involving a skeleton got past the checks (this line only appears when one did) | not seen |  |  |
| geneproof | Skeleton genes kept out of a genepack (fails if a pack held only skeleton genes) | passed | 43 | 1 skeleton gene(s) kept out of a genepack |
| prosthetics | Generated skeleton got no prosthetics, bionics or implants | passed | 43 | Arhaan (Colonist): no tech hediffs generated for a skeleton |
| prosthetics | Skeleton refused a self-installed implant (neuroformer or another mod's; mechlinks still allowed) | not seen |  |  |
| prosthetics | A psylink was refused on a skeleton (royal title, bestowing, anima tree, dev mode) | passed | 43 | Arhaan: PsychicAmplifier refused |
| prosthetics | No install surgery reached a skeleton past the checks (this line only appears when one did) | not seen |  |  |
| parts | Skeleton fabrication and advanced parts research placed on the research tab without overlaps | passed | 43 | Rimshi_AdvancedSkeletonParts at (11, 7.2) on Main |
| parts | A skeleton part was installed on a skeleton (names the part) | passed | 43 | Arhaan: Rimshi_AICore on Brain |
| parts | A KLR or industrial part took reduced damage (first hit each session) | passed | 43 | Arhaan: Shoulder hit for 10, Rimshi_KLRArm made it 8 |
| parts | A steady arm knocked a weapon out of an enemy's hands in melee | not seen |  |  |
| parts | A second AI core repaired a damaged one (names what it removed) | passed | 43 | Arhaan: removed Gunshot |
| parts | A skeleton fitted a limb to itself from the item, no surgery (names the limb, the part and whether the side was picked) | not seen |  |  |
| anatomy | A skeleton got its machine organs (generation, gene added or older save; names the organs) | passed | 43 | Jae-Sung (gene added): Stomach, Heart, Lung, Lung, Kidney, Kidney, Liver, Eye, Eye, Ear, Ear, Nose |
| anatomy | Skeletons in an older save were given machine organs on load | not seen |  |  |
| powerdown | A temporarily power down operation finished on a skeleton (says if it is in maintenance mode) | not seen |  |  |
| powerdown | A skeleton got maintenance mode where it would have got anesthetic (first time each session) | not seen |  |  |
| blood | A hit skeleton leaked ancient oil (black filth) instead of blood | not seen |  |  |
| module | An EMP hit stunned a skeleton the way it stuns a mechanoid (unshielded circuits) | passed | 43 | Arhaan stunned by EMP like a mechanoid |
| module | Skeletons from an older save got the module genes their xenotype now lists | passed | 43 | 24 modules added to skeletons from an older save |
| blood | A hit skeleton showed the black oil spray instead of the red blood spray | passed | 43 | Arhaan: black hit spray instead of blood |
| blood | A sanguophage bit a skeleton and got no hemogen (bloodfeed ability or another mod) | not seen |  |  |
| wounds | A wounded skeleton on the map showed grey and black wound marks instead of red ones | not seen |  |  |
| drugs | A skeleton was refused a drug (ingest menu, drug policy, recreation, AI job, binge or generation; one line per route) | passed | 43 | generation GiveCombatEnhancingDrugs: the Doubtful refused |
| addiction | An addiction or chemical dependency was refused on a skeleton (one line per hediff) | not seen |  |  |
| addiction | A skeleton lost an addiction it already had (older save or gene added) | not seen |  |  |
| addiction | Loading a save removed addictions from skeletons (only appears when there were some) | not seen |  |  |
| drugs | No drug reached a skeleton past the checks (this line only appears when one did; the drug did nothing) | not seen |  |  |
| remains | A skeleton corpse was scrapped for steel, plasteel, uranium and at most one component (names the yield) | passed | 43 | Arhaan yielded 3 Steel, 5 Plasteel, 2 Uranium |
| remains | A butchery bill skipped a skeleton corpse (butcher spot, butcher table or another mod's) | passed | 43 | ButcherCorpseFlesh skipped the corpse of Arhaan |
| remains | A pawn or animal that went to eat a skeleton corpse (a predator after a hunt) stopped instead | not seen |  |  |
| remains | No skeleton corpse was eaten past the checks (this line only appears when one got that far; nothing was eaten) | not seen |  |  |
| remains | A skeleton corpse that had rotted or desiccated before rot was disabled became fresh again (on load or spawn) | not seen |  |  |
| remains | A scrap skeleton remains bill found nothing while a skeleton corpse lay on the map; names why (forbidden, filter, radius, reserved, unreachable) | not seen |  |  |
| selftest | Self-tests: all passed (summary line) | failed | 43 | FAILED: 125 passed, 3 failed |
| selftest | Self-test: skeleton and baseliner generate | passed | 43 | skeleton Arhaan, baseliner Cummings |
| selftest | Self-test: no exceptions while testing | not seen |  |  |
| selftest | Self-test: skeleton has the Skeleton xenotype | passed | 43 | xenotype skeleton |
| selftest | Self-test: skeleton is genderless | passed | 43 | gender None |
| selftest | Self-test: skeleton is biologically 120 or more and chronologically 400 to 1200 | not seen |  |  |
| selftest | Self-test: baseliner age left alone | not seen |  |  |
| selftest | Self-test: skeleton gets no human old age conditions | not seen |  |  |
| selftest | Self-test: skeleton is Asexual | passed | 43 | traits Psychopath, DislikesMen, VTE_Technophobe, RT_TalentNoTalent, Asexual |
| selftest | Self-test: skeleton body is male, thin or hulk | passed | 43 | body Thin |
| selftest | Self-test: skeleton has a skeleton head | passed | 43 | head Rimshi_Head_Mki |
| selftest | Self-test: skeleton has a plating colour | passed | 43 | skin RGBA(0.431, 0.431, 0.431, 1.000) |
| selftest | Self-test: skeleton is bald | passed | 43 | hair Bald |
| selftest | Self-test: skeleton body texture found | passed after fail | 43 | path Rimshi/Body/Naked_Thin |
| selftest | Self-test: skeleton head texture found | passed after fail | 43 | path Rimshi/Head/Mki |
| selftest | Self-test: skeleton has no rest need | passed | 43 | rest need absent |
| selftest | Self-test: skeleton generated without family | passed after fail | 43 | direct relations 0 |
| selftest | Self-test: skeleton fertility is 0 | passed | 43 | fertility 0 |
| selftest | Self-test: skeleton toxic resistance 100% | passed | 43 | toxic resistance 1 |
| selftest | Self-test: skeleton psychic sensitivity 0% | passed | 43 | psychic sensitivity 0 |
| selftest | Self-test: skeleton immune to flu | passed | 43 | immune to flu |
| selftest | Self-test: skeleton can consume components | passed | 43 | component suitable True |
| selftest | Self-test: skeleton refuses meals | passed | 43 | simple meal suitable False |
| selftest | Self-test: one component lasts a skeleton 2 days | passed | 43 | one component lasts 2.00 days |
| selftest | Self-test: skeleton consumes one component at a time | passed | 43 | takes 1 at once |
| selftest | Self-test: skeleton food bar shown as Maintenance with the Second Empire tooltip (human keeps Food) | passed | 43 | skeleton Maintenance, human Food |
| selftest | Self-test: skeleton cannot get malnutrition | passed | 43 | malnutrition False |
| selftest | Self-test: vanilla hunger thought nullified for skeleton | passed | 43 | NeedFood nullified |
| selftest | Self-test: maintenance thought can apply to skeleton | passed | 43 | Rimshi_MaintenanceNeed can apply |
| selftest | Self-test: with Dubs Bad Hygiene, no thirst or bladder and self-cleaning at half rate (passes when not loaded) | passed | 43 | thirst and bladder removed True, hygiene shown as self-cleaning, hygiene rate 0.5 |
| selftest | Self-test: skeleton has deathrest (gene, need, icon); human does not | passed | 43 | skeleton deathrests True, need True, human deathrests False |
| selftest | Self-test: deathrest need shown as CPU integrity with the soft cycle tooltip | passed | 43 | need shown as CPU integrity |
| selftest | Self-test: repair bed draws 15 W per skill level for a cycle and has no chemfuel tank | passed | 43 | cycle draw 15 W per skill level (945 W for the test skeleton), chemfuel tank False |
| selftest | Self-test: a bed soft cycle takes 48 hours | passed | 43 | bed cycle 2 days |
| selftest | Self-test: soft reset is slow for 3 days and speeds up to 100% at 5 days | passed | 43 | after 3 days 16%, after 4 40% |
| selftest | Self-test: a cycle raises integrity in step and ends at 100% | passed | 43 | 50% integrity, half a cycle 75%, whole cycle 100% |
| selftest | Self-test: a bed soft cycle cut short keeps its integrity and causes no overload | passed | 43 | bed cycle cut short: integrity 40%, overload False |
| selftest | Self-test: a cancelled standing cycle leaves 0% integrity and a CPU overload | passed | 43 | standing cycle cut short: integrity 0%, overload True |
| selftest | Self-test: needs other than CPU integrity are frozen during a soft cycle | passed | 43 | while soft cycling: maintenance frozen True, CPU integrity frozen False |
| selftest | Self-test: a skeleton at 0% CPU integrity gets no vanilla deathrest exhaustion thought | passed | 43 | vanilla thought DeathrestExhaustion active False at integrity 0% |
| selftest | Self-test: skeleton shows the CPU integrity gizmo and one soft cycle button instead of vanilla's deathrest gizmo | passed | 43 | gizmos GeneGizmo_Rimshi_CpuIntegrity |
| selftest | Self-test: orders, drafting and the draft button are guarded during a standing soft cycle | passed | 43 | orders, drafting and the draft button are guarded during a standing soft cycle |
| selftest | Self-test: the CPU integrity tooltip is skeleton lore with no vanilla deathrest text | failed | 43 | FAILED: integrity tooltip: <color=#E6E64CFF>Deathrest: 0%</color> / <color=#FF3333FF><color=#D09B61FF>Arhaan</color> should deathrest soon to avoid exhaustion.< |
| selftest | Self-test: the rib cage texture is drawn unrotated and without the medical sheet tint | passed | 43 | rib cage drawRotated False, sheet colour False |
| selftest | Self-test: overload stages (sluggish x0.8, recalibrating at day 2, vigilant x1.1 at day 4, -1 mood a day) | passed | 43 | moving x0.8 sluggish, stage 1 at day 3, paranoid True moving x1.1, penalty -2 on day 5.5 |
| selftest | Self-test: vigilant (paranoid) thought has the four lines and applies to the skeleton only | passed | 43 | paranoid thought active True |
| selftest | Self-test: violent break filter patched (berserk counts, sad wander does not) | passed | 43 | berserk violent True, sad wander violent False |
| selftest | Self-test: a finished cycle clears the overload, fills integrity and flushes memories | passed | 43 | overload False, integrity 100%, memories Rimshi_SoftCycleHoned |
| selftest | Self-test: rib cage is a 1x2 unrotatable medical bed, 80 steel + 3 components, basic mechtech, skeletons only | passed | 43 | 1x2 unrotatable medical bed, 80 steel + 3 components, basic mechtech; skeleton can use True, human False |
| selftest | Self-test: rib cage back and front textures load | passed | 43 | Rimshi/Buildings/RibCage and Rimshi/Buildings/RibCage_front |
| selftest | Self-test: tuning fork links to the repair bed (2 max, same room), takes advanced components, 50 W idle and 1000 W ringing, art loads | passed | 43 | max per bed 2, repair bed links it True, same-room patch True, art True, power 50 W idle, 1000 W ringing, fuel 1 advanced component a cycle, cycle with one fork |
| selftest | Self-test: chemical spire is 3x3 unrotatable, links to the repair bed (1 max, same room), 100 chemfuel, 100 W for 36 h, joins unpowered, art and icon load | passed | 43 | max per bed 1, repair bed links it True, art and icon True, chemfuel 100, charge 100 W for 1.5 days, 3x3 unrotatable True |
| selftest | Self-test: cable fissure is 2x3 rotatable, links to the repair bed (1 max), costs 100 plasteel + 2 advanced components + 60 stone, 250 W untuned, tuning bill 4 components/100 steel/40 blocks/250 research points, art, masks and icon load | passed | 43 | max per bed 1, repair bed links it True, art, masks and icon True, cost (100 plasteel, 2 advanced components, 60 stone) True, untuned draw 250 W, tuning: 4 comp |
| selftest | Self-test: overclocked musculature gives +20 carrying capacity and the heave ability, heave distance is carrying capacity / 10, ability goes with the hediff | failed | 43 | FAILED: carrying capacity 76.5 -> 96.9, heave ability True (gone after True), heave distance 9 cells |
| selftest | Self-test: held tempest makes a skeleton EMP-proof with x1.1 speed and the burst ability; its wake stays EMP-proof; both end, burst damage 5 to 40 | passed | 43 | EMP-proof while held True and in its wake True, burst ability True (gone after True), move speed x1.1, stunnable again after the next cycle True, burst damage 1 |
| selftest | Self-test: rib cage freezes needs and ranks between the repair bed and hospital bed for rescue | passed | 43 | need freeze patched, rescue order repair bed 6, rib cage 7, hospital bed 8 |
| selftest | Self-test: component packing patch applied | passed | 43 | packing patch applied True |
| selftest | Self-test: Skeletons food policy allows only components | passed | 43 | Skeletons policy allows ComponentIndustrial |
| selftest | Self-test: naked thought nullified for skeleton | passed | 43 | Naked nullified |
| selftest | Self-test: human refuses components | passed | 43 | component suitable False |
| selftest | Self-test: human still eats meals | passed | 43 | simple meal suitable True |
| selftest | Self-test: human keeps gender and hunger thought | passed | 43 | gender Male, hunger thought nullified False |
| selftest | Self-test: both repair research projects need Basic mechtech | passed | 43 | Rimshi_SkeletonRepairKits and Rimshi_SkeletonRepairBeds need BasicMechtech |
| selftest | Self-test: repair kit recipe needs the repair kit research | passed | 43 | recipe Make_Rimshi_RepairKit needs Rimshi_SkeletonRepairKits, Rimshi_SkeletonFabrication |
| selftest | Self-test: repair bed needs the repair bed research and an AI persona core | passed | 43 | bed needs Rimshi_SkeletonRepairBeds |
| selftest | Self-test: repair kit is not medicine | passed | 43 | repair kit is medicine False; vanilla tending never uses it |
| selftest | Self-test: skeleton injury healing is 0%, human's is not | passed | 43 | injury healing: skeleton 0%, human 100% |
| selftest | Self-test: skeleton repair is a Smith job using Crafting | passed | 43 | work giver in Smithing |
| selftest | Self-test: a cut skeleton needs repair, a cut human does not | passed | 43 | cut skeleton needs repair True (1 kit), cut human False |
| selftest | Self-test: nothing on a skeleton is tendable with medicine; a human cut still is | passed | 43 | skeleton cut tendable False, skeleton needs tending False, human cut tendable True |
| selftest | Self-test: a cut skeleton does not count as healing on its own (no fully healed message spam); a cut human does | not seen |  |  |
| selftest | Self-test: only skeletons can use a repair bed; skeletons can still use hospital beds | passed | 43 | skeleton True, human False, skeleton in hospital bed True |
| selftest | Self-test: removing the null chromosome restores a gender | passed | 43 | gender after removing null chromosome Male |
| selftest | Self-test: no skeleton gene can roll in gene sets | passed | 43 | 14 skeleton genes, none can roll in gene sets |
| selftest | Self-test: every skeleton gene is marked core or chip, chip category loaded | passed | 43 | 14 core genes, 0 chips, chip category loaded |
| selftest | Self-test: skeleton xenotype carries the unshielded circuits and archaic speech modules, marked core and modifiable | passed | 43 | xenotype modules: Rimshi_UnshieldedCircuits, Rimshi_ArchaicSpeech |
| selftest | Self-test: EMP stuns a skeleton (and it adapts like a mech), not a baseliner | passed | 43 | after an EMP hit: skeleton stunned True (5.0 seconds), baseliner stunned False, skeleton adapts like a mech True |
| selftest | Self-test: archaic speech lowers trade price, negotiation and social impact; archaic persona no longer does | passed | 43 | archaic speech: trade price x0.5, negotiation x0.8, social impact x0.5; archaic persona social impact x1 |
| selftest | Self-test: a skeleton's genetics screen says cores and chips, a baseliner's still says genes | passed | 43 | skeleton screen reads cores / chips, baseliner screen reads germline genes |
| selftest | Self-test: character card tooltip says view cores for a skeleton only, patches applied | passed | 43 | skeleton: Click to view Arhaan's cores. / baseliner: Click to view Cummings's genes. |
| selftest | Self-test: 200 random gene sets hold no skeleton gene | passed | 43 | 0 of 200 random gene sets held a skeleton gene |
| selftest | Self-test: a genepack drops skeleton genes and keeps the rest | passed | 43 | genepack holds Neversleep |
| selftest | Self-test: gene buildings refuse the skeleton, not the baseliner | passed | 43 | skeleton refused True, baseliner refused False |
| selftest | Self-test: implant xenogerm bill unavailable on a skeleton | passed | 43 | implant xenogerm bill on skeleton available False |
| selftest | Self-test: every gene lock patch applied | passed | 43 | 8 gene lock patches applied |
| selftest | Self-test: skeleton has a skeleton adulthood backstory | passed | 43 | backstories Rimshi_SkeletonChildhood5, Rimshi_SkeletonAdulthood10 |
| selftest | Self-test: skeletal imperium and skeleton player faction loaded | passed | 43 | Rimshi_SkeletalImperium with 5 pawn groups, player faction Rimshi_PlayerSkeletons |
| selftest | Self-test: a skeletal imperium pawn kind generates a skeleton | passed | 43 | Rimshi_TownGuard: xenotype skeleton, name Sadayoshi the Sensitive |
| selftest | Self-test: skeleton cannot have children or be implanted | passed | 43 | child with baseliner Arhaan is a skeleton and cannot have children., pregnancy chance 0 |
| selftest | Self-test: pregnancy cannot be added to a skeleton | passed | 43 | pregnancy hediff refused |
| selftest | Self-test: recall array and its research loaded (2000, after basic mechtech, no mechanitor needed) | passed | 43 | Rimshi_RecallArray needs Rimshi_SkeletonRecall (2000, after BasicMechtech, needs mechanitor False) |
| selftest | Self-test: horde skeletons have no faction | passed | 43 | Rimshi_HordeLegionary faction none skeleton, Rimshi_HordeBladeguard faction none skeleton, Rimshi_HordeCenturion faction none skeleton |
| selftest | Self-test: horde skeletons have the corrupted protocol and cannot be recruited | passed | 43 | the Doubtful corrupted True recruitable False, the Droning corrupted True recruitable False, Blanka corrupted True recruitable False |
| selftest | Self-test: horde gunners carry spacer guns, bladeguards melee weapons and shield belts | passed | 43 | Rimshi_HordeLegionary: VWE_Gun_ChargeShotgun, Rimshi_HordeBladeguard: VWEL_LaserSword + shield belt, Rimshi_HordeCenturion: VWE_Gun_GaussRifle |
| selftest | Self-test: horde skeletons are not hostile to each other | passed | 43 | horde skeletons not hostile to each other |
| selftest | Self-test: failsafe core sets the death on downed chance | passed | 43 | death on downed chance 10% |
| selftest | Self-test: a reprogramming session lowers the corruption | passed | 43 | corruption 100% to 66% (warden Intellectual 9) |
| selftest | Self-test: a bionics-rolling kind gives the skeleton none and the baseliner some | passed | 43 | bionics generated: skeleton 0, baseliner 3 |
| selftest | Self-test: no install surgery is offered on a skeleton (bionic arm and natural heart still offered on a baseliner) | passed | 43 | 493 install surgeries, 0 offered on the skeleton; bionic arm and natural heart offered on the baseliner True |
| selftest | Self-test: removing a body part is still offered on a skeleton | passed | 43 | remove body part offered on the skeleton True |
| selftest | Self-test: mechlink allowed and psylink neuroformer refused for skeletons | passed | 43 | mechlink allowed, psylink neuroformer refused |
| selftest | Self-test: giving a skeleton a psylink adds none and no psycast, a baseliner gets one (Royalty only) | passed | 43 | skeleton psylink False, new abilities 0; baseliner psylink True |
| selftest | Self-test: the prosthetics lock patches are applied | passed | 43 | 8 prosthetics lock patches applied |
| selftest | Self-test: every Rimshi bench with bills (skeleton workbench, cable fissure) has a work giver that takes its bills | not seen |  |  |
| selftest | Self-test: every skeleton part install is offered on a skeleton and not on a baseliner | passed | 43 | 19 skeleton part installs, skeleton only |
| selftest | Self-test: a new skeleton has a machine part on all 8 organ types | passed | 43 | 8 machine organs on every organ |
| selftest | Self-test: a skeleton's organs are named after their machine parts (left lung reads left cooling fan), a baseliner's are not | passed | 43 | left lung reads left cooling fan on the skeleton, left lung on the baseliner |
| selftest | Self-test: machine organs are hidden in the health tab like natural organs | passed | 43 | machine organs hidden in the health tab |
| selftest | Self-test: the health tab, surgery menu, medical bills and combat log name parts through the machine names | failed | 43 | FAILED: HealthCardUtility.DrawHediffRow 0, HealthCardUtility.GetTooltip 1, HealthCardUtility.GenerateSurgeryOption 0, Bill_Medical.get_Label 0, <RulesForBodyPar |
| selftest | Self-test: a cooling fan install is offered only once the lung is gone, and puts the cooling fan back | passed | 43 | offered on a fitted lung False, on a missing lung True, cooling fan back True, alive True |
| selftest | Self-test: a fitted lung can be removed (as its cooling fan), a bare one cannot, and a restored one gets its cooling fan | not seen |  |  |
| selftest | Self-test: a KLR arm installs on a skeleton's shoulder | passed | 43 | KLR arm on left shoulder: True |
| selftest | Self-test: all 10 skeleton limbs install themselves from the item, organs and the AI core do not | not seen |  |  |
| selftest | Self-test: a skeleton leg is refused for a baseliner, goes on the skeleton's missing leg first, and takes | not seen |  |  |
| selftest | Self-test: a KLR arm takes 20% off the damage that reaches it | passed | 43 | 10 bullet damage on the KLR arm arrived as 10, reduced to 8 (expected 8), injury 8 |
| selftest | Self-test: an AI core installs, and a second one repairs a damaged core | passed | 43 | core installed True, repair offered when damaged True, injuries left 0, alive True |
| selftest | Self-test: skeleton cannot bleed, baseliner can | passed | 43 | skeleton can bleed False, baseliner True |
| selftest | Self-test: a cut skeleton has bleed rate 0, a cut baseliner bleeds | passed | 43 | bleed rate: cut skeleton 0%, cut baseliner 36% |
| selftest | Self-test: ancient oil filth and black hit effect loaded | passed | 43 | filth Rimshi_Filth_AncientOil colour RGBA(0.078, 0.071, 0.063, 0.784), hit effect Rimshi_Damage_HitAncientOil replaces Damage_HitFlesh |
| selftest | Self-test: a cut skeleton gets a grey and black mech wound overlay, a cut baseliner a flesh one | passed | 43 | cut overlay: skeleton Things/Pawn/Wounds/WoundMechB, baseliner Things/Pawn/Wounds/WoundFleshA; mech wound textures found True |
| selftest | Self-test: the blood patches are applied | passed | 43 | 8 blood patches applied |
| selftest | Self-test: a skeleton cannot take beer or penoxycyline, a baseliner can | passed | 43 | beer: skeleton False, baseliner True (teetotaler False); penoxycyline suitable: skeleton False, baseliner True |
| selftest | Self-test: a policy allowing every drug schedules and carries none for the skeleton, some for a baseliner | passed | 43 | policy allowing every drug: skeleton schedules or carries 0, baseliner 103 |
| selftest | Self-test: no administer drug bill is offered on a skeleton, some are on a baseliner | passed | 43 | 103 administer drug bills, 0 offered on the skeleton, 103 on the baseliner |
| selftest | Self-test: no drug binge mental state can start on a skeleton | passed | 43 | 2 drug binge states, possible on the skeleton: none |
| selftest | Self-test: the drug lock patches are applied | passed | 43 | 16 drug lock patches applied |
| selftest | Self-test: an addiction cannot be added to a skeleton, it can to a baseliner | not seen |  |  |
| selftest | Self-test: a chemical dependency gene on a skeleton adds no dependency hediff | not seen |  |  |
| selftest | Self-test: an addiction already on a skeleton is removed | not seen |  |  |
| selftest | Self-test: the addiction lock patches are applied | not seen |  |  |
| selftest | Self-test: every old skeleton body part maps to its own human part (old mod loaded) | passed | 43 | 30 old parts, unmapped: none, shared targets 0 |
| selftest | Self-test: converted old skeleton is race Human with the human body | passed | 43 | race Human, body Human |
| selftest | Self-test: converted old skeleton has the Skeleton xenotype and no gender | passed | 43 | xenotype skeleton, gender None |
| selftest | Self-test: converted old skeleton has our head and backstories | passed | 43 | head Rimshi_Head_Mki, backstories Rimshi_SkeletonChildhood5, Rimshi_SkeletonAdulthood15 |
| selftest | Self-test: converted old skeleton has our pawn kind | passed | 43 | kind Rimshi_Colonist |
| selftest | Self-test: converted old skeleton has no old hediff and only human body parts | passed | 43 | 12 health conditions, lost 0 |
| selftest | Self-test: a converted skeleton is not converted again | passed | 43 | converted pawn no longer matches |
| selftest | Self-test: a converted skeleton is biologically 120 or more | not seen |  |  |
| selftest | Self-test: a skeleton corpse cannot be eaten, a baseliner corpse can | passed | 43 | ingestible: skeleton corpse False, baseliner corpse True |
| selftest | Self-test: a butchery bill refuses a skeleton corpse and takes a baseliner corpse | passed | 43 | butcher bill takes: skeleton corpse False, baseliner corpse True |
| selftest | Self-test: the scrap skeleton remains bill takes only skeleton corpses, at the crafting spot and machining bench | passed | 43 | scrap bill takes: skeleton corpse True, baseliner corpse False; benches CraftingSpot, TableMachining, VFE_TableMachiningLarge, VGE_CompactMachiningTable |
| selftest | Self-test: a skeleton corpse's rot is inactive and stays fresh when told to desiccate; a baseliner corpse still rots | not seen |  |  |
| selftest | Self-test: a skeleton corpse yields 1-5 each of steel, plasteel and uranium and at most one component | passed | 43 | skeleton corpse yields 3 Steel, 5 Plasteel, 2 Uranium |
| selftest | Self-test: the remains patches are applied | passed | 43 | 5 remains patches applied |
| selftest | Self-test: a skeleton that lost mechanical nature gets it back from its xenotype | not seen |  |  |
| selftest | Self-test: temporarily power down is offered on skeletons only, Anesthetize on baseliners only, and anesthetic becomes maintenance mode | not seen |  |  |
| selftest | Self-test: the maintenance wear patch is applied | not seen |  |  |
| selftest | Self-test: the wear clock holds while frozen, resets once maintained, and waits a full day | not seen |  |  |
| selftest | Self-test: a full day out of maintenance wears one random part that is not the brain | not seen |  |  |
| selftest | Self-test: soft cycle mending is paced over the time left and leaves the skeleton able to get up | not seen |  |  |
| selftest | Self-test: soft cycle mending starts with the legs and leaves the torso when the legs are enough | not seen |  |  |
| selftest | Self-test: soft cycle mending does nothing for a skeleton with no legs | not seen |  |  |

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
