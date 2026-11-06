"""Offline release evidence must preserve public bytes without copying cache extras."""
import base64
import importlib.util
import hashlib
import json
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / 'collect-third-party-notices.py'


class ThirdPartyEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='lt-notice-test-')
        self.root = Path(self.temp.name)
        self.cache = self.root / 'cache'
        self.package = self.cache / 'fixture.package' / '1.2.3'
        self.package.mkdir(parents=True)
        self.release = self.root / 'release'
        self.output = self.root / 'evidence'
        self.archive = self.package / 'fixture.package.1.2.3.nupkg'
        self.make_package()

    def tearDown(self):
        self.temp.cleanup()

    def make_package(self, declared='LICENSE', extra=None, content_hash=None):
        with zipfile.ZipFile(self.archive, 'w') as archive:
            archive.writestr('Fixture.Package.nuspec', '<package><metadata><id>Fixture.Package</id><version>1.2.3</version>'
                             '<authors>Generated fixture</authors><license type="file">' + declared + '</license></metadata></package>')
            archive.writestr('LICENSE', b'Generated license\nExact bytes.\n')
            archive.writestr('runtimes/osx-arm64/native/THIRD-PARTY-NOTICES.txt', b'Generated nested notice\n')
            archive.writestr('lib/net10.0/Fixture.dll', b'not an executable test fixture')
            if extra:
                for name, raw in extra:
                    archive.writestr(name, raw)
        raw = self.archive.read_bytes()
        raw_hash = base64.b64encode(hashlib.sha512(raw).digest()).decode()
        content_hash = content_hash or raw_hash
        (self.package / (self.archive.name + '.sha512')).write_text(raw_hash)
        (self.package / '.nupkg.metadata').write_text(json.dumps({'contentHash': content_hash}))
        for component, name in [('cli', 'Cli'), ('api', 'Api'), ('worker', 'Worker')]:
            folder = self.release / component
            folder.mkdir(parents=True, exist_ok=True)
            (folder / ('LeviathanText.' + name + '.deps.json')).write_text(json.dumps({
                'libraries': {'Fixture.Package/1.2.3': {'type': 'package', 'path': 'fixture.package/1.2.3', 'sha512': 'sha512-' + content_hash}},
                'targets': {'generated': {'Fixture.Package/1.2.3': {'native': {'runtimes/osx-arm64/native/libfixture.dylib': {}}}}}}))

    def run_collector(self):
        return subprocess.run([sys.executable, str(SCRIPT), str(self.release), str(self.output), '--package-cache', str(self.cache)],
                              text=True, capture_output=True, timeout=15)

    def assert_refused_without_output(self):
        result = self.run_collector()
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertFalse(self.output.exists())
        return result

    def test_exact_published_notices_and_inventory_are_preserved_cache_extras_are_ignored(self):
        (self.package / 'LICENSE-private-key.txt').write_text('MUST NOT BE COPIED')
        result = self.run_collector()
        self.assertEqual(0, result.returncode, result.stderr)
        inventory = json.loads((self.output / 'runtime-packages.json').read_text())
        package, = inventory['packages']
        self.assertEqual(['api', 'cli', 'worker'], package['components'])
        self.assertEqual(2, inventory['noticeFiles'])
        self.assertFalse(inventory['attestation'])
        self.assertFalse(inventory['nativeSbomComplete'])
        self.assertFalse(inventory['packageSignaturesVerified'])
        self.assertIn('native-bundled-component-notice-review', package['reviewRequired'])
        self.assertEqual(b'Generated license\nExact bytes.\n', (self.output / 'fixture.package/1.2.3/LICENSE').read_bytes())
        self.assertFalse(any('private-key' in str(path) for path in self.output.rglob('*')))
        for notice in package['notices']:
            self.assertEqual(notice['sha256'], hashlib.sha256((self.output / notice['path']).read_bytes()).hexdigest())

    def test_existing_output_and_sentinel_are_never_replaced(self):
        self.output.mkdir()
        (self.output / 'sentinel').write_text('retained')
        self.assertNotEqual(0, self.run_collector().returncode)
        self.assertEqual('retained', (self.output / 'sentinel').read_text())

    def test_declared_license_traversal_is_rejected_before_writes(self):
        self.make_package(declared='../private.txt')
        self.assert_refused_without_output()

    def test_noncanonical_notice_alias_cannot_overwrite_another_notice(self):
        self.make_package(extra=[('licenses/./LICENSE', b'alias')])
        self.assert_refused_without_output()

    def test_published_symbolic_notice_is_rejected(self):
        symlink = zipfile.ZipInfo('NOTICE')
        symlink.create_system = 3
        symlink.external_attr = (stat.S_IFLNK | 0o777) << 16
        self.make_package(extra=[(symlink, b'../outside')])
        self.assert_refused_without_output()

    def test_missing_declared_notice_does_not_silently_pass(self):
        self.make_package(declared='MISSING.txt')
        self.assert_refused_without_output()

    def test_archive_corruption_is_rejected_before_copy(self):
        with self.archive.open('ab') as stream:
            stream.write(b'unregistered change')
        self.assertIn('raw SHA512', self.assert_refused_without_output().stderr)

    def test_distinct_signed_package_content_hash_and_raw_hash_are_not_confused(self):
        self.make_package(content_hash='restored-signature-independent-fixture-hash')
        self.assertEqual(0, self.run_collector().returncode)

    def test_content_hash_mismatch_with_runtime_manifest_is_rejected(self):
        (self.package / '.nupkg.metadata').write_text(json.dumps({'contentHash': 'different'}))
        self.assertIn('content hash', self.assert_refused_without_output().stderr)

    def test_archive_symlink_cannot_read_outside_package_cache(self):
        moved = self.root / 'outside.nupkg'
        self.archive.rename(moved)
        self.archive.symlink_to(moved)
        self.assertIn('escapes', self.assert_refused_without_output().stderr)


    def test_case_aliases_are_rejected_before_output(self):
        self.make_package(extra=[('license', b'alias')])
        self.assert_refused_without_output()

    def test_unicode_aliases_are_rejected_before_output(self):
        self.make_package(extra=[('NOTICE-\u00e9.txt', b'first'), ('NOTICE-e\u0301.txt', b'second')])
        self.assert_refused_without_output()

    def test_notice_file_cannot_alias_another_notices_parent_directory(self):
        for prefix in ('LICENSES', 'licenses'):
            with self.subTest(prefix=prefix):
                self.make_package(extra=[(prefix, b'file'), ('licenses/NOTICE.txt', b'nested')])
                self.assert_refused_without_output()

    def test_total_payload_budget_is_checked_before_inflating_notice_data(self):
        spec = importlib.util.spec_from_file_location('notice_collector', SCRIPT)
        collector = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(collector)
        observed = []
        original = collector.zipfile.ZipFile.read
        def read(archive, name, *args, **kwargs):
            value = original(archive, name, *args, **kwargs)
            filename = name.filename if isinstance(name, collector.zipfile.ZipInfo) else name
            if collector.is_notice(Path(filename)):
                observed.append(len(value))
            return value
        with patch.object(collector, 'MAX_TOTAL_NOTICE_BYTES', 32), patch.object(collector.zipfile.ZipFile, 'read', read):
            with self.assertRaises(ValueError):
                collector.collect_plan(self.release, self.cache)
        self.assertEqual([], observed)


if __name__ == '__main__':
    unittest.main()
