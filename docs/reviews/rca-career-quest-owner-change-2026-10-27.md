# RCA: career quest cancel on a player hero change, deep review (2026-10-27)

**Trigger:** a player report. A Gondor vassal's career quest "The Defense of the East Bank"
(`captain_of_osgiliath_t2`) showed "Master the One Handed blade 99/100" while the skills screen showed
One Handed 103; the battle and renown objectives showed done and the quest never completed. No issue
yet; filing waits for Mike's word.
**Change:** a career quest whose owner is no longer the main hero is cancelled at session launch and
on the daily tick, and the normal offer then runs for the current hero (Mike's decision: cancel and
offer again).
**Review:** `/deep-review`, risk tier 1 (save state, a campaign tick, a co-op gate). Wave 1: Agent 5
Data flow and Agent 2 Engine compatibility (`deep-reviewer`, Opus 5.5 at max). Codex pre-review at
`xhigh` (prompt `codex-prereview-career-quest-owner-change-2026-10-27.prompt.md`, output in `raw/`),
then a fix re-review: Codex at `high` (`codex-rereview-career-quest-owner-change-2026-10-27.prompt.md`)
and Agent 5.

## Top-line

The mechanism is real and vanilla shows the intended behaviour: `QuestManager.OnPlayerCharacterChanged`
cancels every ongoing quest except a special one, with a reason text, and `CareerQuest` is special
(`SpecialQuestType`, needed so that a load does not cancel it). So after an heir takes over, the former
hero's career quest keeps polling the former hero's skill and can stall for good. The first version of
the fix gated the cancel on co-op module presence, which the repo's co-op rules forbid for a world
mutation (a repeat of the 2026-08-01 class), and it reused the quest StringId, which merged the new
quest into the cancelled quest's journal record. Both are fixed. The review also showed that Player
Switcher changes the hero only in a new campaign's character creation, so mid-campaign only heir
succession produces the stale quest; for the reporting player this cause applies only if they continued
as an heir, which is not yet confirmed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (Codex P2; Agents 5 and 2) | The cancel was gated on `ICoopPresenceProvider.IsCoopActive`, which is true whenever a co-op module is enabled. A solo player with Coop enabled never got the fix, and a dedicated server was protected only by the presence probe. | Co-op predicate (repeat) | I copied presence from the `PlayerPossession` precedent, where it is a documented heir discriminator, and did not read `lessons/campaign-mechanics.md` "Presence is not authority" or the coop-interop "four questions" table before writing a world mutation. | Fix: `CoopSessionPolicy.IsSinglePlayerAuthority`; presence kept only at session launch for the open R1b timing risk; one test per term. Systemic: see "Feedback memories to codify". |
| 2 | MEDIUM (Codex P2; Agent 2 LOW) | The quest offered again reused StringId `"taom_cq_" + def.Id`. `JournalLogsCampaignBehavior.OnQuestStarted` finds the last journal record by StringId (`JournalLogEntry.IsRelatedTo`), so it took over the cancelled quest's record, attributed to the former hero, and flipped it to success on completion. | Engine identity key | The ladder comment and a lessons entry said "the engine keys only map markers by id", and I trusted that claim instead of reading the journal behaviour. | Fix: `CareerQuestService.NewQuestStringId` (definition, owner, campaign milliseconds); the wrong claim corrected in `LadderQuests.cs` and `lessons/state-lifecycle-save.md`. |
| 3 | LOW (Codex P3; Agents 5 and 2) | The docs, interface comment and log said the quest "is offered again" with progress "from zero", and named Player Switcher as a cause. The offer follows the normal rules (the heir's own career, open tier, not declined), threshold objectives start from the heir's values, and Player Switcher never runs mid-campaign. | Claim wider than evidence | I wrote the docs from the investigation's hypothesis before tracing the producers of the state. | Fix: reworded in all three places. Lesson: name every producer of a state before documenting a fix for it. |
| 4 | LOW (Agent 5) | The cancel passed no reason text; vanilla passes one for the same event. | Player feedback | Not compared with vanilla's own cancel. | Fix: vanilla's key and text `{=bYdhYidf}`, which the game already translates. |
| 5 | LOW (Agent 2) | "The engine keys only map markers by id" was wrong in `LadderQuests.cs:21` and `lessons/state-lifecycle-save.md:910`. | Wrong engine claim in a lesson | The claim was never checked against the journal. | Fix: both corrected. |
| 6 | LOW, latent (Agent 5) | `CareerRegistry` matches career ids case-sensitively while the quest lookups ignore case, so a `career_id` differing only in case would be offered but never cancelled. Today's data matches exactly. | Inconsistent comparers | Pre-existing comparer choice. | Not applied (no live case). Follow-up closed 2026-11-04 (Mike: add it when a second such quest is written): a data test that each non-ladder `career_id` resolves exactly; raise with Mike. |

Pre-existing, outside the change:

- **Ladder start id.** `LadderQuests.cs:38` still starts every rung's quest as `"taom_cq_" + def.Id`, so
  two heroes' quests on one rung share a journal record. Follow-up closed 2026-11-04 (32aeb9e10): no issue: filing waits for Mike's word.
- **An old hero's ladder quest stalls (Agent 5 F5).** Kills credit only the current hero's rung quest,
  battles credit every running `CareerQuest`. Follow-up closed 2026-11-04 (Mike, 95a348a60): the daily sweep now cancels it.
- **One-day window (Agent 2).** Between the hero change and the next daily tick, the stale quest still
  counts the heir's battles and can complete for the former hero. Vanilla's immediate hook is
  `OnPlayerCharacterChangedEvent`. Left as is: at most one in-game day, and the sweep must exist anyway
  for saves where the change already happened.

## Fix re-review

Codex at `high` and Agent 5 re-reviewed the fixes. Both found the three fixes sound on every traced path
and no new production defect. Both found the same gap, and one inconsistency remained:

| # | Sev | Finding | Resolution |
|---|---|---|---|
| 7 | MEDIUM (Codex P2; Agent 5 LOW) | The three call sites in `CareerQuestCampaignBehavior` (the quest id, the session-launch flag, the reason text) could revert with every test green: the service tests do not reach an entry point. Codex proved it with in-memory mutation runs. | Fix: `CareerQuestWiringTests` pins each call site from source text. A mutation run that reverted the id and the flag failed two of its three tests; the restored source passes all three. |
| 8 | LOW (Agent 5) | `LadderQuests.cs`, `LordsLadderService.cs` and the ladder entry in `lessons/state-lifecycle-save.md` still said a Player Switcher change produces another hero's quest mid-campaign. | Fix: all three say "after an heir took over"; the lesson carries a correction note. |

**Process note.** The first version of this change, including this RCA's first draft and both Codex
prompts, was committed and pushed inside `6ae924638` by another session while the fix re-review was
still running, together with about 70 unrelated files, under a subject without the version label. The
review findings 7 and 8 land in a follow-up commit.

## Root-cause pattern

Findings 1, 2 and 5 share one cause: I reused a nearby precedent or comment as evidence instead of
reading the authority for the question (the co-op predicate table, the engine's journal code). Finding 3
is the same habit applied to my own investigation: a hypothesis became doc text before its producer was
traced.

## Why each agent missed these

- **Agent 5 Data flow** found 1, 3, 4 and 6; it did not find 2 because it traced the quest list and
  listeners, not the journal behaviour keyed by StringId.
- **Agent 2 Engine compatibility** found 1, 2, 3 and 5.
- **Codex** found 1, 2 and 3 with in-memory probes against the installed DLLs.

## Feedback memories to codify

- **A machine check for presence gates.** The "Presence is not authority" lesson exists and was not read;
  a rule that relies on a read has now missed twice. A gate test that fails a new
  `ICoopPresenceProvider.IsCoopActive` read in a service outside an allowlist (PatchShield install, UI
  registration, `PlayerPossessionService`, this session-launch term) would catch it at build time.
  Follow-up closed 2026-11-04 (53febe1a1): no issue: proposing the gate to Mike first.
- Lesson appended to `lessons/campaign-mechanics.md` (presence gate, repeat) and
  `lessons/state-lifecycle-save.md` (quest StringId keys the journal).
