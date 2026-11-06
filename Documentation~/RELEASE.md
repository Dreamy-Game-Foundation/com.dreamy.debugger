# Automatic startup/preferences candidate — 0.3.0-preview.4

Prepared 2026-11-06. Automatic/Manual startup, manual-owner precedence, temporary build payload and allowlisted local presentation preferences. Fresh matching evidence is recorded in parent sandbox `docs/evidence/debugger/autostart-preferences/`. Earlier candidate counts below are historical. No tag, push or publication is part of this task.

The startup build gate uses an empty scene without a bootstrap/sample host. Default release must contain no debugger UI assets; ReleaseDiagnostics stages config only. Source/archive EditMode and PlayMode tests cover storage/recovery, owner replacement, reload cycles, HUD/console/category/pin restore and capture hiding. Linux/Android builds do not certify device touch/performance, native crash or iOS behavior. Known open device/privacy/licensing/Gmail gates below still apply.

---

# Options workflow candidate — 0.3.0-preview.3

Convenience registration, owned/coalesced notifications, action-specific confirmation and incremental options UI. Fresh evidence belongs to parent sandbox docs/evidence/debugger/options-workflow. Plain reflected methods are immediate unless CheatConfirmation is present; card grants remain confirmed. No commit/tag/publication in this task. Prior candidate receipts below are historical. Main Editor MCP was unavailable for the target project; isolated consumer validation is used and exact scope is recorded externally.

---

# Attribute authoring candidate — 0.3.0-preview.2

Prepared for internal review after migrating game/sample cheat authoring to partial containers. Package manifest and public runtime version agree. Additive metadata attributes preserve the typed registry and view contracts. Reflected methods consistently require confirmation; the default sample grant previously did not. No commit/tag/publication is performed by this task.

Fresh verification belongs in the parent sandbox `docs/evidence/debugger/attribute-migration/README.md`. Preview.1 receipts below are historical, not certification of this candidate. Real device/IL2CPP stripping and runtime validation must match the actual game-owned reflected types and link.xml. Production/privacy/performance gates below remain open.

---

# UI workspace candidate — 0.3.0-preview.1

Prepared 2026-11-05 for private internal testing. No tag, commit, push or publication is performed by the redesign task. Package and public runtime version agree; author remains Dreamy Game Studio. Public tab keys, original SR sources and existing GUIDs are retained. Code/base assets ship together; imported samples, configurations and variants remain game-owned.

## What changed

- Red/graphite workspace, compact wordmark/header, wide sidebar and narrow overlay drawer with close/scrim/back; Cheats/Performance display labels retain Options/Profiler keys.
- Typed resource/ownership metadata and task cards with generic fallback, search/category/pinned filters, category collapse and incremental large-catalog construction. UI does not own game services/save/purchase authority.
- Fixed binding subscription when moving controls into inactive slots or enabling before Start. Grant confirmation revalidates owner and amount; stale intent is rejected and failures have a visible dialog state.
- Timestamped virtualized console rows, always-available severity/search, future-entry collapse toggle, retained selection/scroll and narrow detail Back.
- HUD has separate aligned labels/values, dedicated pointer-owned grip, warmup states, default Off and one sampler. Editor has compact project-config forms and a clearly labelled sample preview. PIN uses bounded dots/progress and separate passphrase handling. System has aligned key/value rows and per-section copy.

## Fresh verification scope

Unity 6000.4.12f1 Linux. Source and exported archive consumers use only this package and Unity Test Framework as direct dependencies. Matching results, source hashes, screenshots and build receipts are recorded in the parent sandbox `docs/evidence/debugger/ui-redesign/verification.json`. Previous preview.6 receipts are historical and do not certify this UI.

Regression coverage includes metadata validation, actual resource/ownership controls, confirmation amount/owner changes, filter/collapse/pin/disposal, inactive binding recovery, 32-digit PIN/passphrase input, narrow drawer/details, multi-pointer grip/cancel, service restart, graph fallback and catalogs of 20/200/1000 options. Fake/local relay tests perform no real destination delivery.

The 1000-option Editor structural registration was observed at roughly 1.27 seconds before incremental population, and roughly 5–6 ms afterward in the source fixture. These are single Editor observations for registration dispatch, not complete construction time, p95, GPU/layout/GC budget or device certification. Construction continues over later frames, with a nominal 4 ms work-loop budget; native layout and cleanup costs are additional.

Visual inspection uses actual offline Unity fixtures at representative portrait/landscape/tablet sizes, with synthetic option values. The AI concepts are references, not test screenshots. Editor native-window capture may be limited by the desktop environment; any missing image proof stays explicit in the external receipt.

## Consumer migration and checks

Keep existing meta identities. Review custom standalone panel/HUD copies against matching new base assets; variants must review overrides. Updated Quick Start templates opt into cards through stable role IDs. Legacy/attribute/partial options remain valid without metadata. Do not overwrite customized samples blindly or move the original SR package.

The confirmation canvas uses the same constant-pixel/retina policy as the access panel and a safe-area-constrained dialog. Game shared fonts are not changed. Changing collapse mode affects future console entries; historical collapsed messages cannot be expanded retroactively.

Run the exported archive guard with `python3 Tools~/verify_package.py candidate.tgz`; then run consumer tests and applicable development/release/IL2CPP builds. Archive hashes live externally to avoid self-referential checksums. No local relay database/log/cache or bot token belongs in the exported package.

## Open gates

- Real mobile touch/DPI/notches/rotation/IME and complete input-system/device matrix; Editor geometry alone is insufficient.
- Weak-device p95/p99 frame time, GC, VRAM, thermal/battery and capture/send under load. No lag-free claim is made.
- On-device GPU cancellation/late callback/low-memory paths and native crash/ANR/OOM. No new image is promised after process death.
- iOS/Xcode/device runtime verification and production screenshot policy: disclosure/consent, masking, withdrawal/cancel/purge and remote HTTPS relay monitoring/delivery.
- Gmail/email stays pending. Full SR settings parity, generic advanced rules and independent per-module Graphy states remain outside this UI release scope.

This candidate is suitable for internal evaluation within the upstream licensing restrictions. Stable/production and publication require their matching owner/device/deployment gates.
