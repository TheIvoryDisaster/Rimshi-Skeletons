import importlib.util
from pathlib import Path
import tempfile
import unittest
spec = importlib.util.spec_from_file_location('uploader', Path(__file__).parents[1]/'scripts/steam_workshop_upload.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class UploadTests(unittest.TestCase):
    def test_only_login_name_and_fixed_arguments_no_password(self):
        with tempfile.TemporaryDirectory() as d:
            exe = Path(d)/'steamcmd.exe'; exe.touch()
            args = m.command(exe, 'my_login', Path(d)/'stage file.vdf')
            self.assertEqual(args[1:4], ['+login', 'my_login', '+workshop_build_item'])
            self.assertEqual(args[-1], '+quit')
    def test_argument_injection_rejected(self):
        with self.assertRaises(ValueError): m.command(Path('/missing/steamcmd.exe'), '+quit', Path('x'))
    def test_missing_installation_rejected(self):
        with self.assertRaises(ValueError): m.command(Path('steamcmd.exe'), 'login', Path('x'))
    def test_untrusted_environment_cannot_upload(self):
        from unittest.mock import patch
        with patch.dict(m.os.environ, {}, clear=True), self.assertRaises(ValueError): m.main()
