# Codex pre-review: check for the join hand-off at the player switch (#1177)

You are an independent reviewer. Read only; change no file. Report findings with file:line evidence, severity (CRITICAL, HIGH, MEDIUM, LOW) and a failure scenario. Missing engine or Coop evidence stays UNVERIFIED. Return the report as your final message.

## Scope

The uncommitted change in `E:\repos\taom-coop-rules` (branch `fix/1177-possession-at-switch` from trunk `4b7a84af4`; `git diff` plus one untracked test file):

- `Main/Features/PlayerPossession/PlayerPossessionBehavior.cs`: `RegisterEvents` adds `CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, OnPlayerCharacterChanged)`. The new `OnPlayerCharacterChanged` calls the existing `CheckForPossession()` inside a `try`/`catch (Exception)` that logs. The hourly listener stays.
- `TAOM.Tests/Features/PlayerPossession/PlayerPossessionSwitchTests.cs` (new): a source pin for both listeners, and a test that the handler logs and returns when the check throws (no game runs, so `Hero.MainHero` throws).
- `docs/features/player-possession.md`: a paragraph on when the check runs, the diagram, the tests list, the in-game check.

Intent: a co-op client's character-creation package (race, startup gold, career, special-resource seed) is re-applied to the hero it plays only on the next `HourlyTickEvent`. A joining client's clock can stand still for minutes, so the first battle can start without the career (BannerlordCoop #4665 logs: career assigned 16:04:43, Coop `Player switch after` 16:05:36, `Mission started ... hasCareer=False` 16:17:51, possession re-apply 16:18:05).

Facts read for this change (verify them; do not trust them):
- v1.5.7 `ChangePlayerCharacterAction.Apply` raises `CampaignEventDispatcher.Instance.OnPlayerCharacterChanged(mainHero, hero, MobileParty.MainParty, isMainPartyChanged)` (`ChangePlayerCharacterAction.cs:53`).
- v1.5.7 `Campaign.Tick` dispatches `TickEvent` and the periodic events only when `_dt > 0f || CurrentTickCount < 3` (`Campaign.cs:978-989`).
- Coop `bannerlord-1.5.x` 995517e60, `source/GameInterface/Services/Heroes/Interfaces/HeroInterface.cs:183-255` (`SwitchToPlayer`): sets `Game.Current.PlayerTroop`, then calls `ChangePlayerCharacterAction.Apply(playerHero)` between two `SuppressForPlayerSwitch` flags, then does captivity, camera, fog of war and publishes `SwitchedPlayer`.
- Coop `LeaveSettlementActionPatches.cs:17-20` says the client is in `LoadingState` at the switch, where `ClientSyncPolicy.AllowOriginal` returns true (`Coop.Core/Client/Policies/ClientSyncPolicy.cs:27-31`).
- Coop's AutoSync setter prefix (`GameInterface/AutoSync/Templates/Patches/PropertySetPrefixTemplate.txt`) is `void`; `BasicCharacterObject.Race` is registered (`Services/BasicCharacterObjects/BasicCharacterObjectSync.cs:35`). Coop's `GiveGoldActionPatches` prefix on `ApplyInternal` returns `ModInformation.IsServer`.

## Questions

1. On a Coop client, does the event reach TAOM's handler at the switch with `Hero.MainHero` already the joined hero? Is the baseline (`RecordBaselineHero` on `OnGameLoaded` and `OnSessionLaunched`) recorded before the switch, so `TryConsumePossession` returns true there? If not, does the hourly fallback still work as before?
2. Is any grant in `JoinReconciliationService.ReapplyCharacterCreationPackage` unsafe or different when it runs inside `SwitchToPlayer` (before Coop's captivity, camera and fog-of-war work, under `LoadingState`), compared with the hourly tick in campaign state? Check race, gold, career (`ICareerCreationHandler.OnCareerSelected`) and the special-resource seed, and `hero.Clan?.Kingdom` at that moment.
3. Every other raiser of the event: single-player heir succession, TAOM's Player Switcher (`HeroSwitchService.cs:116` calls `ChangePlayerCharacterAction.Apply` and then reassigns the clan, moves gold, assigns the career and removes the old hero), Coop's host, other TAOM code. `ICoopPresenceProvider.IsCoopActive` is true whenever the Coop module is loaded, not only in a session. Can the new listener now re-apply the package where the hourly check did not, or in the middle of another feature's hand-over? Order matters: the Player Switcher runs inside TAOM's character-creation finalize loop; `PlayerPossessionBehavior` captures the choices at `OnCharacterCreationIsOver` phase 9.
4. Does the engine's event dispatch already catch a handler's exception? Is the `try`/`catch` needed, and does it hide anything the hourly path would surface?
5. Are the two tests sound: can the guard test pass for the wrong reason, and does the source pin prove the wiring?
6. Is the doc text accurate?

Engine source: the installed DLLs are the truth (`pwsh tools/taom-src.ps1 path <Type>`); the dump at `E:\Decompiled_Bannerlord\_modules_build` and `_shipping_build` is v1.5.7. Coop source: `E:\repos\coop-4593` (bannerlord-1.5.x 995517e60).
