# Third-party notices and source provenance

This private internal package contains a modified fork of **SRDebugger 1.12.1 / SRF**, originally © Stompy Robot LTD. Namespace, class names, assembly boundaries, loading, styling, access, input, profiling and reporting have been changed by Dreamy. Original documentation is retained in `Documentation~/SRDebugger-original-README.txt`; source history and the migration inventory are retained alongside it. Renaming does not remove upstream ownership or restrictions.

The imported vendor README prohibits unauthorized distribution. The package is not offered as an independently redistributable public library. Consumers must have appropriate rights for vendor-derived content; keep this repository private and retain these notices.

Icons originated from Icons8 and are used only within the debugger panel, consistent with the source README restriction. The original background pattern credits Subtle Patterns. Orbitron is credited to the League of Moveable Type and Source Code Pro to Adobe under OFL; original font notices remain under the imported font directory. Dreamy branding uses supplied Dreamy artwork, not a modified upstream vendor trademark.

Selected Graphy implementation and HUD fonts are reused from upstream commit `1068f3de2bc9f1e4a905150abd59f31e6dead679`, as listed below. MIT and separate font licenses are retained.

The relay uses only Python's standard library. No Telegram bot secret, mailbox password, OAuth token or authorized destination is included.


Graphy 4.0.0, commit 1068f3de2bc9f1e4a905150abd59f31e6dead679: MIT, copyright Martín Pane. GraphyFpsWindow adapts G_FpsMonitor into a caller-fed window, computes low samples every 30 fed frames and has no independent Update/singleton/Input System dependency. Original header retained; full license in Documentation~/Graphy-MIT-LICENSE.txt. The full Graphy prefab is not copied; visual source and font reuse are listed below.

Graphy visual integration (1068f3de2bc9f1e4a905150abd59f31e6dead679): GraphMobile shader and G_GraphShader helper under Runtime/Engine/ThirdParty/Graphy. Only shader name/helper namespace isolated. Roboto Regular/Bold reused for the HUD under Apache 2.0; bundled Roboto-LICENSE.txt retained. No Graphy singleton, InputSystem dependency or extra FPS collector.
