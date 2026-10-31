# Castle Recruitment

## Overview

Lets the player **and** AI lords recruit volunteer troops from **castles**, which in vanilla Bannerlord (and TAOM before this feature) can only be done at towns and villages. Castles gain notables with recruitable volunteers, a "Recruit troops" castle menu option, and AI lord parties score, travel to and drain the volunteers of castles that hold some, like they do towns. MCM/JSON-tunable; castle notables' campaign issues/quests are suppressed (relations untouched).

## Why This Exists

Recruitment in Bannerlord is **notable-driven** — you recruit the volunteer troops held by a settlement's `Notables`. Vanilla blocks castles at five independent settlement-type gates:

1. `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement` returns 0 for castles.
2. `NotablesCampaignBehavior.SpawnNotablesAtGameStart` + `SettlementHelper.SpawnNotablesIfNeeded` skip `!IsTown && !IsVillage` — castles never get notables.
3. `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement` early-returns for castles (no volunteer generation).
4. The `recruit_volunteers` game-menu option is registered only on `"town"`/`"village"` menus — the player has no recruit button at a castle.
5. `AiVisitSettlementBehavior` has `!settlement.IsCastle` in its recruitment scoring (line ~269) + targeting (line ~696), and `RecruitmentCampaignBehavior.HourlyTickParty` only calls `CheckRecruiting` for `IsVillage || IsTown` — so AI never scores, travels to, or drains castle volunteers.

Two vanilla pieces already cooperate: the recruit **screen** (`RecruitmentVM`) and the access model (`CanMainHeroDoSettlementAction(RecruitTroops)`) are already settlement-type-agnostic and return true for castles. And **TAOM's `VolunteerRecruitmentService` already authors `castle_*` recruitment pools** (previously consumed only by castle-bound villages) — so castle notables immediately offer culturally-correct LOTR troops with zero new pool data.

## Architecture

### Design challenge

Two vanilla methods **NRE for castles** because a castle's `Settlement.Village` is null:
- `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` line 103: `settlement.IsTown ? settlement.Town : settlement.Village.TradeBound?.Town`.
- `DefaultVolunteerModel.GetBasicVolunteer` line 113: `sellerHero.IsRuralNotable && sellerHero.CurrentSettlement.Village.Bound.IsCastle`.

So we cannot simply widen the vanilla gates and reuse vanilla's volunteer-fill loop. Instead:

### Solution

| Concern | Approach |
|---------|----------|
| **Notable population** (gates 1-2) | A TAOM `CastleRecruitmentBehavior` spawns castle notables via `HeroCreator.CreateNotable(occupation, castle)` — vanilla's own `NotablesCampaignBehavior.OnHeroCreated` then places them and gives gold (no settlement-type gate). Runs on `OnNewGameCreated`, `OnGameLoaded` (retrofits existing saves), and `DailyTickSettlement` (maintenance). Fully additive — no vanilla method patched. Idempotent (counts live notables, spawns only the deficit). |
| **Volunteer generation** (gate 3) | `CastleNotableMaintainer.FillCastleVolunteers` mirrors vanilla's daily fill but uses the service's **pure** `GetSlotProductionProbability` instead of the castle-NRE `GetDailyVolunteerProductionProbability`, and only uses **castle-safe occupations** (never `RuralNotable`, so the `GetBasicVolunteer` NRE path is unreachable). Since #765 the fixed curve is scaled by the War Chronicle `VolunteerRate` multiplier of the castle owner's kingdom through `VolunteerProductionService` (not for a castle of the player's own clan; no culture respawn feats, which castles never had), see [war-chronicle.md](war-chronicle.md). |
| **Player menu** (gate 4) | `CastleRecruitmentBehavior.OnSessionLaunched` registers a `recruit_volunteers` option on the `"castle"` menu (reuses the vanilla loc key `{=E31IJyqs}`); the consequence calls `args.MenuContext.OpenRecruitVolunteers()`. Screen needs no changes. |
| **AI scoring + travel** (gate 5) | One Harmony **transpiler** (`Patch42`) swaps the recruit-gate `get_IsCastle` call in `AiVisitSettlementBehavior.AiHourlyTick` for `CastleAiToggle.IsExcludedFromRecruitScoring` (same stack shape, runtime-toggleable). The gate opens only for a castle where a live notable holds a volunteer in slots 0 to 3 (`CastleVolunteerScan`, vanilla's own-faction estimate). An empty castle keeps vanilla's exact score: no recruit pull and no 0.25/0.16 floor. The x1.4 castle bonus applies to every castle regardless (the second `get_IsCastle` that feeds it is never touched). Vanilla's targeting filter (`FillSettlementsToVisitWithDistancesAsDays`) is no longer patched, so a kingdom lord far from home does not target a friendly foreign castle. |
| **AI in-settlement recruit** (gate 5) | A `HourlyTickPartyEvent` listener in `CastleRecruitmentBehavior` (gated on the AI toggle) hands the party to `CastleAiRecruitDispatcher`, which replays vanilla's outer guards and invokes the private `CheckRecruiting` (bound once per campaign to a closed delegate) for AI lord parties in a non-besieged castle. Reuses vanilla's exact recruit logic. A BannerlordCoop client never raises `HourlyTickPartyEvent` (its `PartyTickPatch` skips the periodic tick there), so the listener needs no authority gate. The hourly recruit stands down in a co-op session (`IsSessionActive \|\| ShouldDeferToHost`, read live after the castle guards, #920). This is not a gold guard: BannerlordCoop already refuses player-controlled parties inside `CheckRecruiting` (its own prefix, which also runs for the dispatcher's delegate call). The stand-down matches BannerlordCoop's hourly-off for every party while a session is live, plus cover for co-op mods TAOM cannot probe (`ShouldDeferToHost`, BannerlordTogether included), which lasts while such a mod is enabled, solo play included. Recruiting on entry is unaffected. |
| **AI recruit gate** (#918) | `TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero` asks `ICastleRecruitmentService.BlocksAiRecruitAtCastle` and returns -1 for an AI lord buying from a castle notable while AI castle recruitment (or the master toggle) is off; this one call covers the entry recruit and the hourly recruit. |
| **Issue/quest suppression** | `CastleRecruitmentBehavior` listens to `CampaignEvents.CanHaveCampaignIssuesEvent` and returns `false` for castle notables. This blocks both issues and quests (quests require an issue first) while leaving relations untouched. Benign side effect: castle notables also never despawn via `CheckAndMakeNotableDisappear` (which is gated by `CanHaveCampaignIssues`) — they are stable. |

```
CastleRecruitmentBehavior (thin event router)
 ├─ OnSessionLaunched ──── castle "Recruit troops" menu option
 ├─ OnNewGameCreated/OnGameLoaded/OnDailyTickSettlement ─→ CastleNotableMaintainer (spawn + fill)
 ├─ HourlyTickPartyEvent ─→ CastleAiRecruitDispatcher ─→ private CheckRecruiting
 └─ CanHaveCampaignIssuesEvent ──── suppress issues for castle notables
                │
                └─ ICastleRecruitmentService (pure: occupation targets, slot probability, toggles)
                        └─ ICastleRecruitmentSettingsProvider (MCM over JSON) ── ICastleRecruitmentConfigProvider (JSON + validation)

TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero (AI recruit gate, #918):
 └─ ICastleRecruitmentService.BlocksAiRecruitAtCastle ─→ -1 for an AI lord buying from a castle notable
                                                         (entry recruit and hourly recruit alike)

Patch42_CastleRecruitment (AI scoring):
 └─ AiHourlyTick transpiler ─── CastleAiToggle.IsExcludedFromRecruitScoring (runtime toggle)
                                 └─ CastleVolunteerScan (volunteer in slots 0-3?)
```

## Configuration

| Setting | MCM (`TaomSettings`) | JSON (`castle_recruitment_config.json`) | Default | Range |
|---------|----------------------|-----------------------------------------|---------|-------|
| Master toggle | `EnableCastleRecruitment` | `Enabled` | `true` | bool |
| AI recruits from castles | `EnableCastleRecruitmentAi` | `AiEnabled` | `true` | bool |
| Notables per castle | `CastleNotablesPerCastle` | `NotablesPerCastle` | `3` | 1-5 |

`NotablesPerCastle` is distributed round-robin across **GangLeader → Headman → Merchant → Artisan** (3 → one of the first three; 5 → two GangLeaders + one each). MCM overrides JSON at runtime; `Reuse.Singleton` providers cache for the whole process (a JSON edit needs an app restart). Disabling the feature stops new spawning / menu / AI / issue suppression but leaves existing castle notables in the save (disabled = inert, not removed).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CastleRecruitment/CastleNotableOccupation.cs` | TAOM-side occupation enum (no `RuralNotable` — castle-NRE) |
| `Main/Features/CastleRecruitment/CastleRecruitmentConfig.cs` + `…ConfigProvider.cs` | JSON POCO + loader with `NotablesPerCastle` range validation |
| `Main/Features/CastleRecruitment/CastleRecruitmentSettingsProvider.cs` | MCM-over-JSON merge |
| `Main/Features/CastleRecruitment/CastleVolunteerScan.cs` | Pure scan: volunteer in the scored slots 0 to 3 (`OwnFactionSlotCount`) |
| `Main/Features/CastleRecruitment/CastleRecruitmentService.cs` | Pure logic: occupation distribution, slot probability curve, toggles, `BlocksAiRecruitAtCastle` |
| `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs` | `MaximumIndexHeroCanRecruitFromHero` consults `BlocksAiRecruitAtCastle` before the alignment rule (#918) |
| `Main/Features/CastleRecruitment/CastleRecruitmentIoC.cs` | DryIoc registration |
| `Main/Features/AlignmentRecruitment/Hooks/Patch114_AiVolunteerEstimate_Postfix.cs` | Zeroes the same vanilla AI recruit estimate for an alignment-blocked pair (see [alignment-recruitment.md](alignment-recruitment.md), #887) |
| `Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs` | Thin event router: menu, population trigger, hourly recruit listener, issue suppression |
| `Main/Features/CastleRecruitment/Hooks/CastleNotableMaintainer.cs` | Engine glue: spawn + volunteer fill |
| `Main/Features/CastleRecruitment/Hooks/CastleAiRecruitDispatcher.cs` | Engine glue: binds `CheckRecruiting` once, replays vanilla's guards, recruits in a castle |
| `Main/Features/CastleRecruitment/Hooks/CastleAiToggle.cs` | Runtime AI toggle and volunteer gate consulted by the transpiler |
| `Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs` | IL surgery (first-`get_IsCastle` + anchor, fail-safe) |
| `Main/Features/CastleRecruitment/Hooks/Patch42_AiHourlyTick_Transpiler.cs` | AI scoring gate |
| `Main/_Module/ModuleData/castle_recruitment/castle_recruitment_config.json` | Config defaults |
| `Main/SubModule.cs` / `Main/IoC.cs` / `Main/Features/TaomSettings.cs` | Wiring + MCM knobs |

## Dependencies

- TAOM `VolunteerRecruitmentService` / `TaomVolunteerModel` (already author `castle_*` pools — castle notables get LOTR troops for free).
- Vanilla `NotablesCampaignBehavior.OnHeroCreated` (auto-places created notables; high-power deaths auto-replaced).
- Vanilla `RecruitmentVM` + `DefaultSettlementAccessModel` (already castle-agnostic).
- `VolunteerProductionService` (TroopProgression) and, through it, `IWarEffectService` (WarChronicle): the War Chronicle volunteer multiplier on the slot curve (#765).

## Tests

`TAOM.Tests/Features/CastleRecruitment/`:
- `CastleRecruitmentServiceTests` (41): the `BlocksAiRecruitAtCastle` truth table (non-castle seller, player buyer, AI toggle off, master toggle off, both on; #918), the #918 check (the blocked counter, the AI-recruit truth table, and every pass, fail and skip line read from a real `OutcomeLog`), occupation distribution (0/1/3/4/5/negative), slot-probability curve (monotonic / clamp / negative), the War Chronicle multiplier on the curve (scaled, none without a kingdom, none for the player's clan), master+AI toggle gating, RuralNotable absence.
- `CastleRecruitmentConfigProviderTests` (9): `NotablesPerCastle` validation (zero/negative/too-high), valid values logging info, missing file, malformed JSON, empty object, caching.
- `CastleVolunteerScanTests` (8): null and empty arrays, all-null slots, slots 4 and 5 not counted, slots 0 and 3 counted, a short array.
- `TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests` (7) pins seven vanilla facts the dispatcher, the scan, the model gate and the #918 check rely on: `CheckRecruiting` still binds to the delegate shape, vanilla `HourlyTickParty` still skips castles (else the listener would double-recruit), the volunteer estimate still counts 4 own and 2 foreign slots, `RecruitVolunteersFromNotable` still calls `VolunteerModel.MaximumIndexHeroCanRecruitFromHero`, it still reaches `ApplyInternal` through `GetRecruitVolunteerFromIndividual`, `ApplyInternal` still raises `OnTroopRecruited`, and the garrison auto-recruit still asks `MaximumIndexGarrisonCanRecruitFromHero`, never the hero query. The dispatcher's copy of vanilla's outer party guard, its `IsUnderSiege` filter and the co-op stand-down are not pinned.
- `TranspilerSiteBindingTests.CastleRecruitment_AiHourlyTickGate_StillResolves_InInstalledEngine`: the real `AiHourlyTick` IL still holds the gate and the swap lands on exactly one call. `ReflectionSiteBindingTests` pins `RecruitmentCampaignBehavior.CheckRecruiting`. The binding tests run in the default suite whenever a game install resolves (`GameAssemblies`); `binding-gate.runsettings` turns Inconclusive into a failure.

Boundary classes (`CastleRecruitmentBehavior`, `CastleNotableMaintainer`, `CastleAiRecruitDispatcher`, `CastleRecruitObserver`, `CastleAiToggle` (its engine loop is game-tested; the decision is `CastleVolunteerScan`), the `Patch42` hook, `TaomVolunteerModel`'s castle extraction and the dispatcher's co-op stand-down) are game-tested by convention (Harmony/CampaignBehavior).

## How-To

**Change castle recruitment strength:** set `Notables Per Castle` (1-5) in MCM, or `NotablesPerCastle` in the JSON.
**Disable AI castle recruitment but keep the player path:** turn off `AI Recruits From Castles`.
**Check #918 from a play session:** `python tools/ingame_verdict.py --issue 918`. With AI castle recruitment off, `CastleRecruitmentService` writes `[Check] id=taom.castle-recruit.ai-off` at each daily tick from `CastleRecruitObserver`'s `DailyTickEvent` listener, keyed by the campaign day (pass with the `blocked=` count of castle-rule refusals since the previous daily tick, fail when an AI lord took a castle notable's volunteer, skip `reason=toggle-on` while both toggles are on), and a fail line at once naming the castle and the lord when one does. A reload inside one game process drops the lines of every day that process already logged, and the engine reruns the saved day only when the save was made in its first half, so test from a fresh launch with one load and restart the game before a retry. The counters `taom.castle-recruit.ai-blocked` and `taom.castle-recruit.ai-recruited` reach the `[Session]` summary. The check sees only `OnTroopRecruitedEvent`, which vanilla garrisons and the player's recruit screen never raise, so the player's recruit screen stays an in-game item in `tools/ingame_checks/918.toml`, and the garrison path is pinned by `CastleRecruitmentEngineMirrorTests` instead.
**Add a castle's recruit pool:** it already inherits via `VolunteerRecruitmentService` (`SettlementMap["castle_X"]` → clan → culture fallback). Author a `castle_X` entry there for a bespoke pool.

## Performance

- The `HourlyTickPartyEvent` listener runs for every party once per in-game hour (about 2,000 to 3,200 parties in a TAOM session) and returns at once on the AI toggle, then on vanilla's party and settlement guards, then reads the co-op provider for a party in a castle (lock-only in solo play without a co-op module; up to two reflective probes per such party per hour when BannerlordCoop is enabled); `CheckRecruiting` is bound to a **closed delegate** once per campaign, so there is no per-call allocation or reflection dispatch.
- The volunteer gate scans a candidate castle's notables on each AI think: about 3 notables x 4 slots, castles only (every non-castle returns before the scan), with no allocation.
- Notable maintenance + volunteer fill run at daily cadence; `Settlement.All` is iterated only on new-game/load.
- The AI transpiler runs once at patch time and injects a single cheap static call (`CastleAiToggle`) into the scoring loop.

## Known Limitations / Notes

- Castle notables use only **GangLeader/Headman/Merchant/Artisan** occupations (RuralNotable would NRE).
- ~3 extra notable heroes per castle increases the campaign hero count modestly (the "moderate" cost). First load of a pre-existing save spawns the initial set (brief one-time cost).
- Castle volunteer regen uses a fixed probability curve, not vanilla's faction-fief-density scaling (a minor, intentional balance simplification: the vanilla formula NREs for castles). Since #765 the curve is scaled by the owner kingdom's War Chronicle `VolunteerRate` multiplier.
- The volunteer fill does **not** re-sort `VolunteerTypes` by tier like vanilla (cosmetic only; all slots are valid troops).
- **Fallback if the IL site breaks.** If `TranspilerSiteBindingTests` goes red after an engine bump, the alternative to patching `AiHourlyTick` is an `AiHourlyTickEvent` scorer with no Harmony. It must cache crowd counts hourly like vanilla's `RefreshTheTargetingSettlementDictionary`, skip foreign castles, and ship only after an in-game balance smoke against town visits.
- **The AI toggle (and the master toggle) stops every AI lord recruit from a castle notable**, on entry and hourly, through `TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero` returning -1 (#918). Garrisons keep their daily recruit (`MaximumIndexGarrisonCanRecruitFromHero`): that is out of the toggle's scope, which the MCM hint words as AI lord parties. The player's own party is never affected.
- Companion-led parties of the player's clan count as AI lords, as they already did for the hourly recruit and the scoring gate.
- **After a BannerlordCoop session ends**, BannerlordCoop keeps vanilla's hourly recruit off for the rest of the process, but TAOM's hourly castle recruit resumes (the session check reads live). A host who loads a solo save in the same process sees castles top up hourly while towns and villages do not, until the game restarts.
- **A castle that holds volunteers is scored like a town**, including vanilla's 0.25/0.16 floor for a healthy lord who cannot recruit (low gold, full party), and for a non-kingdom lord at a foreign castle stocked only in slots 2-3.
- `CastleRecruitmentEngineMirrorTests` pins seven vanilla facts (the `CheckRecruiting` delegate shape, `HourlyTickParty` skipping castles, the estimate's 4 and 2 slot counts, `RecruitVolunteersFromNotable` still calling `VolunteerModel.MaximumIndexHeroCanRecruitFromHero` and reaching `ApplyInternal` through `GetRecruitVolunteerFromIndividual`, `ApplyInternal` raising `OnTroopRecruited`, the garrison recruit's own query); a bump that changes one goes red. The dispatcher's copied party guard, siege filter and co-op stand-down are not pinned.
- See `docs/reviews/rca-castle-recruitment-2026-05-31.md` for the deep-review findings + the "widening a settlement-type gate" root-cause pattern.
- **Missing notable templates are a hard engine NRE, now guarded (2026-07-07).** `HeroCreator.CreateNotable` → `GetRandomTemplateByOccupation` returns **null** when the castle culture's `NotableTemplates` has no entry for the requested occupation, and `CreateHero` NREs on it. Because `EnsureAllCastles` runs inside `OnNewGameCreated`/`OnGameLoaded`, the escaped exception didn't CTD — it stalled `GameLoadingState` into re-running campaign creation every tick (26k+ identical NREs on a tester's infinite new-game loading screen; a stale module folder was the data cause). `CastleNotableMaintainer` now pre-checks the template set per occupation (skip + warn once per culture:occupation, naming the first affected castle) and wraps each castle in a fail-safe try/catch — same guard as `CultureConversionAdapter.ReplaceNotable` (#325). Deep-review hardening (same day): a **null-entry gate** also skips the whole culture when `NotableTemplates` contains a literal null entry (malformed `<notable_templates>` ref — the engine's occupation filter doesn't null-check, so one null entry NREs every `CreateNotable` for the culture); propagated to `CultureConversionAdapter` too. The `CreateNotable == null` branch is a forward-guard only (the engine throws rather than returning null on v1.4.6). Current dev data has full 4-occupation coverage for all 19 castle cultures (audited 2026-07-07). RCA: `docs/reviews/rca-castle-recruitment-guard-2026-07-07.md`.

## Changelog

- 2026-10-19: (#918) "AI Recruits From Castles" off now stops AI lords recruiting castle volunteers on entry as well as hourly, through `TaomVolunteerModel` and the pure `ICastleRecruitmentService.BlocksAiRecruitAtCastle`. (#920) The hourly castle recruit stands down during a co-op session: BannerlordCoop already refuses player parties in `CheckRecruiting`, so this matches its hourly-off while a session is live, plus cover for unprobeable co-op mods, which lasts while such a mod is enabled, solo play included; recruiting on entry is unaffected.
- 2026-10-18: (#886) One transpiler, gated on castles that hold volunteers: an empty castle keeps vanilla's score. Friendly foreign castles are no longer targeted by kingdom lords far from home (the targeting transpiler is gone). A `HourlyTickPartyEvent` listener replaces the `HourlyTickParty` postfix. Binding tests now cover the `AiHourlyTick` IL site and `CheckRecruiting`, and an engine-mirror gate pins three vanilla facts the dispatcher and the scan rely on.
- 2026-10-09: The castle slot curve goes through `VolunteerProductionService`, so the War Chronicle `VolunteerRate` multiplier of the castle owner's kingdom scales it (never for a castle of the player's own clan). Castles get no culture respawn feats, which the maintainer kept on 2026-10-14 (#765).
- 2026-07-07 — Missing-template guard: per-occupation `NotableTemplates` pre-check + per-castle fail-safe try/catch in `CastleNotableMaintainer` (new-game infinite-loading-loop crash on a stale-data install); maintainer takes `IModLogger`.
- 2026-05-31 — Recruit troops from castles (player + AI): new `CastleRecruitment` module (Patch42) — castle "Recruit troops" menu, AI scoring/travel/drain via two transpilers + `HourlyTickParty` postfix, `HeroCreator.CreateNotable` castle notables with castle-safe occupations and daily volunteer fill, issue/quest suppression, MCM "Castle Recruitment" group + `castle_recruitment_config.json` (master/AI toggles, notables-per-castle 1-5 default 3).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/alignment-recruitment.md](./alignment-recruitment.md)
- [docs/features/culture-conversion.md](./culture-conversion.md)
- [docs/modding/npcs-notables-and-townsfolk.md](../modding/npcs-notables-and-townsfolk.md)
- [docs/modding/troubleshooting.md](../modding/troubleshooting.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
