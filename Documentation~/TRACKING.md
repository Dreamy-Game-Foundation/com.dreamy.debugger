# Current candidate: 0.3.0-preview.3

Options workflow follow-up. See CHEAT_OPTIONS.md and docs/evidence/debugger/options-workflow in the sandbox. Older sections are historical.

# Current candidate: 0.3.0-preview.2

Partial/attribute authoring migration. ResourceCheat/OwnershipCheat/CheatId metadata added; package remains independent. See CHEAT_OPTIONS.md and parent docs/evidence/debugger/attribute-migration. Prior UI archive/build receipts do not certify this candidate or project reflection changes.

# Workspace redesign candidate — 0.3.0-preview.1

Current work follows the approved UI plan. Exact fresh receipts, source fingerprints, image review and build limits are in the parent sandbox docs/evidence/debugger/ui-redesign and Documentation~/RELEASE.md. Previous preview.6 receipts below are historical. No mandatory Core/UI dependency or original SR GUID change is introduced.

---

# Current checkpoint — 0.2.0-preview.6

2026-11-05. Candidate fixes and matching validation are tracked in [RELEASE.md](RELEASE.md). This candidate is not published/tagged. Current source and package identity supersede the historical version below; old build receipts do not certify newer code.

Current runtime: red theme, one visible Dreamy logo, closeable sidebar, no package report tab, default-off draggable HUD, shared Graphy renderer, typed/attribute partial examples. Release fixes isolate observer/getter failures, refresh values without row replacement, use a recent FPS alert window, remove unmeasured profiler legends, harden capture ownership/policy checks and service restart caches. Runtime/public manifest versions agree.

Gmail remains pending. Historical local Telegram delivery and Editor GPU capture receipts exist in the parent sandbox; they are not fresh production/device acceptance. No mandatory Dreamy Core dependency is added. Device performance, native crash/ANR/OOM, iOS, production consent/masking/withdrawal and remote deployment remain open gates.

---

# Historical implementation checkpoint — 2026-11-04

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


## 0.2.0-preview.2 — cheat panel setup correction

The sandbox startup scene had no explicit runtime host. Added a direct Cheat Panel menu, visible Edit Mode setup guidance and an undoable/idempotent scene installer; project bootstrap creation does not replace an untitled scene. Five matching EditorIntegration tests passed; actual Bootstrap→MainScene runtime showed 11 options and opened Options after the numeric PIN prompt. This Editor-only patch has no new platform build certification; preview.1 build receipts remain scoped to that release. Parent evidence: `docs/evidence/debugger/cheat-entry-fix/`.


## 0.2.0-preview.3 — pending PIN lifecycle

Live bootstrap verification reproduced a PIN prompt left open while scene activation relocked AccessSession. The stale visible prompt prevented a new request. Relock now dismisses the pending view/callback before clearing access; 8 focused UI PlayMode tests passed, including a real scene change mid-prompt and reopening/unlocking Options. Current targeted EditorIntegration suite also reruns separately. No fresh player/device build certification is claimed for this patch.


## 0.2.0-preview.4 — per-game bound samples and Int64 UI

Quick Start now includes editable resource/ownership delegate templates. The sandbox chooses its own 27 options and service bindings under Assets/_Project/Debugger; package Runtime gained no game/Core/Economy dependency. Numeric controls support Int64 text/arrows with exact stepping and range metadata. Horizontal drag is disabled for Int64 to avoid the legacy double path. Presentation registry refresh replaces one owned container at a time to avoid stale getter reads during scope disposal.

Fresh focused UI PlayMode suite: 9/9 passed, including bounds, exact values beyond 2^53, Int64 maximum and disposal while the Options tab is open. Sandbox wallet/entitlement tests use temporary saves; live Add Coins/Confirm grants and persistence are verified separately in parent project evidence. No ads SDK was present; No Ads invokes the existing persisted entitlement/demo effect. No new device or player build certification is claimed for preview.4.


## 0.2.0-preview.5 — independent GUID identity

379 owned asset/template GUIDs replaced with independent identities; importer settings/fileIDs retained. Sandbox project variants/config and imported samples remapped together. Source archives remain ignored historical originals. Fresh audit: 30 prefabs, no missing scripts; 6 project tests, 65 consumer EditMode and 12 consumer PlayMode passed. Existing consumers need GuidIsolationMap.json when upgrading. Actual original-SR import/coexistence check remains pending user import.
