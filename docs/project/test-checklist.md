# Test checklist

What has been seen working in game, read from the logs. Every test-worthy event writes a line to Player.log in a fixed shape:

    [Rimshi][T] area | key | detail

A failure writes the same line as a warning with `FAILED:` at the start of the detail. The game also logs a build line at startup (version, build number, who built it and when, which DLCs are active), so every result is tied to a build.

**How to use it:** play with Development mode on, then tell Claude to check the logs. Claude pulls Player.log and Player-prev.log from `C:\Users\Matthew\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios`, runs `tools/rimshi_testlog.py` over them and updates this table. Results add up across sessions; any exception that mentions the mod is reported too. A PR that only updates this file is merged by Claude without asking, and its description lists every test that ran and its outcome.

**Self-tests:** with Development mode on, loading or starting a game runs the in-game self-tests once per session (also on demand: Debug actions > Rimshi Skeletons > Run self-tests). They generate a skeleton, a baseliner and a skeletal imperium town guard off the map, check genes, looks, diet, thoughts, immunity, the gene lock, the prosthetics lock, skeleton parts (install bills, KLR arm armour, AI core repair), blood and ancient oil, the drug lock, backstory, faction, reproduction, the recall array and horde skeletons (gear, protocol, failsafe core, one reprogramming session), discard the pawns, and log one `selftest` line per check plus a summary.

**Unit tests:** `python3 -m unittest discover -s tests` runs the offline tests (def cross-references, textures, checklist coverage, the log reader, house style). CI runs them on every push.

**Adding tests:** when code logs a new test line, add a row to `CHECKLIST` in `tools/rimshi_testlog.py` and to the table below.

**Status:** passed, failed, passed after fail (it failed once and later worked), or not seen.

| Area | Item | Status | Build | Last line |
| --- | --- | --- | --- | --- |
| build | Build line at startup | passed | 7 | version dev, build 7 (DESKTOP-ESRRCQ3 2026-10-24T18:45Z), dll written 2026-10-24 18:45 UTC; biotech True, royalty True, ideology True, odyssey True |
| defs | Blocked thoughts linked at startup (lists any not loaded) | passed | 7 | 27 of 30 blocked; not loaded: RG_Wet, RG_ExtremelyWet, RG_HotSpringWet |
| defs | Vanilla hunger thought nullified by component maintenance | not seen |  |  |
| defs | Immunity list linked at startup (lists any not loaded) | passed | 7 | 23 of 23 immunities |
| defs | Component tagged as the maintenance item (nutrition, days one lasts) | not seen |  |  |
| defs | Needs from other mods removed or relabelled (Dubs Bad Hygiene), or says none | not seen |  |  |
| defs | Skeleton backstories loaded | not seen |  |  |
| defs | Outlander, pirate, tribe and Empire xenotype sets include skeletons | not seen |  |  |
| patch | Every patched private method found (this line only appears when one is missing) | not seen |  |  |
| gene | Null chromosome makes a pawn genderless | passed | 7 | Acid: Female to None, body Thin |
| gene | Skeleton frame gives a male, thin or hulk body and a skeleton head | passed | 7 | Acid: body Thin, head Rimshi_Head_Screamer |
| gene | Skeletons generated without family | passed | 7 | Acid: relation generation skipped |
| gene | Removing the null chromosome gives a gender back | passed | 7 | Acid: now Male |
| gene | Relations a new genderless pawn got another way were removed (names them) | not seen |  |  |
| faction | Mod setting on: old Rim-Skeletons faction replaced by ours on load | not seen |  |  |
| faction | Mod setting on: save had no old Rim-Skeletons factions | not seen |  |  |
| faction | Debug action added the skeletal imperium to the world | not seen |  |  |
| faction | Skeletal imperium present in the loaded world (or says none) | not seen |  |  |
| migrate | Mod setting on: an old Rim-Skeletons skeleton converted to ours (lists what changed) | not seen |  |  |
| migrate | Health condition with no matching human body part dropped during conversion (names it) | not seen |  |  |
| migrate | Mod setting on: conversion summary (skeletons converted, conditions dropped) | not seen |  |  |
| migrate | Mod setting on: save had no old Rim-Skeletons skeletons | not seen |  |  |
| migrate | Mod setting on: Rim-Skeletons (Continued) not loaded, nothing to convert | not seen |  |  |
| render | Skeleton body texture drawn | not seen |  |  |
| repair | Crafter started repairing a skeleton with repair kits | not seen |  |  |
| repair | Repair used up a kit | not seen |  |  |
| repair | Repair sealed leaks or treated a tendable condition with kits | not seen |  |  |
| repair | Skeleton fully repaired by a crafter | not seen |  |  |
| bed | Non-skeleton kept out of a skeleton repair bed | not seen |  |  |
| bed | Repair bed repairing a skeleton's injury | not seen |  |  |
| bed | Skeleton fully repaired in a repair bed | not seen |  |  |
| diet | Skeleton consumed a component for maintenance (fails if it ate anything else) | not seen |  |  |
| maintenance | Skeleton out of maintenance did not get malnutrition | not seen |  |  |
| maintenance | Need bar and tooltips relabel needs (this line only appears when a patch target is missing) | not seen |  |  |
| deathrest | Skeleton went to deathrest in a repair bed (gives its chemfuel) | not seen |  |  |
| deathrest | Deathrest burning a repair bed's chemfuel | not seen |  |  |
| deathrest | Deathrest paused: out of chemfuel or not in a repair bed | not seen |  |  |
| deathrest | Skeleton due to deathrest found no roofed, fuelled repair bed | not seen |  |  |
| deathrest | Skeletons from an older save got the deathrest gene | not seen |  |  |
| reproduction | A pregnancy added to a skeleton was refused | not seen |  |  |
| horde | Recall array inherited the mechband antenna graphic | not seen |  |  |
| horde | Recall research on the research tab, overlapping nothing (gives its position) | not seen |  |  |
| horde | Bladeguard melee weapon tags (adds medieval weapons without Royalty) | not seen |  |  |
| horde | Colonist broadcast the recall codes; array burned out | not seen |  |  |
| horde | Skeleton horde dropped in (counts, kinds, weapons, shield belts, failsafe cores) | not seen |  |  |
| horde | Horde called to a map that is gone was dropped | not seen |  |  |
| horde | Horde skeleton went berserk | not seen |  |  |
| horde | Failsafe core set the death on downed chance | not seen |  |  |
| horde | A failsafe skeleton died anyway (says how; not a failure) | not seen |  |  |
| reprogram | Captured horde skeleton set to interrogate and reprogram | not seen |  |  |
| reprogram | Warden reprogramming session lowered the corruption | not seen |  |  |
| reprogram | Reprogrammed skeleton joined the colony | not seen |  |  |
| geneproof | Xenogerm implant on a skeleton refused | not seen |  |  |
| geneproof | Xenogerm reimplant ability on a skeleton refused | not seen |  |  |
| geneproof | Skeleton refused the absorb xenogerm job | not seen |  |  |
| geneproof | No xenogerm reached a skeleton past the checks (this line only appears when one did) | not seen |  |  |
| geneproof | No reimplant involving a skeleton got past the checks (this line only appears when one did) | not seen |  |  |
| geneproof | Skeleton genes kept out of a genepack (fails if a pack held only skeleton genes) | not seen |  |  |
| prosthetics | Generated skeleton got no prosthetics, bionics or implants | not seen |  |  |
| prosthetics | Skeleton refused a self-installed implant (neuroformer or another mod's; mechlinks still allowed) | not seen |  |  |
| prosthetics | A psylink was refused on a skeleton (royal title, bestowing, anima tree, dev mode) | not seen |  |  |
| prosthetics | No install surgery reached a skeleton past the checks (this line only appears when one did) | not seen |  |  |
| parts | Skeleton fabrication and advanced parts research placed on the research tab without overlaps | not seen |  |  |
| parts | A skeleton part was installed on a skeleton (names the part) | not seen |  |  |
| parts | A KLR or industrial part took reduced damage (first hit each session) | not seen |  |  |
| parts | A steady arm knocked a weapon out of an enemy's hands in melee | not seen |  |  |
| parts | A second AI core repaired a damaged one (names what it removed) | not seen |  |  |
| blood | A hit skeleton leaked ancient oil (black filth) instead of blood | not seen |  |  |
| blood | A hit skeleton showed the black oil spray instead of the red blood spray | not seen |  |  |
| blood | A sanguophage bit a skeleton and got no hemogen (bloodfeed ability or another mod) | not seen |  |  |
| drugs | A skeleton was refused a drug (ingest menu, drug policy, recreation, AI job, binge or generation; one line per route) | not seen |  |  |
| drugs | No drug reached a skeleton past the checks (this line only appears when one did; the drug did nothing) | not seen |  |  |
| selftest | Self-tests: all passed (summary line) | failed | 7 | FAILED: 21 passed, 4 failed |
| selftest | Self-test: skeleton and baseliner generate | passed | 7 | skeleton Acid, baseliner Norman |
| selftest | Self-test: no exceptions while testing | not seen |  |  |
| selftest | Self-test: skeleton has the Skeleton xenotype | passed | 7 | xenotype skeleton |
| selftest | Self-test: skeleton is genderless | passed | 7 | gender None |
| selftest | Self-test: skeleton is Asexual | passed | 7 | traits VTE_RefinedPalate, VTE_Insatiable, RT_TalentJackOfAllTrades, Asexual |
| selftest | Self-test: skeleton body is male, thin or hulk | passed | 7 | body Thin |
| selftest | Self-test: skeleton has a skeleton head | passed | 7 | head Rimshi_Head_Screamer |
| selftest | Self-test: skeleton has a plating colour | passed | 7 | skin RGBA(0.294, 0.294, 0.294, 1.000) |
| selftest | Self-test: skeleton is bald | passed | 7 | hair Bald |
| selftest | Self-test: skeleton body texture found | failed | 7 | FAILED: path Rimshi/Body/Naked_Thin |
| selftest | Self-test: skeleton head texture found | failed | 7 | FAILED: path Rimshi/Head/Screamer |
| selftest | Self-test: skeleton has no rest need | passed | 7 | rest need absent |
| selftest | Self-test: skeleton generated without family | failed | 7 | FAILED: direct relations 2 |
| selftest | Self-test: skeleton fertility is 0 | passed | 7 | fertility 0 |
| selftest | Self-test: skeleton toxic resistance 100% | passed | 7 | toxic resistance 1 |
| selftest | Self-test: skeleton psychic sensitivity 0% | not seen |  |  |
| selftest | Self-test: skeleton immune to flu | passed | 7 | immune to flu |
| selftest | Self-test: skeleton can consume components | not seen |  |  |
| selftest | Self-test: skeleton refuses meals | passed | 7 | simple meal suitable False |
| selftest | Self-test: one component lasts a skeleton 2 days | not seen |  |  |
| selftest | Self-test: skeleton consumes one component at a time | not seen |  |  |
| selftest | Self-test: skeleton food bar shown as Maintenance with the Second Empire tooltip (human keeps Food) | not seen |  |  |
| selftest | Self-test: skeleton cannot get malnutrition | not seen |  |  |
| selftest | Self-test: vanilla hunger thought nullified for skeleton | passed | 7 | NeedFood nullified |
| selftest | Self-test: maintenance thought can apply to skeleton | not seen |  |  |
| selftest | Self-test: with Dubs Bad Hygiene, no thirst or bladder and self-cleaning at half rate (passes when not loaded) | not seen |  |  |
| selftest | Self-test: skeleton has deathrest (gene, need, icon); human does not | not seen |  |  |
| selftest | Self-test: repair bed holds chemfuel for deathrest (tank and fuel per deathrest) | not seen |  |  |
| selftest | Self-test: deathrest tooltip relabelled for skeletons | not seen |  |  |
| selftest | Self-test: naked thought nullified for skeleton | passed | 7 | Naked nullified |
| selftest | Self-test: human refuses components | not seen |  |  |
| selftest | Self-test: human still eats meals | passed | 7 | simple meal suitable True |
| selftest | Self-test: human keeps gender and hunger thought | passed | 7 | gender Male, hunger thought nullified False |
| selftest | Self-test: both repair research projects need Basic mechtech | not seen |  |  |
| selftest | Self-test: repair kit recipe needs the repair kit research | not seen |  |  |
| selftest | Self-test: repair bed needs the repair bed research and an AI persona core | not seen |  |  |
| selftest | Self-test: repair kit is not medicine | not seen |  |  |
| selftest | Self-test: skeleton injury healing is 0%, human's is not | not seen |  |  |
| selftest | Self-test: skeleton repair is a Smith job using Crafting | not seen |  |  |
| selftest | Self-test: a cut skeleton needs repair, a cut human does not | not seen |  |  |
| selftest | Self-test: nothing on a skeleton is tendable with medicine; a human cut still is | not seen |  |  |
| selftest | Self-test: only skeletons can use a repair bed; skeletons can still use hospital beds | not seen |  |  |
| selftest | Self-test: removing the null chromosome restores a gender | passed | 7 | gender after removing null chromosome Male |
| selftest | Self-test: no skeleton gene can roll in gene sets | not seen |  |  |
| selftest | Self-test: every skeleton gene is marked core or chip, chip category loaded | not seen |  |  |
| selftest | Self-test: 200 random gene sets hold no skeleton gene | not seen |  |  |
| selftest | Self-test: a genepack drops skeleton genes and keeps the rest | not seen |  |  |
| selftest | Self-test: gene buildings refuse the skeleton, not the baseliner | not seen |  |  |
| selftest | Self-test: implant xenogerm bill unavailable on a skeleton | not seen |  |  |
| selftest | Self-test: every gene lock patch applied | not seen |  |  |
| selftest | Self-test: skeleton has a skeleton adulthood backstory | not seen |  |  |
| selftest | Self-test: skeletal imperium and skeleton player faction loaded | not seen |  |  |
| selftest | Self-test: a skeletal imperium pawn kind generates a skeleton | not seen |  |  |
| selftest | Self-test: skeleton cannot have children or be implanted | not seen |  |  |
| selftest | Self-test: pregnancy cannot be added to a skeleton | not seen |  |  |
| selftest | Self-test: recall array and its research loaded (2000, after basic mechtech, no mechanitor needed) | not seen |  |  |
| selftest | Self-test: horde skeletons have no faction | not seen |  |  |
| selftest | Self-test: horde skeletons have the corrupted protocol and cannot be recruited | not seen |  |  |
| selftest | Self-test: horde gunners carry spacer guns, bladeguards melee weapons and shield belts | not seen |  |  |
| selftest | Self-test: horde skeletons are not hostile to each other | not seen |  |  |
| selftest | Self-test: failsafe core sets the death on downed chance | not seen |  |  |
| selftest | Self-test: a reprogramming session lowers the corruption | not seen |  |  |
| selftest | Self-test: a bionics-rolling kind gives the skeleton none and the baseliner some | not seen |  |  |
| selftest | Self-test: no install surgery is offered on a skeleton (bionic arm and natural heart still offered on a baseliner) | not seen |  |  |
| selftest | Self-test: removing a body part is still offered on a skeleton | not seen |  |  |
| selftest | Self-test: mechlink allowed and psylink neuroformer refused for skeletons | not seen |  |  |
| selftest | Self-test: giving a skeleton a psylink adds none and no psycast, a baseliner gets one (Royalty only) | not seen |  |  |
| selftest | Self-test: the prosthetics lock patches are applied | not seen |  |  |
| selftest | Self-test: every skeleton part install is offered on a skeleton and not on a baseliner | not seen |  |  |
| selftest | Self-test: a KLR arm installs on a skeleton's shoulder | not seen |  |  |
| selftest | Self-test: 10 damage on a KLR arm makes an injury of 8 | not seen |  |  |
| selftest | Self-test: an AI core installs, and a second one repairs a damaged core | not seen |  |  |
| selftest | Self-test: skeleton cannot bleed, baseliner can | not seen |  |  |
| selftest | Self-test: a cut skeleton has bleed rate 0, a cut baseliner bleeds | not seen |  |  |
| selftest | Self-test: ancient oil filth and black hit effect loaded | not seen |  |  |
| selftest | Self-test: the blood patches are applied | not seen |  |  |
| selftest | Self-test: a skeleton cannot take beer or penoxycyline, a baseliner can | not seen |  |  |
| selftest | Self-test: the skeleton's drug policy schedules and carries no drug | not seen |  |  |
| selftest | Self-test: no administer drug bill is offered on a skeleton, some are on a baseliner | not seen |  |  |
| selftest | Self-test: no drug binge mental state can start on a skeleton | not seen |  |  |
| selftest | Self-test: the drug lock patches are applied | not seen |  |  |
| selftest | Self-test: every old skeleton body part maps to its own human part (old mod loaded) | not seen |  |  |
| selftest | Self-test: converted old skeleton is race Human with the human body | not seen |  |  |
| selftest | Self-test: converted old skeleton has the Skeleton xenotype and no gender | not seen |  |  |
| selftest | Self-test: converted old skeleton has our head and backstories | not seen |  |  |
| selftest | Self-test: converted old skeleton has our pawn kind | not seen |  |  |
| selftest | Self-test: converted old skeleton has no old hediff and only human body parts | not seen |  |  |
| selftest | Self-test: a converted skeleton is not converted again | not seen |  |  |

## Manual checks

Things the log cannot show; tick them off by hand after looking in game.

- [ ] A skeleton spawned in dev mode (xenotype Skeleton) looks right: skeleton body and head, steel or rust colour, bald, no beard, no tattoos.
- [ ] A skeleton is genderless in its info card and has the Asexual trait.
- [ ] A skeleton's needs tab shows "Maintenance" instead of "Food"; its tooltip tells of the fall of the Second Empire. A human still shows "Food".
- [ ] When maintenance drops to about 30% (about two days after a full bar), a skeleton walks to a component and strips it ("Stripping component for maintenance"), one at a time, and the bar fills back up.
- [ ] A skeleton refuses ordinary meals (right-click shows "not suitable"), and a human or animal refuses components.
- [ ] Without components, a skeleton gets "maintenance due", then "maintenance overdue", then "worn parts" at 0%, getting one stage worse per day (down to "falling apart", -22); the vanilla hunger thought shows as nullified, and it never gets malnutrition.
- [ ] Deathrest: a skeleton's needs tab has a deathrest bar with the reactor core tooltip. A repair bed shows a chemfuel gauge and haulers fill it (35). When deathrest drops to 5%, the skeleton walks to a roofed repair bed with fuel and deathrests there for about 4 days, burning the tank down (about 35); with an empty tank it pauses with a message; with no fuelled bed it never deathrests and gets deathrest exhaustion.
- [ ] With Dubs Bad Hygiene: a skeleton has no thirst or bladder bar, its hygiene bar reads "Self-cleaning" with the motor debris tooltip, and it drops about half as fast as a human's. The mechanical nature gene lists the changes and "hygiene rate factor x50%".
- [ ] Naked, dark, wet and pain thoughts show as nullified by mechanical nature.
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
- [ ] A hurt skeleton keeps working unless downed or a powered repair bed is free, then goes to it; with only scars it goes during its sleep hours.
- [ ] After the bed research, the skeleton repair bed is in Furniture and costs an AI persona core; deconstructing it gives the core back.
- [ ] The repair bed shows the repair machine art, facing the right way in all four rotations (west mirrors east), and the skeleton lies on the deck.
- [ ] The repair bed is medical by default and a hurt skeleton picks it over a hospital bed. A human cannot be assigned to or rest in it; a hurt skeleton resting in a powered one is repaired, scars included, and gets a "fully repaired" message. Unpowered, the bed says so and does nothing.
- [ ] A gene extractor will not take a skeleton (the menu option is greyed out with "Skeletons have no genes to extract").
- [ ] A xenogerm's "Implant" gizmo will not target a skeleton, and the skeleton's health tab has no "Implant xenogerm" bill.
- [ ] A sanguophage's xenogerm reimplant ability will not target a skeleton.
- [ ] Skeleton genes never show up in traders' or quest genepacks, so the gene assembler never offers them. The custom xenotype editor at game start still does (on purpose).
