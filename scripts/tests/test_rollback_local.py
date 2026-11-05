"""Exercise rollback selection and preflight with disposable installations."""
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "rollback-local.sh"


class RollbackLocalTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="lt-rollback-test-")
        self.root = Path(self.temporary.name)
        self.prefix = self.root / "app"
        self.prefix.mkdir()
        (self.prefix / "identity").write_text("current")

    def tearDown(self):
        self.temporary.cleanup()

    def backup(self, suffix, identity, exit_code=0):
        path = self.root / ("app." + suffix)
        (path / "cli").mkdir(parents=True)
        (path / "identity").write_text(identity)
        binary = path / "cli/LeviathanText.Cli"
        binary.write_text(f"#!/bin/sh\nexit {exit_code}\n")
        binary.chmod(0o755)
        return path

    def test_newest_timestamp_wins_across_backup_kinds(self):
        old = self.backup("pre-update-20261104T120000Z", "old")
        newest = self.backup("backup-20261105T120000Z", "newest")

        result = subprocess.run(["bash", str(SCRIPT), str(self.prefix)], capture_output=True, text=True)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("newest", (self.prefix / "identity").read_text())
        self.assertTrue(old.exists())
        self.assertFalse(newest.exists())
        failed = list(self.root.glob("app.failed-*"))
        self.assertEqual(1, len(failed))
        self.assertEqual("current", (failed[0] / "identity").read_text())

    def test_failed_candidate_check_preserves_current_installation(self):
        backup = self.backup("pre-update-20261105T120000Z", "bad", exit_code=1)

        result = subprocess.run(["bash", str(SCRIPT), str(self.prefix)], capture_output=True, text=True)

        self.assertNotEqual(0, result.returncode)
        self.assertEqual("current", (self.prefix / "identity").read_text())
        self.assertTrue(backup.exists())
        self.assertEqual([], list(self.root.glob("app.failed-*")))


if __name__ == "__main__":
    unittest.main()
