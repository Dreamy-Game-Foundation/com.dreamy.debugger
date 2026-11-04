# Editable quick start

Import this sample through Package Manager. Runtime is maintained in the package; edit this imported folder for game commands.

```csharp
var access = new Dreamy.Debugger.Access.AccessSession(
    new Dreamy.Debugger.Access.AccessPolicy(), () => UnityEngine.Time.realtimeSinceStartupAsDouble);
var registry = new Dreamy.Debugger.Options.CheatRegistry(
    () => UnityEngine.Debug.isDebugBuild && access.IsUnlocked);
var gameCheats = new Dreamy.Debugger.Sample.ProjectCheats();
var lease = registry.Register(gameCheats.CreateOptions());
// Host forwards taps only in the intended trigger area. Default: 5 taps then PIN "0000".
// Render registry.Snapshot() grouped by Category using the later UI layer.
// On host destruction/disable: lease.Dispose(); access.Relock();
```

For fake reports, create `DiagnosticReport("[FAKE] QA incident", "qa-build", true)` and send through `ReportDispatcher(new FakeReportTransport())` with an owner CancellationToken. Choose Success/Offline/RateLimited/ServerError/Reject/Timeout. No network or crash occurs. Timeout is a simulated outcome, not a real timer test.

No automatic bootstrap, production cheat, storage reset, screenshots or sending credentials are included. UI, transport relay, persistent outbox and vendor migration remain phase work, not claims of this foundation.

After importing, open **Window > Dreamy Debugger > Acceptance Playground** to reach the styled Control Center. Install the production sample from Overview; edit access configuration under Access, enter Play Mode in the demo for real keypad/cheat UI, and choose a fake delivery outcome under Bug reports. Simulated delivery sends no network traffic. The production overlay and Editor tool are separate views over the same domain contracts.

`ProjectCheatHost` is an explicit opt-in runtime demo component; add it only in a demo scene. It registers demo options on enable, revokes them on disable/destroy and relocks on pause/focus loss. Host UI supplies taps; there is no invisible global touch interceptor.
