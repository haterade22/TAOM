# Codex Adversarial Review Prompt — v1.4.5 Migration (2026-05-22)

You are reviewing the TAOM (Tales From the Age of Men) v1.3.15 → v1.4.5 Bannerlord migration changeset on branch `bannerlord-1.4.5`. Claude has already run a 5-agent `/deep-review` and caught 1 CRITICAL (silent reflection no-op) + 2 doc inconsistencies. Your job is to find what those 5 agents missed.

The migration scope deliberately did NOT redesign anything — it just brought TAOM up to v1.4.5 API compat. **Anything you flag should be a real bug or a real risk, not a style/polish concern.** Be adversarial.

## TAOM ID CHEATSHEET (for any config-reference checks)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- `docs/migration/v1.4.x-overview.md` — migration session map
- `docs/migration/v1.4.x-changes.md` — full TaleWorlds v1.4.0–v1.4.5 changelog analysis
- `docs/migration/v1.4.x-equipment-overhaul.md` — v1.4.3 equipment system deep dive (3,372 XML occurrences to migrate later in S5a)
- `docs/migration/api-diff-1.3.15-to-1.4.5.md` — high-risk GameModel signature diff
- `docs/migration/TRACKING.md` — per-session status with all S0 findings
- `docs/reviews/rca-v1.4.5-migration-2026-05-22.md` — RCA of the IsFemale silent-no-op deep-review caught + 2 doc nits. **Do NOT re-flag findings already in this RCA unless you have additional severity or scope to add.**

## Changeset under review

### 4 C# files modified
- `Main/Adapters/ChildCreatorAdapter.cs` — rewrote `AssignEquipment` to use new `GetEquipmentForInitialChildrenGeneration` API (single Equipment vs old MBList<MBEquipmentRoster>). Also replaced reflection-on-BasicCharacterObject-IsFemale with direct `hero.IsFemale = isFemale` (deep-review fix). Dead imports removed.
- `Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs` — `CalculateRenownGain` signature: 3-param → 5-param (added `renownMultiplierForWinnerSide`, `includeDescriptions`).
- `Main/Features/Diplomacy/Models/TaomAllianceModel.cs` — `GetScoreOfStartingAlliance` signature: dropped `IFaction evaluatingFaction` param (was added in v1.4.0 fix, removed in v1.4.5).
- `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` — `OnHideoutCompleted` signature: added 3rd param `HideoutEventComponent.HideoutBattleEndState` (v1.4.3 event signature change).

### Supporting infra (out of code-review scope but mention if you find issues)
- `Directory.Build.props` — added `BANNERLORD_OVERRIDE_DIR` for dual-DLL workflow
- `tools/taom-src.ps1` — auto-detect version from `Version.xml`
- `Main/_Module/SubModule.xml` — Native dep bumped `e1.3.0.*` → `e1.4.5.*`
- `tools/migrate_equipment_type_1_4_3.py`, `audit_equipment_roster_coverage.py`, `validate_equipment_flags_1_4_3.py`, `decompile_to_folder.ps1` — new migration tooling

### TAOM.Dependencies restoration (1,444 files)
- Restored from git SHA `0b16cca` (April 2026) — the entire Harmony 2.4.2 internalized fork. Built clean against 1.4.5 (0 errors, 878 benign warnings). **Do NOT review individual Harmony source files — that's third-party.** But DO flag if you suspect any TaleWorlds API the Harmony fork uses (`AccessTools.Method`, type resolution, etc.) might silently fail at runtime under v1.4.5.

## Known Suspects — CONFIRM or DISPUTE

### Suspect 1: ChildCreatorAdapter `useSourceEquipmentType: false` semantics

The rewrite mirrors vanilla 1.4.5 `InitialChildGenerationCampaignBehavior`:
```csharp
EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, civilianEquipment);
var battleEquipment = new Equipment(Equipment.EquipmentType.Battle);
battleEquipment.FillFrom(civilianEquipment, useSourceEquipmentType: false);
EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, battleEquipment);
```

Hypothesis: `useSourceEquipmentType: false` copies all slot contents but keeps `battleEquipment._equipmentType = Battle`. So the result of `AssignHeroEquipmentFromEquipment(hero, battleEquipment)` writes to `hero.BattleEquipment` (because `battleEquipment.IsBattle == true`).

**Verify:** Decompile `Equipment.FillFrom(Equipment, bool)` AND `EquipmentHelper.AssignHeroEquipmentFromEquipment` from `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll` (or the matching path under `E:\Decompiled_Bannerlord\`). Confirm:
1. The `Battle` type is preserved after `FillFrom`.
2. `AssignHeroEquipmentFromEquipment` routes by `equipment.IsBattle` / `IsCivilian` / `IsStealth` and writes to the correct hero slot.

Also confirm: in v1.3.15 the old code picked one Equipment from a list of MBEquipmentRoster via `GetRandomElementInefficiently().GetRandomCivilianEquipment()`. Engine-level filtering by gender + culture was NOT done before — TAOM picked any roster. In v1.4.5 the engine filters internally. Is there any scenario where v1.3.15 produced a child with non-gender-appropriate equipment (because the random roster wasn't gender-filtered) but v1.4.5 won't? Acceptable behavioral change OR latent bug?

### Suspect 2: TaomBattleRewardModel multiplier scaling

The fix added `renownMultiplierForWinnerSide` as the 4th param, passed to `base.CalculateRenownGain`. Vanilla `DefaultBattleRewardModel.CalculateRenownGain` constructs `new ExplainedNumber(contributionShare * renownValue * renownMultiplier)` — the multiplier is baked into the base value. TAOM's `ApplyRenownFeats` and `ApplyFactor` then call `.AddFactor(...)` on that base.

Hypothesis: TAOM feats and career passives now scale proportionally with `renownMultiplierForWinnerSide`. This is consistent with vanilla perk scaling.

**Verify:** Decompile `DefaultBattleRewardModel.CalculateRenownGain` in v1.4.5. Confirm the multiplier is in the initial ExplainedNumber value, not applied at the end via AddFactor. If it's at the end, then TAOM feats would NOT scale with it — that's a behavioral change.

Also: was there an `*ImplicitMultiplier` in v1.3.15's `CalculateRenownGain(party, value, share)` that TAOM was relying on? Decompile v1.3.15's signature from `E:\BannerlordBackup\1.3.15\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`. Compare.

### Suspect 3: TaomAllianceModel — does CanMakeAlliance bypass MaxNumberOfAlliances?

TAOM's `MaxNumberOfAlliances => int.MaxValue` was the old escape hatch to allow unlimited LOTR-faction alliances. In v1.4.5, `DefaultAllianceModel` added 5 new methods including `CanMakeAlliance` (per the Claude API-diff agent), which has its own score-threshold + player-support gates that may independently veto an alliance even when TAOM's MaxNumberOfAlliances is unbounded.

Hypothesis: TAOM's `GetScoreOfStartingAlliance` modifier of "lore alignment" (`+modifier`) flows into `CanMakeAlliance`'s score check, so LOTR-aligned pairs with high scores still pass. But LOTR-misaligned pairs that TAOM previously allowed via the unlimited cap may now fail the 50f threshold.

**Verify:** Decompile `DefaultAllianceModel.CanMakeAlliance` in v1.4.5. Check what score threshold it uses and whether `MaxNumberOfAlliances` short-circuits. If `CanMakeAlliance` uses a hard 50f threshold from `GetScoreOfStartingAlliance`, then:
- Faction pairs where TAOM's `IDiplomacyService.GetAllianceScoreModifier` returns < 50f cumulative will be VETOED in v1.4.5.
- This may BREAK lore-friendly alliances TAOM previously allowed (e.g., Rohan-Gondor at low base score plus TAOM's modifier might not reach 50f).

Also: the `using TaleWorlds.CampaignSystem;` import on line 1 of TaomAllianceModel.cs — is it still needed after dropping the IFaction param? (The Claude perf agent said yes because `CampaignTime` lives there. Verify.)

### Suspect 4: SpecialResourcesBehavior — `HideoutBattleEndState.Retreated` permissiveness

The new 3rd param `HideoutBattleEndState` has 5 values: `None, Retreated, Defeated, Victory, SendTroops`. TAOM ignores the param and earns the resource on any `winnerSide == Attacker && IsPlayerMapEvent`.

Hypothesis (from Claude data-flow agent): `Retreated` fires when `winnerSide=Attacker && !HasWinner` — an abandoned-field case where the player won't actually claim hideout loot. TAOM still awards the special resource in that case, which may be unintended.

**Verify:** Decompile `HideoutEventComponent.OnBeforeFinalize` in v1.4.5 to see exactly when each `HideoutBattleEndState` value fires. Is `Retreated` actually reachable with `winnerSide=Attacker`? Or does it always fire with `winnerSide=Defender` / `None`?

Bonus: in v1.3.15 the 2-param event always fired with `winnerSide=Attacker` ONLY on clean victory. If v1.4.5 expands the conditions under which Attacker fires, TAOM's permissive code may be earning resources in cases v1.3.15 never did. Decompile v1.3.15's `HideoutEventComponent` from the backup to compare.

### Suspect 5: Other reflection sites with the same bug class as IsFemale

The deep-review caught `ChildCreatorAdapter.cs:30` reflecting on `BasicCharacterObject.<IsFemale>k__BackingField` — silent no-op because `CharacterObject.IsFemale` override unconditionally reads `HeroObject.IsFemale`. Grep returned 2 other files using `*FieldValue.*BackingField` patterns in TAOM:

- `Main/Adapters/BannerHeroAdapter.cs`
- `Main/Features/HeroRace/EyeHeightAdjustmentHook.cs`

**Verify:** Read each file. For every `ReflectionHelper.SetFieldValue(target, "<X>k__BackingField", value)` or `GetFieldValue` call:
1. Identify the target's runtime type (not the declared static type — the actual class at runtime).
2. Decompile that class's hierarchy. Does any subclass override property `X` with an override that reads from a different field?
3. If yes → the reflection is bypassed at runtime, just like the IsFemale case.
4. If no → safe.

This is the most important Suspect for catching latent bugs the deep-review may have missed in adjacent files.

### Suspect 6: Harmony 2.4.2 internalized fork — runtime API resolution under v1.4.5

The fork compiled clean against v1.4.5 (0 errors). But Harmony's `AccessTools.Method`, `AccessTools.Field`, and `MonoMod.Utils.DynamicMethodDefinition` etc. are RUNTIME type resolvers — they look up TaleWorlds methods by name at patch-time.

**Verify:** No need to read all 1,444 Harmony source files. Instead:
- Decompile `MonoMod.Core.Interop.CoreCLR.cs` from `Dependencies/ThirdParty/Harmony/MonoMod.Core.Interop/CoreCLR.cs` and check version-specific assumptions (e.g., "V60" vs "V70" CLR layouts). v1.4.5 ships with which .NET runtime version? Has Bannerlord's bundled CLR changed since the fork was authored (April 2026)?
- Does Harmony's `HarmonySharedState` use any TaleWorlds types directly? Per memory `harmony-fork-research.md` it uses `dynamic` type + `byte[]` serialization to remain cross-assembly compatible. Verify this is still true in the restored source.

### Suspect 7: equipment XML data migration not yet applied — runtime consequences

TAOM ships `civilian="true"` on 3,372 `<EquipmentSet>` elements across 51 XML files (S5a will migrate). Until then, vanilla v1.4.5 will:
- Log deprecation warnings on every load (noise, not breakage)
- For `<EquipmentSet>` specifically (not `<EquipmentRoster>` inline), the engine may reject the equipment set entirely → NPC spawns without that civilian gear

**Verify:** Decompile `MBEquipmentRoster.Deserialize` or equivalent in v1.4.5. What happens when an `<EquipmentSet>` has `civilian="true"` but no `equipmentType=`? Is it:
- Accepted (logs warning, treats as Civilian)
- Rejected (logs warning, equipment set is invalid)
- Ignored silently (no warning, no effect)

This determines whether S5a is a SOFT requirement (warnings) or HARD requirement (breakage) for S6 smoke test.

### Suspect 8: Native dep version `e1.4.5.*` wildcard semantics

TAOM's `Main/_Module/SubModule.xml` declares Native dep as `e1.4.5.*` (BUTR schema). The `.*` is a patch-version wildcard.

**Verify:** Is the BUTR XML schema's `eX.Y.Z.*` wildcard interpreted as:
- Equal to or newer than `eX.Y.Z` (open-ended)
- Match `eX.Y.Z.*` for any patch level
- Match only `eX.Y.Z` exactly

If too restrictive, TAOM may refuse to load on Bannerlord v1.4.6 or v1.5.x. If too loose, TAOM may load on v1.4.0–v1.4.4 and crash. Check the BUTR.Bannerlord.ButterLib loader logic if accessible.

## REQUIRED SECTIONS

### VANILLA CODE

For each of Suspects 1-4 above, paste the decompiled v1.4.5 source as a fenced code block alongside TAOM's calling code. If you only paste TAOM source without vanilla, the finding is unverified.

### FEATURE-SPECIFIC DEEP ANALYSIS

Concrete scenarios for each Suspect:
- **Suspect 1:** A player triggers a child birth in a zero-male clan (forcing female-→-male assignment). Trace the equipment assignment end-to-end. Does the child get gender-appropriate equipment in v1.4.5? (After the engine internally filtered by gender.)
- **Suspect 2:** A battle where vanilla `renownMultiplierForWinnerSide = 0.5` (e.g., a Pyrrhic victory). TAOM's Umbar renown feat = +20%. What's the final number? Confirm against vanilla's perk-bonus math.
- **Suspect 3:** Gondor-Mordor alliance attempt. TAOM's diplomacy service returns -10000f (hostile pair). What does `CanMakeAlliance` return? Confirmed VETOED?
- **Suspect 4:** Player attacks bandit hideout. Mid-fight, half of bandits retreat. `winnerSide` resolves to... what? Does TAOM earn the resource for this messy outcome?

### CONFIG CROSS-REFERENCE

The 4 C# changes don't directly reference config IDs, but `SpecialResourcesBehavior.OnHideoutCompleted` indirectly resolves resources for the player's clan/kingdom/culture via `_service.EarnFromHideout(hero.StringId, kingdomId, cultureId)`. Verify the kingdom/culture IDs flowing through `GetHeroIds(hero, out kingdomId, out cultureId)` match the cheatsheet above and the IDs in `Main/_Module/ModuleData/special_resources/special_resources_config.xml`.

### FINDINGS OR OBSERVATIONS

For each Suspect 1-8, output:
- **CONFIRMED [Suspect N]:** {what's broken, where, why, severity}
- **DISPUTED [Suspect N]:** {why the suspect's premise is wrong}
- **NEEDS-USER-INPUT [Suspect N]:** {what design decision the user needs to make}

Add any additional findings outside the 8 suspects that you discover while reading the code.

## QUALITY GATES

The review is incomplete unless ALL of these are true:
- [ ] Every Suspect has a CONFIRMED / DISPUTED / NEEDS-USER-INPUT verdict
- [ ] At least one vanilla decompile code block is included per Suspect 1-4
- [ ] Reflection sites in BannerHeroAdapter + EyeHeightAdjustmentHook are read and assessed (Suspect 5)
- [ ] All findings cite specific file:line in TAOM code
- [ ] Findings have severity ratings: CRITICAL / HIGH / MEDIUM / LOW

## Prior review lessons

**SUCCESSES:**
- Config ID cross-reference catches kingdom/culture mismatches (rohan/dol_guldur class)
- Vanilla decompilation catches missing safety gates (e.g., Patch30 GetOrderPositionOfUnitAux navmesh check, MixedFormations 2026-05-06)
- Lifecycle tracing catches stale caches across save-load boundaries
- Reflection-target verification catches silent no-ops (this changeset's IsFemale bug)

**FAILURES TO AVOID:**
- Codex once assumed `empire=Rohan` (it's Dunland in TAOM)
- Codex flagged vanilla-matching code as bugs (don't flag a pattern that matches vanilla 1.4.5's own implementation)
- Codex skipped hard sections (always do the decompilation)
- Codex re-flagged findings already in the RCA (read the RCA file FIRST)

## Output

Write your full review to: `docs/reviews/codex-adversarial-v1.4.5-migration-2026-05-22.md`

Include a "Summary" section at the top with the verdicts table:

| Suspect | Verdict | Severity |
|---|---|---|
| 1 — ChildCreatorAdapter useSourceEquipmentType | TBD | TBD |
| 2 — TaomBattleRewardModel multiplier | TBD | TBD |
| 3 — TaomAllianceModel CanMakeAlliance veto | TBD | TBD |
| 4 — HideoutBattleEndState permissiveness | TBD | TBD |
| 5 — Other reflection sites | TBD | TBD |
| 6 — Harmony fork runtime resolution | TBD | TBD |
| 7 — XML deprecation runtime impact | TBD | TBD |
| 8 — Native dep version wildcard | TBD | TBD |
| Additional findings | (any) | (varies) |

Be terse but specific. We'd rather have 5 verified findings with code citations than 20 vague hunches.
