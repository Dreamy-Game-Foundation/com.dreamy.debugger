# Editable debugger examples

The package already opens automatically without this sample. Import Quick Start through Package Manager only for editable examples. Customize the imported sample and game adapter; package Runtime stays maintained and independent of game packages.

## Demo-only options

`ProjectCheats` / `ProjectCheatHost` are in-memory examples: God Mode, Coins, Speed, Difficulty, Note and reset. Add their host only to a standalone demo. They do not change your game wallet. The host registers an owned scope and disposes it on disable.

## Connect real game services

`BoundCheatExamples.cs` contains two editable templates with no Core/Economy/Shop dependency:

```csharp
var coins = new Dreamy.Debugger.Sample.ResourceCheatOptions(
    "mygame.coins", "Economy", "Coins",
    () => gameWallet.CoinBalance,
    amount => gameWallet.GrantCoins(amount), initialAmount: 1000);
var noAds = new Dreamy.Debugger.Sample.OwnershipCheatOptions(
    "mygame.no-ads", "Entitlements", "No Ads",
    () => gameEntitlements.HasNoAds,
    () => gameEntitlements.GrantNoAdsForTesting());
var runtime = Dreamy.Debugger.DebuggerRuntime.Instance;
var coinsLease = runtime.Cheats.Register(coins.CreateOptions());
var adsLease = runtime.Cheats.Register(noAds.CreateOptions());
// Dispose both leases when this game's host is disabled/destroyed.
```

The wallet/entitlement names above are illustrative game APIs, not debugger package APIs. Bind to your game's verified services. Register only after those services and DebuggerRuntime are ready. Use the runtime's own access gate; do not create another unlock session for game cheats.

Resource controls show current balance, a bounded grant amount and a confirmation action. Ownership controls show a read-only owned flag and a confirmation action; repeated clicks do not grant an already owned entitlement. Persistence, transactions, events and failure reporting remain the game service's responsibility. The templates do not fabricate store receipts or bundle an ad SDK.

Each game chooses its own IDs/categories/options. Keep game glue under its project assembly, point to project-owned configuration/prefab variants, and leave package Runtime independent. The sandbox demonstrates this in Assets/_Project/Debugger with a real persisted resource wallet.

## Open the UI

Open Window → Dreamy Debugger → Cheat Panel. Add a debugger bootstrap to the startup scene if missing, save, enter Play Mode and open the runtime Options tab. Defaults: top-left 5 taps + PIN 0000. Settings → Overview offers a starter setup when no project configuration is selected.

`FakeReportTransport` supports simulated Success/Offline/RateLimited/ServerError/Reject/Timeout outcomes without sending messages. Synthetic report tools are in Window → Dreamy Debugger → Telegram & Reports.


## Project-owned partial options

Import `GameDebugOptions.cs` and `GameDebugOptions.Economy.cs` into the same game asmdef.
They demonstrate Category, DisplayName, Sort, NumberRange, Increment, Browsable(false),
read-only balance and a confirmed action. Partial types cannot span assemblies.
Bind the constructor delegates to your wallet; the sample does not create a second wallet.

```csharp
var options = new GameDebugOptions(readCoins, grantCoins);
IDisposable lease = runtime.RegisterCheats(options, "game");
// Dispose the lease when the owning host disables or the runtime changes.
```

Attribute options use the same access checks as typed options. Reflection needs preserved
members on IL2CPP (for example `[UnityEngine.Scripting.Preserve]` on your container and
members or your game's link.xml; the sample includes an editable preservation file); device stripping acceptance is still required.
Methods must be public instance `void` with no parameters. Plain actions are immediate; mark destructive methods with CheatConfirmation. Card grants always require confirmation.
Unsupported property types fail registration explicitly. Description tooltips are not implemented. RegisterCheats owns INotifyPropertyChanged subscriptions, coalesces notifications on the main thread and releases them with its lease/runtime. Visible/pinned controls retain 4 Hz fallback polling; registry writes/actions notify immediately. Call runtime.Cheats.NotifyValuesChanged() for an explicit external update.


## Optional task-card presentation

ResourceCheatOptions groups explicit `.balance`, `.amount`, `.grant` roles; OwnershipCheatOptions groups `.owned`, `.unlock`. Each group has a stable ID/title/kind supplied through CheatOption.WithCard. These are presentation hints only. Incomplete or conflicting groups use standard controls; no string matching guesses what a wallet/ad option means. Keep all members of a group in one owned scope, declare metadata before registration and preserve game authorization/transactions.

On the redesigned runtime UI, open Cheats, search/filter/pin or collapse categories. Debugger tools is folded below game groups by default. Resource grants still require confirmation; changing the amount or removing its owner while a dialog is open prevents stale execution. Ownership is grant-only, so an owned entitlement does not become a revoke toggle.


## Default authoring: partial class + attributes

`ProjectCheats.cs` registers once; add options to `ProjectCheats.Player.cs`, `.Economy.cs`, `.QA.cs` or a new partial file in the same namespace/assembly. Category/DisplayName/NumberRange/Increment/Sort/Browsable work through the authorized registry. Plain parameterless void methods run immediately; CheatConfirmation opts into a dialog. Resource/ownership card actions always confirm. CheatId preserves the sample's existing IDs.

`GameDebugOptions.Economy.cs` shows a bound resource card with ResourceCheat roles. Use OwnershipCheat for a read-only owned flag and a grant-only method. See Documentation~/CHEAT_OPTIONS.md. Keep reflected types in the imported sample link.xml. BoundCheatExamples remains available for existing typed integrations; it is no longer required by the sandbox game.


Preview.3 workflow: ProjectCheats implements INotifyPropertyChanged; ProjectCheatHost uses RegisterCheats. Sample reset and grant opt into CheatConfirmation. Keep and Dispose the registration lease. Structural changes are batched; unchanged controls/cards/pins keep identity. Caller-owned standalone registries using RegisterContainer must FlushValueNotifications on their owner thread; the runtime ticks automatically.
