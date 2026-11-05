"""Dreamy diagnostics relay. Durable ingestion, bounded storage, destination retries.
Default fake delivery performs no external requests. Run behind TLS for remote use.
"""
import base64
from contextlib import contextmanager
import hmac
import json
import os
import re
import smtplib
import sqlite3
import threading
import time
import urllib.error
import urllib.request
from email.message import EmailMessage
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

MAX_BODY = 1500000
ID = re.compile(r'^[0-9a-f]{32}$')


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

    def accept(self, payload, destinations):
        with self.lock, self.connect() as db:
            if db.execute('SELECT 1 FROM reports WHERE id=?', (payload['id'],)).fetchone():
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
            # Never store tokens, URLs containing tokens, SMTP credentials, or response bodies.
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
    return {key: body.get(key, default) for key, default in [('schema', 1), ('id', ''), ('message', ''), ('buildId', ''), ('context', ''), ('isSynthetic', False), ('createdUtc', ''), ('screenshotReason', ''), ('screenshotUtc', ''), ('jpegBase64', '')]}


class Relay:
    def __init__(self, store, key='', fake=True, destinations=None, per_minute=30):
        self.store, self.key, self.fake = store, key, fake
        self.destinations = ['fake'] if fake else list(destinations or [])
        self.per_minute, self.hits, self.lock = per_minute, [], threading.Lock()
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
                self.reply(200 if self.path == '/health' else 404, {'service': 'dreamy.debugger', 'fakeDelivery': relay.fake})

            def do_POST(self):
                self.connection.settimeout(15)
                if self.path != '/reports':
                    self.reply(404, {'error': 'not found'}); return
                if relay.key and not hmac.compare_digest(self.headers.get('X-Dreamy-Key', ''), relay.key):
                    self.reply(401, {'error': 'unauthorized'}); return
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
                    if self.headers.get('Idempotency-Key', payload['id']) != payload['id']:
                        raise ValueError('idempotency key mismatch')
                    inserted = relay.store.accept(payload, relay.destinations)
                    self.reply(202, {'id': payload['id'], 'accepted': True, 'duplicate': not inserted})
                except (ValueError, UnicodeError):
                    self.reply(400, {'error': 'invalid report'})
                except DeliveryError as ex:
                    self.reply(503, {'error': 'storage quota'}, int(ex.retry_after))
                except (sqlite3.Error, OSError):
                    self.reply(503, {'error': 'storage unavailable'}, 60)
        return Handler

    def deliver(self, destination, report):
        if self.fake:
            return  # A durable delivery receipt is recorded by Store.one_delivery; no network.
        summary = ('[FAKE] ' if report['isSynthetic'] else '') + 'Dreamy report ' + report['id'] + '\n' + report['message'][:1000] + '\nBuild: ' + report['buildId']
        if destination == 'telegram':
            token, chat = os.environ['TELEGRAM_BOT_TOKEN'], os.environ['TELEGRAM_CHAT_ID']
            # Full diagnostics are a document; text alone is insufficient for stack traces/context.
            data = json.dumps({k: v for k, v in report.items() if k != 'jpegBase64'}, ensure_ascii=False).encode()
            boundary = 'dreamy-' + report['id']
            parts = [('--' + boundary + '\r\nContent-Disposition: form-data; name="chat_id"\r\n\r\n' + chat + '\r\n').encode(),
                     ('--' + boundary + '\r\nContent-Disposition: form-data; name="caption"\r\n\r\n' + summary[:900] + '\r\n').encode(),
                     ('--' + boundary + '\r\nContent-Disposition: form-data; name="document"; filename="report.json"\r\nContent-Type: application/json\r\n\r\n').encode() + data + b'\r\n',
                     ('--' + boundary + '--\r\n').encode()]
            self.request('https://api.telegram.org/bot' + token + '/sendDocument', b''.join(parts), 'multipart/form-data; boundary=' + boundary)
            if report['jpegBase64']:
                # Separate attachment may duplicate after an ambiguous remote acknowledgement; delivery is at-least-once.
                image = base64.b64decode(report['jpegBase64'])
                parts = [('--' + boundary + '\r\nContent-Disposition: form-data; name="chat_id"\r\n\r\n' + chat + '\r\n').encode(),
                         ('--' + boundary + '\r\nContent-Disposition: form-data; name="photo"; filename="screenshot.jpg"\r\nContent-Type: image/jpeg\r\n\r\n').encode() + image + b'\r\n', ('--' + boundary + '--\r\n').encode()]
                self.request('https://api.telegram.org/bot' + token + '/sendPhoto', b''.join(parts), 'multipart/form-data; boundary=' + boundary)
        elif destination in ('gmail', 'smtp'):
            message = EmailMessage(); message['Subject'] = summary.splitlines()[0]; message['From'] = os.environ['MAIL_FROM']; message['To'] = os.environ['MAIL_TO']
            message['Message-ID'] = '<' + report['id'] + '@dreamy-debugger.local>'
            message.set_content(summary + '\n\n' + report['context'])
            message.add_attachment(json.dumps({k: v for k, v in report.items() if k != 'jpegBase64'}).encode(), maintype='application', subtype='json', filename='report.json')
            if report['jpegBase64']:
                message.add_attachment(base64.b64decode(report['jpegBase64']), maintype='image', subtype='jpeg', filename='screenshot.jpg')
            if destination == 'gmail':
                self.request('https://gmail.googleapis.com/gmail/v1/users/me/messages/send', json.dumps({'raw': base64.urlsafe_b64encode(message.as_bytes()).decode()}).encode(), 'application/json', {'Authorization': 'Bearer ' + os.environ['GMAIL_ACCESS_TOKEN']})
            else:
                import ssl
                with smtplib.SMTP(os.environ['SMTP_HOST'], int(os.getenv('SMTP_PORT', '587')), timeout=15) as smtp:
                    smtp.starttls(context=ssl.create_default_context()); smtp.login(os.environ['SMTP_USER'], os.environ['SMTP_PASSWORD']); smtp.send_message(message)
        else:
            raise DeliveryError('unknown destination', permanent=True)

    @staticmethod
    def request(url, body, content_type, headers=None):
        request = urllib.request.Request(url, data=body, headers={'Content-Type': content_type, **(headers or {})}, method='POST')
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                response.read(1024)
        except urllib.error.HTTPError as ex:
            retry = int(ex.headers.get('Retry-After', '30'))
            if ex.code == 429:
                try:
                    retry = max(retry, int(json.loads(ex.read(4096)).get('parameters', {}).get('retry_after', 0)))
                except (ValueError, TypeError):
                    pass
            raise DeliveryError('destination HTTP ' + str(ex.code), retry_after=retry, permanent=400 <= ex.code < 500 and ex.code not in (408, 429)) from None


def main():
    fake = os.getenv('DREAMY_FAKE_DELIVERY', '1') == '1'
    key = os.getenv('DREAMY_INGEST_KEY', '')
    bind = os.getenv('DREAMY_BIND', '127.0.0.1')
    if bind not in ('127.0.0.1', 'localhost', '::1') and not key:
        raise SystemExit('Remote bind requires DREAMY_INGEST_KEY and a TLS reverse proxy')
    store = Store(os.getenv('DREAMY_RELAY_DB', './data/reports.sqlite'))
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
    threading.Thread(target=worker, daemon=True).start()
    server = ThreadingHTTPServer((bind, int(os.getenv('DREAMY_PORT', '8765'))), relay.handler())
    try:
        server.serve_forever()
    finally:
        stop.set(); server.server_close()


if __name__ == '__main__':
    main()
