# Capture, persistence and delivery

## Using the Report tab

Unlock Dreamy Debugger and open **Report**. Describe the issue, optionally attach a screenshot and inspect its preview, then select **Save report**. **Clear** discards the draft. **Remove** removes an attached image. The report includes recent console output, device/build context and available performance metrics.

Use **Previous / Next** to browse saved reports. **Export** writes `report.json` and, when present, `screenshot.jpg` into an ID-named folder. Desktop opens the folder. Android 10+ writes a ZIP to Downloads/DreamyDebugger and opens the system share sheet. **Delete** removes the local copy after confirmation. **Send** is available for pending reports when the project has enabled a relay. **Relay accepted** means the server persisted the report. Delivery to Discord or Telegram has its own status on the relay.

## Host API

- `TrySetAutoReporting(enabled)` controls automatic Unity errors, exceptions and asserts in the unlocked Development/QA session. The project can disable the control with `AllowAutoReportingControls`. The session starts from `AutoReportErrors`.
- `QueueReportAsync(description, screenshot: true)` captures and queues a report using project policy. Use `ListReportsAsync()`, `ExportReportAsync(id)` and `DeleteReportAsync(id)` within the unlocked debugger session.
- `IsSensitiveReportScreen` supplies a game-owned veto for report screenshots. Return true on screens containing sensitive data.
- `SetReportUploadPermission(false)` stops future sends and cancels current client work. A request already accepted by the relay remains subject to the studio's retention policy. Use `DeleteReportAsync(id)` to remove stored reports. Keep upload and screenshot policies off until the game enables them intentionally.

## Client flow

1. Threaded Unity log ingress copies bounded strings into a fixed queue; it performs no screenshot, IO, regex or network request.
2. The main thread groups pending errors by message, severity and representative frame before applying the session cap and cooldown. The report preserves the first stack trace, occurrence count, sampling timestamp and available performance metrics. Optional capture waits for a completed game frame.
3. A GPU copy/downscale feeds AsyncGPUReadback; thread-safe EncodeArrayToJPG encodes raw bytes on one worker. There is one capture in flight, a 24 MiB raw capture budget, a 960 px maximum long edge and a 512 KiB encoded attachment cap. A frame already slower than 50 ms, unsupported GPU, timeout, low memory, sensitive screen or app inactivity produces a text-only report.
4. DiskOutbox serializes filesystem work through a worker gate. Versioned, bounded binary bundles use a flushed temp file and rename/replace. Startup deletes interrupted temporary files and removes corrupt bundles without preventing healthy reports from loading.
5. The host pump attempts one report per idle tick. The runtime keeps relay acknowledgements in local history outside the pending queue. Retryable responses use bounded exponential backoff, jitter and Retry-After. Permanent rejection or exhausted attempts becomes a bounded dead letter. The default shared quota is 10 entries, 20 MiB and seven days. Files explicitly exported by a user are managed separately.
6. The relay acknowledges only after SQLite commit. It fans out asynchronously and records each destination's state.

Automatic capture is opt-in. The sensitive-screen provider is rechecked before GPU submission, after readback/encoding and before persistence. Disable/cancel before readback releases the capture slot. Submitted GPU work retains the slot until completion even after timeout. Closing the runtime cancels outstanding work and releases its visibility leases. When the game can no longer render, use the retained text or previous-session evidence supplied by the host.

## Relay

### Discord test from the Editor

Start the current `server.py` in Discord live mode and put the webhook URL in `DREAMY_DISCORD_WEBHOOK` on the relay. In **Window → Dreamy Debugger → Telegram & Reports**, select a configuration and set **Relay URL** to the relay's `/reports` URL. Enter the matching **Limited Client Key** if the relay requires one. Select **Send Discord Test Report** in Edit Mode or Play Mode. The button works with runtime uploads disabled.

The Editor sends a single synthetic report with a JSON attachment using `/reports/discord-test`. It leaves the runtime queue untouched and collects no game logs or screenshots. For a remote relay, the reverse proxy also needs to forward that path. **Relay accepted** means the report was stored. Verify delivery in the chosen Discord channel using the report ID shown under the button.


From `Tools~/relay` run:

```bash
DREAMY_FAKE_DELIVERY=1 DREAMY_PORT=8765 python3 server.py
python3 -m unittest discover -v
```

The fake mode accepts and persists reports and marks fake deliveries without external requests. The client endpoint is `http://127.0.0.1:8765/reports` for local testing; remote endpoints require HTTPS. Set `DREAMY_INGEST_KEY` at the relay and the matching limited key at the client. The ingestion key limits access to the relay; it is not a client secret or a substitute for per-build policy.

For live deployment use a TLS reverse proxy, persistent volume for `DREAMY_RELAY_DB`, a supervisor and explicit destination configuration. Environment variables stay on the server:

| Destination | Server variables |
| --- | --- |
| Discord | `DREAMY_DESTINATIONS=discord`, `DREAMY_DISCORD_WEBHOOK` |
| Telegram | `DREAMY_DESTINATIONS=telegram`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID` |


The relay keeps destination attempts, bounded retry times and dead state. It enforces payload/storage/rate limits and seven-day retention. SQLite deduplicates the same report ID and payload. An ambiguous destination acknowledgement may cause a retry to post a duplicate. Use the report ID to identify matching messages.

## Evidence

Read `Documentation~/TRACKING.md` and the parent sandbox receipts. Fake scenarios and localhost HTTP prove protocol/state handling, not real account delivery or mobile frame-time cost.

Primary API sources consulted:

- [Unity AsyncGPUReadback](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadback.html).
- [Unity EncodeArrayToJPG](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ImageConversion.EncodeArrayToJPG.html).
- [Telegram Bot API](https://core.telegram.org/bots/api#senddocument).
- [Discord Webhook resource](https://docs.discord.com/developers/resources/webhook).

## Production deployment gate

A mobile build needs an externally reachable HTTPS relay, server-only bot credentials, persistent storage and destination monitoring. Localhost is for Editor/local tests. Production screenshot disclosure/consent, withdrawal/deletion, personal-data masking, remote configuration and weak-device budgets are not certified by this package. Keep automatic screenshots disabled until the host integrates and verifies its policy. Prefer text-only diagnostics when rendering, memory or policy conditions are uncertain. Fatal native crashes may only supply previous persisted context at the next launch.
