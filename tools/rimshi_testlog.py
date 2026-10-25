#!/usr/bin/env python3
"""Rimshi Skeletons test log reader (adapted from Tithe's tools/tithe_testlog.py).

Reads RimWorld Player.log files for the mod's test lines
    [Rimshi][T] area | key | detail
(written by RimshiLog.Test / RimshiLog.Fail) and checks them off against CHECKLIST below. Results accumulate in a
state file (git-ignored), so several play sessions add up. --markdown prints the table for
docs/project/test-checklist.md.

usage: rimshi_testlog.py [--state FILE] [--markdown] LOG [LOG...]

Add a CHECKLIST row whenever new code logs a test line. key matching: an exact key, "*" for any key in the area, or
"*text*" to match text anywhere in the detail.
"""
import argparse, json, os, re, sys

LINE = re.compile(r"\[Rimshi\]\[T\] (?P<area>[^|]+?) \| (?P<key>[^|]+?) \| ?(?P<detail>.*)$")
BUILD = re.compile(r"build (\d+)")

# (area, key, label). Keep in the same order as docs/project/test-checklist.md.
CHECKLIST = [
    ("build", "*", "Build line at startup"),
    ("defs", "blocked thoughts", "Blocked thoughts linked at startup (lists any not loaded)"),
    ("defs", "hunger thought", "Vanilla hunger thought nullified by the fuel reactor"),
    ("defs", "immunities", "Immunity list linked at startup (lists any not loaded)"),
    ("defs", "fuel", "Chemfuel and uranium tagged as edible fuel"),
    ("defs", "backstories", "Skeleton backstories loaded"),
    ("defs", "spawning", "Outlander, pirate, tribe and Empire xenotype sets include skeletons"),
    ("patch", "missing", "Every patched private method found (this line only appears when one is missing)"),
    ("gene", "genderless", "Null chromosome makes a pawn genderless"),
    ("gene", "frame", "Skeleton frame gives a male, thin or hulk body and a skeleton head"),
    ("gene", "no relations", "Skeletons generated without family"),
    ("gene", "gender restored", "Removing the null chromosome gives a gender back"),
    ("gene", "relations cleared", "Relations a new genderless pawn got another way were removed (names them)"),
    ("faction", "replace", "Mod setting on: old Rim-Skeletons faction replaced by ours on load"),
    ("faction", "replace none", "Mod setting on: save had no old Rim-Skeletons factions"),
    ("faction", "added", "Debug action added the skeletal imperium to the world"),
    ("faction", "in world", "Skeletal imperium present in the loaded world (or says none)"),
    ("migrate", "pawn", "Mod setting on: an old Rim-Skeletons skeleton converted to ours (lists what changed)"),
    ("migrate", "lost", "Health condition with no matching human body part dropped during conversion (names it)"),
    ("migrate", "summary", "Mod setting on: conversion summary (skeletons converted, conditions dropped)"),
    ("migrate", "none", "Mod setting on: save had no old Rim-Skeletons skeletons"),
    ("migrate", "old mod missing", "Mod setting on: Rim-Skeletons (Continued) not loaded, nothing to convert"),
    ("render", "body", "Skeleton body texture drawn"),
    ("kit", "filter", "Medicine skipped for a skeleton, or a repair kit skipped for a non-skeleton"),
    ("bed", "refused", "Non-skeleton kept out of a skeleton repair bed"),
    ("bed", "repair", "Repair bed repairing a skeleton's injury"),
    ("bed", "done", "Skeleton fully repaired in a repair bed"),
    ("diet", "ate fuel", "Skeleton ate chemfuel or uranium (fails if it ate anything else)"),
    ("reproduction", "pregnancy blocked", "A pregnancy added to a skeleton was refused"),
    ("horde", "array graphic", "Recall array inherited the mechband antenna graphic"),
    ("horde", "research", "Recall research on the research tab, overlapping nothing (gives its position)"),
    ("horde", "melee", "Bladeguard melee weapon tags (adds medieval weapons without Royalty)"),
    ("horde", "array used", "Colonist broadcast the recall codes; array burned out"),
    ("horde", "arrived", "Skeleton horde dropped in (counts, kinds, weapons, shield belts, failsafe cores)"),
    ("horde", "cancelled", "Horde called to a map that is gone was dropped"),
    ("horde", "berserk", "Horde skeleton went berserk"),
    ("horde", "failsafe", "Failsafe core set the death on downed chance"),
    ("horde", "failsafe died", "A failsafe skeleton died anyway (says how; not a failure)"),
    ("reprogram", "captured", "Captured horde skeleton set to interrogate and reprogram"),
    ("reprogram", "session", "Warden reprogramming session lowered the corruption"),
    ("reprogram", "joined", "Reprogrammed skeleton joined the colony"),
    ("geneproof", "implant refused", "Xenogerm implant on a skeleton refused"),
    ("geneproof", "reimplant refused", "Xenogerm reimplant ability on a skeleton refused"),
    ("geneproof", "absorb refused", "Skeleton refused the absorb xenogerm job"),
    ("geneproof", "implant blocked", "No xenogerm reached a skeleton past the checks (this line only appears when one did)"),
    ("geneproof", "reimplant blocked", "No reimplant involving a skeleton got past the checks (this line only appears when one did)"),
    ("geneproof", "genepack", "Skeleton genes kept out of a genepack (fails if a pack held only skeleton genes)"),
    ("selftest", "summary", "Self-tests: all passed (summary line)"),
    ("selftest", "generate", "Self-test: skeleton and baseliner generate"),
    ("selftest", "no exceptions", "Self-test: no exceptions while testing"),
    ("selftest", "xenotype", "Self-test: skeleton has the Skeleton xenotype"),
    ("selftest", "genderless", "Self-test: skeleton is genderless"),
    ("selftest", "asexual", "Self-test: skeleton is Asexual"),
    ("selftest", "body type", "Self-test: skeleton body is male, thin or hulk"),
    ("selftest", "head", "Self-test: skeleton has a skeleton head"),
    ("selftest", "plating", "Self-test: skeleton has a plating colour"),
    ("selftest", "bald", "Self-test: skeleton is bald"),
    ("selftest", "body texture", "Self-test: skeleton body texture found"),
    ("selftest", "head texture", "Self-test: skeleton head texture found"),
    ("selftest", "no rest need", "Self-test: skeleton has no rest need"),
    ("selftest", "no relations", "Self-test: skeleton generated without family"),
    ("selftest", "fertility", "Self-test: skeleton fertility is 0"),
    ("selftest", "toxic resistance", "Self-test: skeleton toxic resistance 100%"),
    ("selftest", "immunity", "Self-test: skeleton immune to flu"),
    ("selftest", "eats fuel", "Self-test: skeleton can eat chemfuel and uranium"),
    ("selftest", "refuses meals", "Self-test: skeleton refuses meals"),
    ("selftest", "hunger rate", "Self-test: skeleton hunger well below human"),
    ("selftest", "hunger thought", "Self-test: vanilla hunger thought nullified for skeleton"),
    ("selftest", "fuel thought", "Self-test: fuel need thought can apply to skeleton"),
    ("selftest", "blocked thought", "Self-test: naked thought nullified for skeleton"),
    ("selftest", "human refuses fuel", "Self-test: human refuses chemfuel"),
    ("selftest", "human eats meals", "Self-test: human still eats meals"),
    ("selftest", "human unchanged", "Self-test: human keeps gender and hunger thought"),
    ("selftest", "repair research", "Self-test: both repair research projects need Basic mechtech"),
    ("selftest", "kit gated", "Self-test: repair kit recipe needs the repair kit research"),
    ("selftest", "bed gated", "Self-test: repair bed needs the repair bed research and an AI persona core"),
    ("selftest", "kit medicine", "Self-test: repair kit is medicine as strong as industrial medicine"),
    ("selftest", "kit filter", "Self-test: repair kits only for skeletons, medicine only for non-skeletons"),
    ("selftest", "bed users", "Self-test: only skeletons can use a repair bed; skeletons can still use hospital beds"),
    ("selftest", "gene removal", "Self-test: removing the null chromosome restores a gender"),
    ("selftest", "gene set flag", "Self-test: no skeleton gene can roll in gene sets"),
    ("selftest", "gene sets clean", "Self-test: 200 random gene sets hold no skeleton gene"),
    ("selftest", "genepack strip", "Self-test: a genepack drops skeleton genes and keeps the rest"),
    ("selftest", "gene lock", "Self-test: gene buildings refuse the skeleton, not the baseliner"),
    ("selftest", "implant bill", "Self-test: implant xenogerm bill unavailable on a skeleton"),
    ("selftest", "gene lock patched", "Self-test: every gene lock patch applied"),
    ("selftest", "backstory", "Self-test: skeleton has a skeleton adulthood backstory"),
    ("selftest", "faction def", "Self-test: skeletal imperium and skeleton player faction loaded"),
    ("selftest", "faction kind", "Self-test: a skeletal imperium pawn kind generates a skeleton"),
    ("selftest", "cannot reproduce", "Self-test: skeleton cannot have children or be implanted"),
    ("selftest", "pregnancy refused", "Self-test: pregnancy cannot be added to a skeleton"),
    ("selftest", "recall array", "Self-test: recall array and its research loaded (2000, after basic mechtech, no mechanitor needed)"),
    ("selftest", "horde factionless", "Self-test: horde skeletons have no faction"),
    ("selftest", "horde protocol", "Self-test: horde skeletons have the corrupted protocol and cannot be recruited"),
    ("selftest", "horde gear", "Self-test: horde gunners carry spacer guns, bladeguards melee weapons and shield belts"),
    ("selftest", "horde peace", "Self-test: horde skeletons are not hostile to each other"),
    ("selftest", "failsafe", "Self-test: failsafe core sets the death on downed chance"),
    ("selftest", "reprogram session", "Self-test: a reprogramming session lowers the corruption"),
    ("selftest", "convert parts", "Self-test: every old skeleton body part maps to its own human part (old mod loaded)"),
    ("selftest", "convert race", "Self-test: converted old skeleton is race Human with the human body"),
    ("selftest", "convert xenotype", "Self-test: converted old skeleton has the Skeleton xenotype and no gender"),
    ("selftest", "convert story", "Self-test: converted old skeleton has our head and backstories"),
    ("selftest", "convert kind", "Self-test: converted old skeleton has our pawn kind"),
    ("selftest", "convert health", "Self-test: converted old skeleton has no old hediff and only human body parts"),
    ("selftest", "convert again", "Self-test: a converted skeleton is not converted again"),
]


def matches(item, area, key, detail):
    a, k, _ = item
    if a != area:
        return False
    if k == "*":
        return True
    if k.startswith("*") and k.endswith("*") and len(k) > 1:
        return k.strip("*") in detail
    return k == key


def read(logs):
    lines, errors, build = [], [], None
    for path in logs:
        with open(path, encoding="utf-8", errors="replace") as f:
            text = f.read().splitlines()
        for i, raw in enumerate(text):
            m = LINE.search(raw)
            if m:
                area, key, detail = m.group("area").strip(), m.group("key").strip(), m.group("detail").strip()
                if area == "build":
                    b = BUILD.search(detail)
                    build = int(b.group(1)) if b else build
                lines.append((area, key, detail, build, os.path.basename(path)))
            elif ("Exception" in raw or "Error" in raw) and any("RimshiSkeletons" in t or "Rimshi_" in t for t in text[i:i + 12]):
                trace = " / ".join(t.strip() for t in text[i:i + 6] if "Rimshi" in t or t is raw)
                errors.append((os.path.basename(path), i + 1, trace[:300]))
    return lines, errors


def update_state(state, lines):
    """Adds the test lines to the per-row state (passes, fails, status, last detail, build)."""
    for item in CHECKLIST:
        sid = item[0] + "|" + item[1]
        for area, key, detail, build, src in lines:
            if not matches(item, area, key, detail):
                continue
            failed = detail.startswith("FAILED")
            st = state.setdefault(sid, {"passes": 0, "fails": 0})
            st["fails" if failed else "passes"] += 1
            st["last"] = detail[:160]
            st["build"] = build
            if failed:
                st["status"] = "failed"
            else:
                st["status"] = "passed" if st["fails"] == 0 else "passed after fail"
    return state


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("logs", nargs="+")
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument("--state", default=os.path.join(here, "test_state.json"))
    ap.add_argument("--markdown", action="store_true")
    a = ap.parse_args()

    state = json.load(open(a.state)) if os.path.exists(a.state) else {}
    lines, errors = read(a.logs)
    update_state(state, lines)
    json.dump(state, open(a.state, "w"), indent=1)

    rows = []
    for item in CHECKLIST:
        st = state.get(item[0] + "|" + item[1], {})
        rows.append((item[0], item[2], st.get("status", "not seen"), st.get("build"), st.get("last", "")))
    if a.markdown:
        print("| Area | Item | Status | Build | Last line |\n| --- | --- | --- | --- | --- |")
        for r in rows:
            print("| %s | %s | %s | %s | %s |" % (r[0], r[1], r[2], r[3] if r[3] is not None else "", r[4].replace("|", "/")))
    else:
        seen = sum(1 for r in rows if r[2] != "not seen")
        print("%d/%d checklist items seen; %d test lines read; %d Rimshi errors" % (seen, len(rows), len(lines), len(errors)))
        for r in rows:
            print("  [%s] %-10s %s%s" % ("x" if r[2].startswith("passed") else ("!" if r[2] == "failed" else " "), r[0], r[1],
                                         ("  (build %s)" % r[3]) if r[3] is not None else ""))
        unknown = sorted({(l[0], l[1]) for l in lines if not any(matches(i, l[0], l[1], l[2]) for i in CHECKLIST)})
        for area, key in unknown:
            print("  (no checklist row for %s | %s)" % (area, key))
    for e in errors:
        print("ERROR %s:%d %s" % e, file=sys.stderr)


if __name__ == "__main__":
    main()
