"""The Player.log reader: parsing, matching, pass and fail bookkeeping, error spotting."""
import os, tempfile, unittest
from helpers import testlog_module

LOG = """Some unrelated line
[Rimshi][T] build | v7 | version v7, build 12 (ci), dll written now
[Rimshi][T] gene | genderless | Bob: Male to None, body Male
[Rimshi][T] selftest | eats components | FAILED: component suitable False
[Rimshi][T] selftest | eats components | component suitable True
NullReferenceException: Object reference not set
  at RimshiSkeletons.Patch_BodyGraphic.Postfix
"""


class TestLogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = testlog_module()
        fd, cls.path = tempfile.mkstemp(suffix=".log")
        with os.fdopen(fd, "w") as f:
            f.write(LOG)

    @classmethod
    def tearDownClass(cls):
        os.remove(cls.path)

    def test_reads_lines_with_build(self):
        lines, _ = self.mod.read([self.path])
        self.assertEqual(len(lines), 4)
        self.assertEqual(lines[1][:4], ("gene", "genderless", "Bob: Male to None, body Male", 12))

    def test_spots_mod_exceptions(self):
        _, errors = self.mod.read([self.path])
        self.assertEqual(len(errors), 1)

    def test_matching(self):
        m = self.mod.matches
        self.assertTrue(m(("build", "*", ""), "build", "anything", ""))
        self.assertTrue(m(("gene", "frame", ""), "gene", "frame", ""))
        self.assertFalse(m(("gene", "frame", ""), "gene", "genderless", ""))
        self.assertTrue(m(("diet", "*ComponentIndustrial*", ""), "diet", "ate component", "Bob consumed 1 ComponentIndustrial"))

    def test_pass_after_fail(self):
        lines, _ = self.mod.read([self.path])
        state = self.mod.update_state({}, lines)
        st = state["selftest|eats components"]
        self.assertEqual((st["passes"], st["fails"], st["status"]), (1, 1, "passed after fail"))
        self.assertEqual(state["gene|genderless"]["status"], "passed")

    def test_failure_last(self):
        lines, _ = self.mod.read([self.path])
        state = self.mod.update_state({}, lines[:3])
        self.assertEqual(state["selftest|eats components"]["status"], "failed")

    def test_state_accumulates(self):
        lines, _ = self.mod.read([self.path])
        state = self.mod.update_state({}, lines)
        state = self.mod.update_state(state, lines)
        self.assertEqual(state["gene|genderless"]["passes"], 2)


if __name__ == "__main__":
    unittest.main()
