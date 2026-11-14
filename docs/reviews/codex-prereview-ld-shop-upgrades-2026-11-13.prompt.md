# Codex pre-review: The Long Defeat, upgrade the soldiers you already have in the shop (#1161)

**Role:** reviewer (`.ai/roles/reviewer.md`). Read only: edit no file, start no game. Run no `dotnet build` or `dotnet test` in this checkout: another process builds and tests it now, and a second build would collide on `bin/` and `obj/`. Report findings; fix nothing.

**Scope:** the whole feature, in two parts, in this checkout (`E:\repos\taom-ld-upgrades`, branch `feat/ld-shop-upgrade-steps`, base `4f77e449c`).

1. The groundwork commit `313400cff` (read it with `git show 313400cff`; it is an ancestor of the base). Its review was waived to land the branch, so this is its first review. It holds the domain rules (`Domain/SurvivalRun.Upgrades.cs`, `ShopPlan.cs`, `Purse.cs`, `RunView.cs`, `RunEffects.cs`, `RunPlanTroops.cs`, `ShopBot.cs`, `LdFort.cs`), the reader (`Config/UpgradesReader.cs`, `ShopReader.cs`), the director partial (`LongDefeatDirector.Upgrades.cs` and the wiring in `LongDefeatDirector.cs`, `LongDefeatDirector.Effects.cs`, `LongDefeatSession.cs`), the adapter partial (`Main/Adapters/LongDefeatBattlefieldAdapter.Upgrades.cs`, `ILongDefeatBattlefieldAdapter.cs`), the check (`Diagnostics/LongDefeatUpgradeChecks.cs`), the report (`LongDefeatRunReport.cs`), tests, and small `tools/` edits.
2. The uncommitted follow-up: `git diff 4f77e449c` and the `??` rows of `git status --short`. It holds the nine Rohan steps in `Main/_Module/ModuleData/long_defeat/shop.json`, the shop rows (`Client/Features/LongDefeat/LongDefeatUpgradeVM.cs`, `LongDefeatHudVM.cs`, `LongDefeatHudText.cs`), both panel movies (`Main/_Module/GUI/PreFabs/FactionUI/TAOMLongDefeatPanel.xml`, `Main/_Module/GUI/PreFabs/LongDefeat/LongDefeatPanel.xml`), 14 English rows in `Main/_Module/ModuleData/taom_module_strings.xml`, tests, the run judge's upgrade rule in `tools/run_long_defeat.py` with its tests, a new manifest `tools/ingame_checks/1161.toml`, and `docs/features/long-defeat.md`.

Until the follow-up, no `shop.json` listed a step, so the groundwork's fade and spawn never ran in play. The follow-up makes it live.

## The request and the maintainer decisions

Players asked to upgrade the soldiers they already have in the shop, one step at a time up their troop line. The maintainer filed #1161 and asked for it. Translations of the new keys go to the backlog (#1129); the two language seeding tests are red for the 14 keys by the project's rule, and that is not a finding. Nothing else about the design is a maintainer decision.

## What the change does (the builder's design, open to challenge)

- `shop.json` steps are `{from, to, price, name}`, the price for one soldier; the step id is `from>to`. The Rohan steps climb from the `fow_*` copies (which have no upgrade targets) into the campaign troops of `troops/troops_rohan.xml`.
- Between waves, `LongDefeatDirector.TickStanding` reads the standing defenders by troop (`StandingDefendersByTroop`) on the first calm tick, every 0.5 s, and on any tick with defender batches or upgrades. It runs after `Apply(fx)` in the same tick (`LongDefeatDirector.cs`, the main tick).
- `SurvivalRun.TryUpgrade` takes every standing soldier of the step's troop that no waiting order holds and that the gold pays for, spends at once, and emits an `UpgradeOrder` on the next tick. Upgrades take no boon price factor.
- `LongDefeatDirector.ApplyUpgrades` runs inside `Apply`, after the defender batches and before field care. `LongDefeatBattlefieldAdapter.ReplaceDefenders` lists the soldiers first, then for each one reads his health share, calls `agent.FadeOut(true, false)`, and calls the adapter's existing `Spawn` with an `LdPlace` at his position and look direction in his formation class, then `ScaleHealth` and the share. `Upgradable()` excludes riders, heroes, the player, the lord's body, an agent using a game object, and the tower posts class.
- `CompleteUpgrade` books one `upgrade` purchase with its `Count` and refunds the gold of the rest. The `[Check] id=taom.long-defeat.upgrade` line passes when `replaced` equals `min(ordered, found)` and `read` equals `replaced`.
- The shop panel lists a row for each step whose troop stands, with how many stand and how many the gold pays for; a click buys the step. The bot buys the first affordable row after its packs and fortifications.

## Evidence (read it yourself before you rely on it)

- Engine sources for the installed v1.5.7 DLLs: `C:\Users\mikew\.taom-src\v1.5.7-202611101605\` (for example `TaleWorlds.MountAndBlade.Agent.cs`, `...Mission.cs`, `...Team.cs`, `...Formation.cs`, `...AgentMoraleInteractionLogic.cs`). For any other engine type run `pwsh tools/taom-src.ps1 path <Type>`.
- No game run has played this change. No log exists for it.

## Questions to answer (each with file:line evidence, marked observed or inferred)

1. What does `Agent.FadeOut(true, false)` do, managed and native, and when? Which `AgentState` reaches `Mission.OnAgentRemoved` and every `MissionBehavior.OnAgentRemoved`, and on which tick? Is the faded agent still in `Team.ActiveAgents` and `IsActive()` on the same tick, and later? If so, does `TickStanding`'s read right after `ApplyUpgrades` count him, so the shop or the bot sells soldiers who are gone?
2. Who hears that removal, and does any of them count an upgrade as a loss, a death, a rout or a kill? Trace `LongDefeatMissionLogic` (`IsLoss`), `LongDefeatDirector.OnAgentRemoved` and `_field.Classify`, the run's defender counts and casualty totals, the kill feed (#1159), the lord watch, the posts registry, the split, the sally, the agent registry key, vanilla's morale and panic logic, and any other TAOM behaviour that runs in this mission.
3. Is the new soldier equal to a bought soldier of the same troop in everything the run applies at spawn: boons, the endless scale, class or line effects, the formation's current order and arrangement, posts, and any per-agent state that the defender batches path sets and `ReplaceDefenders` does not? Compare the two paths.
4. What do `Spawn`'s arguments in `ReplaceDefenders` mean (`playerSide`, `reinforcement`, `count`, `index`, `forceReinforcement`, `place`)? Does a position on a wall walkway, a stair, a gatehouse or a tower top always give a valid spawn there? Is the look direction a sound facing? Does the new soldier get a horse when the target troop is mounted (the King's Royal Guard and the Riders of Rohan steps), and what decides that?
5. Failure paths: `Spawn` throws or returns an agent that is not active after the fade; `GetObject` returns null; the loop stops partway. Is the gold right, does the check say fail, does the purchase book the right count, and does the director's tick survive?
6. Timing: an order goes out on the tick after its buy. Can that tick be inside a wave (a horn or the bot's horn in the same or the next tick), so a fighting soldier is faded? Does `TryUpgrade`'s `BetweenWaves` match the phase in which `ApplyUpgrades` runs?
7. The domain rules: `ObserveStanding`, `Unsold`, `TryUpgrade`, `CompleteUpgrade`, `SlotOfLine`, the purse sink and refund, the reader's loop and repeat rules. Can any input give a negative count, a double spend, a refund larger than the spend, or a row the buy then refuses?
8. The shop rows: do the binding names in both movies match `LongDefeatHudVM` and `LongDefeatUpgradeVM`? Is the themed movie's `ScrollablePanel` `InnerPanel="OfferClip\ShopRows"` path right for the new wrapper? Is anything unsafe in Gauntlet when the list shrinks to zero or is rebuilt while the cursor is on a row?
9. The data: does each `shop.json` `to` troop load in a Custom Game, with equipment that loads there, in the defender culture, one level up from its `from`? Do the `LongDefeatShippedConfigTests` assertions test what their name says?
10. The tools: do `purchase_problem`, `shipped_upgrades`, `upgrade_purchases` and the judge in `tools/run_long_defeat.py` match what the report writes? Do the regexes in `tools/ingame_checks/1161.toml` match the exact line that `LongDefeatChecks.Record` and the outcome log write?
11. Are the engine claims in the adapter's summary comment, the director's comment and `docs/features/long-defeat.md` "Upgrade the soldiers you have (#1161)" accurate?
12. Anything else that is wrong: a dropped behaviour, a stale comment, a test that does not test what its name says.

## Output

Findings first, ranked by severity (CRITICAL, HIGH, MEDIUM, LOW), each with file:line, the evidence, and observed or inferred. Mark missing engine evidence UNVERIFIED. Then a one-line verdict. No fixes, no patches.
