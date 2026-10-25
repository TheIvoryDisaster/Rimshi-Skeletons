# Changelog fragments

Every change gets a dated changelog row, written here rather than in the table in `docs/project/changelog.md` (2026-10-25, same as Tithe). Add one file per change:

    docs/project/changelog/<yyyy-mm-dd>-<short-name>.md

The file holds the row's text as plain prose, with no table pipes. A blank line starts a second row with the same date. Name the file after the change or the branch (lower case, digits and dashes), so two branches open at once never touch the same file and can't conflict when both merge into beta.

When the change reaches main, the CI build folds every fragment into the table (newest first) and deletes the files, in a commit of its own. Every build checks the names first (`python3 tools/changelog_fold.py --check`).

Never edit the changelog table on a branch.
