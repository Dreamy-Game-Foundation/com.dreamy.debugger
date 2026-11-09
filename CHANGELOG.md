# Changelog

## 0.1.3 — 2026-11-09 (Material Compact, release preparation)

- Fix sidebar tab backgrounds/dividers extending past the header border when a left safe-area inset is present. Allocate inset-aware rail width and align icons/content through rotation and scaled canvases.
- Replace active skin with Barlow/JetBrains Mono, tiny Material PNG icons and flat sliced surfaces; use an alpha Dreamy wordmark extracted from original artwork. Keep existing prefab/script GUIDs.
- Compact responsive resource cards and category pin chips, lazy paged expansion, bounded scrolling and ordinary-control fallback for incomplete pinned cards. Preserve cheat bindings, access and confirmations.
- Add HUD tap-to-cycle and close controls/preferences; separate the move icon from frame-time text and align history warmup to the left. HUD remains default-off and full-screen anchored.
- Use a small centered titleless PIN keypad with independent working Cancel/Delete; keep passphrase and long-PIN support. Correct circular log icons and add a close control to the existing log dock.
- Restyle the Editor settings header/tabs and document new controls, asset provenance and mobile compression presets. Mobile/device rendering and native Editor visual capture remain separate verification gates.

- Update package/runtime version, README setup/sample paths, migration, release tracking and verification scope to 0.1.3. No new required dependency or meta GUID regeneration. This entry prepares the internal release; no tag/publication is implied.

## 0.1.2 — 2026-11-06 (landscape layout)

- Use a responsive, centered PIN dialog with side-by-side prompt/keypad on landscape and a compact passphrase form. Fit the complete dialog inside the safe viewport, including rotation, long PIN dots, zero key, Cancel and Submit.
- Anchor HUD to the full screen without safe-area insets, remove DPI amplification and bound its default footprint by viewport size. Keep a transparent minimum-48px grip hit target independent of the small visible grip. Preserve free dragging/preferences; hide HUD while PIN/passphrase is showing and restore after dismissal.
- Keep the configured tap target operational with no sprite, icon or error art; label its setting Enable hidden tap trigger while preserving the serialized field name.
- Keep all sidebar navigation labels visible on short landscape viewports with compact icon/title rows; refresh safe-area sizers on viewport changes in Editor/Simulator and players.
- Add rendered landscape/portrait/rotation, prompt submission, trigger raycast and HUD placement regressions. Preserve asset/script GUIDs and the 0.1.1 Git packaging fix.

## 0.1.1 — 2026-11-06 (Git import hotfix)

- Fix Git UPM imports losing all eight option-control scripts and their meta files: scope the broad `data/` ignore rule to `/Tools~/relay/data/`. Unity Git packaging filtered `Controls/Data` while the npm archive retained it, so prior tarball-only acceptance missed this failure.
- Preserve assembly definitions, namespaces and all asset/script GUIDs. Resolve CS0234 for `Controls.Data` and CS0246 for OptionsControlBase/ActionControl/DataBoundControl without adding dependencies.
- Add a resolved Git-package parity gate and require a fresh Git install/compile as well as archive verification. Document the affected 0.1.0 Git commit and update instructions; keep historical receipts explicit.

## 0.1.0 — 2026-11-06 (prepared, not published)

First numbered internal release on the new `0.1.x` line, consolidating the development previews below. This deliberately resets the version label from `0.3.0-preview.6`; it is not a source rollback. Existing `v0.2.0-preview.*` repository tags remain historical and unchanged. No tag or remote publication is implied by this entry.

### Features

- Automatic startup in Editor/development builds; optional Manual bootstrap, authored-owner precedence and default release UI exclusion. No mandatory Dreamy Core/UI/Graphy package dependency.
- Configurable 1–20 tap entry at eight screen-edge positions, default five taps plus PIN `0000`, numeric PIN/passphrase, failed-attempt cooldown and session relocking.
- Project-owned partial cheat containers with property/method attributes, owned registration, value notifications, ranges/order, confirmation and resource/ownership cards. Imported Quick Start automatically exposes eight in-memory controls and waits/rebinds to the actual runtime.
- Console search/severity/collapse, virtualized history, full retained stack details with long-text paging and copy. Bounded pending/history budgets.
- Performance view and default-off FPS/Compact/Detailed HUD, shared frame sampler, free dragging, configurable FPS colors and sustained low-FPS warnings. Allowlisted local presentation preferences persist without saving cheat values or secrets.
- Red/graphite interface, Geist typography, category pin icons, closeable drawer, styled confirmation/PIN dialogs and translucent HUD.
- Local diagnostic outbox, optional asynchronous screenshot pipeline with text fallback, bounded retries and Telegram-only relay/setup/discovery tools. Fake transport and explicit live synthetic-report testing. No email/Gmail delivery.

### Setup and maintenance

- Expand README with import methods, sample-to-partial workflow, complete game host, asmdef/link.xml setup, shutdown paths, runtime APIs, local/remote Telegram setup and troubleshooting.
- Align package/runtime version and refresh release, migration and platform-verification documentation. Preserve meta GUIDs and third-party notices.
- Private internal distribution remains subject to upstream licensing. Device performance, mobile IL2CPP runtime, native crash and remote delivery are separate acceptance gates; see Documentation~/RELEASE.md.

## Historical development previews

The entries below record earlier development checkpoints and do not supersede the v0.1.0 setup or verification scope.

## 0.3.0-preview.6 — 2026-11-06 (not published)

- Make imported Quick Start auto-register its eight in-memory cheats in debug builds, wait for the runtime and rebind on registry replacement; preserve authored-host precedence and standalone lifecycle behavior. Document import-to-Play and removal.

- Extend the Geist/flat theme to direct-instantiated confirmation prompts, PIN keypads and debugger modal surfaces. Use a translucent 70% HUD card while retaining full text opacity and drag-only hit testing; keep confirmation/access behavior unchanged.

- Restyle the debugger with graphite surfaces, muted headers, restrained red accents and flat nine-slice controls with thin edges. Use Geist Sans for UI and Geist Mono for stack details, log previews, numeric inputs and FPS HUD. Preserve game fonts, icons and existing access/cheat behavior.

- Keep No matching options inside the results viewport below search, centered without intercepting input. Add optional Inter Regular font and Kenney CC0 PNG surfaces to debugger catalog/clone styling; preserve icon art, theme tint, game fonts and legacy fallback.

- Replace global pin selection with a compact pin/unpin button in each category header. Only the icon changes for none/partial/all pinned; no selection wash or extra toolbar row. Batch large categories across frames and cancel on tab exit. Only pinned remains a filter.

- Move HUD mode controls out of the Performance banner into the scrollable content below it.
- Preserve full received message/stack trace within bounded console memory rather than clipping at 2,048/4,096 characters. Keep pending and visible history within their budgets, including after Clear.
- Remove the 2,600-character details cutoff. Long details use 12,000-character pages with Previous/Next, plain-text wrapping and full original Copy, avoiding the uGUI single-mesh vertex limit.


## 0.3.0-preview.5 — 2026-11-06 (not published)

- Simplify HUD controls to display mode including Off; bottom-left default and free dragging. Keep advanced config/API compatibility without showing every tuning field.
- Start the navigation drawer closed at every width. Keep narrow console detail reading spacious.
- Add eight-position tap-trigger selector to Access settings and apply it in players.
- Rename internal presentation implementations to Dreamy service names with unchanged script GUIDs and MovedFrom metadata. Source integrations using former Impl names must update.
- Remove Gmail/SMTP delivery and configuration entirely; relay supports Telegram only. Historical changelog entries and third-party attribution remain as history.


## 0.3.0-preview.4 — 2026-11-06 (candidate, not published)

- Start automatically in Editor/development players without a scene prefab, config or sample. Add project-owned Automatic/Manual setting and explicit idempotent initialization. Authored disabled/Off hosts take precedence; late manual hosts replace automatic ownership safely.
- Stage a temporary Resources startup payload for builds; default release excludes catalog/UI and explicit ReleaseDiagnostics includes config only. Recover and clean only marked build staging.
- Persist an explicit allowlist of local HUD/FPS/console/category/pin preferences with asynchronous bounded JSON, debounce, backup recovery and per-path write sequencing. Keep credentials, report policy and game cheat values out of the file.
- Add runtime HUD display, FPS warning/colors and Reset local preferences controls. Remember free position across resolutions; capture hiding does not save Off. Preserve pending pin IDs until registration/unlock.
- Update onboarding/API/migration docs and add startup, reload, storage, handover, restore and build-payload regression coverage. Device/iOS/privacy and Gmail gates remain open.


## 0.3.0-preview.3 — 2026-11-05 (candidate, not published)

- Add RegisterCheats/RegisterContainer convenience extensions with disposable notification subscriptions and runtime shutdown ownership. Coalesce worker INotifyPropertyChanged events into main-thread refresh; retain bounded polling fallback.
- Add CheatConfirmation opt-in for plain reflected methods. Plain methods now run immediately; resource/ownership card grants always confirm. Sample reset/grant actions explicitly opt in. Typed Button confirmation is unchanged.
- Preserve surviving definitions, controls, cards and pin identity across structural changes; recompose only affected groups. Batch dynamic-container notifications and coalesce UI reconciliation. Guard stale bindings against same-ID owner replacement.
- Update default sample/host and documentation; add notification/thread/lifetime, nested batch, confirmation and dynamic UI regression tests. Earlier candidate receipts do not certify preview.3.

## 0.3.0-preview.2 — 2026-11-05 (candidate, not published)

- Add ResourceCheat/OwnershipCheat attributes for card roles and stable generated IDs, plus CheatId overrides for renamed members. The attribute adapter feeds the same authorization/confirmation/value-refresh registry.
- Make Quick Start ProjectCheats a partial/attribute example split by feature. Preserve its option IDs; reflected buttons now consistently require confirmation, including the sample grant action.
- Add migration/authoring documentation and regression coverage for card completeness, duplicate rollback, invalid metadata, confirmation and rendered resource/ownership cards.
- UI preview.1 build/archive receipts remain historical; they do not certify preview.2 or game-side reflected containers. No new mandatory package dependency or serialized GUID migration.

## 0.3.0-preview.1 — 2026-11-05 (candidate, not published)

- Implement the approved red/graphite UI workspace: compact header, wordmark crop sprite, wide sidebar, narrow overlay drawer, task cards and standard control fallback.
- Add explicit optional card metadata, search/category/pinned filtering and category collapse without unregistering options. Incremental large-catalog population avoids one synchronous construction burst and cancels on tab/owner lifecycle changes.
- Preserve live value subscriptions across inactive parenting and re-enable before Start. Resource confirmation includes amount and revalidates the same registered action/value; changed values do not execute.
- Add timestamped console rows, always-visible filters, future-entry collapse control, preserved selection/scroll and mobile detail Back.
- Align HUD metric/memory columns and reserve a dedicated multi-pointer-safe grip; retain default Off, shader fallback and shared sampler.
- Compact Editor settings and sample-only preview, grouped fields/validation, long-PIN bounded indicators, passphrase input separation and safer single-instance confirmation.
- Add aligned System rows and section copy, throttled optional refresh, regression coverage and visual/artifact verification.
- Public tab IDs, existing asset GUIDs and package independence are retained. Standalone copied custom HUD/panel assets need review against matching new base assets; old build receipts do not certify this redesign. Device input/GC/thermal/native crash and production privacy/reporting gates remain open; Gmail stays pending.

## 0.2.0-preview.6 — 2026-11-05 (release candidate, not published)

### Added
- Red Dreamy runtime theme, one visible corner logo, reversible sidebar at all widths and compact option flow. Runtime package-support Report Bug/fake shortcuts removed; Editor QA tools remain.
- Graphy mobile graph renderer and Roboto fonts with retained notices, shared FPS statistics, configurable HUD position/scale/thresholds, grip drag/clamp/reset, RAM/audio toggles. HUD defaults Off.
- Attribute-to-registry adapter with order/increment/range/read-only metadata and game-owned partial examples.
- Telegram local setup/discovery helpers, targeted single-flight fake screenshot delivery, explicit relay acceptance status. Gmail remains pending.

### Fixed
- Isolate registration/value observer exceptions so callers always receive their registration lease.
- Refresh visible/pinned values at 4 Hz and invalidate after writes/actions without rebuilding option containers; isolate unavailable getters and restore recovered bindings.
- Use a 0.5-second metric window for low-FPS rules instead of the 1024-frame history; keep historical statistics separate.
- Hide unmeasured Render/Update profiler legends; expose the actual frame-time series.
- Lazy graph material initialization and text-only fallback when the shader is absent/unsupported.
- Service-generation cache invalidation, ownership-safe unregister, re-enable cleanup and prevention of service creation during teardown; restore keyboard listener discovery.
- Single-owner capture operations, cancel/disable cleanup, shared overlay suppression and sensitive-screen rechecks before capture/copy/persistence.
- Whitelist distributed relay scripts/docs so local databases, logs and Python caches cannot enter the UPM archive.
- Close relay SQLite connections after commit/rollback and close rejected setup HTTP responses.
- Synchronize package/runtime version and update setup, migration, reporting and support documentation.

### Scope / migration
- Independent of Dreamy Core/UI/Graphy/Input System packages; no new Core dependency. Preserve existing GUIDs, config and sample edits. Rebuild project-owned visual variants only after reviewing overrides.
- This private internal candidate is not a production/mobile certification. Exact fresh acceptance and open gates are recorded in Documentation~/RELEASE.md; older preview build receipts do not certify this source.

## 0.2.0-preview.5

- Assign independent GUIDs to all owned package assets and sample templates so original SRDebugger can retain its original asset identity.
- Preserve importer settings and fileIDs; existing consumers must migrate references with Documentation~/GuidIsolationMap.json when upgrading from earlier previews.

## 0.2.0-preview.4

- Add editable delegate-bound resource and ownership cheat templates; no Core/Economy/Shop dependency in package Runtime.
- Render Int64 options, preserve exact text/arrow stepping beyond double precision, and propagate numeric bounds to UI controls. Int64 uses text/arrows; horizontal drag remains disabled to avoid precision loss.
- Swap typed option containers atomically during registration/disposal and keep numeric rollback displays synchronized.
- Cover actual Int64 controls, grant bounds and exact stepping in PlayMode.

## 0.2.0-preview.3

- Cancel pending PIN UI when access relocks on scene, focus or pause changes so it can reopen and authenticate normally.
- Add a PlayMode regression for scene transition during PIN entry and reopening the Options tab.

## 0.2.0-preview.2

- Add a direct Cheat Panel menu and runtime Options button with PIN guidance.
- Explain missing scene bootstrap and provide an explicit, undoable, duplicate-safe scene installer.
- Keep standalone demo navigation available without hiding the setup requirements.

## 0.2.0-preview.1

- Migrate SRDebugger/SRF to Dreamy assemblies, generic class names and GUID-preserving catalog assets outside Resources.
- Replace vendor Editor/signup/reporting endpoints with a styled Dreamy Control Center and local-first report flow.
- Integrate configurable 5-tap / 0000 defaults, long PIN/passphrase, session/cooldown and per-operation authorization.
- Connect typed grouped options to the responsive runtime panel, including bounds, confirmation and disposable ownership.
- Add a shared lightweight FPS/frame-time history service and independent HUD modes.
- Add bounded threaded log ingress, async downscaled capture, background JPEG encoding, durable outbox, retries and relay delivery.
- Add reference Telegram/Gmail/SMTP relay, fake scenarios, native evidence provider contract and Android prior-process exit metadata.
- Add clean-consumer UI/domain tests, player build checks and Release scene stripping.
- Keep real destination delivery, mobile/native fault tests and weak-device performance acceptance explicit; this is not a store-ready certification.

## 0.1.0-preview.1

- Initial standalone domain foundation, typed options and synthetic-report sample.
