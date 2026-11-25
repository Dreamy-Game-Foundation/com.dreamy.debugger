# Mobile report setup

Complete this setup before building a Development QA app. Testers use the configured app; they do not need bot tokens, webhooks or a relay on their device.

## Prerequisites

- A computer with Unity, Python 3, and network access to your destinations.
- Telegram: a BotFather bot token, a destination chat ID and permission for the bot to send there. Send `/start` to the bot or a message mentioning it in the group before discovering the chat.
- Discord: an incoming webhook for the target channel.
- A reachable HTTPS relay. For temporary local QA, install `cloudflared` through the Editor's copied install command or official download instructions. Quick Tunnels need no Cloudflare account; their URL is temporary.

## Configure and verify

1. Open **Window > Dreamy Debugger > Telegram & Reports**. Select the configuration used by your QA build. Switch Unity to the target build platform and finish script changes before starting a temporary window-owned tunnel.
2. Click **Set up local Discord / Telegram**. Choose a provider and start its relay in a terminal. Enter the token/webhook at the hidden prompt, never in Relay URL or Limited Client Key. Enable remembering if desired; credentials use the OS credential store. Enter/discover the Telegram chat ID. The launcher validates access; a successful setup alone does not prove message delivery.
3. Expand **Android / iOS HTTPS setup** and click **Prepare ingest key for local launchers**. This creates/reuses a limited client key and saves it in the selected configuration and local launcher settings. It is a client access limit, not a provider secret. Click **Stop Saved Telegram/Discord Relay (keep credentials)**, then start the saved relay again so it loads the key. A custom relay must be restarted by its operator with the same `DREAMY_INGEST_KEY`.
4. Set **Local relay port** to the relay's port: Telegram defaults to 8765, Discord to 8766. Click **Check local relay**. Live mode, full-test support and ingest-key protection must pass. An older relay must be restarted with the updated scripts.
5. Install `cloudflared` using the copied command or official instructions. Use **Locate installed cloudflared** if Unity has an older PATH. Click **Start free QA tunnel**, wait for its URL, then **Check HTTPS relay** and **Apply HTTPS URL to selected configuration**. Alternatively enter your existing HTTPS relay URL ending in `/reports` and check it. Its proxy must forward `/health`, `/reports` and the test routes. Applying keeps in-game sending off.
6. Choose each configured **Test destination**, click **Send full test report** and match the displayed Report ID, JSON and screenshot in the actual chat/channel. Health and HTTP acceptance do not establish destination delivery. Each managed local launcher serves one destination. To send a runtime report to both, run one relay with `DREAMY_DESTINATIONS=discord,telegram` and both server-side credentials; see the [relay guide](../Tools~/relay/README.md).
7. Enable **Enable sending in game**, save, then build/install the Development QA app. Unlock the debugger, open **Report**, send and match its ID and attachments at the destination. Repeat offline, restore networking and verify retry. An Editor fixture does not prove APK delivery.

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
