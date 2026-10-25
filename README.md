# Rimshi Skeletons

A RimWorld 1.6 and 1.7 mod: the skeletons of Rim-Skeletons (Continued) rebuilt as a Biotech xenotype, with no dependency on Humanoid Alien Races.

Requires Harmony and Biotech.

## Layout

| Path | What |
| --- | --- |
| About/ | Mod metadata. The release build adds `<modVersion>`. |
| Defs/, Patches/, Languages/, Textures/ | Mod content (added as it is built). |
| LoadFolders.xml | Load folders; optional content for other mods goes in `Mods/<Name>` behind `IfModActive`. |
| Source/RimshiSkeletons/ | C# source. Builds to `1.6/Assemblies/RimshiSkeletons.dll` or `1.7/Assemblies/RimshiSkeletons.dll` (git-ignored). |
| docs/ | Design and test checklist. Start with docs/README.md. |
| tools/rimshi_testlog.py | Reads `[Rimshi][T]` lines from Player.log and ticks off the test checklist. |
| tests/ | Offline unit tests (def cross-references, textures, checklist coverage, log reader, style); run `python3 -m unittest discover -s tests`. CI runs them. |

## Building

The repo is cloned straight into `RimWorld\Mods\Rimshi-Skeletons` on main (or beta while testing). After pulling, run `build.ps1` (or build `RimshiSkeletons.slnx` in Visual Studio). The project finds the game DLLs and Harmony through relative paths from that location; override `RimWorldManaged` or `HarmonyDir` with `-p:` if yours differ.

Two game versions build from the same source (same setup as Tithe). `build.ps1` builds whichever one the install is on: 1.6 (.NET Framework 4.7.2 + Unity, from `RimWorldWin64_Data\Managed`) or 1.7 (.NET 10 on the Waylith engine, from the game folder, with `UnityStubs.dll` and `Waylith.dll`). `.\build.ps1 1.7` builds just one; in Visual Studio pass `-p:RimWorldVersion=1.7`. The 1.7 build needs the .NET 10 SDK and the Harmony mod's `1.7` folder. GitHub Actions has no 1.7 reference package, so CI compiles and packages 1.6 only; the 1.7 DLL is built locally.

## Versions and releases

GitHub Actions builds every push (see `.github/workflows/build.yml`):

- **main:** each push or merge takes the next major version, tags it `vN` and publishes a release with the packaged mod zip. After the build, a "Fold docs" job folds changelog fragments into `docs/project/changelog.md` and brings `beta` up to date with main.
- **other branches (beta included):** `<last main version>-<branch>.<yyyyMMdd>.<HHmm>` (UTC), uploaded as a workflow artifact. A beta build reads like `9-beta.20261025.1930`.

## Branches and pull requests

Same flow as Tithe:

1. Each piece of work goes on its own `claude/...` branch with a pull request into **beta**.
2. Claude merges that pull request into beta once CI is green, and adds it to the beta pull request's list.
3. One standing pull request, **beta into main**, lists every change on beta with how to try it in game. Matthew tests on beta and merges it himself; that merge is the next `vN` release. Claude opens a fresh one when the next change lands on beta.
4. Test checklist pull requests from log checks only touch the checklist, so they still go straight to main, and Claude merges them.

To test beta in game, switch the mod folder's clone to it (`git switch beta`, then `git pull` and `build.ps1`), and `git switch main` to go back.

The version is compiled into the DLL and logged at startup as `[Rimshi][T] build | <version> | ...`. Local builds report version `dev` with a per-machine build number.

## Credits

Skeletons, their art, backstories and names come from **Rim-Skeletons** by VexedTrees ([Workshop 2577145706](https://steamcommunity.com/sharedfiles/filedetails/?id=2577145706)), continued for 1.6 by Zaljerem as **Rim-Skeletons (Continued)** ([Workshop 3674548651](https://steamcommunity.com/sharedfiles/filedetails/?id=3674548651)). The genderless gene follows the approach of VexedTrees' **Rim-Hivers! - Biotech**.
