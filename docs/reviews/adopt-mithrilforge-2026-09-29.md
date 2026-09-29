# Adopting MithrilForge, second pass (2026-09-29)

Written for: TAOM maintainers deciding what to take from yotthani's MithrilForge.
Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)). First pass:
[adopt-yotthani-animation-handoff-2026-09-18.md](adopt-yotthani-animation-handoff-2026-09-18.md), whose claims and
tiers are not repeated here.

## What was read

| Source | Commit | Read |
|---|---|---|
| `Downloads/MithrilForge-main` (zip) | `a0b2a807`, 2026-09-13 | All 47 files: README, `docs/donor-findings.md`, `docs/delivery.md`, the plan, every `src/` and `tests/` file, build files, the five GLB fixtures' JSON headers. The same snapshot the 09-18 pass saw (extracted 2026-09-18 07:24) |
| `github.com/yotthani/MithrilForge` (private; the maintainer's account can read it) | `91149e11`, 2026-09-27 | Every file added or changed since `a0b2a807`, through `gh api`: `docs/anim-findings.md`, `src/MithrilForge.Anim/*` (9 files), the new CLI, `DdsWriter`, `MeshMaterialDump`, `tools/anim/*` (13 Python modules and a README), `examples/dualwield-recipe.json`, the Anim test project, `MeshMaterialDumpTests`, and the `NameCheckerTests` 1.5.3 diff |
| `yotthani/TpacTool-bannerlord` (the `vendor/TpacTool` submodule, at `5304748`) | none | **Not readable**: the API returns 404 for the maintainer's account, and the zip's copy is empty. Every writer and reader in MithrilForge depends on it |

## What it is

An offline net8.0 command-line tool, separate from any mod; mods consume only its output. Verbs:

- `build <file.glb | folder>`: a GLB file to a verified static-prop `.tpac` without the Modding Kit (clone a vanilla
  donor, replace geometry, UV and texture, remap GUIDs, reload and compare, delete on mismatch).
- `meshmats <AssetPackages dir> <out.csv>`: every LOD0 submesh's render material and textures. Written for TAOM's
  Armory: its test reads `Modules/LOTRLOME_Armory/AssetPackages` (the Osgiliath elite chest's plate and chainmail
  materials, read 2026-09-25).
- `texdds <dir> <names.txt> <out>`: a texture's top mip as a DDS with a DX10 header, BC7 included, without decoding.
- `anim <verb> --recipe <json>`: dump, mirror and re-wrap vanilla AnimationClip payloads (DualWield's left-hand
  attacks). The Python half in `tools/anim` decodes and re-encodes the clip segment `6c1e136f`, corrects what sparse
  key blocks cannot hold (`wrist_fix`, `chain_fix`, `key_fit`), builds combined clips, and checks blade roll and
  contact timing against vanilla offline (`validate`, `impact_sim`).

## Security

- **Safe to learn from:** yes.
- **Safe to run:** not yet. The C# and Python read and write only the recipe and `--out` paths, delete only their
  own rejected output, and open no network connection, launch no process, run no install hook and read no secret
  (one environment variable each: the game path, the recipe path). But the fork every verb runs on is unread. Its
  source, or the diff of its patches (`4c8dfa8`, `5304748`), is to be read before anything built on it runs here.
  Running a prebuilt `MithrilForge.exe` without that is the maintainer's decision, not a default.
- **Licensing:** MIT. ImageSharp is pinned to 2.1.11, the last Apache-2.0 line, on purpose (3.x is the Six Labors
  Split License).

## Findings new to TAOM, checked against TAOM

| # | Claim | TAOM status | Evidence (2026-09-29) |
|---|---|---|---|
| 1 | TAOM's four `AssetPackages/*.tpac` camp props are MithrilForge output | **Confirmed by layout** | Five items each, named and ordered as `TpacPropWriter.WriteMany` writes them, 8-byte aligned; no RDC entry; repo and install copies byte-identical |
| 2 | Those props render in game | **Refuted: they render nowhere** | The releases (`testing`, `patreon`) ship only the editor's `pack0.tpac`: 120 items, no Metamesh. The dev install logs `Loading packages $BASE/Modules/TAOM/Assets...` and never TAOM's `AssetPackages` (`rgl_log_73032.txt:203`). Every camp shows the vanilla fallback. Pre-existing since 2026-08-22 |
| 3 | Prop packages are 8-byte aligned; clip packages must be unaligned | Aligned: **confirmed on TAOM's four, and only there**. Clip half: MithrilForge's claim | Every Kit and vanilla package is packed (`map_icon_parts.tpac`: 6,266 segments, no gap). `tpac_clone_metamesh.serialize` re-serialises the four 25, 16, 21 and 20 bytes short. The props also lack the `f6304064` binding segment, carry zero checksums and keep the donor name on their LOD mesh |
| 4 | `UnknownUInt2 == 2` plays the clip's own segment | **A possible reconciliation, unverified** | The system map reads byte 0 as Loading Type (2 = Never load), which would leave a clip only its own segment. But every vanilla clip carries a segment at every Loading Type, and the reading would leave TAOM's Loading Type 2 troll and elephant clips, which have none, with nothing to play |
| 5 | A movement set naming a module's own action is dropped silently; override vanilla's movement actions in a derived action set | New, unmeasured | anim-findings section 7, probed on 1.4.6 with the XML handed to native intact; the API calls it names exist on 1.5.3 |
| 6 | A module's XSLT sees only modules loaded before it | **Confirmed** | `MBObjectManager.CreateMergedXmlFile` applies module i's stylesheet, then merges its XML (1.5.3) |
| 7 | Only a release clip collides; the combat parameter comes from `CombatParameterId`, never the clip's name | New, unmeasured for the release half | anim-findings section 4; `1h_up` 0.38 to 0.50 and `1h_up_flail` 0.60 to 0.88 match the 1.5.3 file |
| 8 | Order module loading with an optional `DependedModule` | **Conflicts, not adopted** | TAOM never adds a `DependedModule` row to its SubModule.xml files (maintainer rule) |

## Recommendation

**Tier 1, done in this pass (docs and one gate):**
- The static-prop recipe, the four measured differences and the delivery gap as
  [tpac-static-prop-authoring.md](../reference/tpac-static-prop-authoring.md).
- Claims 3 to 7 in the animation system map (sections 6 and 7) and in `bannerlord-animation-clip-flags.md`, attributed.
- Lessons in `lessons/animation-skeleton.md`; the RCA of this review in
  [rca-mithrilforge-adoption-2026-09-29.md](rca-mithrilforge-adoption-2026-09-29.md). No trap-index line: the index is
  at its 45-row cap and the movement-set rule is unmeasured by TAOM.
- `tools/tests/test_prefab_asset_packages.py`: the packages and the code's prefab names stay in step.
  `tpac_clone_metamesh.py` now imports `lz4` and `xxhash` where they are used, so the gate runs without them.

**Owed:**
1. **Decide where the camp props live** (claim 2), the maintainer's call: a Kit import into TAOM's `Assets/`, a
   release step that copies them beside `pack0.tpac` (the dev install would still not read them), or a module with
   no loose tree. Then an in-game check read off `taom_debug_*.log` (`[FieldCamp] placed 1 Field camp entities` is
   the prefab). #506 and #507 are closed without `triage-needs-ingame`; #675 carries that label and already walks a
   field camp and a refuge. Filing or labelling is on the maintainer's word.
2. **The fork:** ask yotthani for read access to `TpacTool-bannerlord`, or for the diff of its patches (`4c8dfa8`,
   `5304748`). Once read: MithrilForge as a standalone tool in `E:\Tools\MithrilForge\` (the Ghidra precedent), its
   own suite in Debug and Release against 1.5.3, and a cube pilot in a module with no loose tree. The TAOM name
   check (`validate_mesh_refs.py --check-name`) and the `/new-map-prop` skill exist since 2026-09-29. First real prop: a per-culture field camp or
   a Refuge variant, art chosen by the maintainer. Needs an issue.
3. **The pose probe** from the 09-18 pass, still unbuilt: a dev-console command sampling an agent's bones every tick
   while an action plays, MithrilForge's "hand-height probe". Read bone frames, not weapon entity frames: MithrilForge
   found the latter always identity, because a wielded weapon hangs natively on its bone. Needs an issue.
4. ~~Nothing runs `tools/tests` on `bannerlord-1.5.x`~~ **Done 2026-09-29:** `.github/workflows/python-tests.yml`
   runs them on 1.5.x pushes and pull requests (Python 3.14 plus the test dependencies; the maintainer's call).

**Tier 2, when a task needs it:** `meshmats` and `texdds` as TAOM tools on TAOM's own TpacTool.Lib; the `impact_sim`
idea (weapon contact against the `combat_parameters.xml` window) for creature strikes.

**Skip for now:** the segment codec and mirroring chain (TAOM's creature clips carry no segment, and TAOM has no dual
wield), `blade_fix` (upstream's own dead end), the DualWield recipe, and the rest of anim-findings' dead-ends table:
its GUID-override and TpacTool-master rows are already in `bannerlord-animation-clip-flags.md` from the 09-18 pass,
the BlendInPeriod and editor-with-Harmony rows are untested here, and the others concern mirroring.
