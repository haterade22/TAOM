TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing an uncommitted change for issue #1105 (Story Missions, slice 4: the story offer) in this working tree, a sibling worktree on branch `feat/story-missions` at HEAD `cb482a5d4`. This is a read-only review: do not edit, stage, build or commit anything.

## Engine version and base (read this first)

- The installed game is Bannerlord v1.5.7 and the worktree pins v1.5.7 (`.claude/pinned-game-version.txt`). Read engine code from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file in its v1.5.7 cache.
- Trunk (`bannerlord-1.5.x`) is 60 commits past this base, at `fc8658e1b`; `cb482a5d4` is its ancestor. Trunk will be merged into this branch before the commit. Some trunk commits touch `Main/Features/TaomSettings.cs`, `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `taom_module_strings.xml` and the pinned settings counts, so do not report those counts (548 total, 516 in `TaomSettings`, 355 relevant).

## The report and the maintainer's decisions

A player report: the War Chronicle (#765) event "The Battle of the Hornburg" opens a 20 day window on Helm's Deep (`town_V2`) and then always ends with "No assault comes to Helm's Deep", because no AI army besieged it inside the window. The chronicle only steers lords toward the target; it spawns no battle. Story Missions slice 1 (on trunk) can host the Helm's Deep defence as a campaign mission (`taom.story_open helms_deep_defend`), but no event offered it. The maintainer approved this plan on 2026-11-13:

1. The offer rides on the existing siege event: an optional `story` block on a Siege event in `war_chronicle.json`, not a new event kind.
2. The map still decides first. A real siege or capture resolves the window as before, and the offer withdraws once the window resolves.
3. A won mission resolves the window Held at once. A lost mission gives the target to the attackers (Isengard first), and the chronicle's existing owner-change path then resolves the window Fell. No verdict (a fault, a stalemate, the player left) leaves the window open.
4. "Let fate decide", or a window that closes with no assault and no mission, resolves Held ("as in the book") with its own text, for story-bearing events only. A `fate` of `Lapsed` in the block keeps the old ending.
5. A declined offer is saved.
6. No offer in co-op, while enlisted, as a prisoner, or with a new MCM switch off.

Do not re-litigate these six decisions.

## Implementer's design choices (open to challenge)

- The saved field is a bool `sd` on the chronicle row (the plan named an offer-state enum `o`). Only the decline is saved; the snooze and the pending offer are session state.
- The chronicle exposes `IChronicleStoryPort` (`GetStoryOffers`, `DeclineStory`, `ResolveStoryWon`). `ChronicleService` implements it, and the container maps the interface to the same singleton (`WarChronicleIoC.cs`).
- A Lost verdict goes through `IStoryCampaignAdapter.GiveSettlement`, which calls `ChangeOwnerOfSettlementAction.ApplyByDefault(ruler, settlement)`, never `ApplyBySiege`: BySiege reads `capturerHero.PartyBelongedTo.Party`, which throws for a ruler with no party, and it opens a fief claim. The adapter then empties the old garrison roster with `TroopRoster.Clear()` and sets `Town.LastCapturedBy` to the ruler's clan. The ruler is the leader of the ruling clan of the first kingdom in the window's `attackers` list that exists, is not eliminated and has a living leader.
- Before the transfer, `StoryOfferService.Apply` checks that `GetStoryOffers` still lists the window. `GetStoryOffers` lists nothing at War Ended, and `ResolveStoryWon` runs inside `ChronicleService.Guarded`, which skips every open window at War Ended.
- The entry point `StoryOfferBehavior` checks every 30 `CampaignEvents.TickEvent` ticks on a free map (`MapPopupGate.IsMapFree`). It shows a two-button inquiry, then opens the game menu `taom_story_offer`. Its "Defend the walls" option is `LeaveType.Mission` and calls `StoryMissionLauncher.Launch(missionId, false, onVerdict)`, which opens the slice 1 mission with `MissionState.OpenNew`. `StoryMissionLogic` reports the verdict once through that callback. The engine is expected to return to the menu after the mission, and the "Continue" click applies the verdict.
- A popup that closes with no answer, a failed launch, a failed transfer and an undecided verdict snooze the offer until the next daily tick.
- The MCM switch `WarChronicleStoryMissions` ("Offer Story Missions", default true) is excluded from the co-op settings fingerprint, because no offer opens in co-op.
- A retreat from the defence (the slice 1 verdict for a defender who leaves) counts as Lost.

## Files in scope

Show a modified file's change with `git diff HEAD -- <path>`; read a new file whole.

New:
- `Main/Features/StoryMissions/StoryOfferService.cs`, `Main/Features/StoryMissions/Hooks/StoryOfferBehavior.cs`
- `Main/Features/WarChronicle/Events/IChronicleStoryPort.cs`
- `Main/Adapters/IStoryCampaignAdapter.cs`, `Main/Adapters/StoryCampaignAdapter.cs`
- `TAOM.Tests/Features/StoryMissions/StoryOfferServiceTests.cs`, `TAOM.Tests/Features/StoryMissions/ShippedStoryOffersTests.cs`, `TAOM.Tests/Features/WarChronicle/Events/ChronicleStoryTests.cs`

Modified:
- `Main/Features/WarChronicle/Events/ChronicleService.cs`, `ChronicleDefinitions.cs`, `ChronicleEventRecord.cs`, `ChronicleConfigValidator.cs`
- `Main/Features/WarChronicle/WarChronicleSaveCodec.cs`, `WarChronicleIoC.cs`, `IWarChronicleSettingsProvider.cs`, `WarChronicleSettingsProvider.cs`
- `Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs`, `Main/Features/StoryMissions/Hooks/StoryMissionLogic.cs`, `Main/Features/StoryMissions/StoryMissionsModule.cs`
- `Main/Features/TaomSettings.cs` (the `WarChronicleStoryMissions` block only), `Main/Features/CoopInterop/CoopSettingsRelevance.cs` (one line)
- `Main/_Module/ModuleData/war_chronicle/war_chronicle.json` (the `hornburg` row), `Main/_Module/ModuleData/taom_wotr_strings.xml` (one row), `Main/_Module/ModuleData/taom_module_strings.xml` (the new `taom_story_offer_*` and `taom_wc_mcm_story*` rows)
- Tests: `TAOM.Tests/Features/WarChronicle/Events/ChronicleConfigValidatorTests.cs`, `ChronicleFixture.cs`, `TAOM.Tests/Features/WarChronicle/WarChronicleSaveCodecTests.cs`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs`, `TAOM.Tests/Features/StoryMissions/StoryMissionWiringPinTests.cs`, `StoryMissionsWiringTests.cs`
- `tools/ingame_checks/1105.toml`
- Docs: `docs/features/story-missions.md` ("The story offer (slice 4)" holds the owner-change listener audit), `docs/features/war-chronicle.md`, `docs/features/coop-interop.md`, `docs/features/bannerlord-together-compat.md`, `docs/reference/feature-map.md`

Context outside the diff: `Main/Features/StoryMissions/Hooks/StoryDevMenuBehavior.cs` (the slice 1 menu that opens the same mission), `StoryEndWatch.cs`, `Domain/StoryVerdict.cs`, `Config/StoryMissionCatalog.cs`, `StoryMission.cs`, `HostedRunOutcomeLog.cs`; `Main/Features/WarChronicle/Events/WarChronicleEventsBehavior.cs` (the owner-change and siege listeners), `WarChronicleBehavior.cs`, `WarChronicleDailyGate.cs`; `Main/Adapters/MapPopupGate.cs`; `Main/Core/Logging/OutcomeLog.cs`; every TAOM `OnSettlementOwnerChanged` listener (`grep -rn OnSettlementOwnerChanged Main`).

## Known suspects: CONFIRM or DISPUTE each, with file:line evidence (v1.5.7 decompile for engine claims)

1. **Return to the menu.** After a mission opened by `MissionState.OpenNew` from a `LeaveType.Mission` menu option, with no encounter, does v1.5.7 return to the game menu `taom_story_offer` and run its init again, so that "Continue" shows with the verdict? Or does it land on the map with no menu, so the verdict is never applied? What does slice 1's `StoryDevMenuBehavior` show about this path?
2. **Save and load inside the menu.** A save made while `taom_story_offer` is open: does the load reopen it? The behavior is built new, so the offer is null and the text reads "The moment has passed." with only "Continue". Is any path stuck, or can the player save during the mission?
3. **The transfer.** `ApplyByDefault` from a menu click with time stopped: check every vanilla and TAOM `OnSettlementOwnerChanged` listener against the audit in `docs/features/story-missions.md`. Is the audit complete and right? What happens to lords and parties inside, notables, the player's fief claims, the player's party if it is inside, a siege camp around it, a pending kingdom decision about the fief? Does the event reach the chronicle's `OnTargetOwnerChanged`, so that the window resolves Fell?
4. **The garrison.** Is `TroopRoster.Clear()` on `Town.GarrisonParty.MemberRoster` safe in v1.5.7: a hero in the roster, the party's wage and limit caches, a `MobileParty` left with no members, AI garrison recruitment afterwards? Is `DestroyPartyAction`, or leaving the garrison as it is, better?
5. **`Town.LastCapturedBy`.** Who reads it in v1.5.7 and in TAOM (fief granting, culture conversion, AI scoring)? Is setting it to the ruler's clan right for a capture with no siege?
6. **The ruler.** Can `Kingdom.RulingClan.Leader` be a prisoner, a child or the player? Is `ApplyByDefault` safe for a ruler who is a prisoner? What if the first attacker kingdom is not at war with the owner while the window is Active?
7. **Results on closed windows.** Apart from War Ended, is there any path where a story result lands on a window that is already resolved or skipped, or where one window resolves twice (an owner change fires twice per conquest)?
8. **Fate Held.** A window that closes with no assault now pays the Held outcome (volunteers, momentum) instead of Lapsed. Does anything downstream (later events' requirements, the Ents event, the war ledger, the status and console text) assume that a Held window was assaulted?
9. **The popup and the announcement.** The window's opening announcement and the offer popup both wait for a free map. Can the offer show before the announcement that explains it? Can the popup open during another popup, a conversation, a siege, a map event, or a settlement menu?
10. **The tick and the gates.** Does `CampaignEvents.TickEvent` run while time is paused on the map? Is `MapPopupGate.IsMapFree` enough to keep the inquiry off a game menu, an encounter or the escape menu? The gates in `StoryOfferService.NextOffer` are single-player authority, not enlisted and not a prisoner. Which other player states should refuse an offer (in an army led by another lord, besieging, in a map event, at sea)?
11. **Session reset.** `StoryOfferService` is a singleton and `StoryOfferBehavior`'s constructor resets it. Is the behavior built new for every campaign? Can a pending `_offer` or `_verdict` survive into a second campaign in the same game session?
12. **Save codec.** `sd` is written only when true and read only when it is a JSON boolean. Check old saves, a save with `sd` on an event that has no story block, and a story block removed in a later version.
13. **Config validation.** `ChronicleConfigValidator.ReadStory`: a block on a non-Siege event, an unknown mission id (only a test against the shipped files checks it), a `fate` other than Held or Lapsed, a missing `fateText`.
14. **The in-game manifest.** Does `tools/ingame_checks/1105.toml` match the lines that `OutcomeLog` writes for `Pass` and `Fail` (read `Main/Core/Logging/OutcomeLog.cs` for the detail format)? The taker pattern accepts `isengard` or `empire` (the Dunland kingdom id).
15. **Tests.** Which decision paths in `StoryOfferService`, the chronicle's story methods and `StoryCampaignAdapter` have no test that would fail if the behaviour broke? Is any test a tautology?

## What NOT to flag

- Files outside the lists above: other sessions own them.
- The six maintainer decisions.
- The missing rows for the new keys in the 12 files under `Main/_Module/ModuleData/Languages/`: the project lead's language pass adds them, and `EveryLanguage_DeclaresARowForEveryEnglishKey` is red for them by design.
- The settings counts (see "Engine version and base").
- Slice 1 code this change does not touch, except where this change depends on it.

## Output

For each suspect: CONFIRMED, DISPUTED or UNVERIFIED, with file:line evidence. Then any other defect you find, ranked CRITICAL, HIGH, MEDIUM or LOW, each with the concrete inputs or state that make it fail.
