#!/usr/bin/env python3
"""Changelog fragments (2026-10-25, same scheme as Tithe).

Branches never edit the table in docs/project/changelog.md. Each change adds its own file to docs/project/changelog/
instead, named <yyyy-mm-dd>-<short-name>.md, holding the row's text (each paragraph becomes one row with the file's
date). Two branches open at once add different files, so they can't conflict when both merge into beta. The CI build
folds the fragments into the table on main, newest first, and deletes them (the "Fold docs" job in build.yml).

usage: changelog_fold.py [--check] [--root DIR]
  --check   only validate the fragments (every build runs this); exits 1 on a bad one
"""
import argparse, os, re, sys

CHANGELOG = os.path.join("docs", "project", "changelog.md")
FRAGMENTS = os.path.join("docs", "project", "changelog")
NAME = re.compile(r"^(\d{4}-\d{2}-\d{2})-[a-z0-9][a-z0-9-]*\.md$")
ROW = re.compile(r"^\| (\d{4}-\d{2}-\d{2}) \|")


def fragments(root):
    """[(date, name, [row text, ...])] sorted oldest first, plus a list of problems."""
    out, problems = [], []
    folder = os.path.join(root, FRAGMENTS)
    if not os.path.isdir(folder):
        return out, problems
    for name in sorted(os.listdir(folder)):
        if name == "README.md":
            continue
        m = NAME.match(name)
        if not m:
            problems.append("%s: name it <yyyy-mm-dd>-<short-name>.md (lower case, digits and dashes)" % name)
            continue
        with open(os.path.join(folder, name), encoding="utf-8") as f:
            text = f.read()
        rows = [" ".join(p.split()) for p in re.split(r"\n\s*\n", text) if p.strip()]
        if not rows:
            problems.append("%s: empty" % name)
            continue
        out.append((m.group(1), name, [r.replace("|", "/") for r in rows]))
    out.sort()
    return out, problems


def fold(root):
    frags, problems = fragments(root)
    if problems:
        return problems
    if not frags:
        return []
    path = os.path.join(root, CHANGELOG)
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    top = next(i for i, line in enumerate(lines) if line.startswith("| ---")) + 1
    # Oldest fragment first, each placed above every row on or before its date, so the table stays newest first and
    # a fragment lands above rows of its own date that were already folded.
    for date, _, rows in frags:
        at = top
        while at < len(lines) and ROW.match(lines[at]) and ROW.match(lines[at]).group(1) > date:
            at += 1
        lines[at:at] = ["| %s | %s |" % (date, r) for r in rows]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    for _, name, _ in frags:
        os.remove(os.path.join(root, FRAGMENTS, name))
    print("folded %d fragment(s): %s" % (len(frags), ", ".join(n for _, n, _ in frags)))
    return []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--root", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    a = ap.parse_args()
    if a.check:
        frags, problems = fragments(a.root)
        print("%d changelog fragment(s) waiting to fold" % len(frags))
    else:
        problems = fold(a.root)
    for p in problems:
        print("changelog fragment " + p, file=sys.stderr)
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
