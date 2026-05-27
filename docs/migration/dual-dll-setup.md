# Dual-DLL Setup — 1.3.15 backup + 1.4.5 live install

Procedure for keeping TAOM buildable against both Bannerlord 1.3.15 (for verification of the source branch) and 1.4.5 (the migration target) during the v1.4.5 migration window.

## Why dual-DLL is needed

The migration spans multiple weeks. Throughout, we need to:
- Decompile 1.3.15 to verify what a method/signature looked like before
- Decompile 1.4.5 to know what changed
- Run TAOM tests against either version
- Diff vanilla XML between versions (SandBox / SandBoxCore ModuleData)

Steam does not retain prior versions. **Once Steam updates Bannerlord to 1.4.5, the 1.3.15 DLLs are gone unless we backed them up.** Mods (LOTRLOME, Lib.Harmony) are version-pinned and reacquirable from Steam Workshop / NuGet; TaleWorlds-owned DLLs are not.

## What was backed up (2026-05-21)

| Location | Contents | Size |
|---|---|---|
| `E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client\` | Full 1.3.15 `bin/Win64_Shipping_Client/` mirror — 85 TaleWorlds DLLs + .NET runtime + launchers. Standard install layout so `BANNERLORD_OVERRIDE_DIR=E:\BannerlordBackup\1.3.15` resolves HintPaths correctly. | 1.475 GB, 8,568 files |
| `E:\Decompiled_Bannerlord_v1.4_OLD\` | Stale 1.4.x decompile archived (pre-existing folder) | 8,036 files |
| `~/.taom-src/v1.3.15\` | Existing on-demand decompile cache (taom-src skill) | varies |

Version stamp confirmed: `<Version><Singleplayer Value="v1.3.15"/></Version>` in `E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client\Version.xml`.

> **Layout note (2026-05-22):** the backup was originally created at `E:\BannerlordBackup\1.3.15-bin\Win64_Shipping_Client\` and reorganized to the standard `<root>\bin\Win64_Shipping_Client\` layout so `Directory.Build.props` can swap `GameFolder` to the backup via `BANNERLORD_OVERRIDE_DIR` without per-csproj HintPath surgery.

## Steam update procedure

1. **Disable Steam auto-update**: Steam → Bannerlord → Properties → Updates → "Only update this game when I launch it".
   - This prevents Steam from updating mid-migration after a reboot.

2. **Verify backup is buildable** (one-time pre-flight check) — before letting Steam update:
   ```powershell
   # Temporarily point the override env var at the backup; ensure dotnet build works against it
   $env:BANNERLORD_OVERRIDE_DIR = "E:\BannerlordBackup\1.3.15"
   dotnet build Main\TAOM.csproj -p:DisableModuleCopy=true --verbosity quiet
   Remove-Item Env:\BANNERLORD_OVERRIDE_DIR   # restore default targeting
   # (Note: backup contains only bin/, not full install — partial verification only)
   ```
   Note: the backup is bin-only, not a full install. For a full-install verification, the live install at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\` is still 1.3.15 at backup time.

3. **Let Steam update to 1.4.5**: launch Bannerlord through Steam → it downloads + applies the update → close.

4. **Verify post-update state**:
   ```powershell
   Get-Content "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Version.xml"
   # Should show: <Singleplayer Value="v1.4.5"/>
   ```

5. **Set environment variables** for dual-DLL workflow:
   ```powershell
   # Persistent (default build target — live 1.4.5 install):
   setx BANNERLORD_GAME_DIR "E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
   # Persistent reference path (for tooling — decompile, diff, etc.):
   setx BANNERLORD_1_3_15_DLLS "E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client"

   # Per-shell override — switch the build target to the 1.3.15 backup:
   $env:BANNERLORD_OVERRIDE_DIR = "E:\BannerlordBackup\1.3.15"
   # ... run dotnet build / dotnet test against 1.3.15 ...
   Remove-Item Env:\BANNERLORD_OVERRIDE_DIR   # return to default 1.4.5 target
   ```
   - `BANNERLORD_GAME_DIR` → live 1.4.5 install (default build target).
   - `BANNERLORD_OVERRIDE_DIR` → opt-in override consumed by `Directory.Build.props`. When set AND `<override>\bin\Win64_Shipping_Client\Bannerlord.exe` exists, `GameFolder` resolves to the override instead of `BANNERLORD_GAME_DIR`. Otherwise silently ignored. **Set this per shell, not persistently** — persistent override would break the default build.
   - `BANNERLORD_1_3_15_DLLS` → 1.3.15 backup path for tooling (decompile scripts, taom-src skill). Not consumed by `Directory.Build.props`.

6. **`Directory.Build.props` is wired** (done 2026-05-22) — it honors `BANNERLORD_OVERRIDE_DIR` with the existence gate above. No further csproj edits needed; existing `$(GameFolder)\bin\$(GameBinariesFolder)\...` HintPaths in `Main/TAOM.csproj` and `TAOM.Tests/TAOM.Tests.csproj` continue to work unchanged.

## Side effect: live game is now 1.4.5

After step 3, the user's playable Bannerlord install is 1.4.5. Implications:

- **Playing 1.3.15 again** during the migration requires manually overlaying the backup `bin/` over the live install — and reverting when done playing 1.4.5. This is a workflow cost.
- **Mods bound to 1.3.15** (LOTRLOME_Armory, etc.) may or may not work under 1.4.5 — that's part of what the migration validates.
- **Save games** created on 1.3.15 should load on 1.4.5 (TaleWorlds maintains save compat across minor versions), but TAOM's SyncData persistence is a separate question.

## Restoring 1.3.15 temporarily (if ever needed)

```powershell
# Stash 1.4.5 binaries
Move-Item "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client" `
          "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client.v1.4.5.bak"

# Overlay 1.3.15 binaries
Copy-Item -Recurse "E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client" `
                   "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"

# Play 1.3.15 ...

# Restore 1.4.5
Remove-Item -Recurse "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
Move-Item "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client.v1.4.5.bak" `
          "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
```

⚠️ The backup contains DLLs only, not ModuleData. If TaleWorlds changed `SandBox/ModuleData/*.xml` between 1.3.15 and 1.4.5 (which they did — see [v1.4.x-equipment-overhaul.md](v1.4.x-equipment-overhaul.md) §"Updated vanilla module XMLs"), running 1.3.15 binaries with 1.4.5 vanilla XML will produce undefined behavior. Restoring full 1.3.15 means restoring the SandBox / SandBoxCore / StoryMode ModuleData too. This is **not currently backed up** — if you need full 1.3.15 playable, plan to re-download via Steam beta branches (TaleWorlds usually keeps 1-2 prior minor versions accessible).

## Decompile workflow (after Steam update)

```powershell
# Archive the stale 1.4.x decompile (DONE 2026-05-21)
# Move-Item E:\Decompiled_Bannerlord E:\Decompiled_Bannerlord_v1.4_OLD

# Fresh 1.4.5 decompile (driven from live install)
$src = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
$dst = "E:\Decompiled_Bannerlord"
# (decompile script TBD — runs ilspycmd over every DLL, organized by category)
```

S0 must write a `tools/decompile_to_folder.ps1` that wraps `ilspycmd` for the bulk-decompile case.

## taom-src skill cache

The `tools/taom-src.ps1` skill caches decompiled types under `~/.taom-src/<version>/`. Current state:
- `~/.taom-src/v1.3.15/` — populated incrementally over months of development
- `~/.taom-src/v1.4.5/` — empty, will populate as S0 + later sessions decompile

The script currently has `$Version = 'v1.3.15'` hardcoded (line 26). S0 task: change to **auto-detect from `Version.xml` in `Get-BinDir`** so the cache automatically tracks whichever Bannerlord version `$env:BANNERLORD_GAME_DIR` currently points at.

## Cross-references

- [v1.4.x-overview.md](v1.4.x-overview.md)
- [v1.4.x-changes.md](v1.4.x-changes.md)
- [v1.4.x-equipment-overhaul.md](v1.4.x-equipment-overhaul.md)
- [v1.4.x-taom-impact.md](v1.4.x-taom-impact.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/migration/templates/README.md](templates/README.md)
- [docs/migration/TRACKING.md](./TRACKING.md)
- [docs/migration/v1.4.x-overview.md](./v1.4.x-overview.md)
- [docs/migration/v1.4.x-taom-impact.md](./v1.4.x-taom-impact.md)

<!-- backlinks-end -->
