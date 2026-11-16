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

## Maintainer review (2026-11-16)

Tier 1 (a GameModel, save data): lens 5 (data flow) and lens 2 (engine compatibility) at `max`. Lens 2 found all 21
engine members present on v1.5.7. Codex did not run: it is out of usage.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| M1 | LOW | The bonus ran after the TroopWeight penalty, which saves the limit it reads as the base the party-screen header shows, so the header counted the weight surplus twice and disagreed with the tooltip (lens 5 trace 9) | Order against a second reader | The model comment named only the daily shed as a reader of the saved base | Fixed: the bonus runs just before the penalty, like the other flat bonuses; the enforced limit is unchanged; the order test is flipped |
| M2 | LOW | After a player character change (heir selection, Player Switcher) the old main party, now AI-led, kept the cached +10,000 limit until its roster changed (lens 5 trace 5) | Cache not invalidated on every path | `IsMainParty` can change without a roster change | Fixed: `OnPlayerCharacterChangedEvent` refreshes every party while the switch is on; two new tests |
| M3 | LOW | The doc said the AI reads the player's party "as huge"; the AI reads the fill ratio, so it sees the party as nearly empty (lens 2 F1, lens 5 trace 10) | Wrong engine claim | The speed model sums limits; the AI was assumed to do the same | Fixed: the doc names both effects |
| M4 | LOW | The hint and the doc left out that morale drops when the switch turns off, and "no size limit" covered the prisoner limit too (lens 2 F2, F4) | Incomplete text | The over-limit morale penalty is a separate model | Fixed: "no troop limit" and "morale drops" in the hint (an English key with no translation yet) and the doc |
| M5 | LOW | The 7 new keys had no rows in the 12 language files (lens 5 item 11; the full verify failed `EveryLanguage_DeclaresARowForEveryEnglishKey`) | Seed skipped | The seed is a separate step | Fixed: English rows seeded in all 12 files |
| M6 | LOW | Row 2's stale-cache window after a load is possible, not shown: after a load every cache counter is zero (lens 2 U2) | Unproven claim | The refresh is cheap, so nobody traced a reader | Not applied: the refresh stays. This row records the doubt |
| M7 | LOW | A co-op session that goes live after the load would keep a switch saved on (lens 2 U1) | Timing outside TAOM | Lens 2 did not check the Coop start order | No change: lens 5 read the installed Coop host code, which sets its container before it loads the save, so the session-launch check sees the session |
| M8 | LOW | No round-trip test of `SyncData`; deleting the write-back line would pass every test (lens 5 trace 1) | Test gate scope | The source match pins the call, not the value | Not applied. FOLLOW-UP no issue: add an `IDataStore` round-trip test if this behavior gains more saved fields |
| M9 | LOW | The MCM **Enable Cheat Menu** hint does not say that Unlimited party stays on when the menu is turned off (lens 5 item 13) | Text | The doc says it; the hint predates the switch | Not applied: editing that hint makes 12 translations stale. FOLLOW-UP no issue: change it with the next translation pass |
