# Capture, persistence and delivery

## Using the Report tab

Use **Test exception** to write a test exception with a stack trace to Console. Enable **Auto report** first to exercise automatic capture. The test follows the same cooldown, screenshot and upload settings as other exceptions. Use **Refresh** to check for it under Saved reports after capture.

Unlock Dreamy Debugger and open **Report**. Describe the issue, optionally attach a screenshot and inspect its preview, then select **Send report**. This saves a local copy and attempts delivery with one tap. If the relay is unreachable, the copy stays on the device for retry. Projects without upload permission show **Save report**. **Clear** discards the draft. **Remove** removes an attached image. The report includes recent console output, device/build context and available performance metrics.

Use **Previous / Next** to browse saved reports. **Export** writes `report.json` and, when present, `screenshot.jpg` into an ID-named folder. Desktop opens the folder. Android 10+ writes a ZIP to Downloads/DreamyDebugger and opens the system share sheet. **Delete** removes the local copy after confirmation. Delivery shows **Pending**, **Sending**, **Sent to relay** or **Failed**. Use **Retry** to resend a failed report with its original ID. Pending reports respect their displayed backoff and retry automatically when the app can upload. **Sent to relay** means the server persisted the report. Check the relay receipt for delivery to Discord or Telegram.

In **Console**, open a log written with a live GameObject or Component context (for example, `Debug.LogError("Sample", this)`) and tap **Inspect** to select that GameObject in Hierarchy. The action appears while scene inspection is available and the object still exists.

## Host API

- `TrySetAutoReporting(enabled)` controls automatic Unity errors, exceptions and asserts in the unlocked Development/QA session. The project can disable the control with `AllowAutoReportingControls`. The session starts from `AutoReportErrors`.
- `QueueReportAsync(description, screenshot: true)` captures and queues a report using project policy. Use `ListReportsAsync()`, `ExportReportAsync(id)` and `DeleteReportAsync(id)` within the unlocked debugger session.
- `IsSensitiveReportScreen` supplies a game-owned veto for report screenshots. Return true on screens containing sensitive data.
- `SetReportUploadPermission(false)` stops future sends and cancels current client work. A request already accepted by the relay remains subject to the studio's retention policy. Use `DeleteReportAsync(id)` to remove stored reports. Keep upload and screenshot policies off until the game enables them intentionally.

## Client flow

For controlled Development APKs that should send without a relay, see [Direct QA reporting](DIRECT_QA.md). Provider credentials are entered after installation and retained only for that session.

### Transport diagnostics

**Direct QA** sends straight to Discord or Telegram from Unity Play Mode or a controlled Android Development APK. Android users enter session credentials in **Report > Delivery tools**. The Editor can also explicitly opt into embedding the selected presets for an internal QA build. The rest of this section describes Relay delivery. [Direct QA setup](DIRECT_QA.md)

`RelayResult` preserves `HttpStatusCode` and a credential-free `Failure` category. `DebuggerRuntime.LastUploadResult` exposes the latest completed transport result in the current session; it is not per-report delivery history. Immediate manual upload failures and Editor tests display its safe summary. HTTP 401/403 refer to the relay's ingest authorization, 400/413/415/422 to request/payload rejection, 429 to rate limiting, 408/504 to timeout and other 5xx to server failure. Network failures without an HTTP response have status 0. Caller cancellation stays cancellation. No response bodies, request headers, exception messages or provider URLs are included in summaries.

Mobile localhost rejection happens before transport construction: there is no HTTP status to inspect. A report saved under this configuration stays local. Relay acceptance proves ingestion, while Discord/Telegram delivery must be checked separately in the server receipt. An ordinary relay JSON body is not a provider webhook or Bot API payload.

1. Threaded Unity log ingress copies bounded strings into a fixed queue; it performs no screenshot, IO, regex or network request.
2. The main thread groups pending errors by normalized message, severity and the first relevant frame. Auto reports share the configured cooldown and a limit of 10 per session. The JSON includes the first error stack (up to 32,768 characters) and up to 100 recent console entries with timestamps, severity, counts and stack traces, within a separate 256 KiB context budget. It keeps the newest entries when it reaches the budget and redacts recognized credentials and personal identifiers. Optional capture waits for a completed game frame.
3. A GPU copy/downscale feeds AsyncGPUReadback. Reports default to a 1440 px maximum long edge and scale down to fit a 24 MiB capture-buffer budget. The encoder tries JPEG quality 92 first and reduces quality only when needed to stay within the 512 KiB attachment cap. A frame already slower than 50 ms, unsupported GPU, timeout, low memory, sensitive screen or app inactivity produces a text-only report.
4. DiskOutbox serializes filesystem work through a worker gate. Versioned, bounded binary bundles use a flushed temp file and rename/replace. Startup deletes interrupted temporary files and removes corrupt bundles without preventing healthy reports from loading.
5. The host pump attempts one report per idle tick. The runtime keeps relay acknowledgements in local history outside the pending queue. Retryable responses use bounded exponential backoff, jitter and Retry-After. Permanent rejection or exhausted attempts becomes a bounded dead letter. The default shared quota is 10 entries, 20 MiB and seven days. Files explicitly exported by a user are managed separately.
6. The relay acknowledges only after SQLite commit. It fans out asynchronously and records each destination's state.

Automatic capture is opt-in. The sensitive-screen provider is rechecked before GPU submission, after readback/encoding and before persistence. Disable/cancel before readback releases the capture slot. Submitted GPU work retains the slot until completion even after timeout. Closing the runtime cancels outstanding work and releases its visibility leases. When the game can no longer render, use the retained text or previous-session evidence supplied by the host.

## Relay

### Read and track reports in chat

- **Discord** shows a report card grouped by environment, performance and error, with up to three stack frames and a screenshot preview when attached. **Telegram** shows device, OS, FPS and other available details in the document caption. It attaches the original JPEG as a file with the same Report ID. Both attach an indented `report.json`. Read `details.stackTrace` for the captured error and `context` for recent console messages and their stacks.
- Search for **Report ID** to find an individual report and its relay receipt. When an automatic error includes the app ID and a usable stack frame, search for its **Issue key** to find related reports across builds. The key uses the app, error type and top frame, with source line numbers stripped. **Occurrences** counts repeats captured in that report.
- Update and restart the relay to apply the layout to new deliveries, including queued reports. The current game build can keep using its existing relay URL.

### Connect a project once

Follow the [first-time mobile setup checklist](MOBILE_REPORT_SETUP.md) before building an Android/iOS QA app. It covers prerequisites, credential prompts, limited keys, HTTPS, destination tests, restart and troubleshooting.

1. Open **Window → Dreamy Debugger → Telegram & Reports** and select your project's configuration. Enter the relay's HTTPS **Relay URL** ending in `/reports` and its **Limited Client Key** if required.
2. Select **Telegram** or **Discord** under **Test destination** and click **Send full test report**. You will receive a QA fixture with a 1440 × 810 screenshot, three stack frames, a sample Android device, 58.7 FPS, memory and tracking IDs. Confirm the Report ID matches Unity. The test works in Edit Mode and with in-game sending disabled.
3. Enable **Enable sending in game**. Restart Play Mode or build the QA app using this configuration. Testers can now send from **Report** without configuring a relay on their devices.

The Relay setup panel and Editor autostart are temporarily disabled. Existing Relay configurations continue to use their saved endpoint. Android and iOS builds need a reachable HTTPS relay operated by the project or a hosting provider. Keep bot tokens and webhook URLs on that relay. The Debugger build checks reject recognizable provider credentials in the relay URL or client key fields of packaged Debugger configurations. The reference relay and deployment instructions ship with the package. [Relay setup guide](../Tools~/relay/README.md)

### Telegram test from the Editor

1. Send **/start** to your bot in the chat you want to use. In **Window → Dreamy Debugger → Telegram & Reports**, expand **Local Discord / Telegram relay setup**, select **Telegram**, and click **Start Telegram Relay in Terminal**. Unity opens a terminal and runs the setup script with a hidden BotFather token prompt. Enter the destination chat ID when asked, or save it in the optional Editor settings. If you need the ID, use **Find Chat ID in Terminal** first.
2. When the launcher prints **Relay ready in background**, copy its **Editor Relay URL** into the selected configuration's **Relay URL**. Enter the matching **Limited Client Key** if configured, then click **Send Telegram Test Report**. The button works with runtime uploads disabled.
3. Confirm the report ID from the Editor appears with `report.json` in Telegram. On the relay machine, the delivery state is also available through `python3 server.py --db ./data/telegram-local.sqlite --delivery-status REPORT_ID` when using the launcher's default database.

The Editor posts a synthetic sample to `/reports/telegram-test` using diagnostics and an image shipped with the package. Enable runtime uploads and restart Play Mode to test real data from the **Report** tab. Reports carry the same Report ID on the JSON and JPEG files. If a remote response is lost or the JPEG fails after the JSON arrives, a retry can repeat the JSON. The relay records successful delivery after Telegram acknowledges both required sends. [Relay setup guide](../Tools~/relay/README.md)

### Remember the token and start the local Telegram relay automatically

In **Telegram & Reports**, enable **Remember bot token on this computer** before starting setup. The first run creates a project-local Python environment and installs the credential helper. Enter the bot token and Chat ID at the terminal prompts. After validation, the token is saved in Windows Credential Locker, macOS Keychain or Linux Secret Service. Unity preferences contain the toggles and port. The remembered chat ID is local under `Library/DreamyDebugger/TelegramRelay` unless a Chat ID is set in the optional Editor settings asset.

Enable **Start saved relay when Unity opens** to reuse or start the relay during later interactive Editor sessions. **Start Saved Telegram Relay** runs the same check now. **Send Telegram Test Report** sends a sample directly to the local Telegram port. **Copy Saved Relay URL** copies the address for the selected configuration when you want to use this relay from the game. If the port is occupied, stop the old relay or change **Saved relay port** under **Manual commands and saved settings**, then update Relay URL.

**Forget Saved Bot Token** deletes the credential and turns off both preferences. A relay already running keeps its current token in memory until you stop it. Stop the owned process listed in `Library/DreamyDebugger/TelegramRelay/telegram-local.pid` when changing or revoking its live credentials. See [relay dependencies and troubleshooting](../Tools~/relay/README.md#remembered-local-relay).

### Remember a Discord webhook and start the local relay automatically

In **Window → Dreamy Debugger → Telegram & Reports → Local relay setup**, select **Discord**. Enable **Remember webhook on this computer** and **Start saved relay when Unity opens**. Click **Start Discord Relay in Terminal** and paste the webhook at the hidden prompt. The first remembered setup installs the same credential helper used for Telegram. The launcher verifies the incoming webhook with a read-only Discord request. It sends a message only after you use the test button or submit a report.

Use **Copy Saved Relay URL** once and paste it into the selected configuration's **Relay URL**. The Discord default is `http://127.0.0.1:8766/reports`. Telegram defaults to port 8765. Click **Send Discord Test Report** and match the report ID in the channel. This button uses the local Discord port. The main **Send full test report** button starts from the selected configuration. If its URL identifies the managed local Telegram relay, testing Discord switches to the running managed local Discord relay for that test. The game uses the single URL saved in its configuration. To send regular reports to both destinations, configure both on that relay.

**Forget Saved Webhook** deletes the Discord credential and disables Discord autostart. A relay already running keeps its current webhook until stopped. Diagnose local Discord delivery using the matching SQLite file under `Library/DreamyDebugger/DiscordRelay`. [Discord webhook API](https://docs.discord.com/developers/resources/webhook)

### Discord test from the Editor

For a manually operated relay, start the current `server.py` in Discord live mode and put the webhook URL in `DREAMY_DISCORD_WEBHOOK` on the relay. In **Window → Dreamy Debugger → Telegram & Reports**, select a configuration and set **Relay URL** to the relay's `/reports` URL. Enter the matching **Limited Client Key** if the relay requires one. Select **Discord** under **Test destination** and click **Send full test report** in Edit Mode or Play Mode. The button works with in-game sending disabled.

The Editor sends the synthetic diagnostics and screenshot fixture using `/reports/discord-test`. For a remote relay, the reverse proxy also needs to forward that path. **Relay accepted** means the report was stored. Verify delivery in the chosen Discord channel using the Report ID shown under the button.


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
