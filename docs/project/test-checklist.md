# Test checklist

What has been seen working in game, read from the logs. Every test-worthy event writes a line to Player.log in a fixed shape:

    [Rimshi][T] area | key | detail

A failure writes the same line as a warning with `FAILED:` at the start of the detail. The game also logs a build line at startup (version, build number, who built it and when, which DLCs are active), so every result is tied to a build.

**How to use it:** play with Development mode on, then tell Claude to check the logs. Claude pulls Player.log and Player-prev.log from `C:\Users\Matthew\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios`, runs `tools/rimshi_testlog.py` over them and updates this table. Results add up across sessions; any exception that mentions the mod is reported too. A PR that only updates this file is merged by Claude without asking, and its description lists every test that ran and its outcome.

**Self-tests:** with Development mode on, loading or starting a game runs the in-game self-tests once per session (also on demand: Debug actions > Rimshi Skeletons > Run self-tests). They generate a skeleton and a baseliner off the map, check genes, looks, diet, thoughts and immunity, discard both pawns, and log one `selftest` line per check plus a summary.

**Unit tests:** `python3 -m unittest discover -s tests` runs the offline tests (def cross-references, textures, checklist coverage, the log reader, house style). CI runs them on every push.

**Adding tests:** when code logs a new test line, add a row to `CHECKLIST` in `tools/rimshi_testlog.py` and to the table below.

**Status:** passed, failed, passed after fail (it failed once and later worked), or not seen.

| Area | Item | Status | Build | Last line |
| --- | --- | --- | --- | --- |
| build | Build line at startup | not seen |  |  |
| defs | Blocked thoughts linked at startup (lists any not loaded) | not seen |  |  |
| defs | Vanilla hunger thought nullified by the fuel reactor | not seen |  |  |
| defs | Immunity list linked at startup (lists any not loaded) | not seen |  |  |
| defs | Chemfuel and uranium tagged as edible fuel | not seen |  |  |
| gene | Null chromosome makes a pawn genderless | not seen |  |  |
| gene | Skeleton frame gives a male, thin or hulk body and a skeleton head | not seen |  |  |
| gene | Skeletons generated without family | not seen |  |  |
| gene | Removing the null chromosome gives a gender back | not seen |  |  |
| render | Skeleton body texture drawn | not seen |  |  |
| diet | Skeleton ate chemfuel or uranium (fails if it ate anything else) | not seen |  |  |
| selftest | Self-tests: all passed (summary line) | not seen |  |  |
| selftest | Self-test: skeleton and baseliner generate | not seen |  |  |
| selftest | Self-test: no exceptions while testing | not seen |  |  |
| selftest | Self-test: skeleton has the Skeleton xenotype | not seen |  |  |
| selftest | Self-test: skeleton is genderless | not seen |  |  |
| selftest | Self-test: skeleton is Asexual | not seen |  |  |
| selftest | Self-test: skeleton body is male, thin or hulk | not seen |  |  |
| selftest | Self-test: skeleton has a skeleton head | not seen |  |  |
| selftest | Self-test: skeleton has a plating colour | not seen |  |  |
| selftest | Self-test: skeleton is bald | not seen |  |  |
| selftest | Self-test: skeleton body texture found | not seen |  |  |
| selftest | Self-test: skeleton head texture found | not seen |  |  |
| selftest | Self-test: skeleton has no rest need | not seen |  |  |
| selftest | Self-test: skeleton generated without family | not seen |  |  |
| selftest | Self-test: skeleton fertility is 0 | not seen |  |  |
| selftest | Self-test: skeleton toxic resistance 100% | not seen |  |  |
| selftest | Self-test: skeleton immune to flu | not seen |  |  |
| selftest | Self-test: skeleton can eat chemfuel and uranium | not seen |  |  |
| selftest | Self-test: skeleton refuses meals | not seen |  |  |
| selftest | Self-test: skeleton hunger well below human | not seen |  |  |
| selftest | Self-test: vanilla hunger thought nullified for skeleton | not seen |  |  |
| selftest | Self-test: fuel need thought can apply to skeleton | not seen |  |  |
| selftest | Self-test: naked thought nullified for skeleton | not seen |  |  |
| selftest | Self-test: human refuses chemfuel | not seen |  |  |
| selftest | Self-test: human still eats meals | not seen |  |  |
| selftest | Self-test: human keeps gender and hunger thought | not seen |  |  |
| selftest | Self-test: removing the null chromosome restores a gender | not seen |  |  |

## Manual checks

Things the log cannot show; tick them off by hand after looking in game.

- [ ] A skeleton spawned in dev mode (xenotype Skeleton) looks right: skeleton body and head, steel or rust colour, bald, no beard, no tattoos.
- [ ] A skeleton is genderless in its info card and has the Asexual trait.
- [ ] A hungry skeleton walks to chemfuel or uranium and eats it ("Fueling reactor with chemfuel").
- [ ] A skeleton refuses ordinary meals (right-click shows "not suitable"), and a human or animal refuses chemfuel.
- [ ] A hungry skeleton gets the "fuel need" thought; the vanilla hunger thought shows as nullified.
- [ ] Naked, dark, wet and pain thoughts show as nullified by mechanical nature.
- [ ] A skeleton never needs sleep (no rest bar).
