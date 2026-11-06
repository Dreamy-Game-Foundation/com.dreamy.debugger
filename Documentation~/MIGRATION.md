# Migration and project ownership

## Historical source migration in the sandbox

In the initial migration, the imported `Assets/Packages/StompyRobot` folder was moved into the debugger submodule using AssetDatabase.MoveAsset. Source scripts/assemblies were renamed while retaining MonoScript GUIDs; resource folders became normal catalog assets. Prefabs and animation bindings were updated through Unity Editor APIs, not YAML edits. The original source hash inventory is in the parent sandbox evidence; the original archive is retained outside the working tree for recovery.

The old standalone Editor windows, signup and vendor HTTP endpoint were replaced. Source history/notices remain under Documentation~ and are not runtime assemblies. A deleted upstream ScrollRectPatch left a dangling prefab component; a GUID-preserving temporary compatibility class allowed the Editor migration to remove that obsolete component. Final prefab validation must show no missing scripts.

New assembly names are Dreamy.Debugger.Runtime (pure domain), Foundation (internal UI helpers), Presentation (forked views/services), Engine (host/integration), optional InputSystem and Editor. There is no dependency on other Dreamy packages.

## Current source ownership

Dreamy is now an independent copy/fork with separate GUIDs (preview.5 onward). The original SR package under Assets/Packages/StompyRobot is retained for comparison and must not be moved, renamed or remapped. Do not delete .meta files to regenerate identities: that breaks serialized references. Preserve original SR assets and run only one bootstrap in a comparison scene. Validate script/reference coexistence after each original import.

Preview.6 changes no existing GUIDs. There are no mandatory Dreamy Core/UI dependencies. Public runtime and manifest version are both 0.2.0-preview.6. Reimport/compare editable samples to get the sample link.xml and partial examples; update preservation names for the game's own assembly.

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
