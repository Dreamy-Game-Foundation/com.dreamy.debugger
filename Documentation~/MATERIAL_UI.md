# Material Compact asset and ownership map

The skin implements the resource-backed Material Compact concept. Production assets are editable uGUI prefabs, individual imported PNG sprites and original font files, not a flattened concept image.

| Resource | Source | License / import |
|---|---|---|
| Barlow Regular / SemiBold | https://github.com/google/fonts/tree/main/ofl/barlow | OFL, bundled notices; original TTFs |
| JetBrains Mono Regular | https://github.com/JetBrains/JetBrainsMono | OFL, bundled notice; original TTF |
| Material icons | https://github.com/google/material-design-icons | Apache 2.0, bundled license; selected 32×32 PNGs |
| Circle, rounded fill/outline | Local simple geometry | Dreamy-authored 32×32 PNGs; sliced border 8 only on surfaces |
| Dreamy Wordmark | Supplied Runtime/Branding/DreamyBanner.png | Alpha extraction of original lettering, 512×84; preserve aspect |

Production files live under Runtime/Branding/Material. Fonts total 482,640 source bytes; this is not an atlas-residency measurement. Tiny sprites have no mipmaps/Read-Write. Default uncompressed imports preserve a crisp comparison baseline; Android/iPhone overrides use ASTC 4×4 (32px icons / 512px logo). These are authoring presets, not proof of device format support, final GPU memory or artifact quality.

Existing catalog/prefab GUIDs remain stable. The catalog selects the new fonts/sprites; historical files remain for compatibility. Editor/DebuggerMaterialAssets.cs is an explicit maintainer authoring utility using AssetDatabase/PrefabUtility, never an automatic first-import migration. Consumers need no setup operation. A writable maintainer checkout is required to regenerate its assets; PackageCache consumers should use the shipped saved prefabs.

Panel/PIN reuse the existing safe-area owner. HUD deliberately anchors to the full canvas and uses the existing scale/drag owner. Dynamic pinned-category controls bind the same option registry; they are lazy, paged and visible only while authorized. They never own economy/save behavior. No second frame sampler, logger or external UI package was introduced.

Verification scope and visual receipts are tracked in the parent workspace under docs/evidence/debugger/material-implementation. Native device, build-size/draw-call/GC and actual Editor-window visual acceptance must be measured independently. The AI concept is a design target, not evidence of these checks.
