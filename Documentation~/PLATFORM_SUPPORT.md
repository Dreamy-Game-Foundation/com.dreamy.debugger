# Support and verification matrix

Current package: **v0.1.6** (Screenshot workflow fixes). Checks and exact limits are in [RELEASE.md](RELEASE.md). Earlier preview build receipts are historical; build success alone does not prove device runtime or performance.

| Capability | Implementation | Verification limit |
| --- | --- | --- |
| Unity 6000.4.12f1, Linux Editor | Responsive panel, Editor tools, numeric/passphrase access, typed options, shared HUD | Automated tests and captured UI; not a mobile performance guarantee |
| Clean consumer without Dreamy Core/UI/Graphy/Input System | Declared Unity module dependencies, optional input adapter | Fresh candidate tests; see RELEASE.md for candidate build results |
| Linux player | Development UI and Release stripping | Candidate build status in RELEASE.md; device-specific gameplay remains the consumer's smoke gate |
| Manual gameplay PNG / UI restore | Editor capture/preview, corner hold, F8 and touch fallback implemented | 23 focused PlayMode tests; native device gestures/performance pending |
| Android 10+ Screenshot Gallery / Share | MediaStore Pictures/DreamyDebugger and system chooser | C# branch compilation only; native device/emulator validation pending |
| Screenshot storage cleanup | Local PNG count/bytes, confirmed delete; Gallery preserved | Isolated filesystem deletion and tab binding/confirmation checks |
| Android IL2CPP ARM64 | UI/report client, JNI OS exit query | Candidate build status in RELEASE.md; no ADB device was connected for runtime/native fault tests |
| Android previous native crash / ANR / OS kill | Query historical ApplicationExitInfo on API 30+ at next launch | OS reason metadata only; not a crash dump or guaranteed image; on-device verification pending |
| iOS native crash | External SDK integration through ICrashEvidenceProvider | No Xcode/Mac/device verification in this environment; no built-in native handler claim |
| Native crash SDKs | Bounded evidence import, deterministic IDs, acknowledge after persistence | Fake provider verifies the contract; host must supply its SDK adapter |
| Screenshot after managed error | Optional async pipeline while rendering remains alive | GPU capability and real capture receipt determine availability |
| Screenshot after process death / OOM / hang | Only previously persisted context or SDK/OS evidence may survive | No new screenshot or callback guarantee |
| Built-in render pipeline | Catalog/UI and capture path | Clean consumer / Editor receipts |
| URP | Original sandbox pipeline with the same final-frame capture API | Editor smoke only; HDRP/WebGL not advertised as verified |
| Telegram | Relay adapter and local setup launcher implemented | Historical local real text/photo delivery exists; fresh fake relay regressions only. Remote production/device delivery pending |
| Frame-time/memory/battery budget | Fixed queues, sampling frequency, async/cap/fallback policy | Weak-device profiling and thermal/battery acceptance remain outstanding |

An abnormal-session marker is not evidence of a confirmed native crash. The package does not deliberately crash the user's scene. Native crash/ANR/OOM testing belongs in dedicated device test builds.

[Android historical exit API](https://developer.android.com/reference/android/app/ActivityManager#getHistoricalProcessExitReasons(java.lang.String,%20int,%20int)) is the primary source for the Android OS query. Capture uses Unity's documented asynchronous GPU readback and JPEG encoder APIs; platform presence alone is not validation.
