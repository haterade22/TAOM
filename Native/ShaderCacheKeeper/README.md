# ShaderCacheKeeper (TAOM build)

A proxy `xinput9_1_0.dll` that keeps Bannerlord's compiled shaders when the module list changes but no module with
shader content did. Feature doc: [docs/features/shader-cache-keeper.md](../../docs/features/shader-cache-keeper.md).

Vendored from yotthani/bannerlord `HoN/ShaderCacheKeeper` at commit `1bbdc17d7602d55eee4050f921b762333f38c99c`
(MIT, (c) 2026 yotthani, see `LICENSE`); TAOM's changes are listed at the top of `keeper.c`. Not taken:
`make_release.ps1`, `test/game_test.ps1`, `test/wait_then_game_test.ps1` (machine-specific paths and a zip
release flow TAOM does not use; TAOM ships through the launcher manifest).

```
python build.py                         # out\xinput9_1_0.dll (MSVC x64 tools via vswhere, or VCVARS64)
pwsh -File test\run_tests.ps1           # exit code = failed checks; writes only under %TEMP%\keeper_test
```
