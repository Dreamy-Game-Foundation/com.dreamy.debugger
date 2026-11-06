# Third-party notices and source provenance

This private internal package contains a modified fork of **SRDebugger 1.12.1 / SRF**, originally © Stompy Robot LTD. Namespace, class names, assembly boundaries, loading, styling, access, input, profiling and reporting have been changed by Dreamy. Original documentation is retained in `Documentation~/SRDebugger-original-README.txt`; source history and the migration inventory are retained alongside it. Renaming does not remove upstream ownership or restrictions.

The imported vendor README prohibits unauthorized distribution. The package is not offered as an independently redistributable public library. Consumers must have appropriate rights for vendor-derived content; keep this repository private and retain these notices.

Icons originated from Icons8 and are used only within the debugger panel, consistent with the source README restriction. The original background pattern credits Subtle Patterns. Orbitron is credited to the League of Moveable Type and Source Code Pro to Adobe under OFL; original font notices remain under the imported font directory. Dreamy branding uses supplied Dreamy artwork, not a modified upstream vendor trademark.

Selected Graphy implementation and HUD fonts are reused from upstream commit `1068f3de2bc9f1e4a905150abd59f31e6dead679`, as listed below. MIT and separate font licenses are retained.

The relay uses only Python's standard library. No Telegram bot secret, mailbox password, OAuth token or authorized destination is included.


Graphy 4.0.0, commit 1068f3de2bc9f1e4a905150abd59f31e6dead679: MIT, copyright Martín Pane. GraphyFpsWindow adapts G_FpsMonitor into a caller-fed window, computes low samples every 30 fed frames and has no independent Update/singleton/Input System dependency. Original header retained; full license in Documentation~/Graphy-MIT-LICENSE.txt. The full Graphy prefab is not copied; visual source and font reuse are listed below.

Graphy visual integration (1068f3de2bc9f1e4a905150abd59f31e6dead679): GraphMobile shader and G_GraphShader helper under Runtime/Engine/ThirdParty/Graphy. Only shader name/helper namespace isolated. Roboto Regular/Bold reused for the HUD under Apache 2.0; bundled Roboto-LICENSE.txt retained. No Graphy singleton, InputSystem dependency or extra FPS collector.

## Interface refresh

Inter Regular 4.1 by The Inter Project Authors, SIL Open Font License 1.1. Source: https://github.com/rsms/inter/releases/tag/v4.1 . Original static TTF and license are in Runtime/Branding/Interface.

Two original grey PNG surfaces from Kenney UI Pack 2.0, CC0. Source: https://kenney.nl/assets/ui-pack . License and original file mapping are in Runtime/Branding/Interface/SOURCES.md.

## Geist typography

Geist Sans and Geist Mono v1.9.1, Copyright Vercel in collaboration with basement.studio. Original unmodified static TTF files, SIL Open Font License 1.1. https://github.com/vercel/geist-font/releases/tag/v1.9.1 . License retained in Runtime/Branding/Interface/Geist-LICENSE.txt.

## Material Compact active skin

Barlow Regular and SemiBold by Jeremy Tribby / Barlow authors, SIL OFL 1.1; JetBrains Mono Regular by JetBrains authors, SIL OFL 1.1. Original font files and notices reside in Runtime/Branding/Material. Material Design Icons by Google, Apache 2.0; full license retained there. Selected icon sources and hashes are recorded by the parent workspace's resource-backed concept inventory. Local circle/rounded-fill/rounded-outline shapes are original simple geometry. The alpha Dreamy wordmark is extracted from supplied DreamyBanner artwork; it is not a font substitution or AI redrawing.

Historical Inter/Geist/Roboto/vendor assets and notices remain for serialized compatibility; the current skin selects Barlow/JetBrains Mono. See Documentation~/MATERIAL_UI.md for source URLs and import choices.
