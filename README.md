# Dreamy Debugger

Independent internal Unity debugger — **0.2.0-preview.6**, release candidate, not published. Red Dreamy panel with console, grouped cheats, profiler and system information; optional Graphy-based performance HUD and local-first diagnostics.

## Install and open

Install this private repository through UPM at a pinned commit, or use a local package/submodule. Git access is required; never put credentials in a manifest URL. Declared minimum: Unity 6000.0; current verification uses **6000.4.12f1**. See [release evidence and limits](Documentation~/RELEASE.md).

1. Open **Window → Dreamy Debugger → Cheat Panel**.
2. In your startup scene, click **Add debugger bootstrap to current scene** if needed. Save, then enter Play Mode.
3. Click **Open cheat panel in Game view**, or tap the top-left trigger **5 times** and enter **0000**. Defaults are configurable.
4. Edit your project-owned configuration through **Window → Dreamy Debugger → Settings**. Pages: Overview, Access, Options, Performance and Reports. Telegram setup and synthetic report tools are under **Window → Dreamy Debugger → Telegram & Reports**.

Installation alone does not start the debugger. The host needs a bootstrap with a project-owned configuration and the package asset catalog. Import **Debugger Quick Start** from Package Manager for editable cheat examples. The Settings starter setup can create a standalone demo without replacing your game setup or existing customization.

## Features

- Console: bounded threaded ingress, history, collapse, filters, search, stack trace, copy and virtualized rows.
- Access: 1–20 taps; numeric PIN of 4–32 ASCII digits, including leading zeroes; optional 8–64 character passphrase; cooldown, session expiry, focus/scene relock. Default **5 / 0000** is for internal testing.
- Cheats: categories, sorting, bool, integer/Int64, float/double, enum, string, read-only value and confirmed action. Registration is disposable; every write/action rechecks access. One failed subscriber/getter cannot break other controls. A failed getter displays **Unavailable** and can recover.
- HUD: **Off by default**, FPS Only / Compact / Detailed. Select a corner, drag the `::` grip within the safe area, or reset to the configured position. Placement is session state. Other HUD graphics pass pointer input through. HUD hides behind the open debugger panel.
- Performance: one shared bounded frame sampler, 4 Hz text, 1 Hz memory, optional 10 Hz audio RMS. Detailed mode uses Graphy's licensed mobile graph renderer and Roboto font with configurable good/caution/critical colors. FPS Only creates no graph material; missing/unsupported shader falls back to text. Frame-time charts do not measure GPU render time. Low-FPS warnings use a separate recent 0.5-second window with sustain/cooldown.
- Diagnostics: bounded managed error snapshots, redaction, optional asynchronous downscaled screenshot, single-flight resource ownership, text fallback, durable outbox, retention, retry and dead letters. Android previous-exit evidence and external native-evidence providers do not guarantee a screenshot during process death.
- Delivery: HTTPS relay client; reference Python relay with durable SQLite acceptance and separate destination retries. Telegram is implemented. **Gmail/email remains pending and outside current release acceptance.**

The runtime panel has one visible Dreamy logo and a hamburger that opens and closes the sidebar at every width. Package support reporting is absent from runtime navigation. Synthetic report/capture tools remain in the Editor.

## Project-owned cheats

```csharp
var runtime = Dreamy.Debugger.DebuggerRuntime.Instance;
var lease = runtime.Cheats.Register(new[] {
    Dreamy.Debugger.Options.CheatOption.Value(
        "player.invincible", "Player", "Invincible",
        () => model.Invincible, value => model.Invincible = value),
    Dreamy.Debugger.Options.CheatOption.Button(
        "save.reset-test", "Save", "Reset test save", ResetTestSave,
        requiresConfirmation: true)
});
// Dispose lease when its owning feature disables/destroys.
```

Register after the runtime and game services are ready. Bind wallet/ad entitlement commands to the game's services; the package owns no economy, ad SDK or save. Quick Start includes resource/ownership templates and `GameDebugOptions.*` partial files with Category, DisplayName, Sort, NumberRange, Increment and Browsable attributes. All partial files belong to the **same game assembly**. Attribute actions are public instance `void` methods without parameters and require confirmation. Description tooltips and INotifyPropertyChanged forwarding are not implemented.

Typed and attribute options refresh after registry writes/actions. Call `runtime.Cheats.NotifyValuesChanged()` for an explicit external update; visible/pinned controls also poll at most 4 Hz without rebuilding rows. Preserve reflected game members for IL2CPP: see the editable sample `link.xml`, and update assembly/type names when copying it. A successful Editor test does not prove device stripping behavior.

## Ownership and dependencies

Maintained package: Runtime, Editor and base assets. Editable consumer content: imported Samples~, configuration, prefab variants, providers and game adapters. Runtime clones configuration before use. Keep project changes in Assets; do not patch package Runtime for each game. Upgrade/migration instructions: [MIGRATION.md](Documentation~/MIGRATION.md).

No Dreamy Core, UI, Datasave, Feature, Assets, external Graphy or Input System package is required. Unity uGUI/modules are declared in package.json; the Input System adapter is optional. Core integration, if needed later, belongs in an optional adapter depending on both packages. Shared-looking helpers alone do not justify adding a mandatory dependency.

Non-development builds strip Development debugger roots. ReleaseDiagnostics retains text diagnostics and removes the UI catalog. Local unlock codes and limited relay ingestion keys are recoverable from a client build; server authority must validate purchases/rewards.

## Reporting and production limits

Uploads and automatic screenshots default off. `GameplayBusy` defaults true; automatic uploads wait for a host-defined idle interval unless explicitly permitted during gameplay. `FlushPendingAsync()` is an explicit host/QA attempt. Keep Telegram bot tokens/mail credentials at the relay, never in a shipped configuration/APK. A phone needs a reachable HTTPS relay; localhost only reaches that phone.

Relay acknowledgement means **durable relay acceptance**, not destination delivery. Local Telegram screenshot delivery was verified historically in the sandbox; credentials, remote production hosting and current device delivery remain deployment gates. No real destination is contacted by the automated regression suite.

Sensitive-screen suppression is checked again around capture and persistence. This is not region masking, a consent manager or a consent-withdrawal flow. Production screenshot consent/disclosure, redaction of personal data, device profiling and crash/ANR/OOM acceptance are host responsibilities still pending. No “zero lag” or fatal-crash screenshot guarantee is made.

Read [REPORTING.md](Documentation~/REPORTING.md), [PLATFORM_SUPPORT.md](Documentation~/PLATFORM_SUPPORT.md), [ACCEPTANCE.md](Documentation~/ACCEPTANCE.md), [TRACKING.md](Documentation~/TRACKING.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Internal use only; upstream licensing and notices still apply.
