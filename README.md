# Dreamy Debugger

Independent internal Unity debugger, `0.2.0-preview.5`. Responsive runtime overlay and native Editor Control Center use a dark Dreamy theme, mint accents and the original SRDebugger layout/control system.

## Install and run

**Cheat panel in your game:** open **Window → Dreamy Debugger → Cheat Panel**. If the current startup scene has no debugger, click **Add debugger bootstrap to current scene**, save the scene and enter Play Mode. Click **Open cheat panel in Game view**, then enter the configured PIN (default `0000`). The controls appear in the runtime **Options** tab. The Editor window shows configuration/status; runtime controls need an active bootstrap in Play Mode.

Add this private repository at a pinned commit through UPM, or use the sandbox submodule at `LocalPackages/com.dreamy.debugger`. Requires authorized access to the private repository; never put Git credentials in a manifest URL.

Open **Window → Dreamy Debugger → Control Center** and choose **Install / update editable production sample**. Open the selected `DebuggerDemo.unity` and enter Play Mode. Tap the top-left trigger **5 times**, then enter **0000**. Quick Start can also be imported from Package Manager to add editable project cheat examples.

The package does not start automatically just because it is installed. Add a project-owned variant of `Runtime/Prefabs/DebuggerBootstrap.prefab` to a startup scene and assign a project-owned `DebuggerConfiguration` asset. The Control Center creates this setup without overwriting an existing project sample.

No Dreamy Core/UI/Feature/Datasave/Assets, Graphy or Input System package is required. Required dependencies are Unity uGUI and the built-in UI/animation/audio/image/network/JNI modules declared in `package.json`. An Input System adapter activates only if that package is installed; legacy input is supported when enabled by the project.

## Included

- Console: threaded ingress queue, bounded log strings/history, collapse, filters, search, stack traces, copy and original virtualized list.
- Access: configurable 1–20 taps; PIN 4–32 ASCII digits preserving leading zeroes; optional 8–64 character passphrase; cooldown, session expiry and relock on focus/scene changes. Default `5 / 0000` is intentional for internal use.
- Cheat categories: bool, int/long, float/double, enum, text, read-only, action, bounds and confirmation. Registration is owned/disposable and execution checks access again.
- HUD: Off/FPS Only/Compact/Detailed, shared frame-time collector, fixed history, text 4 Hz and memory 1 Hz. HUD hides behind an open debugger panel and restores afterward; visibility mode is independent.
- Bug reports: immutable snapshot, redaction, optional downscaled async GPU readback/JPEG encoding, memory/concurrency caps, text-only fallback, durable disk outbox, quotas, expiry, retry/backoff and bounded dead letters.
- Delivery: HTTPS relay client with stable report IDs; local reference relay with SQLite acceptance, destination retries, Telegram documents/photos, Gmail API or SMTP email. Fake delivery is the default.
- Crash support: managed error snapshots, abnormal-session journal, Android 11+ previous OS exit evidence and an external native-SDK evidence import contract. These do not guarantee a new screenshot or managed callback during process death.
- Editor: Control Center pages for access, cheats, HUD, reports, themes and acceptance; package-safe asset authoring/validation, editable production demo and clean-consumer build verification.

Project-bound resource and ownership templates are included in Quick Start (`BoundCheatExamples.cs`). Import/edit the sample and bind delegates to each game’s verified services. The sandbox adapter lives under `Assets/_Project/Debugger`; the standalone demo still uses in-memory values.

## Project-owned cheat options

```csharp
var runtime = Dreamy.Debugger.DebuggerRuntime.Instance;
var registration = runtime.Cheats.Register(new[] {
    Dreamy.Debugger.Options.CheatOption.Value(
        "player.invincible", "Player", "Invincible", () => model.Invincible,
        value => model.Invincible = value),
    Dreamy.Debugger.Options.CheatOption.Button(
        "save.reset-test", "Save", "Reset test save", ResetTestSave,
        requiresConfirmation: true)
});
// Dispose registration on the feature's disable/destroy; don't modify package Runtime.
```

The runtime overlay reads the same registry. The public presentation reflection API is retained for compatibility, but typed options are recommended for AOT, explicit ownership and consistent permission checks. No sample mutates a real game's economy or save.

## Reporting policy

Default uploads and automatic screenshots are off. Reports work offline. Enable them through your project configuration when appropriate; `GameplayBusy` defaults to **true**, so automatic uploads wait until the host marks an idle/menu interval, unless `UploadDuringGameplay` is explicitly enabled. `FlushPendingAsync()` is an explicit QA/host request to attempt pending delivery.

Relay acknowledgement means durable relay acceptance, not confirmed Telegram/email delivery. The client keeps a report until acceptance; the relay tracks destination status separately. Native handlers and OS kills may leave only previously persisted data. See `Documentation~/REPORTING.md` and `PLATFORM_SUPPORT.md`.

## Runtime and customization ownership

Package `Runtime`/`Editor`/base prefabs are maintained and versioned together. Consumer code edits imported samples, project configuration, providers and prefab variants. Runtime settings are cloned before use; no game changes package configuration assets at runtime. Package assets live outside Resources and enter player builds through the bootstrap/catalog references. A release scene processor removes development debugger roots; ReleaseDiagnostics removes the UI catalog while retaining text diagnostics.

Local access credentials and limited ingestion keys are recoverable from a client build. Keep bot/mailbox secrets at the relay. Debugger unlock does not authorize server or purchase operations.

See `Documentation~/TRACKING.md`, `ACCEPTANCE.md`, `MIGRATION.md` and `THIRD_PARTY_NOTICES.md`. This is an internal preview; device and live-destination gates remain explicit in the support matrix.
