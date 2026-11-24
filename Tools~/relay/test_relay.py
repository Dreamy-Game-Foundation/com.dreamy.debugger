import json
import sqlite3
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from unittest.mock import patch
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
    def post(self, body, key='test-only', extra=None, path='/reports'):
        req = urllib.request.Request(f'http://127.0.0.1:{self.server.server_port}{path}',data=json.dumps(body).encode(),headers={'X-Dreamy-Key':key, **(extra or {})})
        try:
            with urllib.request.urlopen(req, timeout=3) as response: return response.status,json.load(response)
        except urllib.error.HTTPError as ex:
            with ex: return ex.code,json.load(ex)
    def test_acceptance_is_durable_and_duplicate_is_idempotent(self):
        self.assertEqual(self.post(report())[0],202); self.assertTrue(self.post(report())[1]['duplicate'])
        recreated=Store(self.store.path)
        with recreated.connect() as db: self.assertEqual(db.execute('SELECT COUNT(*) FROM reports').fetchone()[0],1)
    def test_connection_closes_after_transaction_and_rolls_back_on_failure(self):
        with self.store.connect() as db:
            db.execute('SELECT 1')
        with self.assertRaises(sqlite3.ProgrammingError):
            db.execute('SELECT 1')
        with self.assertRaises(RuntimeError):
            with self.store.connect() as db:
                db.execute('INSERT INTO reports VALUES (?,?,?)', ('rollback', '{}', 0))
                raise RuntimeError('simulate failure')
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT COUNT(*) FROM reports').fetchone()[0], 0)
    def test_wrong_key_is_rejected(self): self.assertEqual(self.post(report(),key='wrong')[0],401)
    def test_duplicate_id_cannot_replace_report(self):
        self.assertEqual(self.post(report())[0], 202)
        other = report()
        other['message'] = 'different report'
        self.assertEqual(self.post(other)[0], 400)
    def test_structured_details_survive_validation(self):
        payload = report()
        payload['details'] = dict(source='auto', severity='Exception', stackTrace='Game.Step()', occurrenceCount=3,
                                  firstSeenUtc='', lastSeenUtc='',
                                  performance=dict(sampledAtUtc=None, fps=59.5, unityAllocatedBytes=10, unityReservedBytes=20))
        self.assertEqual(validate(payload)['details'], payload['details'])
        payload['details']['performance']['fps'] = float('nan')
        with self.assertRaises(ValueError):
            validate(payload)
    def test_discord_sends_multipart_without_mentions(self):
        relay = Relay(self.store, fake=False, destinations=['discord'])
        payload = report()
        payload['message'] = '@everyone'
        payload['jpegBase64'] = '/9j/2Q=='
        with patch.dict('os.environ', {'DREAMY_DISCORD_WEBHOOK': 'https://discord.com/api/webhooks/123/test-only'}):
            with patch.object(relay, 'request') as request:
                relay.deliver('discord', payload)
        url, body, content_type = request.call_args.args
        self.assertTrue(url.endswith('?wait=true'))
        self.assertIn('multipart/form-data', content_type)
        self.assertEqual(request.call_args.kwargs['headers']['User-Agent'],
                         'DiscordBot (https://github.com/Dreamy-Game-Foundation/com.dreamy.debugger, 0.1.8)')
        self.assertIn(b'"allowed_mentions": {"parse": []}', body)
        self.assertIn(b'filename="report.json"', body)
        self.assertIn(b'filename="screenshot.jpg"', body)
        self.assertNotIn(b'test-only', body)
    def test_discord_editor_route_requires_live_discord_configuration(self):
        body = dict(report(), context='')
        route = '/reports/discord-test'
        self.assertEqual(self.post(body, path=route)[0], 409)
        self.assertEqual(self.post(body, key='wrong', path=route)[0], 401)
        self.relay.fake = False
        self.relay.destinations = ['discord']
        with patch.dict('os.environ', {'DREAMY_DISCORD_WEBHOOK': 'invalid'}):
            self.assertEqual(self.post(body, path=route)[0], 409)
    def test_discord_editor_route_only_queues_synthetic_reports_to_discord(self):
        self.relay.fake = False
        self.relay.destinations = ['telegram', 'discord']
        route = '/reports/discord-test'
        with patch.dict('os.environ', {'DREAMY_DISCORD_WEBHOOK': 'https://discord.com/api/webhooks/123/test-only'}):
            self.assertEqual(self.post(report(), path=route)[0], 400)
            self.assertEqual(self.post(dict(report(), context='', isSynthetic=False), path=route)[0], 400)
            body = dict(report(), context='')
            self.assertEqual(self.post(body, path=route)[0], 202)
            self.assertTrue(self.post(body, path=route)[1]['duplicate'])
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT id, destination FROM delivery').fetchall(), [(body['id'], 'discord')])
        with patch.dict('os.environ', {'DREAMY_DISCORD_WEBHOOK': 'https://discord.com/api/webhooks/123/test-only'}):
            with patch.object(self.relay, 'request') as outbound:
                self.assertTrue(self.store.one_delivery(self.relay.deliver))
                outbound.assert_called_once()
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT state FROM delivery').fetchall(), [('delivered',)])
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
