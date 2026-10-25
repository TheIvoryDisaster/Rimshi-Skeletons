# Changelog

Newest first. Every change gets a dated row.

| Date | Change |
| --- | --- |
| 2026-10-25 | Save converter for pawns: with the mod setting on (now "Convert Rim-Skeletons (Continued) factions and skeletons when a save loads"), every old skeleton becomes a human with the skeleton xenotype, keeping its injuries and prosthetics on the matching human parts, with our backstories, heads and pawn kinds; corpses too. A letter lists who was converted. Debug action "Convert old skeletons now". |
| 2026-10-24 | Spawning: skeletal imperium faction with the original roster, skeleton pawn kinds, player faction and "Embarking 'Skeleton'" scenario, 37 skeleton backstories and skeleton names, skeletons 1% of outlanders, pirates and the Empire (0.5% of tribes). Mod settings with "Replace Rim-Skeletons (Continued) factions when a save loads", debug action to add the imperium to an old world. Fixes: skeletons can eat fuel while the old mod is loaded, texture self-tests run on the main thread, stray relations on new genderless pawns removed and logged. |
| 2026-10-24 | RimWorld 1.7 (Waylith) build, same setup as Tithe: `build.ps1` builds `1.7/Assemblies` on a 1.7 install (.NET 10, UnityStubs, Waylith), LoadFolders and About list 1.7. CI still builds 1.6 only and leaves 1.7 out of the release zip. |
| 2026-10-24 | Tests: in-game self-tests (26 checks, run on game load in dev mode or from the debug menu) logging `selftest` lines to Player.log; offline unit tests in `tests/` run by CI. |
| 2026-10-24 | Core xenotype: skeleton genes, xenotype, heads and body art from the original, genderless null chromosome gene, chemfuel and uranium diet with a slow hunger rate and fuel need thought, blocked thoughts and immunities, test log lines. |
| 2026-10-24 | Design added: Kenshi lore theming, skeleton repair kits (repair is the top Smith job using Crafting, optional separate Skeleton repair work type after Doctor), skeleton repair bed costing an AI persona core. |
| 2026-10-24 | Design revised with Matthew's answers: genderless the Rim-Hivers way, anatomy options (rename vs android-style parts), save converter from Rim-Skeletons (Continued), original art used with credit (About.xml, README). Vanilla field claims checked against the 1.6 decompiled source. |
| 2026-10-11 | Repo set up: GitHub Actions build and release (Tithe's scheme), mod skeleton with build stamp and `[Rimshi][T]` test logging, test log reader, design draft for the xenotype port. |
