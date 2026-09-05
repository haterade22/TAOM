# RCA: dismissing a promoted companion back to the ranks (#540), 2026-09-04

**Feature:** a promoted companion can be sent back to the ranks through a dialogue line or a
settlement-menu picker; the hero is removed with one `KillCharacterAction.ApplyByRemove` and one origin
soldier rejoins the party. Shipped with 51 service tests and 21 binding guards after two review rounds; the FieldCommission
subset is 266 green.

**Why this RCA exists:** the review gate returned two confirmed findings and one refuted HIGH. The two
confirmed ones share a root: the design deliberately lifted a vanilla precondition (the fire line hides
itself inside settlements) and the review, not the design, was what asked what that precondition had
been protecting.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH (symptom unverified in play) | Dismissing from inside a keep or tavern scene removed the `Hero` and its `LocationCharacter` (next entry's spawn list) but nothing touched the live scene `Agent`. The companion would keep standing there, and `MissionConversationLogic.IsThereAgentAction` never checks that the hero is alive, so the player could open a conversation with a removed hero. | Campaign mechanics / vanilla interaction | The plan noted vanilla's line is map-only and called the scene path "the one to watch" in the smoke list, then deferred the question to an in-game run. A file in the repo could not answer it, but the installed SandBox assembly could, in three decompiles. Deferring downgraded a knowable fact to a maybe. Same shape as the #533 keybind lesson ("never defer to a smoke a question a file can answer"). | `RemoveCompanionFromGame` now removes the scene agent first, the engine's own passage mechanism (`Agent.FadeOut` behind the same `Mission.State` guard as `MissionAgentHandler.FadeoutExitingLocationCharacter`; the visible fade shipped in round one, the instant form replaced it in round two, finding 5). Two binding guards pin the members. Lesson appended to `lessons/campaign-mechanics.md`: lifting a vanilla condition is widening a gate, so enumerate what the condition protected before shipping. |
| 2 | LOW to MED | The feature doc claimed "co-op clients see neither entry point", but only the menu option was hidden. The dialogue line ran its whole are-you-sure exchange on a client and then silently did nothing at `ConversationEnded`. | Co-op gating / docs | The dialogue behaviour copied `EnlistmentReleaseDialogBehavior`, whose convention gates the consequence and shows the line. The doc sentence described the intended design rather than the copied code. | `PartnerIsDismissable` now returns false on a non-authority peer, so the doc sentence is true by construction. |
| 3 | HIGH, REFUTED | The efficiency agent rated the settlement-menu condition (`GetDismissableCompanions`, one `Hero.AllAliveHeroes` scan per promoted id) a per-frame hot path. | Efficiency | The agent said in its own report that the refresh frequency was unverified and rated HIGH anyway. Read against the installed DLLs: `GameMenuVM.OnFrameTick` calls `GameMenuItemVM.Refresh`, which only re-reads the cached `GameMenuOption.IsEnabled`; the condition delegate runs from `GameMenuVM.Refresh(bool)`, reached on activate, resume, an explicit `MenuContext.Refresh`, or a menu switch. Per menu open, not per frame. | No code change. Lesson appended to `lessons/localization-ui.md` so the next reviewer does not re-guess the cadence. The rule already in the agent prompt ("an unverified cost claim is reported UNVERIFIED, never HIGH") was not followed; the finding was re-verified rather than acted on, per `evidence-over-claims.md` A. |
| 4 | LOW (docs) | The feature doc, CHANGELOG and issue said "the four vanilla `OnCompanionRemoved` listeners behave identically for Fire and Death". The installed game has eight (five in CampaignSystem, three in SandBox); the round-one correction said seven because it grepped for a handler NAME and `PlayerTrackCompanionBehavior` subscribes under another one, so the round-two count enumerates the subscription call itself. Two differ: the notification listener speaks only for Fire, and `CompanionDismissCampaignBehavior` dereferences `ConversationMission.OneToOneConversationAgent` unguarded for a Fire inside a settlement. | Docs / engine claim | The count came from grepping the decompile dump, which carries no SandBox assemblies; the plan agent, the review prompt and I all repeated it. Same shape as the "grep `_shipping_build`, not the dump root" lesson: the dump is a subset and a count taken from it is a floor, not a total. | Prose corrected in all three places. The conclusion survived (both divergences favour the Death detail; the Fire one is a vanilla NRE on the picker path), which is luck, not method. When a claim is "all N listeners", enumerate from the installed DLLs (`ilspycmd <dll> -l c` per module assembly), never from the dump. |

## Root-cause pattern: lifting a vanilla precondition is widening a gate

Vanilla's `companion_fire_condition` requires `Settlement.CurrentSettlement == null`. The design read that
as an annoyance (it is why the user could not find the option in a town) and removed it. The
castle-recruitment lesson already says what to do when a vanilla gate is widened: trace what the gate
was protecting before enabling the new case. Nobody applied it here because the gate was a conversation
condition, not a settlement-type check, and the widening looked like UX rather than mechanics. What it
protected was a live scene agent that the engine's removal path never reconciles.

The general form: **a condition on a vanilla line is a precondition on everything its consequence
does.** When you register a broader line that ends in the same engine call, list the engine state the
original condition excluded and check each item against the call.

## Why each agent missed or found these

- **Standards:** passed correctly; nothing here is an ADR breach.
- **Compatibility:** verified all 32 engine members against the installed DLLs, confirmed the
  deferral, roster and conversation-event claims, found finding 4 by enumerating listeners in the
  SandBox assembly, and independently reached finding 1 from the Fire-only follow-stop branch in
  `CompanionDismissCampaignBehavior`.
- **Efficiency:** produced finding 3 by guessing at a frequency its own prompt told it to verify. Its
  two MEDIUM items (an O(n) `Contains` on a list bounded by the companion limit, and three hero scans
  per player-driven dismissal) are real but below the simplicity criterion's threshold.
- **Completeness:** passed; it confirmed the 17 keys in all 12 language files and the doc sections.
- **Data flow:** found both confirmed findings. Finding 1 came from following `MakeDead` into
  `LocationComplex.RemoveCharacterIfExists` and asking what happens to the agent, which no per-file read
  asks. Finding 2 came from comparing the doc sentence against every path to `Dismiss`.

## Feedback memories to codify

Two lessons appended, one per category file, both linked above. No new rule file: the widening-gate rule
exists; this is a recurrence in a new shape, which the lesson records.

## Re-review of the two fixes

A focused agent re-read both fixes against the installed DLLs and confirmed them on every axis: the
conversation mode is already switched back when `ConversationEnded` fires (`MissionConversationLogic.OnConversationEnd`
runs from `ConversationManager.ConversationEnd`, dispatched before the campaign event), the scene
agent's `Character` is the hero's own `CharacterObject` (`HeroAgentSpawnCampaignBehavior` builds a
`PartyAgentOrigin` from it), `FadeOut` is the verbatim passage call, and `Mission.Agents` is not
mutated synchronously by it. No HIGH or MED. Three LOW: two binding pins it wanted
(`Hero.CharacterObject`, and `FadeOut`'s two same-typed bools by name), both added; and two convention
notes recorded here rather than acted on: `ICoopSessionProvider.IsAuthority` fails open for
BannerlordTogether peers, which is codebase-wide and by design (`CoopSessionPolicy` documents it), and
the dialogue line hides itself on a non-authority peer while the Enlistment dialogues show theirs and
gate only the consequence. The hide is the better shape for an exchange that ends in an irreversible
act; if the project wants one convention, Enlistment is the side to move.

## Second round (nine agents, after the fixes)

Standards, completeness, compatibility and data flow passed. The rest produced the following.

| # | Sev | Finding | Outcome |
|---|-----|---------|---------|
| 5 | MED | Lifecycle: a visible fade keeps the agent `Active` for its duration, `IsThereAgentAction` never checks `IsFadingOut`, and a click in those frames opens a conversation with a dead, un-clanned hero that the wanderer-hire lines would treat as hireable. | Fixed: `FadeOut(hideInstantly: true, hideMount: true)`, the form vanilla uses for a departing multiplayer peer and a duel challenger. Doc, lesson and CHANGELOG updated. |
| 6 | LOW | Localization: "honour" against the module-wide "honor" and a sibling string in the same file. | Fixed in the literal, the registered row and the 12 seeded rows, before any translation or cache entry existed. |
| 7 | test validity | `Evaluate_AnyVerdict_NeverMutates` let each arrangement leak into the next, so after the first iteration every call answered `NotPromoted` and the test named seven verdicts while exercising one. `AssertNothingMutated` covered six mutators and left the promotion-side calls on the same mocks unchecked. | Fixed: a fresh fixture and an outcome assertion per verdict (the assertion is what exposed the leak); the helper now covers every mutator on every mock; a guard-order pin for the one pair a player can trigger at once; the null promoted-list branch; an explicit stub instead of a mock-default coincidence; drift guards for `Hero.StringId` and `MobileParty.MainParty`. Subset 262 to 266. |
| 8 | LOW (docs) | The listener count was still wrong at seven: `PlayerTrackCompanionBehavior` subscribes under another handler name, so a name grep misses it. The fire line belongs to `CompanionRolesCampaignBehavior`, not `LordConversationsCampaignBehavior`, and the settlement cleanup sits in `KillCharacterAction.ApplyInternal` after `MakeDead`, not inside it. | All three corrected in the feature doc, CHANGELOG, review log, memory and the issue body. The count now comes from enumerating `CompanionRemoved.AddNonSerializedListener` across full decompiles of both assemblies. |
| 9 | refuted | Efficiency rated the menu condition MEDIUM again with the round-one proposal (show the option whenever any promoted id exists, filter on click). | Declined: it would show an option that does nothing when nobody qualifies, to save a few hundred string comparisons per menu open. |
| 10 | refuted | Data flow held that a settlement mission normally stays live while the town menu is shown, so the picker path would also hit the agent removal and the doc was misleading. | `Mission.Current` is cleared in `Mission.OnMissionStateFinalize`, called from `MissionState.OnFinalize` when the scene is left; the settlement menus belong to `MapState`. No mission is live on the picker path. A sentence saying so was added to the feature doc. |

Two convention notes were recorded and not acted on: `ICoopSessionProvider.IsAuthority` fails open for
BannerlordTogether peers, codebase-wide and by design, and the dialogue line hides itself on a
non-authority peer where the Enlistment dialogues show theirs and gate only the consequence.

## Verification after the fixes

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FieldCommission"`:
  266 passed, 0 failed (2026-09-04, after both review rounds).
- The scene path remains an owed in-game smoke: the fade-out is engine-verified in signature and
  precedent, not yet watched in play.
