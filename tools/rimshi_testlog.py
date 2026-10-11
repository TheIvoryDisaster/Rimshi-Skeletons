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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("logs", nargs="+")
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument("--state", default=os.path.join(here, "test_state.json"))
    ap.add_argument("--markdown", action="store_true")
    a = ap.parse_args()

    state = json.load(open(a.state)) if os.path.exists(a.state) else {}
    lines, errors = read(a.logs)
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
