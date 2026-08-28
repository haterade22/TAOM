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
| graphify | `graphify` `graphifyy` `Graphify-Labs` `safishamsi/graphify` | Apache-2.0 (MIT when ported, see detail) | behavioural-port | `tools/doc_graph.py` `tools/graph_query.py` | cleared |
| MinHook | `MinHook` `MinHook.x64.dll` | BSD-2-Clause | redistributed | `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll` `Dependencies/NativeSkinFixes.NativeHooks/MinHook/**` | cleared |
| Lib.Harmony | `0Harmony.dll` `Lib.Harmony` | MIT | redistributed | (build-acquired, `Dependencies/TAOM.Dependencies.csproj` PackageReference) | cleared |
| BUTR stack | `ButterLib` `UIExtenderEx` `MBOptionScreen` `MCMv5` `BUTR.CrashReport` | MIT | redistributed | `Dependencies/_Module/bin/Win64_Shipping_Client/{Bannerlord,MCM,BUTR}*.dll` | cleared |
| .NET Foundation | `Microsoft.Extensions` `Microsoft.Bcl` `System.Buffers` `System.Memory` | MIT | redistributed | `Dependencies/_Module/bin/Win64_Shipping_Client/{Microsoft,System}*.dll` | cleared |
| Serilog | `Serilog` | Apache-2.0 | redistributed | `Dependencies/_Module/bin/Win64_Shipping_Client/Serilog*.dll` | cleared |
| Yotthani modules (FieldCamp, Refuge, SupplyLines) | none published | maintainer-commissioned | behavioural-port | `Main/Features/SupplyLines/**` `Main/Features/FieldCamp/**` `Main/Features/Refuge/**` `Main/_Module/AssetPackages/*.tpac` | cleared |
| LOTRAOM | `LOTRAOM` | maintainer-owned | data-port | `Main/_Module/ModuleData/characters/lords.xml` `Main/_Module/ModuleData/**/taom_wanderer*.xml` `Main/_Module/ModuleData/lords.xslt` `Main/_Module/ModuleData/spcultures.xslt` `Main/Features/WarOfTheRingMomentum/**` `Main/Features/Messengers/**` `Main/Features/HeroRace/**` | cleared |
| ADOD_Beasts | `ADOD_Beasts` `ADOD` `ADODHowdahObject` `ADODBeastsMissionLogic` | purchased-asset, code terms informal | behavioural-port | `Main/Features/Elephant/**` `Main/Features/ElephantLike/**` `Main/Features/Mumakil/**` `Main/Features/WarRam/**` `Main/_Module/Prefabs/taom_howdah_agent.xml` | cleared |
| BehaviorTrees | `BehaviorTrees.dll` | maintainer-owned | verbatim-port | `Main/BehaviorTrees/**` | cleared |
| BannerlordTogether | `BannerlordTogether` `BattleLinkMPClient` | no-decompile policy, see detail | interop-only | (none) | cleared |
| BannerlordCoop | `BannerlordCoop` `Bannerlord-Coop-Team` `Bannerlord.Coop` | UNKNOWN | comparison-only | (none) | uncleared |
| external developer drop | `Features_fixed` | UNKNOWN | verbatim-port | `Main/Features/SiegeDismount/**` `Main/Features/MixedFormations/**` `Main/Features/SmartCavalryAI/**` `Main/Features/FiefManagement/**` `Main/Features/QuickActions/**` `Main/Features/EquipPresets/**` `Main/Features/CompanionTactics/**` | uncleared |
| TAOM_Promoted | `TAOM_Promoted` `RF_Promoted` | UNKNOWN | behavioural-port | `Main/Features/FieldCommission/**` | uncleared |
| TransferbuttonMenu | `TransferbuttonMenu` | UNKNOWN | behavioural-port | `Main/Features/QuickActions/**` | uncleared |
| ServeAsSoldier | `ServeAsSoldier` `Serve as Soldier` | UNKNOWN | comparison-only | (none) | uncleared |
| BetaDeps | `BetaDeps` | UNKNOWN | behavioural-port | `Dependencies/Foundation/{DiagLog,RuntimeLog,ReflectionUtils,VersionProbe,IncompatibleModDetector,PatchShield,SaveShield,FailureRecord,FailedModsCatalog,SubModuleConstructionGuard,CollectAssemblyTypesShim}.cs` `Dependencies/AliasStubSubModule.cs` `Dependencies/SubModule.cs` | uncleared |
| NativeSkinFixes | `NativeSkinFixes` | UNKNOWN | verbatim-port | `Dependencies/NativeSkinFixes.NativeHooks/**` `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` | uncleared |
| upstream chariot pack | `upstream chariot pack` `upstream-chariot-pack` | UNKNOWN | behavioural-port | `docs/features/chariot.md` `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` | uncleared |
| ROT-Core | `ROT-Core` `ROT.dll` `ROTTownTradersBehavior` | UNKNOWN | behavioural-port | `Main/Features/EliteEmissary/**` | uncleared |
| TOR_Core | `TOR_Core` | UNKNOWN | comparison-only | (none) | uncleared |
| module audio | `ModuleSounds` `taom_music_module_sounds.xml` | UNKNOWN | redistributed | `Main/_Module/ModuleSounds/**` | uncleared |
| Aniron (Pete Klassen) | `aniron` | UNKNOWN | redistributed | `Main/_Module/GUI/Fonts/aniron.{fnt,bfnt}` | uncleared |
| Minion Pro (Adobe) | `minionpro` `Minion Pro` | Adobe commercial, redistribution NOT granted by a desktop licence | redistributed | `Main/_Module/GUI/Fonts/minionpro.{fnt,bfnt}` | uncleared |
| Ringbearer | `ringbearer` | UNKNOWN | redistributed | `Main/_Module/GUI/Fonts/ringbearer.{fnt,bfnt}` | uncleared |
| Khuzdul vocabulary (J.R.R. Tolkien) | `Khuzdul` `Khazad` `Baruk` `khuzdul-lexicon` | UNKNOWN | verbatim-port | `docs/audio/khuzdul-lexicon.html` `docs/audio/vo-script-dwarves.html` | uncleared |

<!-- provenance-register-end -->

TAOM's own and predecessor modules. The checker skips these entirely.

<!-- taom-owned-start -->
`TAOM` `TAOM_Map` `TAOM_Online` `TAOM.Dependencies` `TAOM.NativeSkinFixes`
`LOTRLOME` `LOTRLOME_Armory` `LOTRAOM`
<!-- taom-owned-end -->

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

**It remains installed on the maintainer's machine and wired into nothing:** no hook, no CI job, no
MCP registration, and no `graphify * install` subcommand was ever run (those write into CLAUDE.md,
AGENTS.md and a PreToolUse hook in `.claude/settings.json`; note `config-protection.sh` guards only
`Directory.Build.props` and the two settings files, and cannot intercept a CLI subprocess anyway, so
not running them is the actual containment). It is a personal ad-hoc C# analysis aid,
not a TAOM tool, which is why it appears in no `tools/` table. Remove with
`uv tool uninstall graphifyy`.

Nothing in this repo derives from it beyond the June concept port recorded above, so the derivation
stays `behavioural-port` and the trial itself adds nothing. Usage guidance, the three verbs and one generated report worth
running, the cases where it must not be used, and why it is deliberately absent from CLAUDE.md:
[`docs/reviews/adopt-graphify-v8-2026-08-18.md`](../reviews/adopt-graphify-v8-2026-08-18.md).

### MinHook

Upstream: https://github.com/TsudaKageyu/minhook · Pin: v1.3.4 (DLL `FileVersion 1.3.4.0`) ·
BSD-2-Clause, Copyright (C) 2009-2017 Tsuda Kageyu.

Ships as a binary at `Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll`, un-ignored explicitly
by `.gitignore:64`, and the headers are vendored at
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

### ADOD_Beasts, BehaviorTrees

Cleared, and for ADOD_Beasts the basis is recorded in the feature doc rather than assumed:
`docs/features/elephant.md:488` and `:695` state the elephant asset was **purchased from Artem, the
ADOD_Beasts author, for use in TAOM**. Note what that covers and what it does not: it is an asset
purchase, so the meshes and animations are on firm ground, while the behavioural port of the C#
(trample, mount-lock, howdah) rests on the same relationship rather than on a separate written grant.
Worth getting one line in writing from Artem covering the code as well as the art.

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
NativeSkinFixes row below, at lower stakes only because audio is easier to swap than a native hook
library.

### Fonts: Aniron, Minion Pro, Ringbearer (UNCLEARED)

`Main/_Module/GUI/Fonts/` ships three faces in Bannerlord's compiled `.fnt`/`.bfnt` form.

`minionpro` is the one to look at first. Minion Pro is an Adobe commercial typeface, and an Adobe
desktop font licence does not generally permit redistributing the font files themselves. That makes
it the only row in this register naming a specifically commercial rights holder, as opposed to the
unknown-terms rows around it.

`aniron` and `ringbearer` are display faces associated with the Lord of the Rings films. Terms
unrecorded.

**What would clear this row:** confirm each face's redistribution terms, or substitute
freely-licensed display faces. Substitution is a small change (three files, plus whatever GUI
references them by name) and removes the exposure outright. Given that TAOM ships free and
non-commercially, that is likely the cheaper path for `minionpro` in particular.

### NativeSkinFixes (UNCLEARED, and the highest-priority row here)

`Dependencies/NativeSkinFixes.NativeHooks/**` is a port of an upstream Nexus mod of the same name,
carried through the v1.3.15 to v1.4.5 migration. Classified `verbatim-port` on the repo's own
evidence: `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md` says the C++ was "copied with
minimal modification from the upstream" and that three of four review findings were "inherited
verbatim from upstream code". No upstream name, version, or terms are recorded anywhere in the repo.

**The built `TAOM.NativeSkinFixes.dll` ships today.** The feature is PARKED and disabled at the wiring
level, so it does nothing at runtime, but a disabled binary is still a redistributed one. Of every row
in this register this is the one where the licence question is actually blocking: a verbatim port with
no identified upstream, shipping in the release. Either identify the upstream and its terms, or drop
the binary from the module until the feature is un-parked.

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

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](../adrs/010-knowledge-base-architecture.md)
- [docs/features/field-camp.md](../features/field-camp.md)
- [docs/features/refuge.md](../features/refuge.md)
- [docs/features/supply-lines.md](../features/supply-lines.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/asset-provenance.md](./asset-provenance.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)
- [docs/reviews/adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md)

<!-- backlinks-end -->
