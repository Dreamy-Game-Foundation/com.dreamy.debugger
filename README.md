# Dreamy Debugger

Internal standalone Unity debugger under construction. No required Dreamy Core/UI/Feature, Input System, Graphy or third-party runtime dependency.

## Available foundation

- Immutable `AccessPolicy`: default **5 taps + PIN `0000`**, configurable count 1–20 and PIN 4–32 ASCII digits (leading zeroes preserved).
- Tap timing, prompt, failed-attempt cooldown, session expiry and explicit relock. Host supplies unscaled monotonic clock and trigger-region input.
- Typed cheat options grouped by category: bool, int/long, float/double, enum, string, read-only value, action, numeric bounds and confirmation.
- Atomic container registration and disposable ownership; every read/write/action checks the host authorization callback.
- Injectable text-report dispatcher and an editable fake-report sample. No automatic reporting/network/startup.

Default `0000` is intentional for internal use. Local verifier reduces plaintext retention but cannot secure client-side privileged operations. Release authorization must be enforced by host/build policy; registry has no permissive default.

## Boundaries

Consumer edits imported `Samples~`, project config and providers. Do not patch package Runtime per game. This foundation currently has no ScriptableObject settings, rendered debugger panel, FPS HUD, persistent outbox, screenshots or Telegram/Gmail sender. Those require subsequent phase gates. Original SRDebugger source remains unchanged in the sandbox pending controlled migration.

See `Documentation~/ACCEPTANCE.md` and `Documentation~/TRACKING.md` for acceptance status. This private repository is not a redistribution license for vendor assets.
