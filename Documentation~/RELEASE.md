# Release candidate — 0.2.0-preview.6

Prepared 2026-11-05 for private internal review. No tag, push or publication is performed. package.json and the public DreamyDebug.Version use the same version; author is Dreamy Game Studio. Runtime/Editor/base assets are versioned together; samples and project configuration remain editable. This preview includes UI, HUD and option work since preview.5 plus the pre-release audit fixes.

## Validation scope

Unity 6000.4.12f1 on Linux. An isolated consumer has only com.dreamy.debugger and Unity Test Framework as direct dependencies: no Dreamy Core/UI, external Graphy or Input System. Parent sandbox evidence is under docs/evidence/debugger/release-preview.6; verification.json is authoritative for exact input fingerprints, fresh results and artifact SHA-256. Earlier receipts are historical.

- Regression coverage: observer failure/registration lease, authorized mutations, immediate and external value updates, broken getter recovery, recent-window FPS warnings, shader fallback/lazy allocation, disabled capture, service restart/cache rebinding, version agreement and measured profiler legends.
- Existing suites cover access/scene relock, bounded queues/outbox/retry, HUD drag/safe area/input pass-through, sidebar open/close while paused, Int64 controls and sample ownership.
- Python relay tests use synthetic data and localhost/fake destinations. They cover acceptance/dedupe, retry/rate limits, payload validation, identifier/token handling and connection close/rollback. No real Telegram/email delivery is attempted by this candidate verification.
- Consumer development/release builds and exported UPM checks are recorded in verification.json. A build receipt proves compilation/packing, not interactive device behavior.

## Fresh candidate results

- Source consumer: **77/77 EditMode, 25/25 PlayMode** passed, no skipped tests.
- Exported UPM consumer: **77/77 EditMode, 25/25 PlayMode** passed. Archive guard checks local-state exclusion, path/symlink safety, required notices and Unity metadata; final artifact hash/results are external to avoid a self-referential checksum.
- Relay: **20/20 tests** passed with fake/local destinations, including connection close and rollback.
- Linux development: build succeeded. Linux non-development: build succeeded, **zero packaged debugger assets** in the build report.
- Android development **IL2CPP ARM64**: build succeeded. No connected device runtime certification; no iOS build.
- Sandbox: main Editor compiles with no console errors; active scene remains BootstrapScene in Edit Mode. Maintained prefab validation reports **28 prefabs, no missing scripts**. No original SR source/meta changes are part of this fix.

## Remaining release gates

This is a candidate for internal testing. Before a production rollout, collect the following on the actual target devices/deployment:

1. Android touch, rotation, safe area, weak-device frame-time/GC/memory/battery/thermal measurements; compare HUD off/on and capture/send under load. Capture is bounded and asynchronous but is not certified lag-free.
2. Real GPU capture cancellation, low-memory, late readback, overlay exclusion and sensitive-screen changes on target graphics APIs. Headless tests cannot certify the GPU path.
3. ARM64 device smoke and reflected game-option stripping; preserve each consumer's members/assembly names. iOS/Xcode and native crash/ANR/OOM/OS kill acceptance remain pending.
4. Remote HTTPS relay deployment, durable volume, destination monitoring, key rotation and real phone-to-Telegram delivery. Historical Editor/local delivery is not this gate.
5. Host privacy/disclosure/consent, withdrawal/deletion and personal-data masking if screenshots are enabled in production. Sensitive-screen suppression alone is insufficient. Gmail/email is pending and excluded from acceptance.
6. Validate package upgrade/rollback and original SR coexistence in each consumer. Do not delete .meta files or modify the original SR source. Restricted upstream licensing/notices remain applicable; this is not an open-source redistribution release.

## Release procedure

Review the dirty package and project adapter changes independently, retain matching evidence, then commit the package and update the consumer submodule pointer. Pin the reviewed commit/tag in consumers. Publish/tag only after explicit owner approval and applicable device/deployment gates. Keep Telegram credentials outside Git and outside the shipped package. The local exported .tgz and its checksum are review artifacts, not a published release. Run `python3 Tools~/verify_package.py candidate.tgz` after every export.
