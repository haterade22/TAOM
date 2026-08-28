# Scene Scripts — Attribution

TAOM's `Main/SceneScripts/` directory contains scene scripts (`ScriptComponentBehavior`-derived classes that Bannerlord's scene editor attaches to map entities) for use by TAOM map authors. The behavioural inspiration for some of these scripts comes from the **Alliance** multiplayer mod (https://github.com/Byak0/Alliance), which ships its own `Alliance.Common/Extensions/CustomScripts/Scripts/` library.

Alliance is licensed under **GPL v3 (copyleft)**. To keep TAOM license-neutral, every script that draws inspiration from Alliance is produced by a **clean-room rewrite** procedure — Alliance source is read once to extract a behavioural spec, the spec is committed to TAOM, and the implementation is written from the spec without re-reading Alliance source.

## Procedure (every CS_* script ported this way)

1. **Spec extraction.** Alliance source is read exactly once. A behavioural spec — property names, attribute types, lifecycle methods, observable behaviour — is written to `docs/scene-scripts/specs/<script-name>.md`. The spec contains *no code* from Alliance, only descriptions of what the script does.
2. **Implementation from spec.** TAOM's implementation is written using only the spec as input. Variable names, helper class names, helper decomposition, exact algorithm expression are TAOM's choices. Industry-standard patterns (quad-strip along a curve, integer state machine, hex-colour parsing) are not copyrightable per *Google v. Oracle* (2021).
3. **Cross-check pass.** Once the implementation compiles and tests pass, Alliance source is re-read one final time to confirm no accidental structural collision. If any found, the implementation is restructured.
4. **Attribution.** Every TAOM file ported this way carries a file-header comment citing Alliance as behavioural inspiration and pointing to its spec document. No Alliance source code appears in TAOM at any commit.

## Why class names like `CS_Road` were preserved

Alliance's convention is to prefix scene scripts with `CS_` ("Custom Script"). TAOM kept this naming for two reasons: (a) class names and short identifiers aren't copyrightable under US law (*Google v. Oracle*); (b) map-authoring community vocabulary uses these names — a map maker searching for "Bannerlord road script" finds Alliance docs and TAOM scripts together.

## Scripts ported under this procedure

| TAOM file | Spec | Alliance source (read once) |
|-----------|------|-----------------------------|
| `Main/SceneScripts/CS_Road.cs` | `docs/scene-scripts/specs/cs-road.md` | `Byak0/Alliance@version/0.6.0.0:Alliance.Common/Extensions/CustomScripts/Scripts/CS_Road.cs` |

(Future entries appended as additional scripts ship.)

## Scripts NOT ported

The other 12 scripts in Alliance's CustomScripts folder were deep-dived and triaged — see `docs/features/scene-scripts.md` for the table with reasons (most depend on Alliance-internal infrastructure that we don't and won't have).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/asset-provenance.md](../reference/asset-provenance.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)

<!-- backlinks-end -->
