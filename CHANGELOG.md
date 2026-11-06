# Changelog

## 0.3.0-preview.6 — 2026-11-06 (not published)

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
