# API Diff: v1.4.5 → v1.4.5 hotfix (2026-05-30)

Generated: 2026-05-30
Source: `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client` (all TaleWorlds DLLs rebuilt 2026-05-30 06:52)
Baseline: `E:\Decompiled_Bannerlord_pre_hotfix_20260529` (decompiled 2026-05-29 14:38, pre-hotfix)
Methodology: full `ilspycmd -p` decompile of both builds → `diff -r`; classify changed lines as
declaration (signature drift → binding risk) vs body-only (no binding impact). Authoritative
cross-checks: TAOM build against the new DLLs + the live binding-verification gate.

## TL;DR — **zero TAOM impact.**

Bannerlord shipped a **same-version-string hotfix**: `Version.xml` still reads `v1.4.5` but every
TaleWorlds DLL was rebuilt. The decompile diff is **10 managed files, all body-only changes
(`decl_changes=0` across every file)** — no method/property/field signature, no member add/remove.
TAOM compiles clean against the new DLLs, the binding gate is green, and the scene/XML audits are
clean. Nothing in TAOM needed changing for the hotfix.

## The 10 changed types (all body-only)

| Type | DLL | Change class | TAOM touchpoint | Impact |
|------|-----|--------------|-----------------|--------|
| `ExplainedNumber` | CampaignSystem | body (StatExplainer display path: `GetLines`/`GetExplanations` local renumbering) | Used by every GameModel; recruitment-cost fix (`74e31ee`) uses `AddFactor`/`LimitMin`/`ResultNumber` | **None** — `AddFactor`/`LimitMin`/`ResultNumber` unchanged; the changed code is the `includeDescriptions:true` explainer path TAOM doesn't use |
| `Clan` | CampaignSystem | body | Patch23/24 postfix/prefix on `UpdateBannerColor` / `UpdateBannerColorsAccordingToKingdom` | **None** — postfix/prefix only need the signature (unchanged) |
| `MobileParty` | CampaignSystem | body | reflection on private `_currentSettlement` (RemoteFiefSettlementSwapper) | **None** — field still present (no decl change) |
| `Hero` | CampaignSystem | body | `GetPerkValue`, `IsPartyLeader`, `HeroViewModel.FillFrom` patch | **None** — members intact |
| `MBSaveLoad` | Core | body | none direct | **None** |
| `Utilities` (Engine) | Engine | body | none direct | **None** |
| `Utilities` (MountAndBlade) | MountAndBlade | body | none direct | **None** |
| `Mission` | MountAndBlade | body | patches on `Initialize` / `SpawnAgent` / `Tick` | **None** — signatures intact |
| `MissionState` | MountAndBlade | body | none direct | **None** |
| `Agent` | MountAndBlade | body | prefix on `EquipItemsFromSpawnEquipment` | **None** — signature intact |

### Fragile hotspots — explicitly cleared
- **3 transpilers** (`Banner.TryGetBannerDataFromCode`, `CampaignSceneNotificationHelper.CreateNotificationCharacter`, `ActionSetCode.GenerateActionSetNameWithSuffix`) depend on IL *body* patterns — a hotfix could break them without a signature change. **None of their target types is in the 10 changed files** → transpilers safe.
- **`NavigationCacheAdapter`** (15 private `NavigationCache<T>` bindings) and **`MapConversationTableau`** (8 private members) — **neither type changed** → safe.

## Verification (authoritative, against the new DLLs)
- **Decompile:** 59 DLLs, 6500 `.cs`, manifest stamped 2026-05-30. Diff vs pre-hotfix baseline: 10 files, `decl_changes=0` each.
- **Build:** `dotnet build Main/TAOM.csproj` → **succeeded** (no compile-breaking signature drift).
- **Binding gate:** `dotnet test --filter "TestCategory=BindingVerification"` → **35/35 green** (after the pre-existing fix below).
- **Scene/XML audits:** `audit_scene_names.py` → "0 missing on disk"; `audit_battle_scenes.py` → "all map_indices covered, 0 missing Scene ids".
- **API snapshot:** `snapshot_api_surface.ps1` regenerated; `git diff` = 1 line (attribution refinement, below) — **no API surface member changed.**

## Incidental findings (NOT hotfix-caused)

### 1. Pre-existing binding-gate false-positive — FIXED
The binding gate (`TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`) reported 2 failures for
`TaomKingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms`. Root cause: the
gate resolved the *base* method on `DefaultKingdomDecisionPermissionModel` with
`BindingFlags.DeclaredOnly`, but that virtual is declared on the **abstract base**
`KingdomDecisionPermissionModel` (Default inherits it without re-declaring). The override is valid —
TAOM compiles and dispatches correctly — so this was a false-positive. **Verified version-independent**:
`IsStartAllianceDecisionAllowedBetweenKingdoms` is on the abstract base in *both* the pre- and
post-hotfix decompile, and neither base file is in the hotfix diff. The gate's `BindingVerification`
category is excluded from the default `dotnet test` run, so the full suite never surfaced it.
**Fix:** added a `BaseLookup` flag set (no `DeclaredOnly`) for base-method resolution so it walks the
inheritance chain; a genuinely removed base virtual still resolves to null (real drift still caught).
Gate now 35/35 green.

### 2. Snapshot attribution refinement
`docs/reference/taleworlds-api-snapshot/patch-targets.md` changed by one line: the resolved declaring
type for the alliance-permission target moved `DefaultKingdomDecisionPermissionModel` →
`KingdomDecisionPermissionModel` (abstract base) — same inheritance root cause as #1, correcting a
stale committed attribution. Not a hotfix change.

## Caveat — native layer not covered by this diff
`ilspycmd` decompiles managed assemblies only. `TaleWorlds.Native.dll` (native) was also rebuilt;
TAOM's `NativeSkinFixes` C++ hooks byte-pattern-scan it at install time. A managed-clean hotfix can
still shift native byte patterns. This can only be confirmed at runtime (load a mission, watch for the
NativeSkinFixes degraded-state banner). Not blocking — flagged for the next in-game smoke test.
