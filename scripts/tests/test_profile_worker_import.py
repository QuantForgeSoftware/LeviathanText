"""Run with python3 -B -m unittest discover -s scripts/tests -p 'test_profile_worker_import.py'.

Exercises the exact profiling script in an isolated checkout with disposable HTTP
listeners and worker stubs. No application build, model or existing service is used.
"""
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
import json
import os
import shutil
import signal
import socket
import subprocess
import tempfile
import threading
import unittest


ROOT = Path(__file__).resolve().parents[2]
API_STUB = r'''#!/usr/bin/env python3
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from urllib.parse import urlsplit
import json, os, sys, time

store = Path(os.environ['LeviathanText__StorePath'])
store.with_name('api.pid').write_text(str(os.getpid()))
port = urlsplit(sys.argv[sys.argv.index('--urls') + 1]).port
time.sleep(float(os.environ.get('PROFILE_TEST_STARTUP_DELAY', '0')))
if os.environ.get('PROFILE_TEST_WRONG_ANNOUNCEMENT') == '1':
    print('      Now listening on: http://127.0.0.1:1', flush=True)
    time.sleep(30)
    sys.exit(1)
class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def respond(self, status, body):
        payload = body.encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)
    def record(self):
        with store.with_name('requests.jsonl').open('a') as log:
            log.write(json.dumps({'method': self.command, 'path': self.path}) + '\n')
    def do_GET(self):
        self.record()
        if self.path == '/': self.respond(200, 'LeviathanText')
        elif self.path.startswith('/api/v1/jobs/'):
            status = 'succeeded' if store.with_name('worker.pid').exists() else 'pending'
            self.respond(200, json.dumps({'status': status}))
        else: self.respond(200, '{"objects":[{"id":"obj-fixture"}]}')
    def do_POST(self):
        self.rfile.read(int(self.headers.get('Content-Length', '0')))
        self.record()
        identifier = 'col-fixture' if self.path.endswith('/projects') else 'job-fixture'
        self.respond(200, json.dumps({'id': identifier}))
server = HTTPServer(('127.0.0.1', port), Handler)
print(f'      Now listening on: http://127.0.0.1:{server.server_port}', flush=True)
server.serve_forever()
'''
WORKER_STUB = r'''#!/usr/bin/env python3
from pathlib import Path
import os, sys, time
store = Path(sys.argv[sys.argv.index('--store') + 1])
store.with_name('worker.pid').write_text(str(os.getpid()))
while True: time.sleep(1)
'''


class ProfileWorkerImportTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='lt-profile-test-')
        self.root = Path(self.temporary.name)
        (self.root / 'scripts').mkdir()
        shutil.copyfile(ROOT / 'scripts/profile-worker-import.sh', self.root / 'scripts/profile-worker-import.sh')
        fixture = self.root / 'tests/LeviathanText.Formats.Tests/Fixtures/sample.page.xml'
        fixture.parent.mkdir(parents=True)
        fixture.write_text('<PcGts/>')
        for name, source in [('Api', API_STUB), ('Worker', WORKER_STUB)]:
            executable = self.root / f'src/LeviathanText.{name}/bin/Debug/net10.0/LeviathanText.{name}'
            executable.parent.mkdir(parents=True)
            executable.write_text(source)
            executable.chmod(0o755)
        self.output = self.root / 'output'

    def tearDown(self):
        self.temporary.cleanup()

    def run_profile(self, port, **extra):
        env = os.environ.copy()
        env.update(PORT=str(port), **extra)
        process = subprocess.Popen(['bash', 'scripts/profile-worker-import.sh', '1', str(self.output)],
            cwd=self.root, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, start_new_session=True)
        try:
            stdout, stderr = process.communicate(timeout=8)
            return process.returncode, stdout, stderr
        finally:
            # Also clean up every stub on a timeout/assertion failure in an unfixed script.
            if process.poll() is None:
                try: os.killpg(process.pid, signal.SIGTERM)
                except ProcessLookupError: pass
            process.wait(timeout=3)

    def existing_service(self):
        requests = []
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def respond(self, body):
                self.send_response(200)
                self.end_headers()
                self.wfile.write(body.encode())
            def do_GET(self):
                requests.append((self.command, self.path))
                self.respond('LeviathanText' if self.path == '/' else '{"status":"pending"}')
            def do_POST(self):
                self.rfile.read(int(self.headers.get('Content-Length', '0')))
                requests.append((self.command, self.path))
                self.respond(json.dumps({'id': 'col-fixture' if self.path.endswith('/projects') else 'job-fixture'}))
        server = HTTPServer(('127.0.0.1', 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(thread.join, 3)
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        return server.server_port, requests

    def test_occupied_port_is_rejected_before_contacting_existing_service(self):
        port, requests = self.existing_service()
        self.output.mkdir()
        (self.output / 'api.log').write_text(f'Now listening on: http://127.0.0.1:{port}\n')
        code, _, _ = self.run_profile(port, PROFILE_TEST_STARTUP_DELAY='0.4')
        self.assertNotEqual(0, code)
        self.assertEqual([], requests, 'Readiness must come from the spawned API, not an existing listener')
        self.assertFalse((self.output / 'worker.pid').exists())

    def test_wrong_listener_announcement_is_rejected_before_contacting_existing_service(self):
        port, requests = self.existing_service()
        code, _, _ = self.run_profile(port, PROFILE_TEST_WRONG_ANNOUNCEMENT='1')
        self.assertNotEqual(0, code)
        self.assertEqual([], requests)
        self.assertFalse((self.output / 'worker.pid').exists())

    def test_owned_listener_completes_the_profile(self):
        with socket.socket() as reservation:
            reservation.bind(('127.0.0.1', 0))
            port = reservation.getsockname()[1]
        self.assert_successful_profile(port)

    def test_dynamic_owned_port_completes_the_profile(self):
        self.assert_successful_profile(0)

    def assert_successful_profile(self, port):
        code, stdout, stderr = self.run_profile(port)
        self.assertEqual(0, code, stderr)
        summary = json.loads(stdout)
        self.assertEqual('succeeded', summary['job_status'])
        self.assertEqual(1, summary['objects'])
        requests = [json.loads(line) for line in (self.output / 'requests.jsonl').read_text().splitlines()]
        self.assertEqual(['/api/v1/projects', '/api/v1/jobs'],
            [request['path'] for request in requests if request['method'] == 'POST'])
        self.assertTrue((self.output / 'rss.csv').exists())
        for name in ('api', 'worker'):
            pid = int((self.output / f'{name}.pid').read_text())
            with self.assertRaises(ProcessLookupError, msg=f'{name} was not cleaned up'):
                os.kill(pid, 0)


if __name__ == '__main__':
    unittest.main()
