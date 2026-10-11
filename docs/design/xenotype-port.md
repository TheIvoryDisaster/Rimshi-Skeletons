# Rimshi Skeletons: design

Status: (draft) 2026-10-11. Nothing here is built yet; this is the plan for review.

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

Facts about vanilla fields below are from memory and must be checked against the decompiled source (`<save data folder>\Decompiled\Assembly-CSharp`) before building. Rows marked **verify** are the ones most likely to be wrong.

| Original feature | Plan | Mechanism |
| --- | --- | --- |
| No sleep | Vanilla Never sleep gene behaviour. | Gene `disablesNeeds: Rest` (as vanilla `Neversleep`). |
| Fuel diet | Food need stays, at a much lower rate, and skeletons only eat chemfuel and uranium. Humans and others will not eat them. | Gene stat factor on hunger rate; patch making chemfuel and uranium ingestible (as the original, with PatchOperationTest guards); Harmony postfix on `FoodUtility.WillEat` (both overloads) checking the gene both ways. **verify** method names. |
| Fuel thoughts | Hunger thoughts read as "fuel need" for skeletons only. | New ThoughtDef using `ThoughtWorker_NeedFood`, `requiredGenes`; vanilla `NeedFood` gets `nullifyingGenes`. **verify** `requiredGenes` on ThoughtDef; else a 10 line ThoughtWorker subclass. |
| Blocked thoughts | Same list, where the thought still exists in 1.6. | Patch each ThoughtDef's `nullifyingGenes` to add the marker gene `Rimshi_MechanicalNature`. |
| Disease and chemical immunity | Same immunity list. | Marker gene's custom `Gene` class adds the hidden `Rimshi_MechanicalNature` hediff in `PostAdd` and removes it in `PostRemove`. The hediff keeps the original's `makeImmuneTo` list. |
| Drugs | Cannot take the same drugs; addiction immune. | Vanilla `AddictionImmune_*` style chemical genes plus a Harmony check in the ingest path for the blocked chemicals. **verify** where ingest is decided. |
| Stats | Same values where a gene can express them. | `statOffsets` / `statFactors` on several genes so the gene list reads well in the xenotype editor (armour, temperature, toxic, psychic, work speeds, movement). Odyssey vacuum resistance under `MayRequire`. |
| Health scale 1.8, body size 1.1 | Body size stays human. Health scale via a gene. | No vanilla gene stat for health scale; Harmony postfix on `Pawn.HealthScale`. **verify** property name. |
| Pain | Pain shock threshold 0.5 and no pain thoughts. | Stat factor on pain shock threshold; vanilla `Pain_Reduced` style behaviour optional. |
| Looks: body | Skeleton body textures replace the human body. | A FurDef in the style of Biotech's furskin: the gene's `fur` points at our body textures, drawn over the body with skin colour. Head drawn by forced head types. **verify** FurDef fields for 1.6 and whether the body node can be hidden. |
| Looks: body types | Male, Thin and Hulk only. | Gene `bodyType`. Female pawns use the Male textures (a copy at the Female path) so nothing renders blank. |
| Looks: heads | The six skeleton heads. | `forcedHeadTypes` on a gene, HeadTypeDefs with gender Any. |
| Looks: colour, hair | Steel and rust colours, bald, no beard, no tattoos. | `skinColorOverride` or a few skin colour genes picked by the xenotype; `hairTagFilter` / `beardTagFilter` to none; tattoos blocked by a style patch. |
| Genderless, asexual | Pawns keep a gender (vanilla needs one) but are always Asexual and sterile. | Gene `forcedTraits: Asexual`, fertility stat factor 0, reproduction blocked. Gender is cosmetic only. |
| Pessimist / Depressive odds | 45% / 25%. | Harmony postfix on pawn generation for skeletons only (genes cannot force a trait at a chance). |
| Disallowed traits | Same list. | `suppressedTraits` where it makes sense plus a generation filter. |
| Age | Thousands of years old, ageless. | Vanilla Ageless behaviour, and a generation postfix that sets a chronological age between 480 and 2500. |
| Mechanical body | Keep the human BodyDef (a xenotype cannot change it). No bleeding, machine-bit "blood", mechanical wound overlays and hit sound, no organ harvesting. | Harmony: bleed rate to 0, blood filth def, fleshtype-style wound graphics. Organ harvest recipes blocked on skeletons. |
| Surgery | Vanilla prosthetic and bionic surgery works unchanged on the human body, so the original's re-targeted recipes are not needed. | None. Optional later: crafting skill instead of medicine for skeleton surgery. |
| Sever AI core | Kept as a recipe for skeletons only. | RecipeDef with `Recipe_ExecuteByCut`, `recipeUsers` Human, hidden unless the patient has the gene (custom worker `AvailableOnNow`). |
| Corpses | Never rot, inedible, shred / smash for steel, plasteel, uranium. | Harmony on rot and `IngestibleNow`; butcher products swapped when the corpse's pawn is a skeleton; shred and smash recipes use a `SpecialThingFilterWorker` "skeleton corpse". |
| Blocked apparel | Same list (vanilla and DLC defs under `MayRequire`). | Harmony on `EquipmentUtility.CanEquip`. |
| Blocked actions | Vomit, biosculpter, xenogerm, gene buildings, breath vapour. | Ports of the original's two patches, keyed on the gene instead of a mod extension. |
| Spawning in other factions | Same rarity: skeletons appear among outlanders, pirates, tribes and the Empire. | Patch each FactionDef's `xenotypeSet` to add `Rimshi_Skeleton` at a small chance (instead of injecting pawn kinds). Empire patch only if Royalty is active. |
| Starting pawn and wanderer chance | 0.1% / 0.9%. | Player faction `xenotypeSet`; wanderer joins inherit it. **verify** which faction wanderers draw from. |
| Skeleton backstories | Skeletons always get skeleton backstories. | Convert the 74 `AlienBackstoryDef`s to vanilla `BackstoryDef` (category `Rimshi_Skeleton`), and a generation postfix that rerolls backstories for skeleton pawns. |
| Skeleton names | Skeleton name maker. | XenotypeDef `nameMaker` / `nameMakerFemale`. **verify**. |
| Skeletal imperium faction | Kept, with its roster. | FactionDef with `xenotypeSet` 100% skeleton and pawn kinds that are ordinary humanlike kinds (no race field). |
| Player faction and scenario | Kept. | Player FactionDef with skeleton xenotype set, ScenarioDef with one skeleton starting pawn. |

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

## Assets

The textures (body, heads, world icon) and the backstory and name text come from the original mod. The original is by VexedTrees, continued by Zaljerem, and the Workshop page does not state a licence. **Before publishing, get permission from the authors or replace the art.** Using them for private testing is fine; the repo is set up so textures can be swapped without code changes.

## Open questions for Matthew

1. Should skeletons keep a vanilla gender (pronouns only), or should we try to force genderless (riskier: many vanilla paths assume male or female)? Recommendation: keep gender, cosmetic only.
2. Is "human body, mechanical behaviour" acceptable (human organs show in the health tab, but bleeding, organ harvest and wounds act mechanical), or do you want the parts relabelled too? Relabelling needs a Harmony patch on part labels and is cosmetic.
3. Should the mod declare itself incompatible with Rim-Skeletons (Continued)? They do not clash on defNames, but running both gives two kinds of skeleton.
4. Asset permission (above).

## Build order (proposed)

1. Xenotype with genes for needs, stats, immunity, looks (no C#), checked in game.
2. Diet patches and fuel thoughts.
3. Spawning: faction xenotype sets, player faction, scenario, backstories, names.
4. Mechanical body behaviour, corpses, shred / smash, sever AI core.
5. Trait odds, blocked apparel and actions, age.
6. Soft dependency patches (Royalty, Ideology, Odyssey, then DBH, AC, QE after decompiling them).

Each step adds `[Rimshi][T]` log lines and rows to the test checklist.
