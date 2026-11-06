# Dreamy Debugger v0.1.1 — Git import hotfix

0.1.0 Git commit `5912da131b70d7574f3e25bd18f7c046ce34024a` reproduces CS0234/CS0246 in a new Unity 6000.4.12f1 project. Its broad `.gitignore` `data/` rule causes UPM Git packaging to omit 16 tracked files in `Controls/Data`: eight scripts and their existing metas. The tested npm tarball includes those files, so previous tarball/source acceptance did not cover the failing installation method.

0.1.1 limits the rule to `/Tools~/relay/data/`, preserves all asmdefs/GUIDs and adds `Tools~/verify_git_install.py`. Fresh Git-install source parity and Unity compile must pass before calling this fix accepted. Exact before/after source commits, checks and compiler receipts are in parent `docs/evidence/debugger/git-import-hotfix/`. Do not count the historical v0.1.0 archive test as proof of Git import.

Maintainer: install the exact Git commit in a new consumer, then run `python3 Tools~/verify_git_install.py <source-repo> <resolved-package-dir>`. Require byte parity for shipped tracked files and compile/import the sample. Retain archive verification, package tests and all device/privacy/deployment limits below. No assembly reference workaround or generated PackageCache edit is part of the fix.

---

# Historical v0.1.0 release preparation


Prepared 2026-11-06 for **private internal distribution**. Package manifest and `DreamyDebug.Version` are `0.1.0`; author is Dreamy Game Studio. This prepares a release artifact and documentation; it does not create a tag, push, upload or publish a release.

## Version policy

The requested `0.1.0` starts a new numbering line from the development source previously labelled `0.3.0-preview.6`. Existing repository tags `v0.2.0-preview.*` are retained. This is numerically lower under semver, so consumers must explicitly pin the intended commit/archive or the future `v0.1.0` tag; a version-range resolver will not treat it as an upgrade from `0.2.x`/`0.3.x`. No GUID, namespace, package identity or cheat ID is reset.

## Contents

- Automatic startup or explicit Manual initialization; independently usable package with Unity-only required dependencies.
- Configurable tap/PIN/passphrase access; Console, attributed partial cheats, owned registry/confirmation/cards, Performance/HUD and System views.
- Quick Start import-to-Play, editable partial files and complete game-owned integration guide.
- Optional local reports/screenshots and Telegram relay. Email is removed; no runtime package-author report tab or embedded bot credentials.
- Runtime presentation preferences, category pins, full retained stack details, Geist/flat interface and translucent draggable HUD.

Read [README](../README.md) for setup and APIs, [migration](MIGRATION.md) before updating an edited sample, and [notices](../THIRD_PARTY_NOTICES.md) for upstream restrictions.

## Release verification

Fresh source/archive receipts, exact artifact hash and package commit are recorded externally in the parent sandbox at `docs/evidence/debugger/release-v0.1.0/`. Artifacts are kept outside the package to avoid recursive/self-referential hashes. The scope is Unity 6000.4.12f1 / Linux and Test Framework 1.6.0; declared minimum Unity 6000.0 is not a claim that all Unity 6 versions were tested.

Acceptance target: 92 EditMode + 64 PlayMode tests including imported Quick Start, compilation of the complete README game host/partial examples, archive metadata/content validation and 20 offline relay/setup tests. The external checkpoint records the actual fresh result and artifact hash.

Earlier preview build/device/visual receipts remain historical. They do not certify v0.1.0. Consult the fresh checkpoint for actual counts and any unavailable check; preparation alone is not a passed gate.

## Checks before distribution

1. Verify package.json, runtime version, README and changelog agree; retain every existing meta GUID and third-party notice.
2. Pack the allowlisted files and run `python3 Tools~/verify_package.py <archive.tgz>`; exclude local relay DB/cache/logs/secrets and repository internals.
3. Install that exact archive in an isolated Unity consumer, import its Quick Start and run EditMode/PlayMode tests. Compile the complete README host/partial examples against it.
4. For the intended game/platform, run Development and ordinary release smoke builds and verify the correct startup policy. For IL2CPP, preserve the game's own reflected containers and test on device.
5. Before enabling automatic screenshots/remote uploads, validate the game's sensitive-screen policy, network/relay configuration, retention and applicable disclosure/consent flow.
6. After reviewing the artifact/receipts, a maintainer can create the private release/tag for the verified commit. Keep existing tags unchanged and record rollback commit/config/sample versions.

## Remaining device/deployment gates

- Mobile touch, notches/DPI/rotation/IME, GPU readback and weak-device p95/p99/GC/thermal/battery under capture/send load.
- On-device IL2CPP/stripping and native fault/ANR/OOM/OS-kill acceptance for the actual game; no screenshot is guaranteed after process death.
- iOS/Xcode/device validation and production HTTPS relay deployment/destination delivery.
- Production privacy/masking/withdrawal/purge behavior belongs to the host; `SensitiveScreen` is a capture veto, not a full masking or consent subsystem.

No lag-free, universal Unity/platform support or production crash-certification claim accompanies this internal release. Vendor-derived content keeps its original restrictions; public redistribution is not granted by the version change.
