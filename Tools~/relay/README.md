# Dreamy reference relay

Run `DREAMY_FAKE_DELIVERY=1 python3 server.py` and `python3 -m unittest discover -v`. Default bind is localhost; fake delivery sends no external requests.

Deployment variables, Gmail token rotation, TLS, storage and destination semantics are documented in `Documentation~/REPORTING.md` at the package root. Use a persistent volume and service supervisor; never copy bot/mailbox credentials into the Unity configuration.

The Unity client posts schema-1 JSON to `/reports`. A 202 response follows durable SQLite commit; duplicate IDs are accepted idempotently. Destination delivery has separate state and bounded retries and can be at-least-once after ambiguous remote acknowledgements. This is a reference service, not a hosted destination or account configuration.
