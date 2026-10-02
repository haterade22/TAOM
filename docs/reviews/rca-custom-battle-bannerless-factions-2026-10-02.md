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

## Two older Custom Battle defects found by the review, fixed in the same change

The review surfaced two pre-existing defects; Mike asked for both to be fixed with the crash.

**A. 24 curated commanders never existed in Custom Battle.** `custom_battle_commanders.json` lists 24 lords (all 12
Mordor ids, 7 of Gondor's, 5 of Rohan's: Sauron, the Witch-king, Boromir, Théoden...) that TAOM builds from vanilla
SandBox lords in `lords.xslt`. SandBox registers its `lords.xml` for `Campaign` and `CampaignStoryMode` only (the same
in the v1.3.0 copy at `E:\LOTRAOMAssets\1.3.0\SandBox\SubModule.xml`), so in a Custom Battle the templates have
nothing to rebuild. Mordor resolved to no one and fell back to its default lords (the warning in both logs). Gondor
showed only Duinhir and Rohan only Éomer and Éowyn, so the failure looked like a short list rather than a broken one.
Fix: `characters/custom_battle_lords.xml`, CustomGame only and registered before the `lords` node, holds a bare stub
per id. `MBObjectManager.CreateMergedXmlFile` (`:962-976`) applies each node's XSLT to everything merged before it,
so `lords.xslt` rebuilds every stub into the full character. A probe confirmed all 24 come out with a name, face and
equipment and skill sets that Custom Battle loads. **Why missed (2026-06-27):** the shipped-data test and the Codex
cross-check counted an id as real when a `lords.xslt` template named it, which proves the id exists in the campaign
only. The test now requires `characters/lords.xml` or a stub.

**B. TAOM's formation default troops never applied in Custom Battle.** `ObjectManagerAdapter.GetAllCultureInfos`
filled troop ids only under `c is CultureObject`, and `CustomGame.OnRegisterTypes` registers cultures as plain
`BasicCultureObject` (`CustomGame.cs:136`), so the branch never ran there and every TAOM default came back empty.
Vanilla's troop picker then marked the first matching troop in load order as the default. For the six re-skinned
cultures it was worse: vanilla's switch picks a Calradian troop (Rohan infantry: the Vlandian Swordsman, 21 of its 23
ids still load for Custom Battle), and TAOM's hook deliberately left a vanilla pick alone. Fix: the adapter reads the
five troop attributes back from the merged `SPCultures` XML for the current game type (`CultureTroopIdReader`, the
pattern `MonsterSizeCatalogAdapter` already uses), and the hook now prefers TAOM's troop over vanilla's (Mike's call,
2026-10-02). All 110 troop references the 22 playable cultures make (76 distinct troops) exist in Custom Battle. **Why missed:** the adapter branch
was written against the campaign's type, and the service tests mock the adapter, so nothing ran the branch under the
game type that uses it; the only runtime trace was an absent DEBUG line.

The fix's second review (2026-10-02) found that replacing vanilla's pick unconditionally regressed five slots. Vanilla
`ArmyCompositionItemVM.IsValidUnitItem` lists a troop in a slot only when it is the slot's culture and its formation
class fits, and ignores any other default. The culture data names a foot archer for every horse-archer slot, mostly
infantry nobles for cavalry, and another culture's troops in seven cultures, so TAOM's troop fits only 33 of the 88
culture-and-slot pairs. Where it did not fit, the hook had thrown away vanilla's valid elite pick and the slot fell to a
lower-tier troop (Dunland and Dale cavalry, Variag infantry and ranged, Easterling horse archers). The service now
offers a troop only when that check passes (`IsEligibleForSlot`, mirroring vanilla's filter, including the
soldier-and-not-obsolete prefilter `ArmyCompositionGroupVM` applies), so vanilla's pick stands everywhere else.
The convergence pass then caught that this filter returned null where it mattered most. The default has a second
consumer: at Start `CustomBattleHelper.PopulateListsWithDefaults` spawns it unchecked for a slot left empty, and a
slot is empty only when nothing passes the very check the filter copies. Abanissa and Shaghana have no soldiers of
their own culture, so every slot was empty, every default null, and Start would throw (`CustomBattleTroopSupplier`
reads `DefaultFormationClass` on the null troop). The service still prefers a fitting troop, but when vanilla has no pick
and nothing fits it returns the first TAOM candidate that loads (their Harad troops), as `9e2a39f4` did. Defaulting the other 55 pairs to TAOM troops needs slot-correct ids in the culture data.

## Status

- #1, #3, #4: fixed in this change. A and B above: fixed in this change, in-game check owed: Mordor's commander
  list shows Sauron first with a real face and gear; Gondor and Rohan show their full lists; Rohan infantry and ranged
  default to the Rohirrim militia; Dunland cavalry still defaults to vanilla's pick; Start one Custom Battle with
  Abanissa (or Shaghana) on a side, the crash path the null fallback guards; one Custom Battle with Sauron as
  commander, since his race-keyed features never ran there (his signature strikes run; TAOM's race combat rules do not,
  they live in campaign-only models).
- #5: not applied, reason above.
- #2 (issue): [#709](https://github.com/haterade22/TAOM/issues/709) covers the crash and A and B, filed on Mike's word
  (2026-10-02), to be closed with `triage-needs-ingame` for the in-game checks above.
- #6 (1.4.5 port): a separate commit on `bannerlord-1.4.5`, recorded on the issue. That branch already defines
  `lord_1_48_1/2/3` in its own `characters/lords.xml`, so it carries 21 stubs, not 24.
- Follow-ups not fixed here, each worth its own issue: slot-correct troop ids for the 55 culture/slot pairs that do not
  fit; cultures with no army of their own in Custom Battle (Abanissa and Shaghana have no own-culture soldiers,
  Lothlórien only a practice dummy); TAOM practice dummies listed as Custom Battle troops (`occupation="Soldier"`);
  177 Calradian soldiers still listed under the six re-skinned cultures; Sauron's race combat rules absent in Custom
  Battle; the banner-key data gate offered above.
- Verification: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, quoted in the commit that carries
  the B fix. The one standing failure is `EveryLanguage_DeclaresARowForEveryEnglishKey` (the `taom_tr_*`
  tournament-rewards keys awaiting their translation run), unrelated to this change. In game: Custom Battle starts with
  22 factions (Mike, 2026-10-02). Note: `9e2a39f4` carried the stubs without the face-gate exemption, so CI run
  37056303520 failed `EveryNpcCharacterDeclaresAFace` on it; the follow-up commit restores it.
