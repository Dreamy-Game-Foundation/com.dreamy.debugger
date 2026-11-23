# Runtime Hierarchy and Inspector (internal candidate)

Open the debugger, unlock the existing access session, and choose **Hierarchy**. The internal `Scene` key, catalog address and enum value 6 remain compatible. The tab uses the shipped catalog and prefabs; consumers do not run an authoring tool or install another inspector package.

`DebuggerConfiguration.EnableSceneInspection` defaults to true. Inspection exists in Development/Qa modes only. Off, ReleaseDiagnostics, a locked session and `DISABLE_DREAMY_DEBUGGER` do not expose the feature. Older catalogs without the Scene entry continue to load their existing tabs.

Search matches object names without case sensitivity, including inactive objects, and keeps ancestor context. Use `type:Rigidbody`, `tag:Player` or `layer:UI` to filter by component, tag or layer. Layer also accepts the numeric index. Rows show scene/path information to distinguish duplicate names. Use the chevrons to expand branches. The **⋯** menu contains scene and active filters, Refresh, Pause, Undo, Redo, and optional property tools. Indexing reconciles the loaded scenes and the runtime owner's DontDestroyOnLoad scene in bounded steps on unscaled time. “Scanning...” means the snapshot is still being collected. Debugger-owned roots are excluded by reference. Object identities and selection last only for this running session. Hidden panels and inactive tabs suspend scanning and discard edit drafts.

Wide layouts show both panes. Narrow and short layouts open Inspector after selection. **Objects** returns to the hierarchy and keeps its search and scroll position. Desktop arrows navigate the hierarchy when focus is inside this workspace and outside an input field; Ctrl+F focuses object search. Escape discards a draft, then returns from the narrow Inspector before the shell closes. Copy path writes to the local clipboard only.

## Editing

Set `AllowSceneEditing` on the configuration assigned to the running debugger. In Development or QA, a successful unlock enables edits immediately. Active, Tag and Layer are next to the selected object name. Their controls are enabled whenever the current session can edit. Changes are applied to live objects and can trigger game callbacks.

With touch, tap a number or vector axis to open the numeric editor for that value. Use minus and plus (step 1 for integers and 0.1 for floating-point types), or tap the value to enter it precisely. Volume, spatial blend, fill amount and alpha use a bounded slider. Apply validates the draft before writing. Cancel and loss of access discard it. On desktop, drag a numeric label or axis with the mouse or trackpad, and use Shift for fine adjustment.

Component headers use a bundled sprite and a foldout chevron. Common built-in types have matching icons, including separate icons for common collider shapes. Project scripts can assign a sprite by full C# type name under `HierarchyComponentIcons` in the debugger configuration. Script and component icons provide the default for types without a specific match.

Common native properties are exposed for Camera, Light, 3D and 2D physics, AudioSource, Animator, renderers, Canvas and common uGUI components. Custom Unity Editor inspectors, serialized asset editing and Editor Undo are outside the runtime Inspector.

Inputs keep a draft; losing focus does not write. The Inspector shows one Apply/Cancel bar while a value is being edited. Press Apply to commit or Cancel to discard the draft. Access is checked again at commit. If the current value differs from the draft baseline, the commit is rejected; Cancel and review the latest value. Closing/hiding the panel, changing tabs/selection, locking access or rebuilding a filtered section discards drafts. Pause freezes the view, not the game's time scale.

Public instance fields and fields with `[SerializeField]` are eligible. Static, constant, NonSerialized, HideInInspector and obsolete fields are excluded; readonly fields remain readonly. Arbitrary properties and methods are not invoked. Transform/RectTransform, Behaviour.enabled, activeSelf, tag and layer use explicit adapters. Tag opens a searchable picker for built-in and project tags. Populate custom tags on the configuration with **Tools › Dreamy Debugger › Copy project tags to selected debugger configurations**. An undefined tag fails without changing the object. Layer lists named project layers and changes only the selected object, leaving children unchanged. Instance ID and activeInHierarchy remain informational. LayoutGroup/Animator may overwrite RectTransform edits.

Vectors have separate X/Y/Z/W fields. Type one axis and press Apply/Enter; changing another axis in the game while editing raises a compound-value conflict. On numeric scalar names and axis labels, drag horizontally to preview a value and release to commit. Hold Shift for fine adjustment. Vertical gestures pass to the scroll view. Minus and plus are inside the touch editor. Losing access, hiding/unbinding the row, application focus loss or cancelling stops the gesture and discards the draft; it does not commit implicitly. Large integer arithmetic never passes through float/double. These operations change runtime state, not project assets.

Wide Inspector panes use inline labels and controls. Narrow or low workspaces switch to Hierarchy/Inspector navigation; field drafts remain separate from pooled views. The Inspector records up to 64 edits to top-level members. Each Apply, toggle, dropdown choice or drag release is one edit. Undo and Redo check permission and compare against the last committed value. If gameplay changed it, the debugger displays a conflict. History resets on a new unlock. Collection elements and nested members have no history because their identity can change while the game runs.

Supported inputs: bool toggles, enum dropdowns, integer types including long/ulong, float/double/decimal, string, enum flags, Vector2/3/4, Vector2Int/Vector3Int and Color. Bool/enum choices commit once per user action; backend refresh does not produce writes. Unknown enum values remain visible without normalization. Flags offer a searchable picker that preserves unknown bits when toggling named flags. Color offers a swatch, RGBA sliders and exact channel inputs. The sliders cover 0–1. The text fields also accept finite HDR values. Numeric overflow, NaN and Infinity are rejected; values never pass through float when their type is long/double/decimal. Strings are limited to 4096 characters. Error rows stop automatic reads until Refresh/Reload.

Serialized GameObject and Component fields can select an assignable object from the loaded scene index or clear the reference with None. Reveal selects the referenced object in the hierarchy. Native object-reference properties and asset references remain read-only. Arrays (one dimension) and exact List<T> instances are paged, with readonly size. Serializable class/struct fields expand to the configured depth, detect cycles and write boxed structs back through field/collection owners. Dictionaries, multidimensional arrays, delegates and polymorphic SerializeReference editing are unsupported. Nulls are displayed without constructing instances.

## Pin, compare and project presentation

Use **⋯ → Show Copy and Pin** to display contextual row actions. **Pin** promotes a field to the top of the selected object's Inspector, including while filtering. **Copy** copies the current scalar or vector value in the same invariant format used by the editor fields. Select **Compare** to capture the currently shown fields of the selected object, then select another object. Differing fields display the captured value. Matching components are paired by type and their order on the GameObject. Clear the member filter and expand any groups you want to include before taking the snapshot.

Use `HierarchyMemberPresentation` to match an inspected member by its full component type name and C# member name, supply a display label and group heading, and choose the built-in inline editor or picker when the member type supports it. The presentation profile cannot introduce new writable members. Use `HierarchyComponentIcons` to override icons.

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
