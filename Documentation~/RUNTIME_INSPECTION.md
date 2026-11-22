# Runtime Scene inspection (internal candidate)

Open the debugger, unlock the existing access session, and choose **Scene**. The tab uses the shipped catalog and prefabs; consumers do not run an authoring tool or install another inspector package.

`DebuggerConfiguration.EnableSceneInspection` defaults to true. Inspection exists in Development/Qa modes only. Off, ReleaseDiagnostics, a locked session and `DISABLE_DREAMY_DEBUGGER` do not expose the feature. Older catalogs without the Scene entry continue to load their existing tabs.

Search matches object names without case sensitivity, including inactive objects, and keeps ancestor context. Rows show scene/path information to distinguish duplicate names. Use the scene and active filters, chevrons, Pause/Resume and Refresh. Indexing reconciles the loaded scenes and the runtime owner's DontDestroyOnLoad scene in bounded steps on unscaled time. “Scanning...” means the snapshot is still being collected. Debugger-owned roots are excluded by reference. Object identities and selection last only for this running session. Hidden panels and inactive tabs suspend scanning and discard edit drafts.

Wide layouts show both panes. Narrow layouts open Inspector after selection; Back keeps hierarchy scroll/search. Desktop arrows navigate the hierarchy when focus is inside this workspace and outside an input field; Ctrl+F focuses object search. Escape discards a draft, then returns from the narrow Inspector before the shell closes. Copy path writes to the local clipboard only.

## Editing

Editing is disabled by default. Enable `AllowSceneEditing` in the configuration, unlock the session, and press **Read only** to confirm runtime editing. The confirmation explains that game callbacks may save data, synchronize state or trigger other effects. No asset serialization or UnityEditor.Undo is performed by this feature. It cannot roll back arbitrary game side effects.

Inputs keep a draft; losing focus does not write. Press Apply to commit or Reload to discard/reload. Access is checked again at commit. If the current value differs from the draft baseline, the commit is rejected; Reload and review the latest value. Closing/hiding the panel, changing tabs/selection, locking access or rebuilding a filtered section discards drafts. Pause freezes the view, not the game's time scale.

Public instance fields and fields with `[SerializeField]` are eligible. Static, constant, NonSerialized, HideInInspector and obsolete fields are excluded; readonly fields remain readonly. Arbitrary properties and methods are not invoked. Transform/RectTransform, Behaviour.enabled and activeSelf have explicit adapters/actions. World position, tag, layer, instance ID and activeInHierarchy are readonly. LayoutGroup/Animator may overwrite RectTransform edits.

Supported inputs: bool toggles, enum dropdowns, integer types including long/ulong, float/double/decimal, string, enum flags, Vector2/3/4, Vector2Int/Vector3Int and Color. Bool/enum choices commit once per user action; backend refresh does not produce writes. Unknown enum values remain visible without normalization. Flags use names or numeric values. Vector/color inputs use comma-separated invariant numbers (RGBA for Color), with a live Color preview. Numeric overflow, NaN and Infinity are rejected; values never pass through float when their type is long/double/decimal. Strings are limited to 4096 characters. Error rows stop automatic reads until Refresh/Reload.

Unity object references are readonly; Reveal navigates to referenced GameObjects/components. Arrays (one dimension) and exact List<T> instances are paged, with readonly size. Serializable class/struct fields expand to the configured depth, detect cycles and write boxed structs back through field/collection owners. Dictionaries, multidimensional arrays, delegates and polymorphic SerializeReference editing are unsupported. Nulls are displayed without constructing instances.

## Configuration and stripping

Defaults: refresh 0.25 seconds, scan budget 1 ms/frame, maximum nested depth 4, collection page size 32. Scans are also capped at 512 visited nodes per tick. Rendering recycles a viewport-sized row pool. These are implementation limits, not measured device performance claims.

Reflection-only game fields may be stripped by IL2CPP. Preserve just your inspected types/fields, for example in the **consumer project's** `Assets/link.xml`:

```xml
<linker>
  <assembly fullname="MyGame.Runtime">
    <type fullname="MyGame.DebuggablePlayer" preserve="fields" />
  </assembly>
</linker>
```

Replace both names with your real assembly/type. Do not preserve every assembly. Import Quick Start and add `SceneInspectionExample` to a demo object to try nested structs, high-precision scores, collections and references. A custom game type needs its own stripping test; Preserve on the sample does not preserve customer code.

## Validation limits

This candidate needs the documented platform, input, visual, performance and commercial gates before a release claim. Editor/Mono tests do not prove Android/iOS IL2CPP, physical touch/keyboard behavior, safe area or thermal budgets. Public redistribution rights for existing Imported Presentation remain governed by RELEASE.md; no upstream inspector source or assets were copied for this feature.
