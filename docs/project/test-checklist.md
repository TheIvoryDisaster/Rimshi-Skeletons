# Test checklist

What has been seen working in game, read from the logs. Every test-worthy event writes a line to Player.log in a fixed shape:

    [Rimshi][T] area | key | detail

A failure writes the same line as a warning with `FAILED:` at the start of the detail. The game also logs a build line at startup (version, build number, who built it and when, which DLCs are active), so every result is tied to a build.

**How to use it:** play with Development mode on, then tell Claude to check the logs. Claude pulls Player.log and Player-prev.log from `C:\Users\Matthew\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios`, runs `tools/rimshi_testlog.py` over them and updates this table. Results add up across sessions; any exception that mentions the mod is reported too. A PR that only updates this file is merged by Claude without asking, and its description lists every test that ran and its outcome.

**Self-tests:** with Development mode on, loading or starting a game runs the in-game self-tests once per session (also on demand: Debug actions > Rimshi Skeletons > Run self-tests). They generate a skeleton, a baseliner and a skeletal imperium town guard off the map, check genes, looks, diet, thoughts, immunity, the gene lock, backstory, faction, reproduction, the recall array and horde skeletons (gear, protocol, failsafe core, one reprogramming session), discard the pawns, and log one `selftest` line per check plus a summary.

**Unit tests:** `python3 -m unittest discover -s tests` runs the offline tests (def cross-references, textures, checklist coverage, the log reader, house style). CI runs them on every push.

**Adding tests:** when code logs a new test line, add a row to `CHECKLIST` in `tools/rimshi_testlog.py` and to the table below.

**Status:** passed, failed, passed after fail (it failed once and later worked), or not seen.

| Area | Item | Status | Build | Last line |
| --- | --- | --- | --- | --- |
| build | Build line at startup | passed | 7 | version dev, build 7 (DESKTOP-ESRRCQ3 2026-10-24T18:45Z), dll written 2026-10-24 18:45 UTC; biotech True, royalty True, ideology True, odyssey True |
| defs | Blocked thoughts linked at startup (lists any not loaded) | passed | 7 | 27 of 30 blocked; not loaded: RG_Wet, RG_ExtremelyWet, RG_HotSpringWet |
| defs | Vanilla hunger thought nullified by the fuel reactor | passed | 7 | NeedFood nullified by Rimshi_FuelReactor |
| defs | Immunity list linked at startup (lists any not loaded) | passed | 7 | 23 of 23 immunities |
| defs | Chemfuel and uranium tagged as edible fuel | passed | 7 | Uranium: nutrition 0.5, food type Fluid |
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
| kit | Medicine skipped for a skeleton, or a repair kit skipped for a non-skeleton | not seen |  |  |
| bed | Non-skeleton kept out of a skeleton repair bed | not seen |  |  |
| bed | Repair bed repairing a skeleton's injury | not seen |  |  |
| bed | Skeleton fully repaired in a repair bed | not seen |  |  |
| diet | Skeleton ate chemfuel or uranium (fails if it ate anything else) | not seen |  |  |
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
| selftest | Self-test: skeleton immune to flu | passed | 7 | immune to flu |
| selftest | Self-test: skeleton can eat chemfuel and uranium | failed | 7 | FAILED: chemfuel and uranium suitable |
| selftest | Self-test: skeleton refuses meals | passed | 7 | simple meal suitable False |
| selftest | Self-test: skeleton hunger well below human | passed | 7 | skeleton/human hunger 0.225 |
| selftest | Self-test: vanilla hunger thought nullified for skeleton | passed | 7 | NeedFood nullified |
| selftest | Self-test: fuel need thought can apply to skeleton | passed | 7 | Rimshi_FuelNeed can apply |
| selftest | Self-test: naked thought nullified for skeleton | passed | 7 | Naked nullified |
| selftest | Self-test: human refuses chemfuel | passed | 7 | chemfuel suitable False |
| selftest | Self-test: human still eats meals | passed | 7 | simple meal suitable True |
| selftest | Self-test: human keeps gender and hunger thought | passed | 7 | gender Male, hunger thought nullified False |
| selftest | Self-test: both repair research projects need Basic mechtech | not seen |  |  |
| selftest | Self-test: repair kit recipe needs the repair kit research | not seen |  |  |
| selftest | Self-test: repair bed needs the repair bed research and an AI persona core | not seen |  |  |
| selftest | Self-test: repair kit is medicine as strong as industrial medicine | not seen |  |  |
| selftest | Self-test: repair kits only for skeletons, medicine only for non-skeletons | not seen |  |  |
| selftest | Self-test: only skeletons can use a repair bed; skeletons can still use hospital beds | not seen |  |  |
| selftest | Self-test: removing the null chromosome restores a gender | passed | 7 | gender after removing null chromosome Male |
| selftest | Self-test: no skeleton gene can roll in gene sets | not seen |  |  |
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
- [ ] A hungry skeleton walks to chemfuel or uranium and eats it ("Fueling reactor with chemfuel").
- [ ] A skeleton refuses ordinary meals (right-click shows "not suitable"), and a human or animal refuses chemfuel.
- [ ] A hungry skeleton gets the "fuel need" thought; the vanilla hunger thought shows as nullified.
- [ ] Naked, dark, wet and pain thoughts show as nullified by mechanical nature.
- [ ] A skeleton never needs sleep (no rest bar).
- [ ] A new world lists the skeletal imperium in the faction setup (one by default) and it appears on the world map.
- [ ] Skeletal imperium visitors, traders and raids are mostly skeletons (with some human villagers and guards), with skeleton names and backstories.
- [ ] The "Embarking 'Skeleton'" scenario starts with one skeleton in the Skeleton Foundation faction.
- [ ] Mod settings show "Convert Rim-Skeletons (Continued) factions and skeletons when a save loads"; with it on and the old mod loaded, an old save's skeletal imperium shows our faction icon and keeps its name and goodwill.
- [ ] With the setting on, a "Skeletons converted" letter names each old skeleton; they look like our skeletons (body, head, plating, bald), keep their names, skills, traits, gear and injuries, and show as xenotype Skeleton. After saving, removing Rim-Skeletons (Continued) and loading again, they are still there and the log has no "Null key" or "Could not resolve reference" errors for them.
- [ ] In a world made before this mod, Debug actions > Rimshi Skeletons > Add the skeletal imperium adds the faction with one settlement.
- [ ] Research tab: "skeleton repair kits" and "skeleton repair beds" sit right of Basic mechtech and need it; neither needs a mechanitor.
- [ ] After the kit research, the machining table has "make skeleton repair kit"; kits look like grey industrial medicine.
- [ ] A doctor tends a hurt skeleton with a repair kit (not medicine), and a hurt human with medicine (not a kit). With no kits, the skeleton is tended without medicine.
- [ ] After the bed research, the skeleton repair bed is in Furniture and costs an AI persona core; deconstructing it gives the core back.
- [ ] A human cannot be assigned to or rest in the repair bed; a hurt skeleton resting in a powered one is repaired, scars included, and gets a "fully repaired" message. Unpowered, the bed says so and does nothing.
- [ ] A gene extractor will not take a skeleton (the menu option is greyed out with "Skeletons have no genes to extract").
- [ ] A xenogerm's "Implant" gizmo will not target a skeleton, and the skeleton's health tab has no "Implant xenogerm" bill.
- [ ] A sanguophage's xenogerm reimplant ability will not target a skeleton.
- [ ] Skeleton genes never show up in traders' or quest genepacks, so the gene assembler never offers them. The custom xenotype editor at game start still does (on purpose).
