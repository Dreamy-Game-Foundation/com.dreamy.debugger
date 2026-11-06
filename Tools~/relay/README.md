# Dreamy reference relay

Run `DREAMY_FAKE_DELIVERY=1 python3 server.py` and `python3 -m unittest discover -v`. Default bind is localhost; fake delivery sends no external requests.


The Unity client posts schema-1 JSON to `/reports`. A 202 response follows durable SQLite commit; duplicate IDs are accepted idempotently. Destination delivery has separate state and bounded retries and can be at-least-once after ambiguous remote acknowledgements. This is a reference service, not a hosted destination or account configuration.


Run `python3 telegram_setup.py` in a private local terminal. Enter the bot token at the hidden prompt and the destination chat ID at the next prompt. The launcher keeps the token in process memory/environment, not files, command arguments or Unity assets. It starts a Telegram-only loopback relay in a separate background session with a separate local database. Wait for `Relay ready in background` before closing the setup window. Startup errors stay visible. It sends reports only when submitted to that relay; no automatic test message is created by the launcher.

In Unity open **Window → Dreamy Debugger → Telegram & Reports**, select the project's configuration, enable Uploads and set `http://127.0.0.1:8765/reports`. Restart Play Mode, create a fake local report, then explicitly upload queued reports to test. Queued reports may contain earlier diagnostics; review the queue before flushing to a real destination. Fake mode simulates delivery; a synthetic `[FAKE]` report submitted to a live relay is actually sent.


The launcher records the server PID in `data/telegram-local.pid`; stop that owned process to stop the relay. Server credentials remain in process environment until it stops; they are not persisted by the launcher.

Loopback is for this desktop Editor only. A physical phone needs an accessible HTTPS relay URL; `127.0.0.1` on a phone points to the phone itself. Use the deployment environment variables below for a server.

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
| `DREAMY_DESTINATIONS` | empty | Set `telegram` for live Telegram routing. |
| `TELEGRAM_BOT_TOKEN` | unset | BotFather HTTP API token; provide through private server environment/secrets, never the Unity build. |
| `TELEGRAM_CHAT_ID` | unset | Numeric target chat ID discovered through setup. |

Unity runtime config: `UploadsEnabled=true`, `RelayUrl=https://<your-relay-host>/reports`, `LimitedClientKey` matching the ingestion key. Enable `AllowScreenshots` only when the host's capture policy permits it; restart Play/rebuild after config changes. Default automatic uploads wait for `GameplayBusy=false` unless `UploadDuringGameplay` is enabled.

A client ingestion key is recoverable from a build; use server quotas, restricted destinations and monitoring. The script is a reference relay and does not provision HTTPS, backups, access management or monitoring. Inspect destination retry/dead-letter state when validating delivery: HTTP 202 means persisted at the relay, not delivered to Telegram. Ambiguous destination acknowledgements can cause duplicate messages. Test local fake delivery first; `[FAKE]` does not suppress network requests when live delivery is enabled.
