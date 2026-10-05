# Provenance Register

Every third-party source TAOM derives from, interoperates with, or was compared against, with its
license and what kind of derivation it is. This file is the single authoritative record. It is also
designed to double as the allowlist for a future `tools/check_provenance.py`, so that the only way to
make a checker accept a new third-party name is to write a row here, which forces the license
question to be answered. **That checker does not exist yet**; for now this file is kept current by
hand, in the same commit as the code it describes.

**The rule this file exists to serve:** name the source and state its license. A bare unattributed
mention is a violation, and so is an unnamed euphemism ("the donor mod", "the upstream pack"). This
reverses an earlier standing rule that said TAOM documentation must not name other mods, recorded at
[`docs/changelog-archive/CHANGELOG-2026-H1.md:2258-2270`](../changelog-archive/CHANGELOG-2026-H1.md).
That rule produced a partial de-naming pass and no enforcement, and it left the repo documenting that
something had been taken while making it impossible to check under what terms. Full rule:
[`.claude/rules/provenance.md`](../../.claude/rules/provenance.md).

**This file does not ship.** It carries `uncleared` rows, which are a working list of open questions,
and publishing an open question is not the same as discharging a notice obligation. The shipped
subset lives in [`Main/_Module/THIRD-PARTY-LICENSES.txt`](../../Main/_Module/THIRD-PARTY-LICENSES.txt)
and [`Dependencies/_Module/THIRD-PARTY-LICENSES.txt`](../../Dependencies/_Module/THIRD-PARTY-LICENSES.txt),
and contains **only** `cleared` rows.

## Vocabulary

**Derivation** (closed set, enforced by the checker):

| Value | Means |
|---|---|
| `clean-room` | Source read once to produce a committed behavioural spec, implementation written from the spec without re-reading the source. TAOM's procedure is [`docs/scene-scripts/ATTRIBUTION.md`](../scene-scripts/ATTRIBUTION.md). Do not claim this unless that procedure was actually followed. |
| `behavioural-port` | Behaviour reproduced from reading the source. Structure, naming, and decomposition are TAOM's. |
| `verbatim-port` | Code shape, identifiers, or constants reproduced. |
| `data-port` | Game data (XML, JSON) copied or machine-derived from the upstream. |
| `redistributed` | The upstream's own binary or data ships in a TAOM release. |
| `interop-only` | Nothing derives from it. TAOM only coexists with its module ids, files, or load order. |
| `comparison-only` | Read for analysis. Nothing in TAOM derives from it. |

**License** is an SPDX id where one applies. Four non-SPDX values are also legal, and each means
something specific: `UNKNOWN` (nobody has established the terms), `maintainer-owned` (TAOM's own
prior work), `purchased-asset, code terms informal` (assets bought, code taken on the same
relationship without a separate written grant), and a short phrase naming a stated restriction where
the source publishes one instead of a licence.

**Status:** `cleared` (we have the right, and the notice obligation is met) · `pending-license` (terms
identified, not yet confirmed or recorded) · `uncleared` (we do not know the terms) · `removed` (the
derivation no longer exists in TAOM).

**Tokens** are the strings the checker matches on. They must be backticked. Nothing outside backticks
is ever treated as a token, which is what keeps the bare word "Alliance" from matching vanilla
`AllianceCampaignBehavior` or the French lore string "Dernière Alliance".

<!-- provenance-register-start -->

| Source | Tokens | License | Derivation | Covers | Status |
|---|---|---|---|---|---|
| Alliance | `Byak0/Alliance` `Alliance mod` | GPL-3.0 | clean-room | `Main/SceneScripts/**` | cleared |
| Alliance.Wargs (Byak0) | `Alliance.Wargs` | author-granted, terms informal | redistributed | `<game>/Modules/Alliance.Wargs/**` (and, after absorption, the warg subset inside `LOTRLOME_Armory`) | cleared |
| BetterExceptionWindow | `BetterExceptionWindow` `BEW` | AGPL-3.0 | comparison-only | (none) | cleared |
| TpacTool | `TpacTool` `szszss/TpacTool` | MIT | behavioural-port | `tools/tpac_skeleton_scan.py` `tools/tpac_clipinfo.py` | cleared |
| NVIDIA SkillSpector | `SkillSpector` `NVIDIA/SkillSpector` | Apache-2.0 | behavioural-port | `tools/audit_claude_config.py` | cleared |
| ECC (Everything Claude Code) | `affaan-m/ECC` `everything-claude-code` `AgentShield` | MIT | behavioural-port | `tools/audit_claude_config.py`; `.claude/skills/{context-budget,skill-stocktake,agent-introspection-debugging,build-fix,verify}/**`; `.claude/hooks/{config-protection,mcp-health-check,mcp-health-mark,block-dangerous-git}.sh`; `tools/blender/harness.py` (`stance_height`); `.claude/skills/context-save` and `context-restore` (failed-approaches field, staleness notice) | cleared |
| graphify | `graphify` `graphifyy` `Graphify-Labs` `safishamsi/graphify` | Apache-2.0 (MIT when ported, see detail) | behavioural-port | `tools/doc_graph.py` `tools/graph_query.py`; `tools/graphify_taom.py` runs the CLI (interop-only, see detail) | cleared |
| MinHook | `MinHook` `MinHook.x64.dll` | BSD-2-Clause | redistributed | (removed 2026-10-05) `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll` `Dependencies/NativeSkinFixes.NativeHooks/MinHook/**` | removed |
| Lib.Harmony | `0Harmony.dll` `Lib.Harmony` | MIT | redistributed | (build-acquired, `Dependencies/TAOM.Dependencies.csproj` PackageReference) | cleared |
| BUTR stack | `ButterLib` `UIExtenderEx` `MBOptionScreen` `MCMv5` `BUTR.CrashReport` | MIT | redistributed | `Dependencies/_Module/bin/Win64_Shipping_Client/{Bannerlord,MCM,BUTR}*.dll` | cleared |
| .NET Foundation | `Microsoft.Extensions` `Microsoft.Bcl` `System.Buffers` `System.Memory` | MIT | redistributed | `Dependencies/_Module/bin/Win64_Shipping_Client/{Microsoft,System}*.dll` and TAOM's own copy of `System.Runtime.CompilerServices.Unsafe.dll`, which the build writes into the install's `Modules/TAOM/bin/<platform>/` | cleared |
| DryIoc (Maksim Volkau) | `DryIoc` `DryIoc.dll` | MIT | redistributed | (build-acquired, `Main/TAOM.csproj` PackageReference 4.8.8; the build writes it into the install's `Modules/TAOM/bin/<platform>/`, never into the repo); notice in `Main/_Module/THIRD-PARTY-LICENSES.txt` since 2026-10-05 | cleared |
| Json.NET (James Newton-King) | `Newtonsoft.Json` | MIT | redistributed | (build-acquired, `Main/TAOM.csproj` PackageReference 13.0.3; the build writes it into the install's `Modules/TAOM/bin/<platform>/`, never into the repo); notice in `Main/_Module/THIRD-PARTY-LICENSES.txt` since 2026-10-05 | cleared |
| Serilog | `Serilog` | Apache-2.0 | redistributed | `Dependencies/_Module/bin/Win64_Shipping_Client/Serilog*.dll` | cleared |
| Yotthani modules (FieldCamp, Refuge, SupplyLines) | none published | maintainer-commissioned | behavioural-port | `Main/Features/SupplyLines/**` `Main/Features/FieldCamp/**` `Main/Features/Refuge/**` `Main/_Module/AssetPackages/*.tpac` | cleared |
| LOTRAOM | `LOTRAOM` | maintainer-owned | data-port | `Main/_Module/ModuleData/characters/lords.xml` `Main/_Module/ModuleData/**/taom_wanderer*.xml` `Main/_Module/ModuleData/lords.xslt` `Main/_Module/ModuleData/spcultures.xslt` `Main/Features/WarOfTheRingMomentum/**` `Main/Features/Messengers/**` `Main/Features/HeroRace/**` | cleared |
| ADOD_Beasts | `ADOD_Beasts` `ADOD` `ADODHowdahObject` `ADODBeastsMissionLogic` | purchased-asset, code terms informal | behavioural-port | `Main/Features/Elephant/**` `Main/Features/ElephantLike/**` `Main/Features/Mumakil/**` `Main/Features/WarRam/**` `Main/Features/Elk/**` `Main/Features/Animalia/**` `docs/reference/lotrlome-armory-snapshot/Prefabs/taom_howdah_platform.xml` (live: `LOTRLOME_Armory/Prefabs`) | cleared |
| BehaviorTrees | `BehaviorTrees.dll` | maintainer-owned | verbatim-port | `Main/BehaviorTrees/**` | cleared |
| BannerlordTogether | `BannerlordTogether` `BattleLinkMPClient` | no-decompile policy, see detail | interop-only | (none) | cleared |
| BannerlordCoop | `BannerlordCoop` `Bannerlord-Coop-Team` `Bannerlord.Coop` | UNKNOWN | comparison-only | (none) | uncleared |
| external developer drop | `Features_fixed` | UNKNOWN | verbatim-port | `Main/Features/SiegeDismount/**` `Main/Features/MixedFormations/**` `Main/Features/SmartCavalryAI/**` `Main/Features/FiefManagement/**` `Main/Features/QuickActions/**` `Main/Features/EquipPresets/**` `Main/Features/CompanionTactics/**` | uncleared |
| TAOM_Promoted | `TAOM_Promoted` `RF_Promoted` | UNKNOWN | behavioural-port | `Main/Features/FieldCommission/**` | uncleared |
| TransferbuttonMenu | `TransferbuttonMenu` | UNKNOWN | behavioural-port | `Main/Features/QuickActions/**` | uncleared |
| ServeAsSoldier | `ServeAsSoldier` `Serve as Soldier` | UNKNOWN | comparison-only | (none) | uncleared |
| BetaDeps | `BetaDeps` | UNKNOWN | behavioural-port | `Dependencies/Foundation/{DiagLog,RuntimeLog,ReflectionUtils,VersionProbe,IncompatibleModDetector,PatchShield,SaveShield,FailureRecord,FailedModsCatalog,SubModuleConstructionGuard,CollectAssemblyTypesShim}.cs` `Dependencies/AliasStubSubModule.cs` `Dependencies/SubModule.cs` | uncleared |
| NativeSkinFixes | `NativeSkinFixes` | UNKNOWN | verbatim-port | (removed 2026-10-05) `Dependencies/NativeSkinFixes.NativeHooks/**` `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` | removed |
| upstream chariot pack | `upstream chariot pack` `upstream-chariot-pack` | UNKNOWN | behavioural-port | `docs/features/chariot.md` `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` | uncleared |
| ROT-Core | `ROT-Core` `ROT.dll` `ROTTownTradersBehavior` | UNKNOWN | behavioural-port | `Main/Features/EliteEmissary/**` | uncleared |
| TOR_Core | `TOR_Core` | UNKNOWN | comparison-only | (none) | uncleared |
| module audio | `ModuleSounds` `taom_music_module_sounds.xml` | UNKNOWN | redistributed | `Main/_Module/ModuleSounds/**` | uncleared |
| Aniron (Pete Klassen) | `aniron` | UNKNOWN | redistributed | `Main/_Module/GUI/Fonts/aniron.{fnt,bfnt}` | uncleared |
| Minion Pro (Adobe) | `minionpro` `Minion Pro` | Adobe commercial, redistribution NOT granted by a desktop licence | redistributed | `Main/_Module/GUI/Fonts/minionpro.{fnt,bfnt}` | uncleared |
| Ringbearer | `ringbearer` `FS_Ringbearer` | Pete Klassen freeware: free distribution only with the original archive, private use only, no modification (see detail) | redistributed | `Main/_Module/GUI/Fonts/ringbearer.{fnt,bfnt}` `Main/_Module/GUI/FactionUI/RuntimeFonts/FS_Ringbearer/**` | uncleared |
| Cinzel (The Cinzel Project Authors) | `Cinzel` `FS_CinzelWide` | OFL-1.1, no Reserved Font Name | redistributed | `Main/_Module/GUI/FactionUI/RuntimeFonts/FS_CinzelWide/**` (licence text beside it) | cleared |
| EB Garamond (The EB Garamond Project Authors) | `EB Garamond` `FS_Garamond` | OFL-1.1, no Reserved Font Name | redistributed | `Main/_Module/GUI/FactionUI/RuntimeFonts/FS_Garamond/**` (licence text beside it) | cleared |
| Kysaro's TAOM_FactionUI, code | `TAOM_FactionUI` | UNKNOWN | behavioural-port | `Main/Features/FactionUI/**` `Main/Adapters/{FrontEnd*,IFrontEnd*,MenuMusicAdapter,IMenuMusicAdapter,PresetAppearanceAdapter,IPresetAppearanceAdapter,FactionRosterAdapter,IFactionRosterAdapter,RosterEntry,NinePatch}.cs` (see detail) | uncleared |
| Kysaro's TAOM_FactionUI, layout and tuning data | `Kysaro` | UNKNOWN | data-port | `Main/_Module/GUI/Prefabs/FactionUI/**` `Main/_Module/GUI/Brushes/TAOM{MainMenu,Loading,CharCreation,FactionScreen}.xml` `Main/_Module/ModuleData/FactionUI/**` | uncleared |
| Kysaro's TAOM_FactionUI, art | none published | UNKNOWN | redistributed | `Main/_Module/GUI/FactionUI/{RuntimeSprites,LoadingScreens}/**` `Main/_Module/Videos/FactionUI/**` | uncleared |
| Khuzdul vocabulary (J.R.R. Tolkien) | `Khuzdul` `Khazad` `Baruk` `khuzdul-lexicon` | UNKNOWN | verbatim-port | `docs/audio/khuzdul-lexicon.html` `docs/audio/vo-script-dwarves.html` | uncleared |
| Cave Troll Lightweight (Fab) | `Cave Troll Lightweight` `cave_troll_lightweight` | purchased-asset, code terms informal | data-port | `tools/oneoff/ue_export_cave_troll.py` `tools/blender/retarget_mannequin_to_human.py`; the retargeted `anim_troll_*` clips in `LOTRLOME_Armory` (live, outside the repo) | cleared |
| Animalia - Elk (male), Animalia - Moose (male) (Fab) | `Animalia` `Elk_M` `Moose_M` `animalia_elk` `animalia_moose` | purchased-asset, code terms informal | data-port | `tools/blender/reskin_animalia_to_horse.py` `tools/blender/retarget_animalia_to_horse.py` `tools/blender/animalia_to_horse_map.json` `tools/blender/measure_animalia_clips.py` `tools/blender/animalia_elk_clip_measure.json` `tools/blender/animalia_moose_clip_measure.json` `tools/gen_animalia_anim_clips.ps1` `docs/features/animalia-elk-moose.md`; meshes, clips and textures in `LOTRLOME_Armory/AssetSources/creature/elk/` and their Kit packages in `LOTRLOME_Armory/Assets/creature/elk/` (live, outside the repo) | cleared |
| Yotthani DualWield handoff, MithrilForge | `MithrilForge` `DualWield` `Bannerlord_Animation_Handoff` `TpacTool-bannerlord` | MIT (MithrilForge); the handoff document was shared with the maintainer by its author, no licence stated | comparison-only | (none; restated facts in `docs/reference/tpac-static-prop-authoring.md`, the animation reference docs and `docs/reference/engine/mission-frame-threads-and-native-costs.md`) | cleared |
| Yotthani `bannerlord` repository (DualWield, FaceLearner) | `yotthani/bannerlord` `HoN/DualWield` `FaceLearner` `FaceLearner.HeadExtract` | UNKNOWN (no licence file; shared with the maintainer by its author) | comparison-only | (none; restated facts in `docs/reference/scripted-melee-strikes.md`, `docs/reference/head-mesh-and-groom-authoring.md` and `docs/reference/engine/mission-frame-threads-and-native-costs.md`) | uncleared |
| Ghidra | `Ghidra` `NationalSecurityAgency/ghidra` `pyghidra` | Apache-2.0 | interop-only | `tools/native_decompile.py` runs the installed tool (see detail) | cleared |
| Hindsight | `Hindsight` `vectorize-io/hindsight` | MIT | comparison-only | (none) | cleared |
| Kingdom Borders (Nexus mod 10699) | `Kingdom Borders` `KingdomBorders` | UNKNOWN | behavioural-port | `Main/Adapters/BorderRenderAdapter.cs`; the Heraldic layout in `Main/Features/RealmBorders/Domain/BorderPainter.cs`; the module-id check in `Main/Features/RealmBorders/Hooks/RealmBordersCampaignBehavior.cs` (interop); see detail | uncleared |

<!-- provenance-register-end -->

TAOM's own and predecessor modules. The checker skips these entirely.

<!-- taom-owned-start -->
`TAOM` `TAOM_Map` `TAOM_Online` `TAOM.Dependencies` `TAOM.NativeSkinFixes`
`LOTRLOME` `LOTRLOME_Armory` `LOTRAOM`
<!-- taom-owned-end -->

`TAOM.NativeSkinFixes` stays in that list because history docs still name it; the DLL itself was removed on 2026-10-05.

Vanilla TaleWorlds module ids (`Native`, `SandBox`, `SandBoxCore`, `StoryMode`, `CustomBattle`,
`BirthAndDeath`, `Multiplayer`, `NavalDLC`) are engine facts, not project policy, and live as a
constant in the checker rather than here.

---

## Detail

### Alliance

Upstream: https://github.com/Byak0/Alliance · Pin: `version/0.6.0.0` · GPL-3.0.

The model everything else should follow. Alliance is copyleft, so a port would pull TAOM's MIT code
under GPL. Instead the source was read once to produce a committed spec, the implementation was
written from the spec, and a cross-check pass confirmed no structural collision. Procedure and the
per-file table: [`docs/scene-scripts/ATTRIBUTION.md`](../scene-scripts/ATTRIBUTION.md). Specs:
`docs/scene-scripts/specs/`. Every covered file carries a four-line header naming Alliance, its
license, and its spec.

### Alliance.Wargs

Upstream: Byak0, the author of the Alliance mod (https://github.com/Byak0/Alliance). Granted to the
TAOM maintainer with full permission to use, which is why the module ships its `AssetSources/` FBX
and PNG alongside the cooked packs. Basis recorded 2026-08-28 on the maintainer's statement; the
grant is informal, so `author-granted, terms informal` is the honest value rather than an SPDX id.
The same shape as the ADOD_Beasts row below, and worth one line in writing for the same reason.

**This corrects an earlier misclassification.** Until 2026-08-28 `Alliance.Wargs` sat in this file's
taom-owned token block, alongside `TAOM_Map` and `LOTRAOM`. That was an affirmative claim of
ownership over another author's art, which `.claude/rules/provenance.md` names as the same defect as
an unattributed taking, pointed the other way. `tools/package_release.py:51` had it right, calling
the module "a redistributed companion" and keeping it out of `DEFAULT_MODULES`.

**Distinct from the Alliance row above.** That row covers a GPL-3.0 clean-room port of Alliance's
scene scripts into `Main/SceneScripts/**`, where the copyleft is the whole reason for the clean-room
procedure. This row covers art assets given directly to the maintainer: a different grant, a
different derivation, and no copyleft reaching TAOM's code.

**What it covers.** The warg skeleton, meshes, textures, 129 animation clips and sound bank, plus the
Isengard orc-rider equipment and uruk skin assets that ship in the same module. TAOM's warg
*behaviour* is not covered here and is not derived from Byak0: `Main/Features/Warg/**` and
`Main/Features/AdvancedCombat/**` are original TAOM work ported from LOTRAOM (see `docs/features/warg-combat.md`).

**Notice obligation.** While the module is a separate player download, the redistribution is Byak0's
own. If the warg data is absorbed into `LOTRLOME_Armory` (which `package_release.py` ships by
default), TAOM starts redistributing these assets itself, and that move must land a
`THIRD-PARTY-LICENSES.txt` entry naming Byak0 in the same change.

### BetterExceptionWindow

Upstream: https://www.nexusmods.com/mountandblade2bannerlord/mods/3535 · Pin: v8.0.0 · AGPL-3.0.

Design reference only, and the reasoning is recorded at
[`docs/features/crash-report.md:7`](../features/crash-report.md): BEW is AGPL, so TAOM authored
equivalents from scratch and used BEW only for *what to patch* and *what to display*. Nothing in
`Main/Features/CrashReport/**` derives from BEW's expression.

### TpacTool

Upstream: https://github.com/szszss/TpacTool · MIT.

The `.tpac` binary format was reverse-engineered from decompiling `TpacTool.Lib.dll`. MIT permits
this; the attribution is at [`docs/tools/spider-skeleton-tpac-tools.md:378`](../tools/spider-skeleton-tpac-tools.md).

### NVIDIA SkillSpector

Upstream: https://github.com/NVIDIA/SkillSpector · Apache-2.0.

A calibrated subset of the deterministic `static_patterns_*` and `behavioral_ast` analyzers, with the
Apache-2.0 §4 attribution preserved in the file header (`tools/audit_claude_config.py:14-22`). The
LangGraph runtime and LLM analyzers were not ported. Note the deliberate carve-out: the upstream's
DRL-1.1 / unlicensed Neo23x0-derived `.yar` files were **not** vendored, and `tools/yara_rules/` is
TAOM clean-room original. Adoption record: [`docs/reviews/adopt-skillspector-2026-06-22.md`](../reviews/adopt-skillspector-2026-06-22.md).

### ECC (Everything Claude Code)

Upstream: https://github.com/affaan-m/ECC (formerly `affaan-m/everything-claude-code`) · MIT.

Ideas and procedures re-expressed in TAOM's own words and code across four reviews (early 2026, the
2026-04-26 ecosystem review, 2026-05-29, 2026-09-29); no ECC file was copied or installed. The
config scanner is a calibrated subset of AgentShield's categories. The file itself names ECC in
`audit_claude_config.py`, the `context-budget`, `skill-stocktake`, `agent-introspection-debugging`,
`context-save` and `context-restore` skills, `block-dangerous-git.sh` and `harness.py`; the early-2026
ports (`build-fix`, `verify`, `config-protection.sh`, `mcp-health-check.sh`, `mcp-health-mark.sh`) carry
no attribution line, and their record is the "Everything-Claude-Code Cherry-Pick" entry in
`docs/changelog-archive/CHANGELOG-2026-H1.md`. Harness tooling only: none of it ships to players, so no
`THIRD-PARTY-LICENSES.txt` entry is owed. Adoption records:
[`docs/reviews/rca-ecc-adoption-2026-05-29.md`](../reviews/rca-ecc-adoption-2026-05-29.md),
[`docs/reviews/adopt-ecc-2026-09-29.md`](../reviews/adopt-ecc-2026-09-29.md).

### graphify

Upstream: https://github.com/Graphify-Labs/graphify (formerly `safishamsi/graphify`) · PyPI `graphifyy` · Apache-2.0.

`tools/doc_graph.py` and `tools/graph_query.py` reproduce three graphify behaviours over TAOM's own
doc-link graph: the `explain` and `path` query verbs, and the god-node / bridge metrics. The
implementation is pure stdlib, reuses `lint_docs`'s link parser, and shares no code with the upstream,
but the source was read during the port, so this is `behavioural-port` and not `clean-room`. Adoption
record: [`docs/reviews/adopt-graphify-2026-06-08.md`](../reviews/adopt-graphify-2026-06-08.md);
ADR-010 Phase 5.

**License note.** The June 2026 port was made against the predecessor repo under **MIT**. The project
has since relicensed to **Apache-2.0**, retaining `LICENSE-MIT` and a `NOTICE` that reads "portions of
this software were contributed under the MIT License prior to the relicensing and remain available
under those terms." Both are permissive and neither constrains a behavioural port. Nothing from
graphify ships in a TAOM release, so no `THIRD-PARTY-LICENSES.txt` entry is owed.

**Trial install, 2026-08-18 to 2026-08-21.** graphify from the upstream `v8` branch (`graphifyy` 0.9.46, `v8` is a branch name, not a release) was installed in an
isolated `uv` venv (pinned Python 3.12, because the `leiden` extra pulls in `graspologic`, which requires Python below 3.13) and
measured against TAOM, including a full multimodal pass at 18.2M input tokens. Nothing was adopted,
and no repo code or config changed.

**Part of the workflow since 2026-09-26** ([ADR-012](../adrs/012-graphify-code-graph-in-the-workflow.md),
#677), reversing the trial's "wired into nothing". `tools/graphify_taom.py` runs the installed CLI
as a subprocess and `.claude/hooks/check-graphify-usage.sh` denies its raw write verbs; that wrapper
is `interop-only`: it copies no graphify code, and graphify's `cache.py` was read only to find where
its stat-index cache is written. Still no MCP registration, no CI job, and no `graphify * install`
subcommand (the gate now denies those). Nothing from graphify ships in a TAOM release, so the
license position above is unchanged. Remove with `uv tool uninstall graphifyy`; the wrapper then
exits 3 and every workflow step records the blast radius as UNCHECKED.

The derivation of `tools/doc_graph.py` stays `behavioural-port`. Usage:
[`docs/features/graphify-code-graph.md`](../features/graphify-code-graph.md); the trial's
measurements: [`docs/reviews/adopt-graphify-v8-2026-08-18.md`](../reviews/adopt-graphify-v8-2026-08-18.md).

### MinHook (REMOVED 2026-10-05)

**Removed 2026-10-05:** NativeSkinFixes was deleted, so MinHook is no longer redistributed and its vendored headers are gone from the tree. The record below describes what was taken; the history is in git.

Upstream: https://github.com/TsudaKageyu/minhook · Pin: v1.3.4 (DLL `FileVersion 1.3.4.0`) ·
BSD-2-Clause, Copyright (C) 2009-2017 Tsuda Kageyu.

Shipped as a binary at `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll`, un-ignored explicitly
in `.gitignore` (the rule went with the feature), and the headers are vendored at
`Dependencies/NativeSkinFixes.NativeHooks/MinHook/include/`. BSD-2-Clause clause 2 requires the
copyright notice be reproduced in binary redistributions, which is why
`Main/_Module/THIRD-PARTY-LICENSES.txt` now exists. Until 2026-08-13 it did not, and this condition
was unmet in every shipped release.

### LOTRAOM

Maintainer-owned predecessor project (Bannerlord 1.2.12).

Cleared: this is the maintainer's own prior work. Recorded here rather than omitted because shipped
files are machine-derived from it and a reader deserves to know which. `characters/lords.xml`, the
`taom_wanderer*` XMLs (three under `ModuleData/`, one under `ModuleData/equipmentsets/`), `lords.xslt`,
and `spcultures.xslt` are generated by
`tools/extract_wanderers.py`, `tools/generate_xslt.py`, and `tools/oneoff/lords-migration/*.ps1`
reading LOTRAOM's ModuleData. `Main/Features/WarOfTheRingMomentum/**` and `Main/Features/Messengers/**`
are behavioural ports of its Momentum and messenger systems.

### Yotthani commissioned modules (FieldCamp, Refuge, SupplyLines)

Maintainer-commissioned work-for-hire, delivered 2026-08 as three standalone Bannerlord 1.4.5
modules by the same coder who built `TAOM_RacePortraits`. Ported into `Main/Features/SupplyLines/**`,
`Main/Features/FieldCamp/**` and `Main/Features/Refuge/**` as behavioural ports: the decompiled
sources were read in full while implementing, structure and naming are TAOM's, and a catalogue of
source defects was fixed rather than carried (#505/#506/#507 record them). The four authored
`AssetPackages/*.tpac` meshes (`fieldcamp_camp_a`, `fieldcamp_palisade_ring`, `refuge_camp_a`,
`refuge_palisade_ring`) are redistributed as delivered; commissioned art, terms cleared with the
maintainer. Their Harmony ids (`HoN.FieldCamp`, `HoN.Refuge`, `com.supplylines.patch`), MCM pages
and save-type ids were NOT carried into TAOM's public surface; TAOM uses its own.

`Main/Features/HeroRace/**` is a behavioural port of its `HeroRace` per-race framing and
eye-height code, including the `CharacterAvatarPatch` / `CharacterImagePatch` config format and
its camera-relative axis naming. In 2026-08 a second, independently rebuilt 1.4.x port of the
same LOTRAOM feature (`TAOM_RacePortraits`, also maintainer-commissioned) was compared against
this one; its decompiled source was read while wiring up Patch72, so that patch is a
behavioural port rather than clean-room. The module itself was not adopted. Its `cave_troll`
avatar offsets were imported as data.

### Yotthani DualWield handoff, MithrilForge

Read in full on 2026-09-18 through `/adopt-external`: `Bannerlord_Animation_Handoff_EN.md` (the DualWield mod's
animation work on Bannerlord 1.4.6, shared with the maintainer by its author, the same commissioned collaborator as
the section above) and a snapshot of `MithrilForge` (MIT, (c) yotthani; its `vendor/TpacTool` submodule points at the
private `TpacTool-bannerlord` fork and was empty). Nothing in TAOM derives from either: no code, data or asset was
taken. Engine and file-format facts the handoff reports were checked against TAOM's own evidence where possible and
restated in TAOM's words, each attributed to the review,
[`docs/reviews/adopt-yotthani-animation-handoff-2026-09-18.md`](../reviews/adopt-yotthani-animation-handoff-2026-09-18.md).

Second pass on 2026-09-29: the private repository at commit `91149e11` was read in full (the anim library and its
Python tools, `docs/anim-findings.md`, the `meshmats` and `texdds` verbs); the fork stayed unreadable. Its facts are
restated, attributed, in [`tpac-static-prop-authoring.md`](tpac-static-prop-authoring.md) and the animation reference
docs; `tools/tests/test_prefab_asset_packages.py` is TAOM's own and ports no MithrilForge code. The four camp and
refuge `AssetPackages/*.tpac` in the section above are MithrilForge's output (its item layout, read 2026-09-29); the
tool is irrelevant to their terms, which remain the commissioned-art clearance above. They have reached no player:
the editor-built releases ship only `pack0.tpac`, which does not contain them. Review:
[`docs/reviews/adopt-mithrilforge-2026-09-29.md`](../reviews/adopt-mithrilforge-2026-09-29.md).

Third pass on 2026-10-02, for the engine-performance programme: `docs/engine/*` at commit `7c556554` (native.md,
hooks.md, ai-formations.md, combat.md, taom-combat.md, rider-ik.md, modding-kit.md) and the `TpacFormat` library's
README, read in a local clone. Nothing was copied. Every native fact TAOM relies on was re-derived on TAOM's own copy of
the v1.5.3 client with TAOM's tools; the rest is tagged as yotthani's in
[`engine/mission-frame-threads-and-native-costs.md`](engine/mission-frame-threads-and-native-costs.md). Review:
[`docs/reviews/adopt-mithrilforge-engine-perf-2026-10-02.md`](../reviews/adopt-mithrilforge-engine-perf-2026-10-02.md).

### Yotthani `bannerlord` repository (DualWield, FaceLearner)

yotthani's private monorepo `yotthani/bannerlord`, shared with the maintainer by its author (the same collaborator as
the two sections above), read on 2026-09-30 at commit `8e040ab` through `/adopt-external`. It holds the DualWield
mod's source (`HoN/DualWield`), the FaceLearner mod and its head tools (`bn faces/`), and other work that was
surveyed and parked. The repository carries no licence file, so the terms are `UNKNOWN`. Nothing in TAOM derives from
it: no code, data or asset was taken. Engine and file-format facts from DualWield and FaceLearner were checked against
the v1.5.3 decompile where possible and restated in TAOM's words, each tagged as verified by TAOM or as yotthani's
measurement, in [`scripted-melee-strikes.md`](scripted-melee-strikes.md) and
[`head-mesh-and-groom-authoring.md`](head-mesh-and-groom-authoring.md). Restating facts needs no licence; the row
stays `uncleared` until the terms are known, and a licence line from yotthani would clear it. Review:
[`docs/reviews/adopt-yotthani-bannerlord-2026-09-30.md`](../reviews/adopt-yotthani-bannerlord-2026-09-30.md).

Read again on 2026-10-02 at commit `2e44db7` for its performance work only (`HoN/DualWield/Core/DwPerf.cs`, the
DualWield perf and limb-ray commit messages, `bn faces/FaceLearner/PERFORMANCE_ANALYSIS.md`, two design specs);
comparison only, nothing taken. Review:
[`docs/reviews/adopt-mithrilforge-engine-perf-2026-10-02.md`](../reviews/adopt-mithrilforge-engine-perf-2026-10-02.md).

### ADOD_Beasts, BehaviorTrees

Cleared, and for ADOD_Beasts the basis is recorded in the feature doc rather than assumed:
`docs/features/elephant.md:488` and `:695` state the elephant asset was **purchased from Artem, the
ADOD_Beasts author, for use in TAOM**. Note what that covers and what it does not: it is an asset
purchase, so the meshes and animations are on firm ground, while the behavioural port of the C#
(trample, mount-lock, howdah) rests on the same relationship rather than on a separate written grant.
Worth getting one line in writing from Artem covering the code as well as the art.

Since 2026-09-29 the elephant's visible body (`sk_elephant_basemesh_a`) and its six harnesses
(`SK_Elephant_Armor_Variations`) are meshes KEYforce delivered, replacing ADOD_Beasts's `elephant_mesh` and armour;
the `elephant_skeleton`, its hit capsules and the animation clips still come from the purchased ADOD_Beasts asset.
Where KEYforce's elephant meshes come from (his own work, or re-exported from the same purchase) is not recorded;
ask him and write the answer here.

Architecture dossier:
[`docs/reference/adod-beasts-architecture-and-taom-port.md`](adod-beasts-architecture-and-taom-port.md).
`BehaviorTrees.dll` was decompiled with `ilspycmd` on 2026-05-24 and inlined into `Main/BehaviorTrees/`
so the stack ships as one assembly; the header at `Main/BehaviorTrees/BehaviorTreesCore.cs:7-13`
records that and the two cleanups applied (ILSpy artifacts dropped, C# 12 primary constructors
rewritten for `LangVersion=10`).

### BannerlordTogether, BattleLinkMPClient

Interop only. Nothing derives from them. TAOM names their module ids in `SubModule.xml`
`ModulesToLoadAfterThis` for load-order reasons and detects them at runtime via `CoopPresence`, which
is a compatibility fact rather than a derivation.

**BannerlordTogether ships an explicit no-decompile / no-AI-analysis policy from its copyright
holders, and TAOM honours it.** Its Harmony id is obtained only from Harmony's public runtime
registry, never by reading its code, and `HarmonyCensusModels` is constrained to carry no IL and no
method bodies. That restriction follows from BT's stated terms and does not generalise to other mods.
Feature doc: [`docs/features/bannerlord-together-compat.md`](../features/bannerlord-together-compat.md).

### BannerlordCoop (UNCLEARED)

A different mod from BannerlordTogether; its launcher id is the bare string `Coop`. TAOM's
relationship with it is interop at runtime, but the research behind that interop is not:
[`docs/research/bannerlordcoop-internals.md`](../research/bannerlordcoop-internals.md) records a full
decompile ("`ilspycmd` 10.0.1 against the installed client assemblies, 6 DLLs into 3,270 `.cs`
files") and four verified Harmony owner ids. By this register's own vocabulary that is
`comparison-only`, the same classification ROT-Core gets, and it is why the row is `uncleared` rather
than `n/a`. The reasoning recorded at the time was that BannerlordCoop is a public upstream project
shipping generated sources in plaintext and carries no policy forbidding it, unlike BT. That
reasoning is worth confirming against the project's actual licence rather than left as an inference.

### external developer drop, `Downloads/Features_fixed/` (UNCLEARED)

Seven features were ported from a drop of decompiled C# supplied by an external developer:
SiegeDismount, MixedFormations, SmartCavalryAI, FiefManagement, QuickActions, EquipPresets,
CompanionTactics. Planning record: [`docs/archive/feature-port-prompts/README.md`](../archive/feature-port-prompts/README.md).

No license, grant, or terms are recorded anywhere in the repo. Several sites declare the derivation as
verbatim rather than behavioural:

- `Main/Features/CompanionTactics/Roles/Models/CombatRole.cs:5` says "Ported verbatim from the original developer's drop"
- `Main/Features/EquipPresets/Models/HoNEquipmentPreset.cs:7` says "Mirrors the decompiled-source shape verbatim"
- `Main/Features/SmartCavalryAI/CavalryChargeService.cs:14` says "Differences from the v1.4 decompile baseline (intentional, port-driven)"

Two artefacts carry the donor's identity into TAOM's own public surface: the `HoN*` type-name prefix
(`HoNFormationPreset`, `HoNEquipmentPreset`, `HoNPresetItemReference`) and the deliberate reuse of the
donor's TaleWorlds SaveSystem `BaseId 726900601` so its saves import.

**To resolve:** check the drop folder itself for a LICENSE or README, then obtain written terms from
the developer who supplied it.

### TAOM_Promoted / RF_Promoted (UNCLEARED)

`Main/Features/FieldCommission/**` is described at [`docs/features/field-commission.md:20`](../features/field-commission.md)
as a "TAOM native rewrite of the `TAOM_Promoted` ('RF_Promoted') donor mod". `Domain/TroopUpgradeGraph.cs:8`
records "Ported from the donor mod's `FindUpgradedDescendantInParty`". No terms recorded. Distinct from
the `Features_fixed` drop.

### TransferbuttonMenu (UNCLEARED)

`Main/Features/QuickActions/**` is declared at [`docs/features/quick-actions.md:5`](../features/quick-actions.md)
as "Ported from the external 1.2.x `TransferbuttonMenu` module". No terms recorded.

### ServeAsSoldier (UNCLEARED)

Declared comparison-only, and much of it genuinely is. But some comments cite its source by file and
line range (`Main/Features/Enlistment/TownLeavePolicy.cs:17-18` cites `Test.cs:2424-2440`), which means
its implementation was read, and `docs/reviews/sas-comparative-analysis-2026-08-08.md` is a line-level
teardown. The installed module carries no LICENSE file, so its Nexus "Permissions and credits" block
is the only source of terms.

### BetaDeps (UNCLEARED), and the derivation type was previously misstated

Eleven classes under `Dependencies/Foundation/`, plus `AliasStubSubModule.cs` and `SubModule.cs`
(whose assembly-version list "mirrors BetaDeps.Foundation.AssemblyVersionShim"). The glob is spelled
out per file rather than as `Foundation/**`, because five files in that folder (`CoopModuleList`,
`CoopPresence`, `CoopPresencePolicy`, `PatchShieldPolicy`, `SaveShieldPolicy`) are TAOM originals with
no BetaDeps derivation, and sweeping them in would assert the opposite.

Until 2026-08-13 the shipped notice claimed a "clean-room rewrite" while ten source headers said
"Ports BetaDeps.Foundation.X". Those cannot both be true, and the headers are the accurate ones: `VersionProbe.cs:13-19` knows that a
specific upstream field "was BetaDeps invention" and `SaveShield.cs:59` knows its exact patch-target
list, neither of which is derivable from a behavioural spec. There are no BetaDeps specs under `docs/`.
This is a behavioural port. `clean-room` is a term of art with a procedure attached, TAOM has that
procedure, and it was not followed here.

The `ModulesToLoadAfterThis` list at `Dependencies/_Module/SubModule.xml:20` is adapted from BetaDeps
v0.7.5.1's. A list of module ids is a compatibility fact about other people's mods, not expression.

### Module audio (UNCLEARED)

`Main/_Module/ModuleSounds/` ships 436 tracked files (342 WAV, 93 MP3) plus the culture music set
introduced in `cf2b9c44` ("17 cultures, 476 tracks"). No upstream, author, or terms are recorded
anywhere in the repo, and `Main/_Module/THIRD-PARTY-LICENSES.txt` historically disclaimed audio
provenance in the same breath as art and game data.

The maintainer's position (2026-08-25) is that the audio is third-party in origin and is not
claimed. That is now stated explicitly rather than left to a blanket disclaimer: the audio is
excluded from TAOM's CC BY-NC-SA grant in `LICENSE-CONTENT.md`, and `THIRD-PARTY-LICENSES.txt`
carries its own section saying TAOM does not hold rights in it.

The playback engine is unaffected and is TAOM original work under MIT: `MusicPlaybackService`,
`MusicTrackIndex`, `MusicTransitionResolver`, `Patch46_Music` and the surrounding code.

**What would clear this row:** identify the upstream and its terms, or replace the audio. Until
then it is redistributed with unknown terms, which is the same class of exposure as the
NativeSkinFixes row below (while that DLL shipped), at lower stakes only because audio is easier to swap than a native hook
library.

### Fonts: Aniron, Minion Pro, Ringbearer (UNCLEARED)

`Main/_Module/GUI/Fonts/` ships three faces in Bannerlord's compiled `.fnt`/`.bfnt` form.

`minionpro` is the one to look at first. Minion Pro is an Adobe commercial typeface, and an Adobe
desktop font licence does not generally permit redistributing the font files themselves. That makes
it the only row in this register naming a specifically commercial rights holder, as opposed to the
unknown-terms rows around it.

`aniron` and `ringbearer` are display faces associated with the Lord of the Rings films. Terms
unrecorded for `aniron`.

Ringbearer's terms are now on file: the faction UI merged from Kysaro's module (#704) ships a second
copy of the face, converted to a runtime bitmap font (`FS_Ringbearer`), with the author's readme beside
it. The readme allows distribution "free of charge only, and only with the complete contents of the
original archive", for "private use only", and forbids "commercial use and/or modification". A bitmap
conversion shipped without the original archive meets none of the three conditions, so both copies
stay uncleared. The faction UI's other two faces, Cinzel and EB Garamond, are OFL-1.1 and cleared.

**What would clear this row:** confirm each face's redistribution terms, or substitute
freely-licensed display faces. Substitution is a small change (three files, plus whatever GUI
references them by name) and removes the exposure outright. Given that TAOM ships free and
non-commercially, that is likely the cheaper path for `minionpro` in particular.

### Kysaro's TAOM_FactionUI (UNCLEARED)

Kysaro, TAOM's lead scener and lead UI designer, built the module TAOM_FactionUI v0.1.0 for TAOM on
Bannerlord v1.5.3 and sent it to the maintainer on 2026-10-01 (the folder arrived as `HateradeUI`): a
compiled DLL and PDB with its prefabs, brushes, tuning files, images, fonts and videos, and no source.
It was merged into TAOM's Main module the same day (#704, the maintainer's decision).

- **Code (`behavioural-port`).** The DLL was decompiled with ilspycmd, and its behaviour was rebuilt in
  TAOM's architecture (patch, service, adapter) while reading the decompile, which rules out
  `clean-room`. Several behaviours were changed on purpose: images load on demand, a hero pick's name
  and skills are copied only at the end of character creation, Player Switcher's panel is hidden per
  character creation instead of the feature disabled for the session, a living hero picked on the
  faction screen is taken over through Player Switcher instead of copied (Mike, 2026-10-01), and
  English string matches became localized ones.
- **Layout and tuning data (`data-port`).** His prefabs (23 ship; five unused ones were removed), the
  four brush files and the six JSON files under `ModuleData/FactionUI/`, edited in place for
  localization, binding fixes and validation.
- **Art (`redistributed`).** The 261 runtime sprites, the ten loading screens and the menu and splash
  videos ship as delivered.
- **Fonts** have their own rows (Cinzel, EB Garamond, Ringbearer).

Nothing on file records terms. Kysaro made the module for TAOM, which makes a grant likely, but a
likely grant is not a recorded one, so the three rows are `UNKNOWN`. **What would clear them:** a
written OK from Kysaro covering the code port and the art. Separately, the review that preceded the
port noted that several faction and hero portraits resemble the film cast; whether that matters for a
free, non-commercial release is the maintainer's call. Not covered because not ported: his replacement
`character_menu_new` scene (its assets are missing), the face generator backdrop and the loading-screen
quotes (both off in his own shipped configuration).

### NativeSkinFixes (REMOVED 2026-10-05)

**Removed 2026-10-05:** the feature, its C++ source and its built DLL were deleted from the tree at the maintainer's request, so on the `bannerlord-1.5.x` line nothing here is redistributed any more and the licence question no longer blocks a release. The `bannerlord-1.4.5` line (the 1.4.8 builds) still carries the feature, parked; the maintainer chose to leave it there (2026-10-05), so the question still applies to that line. The history is in git. The text below records what was taken and why it was flagged.

`Dependencies/NativeSkinFixes.NativeHooks/**` is a port of an upstream Nexus mod of the same name,
carried through the v1.3.15 to v1.4.5 migration. Classified `verbatim-port` on the repo's own
evidence: `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md` says the C++ was "copied with
minimal modification from the upstream" and that three of four review findings were "inherited
verbatim from upstream code". No upstream name, version, or terms are recorded anywhere in the repo.

**The built `TAOM.NativeSkinFixes.dll` shipped until the removal.** The feature was PARKED and disabled at the wiring
level, so it did nothing at runtime, but a disabled binary is still a redistributed one. Of every row
in this register it was the one where the licence question was actually blocking: a verbatim port with
no identified upstream, shipping in the release. The removal resolved it by dropping the binary.

### upstream chariot pack (UNCLEARED, and still unnamed)

The Rhûn war chariot came from a **different** mod than the elephant. The 2026 de-naming pass is
explicit that it de-named "the two upstream creature mods the elephant + chariot were ported from"
(`docs/changelog-archive/CHANGELOG-2026-H1.md:2261-2263`), and the elephant's is ADOD_Beasts, so the
chariot's is a second source. Its name appears nowhere in the repo, which is the euphemism problem in
its purest form: the port is documented, the source is not identifiable, and no one can check the
terms. `docs/features/chariot.md:15` records that "rights to the art confirmed by the maintainer",
which covers the assets but says nothing about the code or who granted it.

This row is deliberately left with the euphemism as its token rather than a guessed name.
**To resolve:** the maintainer names the mod, and the pre-de-naming text is recoverable from history
(`git log -S chariot -- docs/` around 2026-06-12). Then the euphemisms in `docs/features/chariot.md`
(16 sites) and `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs:20` become the real
name, the same way the elephant's did.

### ROT-Core, TOR_Core (UNCLEARED)

`docs/migration/ROT-CORE-ANALYSIS.md` is a full decompile dossier of ROT-Core, produced from
`ROT.dll`. One shipped feature derives from it: [`docs/features/elite-emissary.md:11`](../features/elite-emissary.md)
records that it is "Inspired by ROT's `ROTTownTradersBehavior`". That derivation is why the row is
`behavioural-port` rather than `comparison-only`; a row cannot claim nothing derives from it while
naming thirteen files that do. TOR_Core is a separate source, referenced for engine behaviour only
(`docs/features/dev-console.md:106`), and derives nothing.

---

## Adding a row

1. Name the source as it is published. No euphemisms.
2. Put every string the checker should match in the `Tokens` column, in backticks.
3. Establish the license before writing anything into a shipped file. `UNKNOWN` is a valid value here
   and is never a valid value in `Main/_Module/THIRD-PARTY-LICENSES.txt`.
4. Pick the narrowest true `Derivation`. If you read the source while implementing, it is not
   `clean-room`, regardless of how much you changed.
5. `Covers` lists the concrete TAOM paths, not a feature name. Every file matched by a glob must name
   its source in a header or link this register, which the checker enforces.
6. Add a detail section if the row needs more than the table can hold.
7. For a brand new adoption, run `/adopt-external` first. Its security and license pass is the front
   door; this register is where its answer gets written down.

### Cave Troll Lightweight (Fab)

Bought on Fab (Epic) on 2026-09-17 as a source of troll animation clips, skeletal mesh and textures
for the `cave_troll` race. `tools/oneoff/ue_export_cave_troll.py` exports it out of a UE 5.4 project
into a staging folder outside the repo; the retargeted clips now live in `LOTRLOME_Armory`, which is
`data-port`. **Cleared on 2026-09-23 by the maintainer's decision:** the product was bought, and the
creator's name and the Fab licence tier are not recorded (Mike: "I do not care about the fab
creator/license. We bought the product."). That decision covers every Fab purchase, so the row no longer
waits on the product page. The same acquisition path was used in July 2026 for the Fab Medieval Tent
Collection and the ElvenForestCity kit (both in `TAOM_Map/AssetSources/Scenes/`), which have no row yet;
adding them is open work, not something this row covers.

### Animalia - Elk (male) and Animalia - Moose (male) (Fab)

Bought on Fab on 2026-09-23 (#646): two quadruped packs with their own rigs, clips and textures. They
come out of UE 5.4 through the same exporter, then `tools/blender/reskin_animalia_to_horse.py` bends the
meshes onto `horse_skeleton` keeping the pack's weights and `tools/blender/retarget_animalia_to_horse.py`
moves the clips onto the same skeleton; the textures are converted to 1K. Everything derived is art and
animation data (`data-port`); no code is taken, so the "code terms informal" half of the licence value is
empty here. The derived files sit in `LOTRLOME_Armory/AssetSources/creature/elk/` and reach players
through the Armory, not through a TAOM `bin` folder, so nothing goes into a `THIRD-PARTY-LICENSES.txt`
(the same as ADOD_Beasts). Cleared under the maintainer's Fab decision recorded in the Cave Troll section.
Record: [`docs/features/animalia-elk-moose.md`](../features/animalia-elk-moose.md).

### Khuzdul vocabulary (J.R.R. Tolkien)

Source: J.R.R. Tolkien's published writings, chiefly *The Lord of the Rings* Appendix F and the
Hornburg chapter, *The Silmarillion*, and the linguistic papers in the *History of Middle-earth*
series. License `UNKNOWN`, status `uncleared`.

**Scope of this row is deliberately narrow.** It covers the roughly thirty-eight attested Khuzdul
words and two phrases reproduced verbatim in
[`docs/audio/khuzdul-lexicon.html`](../audio/khuzdul-lexicon.html) Part 1, and their use as spoken lines
in [`docs/audio/vo-script-dwarves.html`](../audio/vo-script-dwarves.html). It is **not** a statement
about TAOM's overall relationship to Tolkien's work, which is the project's founding premise and a
much larger question this register has never addressed. Someone should open that question; this row
does not answer it.

`verbatim-port` is the narrowest true value: the words are reproduced exactly, because a language is
not paraphrasable. TAOM's own coinages, listed in Part 2 of the lexicon and marked as coinages, are
maintainer-owned and are not covered by this row.

**Explicitly excluded: the neo-Khuzdul written by David Salo for the Peter Jackson films.** It is his
creative work, TAOM derives nothing from it, and the lexicon carries a standing rule forbidding its
import. There is no row for it because there is nothing to declare, and that is the intended state.

### Ghidra

Upstream: https://github.com/NationalSecurityAgency/ghidra · Apache-2.0. Installed 2026-09-26 as
12.1.4 at `E:\Tools\ghidra_12.1.4_PUBLIC`, with the PyGhidra 3.1.0 it bundles in a venv at
`E:\Tools\ghidra-venv` (#688). `tools/native_decompile.py` drives the installed tool through
PyGhidra's public API and copies no Ghidra code; PyGhidra's `api.py` and `core.py` were read only to
choose the calls. Nothing from Ghidra ships in a TAOM release, so no `THIRD-PARTY-LICENSES.txt` entry
is owed. Remove with the two `E:\Tools` folders; the tool then exits 2 naming the setup doc. Adoption
record: [`docs/reviews/adopt-ghidra-hindsight-2026-09-26.md`](../reviews/adopt-ghidra-hindsight-2026-09-26.md).

### Hindsight

Upstream: https://github.com/vectorize-io/hindsight · MIT. Evaluated on 2026-09-26 and rejected: it
conflicts with ADR-011's rule that durable knowledge lives in the repository, and its Claude Code
plugin injects recalled text into every prompt and spends an auto-detected LLM key. Its hook scripts
were read for the security pass; nothing was installed or ported. Record:
[`docs/reviews/adopt-ghidra-hindsight-2026-09-26.md`](../reviews/adopt-ghidra-hindsight-2026-09-26.md).

### Kingdom Borders (UNCLEARED)

Kingdom Borders v1.2.2 (Nexus mod 10699, for Bannerlord 1.4.7). The Nexus page returned HTTP 403, so
its terms are unknown and it is treated as all rights reserved. Its DLL was decompiled once for the
adoption review, [`docs/reviews/adopt-kingdom-borders-2026-09-30.md`](../reviews/adopt-kingdom-borders-2026-09-30.md),
and TAOM's Realm Borders (#698) was then written without copying code. Three things come from it:

- **The drawing recipe**, in `BorderRenderAdapter`: a copy of an engine vertex-colour material (the
  mod's is `vertex_color_mat`) with `NoModifyDepthBuffer` (plus `NoDepthTest` to draw through hills),
  each triangle in both windings, and the fade set with `GameEntity.SetAlpha`. Engine names and flags,
  learned from its source. TAOM since puts every border material in the late pass (bit 0x20000000,
  which the mod set in one of its modes) and adds the second winding only for a material that culls
  back faces.
- **The Heraldic layout**, kept on purpose by the approved design: two bands in each realm's colour
  either side of a gap on the line (`BorderPainter`). Its widths are TAOM's own, derived from the Atlas
  look in `BorderLook` (the gap is the ink line's width, 0.56, and each band half the wash, 1.3). The
  first build used the mod's MCM defaults (0.3 and 1.05); on Mike's word (2026-09-30) they were replaced,
  so no constant of the mod's is reproduced.
- **Interop:** the campaign behavior stands aside when a module with the id `KingdomBorders` (read from
  the mod's `SubModule.xml`) is active, so the map never carries two sets of lines.

Everything else differs by design: terrain-aware provinces grown from every fief instead of a Voronoi
split of settlement points, borders chosen from province ownership, tiled meshes instead of one entity
per strip, and the Atlas look, palette, map modes, names and notices are TAOM's. Nothing of it ships.
Clearing the row needs its author's terms.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](../adrs/010-knowledge-base-architecture.md)
- [docs/features/field-camp.md](../features/field-camp.md)
- [docs/features/refuge.md](../features/refuge.md)
- [docs/features/supply-lines.md](../features/supply-lines.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/reference/asset-provenance.md](./asset-provenance.md)
- [docs/reference/lotrlome-warg-changes.md](./lotrlome-warg-changes.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)
- [docs/reviews/adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md)

<!-- backlinks-end -->
