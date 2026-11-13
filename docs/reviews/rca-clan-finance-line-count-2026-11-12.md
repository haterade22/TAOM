# RCA: Clan finance model over the line limit (#1131, deep review 2026-11-12)

## Top-line

Trunk at 646f8a26c failed `EntryPointLineCountGateTests`: `TaomClanFinanceModel` was 163 lines against the ADR-002
limit of 150. Commit d37261d36 (Realms fief income tribute) added about 40 lines of tribute display code to the model
and landed without a full `/verify` run. The fix moves the tribute lines into `RealmsCampaign.Tribute.cs`, a plain
class, and leaves the model at 123 lines with the same behaviour. The tier 1 deep review (lenses 5 and 2 at max
effort; Codex out of usage, Mike 2026-11-12) confirmed 1 MEDIUM and 5 LOW findings. The MEDIUM is outside the diff
and has its own issue.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | All 196 `{=realms_*}` keys in `Main/` and `Client/` C#, the two tribute labels included, have no language row anywhere under `Main/_Module/ModuleData` | Localization gate scope | `harvest_literal_loc_keys.py` and `UnregisteredLocalizationKeyBaselineTests` match only the `taom_` prefix, so a Realms key is never harvested and never reported | FOLLOW-UP #1130: widen both to `realms_`, harvest the rows; a lesson in `localization-ui.md` |
| 2 | LOW | No test pinned which finance projection carries which tribute line, so a moved or dropped call stays green | Test coverage | The tribute work had no wiring test; the move made the gap visible | `TributeFinanceWiringTests`: one count test per member, red when the gold change loses its paid line (mutant run) |
| 3 | LOW | The `gamemodel-registry.md` row and the `cultural-feats.md` file row named no tribute line and no `CalculateClanExpenses` override | Docs drift | d37261d36 changed the model and not the rows that describe it | Both rows now name the tribute lines and the override |
| 4 | LOW | The `CalculateClanIncome` comment said `ClanManagementVM` is the only engine caller; StoryMode's `AchievementsCampaignBehavior.ProgressDailyIncome` also calls it (v1.5.7) | Comment accuracy | The claim was written from one search of the campaign assembly, not of every module | Comment names both callers; neither moves money, so the withdrawal reasoning holds |
| 5 | LOW | The first pin read the whole file, so a call moved to the wrong member still passed | Test design (convergence pass) | The test counted fragments per file, not per member | Counts per member through `CareerCampaignPassivesTests.MemberText` |
| 6 | LOW | The first pin fixed the call order, which failed on a harmless reorder | Test design (convergence pass) | The order was pinned without a reason the order matters | Order dropped; counts only |

## Root-cause pattern

The trunk went red because a commit landed without the gate run that would have stopped it. The finance model is a
GameModel, so ADR-002 counts it as an entry point, and the line gate is in the full C# suite only. Finding 1 is the
same shape in another gate: a localization check scoped by a prefix list sees only the keys its author thought of.
Both gates passed on what they were told to look at.

## Why each agent missed these

- **Earlier work (d37261d36):** no `/verify` before the commit, so the line gate never ran on it.
- **Lens 5 (data flow) and lens 2 (engine):** both found finding 1 independently, and the lenses reported findings 2
  to 4. Finding 4 is confirmed by `AchievementsCampaignBehavior.cs:672` in the v1.5.7 decompile.
- **Convergence pass:** found findings 5 and 6 in the new test.

## Changes

- `Main/Features/CulturalFeats/Models/TaomClanFinanceModel.cs`: the tribute methods and labels removed; four calls to
  `RealmsCampaign.Current?.AddTributeIncome/AddTributeExpenses`; caller comment corrected.
- `Main/Features/Realms/RealmsCampaign.Tribute.cs`: `AddTributeIncome`, `AddTributeExpenses`, `AddTributeLine` and the
  two labels, unchanged in logic; `TributeFrom` and `TributeTo` now private.
- `TAOM.Tests/Features/Realms/TributeFinanceWiringTests.cs`: three per-member count tests.
- `docs/reference/gamemodel-registry.md`, `docs/features/cultural-feats.md`: the rows.

## Verification

- Full C# suite on the branch: exit 0 (`/verify` Step 2).
- Mutant run: removing the gold change's paid line turns `CalculateClanGoldChange_AddsBothTributeLinesOnce` red; the
  restore turns it green.
- No in-game check: the lines and their conditions are unchanged.

## Owed work

- FOLLOW-UP #1130: register the Realms keys and widen the localization gates to `realms_`.
