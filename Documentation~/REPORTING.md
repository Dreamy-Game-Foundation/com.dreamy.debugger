# Capture, persistence and delivery

## Client flow

1. Threaded Unity log ingress copies bounded strings into a fixed queue; it performs no screenshot, IO, regex or network request.
2. Main-thread policy rate-limits and snapshots cached context. Optional capture waits for a completed game frame.
3. A GPU copy/downscale feeds AsyncGPUReadback; thread-safe EncodeArrayToJPG encodes raw bytes on one worker. There is one capture in flight, a 24 MiB raw capture budget, a 960 px maximum long edge and a 512 KiB encoded attachment cap. A frame already slower than 50 ms, unsupported GPU, timeout, low memory, sensitive screen or app inactivity produces a text-only report.
4. DiskOutbox serializes filesystem work through a worker gate. Versioned, bounded binary bundles use a flushed temp file and rename/replace. Startup deletes interrupted temporary files and removes corrupt bundles without preventing healthy reports from loading.
5. The host pump attempts one report per idle tick. Accepted reports are removed locally; retryable responses use bounded exponential backoff/jitter and Retry-After. Permanent rejection or exhausted attempts becomes a bounded dead letter. Default storage is 10 entries, 20 MiB and seven days.
6. The relay acknowledges only after SQLite commit. It fans out asynchronously and records each destination's state.

Automatic capture is opt-in. `CaptureCoordinator.SensitiveScreen` allows a host to suppress an entire sensitive screen before capture. Region masking and continuous pre-crash screenshot recording are not enabled by default. No screenshot is promised after native process termination, ANR, GPU failure or OOM.

## Relay

From `Tools~/relay` run:

```bash
DREAMY_FAKE_DELIVERY=1 DREAMY_PORT=8765 python3 server.py
python3 -m unittest discover -v
```

The fake mode accepts and persists reports and marks fake deliveries without external requests. The client endpoint is `http://127.0.0.1:8765/reports` for local testing; remote endpoints require HTTPS. Set `DREAMY_INGEST_KEY` at the relay and the matching limited key at the client. The ingestion key limits access to the relay; it is not a client secret or a substitute for per-build policy.

For live deployment use a TLS reverse proxy, persistent volume for `DREAMY_RELAY_DB`, a supervisor and explicit destination configuration. Environment variables stay on the server:

| Destination | Server variables |
| --- | --- |
| Telegram | `DREAMY_DESTINATIONS=telegram`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID` |
| Gmail API | `DREAMY_DESTINATIONS=gmail`, `MAIL_FROM`, `MAIL_TO`, `GMAIL_ACCESS_TOKEN` |
| SMTP | `DREAMY_DESTINATIONS=smtp`, `MAIL_FROM`, `MAIL_TO`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD` |

Only set `DREAMY_FAKE_DELIVERY=0` after configuring a test destination. Gmail API access tokens need server-side refresh/rotation by the deployment owner; no OAuth refresh credential or mailbox account is supplied. SMTP recipients can be Gmail addresses without using Gmail API. Multiple destinations are comma-separated. Telegram sends a diagnostic JSON document and an optional photo. Email includes context and optional attachments.

The relay keeps destination attempts, bounded retry times and dead state. It enforces payload/storage/rate limits and seven-day retention. SQLite acceptance deduplicates report IDs. Remote Telegram/mail delivery is **at least once**: if a destination accepts a message but its response is lost, a retry may duplicate that destination message. No exactly-once guarantee is claimed.

## Evidence

Read `Documentation~/TRACKING.md` and the parent sandbox receipts. Fake scenarios and localhost HTTP prove protocol/state handling, not real account delivery or mobile frame-time cost.

Primary API sources consulted:

- [Unity AsyncGPUReadback](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.html).
- [Unity EncodeArrayToJPG](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ImageConversion.EncodeArrayToJPG.html).
- [Telegram Bot API](https://core.telegram.org/bots/api#senddocument).
- [Gmail sending](https://developers.google.com/workspace/gmail/api/guides/sending).
