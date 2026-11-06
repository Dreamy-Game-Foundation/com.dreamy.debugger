# Material Compact (unreleased)

Preserve package meta GUIDs and game-owned partial cheats. Existing HUD mode/free-position preferences remain valid; new hud.cycle and hud.close preferences default true. Reset preferences only when testing first-import defaults. Category pin stable IDs retain their meaning, but the in-game overlay now starts collapsed; tap its chip to expand or × to unpin. It uses the same option owners and never restores game values/actions.

Numeric PIN now has separate Cancel (×) and Delete buttons and a centered compact layout. Merge serialized BackspaceButton, HUD CycleButton/CloseButton and dock CloseButton fields into any game-owned prefab copies/variants. Base package assets already contain these bindings. No Setup Theme run is required. Native Editor-window visuals and mobile-device compression/performance need their own acceptance evidence.

---

# Upgrade to 0.1.2

Update package commit/version and restart Play/rebuild. Existing prefabs/config/metas remain valid; layout adapts at runtime. HUD deliberately ignores safe area and retains normalized free positions, so a restored position can sit closer to screen edges than before. It is smaller by default without DPI boosting; explicit HudScale remains honored. The tap entry target is now visually invisible even when ShowCornerTrigger=true; set false to disable the target entirely. Numeric PIN uses a landscape arrangement when appropriate; the credential, gesture and cooldown policy are unchanged.

# Upgrade to 0.1.1

Update the Git dependency from the affected 0.1.0 commit `5912da1` to the verified 0.1.1 commit. Unity resolves a new package-cache location for a new Git hash; pin the fixed hash rather than keeping the old resolved commit. Do not edit Library/PackageCache or delete script meta files. Imported sample customizations, asmdefs, configuration and IDs need no migration. The change only narrows a packaging ignore rule that omitted the existing option controls from Git imports; the tarball had already contained them.

# Upgrade to v0.1.0

This is the requested internal numbering reset from the development label `0.3.0-preview.6`, not a rollback. Package identity stays `com.dreamy.debugger`; author, namespaces, meta GUIDs and stable cheat IDs are preserved. Older `v0.2.0-preview.*` tags are not moved/deleted. Explicitly select the v0.1.0 commit/archive (or tag after publication); semver ranges can otherwise prefer a numerically higher preview.

- Keep project Configuration, startup settings, prefab variants, imported samples and game-owned containers in version control. Resolve and compile the package, then restart Play.
- New imports of Quick Start auto-register eight in-memory demo controls in debug builds; no manually attached sample host is required. It waits for the actual runtime, rebinds on registry replacement and disposes its lease on disable. Inactive/authored sample hosts prevent automatic creation.
- An already imported sample is a project-owned copy. Compare/merge `ProjectCheatHost.cs`, partials, asmdefs and `link.xml`; do not blindly overwrite your wallet bindings. Do not leave two sample version folders declaring the same assembly/type names.
- Existing game hosts remain valid. Register once per runtime registry, dispose on owner disable, and rebind when the runtime or its registry changes. Custom partials must share class/namespace/assembly; preserve game assembly/type names in link.xml for IL2CPP.
- Default HUD is Off at BottomLeft with free dragging. HUD controls are in Performance below the banner; saved user presentation preferences take precedence over config defaults. Reset local preferences when testing clean defaults.
- Automatic startup is the default. To keep explicit startup choose Manual; to disable the runtime choose Mode Off on the effective config. Preserve authored-host precedence, including inactive/disabled hosts.
- Telegram is the only report destination. Remove old mail deployment configuration and keep bot tokens on the relay. Original SRDebugger remains separate with its own GUIDs; use one active debugger in a comparison scene.

No gameplay save or outbox migration is introduced by this version label. Do not increase PreferenceRevision merely for a package version bump. For rollback pin the known package commit and restore matching game config/variants/sample source; keep third-party notices.

---

# Historical preview migration notes

The sections below describe earlier transitions; current setup and version policy above take precedence.

# Upgrade to 0.3.0-preview.5

HUD mode moved from Cheats to Performance. Default placement is BottomLeft, while saved free positions remain respected. TapPosition defaults TopLeft and supports eight edges/corners in Editor and players. Drawer starts closed on every open. Existing advanced config fields/APIs remain compatible but are hidden from the simplified settings window.

Internal implementation types now use Dreamy names (DreamyProfilerService, DreamyTriggerService, DreamyPanelService, DreamyPinnedOptionsService, DreamyOptionsService and related services). Existing script meta GUIDs are preserved; MovedFrom records previous types. Custom code referencing the old Impl names must update. Source SR package and third-party attribution are untouched.

Mail delivery backends/environment configuration are removed. Non-fake relay destinations must be Telegram. Update old deployments rather than retaining mail destinations.

---

# Upgrade to 0.3.0-preview.4

- Existing scene bootstrap/config/catalog GUIDs and cheat IDs stay valid. Authored bootstrap wins, including inactive/disabled/Off. No asset moves or meta regeneration.
- New projects use Automatic by default. No sample or bootstrap is required. Existing projects that deliberately want no implicit runtime should choose Manual in Window → Dreamy Debugger → Settings.
- The window initially shows its project startup configuration, or the active scene host configuration if no startup config was selected. Select an asset explicitly to make it the Automatic/build config; no arbitrary first-asset discovery.
- Local runtime presentation changes are now remembered. Existing authoring config remains the fallback. Turn PersistRuntimePreferences off for session-only behavior. Reset local preferences restores defaults without touching game save/outbox. Increment PreferenceRevision for intentional preference scope replacement, not on every release.
- Build adapter temporarily owns Assets/__DreamyDebuggerBuild only with its exact marker. Do not use that path for game assets. Default release emits no payload; ReleaseDiagnostics emits config without UI. A failed/interrupted build stage is recovered on the next Editor update/domain load/build. Never manually create Resources copies of the UI/catalog for release.
- Keep game-owned partial containers, samples, prefab variants and link.xml. Merge changed sample tests intentionally: standalone host tests now isolate themselves from Automatic startup. Runtime option values and actions are never replayed from preferences.

---

# Migration and project ownership

## Historical source migration in the sandbox

In the initial migration, the imported `Assets/Packages/StompyRobot` folder was moved into the debugger submodule using AssetDatabase.MoveAsset. Source scripts/assemblies were renamed while retaining MonoScript GUIDs; resource folders became normal catalog assets. Prefabs and animation bindings were updated through Unity Editor APIs, not YAML edits. The original source hash inventory is in the parent sandbox evidence; the original archive is retained outside the working tree for recovery.

The old standalone Editor windows, signup and vendor HTTP endpoint were replaced. Source history/notices remain under Documentation~ and are not runtime assemblies. A deleted upstream ScrollRectPatch left a dangling prefab component; a GUID-preserving temporary compatibility class allowed the Editor migration to remove that obsolete component. Final prefab validation must show no missing scripts.

New assembly names are Dreamy.Debugger.Runtime (pure domain), Foundation (internal UI helpers), Presentation (forked views/services), Engine (host/integration), optional InputSystem and Editor. There is no dependency on other Dreamy packages.

## Current source ownership

Dreamy is now an independent copy/fork with separate GUIDs (preview.5 onward). The original SR package under Assets/Packages/StompyRobot is retained for comparison and must not be moved, renamed or remapped. Do not delete .meta files to regenerate identities: that breaks serialized references. Preserve original SR assets and run only one bootstrap in a comparison scene. Validate script/reference coexistence after each original import.

Preview.6 changes no existing GUIDs. There are no mandatory Dreamy Core/UI dependencies. At that historical checkpoint public runtime and manifest version were 0.2.0-preview.6. Reimport/compare editable samples to get the sample link.xml and partial examples; update preservation names for the game's own assembly.

## Existing game code

- Replace `SRDebug` entry usage with the new debugger host and the documented `DreamyDebug` presentation compatibility API as appropriate.
- Replace partial `SROptions` extensions with project-owned option containers registered through `runtime.Cheats.Register(...)`; a partial class cannot extend a type in another assembly.
- Old SRF/SRDebugger namespace references require source migration. No binary source compatibility is promised across this preview fork.
- Use project-owned bootstrap/config variants. Do not modify imported package base assets.
- Context providers, save resets, rewards and gameplay cheats remain in project code.

## Updating the package

Pin a submodule commit or immutable release tag. Keep configuration/variants/imported samples in Assets. The production installer creates missing assets and keeps existing customization. The original starter folder may retain its original version directory as project-owned history; new consumers use the current package version directory.

Before importing a new version of a sample, compare it with your edits. Avoid importing duplicate sample assemblies from multiple version folders. The sandbox updated its untouched starter files only after comparing them against the previously committed baseline.

For rollback, point the submodule back to a known compatible commit and restore matching project variants/config from version control. A rollback to upstream SRDebugger also requires restoring the matching original source/meta/reference set; do not run both debugger bootstraps together.


## GUID isolation in 0.2.0-preview.5

Earlier previews retained upstream GUIDs. Preview.5 assigns separate GUIDs to Dreamy assets and sample templates. Existing consumers must remap their Dreamy references with GuidIsolationMap.json before importing original SRDebugger. Keep importer settings/fileIDs intact. Do not apply the map to original SR assets, third-party dependencies, source archives or historical evidence. The sandbox migrated its project-owned debugger variant/config and imported Dreamy samples. Package Runtime namespaces remain Dreamy. Original source archives under Documentation~ remain ignored by Unity.

Import original SR only after migration validation; use separate comparison scenes or enable one debugger bootstrap at a time. Actual SR coexistence compilation still needs verification after the original package is supplied/imported.


## 0.3.0-preview.1 workspace

Tab enum/key IDs remain Options and Profiler; visible names are Cheats and Performance. Existing .meta identities are preserved. Optional card metadata is runtime view data; legacy/attribute/partial options keep individual controls unless explicitly grouped. Import/compare the updated Quick Start resource/ownership templates; never overwrite customized samples blindly.

UI assets and runtime/editor code ship together. New view references are supplied by the updated base prefabs. Copied standalone custom prefabs need a deliberate visual merge; variants should review overrides. The confirmation canvas now uses the same constant-pixel/retina policy as the access panel, with an authored safe-area-constrained dialog. Game shared font assets and SR original source/meta stay untouched.

Main sandbox compilation has loaded DreamyDebug and SRDebug side by side; running both bootstraps simultaneously is not the supported comparison workflow. Use separate scenes or one active bootstrap at a time.


## 0.3.0-preview.2 — partial authoring

Existing typed registration stays valid. ResourceCheat derives balance/amount/grant IDs from its card ID; OwnershipCheat derives owned/unlock IDs. CheatId overrides individual IDs when migrating existing members. Keep IDs used by pins/automation. Resource roles require read-only numeric balance, writable numeric amount, and parameterless void action; ownership requires read-only bool and grant-only action. Keep categories consistent across a card. Invalid type/role combinations fail atomic registration.

Default sample ProjectCheats now uses partial files/attributes. Preserve modified imported samples and merge deliberately; add the new partial files and update link.xml when using the example. All reflected methods require confirmation, including the sample grant. Public setter properties stay editable; getter-only/private setters stay read-only. Only same-assembly partials combine; do not extend a package-owned partial class from a game assembly.


## 0.3.0-preview.3 — workflow

Plain reflected methods no longer require confirmation by default. Mark destructive methods with CheatConfirmation; resource/ownership card actions always confirm. Typed Button behavior is unchanged. Replace host Register(options.CreateOptions()) with runtime.RegisterCheats(options, prefix) to own notification subscriptions; keep the same prefix/explicit IDs. RegisterCheats cleans on lease disposal and runtime shutdown. Standalone registry RegisterContainer needs owner-thread FlushValueNotifications in its loop; direct From/Register remains supported without automatic subscription.

DynamicOptionContainer.BeginUpdate batches structural events; surviving presentation definitions/controls are reused, affected card groups are recomposed. Keep metadata immutable while registered and unregister/re-register when metadata changes. Original GUIDs and public tab keys remain unchanged.
