# Changelog

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
