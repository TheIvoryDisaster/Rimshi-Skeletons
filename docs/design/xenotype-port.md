# Rimshi Skeletons: design

Status: (draft) 2026-10-11, revised 2026-10-24 with Matthew's answers. Build steps 1 and 2 (core xenotype, diet) implemented 2026-10-24; see Implemented below.

## Goal

Reimplement **Rim-Skeletons (Continued)** (Workshop 3674548651, packageId `zal.rimskeletons`, by Zaljerem, from VexedTrees' original 2577145706) without Humanoid Alien Races (HAR). Skeletons become a **Biotech xenotype on the vanilla Human race**, built from genes, a few small Harmony patches and ordinary defs.

Hard requirements: RimWorld 1.6, Biotech, Harmony. Everything else (Royalty, Ideology, Odyssey, other mods) is optional and must have a fallback (see Soft dependencies).

## What the original does

Read from the original's files (local folder "3674548651", 1.6 only):

| Area | Original (HAR) | Where |
| --- | --- | --- |
| Race | `Skeleton_` ThingDef_AlienRace, parent Human. Genderless (`hasGenders` false), asexual, life expectancy 2500, age 480 to 2500, body size 1.1, health scale 1.8, `needsRest` false, near-zero hunger. | Defs/AlienRaces/RimskeletonRace.xml |
| Stats | Psychic sensitivity 0.5, toxic resist 1, comfy temp -100 to 250, flammability 0, armor 0.12 blunt / 0.4 sharp / 2.0 heat, move speed 4.25, research 1.3, plants 0.5, social impact 0.5, mental break threshold 0.2, pain shock 0.5, melee dodge 0.5, fertility 0, Odyssey vacuum resistance 1, market value 5800. | same |
| Melee | Fists 8.5 blunt, surprise attack stun. | same |
| Body | Custom `SkeletonBody`: mechanical thorax, reactor, fluid reprocessor, AI core (brain), visual sensors, mech arms, legs etc. Fleshtype `VexedMechanical` (mech wound graphics), blood is machine bits, no bleeding. | Defs/Bodies, Defs/Misc/FleshTypes |
| Looks | Body textures Male / Thin / Hulk, six head types (Mki, Log, Lion, Scout, Catlon, Screamer), steel or rust skin colours, no hair, beard or tattoos. | Textures/SkeletonRace, Defs/HeadTypeDefs |
| Food | Eats only chemfuel and uranium. A patch makes both ingestible (`Nutrition` 0.08 and 0.5, "Fuel reactor with"). | Patches/EdibleChemfuelUranium_Patch.xml |
| Immunity | Hidden hediff `MechanicalNature_Hediff` (given on spawn) makes them immune to food poisoning, toxic buildup, overdose, heart attack, carcinoma, parasites, infection, flu, plague, malaria, sleeping sickness, chemical damage, frostbite, cirrhosis, scaria and eye grit. | Defs/HediffDefs, Defs/HediffGiverSetDefs |
| Drugs | Cannot ingest smokeleaf, alcohol, psychite, wake-up, go-juice (plus some modded chemicals). | race `chemicalSettings` |
| Traits | Always Asexual; Pessimist 45%, Depressive 25%. Disallowed: body purist, gay, bisexual, cannibal, nudist, gourmand, creepy breathing, super-immune, plus VTE traits. | race |
| Thoughts | ~30 thoughts blocked (naked, pain, hunger, dark, wet, ate without table, human leather...), replacements for hunger ("fuel need"), sickness, prosthophile, mech cluster defeated, medical specialist, look change. Butchering a skeleton gives nobody a bad thought. | Defs/ThoughtDefs, race |
| Apparel | Gas masks, psychic foil, psyfocus helmet, eltex skullcap, torture crown and similar head items blocked. | race `blackApparelList` |
| Actions | Cannot vomit, use a biosculpter, absorb xenogerm, or enter a gene building. No breath vapour in the cold. Corpses never rot and are inedible. | C# PawnsMechanical.dll |
| Corpses | Shred (machining table) or smash (crafting spot) for steel 50, plasteel 40, uranium 12. | Defs/RecipeDefs/Recipes_Shred.xml |
| Surgery | About 50 recipes re-target every prosthetic, bionic and archotech limb (vanilla, Royalty, EPOE) at the mechanical body parts, using crafting instead of medicine. "Sever AI core" execution. | Defs/RecipeDefs |
| Spawning | Very rare: starting colonist 0.1%, wanderer 0.9%. Pawn kinds injected into outlander, pirate, tribe and Empire pawn groups. Their own NPC faction "skeletal imperium" with a full military roster (villager to emperor, hydraulic knight). A player faction and an "Embarking skeleton" scenario (one pawn, steel). | Defs/PawnKindDefs_Humanlikes, Defs/FactionDefs, Patches/FactionRedistribute_Patch.xml, Defs/Scenario |
| Flavour | 74 skeleton backstories, skeleton name maker (names, adjectives, syllables), faction names. | Defs/BackstoryDefs, Defs/Names, Languages/English/Strings |
| Compat | Dubs Bad Hygiene (no bladder, hygiene, thirst), Altered Carbon (no stacks or sleeves), Questionable Ethics (no cloning), SOS2 trade tag, Royalty (Empire hostile to the skeleton player faction). | Patches |

## How it maps onto Biotech

The xenotype is `Rimshi_Skeleton` (all our defs use the `Rimshi_` prefix). It is **inheritable** (germline) so it survives Biotech's xenotype rules, though skeletons cannot reproduce anyway.

Checked against the decompiled 1.6 source on 2026-10-24: `GeneDef` has `disablesNeeds`, `makeImmuneTo`, `fur`, `bodyType`, `forcedHeadTypes`, `hairTagFilter`, `skinColorOverride`, `forcedTraits`, `suppressedTraits`, `statFactors`; `XenotypeDef` has `nameMaker` / `nameMakerFemale`; `ThoughtDef` has `nullifyingGenes` and `requiredGenes`; `Hediff_Injury.BleedRate` is 0 on a part covered by a non-organic added part. Rows still marked **verify** were not checked yet.

| Original feature | Plan | Mechanism |
| --- | --- | --- |
| No sleep | Vanilla Never sleep gene behaviour. | Gene `disablesNeeds: Rest` (as vanilla `Neversleep`). |
| Fuel diet | Food need stays, at a much lower rate, and skeletons only eat chemfuel and uranium. Humans and others will not eat them. | Patch making chemfuel and uranium ingestible and tagging them with `FuelExtension` (as the original, with PatchOperationTest guards); Harmony postfix on `FoodUtility.FoodIsSuitable` (what `WillEat`, food search and the ingest menu ask) checking the tag against the gene both ways; postfix on `Need_Food.FoodFallPerTickAssumingCategory` for the hunger rate (x0.1), since no gene stat covers hunger. |
| Fuel thoughts | Hunger thoughts read as "fuel need" for skeletons only. | New ThoughtDef using `ThoughtWorker_NeedFood` with `requiredGenes`; vanilla `NeedFood` gets `nullifyingGenes`. |
| Blocked thoughts | Same list, where the thought still exists in 1.6. | At startup, add the marker gene `Rimshi_MechanicalNature` to each ThoughtDef's `nullifyingGenes`, looked up by name so a missing DLC or mod is skipped and logged. |
| Disease and chemical immunity | Same immunity list. | `makeImmuneTo` on the marker gene with the original's list, filled at startup by name the same way (no hidden hediff needed). |
| Drugs | Cannot take the same drugs; addiction immune. | Vanilla `AddictionImmune_*` style chemical genes plus a Harmony check in the ingest path for the blocked chemicals. **verify** where ingest is decided. |
| Stats | Same values where a gene can express them. | `statOffsets` / `statFactors` on several genes so the gene list reads well in the xenotype editor (armour, temperature, toxic, psychic, work speeds, movement). Odyssey vacuum resistance under `MayRequire`. |
| Health scale 1.8, body size 1.1 | Body size stays human. Health scale via a gene. | No vanilla gene stat for health scale; Harmony postfix on `Pawn.HealthScale`. **verify** property name. |
| Pain | Pain shock threshold 0.5 and no pain thoughts. | Stat factor on pain shock threshold; vanilla `Pain_Reduced` style behaviour optional. |
| Looks: body | Skeleton body textures replace the human body. | Harmony postfix on `PawnRenderNode_Body.GraphicFor` returns our body texture (same shader and skin colour) for pawns with `Rimshi_SkeletonFrame`. Not a FurDef: fur is drawn on top of the human body and tinted with hair colour, so the thinner skeleton art would show human skin around it. |
| Looks: body types | Male, Thin and Hulk only. | The frame gene swaps Female and Fat for Male when added; the body patch also draws Male art for them as a fallback. |
| Looks: heads | The six skeleton heads. | `forcedHeadTypes` on a gene, HeadTypeDefs with gender Any. |
| Looks: colour, hair | Steel and rust colours, bald, no beard, no tattoos. | Three plating genes in the xenotype (steel, aged steel, rust; Biotech picks one at random, about two in three steel), plus plasteel and mech black for custom xenotypes; vanilla `Hair_BaldOnly` and `Beard_NoBeardOnly`; `tattoosVisible` false on the frame gene. |
| Genderless, asexual | Genderless (Gender.None), always Asexual and sterile. | A "null chromosome pattern" gene, done the way Rim-Hivers! - Biotech (VexedTrees, the original skeleton author) does it: Harmony postfixes on `PawnGenerator.GenerateGenes` and `Gene.PostAdd` set `pawn.gender = Gender.None` and swap a Female body type to Male. Our own code, no dependency on Rim-Hivers or its framework. Plus `forcedTraits: Asexual`, fertility stat factor 0. Rim-Hivers also skips relation generation for its pawns; we do the same (the original's relation chances are all 0). |
| Pessimist / Depressive odds | 45% / 25%. | Harmony postfix on pawn generation for skeletons only (genes cannot force a trait at a chance). |
| Disallowed traits | Same list. | `suppressedTraits` where it makes sense plus a generation filter. |
| Age | Thousands of years old, ageless. | Vanilla Ageless behaviour, and a generation postfix that sets a chronological age between 480 and 2500. |
| Mechanical body | Keep the human BodyDef (a xenotype cannot change it) and rename the parts, see Anatomy below. No bleeding, machine-bit "blood", mechanical wound overlays and hit sound. | See Anatomy. |
| Surgery | Prosthetics are locked for now (Matthew, 2026-10-25): skeletons spawn with no prosthetics, bionics or implants and no install surgery (artificial part, implant or natural organ, from any mod) is offered on them; removal still works. Skeleton parts of our own come later behind research and a skeleton workbench. | Skeleton/ProstheticsLock.cs (PawnTechHediffsGenerator prefix, Recipe_Surgery.AvailableOnNow postfix, install ApplyOnPawn guard). Recipes named Rimshi_ are exempt. |
| Sever AI core | Kept as a recipe for skeletons only. | RecipeDef with `Recipe_ExecuteByCut`, `recipeUsers` Human, hidden unless the patient has the gene (custom worker `AvailableOnNow`). |
| Corpses | Never rot, inedible, shred / smash for steel, plasteel, uranium. | Harmony on rot and `IngestibleNow`; butcher products swapped when the corpse's pawn is a skeleton; shred and smash recipes use a `SpecialThingFilterWorker` "skeleton corpse". |
| Blocked apparel | Same list (vanilla and DLC defs under `MayRequire`). | Harmony on `EquipmentUtility.CanEquip`. |
| Blocked actions | Vomit, biosculpter, breath vapour. | Ports of the original's two patches, keyed on the gene instead of a mod extension. |
| Gene lock (done 2026-10-25) | Skeleton genes stay out of Biotech's gene trade both ways, as the original blocked AbsorbXenogerm and EnterBuilding. | `GeneProofing.cs`, all keyed on "carries any gene in the skeleton category": postfixes on `Building_GeneExtractor.CanAcceptPawn`, `Xenogerm.ValidateTarget`, `Recipe_ImplantXenogerm.AvailableOnNow`, `CompAbilityEffect_ReimplantXenogerm.Valid`; prefix on `JobDriver_AbsorbXenogerm.TryMakePreToilReservations`; last guards on `GeneUtility.ImplantXenogermItem` and `GeneUtility.ReimplantXenogerm` (logged as failures if reached); `canGenerateInGeneSet` false on every skeleton gene plus a `Genepack.Initialize` filter, so the assembler never gets them. Not covered: dev mode and other mods calling `AddGene` directly; the custom xenotype editor keeps the genes on purpose. Reproduction is already impossible (null chromosome). |
| Spawning in other factions | Same rarity: skeletons appear among outlanders, pirates, tribes and the Empire. | Patch each FactionDef's `xenotypeSet` to add `Rimshi_Skeleton` at a small chance (instead of injecting pawn kinds). Empire patch only if Royalty is active. |
| Starting pawn and wanderer chance | 0.1% / 0.9%. | Player faction `xenotypeSet`; wanderer joins inherit it. **verify** which faction wanderers draw from. |
| Skeleton backstories | Skeletons always get skeleton backstories. | Convert the 74 `AlienBackstoryDef`s to vanilla `BackstoryDef` (category `Rimshi_Skeleton`), and a generation postfix that rerolls backstories for skeleton pawns. |
| Skeleton names | Skeleton name maker. | XenotypeDef `nameMaker` with `chanceToUseNameMaker` 1. |
| Skeletal imperium faction | Kept, with its roster. | FactionDef with `xenotypeSet` 100% skeleton and pawn kinds that are ordinary humanlike kinds (no race field). |
| Player faction and scenario | Kept. | Player FactionDef with skeleton xenotype set, ScenarioDef with one skeleton starting pawn. |

## Implemented

Build steps 1 and 2, 2026-10-24:

- Genes (`Defs/GeneDefs/Rimshi_Genes.xml`, category "skeleton"): `Rimshi_SkeletonFrame` (heads, body art, no tattoos), `Rimshi_MetalChassis` (armour, temperature, fire, speed, carrying, dodge, pain shock, Odyssey vacuum), `Rimshi_MechanicalNature` (immunities, toxins, darkness, psychic sensitivity, blocked thoughts), `Rimshi_ArchaicMind` (mental break, social, research, plants, mining), `Rimshi_FuelReactor` (diet), `Rimshi_NullChromosome` (genderless, Asexual, fertility 0), five plating colours.
- Xenotype `Rimshi_Skeleton` with those genes plus vanilla `Neversleep`, `Ageless`, `Hair_BaldOnly`, `Beard_NoBeardOnly`. `factionlessGenerationWeight` 0 until step 3 sets the rarity.
- Six head types `Rimshi_Head_*` (`requiredGenes` the frame gene, never random). Textures under `Textures/Rimshi`; the original lacked `Naked_Thin_southm`, so a copy of its stand-in file is used.
- Fuel: `Rimshi_FuelNeed` thought (seven stages, as `ThoughtWorker_NeedFood` needs), vanilla `NeedFood` nullified by the fuel reactor.
- Known side effect, as in the original: chemfuel and uranium count toward the colony's food total, because they are ingestible with a human food type.
- Not yet: health scale 1.8 and the fist attacks (no gene field; step 4), trait odds, disallowed traits, drugs and age (step 6).

Build step 3 (spawning), 2026-10-24, with the faction part of the save converter because Matthew asked for both together:

- Skeletal imperium `Rimshi_SkeletalImperium` (`Defs/FactionDefs`): the original's FactionDef, roster, traders, memes and raid loot. One is generated with a new world (the original's default was none; up to 9999 in the faction setup). Its roster is the original's mix of skeleton kinds and vanilla human villagers and guards, so the faction has no xenotype set; each skeleton kind carries its own (`xenotypeSet` 100% `Rimshi_Skeleton`, `useFactionXenotypes` false). This replaces the "100% skeleton xenotype set" plan above, which would have turned the human villagers into skeletons.
- Pawn kinds (`Defs/PawnKindDefs`): the original's imperium kinds (villager to emperor, hydraulic knight), town guard, drifter and colonist on race Human. Its tribal, pirate and Empire skeleton kinds are not ported. Packed meals dropped (`invNutrition` 0).
- Spawning elsewhere (`Patches/Rimshi_FactionXenotypes.xml`): skeleton xenotype chance 1% for outlanders, pirates and the Empire (Royalty, found by mod name), 0.5% for tribes. Biotech factions with their own xenotype sets (`Inherit="False"`) get none. Factionless pawns: `factionlessGenerationWeight` 0.5 (about 1%). The player faction has no xenotype set (vanilla player factions have none either), so the original's 0.1% starting colonist chance is not reproduced.
- Player faction `Rimshi_PlayerSkeletons` and scenario `Rimshi_SkeletonDrifter` ("Embarking 'Skeleton'": one skeleton, picked with the vanilla xenotype starting-pawn part). The Empire is not a permanent enemy of the skeleton player faction, as for vanilla player factions.
- Backstories (`Defs/BackstoryDefs`): the original's 37 (5 childhood, 32 adulthood) as vanilla `BackstoryDef`, category `Rimshi_Skeleton`. `Patch_SkeletonBackstories` gives every adult skeleton these, in any faction. Vanilla has no trait chances, so only the certain traits are kept (FastLearner and Psychopath disallowed, Undergrounder forced); the 50%-or-less ones wait for step 6.
- Names (`Defs/RulePackDefs`, word lists in `Languages/English/Strings/Rimshi`): skeleton person names through the xenotype's `nameMaker`, always two parts; faction names from the original's namer.
- Mod settings: **Replace Rim-Skeletons (Continued) factions when a save loads** (off by default). On load it swaps the def of any faction using `Archaic_SkeletonEmpire` or `PlayerColonySkeleton` to ours, keeping the faction (name, goodwill, settlements, members). The old skeleton pawns themselves are still converted in step 5.
- Debug action **Add the skeletal imperium** for worlds generated before the mod was added.
- Fixes from Matthew's first self-test run (build 7): skeletons could not eat fuel while Rim-Skeletons (Continued) was loaded, because its Humanoid Alien Races food restriction keeps chemfuel and uranium from every race but its own, humans included; the diet patch now skips the race check for reactor pawns eating fuel. The texture checks ran off the main thread (self-tests now wait until loading finishes). Relations that reach a new genderless pawn by another route than relation generation are removed and logged (`gene | relations cleared`) so the route shows in the next log.

## Anatomy

Matthew chose renaming (2026-10-24). A xenotype cannot have its own BodyDef: the body comes from the race, and every pawn of the Human race shares one. Two ways to make the human body read as a skeleton:

**A. Rename only (small).** One Harmony postfix on the body part label (`BodyPartRecord.Label` / `LabelCap`, **verify** names) that maps human names to the original's for skeleton pawns: brain to AI core, heart to reactor, lungs and kidneys to fluid reprocessors, stomach to fuel intake, eyes to visual sensors, ears to sound analyzers, nose to chemical analyzer, jaw to linguistics operator, and so on. Plus the bleed rate and blood patches from the table. Organs still behave as organs: organ harvesting gives human organs, and a destroyed reactor is a destroyed heart.

**B. Android-style parts (medium).** What Vanilla Races Expanded - Android does (checked in its 1.6 files): androids keep the human body, and every organ carries an "android part" hediff (a `Hediff_AddedPart` subclass with `countsAsAddedPartOrImplant` and its own label: neuropump, reactor, air filter...), with their own items and install recipes. Vanilla then does a lot by itself: injuries on a non-organic added part never bleed (`Hediff_Injury.BleedRate`), no infection, the health tab shows the part's name, and removing it yields our item instead of a human organ. VRE Android adds about 40 Harmony patches on top for polish (health tab, surgery options, death and downed checks, anaesthesia). For us the core is: a gene that adds the part hediffs on `PostAdd`, about 12 part hediffs with matching items (salvaged reactor, fluid reprocessor...), crafting-skill install recipes, `countsAsAddedPartOrImplant` false so transhumanist and body purist thoughts ignore them, and a handful of patches (no anaesthesia, hide the medical options that make no sense). Bionics still install over them as usual.

Decided 2026-10-24: B (android-style parts), built after the core xenotype works, with A's label patch as a fallback for anything not covered by a part hediff (head, torso, limbs).

## Soft dependencies

The rule: anything that needs another mod either loads only when that mod is active, or the mod is a declared requirement. Requirements today: Harmony, Biotech. Optional content uses one of:

- `MayRequire="<packageId>"` on single list items or defs,
- `PatchOperationFindMod` with the mod's **name** in patches,
- `Mods/<Name>` folders loaded with `IfModActive` in LoadFolders.xml for whole files,
- `ModsConfig.IsActive` / `ModLister` checks in C#, and never a hard type reference to another mod's assembly.

| Mod | Original | Plan | Fallback without it |
| --- | --- | --- | --- |
| Royalty | Empire pawn groups, Empire hostile to skeleton player faction, Royalty prosthetics. | Empire `xenotypeSet` patch and hostility patch via FindMod. Royalty prosthetics need nothing (human body). | Skeletons simply never appear in the Empire. |
| Ideology | Several blocked thoughts, faction memes. | `MayRequire="Ludeon.RimWorld.Ideology"` on each. | Those thoughts do not exist, nothing to block. |
| Odyssey | Vacuum resistance 1. | Stat under `MayRequire`. | No vacuum stat. |
| Dubs Bad Hygiene | Exempts SkeletonBody from bladder, hygiene, thirst. | Body exemption no longer works (human body). Decompile DBH to find a gene or pawn level exemption; else a small `Mods/DubsBadHygiene` patch. **open** | Needs present as for humans until solved. |
| Altered Carbon | No stacks, no sleeves for the race. | Their exclusion is per race. Needs a check of AC's code for a gene or pawn hook. **open** | AC treats skeletons as humans. |
| Questionable Ethics | No cloning. | Same: per race exclusion. Decompile QE. **open** | Skeletons can be cloned. |
| Save Our Ship 2 | Trade tag. | The original patch targets a defName that does not exist (`RimskeletonRace`), so it never applied. Dropped. | None. |
| EPOE, VTE, VBE, VFE Insectoids, CE, VAE, Kenshi Armory | Recipes, disallowed traits, chemicals, blocked apparel. | Same items under `MayRequire`. EPOE recipes are not needed. | Items skipped. |
| Rim-Robots, androids mods | `notXenophobistTowards`. | HAR only; no Biotech equivalent. Dropped. | None. |

Build step 5 (save converter, pawn part), 2026-10-25, brought forward because Matthew's save still had two old skeletons (a colonist and a raid leader) that removing the old mod would delete, leaving Vanilla Memes Expanded and others with null references on load:

- `OldSkeletonConversion` (`Source/RimshiSkeletons/Skeleton`), run after the faction swap when the setting is on (renamed **Convert Rim-Skeletons (Continued) factions and skeletons when a save loads**), and from Debug actions > Rimshi Skeletons > Convert old skeletons now. It waits until loading finishes, because spawned pawns leave and re-enter the map while their race changes (map lists are kept per ThingDef).
- Finds every pawn (maps, containers, caravans, world pawns, dead world pawns, corpses on maps and in graves) that has race `Skeleton_`, an old pawn kind, or the old hidden hediff.
- Race: the def becomes Human; Humanoid Alien Races' comps are dropped, Human comps it lacked are added, the others keep their state; the verb tracker is rebuilt for the human fists. A corpse gets the human corpse def too. The thing ID changes from `Skeleton_N` to `HumanN`; every reference is saved again under the new ID, so this is safe.
- Health: conditions are moved, not re-added (same severity, age, comps), onto the human part with the mapped def (`OldParts`: thorax to torso, reactor to heart, fluid reprocessor to a kidney, sensors to eyes, ear and nose, and so on), matched by side and thumb, then in order. Anything without a target is dropped, logged (`migrate | lost`) and listed in the letter. This replaces the "clear health first" plan above: bionics and scars carry over.
- Age: the old race lived to 2500, so biological age is scaled to the same share of a human life (a 1196-year-old becomes 38), never below adult. Chronological age is kept.
- Then the skeleton xenotype (genes, genderless, looks), old backstories to ours by title (random skeleton backstory if none matches), old heads `None_X` to `Rimshi_Head_X`, bald and no beard, old pawn kinds to ours (`OldKinds`; the unported tribal, pirate and Empire kinds go to the drifter, or colonist for the player), and old-mod hediffs and memories removed.
- A "Skeletons converted" letter names each pawn and says the old mod can now be removed. Self-tests (old mod loaded only) check the part mapping and convert a generated old skeleton.

## Converting saves from Rim-Skeletons (Continued)

Matthew asked (2026-10-24) for a mod option that converts the old mod's skeletons in a save into ours, so the old mod can then be removed. He has both loaded and reports the old mod's skeletons show up as plain humans.

Mod settings get a checkbox (off by default). Its faction part is built (step 3): **Replace Rim-Skeletons (Continued) factions when a save loads**; step 5 extends the same option to the pawns. With it on, after a save finishes loading (`GameComponent.LoadedGame`) we walk every pawn the game knows (maps, caravans, world pawns, corpses, pawns in containers such as cryptosleep caskets and transport pods) and convert each one that is an old skeleton:

- **Detect:** race defName `Skeleton_`, or one of the old pawn kinds (`Skeleton_Colonist`, `Skeleton_Drifter`, `Town_Guard_Skele`, `Tribal_Skeleton`, `Pirate_Skele`, `Skele_Spacer_Reaper`, the `Skeleton_*` military kinds), or an old `MechanicalNature_Hediff`. Matching on kind and hediff as well as race also catches skeletons that already show up as humans.
- **Convert:** race to Human (if it is still `Skeleton_`), clearing health first because the old body's parts do not map onto the human body (permanent injuries and old prosthetics are listed in the log and a letter, since they cannot carry over); set the xenotype to `Rimshi_Skeleton` (which applies the genes, genderless, looks and anatomy); map old backstories, head types and pawn kinds to ours by defName; remove the old hidden hediff.
- **Factions:** pawns of the old skeletal imperium and old skeleton player faction move to ours by FactionDef where the faction itself can be switched; otherwise they keep their faction and only the pawn changes.
- **Log:** one `[Rimshi][T] migrate | <pawn> | from ... to ...` line per pawn and a summary letter ("Converted N skeletons; you can now save, quit and remove Rim-Skeletons (Continued)").

The safe order for the player is: keep both mods, turn the option on, load, check the letter, save, quit, remove the old mod, load again. The conversion only works while the old mod is loaded, because once it is gone RimWorld drops any pawn whose race def is missing.

Stretch, if needed: also handle saves where the old mod was already removed, by remapping its defNames on load (`BackCompatibility.BackCompatibleDefName`, `GetBackCompatibleBodyPartIndex`, both present in 1.6) to Human and our defs. More fragile, so only if Matthew needs it.

Why the old skeletons show up as humans is worth a look in his Player.log while building this (it could be Humanoid Alien Races loading after it, an error in the old mod's patches, or a load order problem); the converter does not depend on the answer.

## Assets

The textures (body, heads, world icon) and the backstory and name text come from the original mod, by VexedTrees (Rim-Skeletons, Workshop 2577145706), continued by Zaljerem (Workshop 3674548651). Matthew decided (2026-10-24) to use them with credit, as is usual for continuations of mods whose author has stopped updating. The About.xml description and a CREDITS section in the README name both authors and link both Workshop pages.

## Decisions

Answered 2026-10-24:

1. Gender: genderless, the Rim-Hivers way.
2. Body: android-style mechanical parts (Anatomy option B).
3. Compatibility with Rim-Skeletons (Continued): no incompatibility flag and no compatibility work; this mod is for people who cannot use the other one. The only overlap is the save converter.
4. Assets: use the original art with credit.

## Build order (proposed)

1. Xenotype with genes for needs, stats, immunity, looks, and the genderless patch, checked in game.
2. Diet patches and fuel thoughts.
3. Spawning: faction xenotype sets, player faction, scenario, backstories, names. Done 2026-10-24, see Implemented.
4. Mechanical body behaviour and anatomy, corpses, shred / smash, sever AI core.
5. Save converter from Rim-Skeletons (Continued). Done 2026-10-25, see Implemented.
6. Trait odds, blocked apparel and actions, age.
7. Soft dependency patches (Royalty, Ideology, Odyssey, then DBH, AC, QE after decompiling them).

Each step adds `[Rimshi][T]` log lines and rows to the test checklist.
