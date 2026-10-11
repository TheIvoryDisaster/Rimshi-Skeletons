# Rimshi Skeletons

A RimWorld 1.6 mod: the skeletons of Rim-Skeletons (Continued) rebuilt as a Biotech xenotype, with no dependency on Humanoid Alien Races.

Requires Harmony and Biotech.

## Layout

| Path | What |
| --- | --- |
| About/ | Mod metadata. The release build adds `<modVersion>`. |
| Defs/, Patches/, Languages/, Textures/ | Mod content (added as it is built). |
| LoadFolders.xml | Load folders; optional content for other mods goes in `Mods/<Name>` behind `IfModActive`. |
| Source/RimshiSkeletons/ | C# source. Builds to `1.6/Assemblies/RimshiSkeletons.dll` (git-ignored). |
| docs/ | Design and test checklist. Start with docs/README.md. |
| tools/rimshi_testlog.py | Reads `[Rimshi][T]` lines from Player.log and ticks off the test checklist. |

## Building

The repo is cloned straight into `RimWorld\Mods\Rimshi-Skeletons` on main. After pulling, run `build.ps1` (or build `RimshiSkeletons.slnx` in Visual Studio). The project finds the game DLLs and Harmony through relative paths from that location; override `RimWorldManaged` or `HarmonyDir` with `-p:` if yours differ.

## Versions and releases

GitHub Actions builds every push (see `.github/workflows/build.yml`):

- **main:** each push or merge takes the next major version, tags it `vN` and publishes a release with the packaged mod zip.
- **other branches:** `<last main version>-<branch>.<yyyyMMdd>.<HHmm>` (UTC), uploaded as a workflow artifact.

The version is compiled into the DLL and logged at startup as `[Rimshi][T] build | <version> | ...`. Local builds report version `dev` with a per-machine build number.
