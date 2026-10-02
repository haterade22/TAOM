# RCA: Custom Battle Start crash on banner-less vanilla cultures (f9a7181d)

**Date:** 2026-10-02
**Scope:** the root-cause fix for crash `f9a7181d` and the `/deep-review` of that fix (6 lenses, two waves).
**Trigger:** Mike picked factions in Custom Battle (v2.0.32 dirty build, Bannerlord v1.5.3.122374) and pressed
Start. The game threw `ArgumentOutOfRangeException` in `TaleWorlds.Core.Banner.ChangePrimaryColor`, called from
vanilla `CustomBattleHelper.GetCustomBattleParties` <- `CustomBattleVM.PrepareBattleData` <- `ExecuteStart`. No
TAOM frame was on the throwing stack.

## Top-line

TAOM's Custom Battle faction picker listed three vanilla cultures, `nord`, `vakken` and `darshi`, that have no
`faction_banner_key`. Picking any of them crashed Start. The fix filters the picker to cultures whose banner has at
least one layer (`CultureInfo.HasFactionBanner`), which takes the list from 25 to 22. Mike's post-fix session
(`taom_debug_2026-10-02_11-38-06.log`) logs "Loaded 22 TAOM factions" and starts four Custom Battle missions.
The review found no defect in the fix; it found two untested filter clauses, two stale docs (one of which stated
the opposite of this crash), and owed paperwork.

## Root cause

1. `SandBoxCore/ModuleData/spcultures.xml` (the copy the engine loads; `SandBox/ModuleData/spcultures.xml` is not
   registered by any `SubModule.xml`) defines `nord`, `vakken` and `darshi` with `can_have_settlement="true"`,
   `is_main_culture="false"`, and no `faction_banner_key`, `color` or `color2`.
2. `BasicCultureObject.Deserialize` (v1.5.3, `:57`) gives a culture with no key `new Banner()`, an empty layer
   list, and (`:47`) a `Color` of `uint.MaxValue`.
3. `CustomBattleHelper.GetCustomBattleParties` (`:212-213`) copies each side's culture banner through
   `Banner(Banner, uint, uint)`, which calls `ChangePrimaryColor`. That method writes `_bannerDataList[0]` whenever
   `BannerManager.GetColorId(color) >= 0`. White `0xffffffff` is in Native's banner palette (id 172), so the write
   always happens, and an empty list throws.
4. Vanilla never reaches this with an empty banner. Its `CustomBattleData.Factions` getter is a hard-coded list:
   the six main cultures, plus `nord` only when NavalDLC is active, and NavalDLC's
   `NavalDLC_SandBoxCore_SPCultures.xslt` is what gives `nord` a key. Every culture vanilla lists has a banner.
5. TAOM's Patch19 prefix replaces that list with `CustomBattleService.GetFactionIds()`, a data filter
   (`CanHaveSettlement && !IsBandit`). The filter admits every settlement-capable non-bandit culture in the
   registry, which includes the three vanilla leftovers: 16 TAOM cultures + 6 re-skinned vanilla + 3 leftovers = 25,
   matching the crash log. The filter has been in this shape since the feature shipped (2026-03-27); the crash
   needed someone to pick one of the three, which Mike did at 11:15:47 (`nord`) and 11:15:55 (`vakken`).

**Fix:** `ObjectManagerAdapter.GetAllCultureInfos` sets `HasFactionBanner = c.Banner?.BannerDataList?.Count > 0`,
and `GetFactionIds` requires it. The engine lens confirmed the predicate is exact rather than cautious: every
access to layer 1 or later on the Start path is length-checked, and a malformed key also parses to an empty list,
so it is excluded too.

## Findings (the `/deep-review` of the fix)

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | MED | `docs/modding/cultures.md:55` told modders that omitting `faction_banner_key` "gives a blank banner rather than a crash". This crash is the counterexample. | Docs: engine claim | The row was written from the deserializer alone (`Banner` is never null), which is true, and generalised to "no crash" without reading any consumer of the empty banner. | Fixed: the row now says the load succeeds but recolouring code crashes, and that the picker leaves such a culture out. Lesson below. |
| 2 | MED | No GitHub issue exists for the crash (AGENTS.md "Documentation duty"). | Process | Investigation ran in plan mode and went straight to the fix; filing is public and waits on Mike's word. | Owed: file before the commit so the body can reference it. |
| 3 | LOW | No test isolated `!IsBandit` or `CanHaveSettlement`. Both bandit fixtures had `CanHaveSettlement = false`, so deleting either clause left the suite green. In shipped data `!IsBandit` is the only guard keeping eight settlement-capable, banner-bearing raider cultures (`dunland_raiders` and seven more) out of the picker. | Tests: skip-guard exhaustion | Pre-existing since the feature's first tests: each fixture excluded the culture for two reasons at once, so no test could tell which clause did the work. The new third clause raised the cost of that. | Fixed: `GetFactionIds_BanditCulture_IsExcluded` (renamed from `GetFactionIds_ExcludesBanditCultures`) now uses the real raider shape plus a control culture, and `GetFactionIds_CultureWithoutSettlement_IsExcluded` is new. Mutation-checked: removing `!IsBandit` fails the first, removing `CanHaveSettlement` fails the second. |
| 4 | LOW | `docs/features/custom-battles.md` listed the old three criteria, told modders a culture needs only `can_have_settlement`, and gave a stale test count. | Docs | Same change, docs not yet touched when the review ran. | Fixed: criteria, How-To step 3, test count (32) and a dated changelog bullet. |
| 5 | LOW | The code comments say a banner-less culture throws on Start; the engine only throws when the culture's colour is in the palette. | Comment precision | n/a | Not applied: true for every culture the filter removes (no `color` means white, which is in the palette), and the predicate is right either way. Recorded here instead. |
| 6 | n/a | The 1.4.5 line (patreon channel) almost certainly ships the same crash: `bannerlord-1.4.5` has the old filter and byte-identical files, and the v1.4.8 decompile has the same unguarded `ChangePrimaryColor`. The only older culture data on this machine (the v1.3.15 dedicated server) defines all three without keys; v1.4.8 client data was not checked. | Port | Not a miss: the review was scoped to this branch. | Owed on Mike's word: cherry-pick to `bannerlord-1.4.5`. |

Two wrong facts in the orchestrator's brief were caught by the lenses and corrected before they reached a doc: the
two adapter files are LF in the working copy (not CRLF), and the loaded culture file is SandBoxCore's, not SandBox's.

## Root-cause pattern: a data filter that replaces a vanilla hard-coded list

Vanilla's faction list was safe by construction: a fixed set of ids, each with a banner. TAOM replaced it with a
predicate over the whole culture registry, which holds 42 cultures (16 from SandBoxCore's `spcultures.xml`,
26 from `taom_spcultures.xml`), not just the 22 playable factions. The predicate
asked "can this culture own settlements and is it not a bandit", and nobody asked what else vanilla's fixed list
had guaranteed. It is the data-side form of the rule already written for engine calls
([`lessons/adapters-taleworlds-api.md`](lessons/adapters-taleworlds-api.md) "Broadening an engine call to a wider
entity set re-opens every precondition its vanilla callers relied on").

**This is the third leak of the same three vanilla leftovers into a TAOM-wide culture set:**

1. BannerBearers 2026-07-16: the fail-open default banner handed the Gondorian standard to `nord`, `vakken`,
   `darshi` and seven other unmapped cultures ([`lessons/data-content-cultures.md`](lessons/data-content-cultures.md)
   "A config's default/fallback value applies to the WHOLE set").
2. Landless cultures #374 2026-08-04: `darshi`, `nord`, `vakken` own no settlement and can sit on a lord
   (`lessons/data-content-cultures.md` "A culture that owns no settlement is a latent CTD").
3. This crash: they pass a settlement-and-bandit filter and carry no banner.

A repeat offender earns a stronger preventive action than a prose rule. The lesson below names the three ids, and
the optional shipped-data gate (every settlement-capable non-bandit TAOM culture has a parsable key) is offered to
Mike so a future TAOM culture without a key fails CI instead of silently vanishing from the picker.

## Why each lens found what it found

| Lens | Result | Why |
|---|---|---|
| 1 Standards | 0 rule violations; found #2, #4, #3 | Ran `gh issue list` for the crash id and counted `[TestMethod]`s against the doc. |
| 2 Engine compatibility | 26 verified, 0 incompatible; found #5 | Decompiled `BannerManager.GetColorId` and the palette, which is why it saw the throw is colour-conditional. Also flagged the SandBox/SandBoxCore mix-up. |
| 3 Efficiency | no issues | The check runs once per culture per process on a UI path. |
| 4 Completeness | found #1, #6, #2, #3 | Read the modder docs that describe the attribute, not only the feature doc, and read the 1.4.5 branch with `git show`. |
| 5 Data flow | 16 flows, all connected; found #3 | Traced every reader of `CustomBattleData.Factions` and proved no other route lets a banner-less culture reach Start. Confirmed the post-fix log. |
| 6 Design | 0 KEEP proposals | Judged `IsMainCulture`, a Harmony guard, `IsBannerDataListEmpty()`, a hook-side filter and a data fix; all REJECT under the simplicity criterion. |

Finding #3 was reported independently by three lenses (Standards, Completeness, Data flow), which is the expected shape for a mechanical test gap.

## Follow-ups outside this change (not fixed here)

- **Curated Mordor commanders never resolve.** `CustomBattleService: curated faction 'mordor' resolved to no existing
  commanders` fires before and after the fix. Its first id, `lord_1_17`, exists only in vanilla SandBox data. Cause
  UNVERIFIED; worth `/investigate` and its own issue.
- **TAOM's default-troop override is likely dead in Custom Battle.** `ObjectManagerAdapter.GetAllCultureInfos` fills
  troop ids only when `c is CultureObject`, but `CustomGame` registers plain `BasicCultureObject` (`CustomGame.cs:136`),
  so `GetDefaultTroopIdForFormation` returns null and vanilla falls back to the first troop per slot. The post-fix
  session logs zero `CustomBattleTroopHook: Resolved` lines. Worth its own issue.

## Status

- #1, #3, #4: fixed in this change.
- #5: not applied, reason above.
- #2 (issue), #6 (1.4.5 port): owed on Mike's word.
- Verification: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: 12,321 passed, 1 failed, 2 skipped.
  The one failure is `EveryLanguage_DeclaresARowForEveryEnglishKey` (the `taom_tr_*` tournament-rewards keys awaiting
  their translation run), unrelated to this change. In game: Custom Battle starts with 22 factions (Mike, 2026-10-02).
