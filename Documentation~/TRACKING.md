# Implementation checkpoint — 2026-11-04

Version: **0.2.0-preview.1**. Software implementation spans all planned phases. Acceptance is separated from implementation; hardware/live-service gates are not marked passed without receipts.

| Phase | Implemented | Matching acceptance / remaining limits |
| --- | --- | --- |
| 0 baseline | Source inventory and GUID-preserving migration ledger; upstream notices/archive | Static baseline and compile known. Original on-device performance baseline was not collected |
| 1 package/fork | Dreamy assemblies/class names, optional input adapter, prefab/catalog migration, package-safe authoring, no required Dreamy Core/UI/Graphy | Clean consumer, prefab script audit, dependency and player builds. Internal preview only; upstream redistribution restrictions retained |
| 2 access/cheats/UI | Styled Editor Control Center, responsive runtime panel, configurable 5/0000 defaults, PIN/passphrase, session/cooldown, typed grouped cheats, confirmation and per-operation guards | EditMode/PlayMode tests; live numeric unlock and 11-option runtime screenshot. Real mobile touch/focus remains device acceptance |
| 3 HUD | Off/FPS/Compact/Detailed, shared fixed history, slow text/memory refresh, no input raycasts | Toggle/single-sampler tests, runtime screenshot. Weak-device p95/GC/thermal budget not certified |
| 4 local reports | Bounded threaded ingress, snapshot/redaction, async downscale/readback/encoding, text fallback, local quota/retention/corruption recovery/dead letter | Outbox tests and actual Editor GPU capture receipt/JPEG. No continuous pre-crash recorder enabled; optional checkpoint history stays gated by device profiling |
| 5 relay | HTTP client, SQLite durable acknowledgement, dedupe, bounded retry, Telegram/Gmail/SMTP adapters, fake delivery | Relay unit tests and C#→localhost relay duplicate/ack smoke. No real bot/mailbox credential was supplied; live account delivery not run |
| 6 heavy crash | Managed error policy, abnormal session journal, Android API30+ historical OS evidence with watermark, external native evidence importer | Fake provider import test and Android compilation. No device native crash/ANR/OOM test; no universal fatal handler or crash-time screenshot guarantee |
| 7 reuse/release | Editable samples/config/prefab ownership, installer customization preservation, migration/rollback docs, Release scene stripping and consumer build verification | Clean tests and Linux/Android builds. No store signing, iOS/Xcode, weak-device smoke or production certification |

Fresh evidence lives in parent `docs/evidence/debugger/phase-completion/`; exact final test/build counts, input hashes and remaining gates are recorded in its verification report. Earlier zero-test, compile failure, incomplete or interrupted runs are not acceptance.

Observed checkpoints include 64 clean-consumer EditMode tests, 10 PlayMode tests, 13 relay tests; the final report records fresh re-runs after the final code changes. Linux development/release and Android IL2CPP ARM64 build receipts exist. The Linux release report records no packaged debugger assets.

The actual async GPU capture completed with a bounded JPEG. One cold dispatch measured 2.7732 ms and one warm dispatch 0.0433 ms in the Linux Editor. These are two smoke observations, not p95 measurements or proof of mobile no-lag behavior.

The Control Center compiled and opened, but native desktop pixel capture returned black under this Linux desktop and was rejected as visual evidence. Runtime GameView capture works and is the retained visual evidence.

## Resume / external gates

No further placeholder code is required to configure local diagnostics and the reference relay. Remaining acceptance needs real destinations and devices: provision relay credentials outside the client/repository; exercise Android/iOS touch, capture failures, native/ANR/OOM test builds and sustained weak-device profiling. Supply an SDK-specific ICrashEvidenceProvider on platforms requiring crash dumps. Do not mark those gates complete from desktop tests or build success.
