# Mobile report setup

For an Android Development APK that sends straight to Discord or Telegram, use [Direct QA](DIRECT_QA.md). **Local Only** saves and exports reports from the device.

The Relay setup panel and Editor autostart are temporarily disabled. Existing configured Relay builds can still use their endpoint. The steps below describe the Relay setup UI when enabled.

## Set up in Unity

1. Open **Window > Dreamy Debugger > Telegram & Reports**, choose **Relay**, and click **Set up relay**.
2. Choose a destination and connection type.

   | Connection | What to enter |
   | --- | --- |
   | **Existing HTTPS relay** | The relay address and its optional limited ingest key. Unity adds `/reports` to a base address. |
   | **Local relay (Editor)** | Choose a saved destination or enter a Discord webhook, or a Telegram bot token and chat ID. |
   | **Temporary HTTPS (mobile QA)** | Use the same destination fields. Unity starts and checks a Cloudflare Quick Tunnel for the local relay. |

3. Click **Connect, test and enable**. The wizard prepares an ingest key for a managed local relay, starts or reuses the relay, sends a sample and applies the configuration when the relay accepts it. Confirm the Report ID in the destination before building. To send runtime reports to both providers from one relay, see the [relay guide](../Tools~/relay/README.md).
4. Build and install the Development QA app. Open **Report**, send a report and match its ID and attachments at the destination.

A hosted relay needs no local tools. Local setup uses Python 3.9+ with venv and an available OS credential store. Temporary HTTPS also needs cloudflared. If an executable is missing, the wizard offers its platform installer. Finish any system prompts, then retry. Linux requires an accessible Secret Service for saved credentials. Manual operations, including stop and forget, live under **Advanced report options > Developer test tools > Manual relay controls**.

## Keep the URL alive during QA

Mobile builds reject localhost, including `adb reverse`. Keep the relay computer and tunnel running. A Quick Tunnel URL changes on restart: apply the new URL and rebuild the app.

The Reports window stops its own tunnel when it closes, scripts reload or Unity exits. For a build that reloads scripts, run the tunnel independently in a terminal:

```sh
cloudflared tunnel --no-autoupdate --url http://127.0.0.1:8766
```

Use your relay's actual port. Enter the printed HTTPS host plus `/reports` in Unity and verify it before building. Keep that terminal running. A stable hosted HTTPS relay avoids rebuilds caused by changing tunnel URLs.

## Restart, stop and troubleshoot

Android/iOS builds fail validation when uploads are enabled with a localhost, non-HTTPS or malformed report URL. Configure and verify HTTPS first, or disable uploads for a local-storage-only build. This build check validates the URL format; it does not establish network reachability or destination delivery.

- Failed local check: confirm port, running process, matching limited key and updated scripts. Restart after changing the key. Never replace the limited key with a Telegram token or Discord webhook.
- Failed HTTPS check: confirm tunnel/network/TLS and proxy routes. A stopped tunnel needs a new URL. Test the current URL before rebuilding.
- Accepted report without a chat message: use the Report ID to inspect the relay delivery receipt/log. Check destination permissions and provider errors. Do not treat **Sent to relay** as delivered to chat.
- Credential-store error: unlock/allow your system store and retry. The first remembered setup installs its credential helper in a project-local Python environment. Linux needs an available unlocked Secret Service.
- **Stop Saved Relay** checks the live project/destination profile and saved PID before stopping; it leaves unrelated processes alone and retains credentials. Stop custom relays with their own launcher/operator.
- **Forget Saved Token/Webhook** removes the saved credential separately. A running relay retains its credential in memory until stopped. Clearing Unity's `Library` removes local settings, not the OS credential entry.

Provider credentials stay on the relay. The limited ingestion key is included in the client and is not a replacement for the project's upload, screenshot and retention policies. Keep setup logs, local databases and credentials out of source control.
