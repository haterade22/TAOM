Plan 035: make the release packager refuse a TAOM_Map scene that ships its terrain shader header without
its compressed shader cache, and report each packaged DLL's JIT-optimization state, so a release can no
longer silently send players a campaign map that compiles its shaders on first use.

WHY (verified 2026-10-02 by a read-only audit, re-check before planning):
- The testing channel's E:/LOTRAOM_Releases/testing/Modules/TAOM_Map/SceneObj/Main_map/ShaderCache/D3D11/
  holds only terrain_shaders_header_data.bin (45,832 bytes, 2026-09-28) and no compressed_shader_cache.sack,
  while the patreon and public channels ship a 1,835,980-byte sack (2026-09-14); the live dev install
  matches testing. 44 of 47 TAOM_Map scenes carry sacks. Without the sack the engine compiles the
  campaign-map shaders on first use, and yotthani measured that map-shader variants missing from the sack
  ("Missing shader from sack" in rgl_log) leave props undrawn until the compile is stored, which can last a
  first session (MithrilForge docs/engine/modding-kit.md:767-772, comparison only). Issue #448 is open:
  TAOM sacks are format 0x0782 where the engine expects 0x0783; read it before choosing the check's
  wording. Restoring the missing sack is the maintainer's art-pipeline step (FOR-MIKE), not this plan's.
- TAOM ships Debug builds on purpose (crash-report frames); the packaged TAOM.dll and TAOM.Dependencies.dll
  carry DebuggableAttribute with DisableOptimizations. This plan only REPORTS the state per DLL in the
  packager output (one line each); it never refuses a Debug build.

WHAT: find the release packaging tool (tools/package_release.py per the release docs; verify the name and
how /release calls it, .claude/skills/release/SKILL.md) and add: (1) a scene shader-cache check over every
TAOM_Map scene being packaged: a scene folder with ShaderCache/D3D11/terrain_shaders_header_data.bin and no
compressed_shader_cache.sack beside it is an error that names the scene (a scene with neither file is
fine); decide with the existing check conventions in that tool whether errors abort the packaging, and
follow them. (2) a DLL report: for each packaged managed DLL of TAOM's own (TAOM.dll, TAOM.Dependencies.dll,
others the tool packages), print whether its DebuggableAttribute disables JIT optimization (read the
attribute from metadata without loading the assembly into the Python process: a byte check of the
custom-attribute blob is what the audit used; a more robust approach that the tool's environment supports
is fine).

TESTS (tools/tests/, unittest): a temp tree with a scene that has both files (ok), header without sack
(error naming the scene), neither (ok), a sack without header (ok); the DLL report on small fixture
assemblies or byte fixtures with and without the DisableOptimizations bit. Record the Python suite's
failure set before the first edit (baseline.md says the first tools plan records it).

OUT OF SCOPE: rebuilding or copying any sack, editing E:/LOTRAOM_Releases or the live installs, changing
the build configuration.
