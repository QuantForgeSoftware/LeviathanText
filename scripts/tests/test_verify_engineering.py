"""The non-fitting entry point must retain its audited exclusions and durable receipts."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'verify-engineering.sh'
EXCLUDED = {
    'TrainingDiagnosticsCommandTests', 'TrainingAlphabetTests',
    'LearnedLayoutIntegrationTests', 'LayoutModelApiTests',
    'PatchLayoutModelTests', 'LearnedLineDetectorTests',
}


class EngineeringVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='lt-engineering-test-')
        self.root = Path(self.temp.name)
        self.trace = self.root / 'calls.jsonl'
        fake_bin = self.root / 'bin'
        fake_bin.mkdir()
        fake = fake_bin / 'dotnet'
        fake.write_text(f'#!{sys.executable}\n' + '''import json, os, sys
with open(os.environ['LT_ENGINEERING_TRACE'], 'a') as stream:
    stream.write(json.dumps(sys.argv[1:]) + '\\n')
with open(os.environ['LT_ENGINEERING_TRACE'] + '.env', 'a') as stream:
    stream.write(json.dumps({key: os.environ.get(key) for key in
        ('LEVIATHANTEXT_RUN_FITTING', 'LEVIATHANTEXT_RUN_CORPUS', 'LEVIATHANTEXT_RUN_LOCKED', 'LEVIATHAN_OLLAMA_LIVE')}) + '\\n')
if os.environ.get('LT_ENGINEERING_FAIL') and sys.argv[1] == 'test':
    print('registered failing test')
    raise SystemExit(1)
if sys.argv[1] == 'test':
    from pathlib import Path
    args = sys.argv[1:]
    kind = os.environ.get('LT_ENGINEERING_RECEIPT', 'valid')
    if kind != 'missing':
        folder = Path(args[args.index('--results-directory') + 1])
        name = args[args.index('--logger') + 1].split('LogFileName=', 1)[1]
        executed = 0 if kind == 'empty' else 1
        failed = 1 if kind == 'failed' else 0
        (folder / name).write_text(f'<TestRun><ResultSummary><Counters total="1" executed="{executed}" failed="{failed}" /></ResultSummary></TestRun>')
''')
        fake.chmod(0o755)
        self.env = dict(os.environ, PATH=str(fake_bin) + os.pathsep + os.environ['PATH'],
                        LT_ENGINEERING_TRACE=str(self.trace))

    def tearDown(self):
        self.temp.cleanup()

    def run_script(self, *args):
        return subprocess.run(['bash', str(SCRIPT), *args], cwd=self.root,
                              env=self.env, text=True, capture_output=True, timeout=15)

    def calls(self):
        return [json.loads(line) for line in self.trace.read_text().splitlines()] if self.trace.exists() else []

    def test_default_build_and_all_selected_test_calls_retain_fitting_exclusions(self):
        receipts = self.root / 'fresh receipts'
        result = self.run_script('--results', str(receipts))
        self.assertEqual(0, result.returncode, result.stderr)
        calls = self.calls()
        self.assertEqual('build', calls[0][0])
        tests = calls[1:]
        self.assertEqual(15, len(tests))
        for call in tests[:13]:
            self.assertEqual('test', call[0])
            self.assertNotIn('LeviathanText.ML.Tests', call[1])
            self.assertNotIn('LeviathanText.Pilot.Tests', call[1])
            exclusions = call[call.index('--filter') + 1]
            self.assertEqual(EXCLUDED, {item.removeprefix('FullyQualifiedName!~') for item in exclusions.split('&')})
            self.assertIn('--no-build', call)
            self.assertEqual(str(receipts), call[call.index('--results-directory') + 1])
        self.assertEqual('tests/LeviathanText.TestPolicy.Tests', tests[12][1])
        self.assertEqual(15, len((receipts / 'summary.txt').read_text().splitlines()))
        self.assertTrue((receipts / 'filter.txt').is_file())
        expected_classes = ['LabelCodecTests', 'LabelCodecInvariantTests']
        for call, selected_class in zip(tests[13:], expected_classes):
            self.assertEqual('test', call[0])
            self.assertEqual('tests/LeviathanText.ML.Tests', call[1])
            self.assertEqual('FullyQualifiedName~LeviathanText.ML.Tests.' + selected_class + '.',
                             call[call.index('--filter') + 1])
            self.assertIn('--no-build', call)
        self.assertTrue((receipts / 'codec-filters.txt').is_file())

    def test_no_build_selection_reuses_existing_release_outputs(self):
        result = self.run_script('--no-build', '--results', str(self.root / 'receipts'))
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(15, len(self.calls()))
        self.assertTrue(all(call[0] == 'test' for call in self.calls()))

    def test_inherited_experiment_or_provider_flags_cannot_enable_capabilities_in_the_engineering_gate(self):
        for key in ('LEVIATHANTEXT_RUN_FITTING', 'LEVIATHANTEXT_RUN_CORPUS', 'LEVIATHANTEXT_RUN_LOCKED', 'LEVIATHAN_OLLAMA_LIVE'):
            self.env[key] = '1'
        result = self.run_script('--results', str(self.root / 'forced-safe'))
        self.assertEqual(0, result.returncode, result.stderr)
        environments = [json.loads(line) for line in Path(str(self.trace) + '.env').read_text().splitlines()]
        self.assertEqual(16, len(environments))
        self.assertTrue(all(set(item.values()) == {'0'} for item in environments))

    def test_invalid_flags_never_start_dotnet_or_create_requested_receipts(self):
        for args in [('--filter', 'anything'), ('--no-build', '--no-build'), ('--results',)]:
            with self.subTest(args=args):
                result = self.run_script(*args)
                self.assertEqual(2, result.returncode)
        self.assertEqual([], self.calls())

    def test_prior_receipts_are_never_overwritten(self):
        receipts = self.root / 'previous'
        receipts.mkdir()
        (receipts / 'summary.txt').write_text('retained evidence')
        result = self.run_script('--results', str(receipts))
        self.assertNotEqual(0, result.returncode)
        self.assertEqual('retained evidence', (receipts / 'summary.txt').read_text())
        self.assertEqual([], self.calls())

    def test_failed_suite_stops_with_failure_receipt(self):
        self.env['LT_ENGINEERING_FAIL'] = '1'
        receipts = self.root / 'failure'
        result = self.run_script('--no-build', '--results', str(receipts))
        self.assertEqual(1, result.returncode)
        self.assertEqual(1, len(self.calls()))
        self.assertIn('Contracts failed', (receipts / 'summary.txt').read_text())
        self.assertIn('registered failing test', (receipts / 'Contracts.log').read_text())


    def test_zero_exit_with_missing_empty_or_failing_receipt_is_not_success(self):
        for kind in ('missing', 'empty', 'failed'):
            with self.subTest(kind=kind):
                self.env['LT_ENGINEERING_RECEIPT'] = kind
                receipts = self.root / ('receipt-' + kind)
                result = self.run_script('--no-build', '--results', str(receipts))
                self.assertEqual(1, result.returncode)
                self.assertIn('invalid receipt', result.stderr)
                self.assertNotIn('passed', (receipts / 'summary.txt').read_text())


if __name__ == '__main__':
    unittest.main()
