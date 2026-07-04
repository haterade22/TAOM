# RCA — War of the Ring Momentum port (#327), deep-review 2026-07-03

Port of LOTRAOM's "Momentum" system to TAOM 1.4.6 (commits 0ea7e28d / 3408c2d9 / 5413d476 on `feature/wotr-momentum`). Five-agent `/deep-review` after the three feature commits. All six confirmed findings fixed in-session; three items accepted-with-note (donor parity / self-consistent design). No HIGH deferred.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | Popup registered `GenericCampaignPanelsGameKeyCategory` (window-toggle keys only) but queried `IsHotKeyReleased("Exit")` — the "Exit" key lives in `GenericPanelGameKeyCategory` (singular). Escape-close silently never fired. | Bannerlord API | Ported the category string verbatim from LOTRAOM's `MomentumInterface`, which was close-button-only — the Escape poll is NEW port code, so the donor never exercised the "Exit" key against that category. Two similarly-named categories; the wrong one binds without error. | LESSONS-LEARNED (Localization & UI): "a hotkey-category string is only correct against the specific key you query — verify the queried key id exists in the registered category." TAOM precedent `GauntletFiefManagementScreen` uses the correct pair. |
| 2 | HIGH | `MomentumEnabled` master toggle couldn't retract an already-shown map meter: the only `RemoveMapMeter()` caller (`RefreshMapMeter`) sat *after* `OnDailyTick`'s `if (!MomentumEnabled) return`, and the item VM's `IsIndicatorVisible` never folded the master toggle. | GameModel/UI toggle fold | Same class as CombatMechanics 2026-07-02 (`GetHorseChargePenetration` unfolded). The retract path existed but was unreachable behind the very guard the toggle trips; the visibility poll checked `ShowMapMeter` but not `MomentumEnabled`. | Fixed: `RefreshMapMeter()` runs before the enabled-guard; `IsIndicatorVisible` folds `MomentumEnabled`. LESSONS-LEARNED (GameModels & Services) already has the master-toggle-fold rule; this is a UI-layer instance — extend the mental model to "the code path that DISABLES must not sit behind the disable guard." |
| 3 | MED | `MomentumIndicatorMapView.OnFinalize` removed the layer without `ResetInputRestrictions()` (paired with the `SetInputRestrictions` in `CreateLayout`). | Standards (gui-ui) | Followed LOTRAOM's teardown verbatim; the donor's `GauntletView.OnFinalize` also omitted it (1.2.x was more forgiving). The gui-ui rule pairs Set/Reset. | Fixed. Rule already exists in `gui-ui.md` "Custom GauntletLayer Input Wiring"; port-from-donor didn't re-audit teardown against it. |
| 4 | MED | `KingdomStrengthAdapter.GetTotalStrength` + `MomentumPopupVM.ResolveKingdom` used `Kingdom.All.FirstOrDefault` linear scan; strength adapter is called per enrolled kingdom inside per-battle / daily strength scoring (O(kingdoms²) per event). | Efficiency | Copied the existing TAOM idiom (`AllianceAdapter`, `KingdomBannerAdapter`, `SiegeDefenseService` all use `Kingdom.All.FirstOrDefault`) — an established but suboptimal pattern for hot-ish paths. | Fixed via `MBObjectManager.Instance.GetObject<Kingdom>(id)` hash lookup. Not per-frame/per-hit, so severity is really MED not HIGH; other TAOM adapters share the pattern but on cold paths. |
| 5 | LOW | `Enum.GetValues(typeof(MomentumActionType))` allocated an array per popup rebuild. | Efficiency (GC) | Popup-open-only; trivial. | Fixed: cached `static readonly` array. |
| 6 | MED | Elimination-victory path (`OnKingdomDestroyed`) didn't call `RefreshMapMeter()`, so after a side is wiped out the meter's MapView stayed registered (hidden only by the 1s poll) instead of being removed — unlike the daily-tick victory path. | Data flow (lifecycle) | The two victory entry points (daily tick, kingdom-destroyed) diverged: only one carried the meter-removal call. Parallel-path inconsistency. | Fixed: added `RefreshMapMeter()` to the elimination branch. |
| 7 | — | Dead `softCapDivisor` config + `DisplayMomentum` query property + `GetDisplayMomentum` tanh domain method — zero consumers (slider uses linear `SliderValue`, popup uses raw `InternalMomentum`). | Simplicity / dead code | Ported the donor's tanh soft-cap faithfully, but the donor never wired it into its UI either (its slider + popup used the same linear/raw values the port does). Carried forward as dead-with-validation-and-tests. | Deleted per `simplicity-criterion.md` (dead code that holds parity → remove): config field + validation + domain method + query property + 8 tests. |

## Root-cause pattern: faithful-port re-audit gap

Findings 1, 3, and 7 share one root: **a faithful port inherits the donor's latent defects and dead code.** LOTRAOM's `MomentumInterface` was close-button-only (so its wrong hotkey category never mattered), its `GauntletView.OnFinalize` skipped `ResetInputRestrictions` (1.2.x tolerated it), and its tanh soft-cap was UI-dead. Porting verbatim carried all three into 1.4.6 where #1 and #3 became real defects. This mirrors `feedback_native_port_hot_path_audit.md` (C++ ports inherit unaudited logging) — "the donor worked" does not mean "the port is fit to ship." The `/deep-review` agents caught all three because they audit against TAOM's *current* rules, not the donor's behavior — which is exactly their value on a port.

Finding 2 is a fresh instance of the master-toggle-fold class (GameModels & Services in LESSONS-LEARNED), first seen in CombatMechanics 2026-07-02 — here in the UI layer, where the disable path sat behind the disable guard.

## Why each agent's result

- **Standards** caught #3 (its gui-ui GauntletLayer-input rule fired). Did not catch #1 (a hotkey *string* is a runtime binding, not a standards check) or #2 (routing/reachability, not a standards rule).
- **Compatibility** caught #1 — it decompiled the two categories and confirmed "Exit" is absent from the registered one. This is the API agent's core competency (a silent-never-fires binding).
- **Efficiency** caught #4 and #5.
- **Completeness** confirmed 150→142 tests, IoC, localization — no functional finding (correct; nothing missing except the feature doc, written at close).
- **Data flow** caught #2, #6, and #7 (the dead `softCapDivisor` trace) — the highest-value agent again, per the skill's own note that every prior HIGH in this project was a data-flow gap.

## Accepted, documented (not bugs)

- **Raid / army-gathered don't apply the participation multiplier or victory-gate credit.** Donor parity — LOTRAOM applied both only to battles and sieges. Documented inline on `RaidOutcomeSnapshot` / `ArmyGatheredSnapshot` (the missing DTO field read as an oversight to the agent precisely because it lacked the comment other deliberate deviations carry).
- **Popup "Total:" (positive = Free ahead) vs map slider (positive = Evil ahead) use opposite sign conventions.** Each is self-consistent within its own layout (the popup puts Gondor on the left, so positive-total = left-ahead matches; the slider fills rightward toward Mordor). No number is shown on the slider, so the two are never numerically contradictory on screen. Left as-is.
- **Same-day FullWar-trigger race:** a battle resolving earlier in the campaign day than the daily enrollment sweep is dropped (war "starts" that tick). Genuine one-tick edge, accepted low-severity.
- **Popup-rebuild allocations (`.ToList()` in `FillSplit`, per-side LINQ):** popup-open-only, imperceptible; not worth the churn per `simplicity-criterion.md`.

## LESSONS-LEARNED entries appended

- Localization & UI: hotkey-category / queried-key verification on ported UI.
- GameModels & Services: master-toggle fold extends to "the disable code path must not sit behind the disable guard" (UI-layer instance).
