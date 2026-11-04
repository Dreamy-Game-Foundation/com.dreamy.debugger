# Phase acceptance contract

A phase has separate implementation and acceptance status. Only fresh matching receipts can mark a gate passed.

| Phase | Automated acceptance | Manual/device acceptance |
| --- | --- | --- |
| 0 baseline | Inventory hashes, manifest/lock, source assembly graph, compile baseline | Original UI behavior and CPU/GC/memory baseline |
| 1 package/fork | Standalone consumer compile without Core/UI/Graphy/Input System, GUID/reference audit, dependency cycles, no sample references in Runtime | Import/migrate source prefabs; no missing script; package upgrade |
| 2 access/cheats | Default 5 taps + 0000; custom count/PIN; timeout/reset/pointer/cooldown/TTL; grouped bool/int/float/enum/string/read-only/action; duplicate rollback, owner disposal, permissions and confirmation | Touch/keyboard, safe area, focus, scene unload, no interception of game input, live option UI |
| 3 HUD | FPS window calculation, sampler count, enable/disable cleanup | Off/FPS/Compact/Detailed, portrait/landscape, p95 CPU and GC on device |
| 4 local reports | Bounded queues, dedupe, disk full/corrupt bundle, restart recovery, capture cancellation/late callback/resource cleanup | GPU readback support/error, low memory, screenshot overlay exclusion, stutter measurement |
| 5 relay | Fake success/offline/429/5xx/reject/timeout/cancel, ack loss, idempotency, backoff/quota and dead letter | Test relay and destination delivery with approved test credentials |
| 6 crashes | Provider mapping/correlation, stale screenshot age, abnormal session classification | Dedicated test builds: managed exception/native crash/ANR/OS kill/OOM; never in user scene |
| 7 release | Sample import/customization/upgrade, release-off and diagnostics builds, IL2CPP/stripping, dependency checks | Clean consumer + weak device smoke/performance and rollback |

Use synthetic report data with an explicit fake marker. Never crash the user's active scene to test a report. Fake transport is necessary but cannot prove delivery or native crash support. Performance needs measurements; no unit test claims “no lag”.
