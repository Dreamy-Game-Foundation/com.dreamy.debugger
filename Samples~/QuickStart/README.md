# Editable debugger examples

Import Quick Start through Package Manager. Customize the imported sample and game adapter; package Runtime stays maintained and independent of game packages.

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

Open Window → Dreamy Debugger → Cheat Panel. Add a debugger bootstrap to the startup scene if missing, save, enter Play Mode and open the runtime Options tab. Defaults: top-left 5 taps + PIN 0000. The Control Center also offers a standalone demo installer.

`FakeReportTransport` supports simulated Success/Offline/RateLimited/ServerError/Reject/Timeout outcomes without sending messages. The Acceptance Playground menu opens Control Center.
