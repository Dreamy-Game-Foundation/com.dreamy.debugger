# Tạo cheat bằng partial class và attribute

Mỗi game sở hữu container và service bindings riêng. Không cần Core/UI; package chỉ đọc metadata và hiển thị. Reflection chạy khi đăng ký, không quét assembly mỗi frame.

## Bắt đầu

1. Import Quick Start hoặc tạo class `public sealed partial class GameCheatOptions` trong assembly game.
2. Tạo object sau khi service game sẵn sàng. Host gọi `runtime.RegisterCheats(options, "game")` một lần và giữ lease để Dispose khi owner đóng.
3. Thêm file partial cùng class/namespace/assembly. Public property có setter là control sửa được; getter-only/private setter là read-only; public void không tham số là button; thêm `[CheatConfirmation]` nếu cần dialog.
4. Giữ type/members trong link.xml của game nếu build stripping/IL2CPP. Partial ở assembly game không thể bổ sung member vào class ở assembly package.

```csharp
using System.ComponentModel;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;

public sealed partial class GameCheatOptions
{
    [Category("Economy"), DisplayName("Grant amount")]
    [NumberRange(1, 1000000), Increment(100), Sort(1)]
    public long GrantAmount { get; set; } = 100;

    [Category("Economy"), DisplayName("Add coins")]
    public void AddCoins() => wallet.AddCoins(GrantAmount); // service do game cung cấp
}
```

Ví dụ cần constructor/service `wallet` của game; package không cung cấp API wallet này. Public helper không dùng làm cheat phải private hoặc `[Browsable(false)]`. Field/static/method có tham số không được tạo control. Property phải dùng kiểu hỗ trợ: bool/string/enum hoặc số. Authorization/range/confirmation vẫn đi qua registry; attribute không cấp quyền server.

## Thẻ tài nguyên

```csharp
private const string Coins = "game.coins";

[Category("Economy"), ResourceCheat(Coins, "Coins", CheatCardRole.Balance)]
public long CoinBalance => wallet.Coins;

[Category("Economy"), ResourceCheat(Coins, "Coins", CheatCardRole.Amount)]
[NumberRange(1, 1000000), Increment(100)]
public long CoinAmount { get; set; } = 100;

[Category("Economy"), ResourceCheat(Coins, "Coins", CheatCardRole.Action)]
public void AddCoins() => wallet.AddCoins(CoinAmount);
```

Ba member tạo một card; ID tự sinh là `game.coins.balance`, `.amount`, `.grant`. Title và category phải nhất quán; mỗi role một member. Balance phải numeric read-only, Amount numeric writable, Action parameterless void. Tên C# có thể đổi mà ID/card không đổi. Thiếu role thì UI giữ generic controls thay vì làm mất option.

## Thẻ ownership / No Ads

```csharp
[Category("Entitlements"), OwnershipCheat("game.no-ads", "No Ads", CheatCardRole.Owned)]
public bool NoAdsOwned => purchases.NoAdsOwned;

[Category("Entitlements"), OwnershipCheat("game.no-ads", "No Ads", CheatCardRole.Action)]
public void GrantNoAds()
{
    if (!NoAdsOwned) purchases.GrantDebugNoAds();
}
```

`purchases` là service game tự cung cấp. ID tự sinh `.owned` và `.unlock`. Owned là bool chỉ đọc; method phải kiểm tra ownership và gọi đường grant/save của game. Không giả IAP receipt hoặc thu hồi giao dịch mua thật. UI ẩn action khi owned; guard trong game vẫn cần cho lời gọi trực tiếp/automation.

## ID, nhãn và tương thích

- Không có card/ID attribute: ID là `<prefix>.<MemberName>`, nhãn là tên member.
- `[CheatId("existing.id")]` ưu tiên cao nhất, giữ ID cũ khi đổi tên/migrate. ID cần duy nhất trong registry.
- ResourceCheat/OwnershipCheat cung cấp ID/nhãn mặc định theo role. DisplayName ghi đè nhãn; Category/Sort định nhóm/thứ tự. CheatId không thay card ID.
- Typed CheatOption APIs vẫn hỗ trợ. Không đăng ký cùng option hai lần bằng hai authoring path.
- Nếu giá trị thay đổi bên ngoài, host có thể gọi `runtime.Cheats.NotifyValuesChanged()`; panel cũng có refresh giá trị bounded hiện có. Không đăng ký lại container mỗi frame.
- Giữ Runtime/game free of UnityEditor. Sample chỉnh được; Runtime package là phần bảo trì dùng chung.

## Trước khi giao cho QA

Kiểm tra locked/unconfirmed không mutation, numeric range, amount đổi khi dialog mở, owner disposed, save/ownership thật, card đầy đủ, enum/read-only đúng và build stripping chứa member. Editor compile/pass không thay cho build/chạy trên thiết bị.


## Preview.3 — registration và notification

`runtime.RegisterCheats(container, prefix)` thuộc Engine, trả lease sở hữu subscription INotifyPropertyChanged và được release khi runtime shutdown. `registry.RegisterContainer(container, prefix)` dùng cho registry độc lập; caller tự Dispose và tick FlushValueNotifications trên owner thread. Notification được coalesce qua RequestValueRefresh, kể cả worker thread; getters/UI không chạy ở worker. Pure From(...) chỉ tạo definitions, không tự subscribe.

Plain method không confirmation mặc định. Dùng CheatConfirmation cho destructive action; resource/ownership card grants luôn confirm. Typed Button requiresConfirmation giữ hành vi cũ. Cấu trúc registry có delta: giữ definition/control không đổi, recompose card khi role thay đổi, batch structural notifications để tránh rebuild toàn bộ UI.

Registry membership changes immediately; structural presentation updates coalesce until the next runtime tick. Stale bindings reject removed/replaced owners even if the same ID is reused. Keep metadata immutable while registered.
