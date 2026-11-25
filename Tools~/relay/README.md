# Dreamy reference relay

Discord displays a formatted report card with an inline screenshot when included. Telegram displays diagnostics in the JSON caption and sends the original JPEG as a file with the same Report ID. Look up a single submission by **Report ID** or group related automatic errors in chat by **Issue key** when present. Update `server.py` and restart the relay before using **Send full test report**. [Discord webhook API](https://docs.discord.com/developers/resources/webhook#execute-webhook) · [Telegram Bot API](https://core.telegram.org/bots/api#senddocument)

For expanded console reports, configure the reverse proxy to allow request bodies up to the relay's 3,000,000-byte limit.

Run `DREAMY_FAKE_DELIVERY=1 python3 server.py` and `python3 -m unittest discover -v`. Default bind is localhost; fake delivery sends no external requests.


The Unity client posts schema-1 JSON to `/reports`. A 202 response follows durable SQLite commit; duplicate IDs are accepted idempotently. Destination delivery has separate state and bounded retries and can be at-least-once after ambiguous remote acknowledgements. This is a reference service, not a hosted destination or account configuration.


In **Window → Dreamy Debugger → Telegram & Reports**, expand **Local relay setup (desktop testing)**, select **Telegram**, and click **Start Telegram Relay in Terminal**. Unity opens a terminal directly. **Find Chat ID in Terminal** opens the discovery prompt if needed. The **Manual commands and saved settings** foldout has copyable fallback commands. To run setup directly, use `python3 telegram_setup.py` in a private local terminal. Enter the bot token at the hidden prompt and the destination chat ID at the next prompt. The launcher passes the token through the process environment and starts a Telegram-only loopback relay with a separate local database. Wait for `Relay ready in background` before closing the setup window. Startup errors stay visible. Click the test button to submit a sample.

In Unity open **Window → Dreamy Debugger → Telegram & Reports**, choose the project's configuration and set **Relay URL** to the address printed by the launcher, usually `http://127.0.0.1:8765/reports`. Set **Limited Client Key** if the relay requires it. Select **Telegram** under **Test destination** and click **Send full test report** in Edit Mode or Play Mode. Find the 1440 × 810 sample JPEG and diagnostics using the same Report ID in the bot chat.

From this folder, check the Telegram destination receipt for the default local launcher database:

```sh
python3 server.py --db ./data/telegram-local.sqlite --delivery-status REPORT_ID
```

Use the deployed relay's SQLite path when checking production delivery. The receipt shows `pending`, `delivered` or `dead`, attempts and any sanitized error. A pending entry may show when the next retry is due. A `destination HTTP 401` usually means the bot token needs attention. For 400/403, check the chat ID, bot membership and permission to post documents or photos. Keep runtime uploads disabled until you want ordinary game reports to be sent. To test the runtime queue, enable Uploads, restart Play Mode, save a sample report and send that report from the **Report** tab.


The launcher records the server PID in `data/telegram-local.pid`; stop that owned process to stop the relay. Server credentials remain in process environment until it stops; they are not persisted by the launcher.

Loopback is for this desktop Editor only. A physical phone needs an accessible HTTPS relay URL; `127.0.0.1` on a phone points to the phone itself. Use the deployment environment variables below for a server.

## Remembered local relay

Enable **Remember bot token on this computer**, then click **Start Telegram Relay in Terminal**. First-time installation requires Python 3.9+ with venv/pip and network access to the configured Python package index. On Debian/Ubuntu install `python3-venv` if venv creation fails. The helper installs `keyring==25.6.0` in `Library/DreamyDebugger/TelegramRelay/credential-env` and uses the native backend for the host. On Linux an unlocked Secret Service, such as GNOME Keyring, must be available in the desktop session. If access is refused, unlock the system store and retry or use manual setup.

Use **Start saved relay when Unity opens** for automatic startup or **Start Saved Telegram Relay** to start immediately. A matching local relay for this project and chat is reused. A different relay on that port is left alone. Saved-mode databases, logs and PID are under `Library/DreamyDebugger/TelegramRelay`; the DB name starts with `telegram-` and ends with `.sqlite`. Databases are separate per project and destination. Use the matching DB path with `server.py --delivery-status`. The CLI's manual mode still uses `data/telegram-local.sqlite` by default.

Forgetting the saved token disables future automatic launches. Stop any running local relay before entering a replacement token if you want the new token to take effect immediately. If `Library` is cleared, run remembered setup again to restore the helper and local Chat ID. The credential store is managed separately from Unity's cache.

### Discord

Select **Discord** under **Local relay setup** in Unity. The terminal opens with a hidden webhook prompt. The local setup uses port 8766 and a separate credential entry, database, log and PID. To open the same setup from a terminal, supply the Unity project root:

```sh
python3 telegram_setup.py --destination discord --project-dir "/path/to/project" --remember-token
```

For receipt lookup, use the matching `discord-*.sqlite` file under `Library/DreamyDebugger/DiscordRelay`. The launcher's metadata contains a destination fingerprint. The webhook URL remains in the system credential store and the running relay process. Start and forget operations for Discord affect only its local relay configuration. Stop the running relay before replacing its live webhook.

## Finding and saving the destination

Bot token comes from @BotFather → /mybots → choose bot → API Token. It is not the bot username or numeric bot ID.

Send /start to your bot in the private chat that should receive reports. For a group, add the bot and send /start@YourBotUsername in that group. Run `python3 telegram_setup.py --discover-chat` and enter the token privately. It calls getMe/getUpdates only; it sends no report, does not acknowledge updates with an offset and does not change webhooks. Recent chat IDs are listed. If a webhook/other poller owns the bot, discovery reports the conflict rather than deleting it.

Save the desired destination in `Assets/Editor/DreamyDebugger/TelegramRelaySettings.asset`, field ChatId. Control Center → Telegram & Reports offers buttons to copy discovery/setup commands. The setup command reads that SO with `--settings`; the asset contains no token and stays under Editor. The launcher verifies the bot token and destination with read-only Telegram requests before starting the relay. This Editor SO configures the local relay launcher; it is not a client-side routing authority for a deployed relay.

Official API: https://core.telegram.org/bots/api#getupdates and https://core.telegram.org/bots/api#getchat.

If token input fails before connecting: restart the discovery command to load the updated launcher. Paste with right-click or Ctrl+Shift+V. Hidden input displays no characters, including asterisks. The launcher accepts surrounding quotes, zero-width copy characters and an optional `bot` prefix; it explains invalid input without echoing it and allows retries. A numeric chat/bot ID or Telegram client api_id/api_hash is not a BotFather HTTP API token. A successful format check is not authentication: getMe must still succeed. Never paste tokens into bug reports/chat.


## Remote deployment configuration

Use Python 3 with the standard library. Run `server.py` under a service supervisor with a persistent writable directory and an HTTPS reverse proxy; TLS is terminated by the proxy, not by this script. Restrict direct access to the Python listener. No deployment or external delivery is performed by these instructions.

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `DREAMY_BIND` | `127.0.0.1` | Listener address. Non-loopback bind requires a non-empty ingestion key; the operator must configure TLS separately. |
| `DREAMY_PORT` | `8765` | Internal listener port. |
| `DREAMY_RELAY_DB` | `./data/reports.sqlite` | Durable SQLite path. Keep the containing directory on persistent storage. |
| `DREAMY_INGEST_KEY` | empty | Limited ingestion key; match Unity `LimitedClientKey`. Required when binding remotely. |
| `DREAMY_FAKE_DELIVERY` | `1` | Default simulates delivery. Set `0` only when intentionally enabling live destination requests. |
| `DREAMY_DESTINATIONS` | empty | Set `discord`, `telegram` or `discord,telegram` for live routing. |
| `DREAMY_DISCORD_WEBHOOK` | unset | Discord webhook URL stored as a server secret. The relay sends a short message, JSON and an optional JPEG with mentions disabled. |
| `TELEGRAM_BOT_TOKEN` | unset | BotFather HTTP API token; provide through private server environment/secrets, never the Unity build. |
| `TELEGRAM_CHAT_ID` | unset | Numeric target chat ID or a public channel username prefixed with @. |

Unity runtime config: `UploadsEnabled=true`, `RelayUrl=https://<your-relay-host>/reports`, `LimitedClientKey` matching the ingestion key. Enable `AllowScreenshots` only when the host's capture policy permits it; restart Play/rebuild after config changes. Default automatic uploads wait for `GameplayBusy=false` unless `UploadDuringGameplay` is enabled.

A client ingestion key is recoverable from a build. Use server quotas, restricted destinations and monitoring, and deploy separate relay instances and keys per environment when isolation is needed. Provision HTTPS, backups, access management and monitoring with the host infrastructure. HTTP 202 means the relay persisted the report. Verify delivery using the relay receipt and the report ID in Discord or Telegram. If the relay sits behind an HTTPS reverse proxy, forward `/reports/telegram-test` to enable the Telegram Editor test. Ambiguous destination acknowledgements can cause duplicate messages. Use fake mode for simulated delivery. A synthetic report is sent to live destinations when live delivery is enabled.
