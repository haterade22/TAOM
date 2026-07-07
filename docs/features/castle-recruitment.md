# Castle Recruitment

## Overview

Lets the player **and** AI lords recruit volunteer troops from **castles**, which in vanilla Bannerlord (and TAOM before this feature) can only be done at towns and villages. Castles gain notables with recruitable volunteers, a "Recruit troops" castle menu option, and AI lord parties score/travel to/drain castle volunteers like they do towns. MCM/JSON-tunable; castle notables' campaign issues/quests are suppressed (relations untouched).

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
| **Volunteer generation** (gate 3) | `CastleNotableMaintainer.FillCastleVolunteers` mirrors vanilla's daily fill but uses the service's **pure** `GetSlotProductionProbability` instead of the castle-NRE `GetDailyVolunteerProductionProbability`, and only uses **castle-safe occupations** (never `RuralNotable`, so the `GetBasicVolunteer` NRE path is unreachable). |
| **Player menu** (gate 4) | `CastleRecruitmentBehavior.OnSessionLaunched` registers a `recruit_volunteers` option on the `"castle"` menu (reuses the vanilla loc key `{=E31IJyqs}`); the consequence calls `args.MenuContext.OpenRecruitVolunteers()`. Screen needs no changes. |
| **AI scoring + travel** (gate 5) | Two Harmony **transpilers** (`Patch42`) swap the single `get_IsCastle` call in `AiVisitSettlementBehavior.AiHourlyTick` (scoring) and `FillSettlementsToVisitWithDistancesAsDays` (targeting) for `CastleAiToggle.IsCastleAndAiDisabled` — same stack shape, runtime-toggleable. |
| **AI in-settlement recruit** (gate 5) | A `Patch42` **Postfix** on `HourlyTickParty` invokes the private `CheckRecruiting` (bound once to an open delegate) for AI parties present in a non-besieged castle. Reuses vanilla's exact recruit logic. |
| **Issue/quest suppression** | `CastleRecruitmentBehavior` listens to `CampaignEvents.CanHaveCampaignIssuesEvent` and returns `false` for castle notables. This blocks both issues and quests (quests require an issue first) while leaving relations untouched. Benign side effect: castle notables also never despawn via `CheckAndMakeNotableDisappear` (which is gated by `CanHaveCampaignIssues`) — they are stable. |

```
CastleRecruitmentBehavior (thin event router)
 ├─ OnSessionLaunched ──── castle "Recruit troops" menu option
 ├─ OnNewGameCreated/OnGameLoaded/OnDailyTickSettlement ─→ CastleNotableMaintainer (spawn + fill)
 └─ CanHaveCampaignIssuesEvent ──── suppress issues for castle notables
                │
                └─ ICastleRecruitmentService (pure: occupation targets, slot probability, toggles)
                        └─ ICastleRecruitmentSettingsProvider (MCM over JSON) ── ICastleRecruitmentConfigProvider (JSON + validation)

Patch42_CastleRecruitment (AI):
 ├─ AiHourlyTick transpiler ─┐
 ├─ FillSettlements transpiler ┴─ CastleAiToggle.IsCastleAndAiDisabled (runtime toggle)
 └─ HourlyTickParty postfix ──── invokes private CheckRecruiting for castles
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
| `Main/Features/CastleRecruitment/CastleRecruitmentService.cs` | Pure logic: occupation distribution, slot probability curve, toggles |
| `Main/Features/CastleRecruitment/CastleRecruitmentIoC.cs` | DryIoc registration |
| `Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs` | Thin event router: menu, population trigger, issue suppression |
| `Main/Features/CastleRecruitment/Hooks/CastleNotableMaintainer.cs` | Engine glue: spawn + volunteer fill |
| `Main/Features/CastleRecruitment/Hooks/CastleAiToggle.cs` | Runtime AI toggle consulted by the transpilers |
| `Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs` | Shared IL surgery (first-`get_IsCastle` + anchor, fail-safe) |
| `Main/Features/CastleRecruitment/Hooks/Patch42_AiHourlyTick_Transpiler.cs` | AI scoring gate (T3) |
| `Main/Features/CastleRecruitment/Hooks/Patch42_FillSettlements_Transpiler.cs` | AI targeting gate (T4) |
| `Main/Features/CastleRecruitment/Hooks/Patch42_HourlyTickParty_Postfix.cs` | AI in-settlement recruit (T2) |
| `Main/_Module/ModuleData/castle_recruitment/castle_recruitment_config.json` | Config defaults |
| `Main/SubModule.cs` / `Main/IoC.cs` / `Main/Features/TaomSettings.cs` | Wiring + MCM knobs |

## Dependencies

- TAOM `VolunteerRecruitmentService` / `TaomVolunteerModel` (already author `castle_*` pools — castle notables get LOTR troops for free).
- Vanilla `NotablesCampaignBehavior.OnHeroCreated` (auto-places created notables; high-power deaths auto-replaced).
- Vanilla `RecruitmentVM` + `DefaultSettlementAccessModel` (already castle-agnostic).

## Tests

`TAOM.Tests/Features/CastleRecruitment/`:
- `CastleRecruitmentServiceTests` (16) — occupation distribution (0/1/3/4/5/negative), slot-probability curve (monotonic / clamp / negative), master+AI toggle gating, RuralNotable absence.
- `CastleRecruitmentConfigProviderTests` (8) — `NotablesPerCastle` validation (zero/negative/too-high), missing file, malformed JSON, empty object, caching.

Boundary classes (`CastleRecruitmentBehavior`, `CastleNotableMaintainer`, the `Patch42` hooks) are game-tested by convention (Harmony/CampaignBehavior).

## How-To

**Change castle recruitment strength:** set `Notables Per Castle` (1-5) in MCM, or `NotablesPerCastle` in the JSON.
**Disable AI castle recruitment but keep the player path:** turn off `AI Recruits From Castles`.
**Add a castle's recruit pool:** it already inherits via `VolunteerRecruitmentService` (`SettlementMap["castle_X"]` → clan → culture fallback). Author a `castle_X` entry there for a bespoke pool.

## Performance

- `HourlyTickParty` postfix (per-AI-party-per-hour, the hottest path) binds `CheckRecruiting` to an **open delegate** once — zero per-call allocation, no reflection dispatch.
- Notable maintenance + volunteer fill run at daily cadence; `Settlement.All` is iterated only on new-game/load.
- The AI transpilers run once at patch time and inject a single cheap static call (`CastleAiToggle`) into the scoring loop.

## Known Limitations / Notes

- Castle notables use only **GangLeader/Headman/Merchant/Artisan** occupations (RuralNotable would NRE).
- ~3 extra notable heroes per castle increases the campaign hero count modestly (the "moderate" cost). First load of a pre-existing save spawns the initial set (brief one-time cost).
- Castle volunteer regen uses a fixed probability curve, not vanilla's faction-fief-density scaling (a minor, intentional balance simplification — the vanilla formula NREs for castles).
- The volunteer fill does **not** re-sort `VolunteerTypes` by tier like vanilla (cosmetic only; all slots are valid troops).
- See `docs/reviews/rca-castle-recruitment-2026-05-31.md` for the deep-review findings + the "widening a settlement-type gate" root-cause pattern.
- **Missing notable templates are a hard engine NRE, now guarded (2026-07-07).** `HeroCreator.CreateNotable` → `GetRandomTemplateByOccupation` returns **null** when the castle culture's `NotableTemplates` has no entry for the requested occupation, and `CreateHero` NREs on it. Because `EnsureAllCastles` runs inside `OnNewGameCreated`/`OnGameLoaded`, the escaped exception didn't CTD — it stalled `GameLoadingState` into re-running campaign creation every tick (26k+ identical NREs on a tester's infinite new-game loading screen; a stale module folder was the data cause). `CastleNotableMaintainer` now pre-checks the template set per occupation (skip + warn once per culture:occupation, naming the first affected castle) and wraps each castle in a fail-safe try/catch — same guard as `CultureConversionAdapter.ReplaceNotable` (#325). Deep-review hardening (same day): a **null-entry gate** also skips the whole culture when `NotableTemplates` contains a literal null entry (malformed `<notable_templates>` ref — the engine's occupation filter doesn't null-check, so one null entry NREs every `CreateNotable` for the culture); propagated to `CultureConversionAdapter` too. The `CreateNotable == null` branch is a forward-guard only (the engine throws rather than returning null on v1.4.6). Current dev data has full 4-occupation coverage for all 19 castle cultures (audited 2026-07-07). RCA: `docs/reviews/rca-castle-recruitment-guard-2026-07-07.md`.

## Changelog

- 2026-07-07 — Missing-template guard: per-occupation `NotableTemplates` pre-check + per-castle fail-safe try/catch in `CastleNotableMaintainer` (new-game infinite-loading-loop crash on a stale-data install); maintainer takes `IModLogger`.
- 2026-05-31 — Recruit troops from castles (player + AI): new `CastleRecruitment` module (Patch42) — castle "Recruit troops" menu, AI scoring/travel/drain via two transpilers + `HourlyTickParty` postfix, `HeroCreator.CreateNotable` castle notables with castle-safe occupations and daily volunteer fill, issue/quest suppression, MCM "Castle Recruitment" group + `castle_recruitment_config.json` (master/AI toggles, notables-per-castle 1-5 default 3).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-conversion.md](./culture-conversion.md)

<!-- backlinks-end -->
