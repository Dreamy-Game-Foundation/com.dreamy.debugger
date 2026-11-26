# Direct QA reporting

Direct QA sends from a controlled Android Development APK straight to Discord or Telegram over the internet. A single APK can contain both destinations. This mode uses Unity and the provider API, with no Python or tunnel installer.

## Editor setup checklist

1. Open **Window → Dreamy Debugger → Telegram & Reports**. Select a saved **Project configuration** with debugger mode **Development** or **Qa**, choose **Direct QA**, then enable sending.
2. Open Direct QA to restore saved Discord and Telegram destinations from this computer's credential store. **Restore saved Discord / Telegram** retries the lookup and fills missing destinations. For manual entry, enable **Include in Android Development build** and enter a Discord webhook or Telegram bot token and Chat ID. When both are configured, choose the default destination. Entries live in the current Unity Editor session unless you saved them using the local relay setup.
3. Use **Send Discord test message** and **Send Telegram test message** to verify each configured destination. The corresponding check updates from the provider acknowledgement. Changing a credential invalidates its previous test.
4. Open **Build Profiles**, select Android and enable **Development Build**. In the installed app, unlock the debugger, go to **Report**, and select **Discord** or **Telegram**. The check identifies the active destination. The APK loads the Editor default on every full app restart. Pending reports follow the active destination at send time. Reports already marked delivered remain in history.

**Delivery tools** provides the text connection test, the test report with JSON and optional screenshot, and explicit retry for uncertain delivery. Credentials are entered in Unity Editor. Android Report exposes the destination selector and does not expose credential inputs.

**Local Only** guides you through saving a report, optionally attaching a screenshot, and exporting it on the device. **Relay** guides you through entering the endpoint, sending a test, and enabling in-game delivery. **Use saved local Discord/Telegram** fills that relay's local URL and saved limited key. Use a reachable HTTPS endpoint for phone testing.

Anyone holding the internal QA APK can extract its embedded credentials. Build staging is temporary. The stage and the Direct QA code are omitted for a non-Development Store Off APK. The preset is also unavailable in ReleaseDiagnostics. Reporting metadata, PlayerPrefs, receipts and diagnostics exclude the destination credentials.

## Direct QA and Relay

| | Direct QA | Relay |
|---|---|---|
| Delivery | Phone calls Discord or Telegram. Select one destination at a time. | Phone submits to an HTTPS relay. The relay can deliver the same report to both providers. |
| Credentials | QA APK contains the selected test provider credentials. | Provider credentials live on the relay host. The APK uses the relay URL and a limited ingest key. |
| Delivery state | Delivered directly means the provider acknowledged the required files. | Sent to relay means the relay stored the report. Its destination receipts record delivery. |
| Dependencies | Android Development Build and an internet connection. | A reachable relay. For local setup, use the Editor buttons to install Python and, if using the optional HTTPS tunnel, cloudflared. |

## Delivery, retry and ambiguity

Discord shows a color-coded report card with a screenshot preview when available. Telegram uses a formatted caption on the report document and sends the original JPEG as a separate document with the same Report ID. Both summaries show the issue, device, scene, performance, and a short stack trace when available. Open `report.json` for the full captured stack and console excerpt.

The existing outbox schema, report IDs, quota and retry schedule remain unchanged. Successful Direct QA submissions use the existing accepted state; the UI labels them **Delivered directly** rather than **Sent to relay**. This means the provider acknowledged all required files, not that a human read them. Relay mode still means only relay acceptance.

Discord uses a single multipart request with content, report.json and optional screenshot.jpg and requires the wait=true message acknowledgement. Telegram first acknowledges report.json, then the original JPEG document. Durable receipts store only a hashed destination identity, report ID and completed/in-flight steps. A retry after an acknowledged Telegram JSON step sends only the remaining image. Completed direct requests are suppressed across app restarts for the same destination/report ID. Receipts have an eight-day bounded retention, longer than the default seven-day outbox retention.

400/401/403/404/413 are permanent failures until corrected; 429 respects header/body Retry-After, and 5xx use existing bounded retries. Each file is limited to 1MiB and screenshots retain the stricter existing 512KiB cap. Responses are capped at 64KiB and requests, including response reading, have a15-second deadline. TLS validation remains enabled and redirects are disabled.

When the OS reports no network, reports remain pending without invoking the provider and resume when connectivity returns. If connectivity is lost during an actual request, or an acknowledgement is invalid, the provider may already have received it. Automatic resending stops. Select the failed report and use **Retry uncertain selected report** twice to explicitly accept possible duplication; completed Telegram steps are retained. Providers do not offer end-to-end exactly-once semantics, so this confirmation is required rather than claiming a lost acknowledgement can never duplicate.

Acknowledgement receipts contain no provider credentials. Direct API access grants the holder of the QA APK provider privileges. Production builds must use Local Only or Relay. The privileged adapters and destination UI compile only under UNITY_EDITOR or DEVELOPMENT_BUILD, with runtime Development/QA, unlock and selected-mode checks as well as build validation.
