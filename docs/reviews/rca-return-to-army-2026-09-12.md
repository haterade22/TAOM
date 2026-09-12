# RCA: Return to Army (Patch87, #566), deep review 2026-09-12

**Summary.** Five review agents over a one-prefix feature. Zero HIGH, zero MEDIUM. Two LOW items
were acted on in the same session, and one observation turned into a paragraph of documentation.
Nothing changes what the player sees. This file exists because the review gate applies to every
confirmed finding, not only the loud ones, and because the one real finding has a general shape.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The "point of no return" comment and the second `try` began at the gate-position write, so a throw on that one statement returned `false` (skipping vanilla) although nothing vanilla reads had changed yet. Vanilla would still have been safe to run there. Reported by the data-flow agent (trace 9). | Harmony error path | The block was drawn around "the leave sequence" as a unit, copying vanilla's five statements as one thing, instead of asking of each statement whether vanilla's preconditions survive it. Only `LeaveSettlement` breaks one (`CurrentSettlement` nulled, `LeaveSettlementAction.cs:27`); the position write breaks none. | Moved the position write into the deferrable block and put the boundary on `LeaveSettlement`. Lesson appended to `docs/reviews/lessons/harmony-il.md`: place the no-return line at the first statement that breaks a vanilla precondition, per statement, never at the top of the block. |
| 2 | LOW | `main` and `settlement` were declared non-nullable while assigned from `?.` chains. Reported by the standards agent as a nullable violation. | Style | The build emits no warning here (engine types are nullable-oblivious, so the compiler stays silent), which is exactly why nobody noticed: the declarations read as ordinary and compiled clean. The agent's claim of a "type mismatch" was not confirmed; a rebuild with incremental compilation off produced zero diagnostics on the file. | Declared both as nullable anyway, since the null check two lines later is the whole point of the locals and the annotation says so. Recorded here as style, not defect. No rule change. |
| 3 | note | `PlayerEncounter.Finish` forces `TimeControlMode = Stop` only for a party with no army (:1015), so for this patch's target it does not. Reported by the compatibility agent as a behaviour to document. | Documentation | Not a defect once traced: `Finish` calls `GameMenu.ExitToLast` while the menu is up (:1019-1021) and that sets Stop unconditionally (`GameMenu.cs:376-378`), so the map is paused after the leave exactly as after vanilla's own Leave. The patch comment had not said how time ends up stopped, so a reader could only take it on faith. | One paragraph in the patch comment and in `docs/features/return-to-army.md` naming both sites, plus the note that the time-control lock is set only around the character portrait popup (`CampaignEvents.cs:2230-2242`). |

## Root-cause pattern

Only finding 1 has one. The two error-path lessons already on file ("fall through to vanilla on
error is only safe when vanilla is a safe default at THAT call site" and "a mutation performed
before a notification must not be undone by the notification throwing") both say *whether* to defer.
Neither says *where* the line goes when a block has several statements and only one of them breaks
a vanilla precondition. The session drew the line at the top of the block, which is the conservative
direction for crashes (skip vanilla more often, never less) and the wrong direction for diagnosis: a
statement that cannot break vanilla was logged as "vanilla skipped, its body would now dereference a
null", which is false for that statement and would send the next reader hunting for a null that was
never there.

## Why each agent missed or caught it

- **Standards (Agent 1):** not in its rule set; it reads for ADRs and declarations, which is how it
  produced finding 2 and not finding 1.
- **Compatibility (Agent 2):** verified every engine member and every cited line, and read `Finish`
  closely enough to produce note 3. It read the error path as control flow, not as a per-statement
  precondition question.
- **Efficiency (Agent 3):** out of scope by design.
- **Completeness (Agent 4):** out of scope by design.
- **Data flow (Agent 5):** caught it, because trace 9 asked the per-statement question directly
  ("if the position write throws, is vanilla still safe?"). That is the question the new lesson
  makes standing.

## Feedback memories to codify

None beyond the one lesson entry. Finding 2 is a style preference the compiler does not enforce
for oblivious engine types; finding 3 is documentation.

## Verification after the fixes

Build clean with incremental compilation off, no diagnostics on the new files. Filtered and full
test runs recorded in the CHANGELOG entry for this change.

## Codex pass (review 98, GPT-6-Astra at ultra, after the fixes above)

Prompt: `docs/reviews/codex-adversarial-return-to-army-2026-09-12.prompt.md`. Raw transcript:
`docs/reviews/raw/codex-adversarial-return-to-army-2026-09-12.md` (653 KB, gitignored). Eight
Known Suspects handed over; Codex answered all eight from the installed v1.4.8 DLLs and the
installed CoopNightly build, rebuilt and ran the 17 scoped tests itself (17 green), and reported no
P1 or P2. Two P3 observations.

| # | Codex | Mine | Agree | Reason |
|---|---|---|---|---|
| C1 | P3 | P3 | yes | The co-op veto rationale said replicated fields mean "peers agree on it". CoopNightly applies army membership through `GameThread.RunSafe(blocking: false)` and registers `AttachedTo` and `CurrentSettlement` as separately synced properties, so replication exists but equal snapshots at an arbitrary callback are not established. The honest basis is that the prefix runs the same `LeaveSettlement` + `Finish` calls vanilla Leave runs, reaching the co-op mod's `LeaveSettlementAction` interception the same way. Reworded; two-peer execution marked UNVERIFIED. |
| C2 | P3 | P3 | yes | The vanilla drift guard pins call names, not branches. Codex ran `IlCallScanner` over a synthetic body with `IsVillage \|\| IsCastle` and it passed. Already acknowledged in the test comment; the double-leave consequence the prompt suggested is wrong (the prefix returns false, vanilla never runs on that row), the real risk is silently suppressing a new vanilla behaviour. A body fingerprint was declined (it would fail on every engine bump for reasons unrelated to this patch); the manual re-read at each bump is now stated in the registry and the feature doc. |

Suspects worth keeping: S1 (no menu re-opens after the leave; `SetMoveModeHold` clears the
short-term target so `Army.Tick` cannot merge the party) DISPUTED with line citations; S2 (attached
member whose leader left) DISPUTED, because the leader's `CurrentSettlement` setter propagates null
to every attached party (`MobileParty.cs:609-611`), which the in-house data-flow trace had missed;
S4 (time) DISPUTED in single-player, with the new fact that CoopNightly patches the game-menu time
writes out; S5 (castle) DISPUTED, the castle's own Leave uses the identical callback; S6 (other TAOM
writers of `Army`/`AttachedTo`) DISPUTED as an incompatibility, with the enlistment "Army is null
outside a battle" claim qualified to the successful non-leader steady state.

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| C1 | Co-op rationale overstated | Doc accuracy / over-claiming | "Replicated" was read as "agreed", which is the tempting shortcut whenever a field is on a sync list; nobody opened the co-op mod's handler to see it applies membership asynchronously. | Reworded to the same-integration argument. Lesson for the next veto entry: the safe claim is "runs the same engine calls vanilla runs, so it meets the co-op mod at the same interception", never "peers agree". |
| C2 | Drift guard pins calls, not branches | Test coverage claim | The limitation was written into the test comment and then the prompt still offered a double-leave hypothesis built on the opposite assumption. | The engine-bump re-read is now in the registry entry and the feature doc, where the person doing the bump will read it. |

Codex cost: 122,509 tokens. Zero false positives. Two things it did that the five in-house agents
did not: it opened the installed co-op mod's patches to test a rationale about co-op, and it ran the
repo's own IL scanner against synthetic bodies to prove a coverage claim rather than assert it.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/return-to-army.md](../features/return-to-army.md)
- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
