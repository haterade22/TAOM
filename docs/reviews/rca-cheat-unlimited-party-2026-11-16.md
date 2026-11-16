# RCA: review of the cheat menu's Unlimited party switch (2026-11-16)

## Top-line

Commit 18067e407 ("Unlimited party switch keeps enemy troops") was not pushed before PR #1321 merged (squash
e0e562058), so it goes out on its own branch, `feat/cheat-unlimited-party`. A `/deep-review` at tier 1 (the change
writes save data and a GameModel), lenses 2 (engine compatibility), 5 (data flow) and 7 (player text), found no
CRITICAL or HIGH finding: 2 MEDIUM, 9 LOW. Every finding was re-read against the code and fixed with tests first, except
the optional whole-phrase rework of the on/off label, which goes to haterade as a choice. Codex did not run.

## Findings and root cause table

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `PartyBase.PartySizeLimit` caches the model's result until `MemberRoster.VersionNo` changes, and the switch never bumped it. Turned on, the old limit stayed (the party screen's transfer-all cap, the Over Limit question); turned off, the +10,000 stayed until some roster change (E1, F1) | Engine cache not invalidated | The bonus was added in the model and tested there; nobody asked who caches the model's answer. The same trap was recorded for the Eye of Sauron on 2026-10-14 and not re-read | Fixed: `CheatMenuService.SetUnlimitedParty` calls the existing `IPartySizeCacheInvalidator.InvalidateLedBy` with the main hero after the write (it also refreshes the army leader party's speed cache). Tests: called both ways, not on a co-op refusal. Lesson appended to `lessons/gamemodels-services.md` |
| 2 | LOW | A save loaded with the switch on had the same stale-cache window (E3) | Engine cache not invalidated | As row 1 | Fixed: `ReconcileAtSessionLaunch`, called from `OnSessionLaunched` after the save is read, refreshes the cache when the switch is on. It runs at session launch, not inside `SyncData`, so the world's parties exist and it runs on the main thread |
| 3 | MEDIUM | Only the setter checked co-op, so a single-player save with the switch on kept acting when loaded into a shared session (F2) | Co-op gate on one path only | The gate was put where the player acts, not where the saved state takes effect | Fixed: `ReconcileAtSessionLaunch` turns the switch off where `IsSinglePlayerAuthority` fails, as `BattleCheatService.TickGodMode` does for god mode. Tests for both sides; noted in `coop-interop.md` |
| 4 | LOW | `party == PartyBase.MainParty` reads `Campaign.Current.MainParty.Party`; vanilla nulls the main party for a moment in `OnPlayerCharacterChanged`, and the model ran that read for every party with the switch off (E2) | Null window in an engine getter | The getter's body was not read (IL of v1.5.7: `Campaign.get_Current`, `get_MainParty`, `get_Party`, no null check on the main party) | Fixed: the switch is read first, then `party.MobileParty?.IsMainParty == true`. Source pin in `CheatPartyRulesTests` |
| 5 | LOW | The +10,000 had no tooltip line (E4, F8) | Missing label | `AddResultFrameBonus` takes an optional label and the call left it out | Fixed: "Unlimited party (cheat)", new key `taom_cheat_unlimited_party_bonus` (English row only) |
| 6 | LOW | The label set `{STATE}` through the global `MBTextManager.SetTextVariable` (E5) | Global text variable | Copied from older menu code | Fixed: `args.Text.SetTextVariable`. `GameMenuOption.GetConditionsHold` builds `MenuCallbackArgs` with the option's own `Text` (v1.5.7 IL), so the variable stays on that option. Source pin |
| 7 | LOW | A comment said the engine's agent cap is "about 2,000" in battle, with no source (E6) | Unverified number | | Fixed: the number is gone |
| 8 | LOW | `CheatMenuWiringTests` should count three `Pick` calls (F6) | Wiring test | | Already on trunk: the merged #1321 pins `taom_cheat_opt_relation`, `taom_cheat_troops_any` and `Pick(id, ` and expects 3. No change |
| 9 | LOW | The hint and the on message said enemy troops "do not desert"; low-morale desertion still applies to them (lens 7) | Text claims more than the code does | The text described the alignment rule as if it were all desertion | Fixed: "do not desert because of their culture", the C# literal and the row identical. Source pin |
| 10 | LOW | Docs: the registry row did not list the bonus; alignment desertion did not mention the main-party exemption; the cheat menu doc said screens "never refuse", left out what the switch does not change, that it stays on with the MCM menu switch off, and the army-level side effect (F5, F7, E1 wording) | Docs not updated with the behaviour | | Fixed in `gamemodel-registry.md`, `alignment-desertion.md` and `cheat-menu.md` |
| 11 | LOW | `CheatWorldAdapter`'s by-name read of `PartyScreenHelper.GetRosterWithAllGameTroops` needs a binding-gate row (E7) | Gate bypassed | | Already on trunk from the #1321 review (`ReflectionSiteBindingTests` DataRow, `reflection-sites.md` row). No change |

## Why the review caught these

- Lens 2 (engine) read the `PartySizeLimit` getter and its version key (row 1), the `PartyBase.MainParty` getter (row 4)
  and the text-variable scope (row 6).
- Lens 5 (data flow) followed the saved flag from `SyncData` to its readers and found the load and co-op paths that skip
  the setter (rows 2, 3).
- Lens 7 (text) compared each sentence with the desertion code (row 9).

## Not applied

- The label reads "Unlimited party: {STATE}" with "on" and "off" as separate keys, so a translator cannot reorder the
  whole phrase. A whole-phrase pair of keys would fix that; it is a text design choice, left to haterade in the PR.

## Tests added

In `CheatPartyRulesTests`: `SetUnlimitedParty_RefreshesTheMainPartysCachedSizeLimit` (on and off),
`SetUnlimitedParty_InCoop_RefreshesNothing`, `SessionLaunch_SwitchLoadedOn_InSinglePlayer_StaysOnAndRefreshesTheCache`,
`SessionLaunch_SwitchLoadedOn_InCoop_TurnsItOffAndRefreshesTheCache`, `SessionLaunch_SwitchOff_DoesNothing` (both
sides), `Behavior_ReconcilesTheSwitchAtSessionLaunch_AfterTheSaveIsRead`, `PartySizeModel_NamesTheUnlimitedBonusInTheTooltip`.
In `CheatMenuWiringTests`: `UnlimitedPartyLabel_SetsItsStateOnItsOwnText`,
`UnlimitedPartyTexts_SayEnemyTroopsKeepOnlyTheCultureRule`. With the two service fixes removed, three of them fail
(both cases of the refresh test and the co-op session-launch test).
