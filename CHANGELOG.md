# Changelog

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
