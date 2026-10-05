# NativeSkinFixes

**Status: REMOVED 2026-10-05.** The code, tests, native project and shipped binaries are gone from the tree.

## What it was

Three MinHook detours into `TaleWorlds.Native.dll`, installed by a TAOM-built C++ DLL (`TAOM.NativeSkinFixes.dll`): a freeze of hand morphs under `covers_head` helmets, and hair and beard cloth physics that never registered. It was parked and disabled at the wiring level in `SubModule.cs` since 2026-07-08 and was never re-verified in game past v1.4.6.

## Why it was removed

The maintainer decided it is no longer needed. Bannerlord v1.5.4's release notes also say the engine now fixes the `covers_head` plus full-glove hand animation bug itself. They claim nothing for hair and beard cloth, so vanilla behaviour there is accepted as is.

## Where the code lives now

Git history only on this line: `git log --diff-filter=D -1 -- Main/Features/NativeSkinFixes` names the removal commit, and its parent holds the last copy. The `bannerlord-1.4.5` line (the 1.4.8 builds) still carries the feature, parked; the maintainer chose to leave it there (2026-10-05). The deleted paths:

- `Main/Features/NativeSkinFixes/` (installer and interop classes)
- `TAOM.Tests/Features/NativeSkinFixes/`
- `Dependencies/NativeSkinFixes.NativeHooks/` (the C++ project, including the vendored MinHook headers and libs)
- `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll`

The MCM setting `TaomSettings.EnableNativeSkinFixes` and the strings `taom_nativeskinfixes_loaded` and `taom_nativeskinfixes_degraded` went with it, as did the CI static-CRT step. Stale copies of the two DLLs in the dev install's `bin` folders and the 1.5.x testing channel were backed up to `E:\Backups\nativeskinfixes-removal-2026-10-05\` and deleted (the patreon and public channels carry the 1.4.8 line, which keeps the feature, so their copies stay), and the 1.5.x `tools/package_release.py` now refuses both names (`RETIRED_BINARIES`). The commit hook `check-native-dll-crt.sh` is retired separately, once its `settings.json` entry is removed.

## History

- Issues: #82, #304; the removal and its owed in-game check: #736
- [Lessons: native C++ port](../reviews/lessons/native-cpp-port.md)
- [RCA: the native port, 2026-05-26](../reviews/rca-native-skin-fixes-port-2026-05-26.md)
- [RCA: the static CRT, 2026-06-18](../reviews/rca-native-skin-fixes-crt-2026-06-18.md)
- [Investigation: the load failure, 2026-06-18](../investigations/native-skin-fixes-load-failure-2026-06-18.md)

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/migration/dr3-maintenance.md](../migration/dr3-maintenance.md)
- [docs/migration/v1.5.4-impact.md](../migration/v1.5.4-impact.md)
- [docs/reference/lotrlome-armory-snapshot/README.md](../reference/lotrlome-armory-snapshot/README.md)

<!-- backlinks-end -->
