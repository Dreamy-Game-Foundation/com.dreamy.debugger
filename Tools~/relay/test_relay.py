import base64
import io
import json
import sqlite3
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from unittest.mock import patch
from contextlib import redirect_stdout
from email import policy
from email.parser import BytesParser
from html import unescape
from http.server import ThreadingHTTPServer
from pathlib import Path
import re
from server import DeliveryError, Relay, Store, discord_embed, report_summary, show_delivery_status, telegram_caption, validate


def report(key='a'*32):
    return dict(schema=1, id=key, message='[FAKE] test', buildId='test', context='test context', isSynthetic=True, createdUtc='', screenshotUtc='', screenshotReason='not requested', jpegBase64='')


def automatic_report():
    return dict(
        report(), isSynthetic=False, buildId='128', createdUtc='2026-11-25T08:20:42.0123456Z',
        message='NullReferenceException: Không mở được Shop\nmore detail',
        context='app=com.dreamy.game\nversion=0.4.2\nunity=6000.0\nplatform=Android\ndevice=Pixel 7\nos=Android 14\nscene=Assets/Scenes/Lobby.unity\nError: raw log detail',
        details=dict(source='auto', severity='Exception', stackTrace='ShopPanel.Refresh() (at Assets/Shop.cs:42)',
                     occurrenceCount=3, firstSeenUtc='', lastSeenUtc='',
                     performance=dict(sampledAtUtc=None, fps=58.7, unityAllocatedBytes=356*1048576, unityReservedBytes=512*1048576)),
    )


def multipart_fields(body, content_type):
    message = BytesParser(policy=policy.default).parsebytes(f'Content-Type: {content_type}\r\n\r\n'.encode() + body)
    return {part.get_param('name', header='content-disposition'): part.get_payload(decode=True)
            for part in message.iter_parts()}


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
    def test_health_advertises_full_report_testing(self):
        with urllib.request.urlopen(f'http://127.0.0.1:{self.server.server_port}/health', timeout=3) as response:
            health = json.load(response)
        self.assertEqual(health['service'], 'dreamy.debugger')
        self.assertIs(health['supportsFullTest'], True)
        self.assertEqual(health['maxReportBytes'], 3000000)
    def test_long_unicode_stack_and_console_context_survive_ingress_and_attachment(self):
        payload = automatic_report()
        payload['details']['stackTrace'] = 'Game.Step()\n' + 'Đệ quy\n' * 3400 + 'STACK END'
        payload['context'] += '\n' + '[Warning] Đang kết nối lại\n' * 4000 + 'CONSOLE END'
        self.assertGreater(len(payload['details']['stackTrace'].encode()), 32000)
        self.assertEqual(self.post(payload)[0], 202)
        relay = Relay(self.store, fake=False, destinations=['discord'])
        with patch.dict('os.environ', {'DREAMY_DISCORD_WEBHOOK': 'https://discord.com/api/webhooks/123/test-only'}):
            with patch.object(relay, 'request') as request:
                relay.deliver('discord', validate(payload))
        attachment = json.loads(multipart_fields(*request.call_args.args[1:])['files[0]'])
        self.assertEqual(attachment['details']['stackTrace'], payload['details']['stackTrace'])
        self.assertEqual(attachment['context'], payload['context'])
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
        parts = multipart_fields(body, content_type)
        message = json.loads(parts['payload_json'])
        self.assertEqual(message['allowed_mentions'], {'parse': []})
        self.assertEqual(message['attachments'], [{'id': 0, 'filename': 'report.json'}, {'id': 1, 'filename': 'screenshot.jpg'}])
        embed, = message['embeds']
        self.assertIn('Test report', embed['author']['name'])
        self.assertIn('@everyone', embed['description'])
        self.assertEqual(embed['image'], {'url': 'attachment://screenshot.jpg'})
        self.assertEqual(dict((field['name'], field['value']) for field in embed['fields'])['Report ID'], payload['id'])
        self.assertEqual(json.loads(parts['files[0]']), {k: v for k, v in payload.items() if k != 'jpegBase64'})
        self.assertEqual(parts['files[1]'], b'\xff\xd8\xff\xd9')
        self.assertNotIn(b'test-only', body)

    def test_automatic_summary_groups_the_same_error_across_builds(self):
        first = automatic_report()
        summary = report_summary(first)
        fields = dict(summary[3])
        self.assertEqual(summary[2], 'Không mở được Shop')
        self.assertEqual(fields['Platform · version'], 'Android · 0.4.2')
        self.assertEqual(fields['Scene'], 'Lobby')
        self.assertEqual(fields['Error'], 'NullReferenceException')
        self.assertEqual(fields['Stack trace'], 'ShopPanel.Refresh()')
        self.assertEqual(fields['Device'], 'Pixel 7 · Android 14')
        self.assertEqual(fields['Performance'], '58.7 FPS · 17.0 ms/frame')
        self.assertEqual(fields['Unity memory'], '356 / 512 MiB')
        self.assertEqual(fields['Occurrences'], '3')
        self.assertRegex(fields['Issue key'], r'^DD-[0-9A-F]{12}$')

        second = dict(first, id='b'*32, buildId='129', message='NullReferenceException: Another description')
        second['details'] = dict(first['details'], stackTrace='UnityEngine.Debug:LogError (object)\nat ShopPanel.Refresh() [0x123] in <assembly>:0')
        self.assertEqual(dict(report_summary(second)[3])['Issue key'], fields['Issue key'])
        second['message'] = 'ArgumentException: Another description'
        self.assertNotEqual(dict(report_summary(second)[3])['Issue key'], fields['Issue key'])
        different_method = dict(first, details=dict(first['details'], stackTrace='ShopPanel.Close()'))
        self.assertNotEqual(dict(report_summary(different_method)[3])['Issue key'], fields['Issue key'])
        different_app = dict(first, context=first['context'].replace('com.dreamy.game', 'com.dreamy.other'))
        self.assertNotEqual(dict(report_summary(different_app)[3])['Issue key'], fields['Issue key'])
        second['context'] = 'Legacy context without project information'
        self.assertNotIn('Issue key', dict(report_summary(second)[3]))

    def test_chat_summaries_bound_and_escape_untrusted_text(self):
        payload = automatic_report()
        markup = '<a href="tg://user?id=123">🛒&</a> ' * 30
        payload['message'] = markup + '\nsecond line stays in attachment'
        payload['context'] = '\n'.join(f'{key}={markup}' for key in ('app', 'platform', 'version', 'scene', 'unity', 'device', 'os'))
        payload['details']['stackTrace'] = '\n'.join(f'ShopPanel.Refresh{index}({markup})' for index in range(6))
        summary = report_summary(payload)
        caption = telegram_caption(summary)
        self.assertNotIn('<a href=', caption)
        self.assertIn('&lt;a href=', caption)
        self.assertLessEqual(len(unescape(re.sub(r'<[^>]+>', '', caption)).encode('utf-16-le')) // 2, 1024)
        self.assertNotIn('second line', caption)
        self.assertIn(payload['id'], caption)
        self.assertIn('DD-', caption)
        self.assertIn('<pre>', caption)
        embed = discord_embed(summary, False)
        self.assertNotIn('image', embed)
        self.assertEqual(embed['timestamp'], '2026-11-25T08:20:42+00:00')
        self.assertLessEqual(len(embed['description']), 4096)
        self.assertTrue(all(len(field['value']) <= 1024 for field in embed['fields']))

    def test_legacy_manual_report_has_a_readable_summary(self):
        payload = dict(report(), isSynthetic=False, message='Không mở được Shop', createdUtc='old-format')
        summary = report_summary(payload)
        embed = discord_embed(summary, False)
        self.assertIn('Manual report', embed['author']['name'])
        self.assertEqual(embed['description'], '**Không mở được Shop**')
        self.assertNotIn('timestamp', embed)
        self.assertNotIn('Issue key', dict(summary[3]))
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
    def test_telegram_editor_route_isolated_and_receipt_has_no_payload(self):
        route = '/reports/telegram-test'
        body = dict(report(), context='')
        config = {'TELEGRAM_BOT_TOKEN': '123:fixture-token', 'TELEGRAM_CHAT_ID': '-100123'}
        with patch.dict('os.environ', config):
            self.assertEqual(self.post(body, path=route)[0], 409)
            self.relay.fake = False
            self.relay.destinations = ['discord', 'telegram']
            with patch.dict('os.environ', {'TELEGRAM_CHAT_ID': ''}):
                self.assertEqual(self.post(body, path=route)[0], 409)
            self.assertEqual(self.post(body, key='wrong', path=route)[0], 401)
            self.assertEqual(self.post(report(), path=route)[0], 400)
            self.assertEqual(self.post(dict(body, jpegBase64='/9j/2Q=='), path=route)[0], 400)
            self.assertEqual(self.post(dict(body, isSynthetic=False), path=route)[0], 400)
            other = dict(body, id='b' * 32)
            self.assertEqual(self.post(other)[0], 202)
            self.assertEqual(self.post(other, path=route)[0], 400)
            self.assertEqual(self.post(body, path=route)[0], 202)
            self.assertTrue(self.post(body, path=route)[1]['duplicate'])
            with self.store.connect() as db:
                self.assertEqual(db.execute('SELECT destination FROM delivery WHERE id=?', (body['id'],)).fetchall(), [('telegram',)])
                db.execute('UPDATE delivery SET next=1e20 WHERE id=?', (other['id'],))
            with patch.object(self.relay, 'request') as send:
                self.assertTrue(self.store.one_delivery(self.relay.deliver))
                send.assert_called_once()
        output = io.StringIO()
        with redirect_stdout(output):
            show_delivery_status(self.store.path, body['id'])
        receipt = json.loads(output.getvalue())
        self.assertEqual(receipt['id'], body['id'])
        self.assertEqual(receipt['delivery'], [
            dict(destination='telegram', state='delivered', attempts=1, retryAfterSeconds=0, error='')])

    def test_full_editor_report_routes_one_sample_with_all_diagnostics(self):
        self.relay.fake = False
        self.relay.destinations = ['telegram', 'discord']
        screenshot = (Path(__file__).resolve().parents[2] / 'Editor' / 'RelaySampleScreenshot.bytes').read_bytes()
        payload = dict(automatic_report(), isSynthetic=True, jpegBase64=base64.b64encode(screenshot).decode())
        payload['details']['source'] = 'delivery-test'
        payload['details']['stackTrace'] += '\nShopController.Open() (at Assets/ShopController.cs:87)'
        config = {'DREAMY_DISCORD_WEBHOOK': 'https://discord.com/api/webhooks/123/test-only',
                  'TELEGRAM_BOT_TOKEN': '123:fixture-token', 'TELEGRAM_CHAT_ID': '-100123'}
        for dest, outgoing_count in (('discord', 1), ('telegram', 2)):
            with self.subTest(destination=dest), patch.dict('os.environ', config):
                body = dict(payload, id=('d' if dest == 'discord' else 'e') * 32)
                self.assertEqual(self.post(body, path=f'/reports/{dest}-test')[0], 202)
                with patch.object(self.relay, 'request') as request:
                    self.assertTrue(self.store.one_delivery(self.relay.deliver))
                self.assertEqual(request.call_count, outgoing_count)
                first = multipart_fields(*request.call_args_list[0].args[1:])
                rendered = first.get('payload_json', first.get('caption')).decode()
                for expected in ('sample data', 'Pixel 7', '58.7 FPS', 'ShopController.Open()', body['id']):
                    self.assertIn(expected, rendered)
                sent_image = (first['files[1]'] if dest == 'discord'
                              else multipart_fields(*request.call_args_list[1].args[1:])['document'])
                self.assertEqual(sent_image, screenshot)
                with self.store.connect() as db:
                    self.assertEqual(db.execute('SELECT destination FROM delivery WHERE id=?', (body['id'],)).fetchall(), [(dest,)])

    def test_telegram_sends_report_and_original_screenshot_document(self):
        relay = Relay(self.store, fake=False, destinations=['telegram'])
        payload = dict(automatic_report(), jpegBase64='/9j/2Q==')
        with patch.dict('os.environ', {'TELEGRAM_BOT_TOKEN': '123:fixture-token', 'TELEGRAM_CHAT_ID': '-100123'}):
            with patch.object(relay, 'request') as send:
                relay.deliver('telegram', payload)
        document, photo = send.call_args_list
        self.assertTrue(document.args[0].endswith('/sendDocument'))
        self.assertTrue(photo.args[0].endswith('/sendDocument'))
        self.assertIn(b'filename="report.json"', document.args[1])
        self.assertNotIn(b'jpegBase64', document.args[1])
        self.assertIn(b'filename="screenshot.jpg"', photo.args[1])
        self.assertIn(payload['id'].encode(), photo.args[1])
        self.assertNotEqual(document.args[2], photo.args[2])
        self.assertTrue(all(call.kwargs['telegram'] for call in (document, photo)))
        document_parts = multipart_fields(*document.args[1:])
        photo_parts = multipart_fields(*photo.args[1:])
        self.assertEqual(document_parts['parse_mode'], b'HTML')
        self.assertEqual(photo_parts['parse_mode'], b'HTML')
        self.assertIn(b'<b>Issue key</b>', document_parts['caption'])
        self.assertIn(b'<b>Report ID</b>', document_parts['caption'])
        self.assertIn(b'2026-11-25', document_parts['caption'])
        self.assertEqual(json.loads(document_parts['document']), {k: v for k, v in payload.items() if k != 'jpegBase64'})
        self.assertEqual(photo_parts['document'], b'\xff\xd8\xff\xd9')

    def test_telegram_api_failure_does_not_record_delivery_and_retries(self):
        body = dict(report(), context='')
        self.store.accept(body, ['telegram'])
        responses = [
            io.BytesIO(b'{"ok": false, "error_code": 429, "parameters":{"retry_after":45}}'),
            io.BytesIO(b'{"ok": true, "result":{"message_id":1}}')
        ]
        relay = Relay(self.store, fake=False, destinations=['telegram'])
        with patch.dict('os.environ', {'TELEGRAM_BOT_TOKEN': '123:fixture-token', 'TELEGRAM_CHAT_ID': '100123'}):
            with patch('server.urllib.request.urlopen', return_value=responses[0]):
                self.store.one_delivery(relay.deliver, now=100)
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT state,next,error FROM delivery').fetchone(), ('pending', 145, 'Telegram API 429'))
        with patch.dict('os.environ', {'TELEGRAM_BOT_TOKEN': '123:fixture-token', 'TELEGRAM_CHAT_ID': '100123'}):
            with patch('server.urllib.request.urlopen', return_value=responses[1]):
                self.store.one_delivery(relay.deliver, now=145)
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT state,attempts,error FROM delivery').fetchone(), ('delivered', 2, ''))

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
