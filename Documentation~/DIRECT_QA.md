# Direct QA reporting

Direct QA sends from a controlled Android Development APK straight to Discord or Telegram over the internet. A single APK can contain both destinations. This mode uses Unity and the provider API, with no Python or tunnel installer.

## Editor setup checklist

1. Open **Window → Dreamy Debugger → Telegram & Reports**. Select a saved **Project configuration** with debugger mode **Development** or **Qa**, choose **Direct QA**, then enable sending.
2. Enter Discord or Telegram credentials for Unity Play Mode, or restore them from this computer. The separate **Include credentials in Android Development build** switch is off by default and resets when the session profiles change. Enable it only for an internal QA APK that should ship with these exact presets.
3. Use **Send Discord test message** and **Send Telegram test message** to verify each configured destination. The corresponding check updates from the provider acknowledgement. Changing a credential invalidates its previous test.
4. In **Build Profiles**, select Android and enable **Development Build**. In the app, unlock **Report** and select a preset, or open **Delivery tools** to set up Discord or Telegram and test the connection. Credentials entered on the device live for the current app session.

**Delivery tools** also provides the text test, a test report with JSON and an optional screenshot, and recovery for uncertain delivery.

For Unity Play Mode, restore or enter the destinations under **Telegram & Reports** and enable Direct QA sending before pressing Play. Open **Report** in Game view and select a configured destination. Restart Play Mode after changing destinations.

**Local Only** saves and exports reports. The Relay setup panel and Editor autostart are temporarily hidden. Existing Relay configurations are still supported. See [mobile relay setup](MOBILE_REPORT_SETUP.md).

Anyone holding the internal QA APK can extract its embedded credentials. Build staging is temporary. The stage and the Direct QA code are omitted for a non-Development Store Off APK. The preset is also unavailable in ReleaseDiagnostics. Reporting metadata, PlayerPrefs, receipts and diagnostics exclude the destination credentials.

## Direct QA and Relay

| | Direct QA | Relay |
|---|---|---|
| Delivery | Phone calls Discord or Telegram. Select one destination at a time. | Phone submits to an HTTPS relay. The relay can deliver the same report to both providers. |
| Credentials | Session-only input by default. Internal QA builds can explicitly embed presets. | Provider credentials live on the relay host. The APK uses the relay URL and a limited ingest key. |
| Delivery state | Delivered directly means the provider acknowledged the required files. | Sent to relay means the relay stored the report. Its destination receipts record delivery. |
| Dependencies | Android Development Build and an internet connection. | A reachable relay. For local setup, use the Editor buttons to install Python and, if using the optional HTTPS tunnel, cloudflared. |

## Delivery, retry and ambiguity

Discord shows a color-coded report card with a screenshot preview when available. Telegram uses a formatted caption on the report document and sends the original JPEG as a separate document with the same Report ID. Both summaries show the issue, device, scene, performance, and a short stack trace when available. Open `report.json` for the full captured stack and console excerpt.

The wire and exported report schemas, report IDs, quotas and retry schedule are unchanged. Each new Direct QA report stores a local hashed destination key and waits for that destination after a switch or restart. To send an older report or one saved before setup, select it and confirm **Assign selected report** in Delivery tools. Reports already bound to a destination can be exported if that destination is unavailable. **Delivered directly** means the provider acknowledged the required files. Relay acceptance means the relay persisted the report.

Discord uses a single multipart request with content, report.json and optional screenshot.jpg and requires the wait=true message acknowledgement. Telegram first acknowledges report.json, then the original JPEG document. Durable receipts store only a hashed destination identity, report ID and completed/in-flight steps. A retry after an acknowledged Telegram JSON step sends only the remaining image. Completed direct requests are suppressed across app restarts for the same destination/report ID. Receipts have an eight-day bounded retention, longer than the default seven-day outbox retention.

400/401/403/404/413 require correction. 429 respects Retry-After. A 5xx response without a delivery acknowledgement leaves the report in an uncertain state. Each file is limited to 1 MiB and screenshots to 512 KiB. Responses are capped at 64 KiB. TLS verification is enabled and redirects are disabled.

When the OS reports no network, reports remain pending without invoking the provider and resume when connectivity returns. If connectivity is lost during an actual request, or an acknowledgement is invalid, the provider may already have received it. Automatic resending stops. Select the failed report and use **Retry uncertain selected report** twice to explicitly accept possible duplication; completed Telegram steps are retained. Providers do not offer end-to-end exactly-once semantics, so this confirmation is required rather than claiming a lost acknowledgement can never duplicate.

Acknowledgement receipts contain no provider credentials. Direct API access grants the holder of the QA APK provider privileges. Production builds must use Local Only or Relay. The privileged adapters and destination UI compile only under UNITY_EDITOR or DEVELOPMENT_BUILD, with runtime Development/QA, unlock and selected-mode checks as well as build validation.
