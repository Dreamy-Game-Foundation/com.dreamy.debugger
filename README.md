# Dreamy Debugger

**v0.1.6 · Dreamy Game Studio · Unity 6000.0+**

Debugger runtime độc lập cho Unity, gồm Console, cheat theo nhóm, Performance HUD, thông tin hệ thống và diagnostics. Hỗ trợ màn hình ngang/dọc và safe area; không cần Dreamy Core/UI hoặc cài Graphy riêng.

## Cài đặt và sử dụng

Hướng dẫn chi tiết có ảnh UI thật và animation tương tác, mở offline: [Cheats & Screenshot — Quick Guide](Documentation~/QUICK_GUIDE.html).

1. Package Manager → **+ → Install package from Git URL**:
   ```text
   https://github.com/Dreamy-Game-Foundation/com.dreamy.debugger.git#v0.1.6
   ```
   Với local package, chọn **Install package from disk** → `package.json`.
2. Chờ compile rồi nhấn **Play**. Mặc định debugger tự khởi động, không cần sample hoặc prefab trong scene.
3. **Tap 5 lần ở góc trên trái → nhập PIN `0000`**. Có thể mở từ **Window → Dreamy Debugger → Cheat Panel → Open cheat panel in Game view**.

## Tính năng chính

- **Console:** tìm kiếm, lọc severity, collapse, xem stack trace và copy log; hỗ trợ dock nổi.
- **Cheats:** bool, số, enum, string, read-only, button; tìm kiếm, ghim category và xác nhận thao tác. Có card tài nguyên và ownership.
- **Performance:** FPS, frame time, memory và lịch sử; HUD kéo được, chạm để đổi chế độ, nút × để tắt.
- **System & diagnostics:** thông tin build/thiết bị, report lưu cục bộ, screenshot tùy chọn và gửi Telegram qua relay.
- **Screenshot Mode:** ẩn debugger và UI gameplay tùy chọn, chụp PNG ở độ phân giải Game View/thiết bị, lưu cục bộ và khôi phục UI bằng phím tắt hoặc cử chỉ.

## Giao diện

Ảnh render thật từ Unity trong bộ test v0.1.3, sử dụng dữ liệu giả lập; không phải ảnh chụp trên điện thoại thật.

**Cheats:** card tài nguyên, tìm kiếm/lọc, ghim category và sidebar đóng/mở.

![Panel Cheats của Dreamy Debugger](Documentation~/screenshots/material-cheats-852x393.png)

| HUD và category đã ghim | Keypad nhập PIN |
|---|---|
| ![HUD trong suốt và chip ghim gọn](Documentation~/screenshots/material-pinned-hud-852x393.png) | ![Keypad PIN với Cancel và Delete riêng](Documentation~/screenshots/pin-640x300.png) |
| HUD kéo tự do, chạm vùng số để đổi chế độ, nút × để tắt; category ghim thu thành chip nhỏ. | Keypad căn giữa, hỗ trợ PIN dài; nút huỷ và xoá ký tự riêng biệt. |

<details>
<summary><strong>Cheats màn dọc — xem bố cục tự xuống hàng</strong></summary>

Sidebar đóng để dành chỗ cho nội dung; các card tài nguyên xếp dọc theo chiều rộng màn hình.

<img src="Documentation~/screenshots/material-cheats-393x852.png" alt="Cheats màn dọc 393×852" width="240" />

</details>

| Category ghim khi mở rộng | Console dock |
|---|---|
| ![Category ghim mở rộng trong vùng cuộn](Documentation~/screenshots/material-pinned-expanded-852x393.png) | ![Console dock với bộ lọc và nút đóng](Documentation~/screenshots/material-dock-852x393.png) |
| Chạm chip để mở control; cuộn vùng ghim để xem phần bên dưới. | Ghim log nổi, lọc severity, thu gọn hoặc đóng dock; dùng chung nguồn log Console. |

## Setup trong project

### 1. Chọn cách khởi động

Mở **Window → Dreamy Debugger → Settings → Overview**:

| Loading mode | Cách dùng |
|---|---|
| **Automatic** (mặc định) | Play trong scene bất kỳ; package tự tạo runtime. Không cần import sample, đặt prefab hoặc gọi API khởi tạo. |
| **Manual** | Game quyết định thời điểm khởi tạo: thêm bootstrap vào startup scene bằng window, hoặc gọi `DebuggerBootstrap.Initialize(configuration)` từ game bootstrap khi đang Play. |

Runtime sống qua scene bằng `DontDestroyOnLoad`. Chỉ dùng một bootstrap; không đặt vào mọi scene. Bootstrap đã authored trong scene được ưu tiên kể cả inactive, disabled hoặc Mode Off; Automatic không tự bật thay nó. Không cần chạy Setup Theme hoặc rebuild assets để bắt đầu.

Nếu dùng Manual bằng code, gọi sau khi game đã vào Play:

```csharp
using Dreamy.Debugger;

// configuration là DebuggerConfiguration của project; truyền null để resolve defaults.
var runtime = DebuggerBootstrap.Initialize(configuration);
// runtime có thể null nếu Mode Off hoặc build policy không cho phép chạy.
```

### 2. Tạo configuration riêng

1. **Create → Dreamy → Debugger → Configuration** để tạo asset trong project.
2. Vào **Window → Dreamy Debugger → Settings**, gán asset vào **Project configuration**.
3. Chỉnh policy/defaults trên asset; Stop/Play lại hoặc build lại để áp dụng. Runtime clone configuration khi khởi tạo, không đọc lại policy từ asset mỗi frame.

Lựa chọn configuration lưu tại `ProjectSettings/DreamyDebuggerSettings.asset`; commit file này và asset/config `.meta` để các thành viên dùng cùng cấu hình. Nếu đã có bootstrap trong scene, kiểm tra configuration được gán trên bootstrap đó.

| Trường | Ý nghĩa / mặc định |
|---|---|
| `Mode` | `Development` mặc định; `Off` tắt runtime. `Development`/`Qa` chỉ khởi tạo trong Editor hoặc Development Build. `ReleaseDiagnostics` cho phép diagnostics ở build thường, không tạo panel/HUD và không cho chạy cheat. |
| `RequiredTaps`, `TapPosition` | Số tap và góc kích hoạt; mặc định **5 tap, góc trên trái**. |
| `Credential`, `Passphrase` | PIN mặc định **0000**; bật Passphrase nếu cần nhập chuỗi thay PIN. |
| `SessionSeconds` | Thời gian phiên unlock; mặc định 300 giây. |
| `ShowCornerTrigger` | Bật vùng nhận tap vô hình ở góc cấu hình. |
| `InitialHud`, `InitialHudPosition` | HUD mặc định Off, vị trí dưới trái. |
| `PersistRuntimePreferences` | Nhớ lựa chọn giao diện trên từng máy/thiết bị; preference đã lưu có thể ghi đè defaults HUD. |
| `AutoReportErrors`, `AllowScreenshots`, `UploadsEnabled` | Mặc định tắt; chỉ bật khi đã cấu hình reporting phù hợp. |

### 3. Kiểm tra lần đầu và tắt khi cần

1. Play → tap góc đã chọn đủ số lần → nhập PIN → mở được Console, Cheats, Performance và System.
2. Cheats chưa có option game là bình thường: game hoặc sample cần đăng ký container riêng.
3. Performance → chọn HUD; kéo vị trí, chạm vùng số để đổi chế độ, nút × để tắt.
4. Đóng panel chỉ đóng giao diện. Gọi `DebuggerRuntime.Instance?.Relock()` để khóa lại; phiên cũng khóa khi mất focus/pause.
5. Muốn tắt runtime: đặt **Mode = Off** trong configuration đang dùng rồi restart. Chọn **Manual** chỉ dừng tự khởi động, bootstrap authored vẫn có thể chạy.

## Screenshot — tab riêng cho ảnh gameplay

Mở debugger bằng PIN → chọn tab **Screenshot** có icon máy ảnh. Không còn điều khiển Screenshot trong Cheats. Từ **0.1.6**, Screenshot mode, lựa chọn UI trong ảnh và Hide UI là ba điều khiển độc lập.

### Chụp một ảnh hoặc chụp tiếp

1. Chọn **Keep gameplay UI**, **Hide all Canvas UI** hoặc **Hide selected UI groups**. Debugger luôn được ẩn khỏi ảnh.
2. Chọn **Now / 3s / 5s**; mặc định ba giây. Hai nút Capture và Hide nằm cố định ở chân tab, kể cả màn hình ngang ngắn.
3. Bấm **Capture**. Panel đóng, gameplay tiếp tục; khi hết đếm ngược hệ thống ẩn UI được chọn và chụp PNG.
4. UI được khôi phục; thẻ thumbnail có **View / Again / Close**. View mở ảnh trong app; Again dùng lại lựa chọn trước đó.
5. Trong tab Screenshot, phần ảnh gần nhất có **View image / Capture again**, cùng các thao tác lấy ảnh phù hợp nền tảng.

Để chọn từng nhóm, thêm **ScreenshotHideTarget** vào HUD/controls/popup root trong project; trường **Group Name** là tên dễ đọc trong tab. Tick nhóm cần ẩn; nhóm không tick được giữ nguyên. Nếu chưa đăng ký nhóm hoặc chưa chọn nhóm nào, tab hướng dẫn và khóa thao tác chụp của lựa chọn này. CanvasGroup con có `ignoreParentGroups`, Canvas lồng nhau, popup và target mới vẫn được xử lý. Mỗi target mới mặc định được chọn.

### Bật mode để chụp ngay, không cần Hide UI

**Screenshot mode** mặc định OFF. Bật **Screenshot mode: ON**, chọn **UI IN CAPTURED PNG**, rồi chạm nhanh góc trên phải trong safe area để chụp. Không cần bấm Hide UI trước. Chạm góc chụp bỏ qua Capture Delay; Capture button vẫn dùng Now / 3s / 5s. Khi mode ON, không mở preview sau từng ảnh để có thể chụp tiếp.

Góc phải báo **Tap received / Capturing…** rồi ẩn phản hồi trước frame chụp; lưu xong mới báo **Saved** hoặc lỗi. Vùng chụp không chặn gameplay input: control game bên dưới có thể cùng nhận lần chạm. Tắt mode để ngừng nhận tap góc; chế độ này không tự ẩn UI.

### Hide UI là thao tác riêng

Trong **HIDE UI**, chỉ chọn **Hide all UI** hoặc **Hide selected**; không có Keep. Lựa chọn này độc lập với preset của ảnh chụp. Ví dụ: Capture chọn Keep vẫn có thể bấm Hide all để ẩn toàn bộ Canvas gameplay. Chọn thời hạn tự hiện lại **15 / 30 / 60 giây**, rồi bấm nút **Hide all UI** hoặc **Hide selected UI**. Thao tác này chỉ ẩn UI, không chụp ảnh, không chờ Capture Delay và không yêu cầu Screenshot mode phải ON.

Controls gameplay ẩn bằng alpha vẫn bấm được. Khi đã ẩn, có thể chụp bằng mode đang bật, nút thiết bị hoặc toolbar LDPlayer. Nếu UI đã bị ẩn thủ công, Capture Keep không tự bật nó trở lại; dùng Restore UI trước nếu muốn ảnh có UI.

- Giữ **góc trên trái trong safe area 1,5 giây** để khôi phục; vòng tiến độ chỉ hiện lúc giữ. Thả sớm hoặc kéo ra ngoài để hủy.
- **F8** và giữ ba ngón là cách phụ. Restore UI không phải nút bật/tắt Screenshot mode.
- Hết thời hạn, đổi active scene, relock, pause hoặc shutdown đều khôi phục UI. Chỉ chuyển focus sang toolbar không tự kết thúc mode.
- Ảnh do thiết bị/giả lập chụp được công cụ đó lưu, không đi qua pipeline PNG/timeout và không tính vào dung lượng ảnh của debugger.

### Editor và lấy ảnh ra ngoài app

- **Unity Editor:** mở **Window → Dreamy Debugger → Screenshot**. Bảng điều khiển nằm ngoài Game View: chọn nhóm UI, chụp, hiện lại/hủy, xem ảnh gần nhất, mở ảnh gốc và thư mục. Cần Play Mode và debugger đã unlock.
- **Android 10+:** tab có **Save to gallery / Share image**. Xuất ảnh vào `Pictures/DreamyDebugger` qua MediaStore rồi chia sẻ bằng content URI. Không thêm quyền đọc/ghi toàn bộ bộ nhớ. Share mở bảng chọn chia sẻ của hệ thống; không tự gửi đến người nhận. Lần Share đầu tiên cũng lưu bản gallery để có URI chia sẻ.
- **Giả lập Android:** có thể dùng hai nút trên như trên điện thoại; hoặc dùng Hide UI rồi nút screenshot của emulator để lưu thẳng về máy tính.
- **Android cũ / iOS:** vẫn chụp PNG và xem lại trong app; adapter xuất Gallery/Photos/Share native chưa có cho các nền tảng này. Dùng công cụ chụp của thiết bị hoặc tích hợp adapter riêng.

PNG gốc lưu ở `Application.persistentDataPath/DreamyDebugger/Screenshots`. Kích thước trả về là kích thước ảnh thực tế, kể cả khi Game View đổi kích thước trong lúc chờ IO. Chế độ này không đổi aspect ratio, không có preset store và không gửi relay/outbox; `AllowScreenshots` và giới hạn JPEG của reporting không áp dụng cho chụp thủ công đã unlock.

### Android / LDPlayer và chụp ngoài

- Android: có thể dùng tổ hợp nút chụp màn hình của thiết bị (thường Power + Volume Down), hoặc Hidden shutter để tạo PNG riêng của debugger rồi xuất Gallery khi thoát mode.
- LDPlayer: dùng chức năng Screenshot trên toolbar; **View** trong thông báo sau chụp mở đường dẫn ảnh. Phím tắt/thư mục phụ thuộc cấu hình LDPlayer; không hardcode đường dẫn Windows trong game.
- Game không tự nhận biết mọi ảnh chụp ngoài và không tự khôi phục UI sau mỗi ảnh: Android 14 chỉ phát hiện một số kiểu chụp bằng nút phần cứng; không phát hiện đầy đủ ADB/instrumentation hay công cụ host. Dùng thời hạn 15/30/60 giây hoặc chủ động giữ góc trên trái/F8 để thoát. Mỗi ảnh chụp qua Hidden shutter gia hạn lại thời hạn; không bật preview khi còn ẩn UI.

Nguồn: [Unity ScreenCapture](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/screencapture/capturescreenshot), [LDPlayer Screenshot / View](https://www.ldplayer.net/support/tips-for-using-ldplayer.html), [Android screenshot detection limits](https://developer.android.com/about/versions/14/features/screenshot-detection). Android/LDPlayer native runtime cần kiểm chứng riêng.

### Quản lý dung lượng ảnh

Cuộn đến **Local Screenshot Storage** trong tab Screenshot để xem số PNG cục bộ và tổng dung lượng. Mục này vẫn có khi chưa có preview trong phiên hiện tại.

- **Refresh storage usage:** đọc lại số ảnh và dung lượng.
- **Delete local screenshots:** bấm lần đầu để hiện xác nhận, bấm **Confirm delete all local screenshots** để xóa; **Keep screenshots** để hủy. Xác nhận tự hết hạn sau 10 giây hoặc khi đóng tab.
- Chỉ xóa PNG có tên do debugger tạo ở thư mục Screenshots, không duyệt thư mục con. Preview của ảnh đã xóa được giải phóng. Ảnh đã xuất ra Gallery hoặc chụp bằng thiết bị/giả lập được quản lý riêng.

Ảnh không tự xóa theo số lượng/thời gian nên sẽ tích lũy trên bộ nhớ thiết bị; chúng không làm tăng kích thước bản cài. Quét dung lượng chạy ngoài main thread khi mở tab và sau khi chụp/xóa, không quét mỗi frame. Chụp, giải mã preview và xuất Gallery có thể làm tăng RAM tạm thời; chưa có số đo hiệu năng trên thiết bị thật.

### Vòng đời ảnh và giới hạn

Unity xuất PNG qua render path gốc vào file tạm riêng. Sau khi file có header/IEND hoàn chỉnh, UI được khôi phục và file kết quả được ghi qua file tạm rồi đổi tên. File native xuất hiện trễ sau khi hủy không được công bố; file tạm quá một phút được dọn ở lần chụp sau. Preview giữ một texture tối đa 512 px cạnh dài, thay thế ảnh cũ và giải phóng khi shutdown. Trên Android/iOS, native CaptureScreenshot nhận đường dẫn tương đối dưới persistentDataPath; desktop/Editor dùng đường dẫn tuyệt đối. Timeout chờ render/ghi PNG là 30 giây, không tính Capture Delay. Nếu báo **Waiting for rendered PNG**, chưa nhận được PNG hoàn chỉnh; giữ app/Game View render hoặc dùng nút chụp thiết bị/giả lập. Có giới hạn 64 MiB PNG và 128 MiB ước lượng bộ nhớ; cần kiểm chứng trên thiết bị thật.

Ẩn UI gameplay chỉ thay đổi/khôi phục `CanvasGroup.alpha`; giữ nguyên `interactable` và `blocksRaycasts` để nút/joystick vô hình vẫn hoạt động. Riêng UI debugger bị chặn input khi ẩn. Không tắt GameObject, dừng logic gameplay hoặc ghi preferences. GameObject đã đóng không bị mở lại. Script/tween tự ghi CanvasGroup cần phối hợp với Screenshot Mode vì giá trị trước lúc ẩn được khôi phục khi thoát. UI Toolkit, OnGUI, SpriteRenderer và text 3D ngoài Canvas cần tích hợp riêng.

```csharp
var screenshots = DebuggerRuntime.Instance.Screenshots;
screenshots.GameplayUi = ScreenshotGameplayUi.RegisteredTargets;
screenshots.DelaySeconds = 3;
var result = await screenshots.CapturePngAsync();
if (result.Success) Debug.Log(result.Path);
// screenshots.HiddenShutterEnabled = true; // arm corner capture; no HideUi required
// screenshots.HideGameplayUi = ScreenshotHideUi.RegisteredTargets;
// screenshots.HideUi(); screenshots.RestoreUi();
// screenshots.AutoRestoreSeconds = 30;
```

Prefab `ScreenshotTab`, `ScreenshotOverlay` và `ScreenshotHoldFeedback` được đóng gói sẵn, có bindings và catalog. Menu **Tools → Dreamy Debugger → Author Screenshot UI** chỉ dành cho người bảo trì package; consumer không cần chạy.

## Thêm cheat option

### Cách 1: sửa sample để thử ngay

1. **Package Manager → Dreamy Debugger → Samples → Debugger Quick Start → Import**.
2. Unity chép source vào `Assets/Samples/Dreamy Debugger/0.1.6/Debugger Quick Start`. Sửa bản trong **Assets**, không sửa source package.
3. Play → 5 tap góc trái trên → PIN `0000` → Cheats có 8 control mẫu. Host tự đăng ký trong Editor/Development Build, không cần tạo scene demo hoặc gắn component.
4. Tạo file `ProjectCheats.Testing.cs` bên cạnh `ProjectCheats.cs`:

```csharp
using System.ComponentModel;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger.Sample
{
    public sealed partial class ProjectCheats
    {
        private bool showHitboxes;

        [Category("Testing"), DisplayName("Show hitboxes")]
        [CheatId("sample.show-hitboxes")]
        public bool ShowHitboxes
        {
            get => showHitboxes;
            set => SetValue(ref showHitboxes, value);
        }
    }
}
```

Stop/Play lại: category **Testing** có toggle **Show hitboxes**. `SetValue` có sẵn trong partial gốc và phát `INotifyPropertyChanged`. Toggle này chỉ sửa biến mẫu; muốn hiện hitbox thật phải nối setter với renderer/service của game.

Các partial phải cùng **class, namespace và assembly**. Không đăng ký thêm container vì sample host đã làm việc đó. Giữ asmdef và `link.xml` của sample. Sample chỉ sửa dữ liệu trong RAM, không tự sửa wallet, save hay `Time.timeScale` của game.

Sample import là bản sao, không tự cập nhật theo package. Backup/merge phần đã sửa khi nâng cấp; tránh giữ hai thư mục version cùng khai báo type `Dreamy.Debugger.Sample`. Khi chuyển sang integration game riêng, xóa sample hoặc bỏ host mẫu để tránh catalog demo và ID trùng.

### Cách 2: nối service game thật

Tạo container trong code project. Ví dụ dưới đây dùng interface adapter **do game tự triển khai**, không phải API wallet/player của package.

**`GameCheatOptions.cs`** — contract và container:

```csharp
using System;
using System.ComponentModel;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;

namespace MyGame.Debugging
{
    public interface IGameDebugService
    {
        bool GodMode { get; set; }
        long Coins { get; }
        void AddCoins(long amount);
        void ResetProgress();
    }

    public sealed partial class GameCheatOptions
    {
        private readonly IGameDebugService game;

        public GameCheatOptions(IGameDebugService game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
        }

        [Category("Player"), DisplayName("God mode")]
        [CheatId("game.god-mode")]
        public bool GodMode
        {
            get => game.GodMode;
            set => game.GodMode = value;
        }

        [Category("Save"), DisplayName("Reset progress")]
        [CheatId("game.reset-progress"), CheatConfirmation]
        public void ResetProgress() => game.ResetProgress();
    }
}
```

**`GameCheatOptions.Economy.cs`** — thêm option bằng partial, không sửa host:

```csharp
using System.ComponentModel;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;

namespace MyGame.Debugging
{
    public sealed partial class GameCheatOptions
    {
        private const string CoinsCard = "game.coins";

        [Category("Economy"), ResourceCheat(CoinsCard, "Coins", CheatCardRole.Balance)]
        public long CoinBalance => game.Coins;

        [Category("Economy"), ResourceCheat(CoinsCard, "Coins", CheatCardRole.Amount)]
        [NumberRange(1, 1000000), Increment(100), Sort(1)]
        public long CoinAmount { get; set; } = 100;

        [Category("Economy"), ResourceCheat(CoinsCard, "Coins", CheatCardRole.Action)]
        public void AddCoins() => game.AddCoins(CoinAmount);
    }
}
```

Ba member tạo một resource card: balance chỉ đọc, amount chỉnh được và nút grant có xác nhận. Giữ cùng card ID/title/category và một member cho mỗi role. Service game chịu trách nhiệm grant/save, giới hạn nghiệp vụ và quyền truy cập; package chỉ hiển thị và gọi adapter.

**`GameCheatHost.cs`** — đăng ký theo vòng đời owner:

```csharp
using System;
using Dreamy.Debugger;
using UnityEngine;

namespace MyGame.Debugging
{
    public sealed class GameCheatHost : MonoBehaviour
    {
        private IDisposable lease;

        // Game bootstrap gọi sau khi service và debugger runtime đã sẵn sàng.
        public void Bind(IGameDebugService service)
        {
            Unbind();
            if (!isActiveAndEnabled) return;

            var runtime = DebuggerRuntime.Instance;
            if (runtime == null || !runtime.isActiveAndEnabled || runtime.Cheats == null)
                return; // Mode Off hoặc runtime chưa sẵn sàng.

            lease = runtime.RegisterCheats(new GameCheatOptions(service), "game");
        }

        public void Unbind()
        {
            lease?.Dispose();
            lease = null;
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
```

Để nối ví dụ vào project:

1. Triển khai `IGameDebugService` bằng adapter gọi player/wallet/save services hiện có.
2. Gắn `GameCheatHost` vào GameObject owner của integration, ví dụ startup root.
3. Sau khi service sẵn sàng **và** runtime đã khởi tạo, game bootstrap gọi `host.Bind(adapter)`. Với Automatic, không giả định runtime đã có trong `Awake`; đợi `DebuggerRuntime.Instance` có `Cheats` rồi Bind. Với Manual, khởi tạo debugger trước khi Bind.
4. Nếu runtime chưa sẵn sàng, `Bind` ở ví dụ chỉ trả về, không tự retry. Bootstrap phải gọi lại sau khi runtime sẵn sàng. Sau khi disable/enable host hoặc thay runtime/service, cũng Bind lại.
5. Gọi `host.Unbind()` trước khi hủy service nếu service kết thúc trước host. Không đăng ký lại trong mỗi `Update`.

Nếu code game dùng asmdef, thêm references **Dreamy.Debugger.Runtime**, **Dreamy.Debugger.Engine** và **Dreamy.Debugger.Presentation** như sample. Không đưa `UnityEditor` vào assembly runtime.

### Quy tắc authoring và attribute

| Khai báo | Control / hành vi |
|---|---|
| Public property `bool` có setter | Toggle. |
| Public property số có setter | Ô số; dùng `NumberRange` và `Increment` để giới hạn/bước tăng. |
| Public property `enum` / `string` | Chọn enum / nhập text. |
| Getter-only hoặc private setter | Giá trị chỉ đọc. |
| Public `void` không tham số | Button; thêm `CheatConfirmation` để yêu cầu xác nhận. |
| `Category`, `DisplayName`, `Sort` | Nhóm, nhãn và thứ tự hiển thị. |
| `CheatId` | ID ổn định; giữ ID khi đổi tên member. ID phải duy nhất trong registry. |
| `Browsable(false)` | Ẩn public helper không muốn tạo control. |

Field, static member và method có tham số không được tạo control. Public helper nên private hoặc được đánh dấu `Browsable(false)`.

Nếu không có `CheatId`/card attribute, ID mặc định là `<prefix>.<MemberName>`. Resource card tự tạo `.balance`, `.amount`, `.grant`; `CheatId` ghi đè ID member nhưng không thay card ID. Không đăng ký cùng option bằng cả attribute và typed API.

Khi giá trị đổi từ gameplay, có thể triển khai `INotifyPropertyChanged` như sample để lease tự nhận notification, hoặc gọi `runtime.Cheats.NotifyValuesChanged()` trên owner thread sau khi service đổi. Không cần đăng ký lại container để refresh. Getter/setter nên nhẹ; getter lỗi sẽ hiển thị `Unavailable`.

Xem [CHEAT_OPTIONS](Documentation~/CHEAT_OPTIONS.md) cho ownership card, typed API và quy tắc registry.

### Giữ option trong build IL2CPP / stripping

Reflection cần giữ type/member. Tạo `Assets/Debugging/link.xml` cho container game, ví dụ:

```xml
<linker>
  <assembly fullname="Assembly-CSharp">
    <type fullname="MyGame.Debugging.GameCheatOptions" preserve="all" />
  </assembly>
</linker>
```

Nếu dùng asmdef, thay `Assembly-CSharp` bằng **name trong asmdef của game**. Đổi namespace/type thì sửa `fullname`. Partial không tạo type riêng nên chỉ cần giữ container một lần. Giữ `link.xml` của sample nếu vẫn dùng sample.

Trước khi giao build: bật **Development Build** khi dùng Development/Qa, kiểm tra mở PIN, control đọc/ghi đúng service, xác nhận grant/reset, disable owner thì option được gỡ và chạy thử trên build IL2CPP. Editor compile thành công không chứng minh reflection còn đủ sau stripping.

## Lỗi thường gặp

| Hiện tượng | Cần kiểm tra |
|---|---|
| Tap không mở | Loading mode, Mode/build policy, đúng góc và số tap; có bootstrap authored inactive/disabled chặn Automatic không. |
| Đổi configuration không có hiệu lực | Asset đang được chọn trong Settings/bootstrap, đã restart Play chưa; HUD có thể đang dùng preference đã lưu. |
| Panel mở nhưng Cheats trống | Đã import sample hoặc gọi Bind/RegisterCheats sau khi runtime/service sẵn sàng chưa. |
| Partial mới không hiện | Cùng namespace/class/assembly, member public và kiểu hỗ trợ; Stop/Play sau khi sửa. |
| Option trùng / lỗi ID | Hai bản sample đã import, hai host, đăng ký lại container hoặc ID trùng. |
| Cheat chỉ sửa số trên UI | Setter/action đang sửa model mẫu; nối adapter với service và đường save thật. |
| Editor có cheat, IL2CPP mất | `link.xml` giữ đúng assembly/type/member; kiểm tra build policy. |
| Không compile khi import Git bản 0.1.0 | Cập nhật 0.1.1 trở lên; không sửa PackageCache hoặc xóa `.meta` để chữa thiếu control scripts. |

## Lưu ý quan trọng

- Đổi PIN mặc định và kiểm tra build policy trước khi giao build. PIN/attribute không thay quyền truy cập hoặc xác thực server của game.
- Upload và screenshot tự động mặc định tắt. Muốn gửi Telegram, cấu hình relay; không đưa bot token vào client. Xem [reporting](Documentation~/REPORTING.md) và [cài relay](Tools~/relay/README.md).
- Chỉ cài một bản Dreamy Debugger; giữ nguyên `.meta`/GUID khi nâng cấp. Không cần Setup Theme hoặc rebuild assets để dùng.
- Ảnh và kết quả kiểm tra trong Unity không thay kiểm thử thiết bị thật, IL2CPP hoặc hiệu năng production. Xem [phạm vi kiểm chứng](Documentation~/RELEASE.md) và [hỗ trợ nền tảng](Documentation~/PLATFORM_SUPPORT.md).

## Tài liệu

[Migration](Documentation~/MIGRATION.md) · [Changelog](CHANGELOG.md) · [Acceptance](Documentation~/ACCEPTANCE.md)

Package dùng nội bộ; giữ license và điều kiện upstream.
