# Dreamy Debugger

**0.3.0-preview.6 · Dreamy Game Studio · Internal preview**

Tool debug runtime cho Unity: Console, cheat theo nhóm, bảng hiệu năng và diagnostics có screenshot tùy chọn. Giao diện Dreamy đỏ/graphite; hỗ trợ màn hình ngang, dọc và safe area. Package độc lập, không bắt buộc Dreamy Core/UI/Economy/Datasave hoặc cài Graphy riêng.

- Unity tối thiểu khai báo: **6000.0**. Môi trường kiểm tra hiện tại: **6000.4.12f1 / Linux**.
- Cheat mặc định viết bằng **partial class + attribute**, giống cách tổ chức SROptions; mỗi game sở hữu container và service bindings riêng.
- Mặc định: **5 tap ở góc trên trái → PIN `0000`**, HUD tắt, upload và screenshot tự động tắt.
- Đây là candidate nội bộ. Xem [release/giới hạn kiểm chứng](Documentation~/RELEASE.md) trước khi đưa vào production.

## Mục lục

1. [Tính năng](#1-tính-năng)
2. [Import và chạy lần đầu](#2-import-và-chạy-lần-đầu)
3. [Cấu hình](#3-cấu-hình)
4. [Thêm cheat bằng partial và attribute](#4-thêm-cheat-bằng-partial-và-attribute)
5. [API runtime](#5-api-runtime)
6. [Screenshot và Telegram](#6-screenshot-và-telegram)
7. [Build, ownership và nâng cấp](#7-build-ownership-và-nâng-cấp)
8. [Lỗi thường gặp](#8-lỗi-thường-gặp)
9. [Tài liệu và trạng thái](#9-tài-liệu-và-trạng-thái)

## 1. Tính năng

| Nhóm | Có thể làm gì? |
|---|---|
| Console | Xem log/warning/error, timestamp, tìm kiếm, lọc severity, collapse, chọn xem stack trace và copy. Danh sách dùng virtualized rows; giữ lựa chọn/vị trí đọc khi có log mới. |
| Truy cập | Đổi số tap, PIN hoặc passphrase; cooldown khi nhập sai, hết phiên, khóa lại khi mất focus/pause/đổi scene. Mở bằng API vẫn đi qua access panel. |
| Cheat | Bool, số, enum, string, read-only và button có xác nhận. Category, tên hiển thị, sort, range, increment; tìm kiếm/lọc/pin/thu gọn nhóm. |
| Resource card | Gộp balance → amount → action trên một thẻ. Dialog xác nhận kiểm tra lại amount và owner trước khi thực hiện. |
| Ownership card | Hiện owned/read-only và nút grant; ẩn nút khi đã sở hữu. Game quyết định grant/save và guard cấp lặp. |
| Performance HUD | Off / FPS Only / Compact / Detailed; FPS, ms, average, 1%, .1%, memory và audio RMS tùy chọn. Mặc định góc dưới trái, kéo grip `::`, reset vị trí. |
| Performance | Chung một frame sampler, lịch sử có giới hạn, màu good/caution/critical và cảnh báo FPS thấp có sustain/cooldown. Frame-time không phải GPU render timing. |
| System | Thông tin build/Unity/thiết bị theo hàng key/value, copy từng section. |
| Diagnostics | Managed error snapshots, redaction, screenshot tùy chọn, queue trên đĩa, retention, retry/dead letters và text fallback. |
| Telegram | Client gửi tới relay; relay Python mẫu nhận bền vững bằng SQLite rồi chuyển Telegram. Chỉ hỗ trợ Telegram. |
| Editor | Window Settings, tạo bootstrap/config/demo, xem options, cấu hình HUD và công cụ fake report. Runtime không có tab report bug của package/author. |

Đổi collapse chỉ ảnh hưởng log mới; không bung lại những log lịch sử đã gộp. Getter cheat lỗi hiện `Unavailable` và có thể phục hồi. Catalog lớn tạo UI dần qua nhiều frame; vẫn cần profile thiết bị thật.

## 2. Import và chạy lần đầu

### 2.1. Cài package

Có thể dùng UPM Git URL của private repository, local package hoặc Git submodule. Với Git, pin một commit/tag đã kiểm tra; không đưa credential vào URL manifest. Với local package, chọn thư mục có `package.json` hoặc tham chiếu `file:` từ manifest của project.

Unity tự resolve các dependency uGUI/modules trong [package.json](package.json). Chờ compile hoàn tất.

### 2.2. Dùng ngay trong scene bất kỳ

1. Nhấn **Play**; mặc định Loading mode là **Automatic**.
2. Tap góc trên trái **5 lần**, nhập **0000**. Hoặc **Window → Dreamy Debugger → Cheat Panel → Open cheat panel in Game view**, rồi nhập PIN.
3. Console, Cheats, Performance và System đã sẵn sàng. HUD mặc định **Off ở lần đầu**; các thay đổi giao diện được nhớ trên máy/thiết bị đó.

Không cần import sample, tạo config hoặc đặt prefab vào scene để mở panel. Không có cheat sửa dữ liệu game tự đăng ký. Thêm các partial container của game bằng API ở mục 4.

### 2.3. Cấu hình riêng và sample tùy chọn

Mở **Window → Dreamy Debugger → Settings**. Overview chọn **Automatic / Manual**. Configuration có thể để trống để dùng defaults; tạo asset bằng **Create → Dreamy → Debugger → Configuration** và chọn vào ô **Project configuration** khi cần đổi PIN, policy hoặc defaults. Lựa chọn này lưu tại `ProjectSettings/DreamyDebuggerSettings.asset`.

**Manual**: thêm bootstrap vào startup scene bằng window, hoặc gọi `DebuggerBootstrap.Initialize(config, catalog)` trong game bootstrap khi Play. API vẫn áp dụng access/build policy. Bootstrap đã có trong scene được ưu tiên kể cả inactive, disabled hoặc Mode Off; auto không tự bật thay nó. Nếu scene load một bootstrap sau auto, runtime mới tiếp quản và giữ tùy chọn giao diện của phiên.

Quick Start trong Package Manager chỉ là **ví dụ có thể sửa**. Import sample nếu muốn tham khảo partial cheat, resource/ownership cards và transport giả. Overview → Optional sample có nút import editable sample. Demo scene có thể tạo bằng `DebuggerAssetBuilder.InstallSample()` trong Editor. Không thêm demo host vào game thật để thay game services.

Runtime sống qua scene bằng `DontDestroyOnLoad`. Chỉ cần một bootstrap nếu dùng Manual; không thêm vào mọi scene. Config được clone khi khởi tạo; sửa asset không đổi policy của phiên đang chạy. HUD và console thay đổi trực tiếp bằng runtime controls.

### 2.4. Các cửa sổ Editor

| Menu | Mục đích |
|---|---|
| Window → Dreamy Debugger → Settings | Các trang Overview, Access, Options, Performance, Diagnostics. |
| Window → Dreamy Debugger → Cheat Panel | Mở tab Cheats trong Game View khi Play. |
| Window → Dreamy Debugger → Telegram & Reports | Mở trang Diagnostics để cấu hình relay và thử report. |

Các mục Advanced để rebuild asset/visual là công cụ bảo trì package, không cần chạy sau mỗi import.

## 3. Cấu hình

Asset cấu hình: **Create → Dreamy → Debugger → Configuration**. Chọn asset trong cửa sổ Settings để cấu hình Automatic startup. Asset là tùy chọn.

### Access và build mode

| Field/API | Mặc định | Ý nghĩa |
|---|---|---|
| `Mode` | `Development` | Off / Development / Qa / ReleaseDiagnostics. |
| `RequiredTaps` | `5` | 1–20 tap trong trigger. |
| `TapGapSeconds` | `0.4` | Khoảng cách tối đa giữa hai tap. |
| `SequenceTimeoutSeconds` | `2.5` | Thời gian hoàn thành chuỗi tap. |
| `Credential` | `"0000"` | PIN 4–32 chữ số ASCII, giữ được số 0 đầu. |
| `Passphrase` | `false` | Bật để dùng chuỗi 8–64 ký tự thay PIN số. |
| `SessionSeconds` | `300` | Thời gian phiên unlock. |
| `ShowCornerTrigger` | `true` | Hiển thị trigger ở góc. |

`Development` và `Qa` có overlay khi Editor/development build; không dùng Qa như cách bật cheat trong release build thường. `ReleaseDiagnostics` giữ text diagnostics, không có cheat UI/HUD catalog. `Off` tắt runtime. PIN ngăn mở nhầm; không phải cơ chế bảo mật server hay chống sửa client.

### HUD và cảnh báo

| Field/API | Mặc định | Ý nghĩa |
|---|---|---|
| `InitialHud` | `Off` | Off / FpsOnly / Compact / Detailed. |
| `InitialHudPosition` | `TopRight` | TopRight / TopLeft / BottomRight / BottomLeft / Free. |
| `HudOffset`, `HudScale` | `(12,12)`, `1` | Offset và scale; scale 0.5–2. |
| `HudDragEnabled` | `true` | Kéo bằng grip `::`; phần còn lại không chặn touch game. |
| `HudMemory`, `HudAudio` | `true`, `false` | Memory và audio RMS tùy chọn. |
| `GoodFps`, `CautionFps` | `60`, `30` | Mốc màu; Good/Caution/Critical tùy chỉnh bằng `FpsGood/FpsCaution/FpsCritical`. |
| `LowFpsWarning` | `false` | Ghi warning khi FPS thấp kéo dài. |
| `LowFpsThreshold/Sustain/Cooldown` | `30 / 3 / 60` | Ngưỡng FPS / số giây liên tục / thời gian cooldown. |

HUD mặc định tắt và được ẩn khi panel mở. HUD mode/vị trí kéo/scale/memory/audio và drag được lưu local khi có thao tác người dùng, không ghi ngược vào config. Text cập nhật 4 Hz, memory 1 Hz, graph/audio tùy chọn 10 Hz; không tạo sampler thứ hai. Shader thiếu/không hỗ trợ thì fallback về text. Không coi số đo trong Editor là benchmark game trên điện thoại.

### Reports

| Field | Mặc định | Ý nghĩa |
|---|---|---|
| `AutoReportErrors` | `false` | Tạo report từ managed errors trong giới hạn phiên/cooldown. |
| `AllowScreenshots` | `false` | Cho phép ảnh khi caller/policy yêu cầu. |
| `UploadsEnabled` | `false` | Cho phép runtime tạo relay transport. |
| `RelayUrl` | rỗng | Endpoint `/reports` của relay; production dùng HTTPS. |
| `LimitedClientKey` | rỗng | Ingestion key giới hạn, không phải bot token. |
| `UploadDuringGameplay` | `false` | Cho phép upload tự động lúc game busy. |
| `ReportCooldownSeconds` | `60` | Cooldown report tự động. |
| `ScreenshotLongEdge` | `960` | Resize cạnh dài 240–960 trước encode. |

## 4. Thêm cheat bằng partial và attribute

### 4.1. Một container, nhiều file theo tính năng

Ví dụ cấu trúc ở project, tất cả cùng namespace và asmdef:

```text
Assets/Game/Debugging/
  GameCheatOptions.cs                # container, constructor nhận service
  GameCheatOptions.Economy.cs        # tài nguyên
  GameCheatOptions.Inventory.cs      # item/skin
  GameCheatOptions.Entitlements.cs   # No Ads/VIP/pass
  GameCheatOptions.Gameplay.cs       # time scale/level/testing
  GameCheatOptions.Diagnostics.cs    # thông tin chỉ đọc
  GameCheatHost.cs                   # đăng ký và Dispose
  link.xml                          # bảo toàn reflection khi stripping
```

Partial phải cùng **class, namespace, assembly**. Không extend partial trong Runtime package từ assembly của game. Runtime package là phần dùng chung; code sample đã import, game adapter, config và prefab variant là phần bạn được tùy chỉnh.

Nếu game dùng asmdef, tham chiếu `Dreamy.Debugger.Runtime`, `Dreamy.Debugger.Engine`, `Dreamy.Debugger.Presentation`. Assembly trực tiếp dùng uGUI types còn cần reference tương ứng; ví dụ cheat dưới đây không cần tự viết UI.

### 4.2. Ví dụ tối thiểu có thể dùng ngay

`GameCheatOptions.cs`:

```csharp
using System.Collections.Generic;
using Dreamy.Debugger;
using Dreamy.Debugger.Options;

namespace MyGame.Debugging
{
    public sealed partial class GameCheatOptions
    {
        public IEnumerable<CheatOption> CreateOptions() =>
            AttributedCheatOptions.From(this, "mygame");
    }
}
```

`GameCheatOptions.Gameplay.cs`:

```csharp
using System.ComponentModel;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
using UnityEngine;

namespace MyGame.Debugging
{
    public sealed partial class GameCheatOptions
    {
        [Category("Gameplay"), DisplayName("Time scale"), CheatId("mygame.time-scale")]
        [NumberRange(0.1, 3), Increment(0.1), Sort(0)]
        public float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }

        [Category("Gameplay"), DisplayName("Reset time scale"), Sort(1)]
        public void ResetTimeScale() => Time.timeScale = 1;
    }
}
```

Đăng ký trong bootstrap/feature của game, **sau khi runtime và service đã sẵn sàng**:

```csharp
// using Dreamy.Debugger; — extension RegisterCheats
var runtime = Dreamy.Debugger.DebuggerRuntime.Instance;
if (runtime != null)
{
    var options = new MyGame.Debugging.GameCheatOptions();
    var lease = runtime.RegisterCheats(options, "mygame");
    // Giữ lease trong field của owner; gọi lease.Dispose() khi owner disable/destroy.
}
```

Đây là đoạn tích hợp, không phải component tự chạy. Import sample để tham khảo host đầy đủ tại [ProjectCheatHost.cs](Samples~/QuickStart/ProjectCheatHost.cs). Host game cần đợi service game, tránh đăng ký lại mỗi frame và rebind nếu owner runtime thay đổi. RegisterCheats tự nối INotifyPropertyChanged (nếu có) và tự dọn khi runtime bị hủy; owner vẫn giữ và Dispose lease khi đóng feature. Quick Start default container là [ProjectCheats.cs](Samples~/QuickStart/ProjectCheats.cs) với các partial `.Player`, `.Economy`, `.QA`.

### 4.3. Các attribute và kiểu control

| Khai báo | Kết quả |
|---|---|
| Public bool property | Toggle. |
| Public numeric property | Number control; hỗ trợ int/long/float/double và các kiểu số được adapter hỗ trợ. |
| Public enum property | Dropdown. |
| Public string property | Text input. |
| Getter-only hoặc private setter | Chỉ đọc. |
| Public instance `void Method()` không tham số | Button chạy ngay sau kiểm tra access; thêm CheatConfirmation nếu cần dialog. |
| `[Category("Economy")]` | Nhóm hiển thị. |
| `[DisplayName("Add coins")]` | Nhãn hiển thị. |
| `[Sort(10)]` | Thứ tự trong nhóm. |
| `[NumberRange(1, 1000)]` | Giới hạn số khi ghi qua registry. |
| `[Increment(10)]` | Bước tăng/giảm. |
| `[Browsable(false)]` | Không đưa member vào panel. |
| `[CheatConfirmation]` | Yêu cầu dialog cho action thường; card grant luôn có confirmation. |
| `[CheatId("stable.id")]` | Giữ ID độc lập với tên C#. |
| `[ResourceCheat(id, title, role)]` | Gộp card tài nguyên. |
| `[OwnershipCheat(id, title, role)]` | Gộp card ownership. |

Category/DisplayName/Browsable dùng `System.ComponentModel`; NumberRange/Increment/Sort dùng `Dreamy.Debugger.Presentation`; CheatId/CheatConfirmation/ResourceCheat/OwnershipCheat dùng `Dreamy.Debugger.Options`. Helper nên private. Field, static và method có tham số không tự tạo control. Description tooltip chưa được triển khai; notification forwarding có qua API RegisterCheats/RegisterContainer.

### Notification và confirmation theo action

Container có thể implement `INotifyPropertyChanged`. Gọi PropertyChanged trong setter hoặc khi service báo thay đổi; host dùng RegisterCheats sẽ tự subscribe/unsubscribe theo lease. Notification được gộp và chuyển thành refresh ở tick main thread tiếp theo, không gọi UI từ worker. Vẫn giữ polling 4 Hz làm fallback; không phải quét reflection mỗi frame.

```csharp
using System.ComponentModel;

public sealed partial class GameCheatOptions : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private int testAmount;
    public int TestAmount
    {
        get => testAmount;
        set
        {
            if (testAmount == value) return;
            testAmount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TestAmount)));
        }
    }
}
```

Đặt ví dụ này trong **cùng namespace** với các partial của game. Getter chạy trên main thread; service của game vẫn phải quản lý thread-safety của dữ liệu. API thấp `Register(AttributedCheatOptions.From(...))` không tự subscribe notification; dùng RegisterCheats/RegisterContainer khi cần cơ chế này.

Action thường chạy ngay. Gắn `[CheatConfirmation]` cho thao tác nguy hiểm; resource/ownership card grants luôn xác nhận, không cần lặp attribute. Quyền access luôn được kiểm tra dù có dialog hay không. Ví dụ destructive method:

```csharp
[CheatConfirmation]
public void ResetTestSave() { /* Gọi service reset của game. */ }
```

Thành viên registry đổi ngay; presentation/UI gộp thay đổi ở tick kế tiếp. Khi thêm/bỏ option, bridge giữ definition/control không đổi; chỉ dựng lại card bị ảnh hưởng khi đủ/thiếu role. DynamicOptionContainer có BeginUpdate() để gộp add/remove thành một batch. Batch không thay cho lease cleanup hoặc kiểm tra quyền.

### 4.4. Resource card và ownership card

Tham khảo mẫu có delegate binding hoàn chỉnh: [GameDebugOptions.cs](Samples~/QuickStart/GameDebugOptions.cs) và [GameDebugOptions.Economy.cs](Samples~/QuickStart/GameDebugOptions.Economy.cs).

| Card | Các member cần có | ID mặc định sinh ra |
|---|---|---|
| `ResourceCheat("mygame.coins", "Coins", role)` | Balance numeric chỉ đọc, Amount numeric có setter, Action | `mygame.coins.balance`, `.amount`, `.grant` |
| `OwnershipCheat("mygame.no-ads", "No Ads", role)` | Owned bool chỉ đọc, Action grant-only | `mygame.no-ads.owned`, `.unlock` |

Role lấy từ `CheatCardRole.Balance/Amount/Action/Owned`. Các member cùng card phải có ID/title/category nhất quán. Card thiếu role sẽ fallback sang control thông thường; role/type sai làm đăng ký thất bại. `[CheatId]` ghi đè option ID nhưng không đổi card ID; `[DisplayName]` ghi đè nhãn mặc định.

Nối balance/grant/owned vào wallet, save và ads/purchase service của game. Package không cung cấp economy hoặc SDK quảng cáo. Ownership action cần guard `if (!owned)` ở phía game; UI không thay cho luật cấp thưởng. Không dùng cheat để giả IAP receipt hoặc thu hồi giao dịch mua thật.

### 4.5. ID, vòng đời và IL2CPP

- Không có CheatId/card metadata: ID là `<prefix>.<MemberName>`. Đổi tên member sẽ đổi ID.
- Giữ ID ổn định cho pin, test và automation. ID trùng làm registration rollback, không thêm một phần.
- Mỗi lần Register trả `IDisposable`: giữ lease và Dispose khi owner đóng. Không Register typed và attributed path cho cùng ID.
- Khi giá trị thay đổi ngoài registry, gọi `NotifyValuesChanged()`; không cần dựng lại catalog. Panel có refresh giá trị bounded hiện có.
- Reflection cần giữ member khi stripping. Copy [link.xml](Samples~/QuickStart/link.xml), sửa đúng assembly/type của game, ví dụ:

```xml
<linker>
  <assembly fullname="MyGame.Debugging">
    <type fullname="MyGame.Debugging.GameCheatOptions" preserve="all" />
  </assembly>
</linker>
```

Assembly fullname là `name` trong asmdef, không mặc định bằng namespace. Editor pass không chứng minh IL2CPP/device runtime; kiểm tra build sau khi đổi reflected types. Xem [hướng dẫn chi tiết](Documentation~/CHEAT_OPTIONS.md).

## 5. API runtime

### 5.1. Panel và access

Namespace chính: `Dreamy.Debugger`; enum tab ở `Dreamy.Debugger.Presentation`.

```csharp
using Dreamy.Debugger;
using Dreamy.Debugger.Presentation;

var runtime = DebuggerRuntime.Instance;
if (runtime != null)
{
    runtime.Open(DefaultTabs.Options);  // Nhãn UI: Cheats; vẫn kiểm tra PIN/access.
    runtime.SetHud(HudMode.Compact);
    runtime.Relock();                   // Khóa lại và đóng panel.
}
```

| API | Ý nghĩa |
|---|---|
| `DebuggerRuntime.Instance` | Runtime hiện tại; có thể null khi chưa ready hoặc mode/build không cho phép. |
| `Open(DefaultTabs tab = DefaultTabs.Console)` | Mở panel ở tab Console / Options / Profiler / SystemInformation. Nhãn UI là Cheats/Performance nhưng key giữ Options/Profiler. |
| `Relock()` | Hủy prompt đang có, khóa access và ẩn panel. |
| `CanCheat` | Runtime có presentation hợp lệ và phiên access đang unlock. Không thay cho quyền server. |
| `Access.IsUnlocked` | Trạng thái unlock hiện tại. |
| `Access.Tap(int pointerId = 0)` | Custom input adapter gửi tap đã nằm trong trigger hợp lệ. |
| `Access.SubmitPin(string)` | Trả UnlockResult: Unlocked / InvalidPin / GestureRequired / Cooldown. |
| `Access.RequestPrompt()` | Hỗ trợ custom prompt của host; gọi trực tiếp bỏ qua gesture, chỉ dùng khi host chủ động muốn hành vi đó. |
| `Confirm(string, Action, Func<bool> validate = null)` | Mở dialog xác nhận; callback có kiểm tra access, validator có thể từ chối khi state đổi. |
| `DreamyDebug.Instance?.HideDebugPanel()` | Ẩn panel; không đồng nghĩa với Relock. DreamyDebug là static class global. |

Custom input cần tự quản vùng trigger và tránh gửi trùng với input có sẵn. Built-in panel xử lý PIN/access nên đa số game chỉ cần `runtime.Open(...)`.

### 5.2. Cheat registry

`runtime.Cheats` có kiểu `Dreamy.Debugger.Options.CheatRegistry`.

| API | Contract |
|---|---|
| `runtime.RegisterCheats(object container, string prefix)` | API Engine khuyến nghị; lease sở hữu notification và được dọn khi runtime hủy. |
| `RegisterContainer(object container, string prefix)` | Extension trên registry cho integration độc lập; phải Dispose lease. |
| `Register(IEnumerable<CheatOption>)` | Đăng ký atomic, trả IDisposable lease. |
| `Snapshot()` | Danh sách option đã sort theo category/order/display name. |
| `Read(string id)` | Đọc giá trị; vẫn kiểm tra access. |
| `Set(string id, object value)` | Kiểm tra access, đúng kiểu/range rồi gọi setter. |
| `Execute(string id, bool confirmed = false)` | Gọi action; action cần confirmation sẽ từ chối nếu confirmed=false. |
| `RequestValueRefresh()` / `FlushValueNotifications()` | Gộp request kể cả từ worker thread; flush chỉ ở owner thread. Runtime xử lý tự động; registry độc lập cần tự tick. |
| `NotifyValuesChanged()` | Báo giá trị ngoài registry vừa thay đổi; không đổi danh sách option. |
| `Changed`, `ValuesChanged` | Event cấu trúc/giá trị; subscriber lỗi được cách ly. |

Automation phải tự có bước xác nhận trước `Execute(id, confirmed: true)`; cờ này không mở dialog. `Set` cần đúng boxed type, ví dụ `100L` cho long, `1.5f` cho float. Sai quyền → UnauthorizedAccessException; sai kiểu/range/ID → exception tương ứng.

API typed vẫn có cho integration nâng cao:

```csharp
using Dreamy.Debugger.Options;

var option = CheatOption.Value("game.speed", "Gameplay", "Speed",
    () => model.Speed, value => model.Speed = value, minimum: 0.1, maximum: 3);
var action = CheatOption.Button("game.reset", "Gameplay", "Reset",
    ResetTestState, requiresConfirmation: true);
```

`model`/`ResetTestState` là state/hàm của game cần tự cung cấp. Getter không có setter tạo read-only. `WithPresentation(order, increment)` và `WithCard(id, title, kind, role)` thêm metadata; thay metadata sau đăng ký cần re-register. Authoring thông thường nên dùng partial/attribute ở mục 4.

### 5.3. HUD

```csharp
runtime.SetHud(HudMode.Detailed);               // Lazy-create HUD khi cần.
runtime.Hud?.SetPosition(HudPosition.BottomLeft);
runtime.Hud?.ResetPosition();
runtime.SetHud(HudMode.Off);
```

`Hud` có thể null trước lần bật đầu. `SetHud` không bật presentation trong Off/ReleaseDiagnostics hoặc build không cho phép. Thay đổi bằng API là ý định người dùng và được lưu local khi PersistRuntimePreferences=true; không sửa config asset.

### 5.4. Report và capture policy

| API | Ý nghĩa |
|---|---|
| `QueueReportAsync(string description, bool synthetic=false, bool screenshot=false, bool sendImmediately=false)` | Tạo report; trả Task<bool>. True nghĩa lưu được vào outbox, không bảo đảm Telegram đã nhận. |
| `CreateAndSendFakeReportAsync()` | Tạo report `[FAKE]`, yêu cầu screenshot và thử gửi ngay. Ảnh vẫn cần AllowScreenshots. Đây là gửi thật nếu relay live. |
| `FlushPendingAsync()` | Thử xử lý queue ngay; không phải bảo đảm gửi hết/mọi destination đã nhận. |
| `GameplayBusy` | Mặc định true; game đặt false lúc idle/menu nếu muốn automatic upload. |
| `Status` | Trạng thái thao tác gần nhất để hiển thị/kiểm tra. |
| `LastManualReportId` | ID của report có yêu cầu gửi ngay gần nhất. |
| `GetComponent<CaptureCoordinator>().SensitiveScreen` | Delegate trả true để từ chối capture màn hình nhạy cảm. Không phải region masking hoặc consent manager. |

Gọi Unity-facing API ở main thread. Ví dụ trong async method của host:

```csharp
bool saved = await runtime.QueueReportAsync(
    "QA: UI không cập nhật sau khi nhận thưởng", screenshot: true);
// saved chỉ xác nhận lưu local. Xem runtime.Status để biết trạng thái gần nhất.
```

Trong menu/idle, host có thể đặt `runtime.GameplayBusy = false`; khi vào trận đặt lại true. Cả `sendImmediately` và Flush là yêu cầu chủ động, không dùng chúng mỗi frame để ép upload qua chính sách idle. Khi UploadsEnabled=false/không có transport, report vẫn có thể lưu local; Flush không cung cấp bằng chứng delivery.

### 5.5. Log, System info và API presentation

Dùng logging Unity hiện có để đưa vào Console:

```csharp
UnityEngine.Debug.Log("Scene ready");
UnityEngine.Debug.LogWarning("Asset load chậm hơn budget");
// Debug.LogError/LogException cũng được thu nhận; nếu bật AutoReportErrors,
// chúng có thể tạo report thật theo policy của phiên.
```

`DreamyDebug.Instance` cung cấp các API presentation bổ sung:

| API | Công dụng |
|---|---|
| `IsDebugPanelVisible` | Đọc panel đang mở hay đóng. |
| `IsTriggerEnabled` | Bật/tắt trigger của presentation. |
| `IsTriggerErrorNotificationEnabled` | Bật/tắt thông báo error ở trigger. |
| `AddSystemInfo(InfoEntry entry, string category = "Default")` | Thêm thông tin game vào System tab. |
| `PinAllOptions(string category)` | Pin các option trong category. |

Đây là API presentation, không phải nơi cấp quyền game/server. API legacy `AddOptionContainer`/`AddOption` vẫn tồn tại để compatibility; luồng cheat mới dùng `runtime.RegisterCheats` (nội bộ chuyển qua adapter và registry) để giữ chung authorization, confirmation và lifetime. Tham khảo [IDebugService](Runtime/Imported/Presentation/Scripts/Services/IDebugService.cs) và [InfoEntry](Runtime/Imported/Presentation/Scripts/Services/ISystemInformationService.cs) khi mở rộng presentation.

### 5.6. Tích hợp crash evidence từ SDK ngoài

Namespace `Dreamy.Debugger.Reporting` có `ICrashEvidenceProvider`:

- `CollectPreviousAsync(CancellationToken)` trả danh sách `CrashEvidence` ở lần mở game sau.
- `AcknowledgeAsync(string sourceId, CancellationToken)` xác nhận evidence đã được nhận bền vững.
- `CrashEvidenceImporter.ImportAsync(provider, runtime.Outbox, buildId, token)` đưa evidence vào outbox, trả số evidence đã nhận; importer chỉ acknowledge sau khi lưu local thành công.

Game tự cung cấp adapter SDK và cancellation/lifecycle owner. Package không tự cài Firebase Crashlytics hoặc native crash SDK. Import là bước lưu evidence, không phải lời hứa có screenshot lúc crash hay đã chuyển Telegram. Xem [contract/source](Runtime/Reporting/CrashEvidence.cs) và [platform support](Documentation~/PLATFORM_SUPPORT.md).

### 5.12. Khởi động và local preferences

```csharp
// Automatic đã là mặc định; API này dành cho Manual/custom bootstrap.
var runtime = DebuggerBootstrap.Initialize(); // Idempotent, trả null nếu policy không cho phép.
runtime.SetHud(HudMode.Compact);
runtime.SetHudPosition(HudPosition.BottomLeft);
runtime.SetHudScale(1.25f);
runtime.SetPreference("hud.memory", true);
runtime.SetPreference("fps.warning", true);
runtime.SetPreference("fps.threshold", 30f);
runtime.SetFpsColor("fps.good-color", "#76D43B");
await runtime.FlushPreferencesAsync(); // Không block game thread.
runtime.ResetLocalPreferences();      // Khôi phục defaults; không xóa outbox/save.
```

`PersistRuntimePreferences` mặc định true; false giữ tùy chỉnh trong phiên. `PreferenceRevision` mặc định 1; tăng giá trị nếu muốn một scope mới sau khi thay defaults của dự án. Không tăng theo mỗi build/version. Config defaults → local preferences → thao tác mới trong phiên; build/access policy luôn thắng.

Lưu: HUD mode/corner/normalized free position/scale/memory/audio/drag; FPS thresholds/colors/warning; console severity/collapse; category collapse và pin stable IDs. Pin khôi phục sau unlock và khi option xuất hiện, không chạy action hoặc sửa giá trị cheat.

Không lưu: PIN, unlock, token, relay/report policy, log/search text, cheat values, resource amount/balance, entitlement, time scale hoặc target FPS. File JSON schema 1 tối đa 32 KiB/160 mục, namespace theo app/channel/project Editor/revision; nằm trong `Application.persistentDataPath/DreamyDebugger/Preferences`. IO chạy worker, debounce 0.5 s và một writer cho mỗi đường dẫn; drag lưu ở cuối thao tác. Pause/focus loss/quit yêu cầu flush không chặn. Hard kill ngay trước khi ghi có thể mất thay đổi mới nhất. Primary hỏng/oversize thử backup; schema chưa biết dùng defaults. File không ghi được thì UI vẫn dùng được trong phiên, store thử lại sau; `Preferences.LastError` cho biết lỗi gần nhất.

HUD không nháy defaults trong lúc đang load. Ẩn khi capture/resize/teardown không trở thành preference Off mới. `ResetLocalPreferences()` chỉ đặt lại prefs, không đổi config hoặc đụng outbox/game save. Chỉ các key allowlist được ghi. Dùng API typed cho mode/position/scale; `SetPreference` bool/number và `SetFpsColor` dành cho những key nêu trên.

## 6. Screenshot và Telegram

### Local trong Unity Editor

1. Mở **Window → Dreamy Debugger → Telegram & Reports**.
2. Dùng command setup/discover do window cung cấp. Chạy command trong terminal riêng; nhập **BotFather HTTP API token** ở prompt ẩn. Bot token không phải username, chat ID hoặc Telegram api_id/api_hash.
3. Thêm bot vào nhóm/kênh hoặc `/start` private bot; lấy chat ID bằng `telegram_setup.py --discover-chat`. Link mời nhóm không phải chat ID.
4. Chat ID có thể lưu trong Editor SO `Assets/Editor/DreamyDebugger/TelegramRelaySettings.asset`; không lưu bot token vào SO.
5. Chạy launcher `telegram_setup.py`, chờ `Relay ready in background`.
6. Config runtime: `UploadsEnabled=true`, `RelayUrl=http://127.0.0.1:8765/reports`. Muốn ảnh thì thêm `AllowScreenshots=true`. Restart Play.
7. Bấm **Create + send [FAKE] screenshot report** để kiểm tra ảnh Game View; hoặc **Simulate delivery — no network** nếu chỉ muốn giả lập kết quả mà không gửi.

Nút **Upload queued reports now** có thể xử lý cả report tồn trước đó. `[FAKE]` chỉ là nhãn synthetic, không làm live relay ngừng gửi. Bot token và destination routing thuộc relay; client Unity không giữ bot token.

### Trên điện thoại/production

- Điện thoại phải truy cập được HTTPS relay thật. `127.0.0.1` trên điện thoại là chính điện thoại, không phải máy Editor.
- Deploy relay với storage bền vững và cấu hình Telegram ở server. Chỉ đưa endpoint và limited ingestion key vào game.
- Report được relay nhận bền vững chưa có nghĩa Telegram đã giao thành công; destination có retry/trạng thái riêng.
- Host xử lý thông báo/đồng ý áp dụng, màn hình nhạy cảm, dữ liệu cá nhân, retention và vận hành. Screenshot chỉ lấy nội dung game; không có region masking/consent-withdrawal tự động.
- Capture/encode/send có giới hạn và fallback text, nhưng vẫn cần đo frame-time/GC trên thiết bị yếu. Native crash/ANR/OOM hoặc process đã chết không bảo đảm có callback/ảnh mới.


## 7. Build, ownership và nâng cấp

| Nội dung | Ai bảo trì/chỉnh? |
|---|---|
| Runtime, Editor, base prefabs/catalog của package | Phần package dùng chung. |
| Samples sau khi import vào Assets | Người dùng/game sửa theo nhu cầu. |
| GameCheatOptions, host, service bindings | Game sở hữu; không sửa Runtime package cho từng game. |
| Config/prefab variants | Game sở hữu; kiểm tra override khi nâng base assets. |
| Bot token, routing và relay destination | Server/Editor local setup; không nhúng vào APK. |

Không có dependency bắt buộc Dreamy Core/UI/Datasave/Feature/Assets, Graphy package ngoài hoặc Input System package. Adapter Input System tùy chọn; code cheat có thể gọi service của game nếu game chọn dùng Core/Economy.

Non-development build strip Development/Qa debugger roots; ReleaseDiagnostics bỏ UI catalog và giữ text diagnostics. Kiểm tra đúng mode/build trước khi kết luận panel bị hỏng. PIN và client ingestion key đều có thể đọc từ client build, không dùng chúng làm quyền cấp reward/purchase trên server.

Khi update: giữ meta GUID, merge sample đã tùy chỉnh, kiểm tra prefab variants và link.xml. Không regenerate toàn bộ meta, không thay font dùng chung của game. Standalone copy của panel/HUD cần đối chiếu field mới với base cùng phiên bản. [MIGRATION.md](Documentation~/MIGRATION.md) mô tả thay đổi từng candidate.

## 8. Lỗi thường gặp

| Hiện tượng | Kiểm tra |
|---|---|
| Import xong chưa có menu | Compile errors, DISABLE_DREAMY_DEBUGGER, đúng Unity/dependencies và package đã resolve. |
| Mở window được nhưng không có panel | Có đang Play? Loading Automatic hay Manual? Bootstrap inactive/Off trong scene có đang chủ động chặn auto? Mode/build có cho phép UI? |
| Không có cheat game | Host đã chạy sau runtime/service ready chưa? Container đã Register? Có dispose sớm/ID trùng không? |
| Hiện cheat mẫu thay vì tài nguyên game | Thay ProjectCheatHost demo bằng host nối wallet thật; tránh hai host cùng đăng ký catalog. |
| PIN không nhận | Numeric PIN 4–32 ASCII digits hoặc bật Passphrase cho 8–64 ký tự; restart Play sau đổi config. |
| Không thấy HUD | Mặc định Off; bật trong Performance hoặc SetHud. HUD bị ẩn khi panel đang mở. |
| Option hiện Unavailable | Getter đang lỗi hoặc service đã bị hủy; sửa binding/owner. |
| Build IL2CPP thiếu option | Kiểm tra link.xml đúng assembly/type và reflected public members còn tồn tại. |
| Thẻ bị tách thành nhiều control | Kiểm tra đủ role, cùng card ID/title/category, và đúng kiểu read-only/writable. |
| Report có log nhưng không có ảnh | AllowScreenshots, screenshot request, sensitive-screen gate, GPU/low-memory hoặc capture fallback. |
| Local report có nhưng không gửi | UploadsEnabled, RelayUrl, relay ready, network, GameplayBusy/idle policy; xem Status và relay state. |
| Điện thoại không gửi về relay local | Không dùng localhost của máy Editor làm URL trên điện thoại; deploy endpoint truy cập được. |
| Gửi xong chưa thấy Telegram | Relay acceptance và destination delivery khác nhau; kiểm tra bot/chat permissions và relay retries. |

## 9. Tài liệu và trạng thái

- [Authoring partial/attribute](Documentation~/CHEAT_OPTIONS.md)
- [Migration](Documentation~/MIGRATION.md), [Changelog](CHANGELOG.md)
- [Reporting](Documentation~/REPORTING.md), [relay](Tools~/relay/README.md)
- [Platform support](Documentation~/PLATFORM_SUPPORT.md), [acceptance](Documentation~/ACCEPTANCE.md)
- [Release và evidence scope](Documentation~/RELEASE.md), [tracking](Documentation~/TRACKING.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

Xem receipt đúng preview.3 trong tài liệu release; receipt preview.2 là lịch sử. Đây không phải chứng nhận touch/DPI/rotation/IME, GPU capture, native crash, p95/GC/thermal hoặc iOS trên thiết bị thật. Build của sample cũng không thay cho IL2CPP/runtime test container riêng của game.

Internal use only; giữ các notice và điều kiện upstream. Giao diện có phần renderer/font Graphy theo license được lưu trong package, không yêu cầu cài Graphy riêng.

Điều khiển HUD nằm trong tab **Performance** của panel runtime: **HUD Off / FPS / Compact / Detail**. Không còn option HUD trong Cheats. Mặc định góc dưới trái; kéo grip để đặt tự do và nhớ vị trí đã kéo. Các field/API nâng cao giữ lại để tương thích, không hiện dày đặc trong giao diện. **Settings → Access → Tap trigger position** chọn tám vị trí quanh mép màn hình. Sidebar luôn đóng lúc mở panel.

Console details retain the message and stack trace Unity supplies. Normal logs scroll directly; very long details expose Previous/Next pages (12,000 characters per page), and Copy includes the entire retained entry. The intake/history memory budgets stay bounded at 2 MiB each: oversized entries or a full incoming queue increment DroppedCount instead of silently clipping a stack trace. HUD mode controls sit below the Performance banner, inside its scrolling content.

Để ghim cheat: trong **Cheats**, bấm icon ghim nhỏ ở góc phải header mỗi category, cạnh nút thu gọn. Chưa ghim hết → ghim cả nhóm; ghim hết → bỏ ghim cả nhóm. Icon xám: chưa ghim; vàng: ghim một phần; đỏ: ghim đủ. Chỉ icon đổi trạng thái, không phủ màu lên các cheat. **Only pinned** chỉ lọc những mục đã ghim. Thao tác áp dụng cho cả category, kể cả option đang ẩn bởi tìm kiếm/thu gọn; không chạy action hoặc đổi giá trị game.

Debugger UI uses Geist Sans + Geist Mono v1.9.1 (OFL) and a flat Kenney UI Pack PNG surface (CC0), loaded through its catalog and styled once per clone. Theme colors and existing icon art are retained. See Runtime/Branding/Interface/SOURCES.md for provenance. The empty search state is centered inside the results viewport below the toolbar.

HUD cards use a 70% opaque graphite background with fully legible Geist Mono labels; only the drag grip receives HUD input. Confirmation/PIN and debugger-created legacy modal/popover surfaces share Geist, flat panels, neutral secondary controls and lighter dimmers. This styling does not add blur, change game-owned popups, or enable the disabled reporter UI.
