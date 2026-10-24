"""House style from docs/README.md: no em or en dashes in anything we write."""
import glob, os, unittest
from helpers import ROOT

CHECKED = ("About", "Defs", "Patches", "Languages", "Source", "docs", "tools", "tests", ".github")


class StyleTests(unittest.TestCase):
    def test_no_em_or_en_dashes(self):
        files = [os.path.join(ROOT, "README.md")]
        for folder in CHECKED:
            files += [p for p in glob.glob(os.path.join(ROOT, folder, "**", "*"), recursive=True)
                      if os.path.isfile(p) and p.endswith((".xml", ".cs", ".md", ".py", ".yml", ".csproj"))
                      and os.sep + "obj" + os.sep not in p and os.sep + "bin" + os.sep not in p]
        for path in files:
            with open(path, encoding="utf-8") as f:
                text = f.read()
            with self.subTest(file=os.path.relpath(path, ROOT)):
                self.assertNotIn(chr(0x2014), text, "em dash")
                self.assertNotIn(chr(0x2013), text, "en dash")


if __name__ == "__main__":
    unittest.main()
