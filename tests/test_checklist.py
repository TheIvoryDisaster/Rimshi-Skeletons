"""Every test line the C# can write has a checklist row, and the checklist doc matches the reader."""
import os, re, unittest
from helpers import ROOT, source_text, testlog_module

LOG_CALL = re.compile(r'RimshiLog\.(?:Test|Fail)\(\s*"([^"]+)",\s*"([^"]+)"')
SELFTEST_CALL = re.compile(r'\bCheck\("([^"]+)"')


class ChecklistTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = testlog_module()
        cls.calls = set()
        for path, text in source_text().items():
            cls.calls |= {(a, k, path) for a, k in LOG_CALL.findall(text)}
            if path.endswith("SelfTests.cs"):
                cls.calls |= {("selftest", k, path) for k in SELFTEST_CALL.findall(text)}

    def test_found_log_calls(self):
        self.assertGreater(len(self.calls), 20)

    def test_every_log_line_has_a_row(self):
        for area, key, path in sorted(self.calls):
            with self.subTest(area=area, key=key, file=path):
                self.assertTrue(any(self.mod.matches(item, area, key, "") for item in self.mod.CHECKLIST),
                                "add a CHECKLIST row in tools/rimshi_testlog.py")

    def test_every_row_has_a_log_line(self):
        for area, key, label in self.mod.CHECKLIST:
            if key == "*" or key.startswith("*"):
                continue
            with self.subTest(area=area, key=key):
                self.assertIn((area, key), {(a, k) for a, k, _ in self.calls}, "row with no code writing it")

    def test_doc_table_matches_reader(self):
        doc = open(os.path.join(ROOT, "docs", "project", "test-checklist.md"), encoding="utf-8").read()
        table = re.findall(r"^\| (\S+) \| (.+?) \| [^|]+ \| [^|]* \| [^|]* \|$", doc, re.M)
        table = [(a, l) for a, l in table if a not in ("Area", "---")]
        self.assertEqual(table, [(a, l) for a, _, l in self.mod.CHECKLIST], "docs/project/test-checklist.md table out of step")

    def test_no_duplicate_rows(self):
        keys = [(a, k) for a, k, _ in self.mod.CHECKLIST]
        self.assertEqual(len(keys), len(set(keys)))


if __name__ == "__main__":
    unittest.main()
