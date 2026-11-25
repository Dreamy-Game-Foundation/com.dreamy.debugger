# Direct QA reporting

Direct QA sends from a controlled Development APK to one Discord webhook or Telegram bot/chat without an Editor, Python process or relay. Relay remains the default enum value for existing assets; UploadsEnabled continues to control sending. Local Only always retains reports without a transport.

## Setup once per app session

1. Select **Direct QA** in Reporting mode, enable sending and screenshots as desired, and build a **Development** player with debugger mode Development or Qa. A non-Development build or ReleaseDiagnostics configuration cannot select Direct QA.
2. On the device, unlock the debugger, open **Report > Direct QA setup**, select Discord or Telegram, enter the credential (masked) and Telegram Chat ID if needed, and tap **Apply session**. The credential fields clear after apply. No provider credentials belong in configuration assets, Relay URL or Limited Client Key.
3. Tap **Test Connection** for a small text message. Confirm it in the destination. Then **Send Test Report** queues a synthetic JSON report with an optional captured screenshot; confirm its Report ID and attachments.
4. Use the normal Report form for manual reports and Auto report for captured Unity errors. Keep the app open with network access for sending. One direct destination is active per session; switching provider forgets the previous session transport, and acknowledged receipts remain separate per destination.
5. **Forget** cancels further sends and clears the session transport. App exit loses credentials. Re-enter them after relaunch to send pending reports. Secrets are managed strings in process memory, not encrypted long-term storage; use restricted disposable QA destinations/devices.

The setup UI inherits the Report tab's safe area/scroll owner and uses masked native uGUI InputFields. Screenshots are vetoed while provisioning is open; the test report closes provisioning before capture. Provider secrets are never written by the package to assets, PlayerPrefs, report JSON, receipts, logs or URL diagnostics.

## Delivery, retry and ambiguity

The existing outbox schema, report IDs, quota and retry schedule remain unchanged. Successful Direct QA submissions use the existing accepted state; the UI labels them **Delivered directly** rather than **Sent to relay**. This means the provider acknowledged all required files, not that a human read them. Relay mode still means only relay acceptance.

Discord uses a single multipart request with content, report.json and optional screenshot.jpg and requires the wait=true message acknowledgement. Telegram first acknowledges report.json, then the original JPEG document. Durable receipts store only a hashed destination identity, report ID and completed/in-flight steps. A retry after an acknowledged Telegram JSON step sends only the remaining image. Completed direct requests are suppressed across app restarts for the same destination/report ID. Receipts have an eight-day bounded retention, longer than the default seven-day outbox retention.

400/401/403/404/413 are permanent failures until corrected; 429 respects header/body Retry-After, and 5xx use existing bounded retries. Each file is limited to 1MiB and screenshots retain the stricter existing 512KiB cap. Responses are capped at 64KiB and requests, including response reading, have a15-second deadline. TLS validation remains enabled and redirects are disabled.

When the OS reports no network, reports remain pending without invoking the provider and resume when connectivity returns. If connectivity is lost during an actual request, or an acknowledgement is invalid, the provider may already have received it. Automatic resending stops. Select the failed report and use **Retry uncertain selected report** twice to explicitly accept possible duplication; completed Telegram steps are retained. Providers do not offer end-to-end exactly-once semantics, so this confirmation is required rather than claiming a lost acknowledgement can never duplicate.

Acknowledgement receipts contain no provider credentials. Credentials entered into the QA device must be trusted; direct API access grants the holder provider privileges. Production builds must use Local Only or Relay. The privileged adapters and credential UI compile only under UNITY_EDITOR or DEVELOPMENT_BUILD, with runtime Development/QA, unlock and selected-mode checks as well as build validation.
