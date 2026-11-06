# RCA: Messenger session rules (#898 part 1, deep-review, 2026-10-23)

> **1.4.x port note (plans/131, 2026-11-04).** Ported with `1dc0a8d7c` as `MessengerSessionRulesModule`
> plus one `FeatureModules.All` line (decision D2), keeping the 1.5.x number Patch117. The open-conversation
> flag came from the messenger hunks of `06a1a1b71` (Lord Courtesies, not ported). Tokens of Esteem and
> Lord Courtesies do not exist on 1.4.x, so finding 3 and the troop-gift lines have no 1.4.x counterpart.
> The six targets, `EnterSettlementAction.ApplyForParty` (`:71-75`) and `PlayerEncounter.Finish`
> (`:1082-1084`) were re-read on the installed v1.4.8.

## Top line

`/deep-review` ran seven lenses in two waves on the change that keeps a messenger conversation from moving armies,
enlisting or handing things over at a distance. It confirmed one MED-HIGH, several MEDIUM and about fifteen LOW
findings, all fixed before the commit, plus five behaviour-preserving simplifications. Two findings widened the
scope by the maintainer's standing rule ("proceed with your recommendations"): three more vanilla or TAOM lines that
assume the player stands beside the hero are now hidden through a messenger.

The pattern behind the main findings: the plan listed the dialog lines that move a party, but a messenger
conversation is not only its dialog. Messengers itself enters the target's settlement, and the engine's settlement
entry has side effects of its own.

## Findings

| # | Sev | Bug | Why missed | Fix |
|---|---|---|---|---|
| 1 | MED-HIGH | For a target in a settlement, Messengers calls `PlayerEncounter.EnterSettlement`; `EnterSettlementAction.ApplyForParty` then attaches a main party that is in an army it does not lead, whose leader is in that settlement, to that army, and `Finish(true)` leaves an attached party in place. | The plan audited dialog consequences only; the messenger's own encounter setup was taken as neutral. | `MessengerEntryPolicy.WouldAttachToArmy` mirrors the engine's condition term for term; in that state the messenger uses the field conversation. |
| 2 | MED | A throw while the messenger conversation opened left the open-conversation flag set for the rest of the session; with the new rules that hid army lines, board games and gifts, and greyed enlisting, in every later in-person conversation. | The flag predated this change and its cost was low; this change raised the cost without revisiting the flag's end paths. | Every failure runs `AbortConversation`, which closes the flag and schedules the normal cleanup. |
| 3 | MED | Tokens of Esteem filtered its gift list to purses through a messenger, but a messenger only ever reaches a lord, and a lord's list never holds a purse: the line always ended in "You carry nothing I can accept", and a hostile lord could still cost relation from across the map. | The filter was written from the gift catalogue, not from which entries a messenger can reach. | The gift line is hidden while a messenger conversation is open, as Lord Courtesies already does for its troop gift. |
| 4 | MED | Three lines that hand things over or move a hero had no presence check: clan member troop and inventory exchange, companion recall, and the gift line. | The issue named only army join, enlisting, board games and troop gifts. | Hidden through a messenger (clan exchange and recall as further Patch117 targets). Wanderer hiring and barter stay on purpose. |
| 5 | MED | No test exercised the link from the module's statics to the patch, so a broken `InitializeStatics` would have brought every hidden line back with a green suite. | The postfix was tested only through `Filter` with explicit arguments. | A wiring test initializes the module and runs the postfix. |
| 6 | MED | The API snapshot (`patch-targets.md`) was not regenerated, so the next engine bump would not have compared the new targets' bodies. | The step is in the snapshot README, not in the build brief's checklist. | Regenerated; `-Check` exits 0. The snapshot records the first `TargetMethods` target only (`tools/snapshot_api_surface.ps1:185`), so `engine_body_diff` compares one of the six bodies; the binding test pins all six names and signatures. |
| L | LOW | An untagged test that builds a `TextObject`; redundant and contradictory test rows; a duplicated reason path; a try/catch around a read that cannot throw; a missing registry "Callers and thread" and "PatchShield" paragraph; stale line citations; a gathering army line described as live (nothing in v1.5.5 leads to it); a wrong comment about the session starter; `tokens-of-esteem.md` not updated. | Hygiene. | Fixed. |

## Why each lens caught what it did

- **Data flow (5)** found the settlement-entry attach, the remaining hand-over lines, the stuck flag and the untested link: it traced the messenger's whole encounter, not only the patched lines.
- **Engine (2)** confirmed every target and the Harmony order, and that the gathering line is unreachable.
- **Standards (1)** found the untagged test and the snapshot drift.
- **Completeness (4)** and **Design (6)** both found the Tokens of Esteem dead end and the module-to-patch gap.
- **Efficiency (3)** found nothing: each postfix reads two statics and a volatile flag, once per conversation step.
- **XML (7)** found nothing: the new key is correct in all 13 files.

## Lesson

`lessons/adapters-taleworlds-api.md`, "A remote conversation is an encounter: audit its setup as well as its lines".
