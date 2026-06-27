# Codex Adversarial Review — Custom Battle Curated Commander Lists (2026-06-27)

Model: gpt-5.5 @ xhigh. Prompt: `codex-adversarial-custom-battle-lords-2026-06-27.prompt.md`. Tokens: ~383k.
(This is the extracted final report; the raw streaming log + decompile dumps were discarded for size.)

**KNOWN SUSPECTS**

1. **DISPUTED** — Cross-culture commanders are harmless in the traced custom-battle path. TAOM intentionally bypasses culture in `CustomBattleService.cs:74-79`. Vanilla v1.4.6 `CustomBattleSideVM.OnCultureSelection` updates composition/banner colors from the selected faction culture, not the selected commander. `UpdateCharacterVisual` fills equipment from `SelectedCharacter` but banner/colors from `FactionSelectionGroup.SelectedItem.Faction`. `CustomBattleVM.PrepareBattleData` passes selected commanders and selected factions separately; `CustomBattleHelper.GetCustomBattleParties` builds combatants from faction culture/banner; `CustomBattleCombatant.SetGeneral` only assigns `_general = generalCharacter`.

2. **CONFIRMED** — All-unresolvable curated faction produces a stale/global wrong-list, not an NRE. `SideCommanderFilter` warns and skips null resolutions (`SideCommanderFilter.cs:31-42`); both side-VM postfixes then return on `commanders.Count == 0` and leave the dropdown untouched (`CustomBattleSideVM_OnCultureSelection_Patch.cs:38-42`, `CustomBattleSideVM_RefreshValues_Patch.cs:33-37`). Vanilla `RefreshValues` already filled the selector from global `CustomBattleData.Characters`, so the visible list remains unfiltered. → Finding #1.

3. **DISPUTED** — Master-list/dropdown decoupling holds. The master hook uses `GetCommanderIds()` + regex filtering; the dropdown resolves curated ids through `GetBasicCharacter`. Vanilla `PrepareBattleData` reads `PlayerSide.SelectedCharacter`/`EnemySide.SelectedCharacter` directly; `CustomBattleData.Characters` is only used in sergeant mode to pick a separate random player-side general, not to validate selected commanders.

4. **DISPUTED** — `HasCuratedEntry` and `GetCuratedCommanderIds` are consistent. Both short-circuit whitespace and hit the same `Lazy<Dictionary<...>>` with `StringComparer.OrdinalIgnoreCase` (`CustomBattleCommandersProvider.cs:43-57`).

5. **DISPUTED** — Config nullability/parse edge cases do not NRE. Missing file → empty (`:65-68`); parse failure → empty (`:71-80`); absent/null `factions` → empty (`:83-87`); null faction arrays normalized to empty list (`:116`); empty lists skipped (`:138-142`); root `_comment` ignored by Newtonsoft (DTO maps only `Factions`).

6. **DISPUTED** — Nazgûl culture edit does not break the checked Dol Guldur-keyed systems. `lord_1_48_1/2/3` are `Culture.mordor` in both `characters/lords.xml` and `lords.xslt`; load order registers transformed `lords` before `characters/lords` (`SubModule.xml:61-68`, `:122-129`), and vanilla `MBObjectManager.RegisterObject` keeps the first id, so the XSLT object is effective. Child generation excludes both `mordor` and `dolguldur` (`initial_child_generation.json:8-12`; `InitialChildGenerationService.cs:74-77`). Nazgûl registry is id-keyed (`NazgulRegistry.cs:26-29`). Volunteer recruitment cascades settlement → clan → context culture, not hero personal culture (`VolunteerRecruitmentService.cs:459-466`). Caveat: new games treat these three as Mordor for any `Hero.Culture`-first system (e.g. `CultureFeatAdapter.ResolvePartyCulture`); old saves retain Dol Guldur hero culture.

**CONFIG CROSS-REFERENCE**

| Faction key | Selectable culture | Commander ids | Result |
|---|---|---|---|
| `mordor` | OK | 12 ids incl. `lord_1_48`, `lord_1_48_1/2/3` | 12/12 resolve |
| `gondor` | OK | 8 ids incl. `lord_WE9_l` | 8/8 resolve |
| `vlandia` | OK (Rohan) | 7 ids incl. `lord_4_3_1`, `lord_4_3_2` | 7/7 resolve |
| `mirkwood` | OK | `lord_M1_1`, `lord_M1_11` | 2/2 resolve |
| `rivendell` | OK | `lord_R1_1`, `lord_R2_1`, `lord_R1_3`, `lord_R1_4` | 4/4 resolve |
| `lothlorien` | OK | `lord_L1_1`, `lord_L1_2` | 2/2 resolve |
| `isengard` | OK | 5 ids | 5/5 resolve |
| `erebor` | OK | `lord_E1_1`, `lord_E1_2`, `lord_E1_5` | 3/3 resolve |

Missing ids: **none**. Invalid faction keys: **none**. `rohan` and `dol_guldur` not present.

**FINDINGS**

1. **[HIGH] `CustomBattleSideVM_OnCultureSelection_Patch.cs:39` — Empty curated resolution leaves a stale/global commander dropdown.** A configured faction with only unresolvable ids stays "curated" at the service layer (`CustomBattleService.cs:78-79`), but `SideCommanderFilter` skips every id and the UI patches return without clearing/rebuilding, so the player can see/select unrelated commanders from the global list. Fix: when a curated list resolves to zero live characters, fall back to the default per-faction path or clear the selector — do not leave the previous/global list.

2. **[LOW] `docs/features/custom-battles.md:77` — Documentation drift.** "No external configuration files" contradicts the now-documented `custom_battle_commanders.json`. Fix: update the Configuration section.

**THINGS THE IMPLEMENTER MAY HAVE MISSED**

- No shipped-data regression test that parses the real `custom_battle_commanders.json` and verifies every faction key/id against real culture + lord sources (existing tests use synthetic JSON + a fake object manager).
- `lord_WE9_l` is `Culture.gondor` in `characters/lords.xml` but its effective runtime culture is the earlier XSLT `Culture.empire` object (duplicate-id registration order) — matches the cross-culture intent, but worth documenting.
- Existing saves may retain `Hero.Culture=dolguldur` for the three lesser Nazgûl while new games load `mordor`. No custom-battle break, but culture-driven campaign effects can differ by save.

**OBSERVATIONS**

- Cross-culture commanders are architecturally compatible with vanilla custom battle: selected faction drives banner/combatant culture/troop defaults; selected commander drives the character/general object.
- Static review + installed v1.4.6 decompile; the test suite was not run by Codex.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 0 | LOW: 1
VERDICT: ISSUES FOUND

---

## TAOM disposition (2026-06-27)

Both findings verified against source and FIXED in-session (commit follow-up to `656daae8`):

- Finding #1 (HIGH): `CustomBattleService.GetCommanderIdsForFaction` now filters curated ids by character existence and falls through to the default per-culture path when none survive (logs a warning). +2 fallback service tests + the shipped-data regression test Codex recommended (`CustomBattleCommandersShippedDataTests`). CustomBattles tests 61 → 65, all green.
- Finding #2 (LOW): doc Configuration section corrected.

RCA: `rca-custom-battle-lords-2026-06-27.md`. Lesson → `LESSONS-LEARNED.md` (GameModels & Services): a "fall back to default" fail-safe must cover runtime-unresolvable inputs, not just load-invalid ones.
