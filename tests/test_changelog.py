"""Changelog fragments (tools/changelog_fold.py): names, folding order, and the repo's own fragments."""
import os, shutil, tempfile, unittest
from helpers import ROOT, tool_module

TABLE = """# Changelog

| Date | Change |
| --- | --- |
| 2026-10-25 | Already folded. |
| 2026-10-24 | Older. |
"""


class ChangelogFoldTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = tool_module("changelog_fold")

    def setUp(self):
        self.root = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.root, "docs", "project", "changelog"))
        with open(os.path.join(self.root, "docs", "project", "changelog.md"), "w") as f:
            f.write(TABLE)

    def tearDown(self):
        shutil.rmtree(self.root)

    def fragment(self, name, text):
        with open(os.path.join(self.root, "docs", "project", "changelog", name), "w") as f:
            f.write(text)

    def test_repo_fragments_are_valid(self):
        self.assertEqual(self.mod.fragments(ROOT)[1], [])

    def test_bad_name_is_a_problem(self):
        self.fragment("Fix thing.md", "text")
        self.assertEqual(len(self.mod.fragments(self.root)[1]), 1)

    def test_fold_keeps_newest_first_and_deletes_fragments(self):
        self.fragment("2026-10-26-new.md", "Newest.\n\nSecond row | with a pipe.")
        self.fragment("2026-10-25-same-day.md", "Same day.")
        self.assertEqual(self.mod.fold(self.root), [])
        lines = open(os.path.join(self.root, "docs", "project", "changelog.md")).read().splitlines()
        self.assertEqual(lines[4:9], [
            "| 2026-10-26 | Newest. |",
            "| 2026-10-26 | Second row / with a pipe. |",
            "| 2026-10-25 | Same day. |",
            "| 2026-10-25 | Already folded. |",
            "| 2026-10-24 | Older. |",
        ])
        self.assertEqual(os.listdir(os.path.join(self.root, "docs", "project", "changelog")), [])


if __name__ == "__main__":
    unittest.main()
