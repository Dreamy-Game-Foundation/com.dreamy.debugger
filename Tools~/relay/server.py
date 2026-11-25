"""Dreamy diagnostics relay. Durable ingestion, bounded storage, destination retries.
Default fake delivery performs no external requests. Run behind TLS for remote use.
"""
import argparse
import base64
from contextlib import closing, contextmanager
from datetime import datetime, timezone
import hashlib
import hmac
from html import escape
import json
import os
import re
import sqlite3
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

MAX_BODY = 3000000
ID = re.compile(r'^[0-9a-f]{32}$')
TELEGRAM_TOKEN = re.compile(r'[0-9]+:[A-Za-z0-9_-]+')
TELEGRAM_CHAT_ID = re.compile(r'-?[0-9]+|@[A-Za-z0-9_]+')
TEST_DESTINATIONS = {'/reports/discord-test': 'discord', '/reports/telegram-test': 'telegram'}
DISCORD_WEBHOOK = re.compile(r'https://discord\.com/api(?:/v\d+)?/webhooks/\d+/[A-Za-z0-9_-]+')
DISCORD_USER_AGENT = 'DiscordBot (https://github.com/Dreamy-Game-Foundation/com.dreamy.debugger, 0.1.8)'
TELEGRAM_CAPTION_LIMIT = 1024
DELIVERY_TEST_SOURCE = 'delivery-test'


def preview(text, limit):
    text = re.sub(r'[\x00-\x08\x0e-\x1f\x7f\u202a-\u202e\u2066-\u2069]', '', text)
    text = ' '.join(text.split())
    return clip_utf16(text, limit)


def clip_utf16(text, limit):
    utf16 = text.encode('utf-16-le')
    if len(utf16) <= limit * 2:
        return text
    return utf16[:(limit - 1) * 2].decode('utf-16-le', errors='ignore').rstrip() + '…'


def error_frames(stack):
    frames = []
    for line in stack.splitlines():
        if '(' not in line:
            continue
        frame = re.sub(r'^at\s+', '', line.strip())
        frame = re.split(r'\s+\(at\s|\s+\[0x[\da-fA-F]+\]|\s+in\s+(?=[<\\/]|[A-Za-z]:)', frame, maxsplit=1)[0]
        if (frame.partition('(')[0].strip() and not re.match(r'UnityEngine\.(Debug|DebugLogHandler|Logger)[.:]', frame)
                and not frame.startswith('Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService')):
            frames.append(frame.strip())
            if len(frames) == 3:
                break
    return frames


def report_summary(report):
    context = {}
    for line in report['context'].splitlines():
        key, separator, value = line.partition('=')
        if not separator or key in context or key not in {'app', 'version', 'unity', 'platform', 'device', 'os', 'scene'}:
            break
        context[key] = value.strip()

    fields = []
    platform, version = (preview(context.get(key, ''), 32) for key in ('platform', 'version'))
    environment = ' · '.join(part for part in (platform, version) if part)
    if environment:
        fields.append(('Platform · version', environment))
    scene_name = context.get('scene', '').replace('\\', '/').rsplit('/', 1)[-1]
    scene = preview(re.sub(r'\.unity$', '', scene_name, flags=re.IGNORECASE), 56)
    if scene:
        fields.append(('Scene', scene))
    device = ' · '.join(value for value in (preview(context.get('device', ''), 48), preview(context.get('os', ''), 36)) if value)
    if device:
        fields.append(('Device', device))
    unity = preview(context.get('unity', ''), 24)
    if unity:
        fields.append(('Unity', unity))

    details = report.get('details', {})
    full_test = report['isSynthetic'] and details.get('source') == DELIVERY_TEST_SOURCE
    automatic = not report['isSynthetic'] and details.get('source') == 'auto'
    performance = details.get('performance', {})
    fps = performance.get('fps')
    if fps:
        fields.append(('Performance', f'{fps:.1f} FPS · {1000 / fps:.1f} ms/frame' if fps >= .1 else '< 0.1 FPS'))
    allocated = performance.get('unityAllocatedBytes', 0)
    reserved = performance.get('unityReservedBytes', 0)
    if allocated and reserved:
        fields.append(('Unity memory', f'{allocated / 1048576:.0f} / {reserved / 1048576:.0f} MiB'))

    subject = next((line.strip() for line in report['message'].splitlines() if line.strip()), '')
    if report['isSynthetic']:
        category, color = 'Delivery test · sample data' if full_test else 'Test report', 0x78849A
    elif automatic:
        category, color = 'Automatic error', 0xE25568
    else:
        category, color = 'Manual report', 0x54C7A0

    if full_test or automatic:
        exception = re.match(r'([\w.]*Exception)\b', subject)
        severity = exception[1] if exception else details.get('severity') or 'Error'
        if exception and subject[exception.end():].startswith(':'):
            subject = subject[exception.end() + 1:].strip() or severity
        fields.append(('Error', preview(severity, 54)))
        fields.append(('Occurrences', str(details['occurrenceCount'])))
        frames = error_frames(details.get('stackTrace', ''))
        if frames:
            fields.append(('Stack trace', '\n'.join(preview(frame, 110) for frame in frames)))
            if context.get('app'):
                signature = '\n'.join((context['app'], severity, re.sub(r'\s+', '', frames[0])))
                issue_key = 'DD-' + hashlib.sha256(signature.encode()).hexdigest()[:12].upper()
                fields.append(('Issue key', issue_key))

    build = preview(report['buildId'], 48)
    if build and (full_test or not report['isSynthetic']):
        fields.append(('Build ID', build))
    fields.append(('Report ID', report['id']))

    timestamp = None
    try:
        stamp = datetime.fromisoformat(re.sub(r'\.\d+(?=Z|[+-])', '', report['createdUtc']).replace('Z', '+00:00'))
        if stamp.tzinfo is not None:
            timestamp = stamp.astimezone(timezone.utc).isoformat(timespec='seconds')
    except (ValueError, OverflowError):
        pass
    return category, color, preview(subject, 150) or 'Report', fields, timestamp


def discord_embed(summary, has_image):
    category, color, subject, fields, timestamp = summary

    def markdown(text):
        return re.sub(r'([\\`*_\[\]<>#|~])', r'\\\1', text)

    values = dict(fields)
    blocks = []
    groups = (
        ('Environment', ('Platform · version', 'Device', 'Scene', 'Unity', 'Build ID')),
        ('Performance', ('Performance', 'Unity memory')),
        ('Error', ('Error', 'Occurrences')),
    )
    for heading, keys in groups:
        rows = [
            markdown(values[key]) if key in ('Platform · version', 'Performance', 'Error')
            else f'**{key}** {markdown(values[key])}'
            for key in keys if values.get(key)
        ]
        if rows:
            blocks.append({'name': heading, 'value': '\n'.join(rows), 'inline': True})
    stack = values.get('Stack trace')
    if stack:
        blocks.append({'name': 'Stack trace', 'value': '```\n' + stack.replace('`', 'ˋ') + '\n```', 'inline': False})
    if values.get('Issue key'):
        blocks.append({'name': 'Issue key', 'value': values['Issue key'], 'inline': True})
    blocks.append({'name': 'Report ID', 'value': values['Report ID'], 'inline': True})

    embed = {
        'author': {'name': 'Dreamy Debugger · ' + category},
        'description': f'**{markdown(subject)}**',
        'color': color,
        'fields': blocks,
    }
    if timestamp:
        embed['timestamp'] = timestamp
    if has_image:
        embed['image'] = {'url': 'attachment://screenshot.jpg'}
    return embed


def telegram_caption(summary):
    category, _, subject, fields, timestamp = summary
    byline = 'Dreamy Debugger · ' + category
    lines = [f'<b>{escape(subject)}</b>', f'<i>{byline}</i>', '']
    visible = [subject, byline, '']
    stack = ''
    for name, value in fields:
        if name == 'Stack trace':
            stack = value
        elif value:
            shown = f'<code>{escape(value)}</code>' if name in ('Issue key', 'Report ID') else escape(value)
            lines.append(f'<b>{name}</b> {shown}')
            visible.append(f'{name} {value}')
    if timestamp:
        stamp = f'{timestamp[:10]} · {timestamp[11:19]} UTC'
        lines.append(f'<i>{stamp}</i>')
        visible.append(stamp)
    stack_header = '\n\nStack trace\n'
    budget = TELEGRAM_CAPTION_LIMIT - len(('\n'.join(visible) + stack_header).encode('utf-16-le')) // 2
    if stack and budget > 1:
        lines.extend(('', '<b>Stack trace</b>', f'<pre>{escape(clip_utf16(stack, budget))}</pre>'))
    return '\n'.join(lines)


class DeliveryError(Exception):
    def __init__(self, message, retry_after=0, permanent=False):
        super().__init__(message)
        self.retry_after, self.permanent = retry_after, permanent


class Store:
    def __init__(self, path, max_rows=1000):
        self.path, self.max_rows = str(path), max_rows
        self.lock = threading.RLock()
        Path(self.path).parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as db:
            db.execute('PRAGMA journal_mode=WAL')
            db.execute('CREATE TABLE IF NOT EXISTS reports (id TEXT PRIMARY KEY, body TEXT NOT NULL, created REAL NOT NULL)')
            db.execute('CREATE TABLE IF NOT EXISTS delivery (id TEXT, destination TEXT, state TEXT NOT NULL, attempts INTEGER DEFAULT 0, next REAL DEFAULT 0, error TEXT DEFAULT "", PRIMARY KEY(id,destination))')

    @contextmanager
    def connect(self):
        db = sqlite3.connect(self.path, timeout=15)
        try:
            with db:
                yield db
        finally:
            db.close()

    def accept(self, payload, destinations, exact_destination=False):
        with self.lock, self.connect() as db:
            previous = db.execute('SELECT body FROM reports WHERE id=?', (payload['id'],)).fetchone()
            if previous:
                if json.loads(previous[0]) != payload:
                    raise ValueError('report id already used')
                if exact_destination:
                    queued = db.execute('SELECT destination FROM delivery WHERE id=?', (payload['id'],)).fetchall()
                    if queued != [(destinations[0],)]:
                        raise ValueError('report id already used for another destination')
                return False
            count = db.execute('SELECT COUNT(*) FROM reports').fetchone()[0]
            if count >= self.max_rows:
                raise DeliveryError('relay storage quota', retry_after=60)
            db.execute('INSERT INTO reports VALUES (?,?,?)', (payload['id'], json.dumps(payload), time.time()))
            for destination in destinations:
                db.execute('INSERT INTO delivery(id,destination,state) VALUES (?,?,"pending")', (payload['id'], destination))
            return True

    def cleanup(self, retention_days=7):
        with self.lock, self.connect() as db:
            cutoff = time.time() - retention_days * 86400
            db.execute('DELETE FROM delivery WHERE id IN (SELECT id FROM reports WHERE created<?)', (cutoff,))
            db.execute('DELETE FROM reports WHERE created<?', (cutoff,))

    def one_delivery(self, send, now=None):
        now = time.time() if now is None else now
        # A single worker invokes this method. Rows remain retryable if the process dies during send.
        with self.lock, self.connect() as db:
            row = db.execute('SELECT d.id,d.destination,d.attempts,r.body FROM delivery d JOIN reports r USING(id) WHERE d.state="pending" AND d.next<=? ORDER BY r.created LIMIT 1', (now,)).fetchone()
        if not row:
            return False
        report_id, destination, attempts, body = row
        state, next_at, error = 'delivered', 0, ''
        try:
            send(destination, json.loads(body))
        except DeliveryError as ex:
            error = str(ex)[:160]
            state = 'dead' if ex.permanent or attempts >= 7 else 'pending'
            next_at = now + max(ex.retry_after, min(3600, 10 * 2 ** attempts))
        except Exception as ex:
            # Never store tokens, URLs containing tokens, mail credentials, or response bodies.
            error = type(ex).__name__
            state = 'dead' if attempts >= 7 else 'pending'
            next_at = now + min(3600, 10 * 2 ** attempts)
        with self.lock, self.connect() as db:
            db.execute('UPDATE delivery SET state=?,attempts=attempts+1,next=?,error=? WHERE id=? AND destination=?', (state, next_at, error, report_id, destination))
        return True


def validate(body):
    if not isinstance(body, dict) or body.get('schema') != 1 or not isinstance(body.get('id'), str) or not ID.fullmatch(body['id']):
        raise ValueError('invalid schema/id')
    for key, limit in [('message', 65536), ('buildId', 256), ('context', 262144), ('screenshotReason', 256), ('createdUtc', 100), ('screenshotUtc', 100)]:
        value = body.get(key, '')
        if not isinstance(value, str) or len(value.encode('utf-8')) > limit:
            raise ValueError('invalid field: ' + key)
    if not body.get('message') or type(body.get('isSynthetic', False)) is not bool:
        raise ValueError('invalid message/marker')
    encoded = body.get('jpegBase64', '')
    if not isinstance(encoded, str) or len(encoded) > 700000:
        raise ValueError('image cap')
    image = base64.b64decode(encoded, validate=True) if encoded else b''
    if len(image) > 524288 or image and not image.startswith(b'\xff\xd8'):
        raise ValueError('invalid jpeg')
    # Only known protocol fields are persisted; unknown fields cannot carry arbitrary blobs.
    payload = {key: body.get(key, default) for key, default in [('schema', 1), ('id', ''), ('message', ''), ('buildId', ''), ('context', ''), ('isSynthetic', False), ('createdUtc', ''), ('screenshotReason', ''), ('screenshotUtc', ''), ('jpegBase64', '')]}
    if 'details' in body:
        details = body['details']
        allowed = {'source', 'severity', 'stackTrace', 'occurrenceCount', 'firstSeenUtc', 'lastSeenUtc', 'performance'}
        if not isinstance(details, dict) or set(details) != allowed:
            raise ValueError('invalid report details')
        for key, cap in [('source', 128), ('severity', 128), ('stackTrace', 131072), ('firstSeenUtc', 100), ('lastSeenUtc', 100)]:
            if not isinstance(details[key], str) or len(details[key].encode('utf-8')) > cap:
                raise ValueError('invalid report detail: ' + key)
        if type(details['occurrenceCount']) is not int or not 1 <= details['occurrenceCount'] <= 2147483647:
            raise ValueError('invalid occurrence count')
        metrics = details['performance']
        if not isinstance(metrics, dict) or set(metrics) != {'sampledAtUtc', 'fps', 'unityAllocatedBytes', 'unityReservedBytes'}:
            raise ValueError('invalid performance metrics')
        stamp, fps = metrics['sampledAtUtc'], metrics['fps']
        if stamp is not None and (not isinstance(stamp, str) or len(stamp) > 100):
            raise ValueError('invalid metric timestamp')
        if fps is not None and (type(fps) not in (int, float) or not 0 < fps <= 1000000):
            raise ValueError('invalid fps')
        if any(type(metrics[k]) is not int or not 0 <= metrics[k] <= 2**63-1 for k in ('unityAllocatedBytes', 'unityReservedBytes')):
            raise ValueError('invalid memory metrics')
        payload['details'] = details
    return payload


class Relay:
    def __init__(self, store, key='', fake=True, destinations=None, per_minute=30):
        self.store, self.key, self.fake = store, key, fake
        self.destinations = ['fake'] if fake else list(destinations or [])
        self.per_minute, self.hits, self.lock = per_minute, [], threading.Lock()
        if not fake and any(destination not in ('telegram', 'discord') for destination in self.destinations):
            raise ValueError('unknown delivery destination')
        if not self.destinations:
            raise ValueError('no destinations configured')

    def handler(self):
        relay = self
        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_):
                pass  # Access logs must not expose ingestion keys or payloads.

            def reply(self, code, data, retry=0):
                payload = json.dumps(data).encode()
                self.send_response(code)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(payload)))
                if retry:
                    self.send_header('Retry-After', str(retry))
                self.end_headers()
                self.wfile.write(payload)

            def do_GET(self):
                health = {'service': 'dreamy.debugger', 'fakeDelivery': relay.fake, 'pid': os.getpid(),
                          'supportsFullTest': True, 'maxReportBytes': MAX_BODY}
                for destination in ('telegram', 'discord'):
                    profile = os.getenv('DREAMY_LOCAL_' + destination.upper() + '_PROFILE')
                    if profile:
                        health['local' + destination.title() + 'Profile'] = profile
                self.reply(200 if self.path == '/health' else 404, health)

            def do_POST(self):
                self.connection.settimeout(15)
                test_destination = TEST_DESTINATIONS.get(self.path)
                if self.path != '/reports' and not test_destination:
                    self.reply(404, {'error': 'not found'}); return
                if relay.key and not hmac.compare_digest(self.headers.get('X-Dreamy-Key', ''), relay.key):
                    self.reply(401, {'error': 'unauthorized'}); return
                if test_destination and (relay.fake or test_destination not in relay.destinations or
                                         not relay.destination_configured(test_destination)):
                    self.reply(409, {'error': 'live delivery is not configured'}); return
                try:
                    length = int(self.headers.get('Content-Length', '0'))
                    if length <= 0 or length > MAX_BODY:
                        self.reply(413, {'error': 'payload cap'}); return
                    with relay.lock:
                        now = time.monotonic()
                        relay.hits = [t for t in relay.hits if now - t < 60]
                        if len(relay.hits) >= relay.per_minute:
                            self.reply(429, {'error': 'rate limit'}, 60); return
                        relay.hits.append(now)
                    payload = validate(json.loads(self.rfile.read(length)))
                    if test_destination:
                        legacy_test = not (payload['context'] or payload['jpegBase64'] or 'details' in payload)
                        full_test = payload.get('details', {}).get('source') == DELIVERY_TEST_SOURCE and payload['jpegBase64']
                        if not payload['isSynthetic'] or not (legacy_test or full_test):
                            raise ValueError('delivery test requires a synthetic sample')
                    if self.headers.get('Idempotency-Key', payload['id']) != payload['id']:
                        raise ValueError('idempotency key mismatch')
                    destinations = [test_destination] if test_destination else relay.destinations
                    inserted = relay.store.accept(payload, destinations, exact_destination=bool(test_destination))
                    self.reply(202, {'id': payload['id'], 'accepted': True, 'duplicate': not inserted})
                except (ValueError, UnicodeError):
                    self.reply(400, {'error': 'invalid report'})
                except DeliveryError as ex:
                    self.reply(503, {'error': 'storage quota'}, int(ex.retry_after))
                except (sqlite3.Error, OSError):
                    self.reply(503, {'error': 'storage unavailable'}, 60)
        return Handler

    @staticmethod
    def destination_configured(destination):
        if destination == 'discord':
            return bool(DISCORD_WEBHOOK.fullmatch(os.getenv('DREAMY_DISCORD_WEBHOOK', '')))
        return bool(TELEGRAM_TOKEN.fullmatch(os.getenv('TELEGRAM_BOT_TOKEN', '')) and
                    TELEGRAM_CHAT_ID.fullmatch(os.getenv('TELEGRAM_CHAT_ID', '')))

    def deliver(self, destination, report):
        if self.fake:
            return  # A durable delivery receipt is recorded by Store.one_delivery; no network.
        summary = report_summary(report)
        data = json.dumps({k: v for k, v in report.items() if k != 'jpegBase64'}, ensure_ascii=False, indent=2).encode()
        has_image = bool(report['jpegBase64'])
        if destination == 'telegram':
            if not self.destination_configured('telegram'):
                raise DeliveryError('Telegram token or chat ID is missing or invalid', permanent=True)
            token, chat = os.environ['TELEGRAM_BOT_TOKEN'], os.environ['TELEGRAM_CHAT_ID']
            url = 'https://api.telegram.org/bot' + token
            self.send_telegram(url, chat, 'sendDocument', telegram_caption(summary), 'document', 'report.json', 'application/json', data)
            if has_image:
                # Keep the report ID on both messages for correlation after an ambiguous retry.
                self.send_telegram(url, chat, 'sendDocument', f'<b>Original screenshot</b>\n<code>{report["id"]}</code>',
                                   'document', 'screenshot.jpg', 'image/jpeg', base64.b64decode(report['jpegBase64']))
        elif destination == 'discord':
            webhook = os.getenv('DREAMY_DISCORD_WEBHOOK', '')
            if not DISCORD_WEBHOOK.fullmatch(webhook):
                raise DeliveryError('Discord webhook is missing or invalid', permanent=True)
            message = {'embeds': [discord_embed(summary, has_image)], 'allowed_mentions': {'parse': []},
                       'attachments': [{'id': 0, 'filename': 'report.json'}]}
            files = [('files[0]', 'report.json', 'application/json', data)]
            if has_image:
                message['attachments'].append({'id': 1, 'filename': 'screenshot.jpg'})
                files.append(('files[1]', 'screenshot.jpg', 'image/jpeg', base64.b64decode(report['jpegBase64'])))
            boundary = 'dreamy-' + os.urandom(16).hex()
            parts = [('payload_json', None, 'application/json', json.dumps(message).encode()), *files]
            body = bytearray()
            for field, filename, content_type, content in parts:
                disposition = f'Content-Disposition: form-data; name="{field}"'
                if filename:
                    disposition += f'; filename="{filename}"'
                body.extend(f'--{boundary}\r\n{disposition}\r\nContent-Type: {content_type}\r\n\r\n'.encode())
                body.extend(content)
                body.extend(b'\r\n')
            body.extend(f'--{boundary}--\r\n'.encode())
            self.request(webhook + '?wait=true', bytes(body), 'multipart/form-data; boundary=' + boundary,
                         headers={'User-Agent': DISCORD_USER_AGENT})
        else:
            raise DeliveryError('unknown destination', permanent=True)

    def send_telegram(self, url, chat, method, caption, field, filename, mime, data):
        boundary = 'dreamy-' + os.urandom(16).hex()
        start = '--' + boundary + '\r\n'
        parts = [f'{start}Content-Disposition: form-data; name="chat_id"\r\n\r\n{chat}\r\n'.encode(),
                 f'{start}Content-Disposition: form-data; name="parse_mode"\r\n\r\nHTML\r\n'.encode(),
                 f'{start}Content-Disposition: form-data; name="caption"\r\n\r\n{caption}\r\n'.encode(),
                 f'{start}Content-Disposition: form-data; name="{field}"; filename="{filename}"\r\nContent-Type: {mime}\r\n\r\n'.encode(),
                 data, f'\r\n--{boundary}--\r\n'.encode()]
        self.request(url + '/' + method, b''.join(parts), 'multipart/form-data; boundary=' + boundary, telegram=True)

    @staticmethod
    def request(url, body, content_type, headers=None, telegram=False):
        request = urllib.request.Request(url, data=body, headers={'Content-Type': content_type, **(headers or {})}, method='POST')
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                if telegram:
                    try:
                        result = json.loads(response.read(65536))
                    except (ValueError, UnicodeError):
                        raise DeliveryError('invalid Telegram acknowledgement') from None
                    if not isinstance(result, dict) or result.get('ok') is not True:
                        code = result.get('error_code') if isinstance(result, dict) else None
                        if type(code) is int and 400 <= code <= 599:
                            retry = retry_seconds(result)
                            raise DeliveryError('Telegram API ' + str(code), retry_after=retry,
                                                permanent=400 <= code < 500 and code not in (408, 429))
                        raise DeliveryError('invalid Telegram acknowledgement')
                else:
                    response.read(1024)
        except urllib.error.HTTPError as ex:
            retry = 0
            if ex.code == 429:
                try:
                    retry = max(0, float(ex.headers.get('Retry-After', '0')))
                except (ValueError, TypeError):
                    pass
                try:
                    detail = json.loads(ex.read(4096))
                    retry = max(retry, retry_seconds(detail))
                except (ValueError, TypeError):
                    pass
            raise DeliveryError('destination HTTP ' + str(ex.code), retry_after=retry, permanent=400 <= ex.code < 500 and ex.code not in (408, 429)) from None



def retry_seconds(detail):
    try:
        seconds = float(detail.get('retry_after', detail.get('parameters', {}).get('retry_after', 0)))
        return seconds if 0 < seconds < float('inf') else 0
    except (AttributeError, TypeError, ValueError, OverflowError):
        return 0


def show_delivery_status(path, report_id):
    if not ID.fullmatch(report_id):
        raise SystemExit('Invalid report ID')
    try:
        with closing(sqlite3.connect(Path(path).resolve().as_uri() + '?mode=ro', uri=True)) as db:
            rows = db.execute('SELECT destination,state,attempts,next,error FROM delivery WHERE id=? ORDER BY destination',
                              (report_id,)).fetchall()
    except sqlite3.Error:
        raise SystemExit('Cannot read relay database') from None
    if not rows:
        raise SystemExit('Report ID not found in relay delivery history')
    print(json.dumps({'id': report_id, 'delivery': [
        dict(destination=destination, state=state, attempts=attempts,
             retryAfterSeconds=max(0, round(next_at - time.time())) if state == 'pending' else 0, error=error)
        for destination, state, attempts, next_at, error in rows
    ]}, indent=2))

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--delivery-status', metavar='REPORT_ID', help='Read the delivery receipt from the local relay database')
    parser.add_argument('--db', default=os.getenv('DREAMY_RELAY_DB', './data/reports.sqlite'), help='Relay SQLite database path')
    args = parser.parse_args()
    if args.delivery_status is not None:
        show_delivery_status(args.db, args.delivery_status)
        return
    fake = os.getenv('DREAMY_FAKE_DELIVERY', '1') == '1'
    key = os.getenv('DREAMY_INGEST_KEY', '')
    bind = os.getenv('DREAMY_BIND', '127.0.0.1')
    if bind not in ('127.0.0.1', 'localhost', '::1') and not key:
        raise SystemExit('Remote bind requires DREAMY_INGEST_KEY and a TLS reverse proxy')
    store = Store(args.db)
    relay = Relay(store, key, fake, os.getenv('DREAMY_DESTINATIONS', '').split(','))
    stop = threading.Event()
    def worker():
        last_cleanup = 0
        while not stop.is_set():
            try:
                if time.time() - last_cleanup > 60:
                    store.cleanup(); last_cleanup = time.time()
                store.one_delivery(relay.deliver)
            except Exception:
                pass  # Host supervisor owns observability; secrets/payloads are never printed.
            stop.wait(1)
    server = ThreadingHTTPServer((bind, int(os.getenv('DREAMY_PORT', '8765'))), relay.handler())
    threading.Thread(target=worker, daemon=True).start()
    try:
        server.serve_forever()
    finally:
        stop.set(); server.server_close()


if __name__ == '__main__':
    main()
