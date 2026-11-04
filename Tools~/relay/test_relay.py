import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path
from server import DeliveryError, Relay, Store, validate


def report(key='a'*32):
    return dict(schema=1, id=key, message='[FAKE] test', buildId='test', context='test context', isSynthetic=True, createdUtc='', screenshotUtc='', screenshotReason='not requested', jpegBase64='')


class RelayTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.store = Store(Path(self.temp.name)/'reports.sqlite')
        self.relay = Relay(self.store, key='test-only', fake=True)
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), self.relay.handler())
        self.thread = threading.Thread(target=self.server.serve_forever); self.thread.start()
    def tearDown(self):
        self.server.shutdown(); self.server.server_close(); self.thread.join(); self.temp.cleanup()
    def post(self, body, key='test-only', extra=None):
        req = urllib.request.Request('http://127.0.0.1:%s/reports'%self.server.server_address[1],data=json.dumps(body).encode(),headers={'X-Dreamy-Key':key, **(extra or {})})
        try:
            with urllib.request.urlopen(req, timeout=3) as response: return response.status,json.load(response)
        except urllib.error.HTTPError as ex:
            with ex: return ex.code,json.load(ex)
    def test_acceptance_is_durable_and_duplicate_is_idempotent(self):
        self.assertEqual(self.post(report())[0],202); self.assertTrue(self.post(report())[1]['duplicate'])
        recreated=Store(self.store.path)
        with recreated.connect() as db: self.assertEqual(db.execute('SELECT COUNT(*) FROM reports').fetchone()[0],1)
    def test_wrong_key_is_rejected(self): self.assertEqual(self.post(report(),key='wrong')[0],401)
    def test_schema_validation(self):
        r=report();r['schema']=2;self.assertEqual(self.post(r)[0],400)
    def test_invalid_id_cannot_escape_storage(self):
        r=report('../../escape');self.assertEqual(self.post(r)[0],400)
    def test_mismatched_idempotency_key_is_rejected(self): self.assertEqual(self.post(report(),extra={'Idempotency-Key':'b'*32})[0],400)
    def test_payload_caps(self):
        r=report();r['context']='x'*262145;self.assertEqual(self.post(r)[0],400)
    def test_invalid_jpeg(self):
        r=report();r['jpegBase64']='YWJj';self.assertEqual(self.post(r)[0],400)
    def test_rate_limit(self):
        self.relay.per_minute=1;self.post(report());self.assertEqual(self.post(report('b'*32))[0],429)
    def test_storage_quota_returns_retryable_status(self):
        self.store.max_rows=1;self.post(report());self.assertEqual(self.post(report('b'*32))[0],503)
    def test_fake_delivery_is_persisted_without_external_network(self):
        self.post(report());self.assertTrue(self.store.one_delivery(self.relay.deliver))
        with self.store.connect() as db:self.assertEqual(db.execute('SELECT state FROM delivery').fetchone()[0],'delivered')
    def test_destination_retry_respects_retry_after(self):
        self.post(report())
        def rate_limited(*_):raise DeliveryError('429',retry_after=120)
        self.store.one_delivery(rate_limited,now=100)
        with self.store.connect() as db: self.assertEqual(db.execute('SELECT next FROM delivery').fetchone()[0],220)
        self.assertFalse(self.store.one_delivery(self.relay.deliver,now=200))
    def test_permanent_destination_failure_is_dead(self):
        self.post(report())
        def bad(*_):raise DeliveryError('bad destination',permanent=True)
        self.store.one_delivery(bad)
        with self.store.connect() as db:self.assertEqual(db.execute('SELECT state FROM delivery').fetchone()[0],'dead')
    def test_unknown_fields_are_not_persisted(self):
        r=report();r['secretBlob']='ignored';self.assertNotIn('secretBlob',validate(r))

if __name__=='__main__':unittest.main()
