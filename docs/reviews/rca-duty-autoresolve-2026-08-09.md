# RCA — field-duty auto-resolve rework, 2026-08-09

**Change:** `084a3b8c` (rework) + `7c93e0fd` (Rohan glove root cause), fixed by `7c1a1a92` (Codex)
and `20d3344d` (deep review).
**Reviews:** independent Codex pass — **FIX FIRST**, 1×P1 / 2×P2 / 2×P3. Five-agent deep review —
5×MED. **Ten confirmed findings. Zero were caught before commit.**

## Top-line

The rework itself is sound and the design pass that preceded it was good: four agents designed it,
converged on the same residual (`IsEnlisted` includes `EnlistedPlayerCaptive`), and I fixed that
before writing a line. What failed was everything *after* the design.

**I committed C# without running `/deep-review`, which CLAUDE.md marks Mandatory.** Ten findings
followed. The single most expensive was not a logic error — it was that I did not run the gate.

## Findings

| # | Sev | Finding | Why missed | Preventive action |
|---|-----|---------|-----------|-------------------|
| 1 | P1 | Field duties were assigned **silently** — `DailyOfferTick` starts one directly while interactive duties get a popup, so the player gained/lost trust from an order they never saw. | The old model was self-announcing **by accident**: it made the player visible and sent them travelling. Removing the detach removed the announcement with it, and I only tracked what I was deleting, not what the deletion was incidentally providing. | When deleting a mechanic, list what it did *incidentally* as well as deliberately. Ask "what did this side-effect communicate?" |
| 2 | P2 | Offers gated on `IsEnlisted`, so a **prisoner could be offered camp work**. | I fixed the *runtime* captivity guard because four design agents flagged it, and never asked whether the *offer* path had the same predicate. A fix applied at one layer, not swept to its siblings. | When a predicate is found too wide at one call site, grep every other use of it in the same feature before closing. |
| 3 | P2 | An active duty could resolve during the 7-day `CommanderUnavailable` grace — paying out with no company to report to. | Same root as #2. My guard named `EnlistedPlayerCaptive` explicitly instead of asserting the *positive* state (`attached-or-in-battle`), so it enumerated one exception and missed the other. | Prefer positive state requirements over enumerated exceptions. `state != Attached && state != Battle` cannot miss a case; `state == Captive` always can. |
| 4 | P3 | `IDutyWorldAdapter` kept nine dead members incl. `SpawnLooterParty`/`DestroyParty` — "the #375 surface is one method call away." | I deleted the **caller** and reported the surface removed. It was not: the method survived. I checked my own diff rather than the resulting API. | "Removed X" means X is gone, not that its last caller is. Grep the member, not the call site. |
| 5 | P3 | `GetDuties_ZeroDeadline_SkipsRow` passed **for the wrong reason** — it wrote retired keys and omitted `supportSkills`, so the row was skipped by a different rule than the one under test. | A green test after a schema change reads as "still covered". Nobody asks *which rule* made it green. | After changing a validator's field set, re-read every test that exercises it and confirm the row fails for the stated reason. Add a positive control. |
| 6 | MED | **The "shared" constant was not shared.** Two comments asserted `RankBonusPerLevel` was single-source so the two duty systems could not drift; `InteractiveDutyPresenter` still held its own `= 4`. | I *intended* to relocate it, wrote the comment describing the intent, and moved on. The comment described the design, not the code. | See the root-cause section — this is the whole story. |
| 7 | MED | The healing-regime comment still described "12 of 13 field duties detach for 4–6 days (only `WaitHours` stays attached)" after zero detached and `WaitHours` was deleted. It also hid a real behaviour change (a duty day is now a parked day, so TAOM heals where vanilla did). | The comment was written **that morning** and was correct then. I never re-read it while removing the model it described. | When deleting a concept, grep its name across comments, not only code. `WaitHours` and `DetachedOnDuty` were both greppable. |
| 8 | MED | `EnlistedDetachedOnDuty` became unproducible, stranding 5 transition edges, a 30-line reconciler handler, and a talk-to-commander branch. | I reasoned carefully about the enum member's *save-compat* and stopped there, satisfied. The state's **downstream consumers** were a separate question I never asked. | Retiring a state has two halves: can it still be produced, and who still consumes it. Answer both. |
| 9 | MED | 26 runtime-composed loc keys shipped unregistered. | Keys built as `"..." + duty.Id + "_success"` are invisible to a `{=key}` grep — the exact hazard `generate_enlistment_duty_strings.py` exists to close, which I had extended *that day* and did not extend again. | The generator is the checklist. Any new runtime-composed key family means extending it in the same commit. |
| 10 | MED | `ExitSettlementForDuty` left with zero callers and a 10-line doc describing the deleted deadline model. | Same as #4 — deleted the caller, kept the method. | (as #4) |

## Root cause: I shipped the defect class I spent the session codifying

Findings 6 and 7 are the same bug as the one that killed the game that morning.

The #375 crash shipped because `OnTargetPartyDestroyed` carried a comment asserting the adapter's
`IsActive` check broke the recursion. It did not. I wrote the RCA for that, appended the lesson to
`lessons/testing-qa.md` — *"a comment asserting engine behaviour is a claim; hold it to
commit-message standards"* — and then, hours later, wrote:

> `/// Rank contribution to the check. Shared with the interactive duties so the two cannot drift.`

while leaving a second `const int RankBonusPerLevel = 4` in the file next door.

**The mechanism is worth naming precisely, because "be more careful" will not fix it.** I wrote the
comment at the moment I formed the *intention* to relocate the constant. The intention was correct.
The comment described it accurately. Then the code went a different way — I aliased in one file and
never touched the other — and nothing re-read the comment, because comments are not compiled,
tested, or diffed for truth.

That is the same shape as finding 7: a comment written that morning, correct that morning, describing
a model I deleted that evening. And the same shape as the original #375 comment. Three instances, one
mechanism: **a comment is written against the author's model of the code, and only the code is
subsequently checked.**

The existing lesson tells you to verify a comment's *claim about the engine*. It does not cover a
comment's claim about **our own adjacent code**, which is what all three of these were. That scope
gap is the actionable output of this RCA.

## Why the reviews caught it and I did not

Both independent passes found finding 6 immediately, by the same method: `grep -rn RankBonusPerLevel`.
Two hits with two different definitions. It took one command.

I never ran it because I had no reason to doubt myself — I had just written the alias. The reviewers
had no such prior, so they checked. **That is the entire value of the gate I skipped**, and it is why
"I'll review it carefully myself" is not a substitute: the author cannot hold the reviewer's prior.

## Preventive actions

1. **Applied.** All ten findings fixed across `7c1a1a92` and `20d3344d`.
2. **Applied.** `lessons/testing-qa.md` → *"Widen 'a comment is a claim' to cover claims about our
   OWN adjacent code."* The prior entry was scoped to *engine* behaviour, one category too narrow.
   Trigger words: shared, single source, cannot drift, always, never, only, unconditional — each is
   one grep from verified or falsified.
3. **Process.** `/deep-review` before the commit, not after. Every finding here was findable
   pre-commit; the reviews were run late and still found all ten, which means the gate works and the
   timing was the only failure.
   **Held for the rest of the day**, and it kept paying: the Codex pass on the coverage guards found
   five more (including two comments of exactly the class this RCA is about), and the adversarial
   pass on PRs #440/#442 refuted all four of its own HIGHs while surfacing a real missing exception
   guard and the #443 team-topology gap.
4. **Deletion checklist**, from findings 4, 7, 8 and 10 — when removing a mechanic:
   grep the concept's name in *comments*; grep each removed member for surviving *declarations*, not
   just callers; and ask what the mechanic communicated *incidentally*.

## Follow-up: finding 9 generalised, and it found a second instance

Finding 9 was a *local* fix — extend the generator, register the 26 keys. The class it belongs to is
broader: **a localization key composed from data is invisible to the `{=key}` grep that every
registration audit is built on.** So the audit reports clean while being structurally incapable of
seeing the family.

Sweeping all four composition sites in the codebase found a second live instance: all 96
character-creation narrative rows for `goblin` and `mistymountainorcs` (#432), unregistered — two
entire cultures, while the other sixteen were complete. Registered in `83f970df` with
`NarrativeStringRegistrationTests` pinning presence *and* value drift.

Enumerating all four sites, rather than sampling until one turned up, is what makes "there are no
more" a claim worth making. The forms to grep are `"{=" +`, `"{=prefix" +` and `$"{{=`. Lesson
recorded in `lessons/localization-ui.md`.

One more pattern fell out of it, worth more than either instance: the cultures that lose coverage
are always the non-vanilla ones. `goblin` / `mistymountainorcs` here; `shaghana` / `abanissa` had no
eligible careers (review #24) and have no enlistment rosters (#431). They were added after the
tables that enumerate cultures were written, so every hand-maintained per-culture list omits them.
**A per-culture invariant must be driven off the culture list itself.**

## Verification

Build 0 errors · suite **6276 passing / 0 failing** (6274 at the time of the rework, +2 for the
narrative-registration guard) · `validate_moduledata.py` PASS · `lint_docs.py` clean ·
217 enlistment localization keys, all 12 languages id-identical to English.

**Not verified: any of it in a live game.** Tracked on #428 and #375.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
