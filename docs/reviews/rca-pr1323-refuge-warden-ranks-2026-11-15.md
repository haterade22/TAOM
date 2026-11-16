# RCA: review of PR #1323, a promoted warden returns to the ranks

PR #1323 (REDACTEDSTUFF, head 1b755bdbe) changes the refuge dismantle: a promoted warden (a soldier the founding turned
into a companion) is removed with `KillCharacterAction.ApplyByRemove` and one of his source troop rejoins the party,
unless he has a spouse, children, a clan to lead, or is no longer the player's companion. A warden who was a companion
before stays one. The owner asked for this on 2026-11-15, because found, promote, dismantle gave a free companion every
time. The review ran at tier 1 (an engine kill action, save fields 107 and 108): lens 5 (data flow) and lens 2 (engine
compatibility), each at the agent definition's `max`. Codex did not run: it is out of usage (Mike, 2026-11-15).

Lens 2 found all 23 TaleWorlds members present on v1.5.7 with matching signatures. Lens 5 traced both callers of
`ReleaseWarden`, the two save fields end to end, the failed-founding path and the double-release guards, and found them
connected.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The code, the interface and the doc said the removal is silent. The engine's `CommentOnCharacterKilledBehavior.OnBeforeHeroKilled` adds a `CharacterKilledLogEntry` whatever `showNotification` says, and that entry is a visible chat notification: "<name> was lost." (lens 2 W1; the session read lines 19 to 30 and line 167) | Engine claim from a parameter name | `showNotification: false` was read as "no message at all"; it only stops the death pop-up | Fixed: the comments and `refuge.md` now name the chat line, and the in-game check expects it. Lesson appended to `adapters-taleworlds-api.md` |
| 2 | LOW | The new key `taom_rf_warden_ranks` had no row in the 12 language files (lens 5 gap 1) | Seed skipped | The branch predates the trunk seed of e4f783717 | Fixed: seeded after the trunk merge. Existing lesson `localization-ui.md`, "Deferring the paid translation never defers the free seed" |
| 3 | LOW | The governor tie could never be true: `AddHeroToPartyAction` removes a governorship before the facts are read (lens 5 item 2, lens 2 D1) | Dead check | The test fakes return fixed facts, so they cannot see the order | Fixed: `IsGovernor` and its test row are deleted (parity: the check never fired). The doc says why a governorship is not a tie |
| 4 | LOW | Comments said the hero "lives on until the event ends", that the call deletes the hero record, that callers never reach it in a map event, and that the order keeps the refuge from being leaderless (lens 2 W2, W3; lens 5 items 3, 5) | Stale or loose wording | The policy changed; the comments kept the old reasons | Fixed: a death mark runs on a later daily tick, the hero stays in the records as dead, the guard checks the refuge's event only, and the order avoids the kill's party-leader branch. `RefugeService`'s "never killed" line and the "no-kill" line in `UnwindPromotion` are updated; the warning text names both callers |
| 5 | LOW | `RemoveMintedCompanion` relies on no caller reaching it while the main party is in a battle or a siege; the dismantle guard checks the refuge's map event (lens 5 item 3) | Guard on the wrong party | No path was found that reaches it | Not applied: a guard for a case nothing reaches needs Mike's word (`simplicity-criterion.md`). FOLLOW-UP no issue: add a main-party event check if a path is found |
| 6 | LOW | `IsHeroWithRefugeParty` matches any refuge, not the one being dismantled (lens 5 item 4) | Identity check too wide | No path was found that moves a warden into a second refuge | Not applied, same reason as row 5. FOLLOW-UP no issue: pass the refuge's party id if a path is found |
| 7 | LOW | No in-game check named the return-to-ranks path (lens 5 item 5) | Owed check missing | The PR text listed it, the doc did not | Fixed: `refuge.md` "Owed" names the steps and the two chat lines |

## Why each lens caught or missed these

- Lens 2 alone found row 1: it followed the kill action's event listeners into SandBox. Lens 5 read only the pop-up branch and reported "no death message", so the two disagreed; the session read the listener and sided with lens 2.
- Both lenses found row 3 on their own.
- Lens 5 alone found rows 2, 5, 6 and 7 by tracing the keys, the guards and the identity checks.

## In-game checks owed

`refuge.md` "Owed": dismantle a refuge with a promoted warden, and once more with a companion warden.
