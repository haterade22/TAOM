# RCA — SAS-survivor implementations (six work items), 2026-08-08

**Review:** 6 deep-review agents + an independent Codex pass, over ~25 changed files.
**Outcome:** Codex `P1: none / P2: none / P3: none — VERDICT: SHIP`. Five of six Claude agents clean.
**Confirmed findings: 6. Behavioural defects among them: 0.**

That last line is the whole story of this review, and it is worth more than the findings themselves.

## Top-line

Six agents implemented six independent work items **in parallel with builds forbidden** — concurrent
`dotnet build` corrupts `obj/`, so none of them could compile what they wrote. The predicted failure
mode was wrong-but-compiling API use, drift between the six slices, and duplicated logic.

**None of that happened.** The code compiled with 0 errors on the first attempt, the suite went
6089 → 6234 green, and both the data-flow agent (11 traces) and Codex (9 hypotheses) found zero
gaps.

Every confirmed finding was instead a **comment, doc, or test message asserting something that was
not quite true.** Six for six.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | MED | The CHANGELOG described `generate_enlistment_rosters.py`'s change as "three comments corrected". It actually gained `TREE_ALIASES` + `apply_tree_aliases()` — a real code path that makes a re-seed reproduce the lothlorien/battania kits instead of falling back to the default kit. | Scope mislabel | I wrote the entry from the agent's own summary line, which led with the comment fixes. I did not diff the file before describing it. | Diff a file before writing prose about what changed in it. A functional change described as a comment fix skips the scrutiny a code change gets. |
| 2 | MED | `generate_enlistment_duty_strings.py` uses a **third**, undocumented XML I/O idiom (`io.open(..., encoding="utf-8", newline="")`) rather than either sanctioned form. Byte-faithful today; a future edit stripping `newline=""` as boilerplate silently converts a 13-line insert into a whole-file CRLF→LF rewrite. | Convention drift | `tools/README.md` names two idioms and nothing enforces the choice. The output was verified correct, so no test or lint could see the latent risk. | Documented **why** the kwarg is load-bearing at the call site. A comment is the right fix for "someone will delete this thinking it is noise." |
| 3 | LOW | That script's "already registered" regex was a bare `id="([^"]+)"`, unanchored — it would also match an id mentioned inside an XML comment, silently treat that key as registered, skip writing it, and report nothing. | Latent fragility | Zero `id="` occurrences inside comments today, so the bug is unreachable and invisible. The sibling roster script already anchors its equivalent regex; the inconsistency was never compared. | Anchored to `<string\s+id="`. Verified empirically — the generator still reports `already registered: 97`, which only holds if the anchored form matches all 97. |
| 4 | LOW | Twelve tests assert `WagePolicy.ComputeDaily`'s `Forfeited`/`NewlyDeferred` with messages like *"must be reported"* — implying they cover the forfeit-reporting path. They do not: those fields have **zero production consumers**, and `ServiceRewardService` deliberately re-derives the reported figure by conservation from what the transfer actually delivered. | False coverage claim | The assertions are correct and the tests pass, so nothing fails. Only tracing consumers of the asserted field reveals the message is about a value nobody reads. | Rewrote the messages and added a scope header naming the plan-vs-actual split. |
| 5 | LOW | A comment justified the new `taom_enlist_wait_board_v2` key with *"a registered translation wins over the inline default … in all 12 languages."* Wrong by one, and interestingly so — see below. | Over-generalised engine claim | The rule is true for the languages one is normally thinking about, so it generalises to "all". English is structurally immune, and English is the language the developer plays in. | Comment corrected; durable fact appended to `docs/reviews/lessons/localization-ui.md`. |
| 6 | LOW | `taom_enlist_wait_board` was left registered in all 13 files with zero C# references after being superseded. | Dead weight | Superseding a key is a two-step operation and only the first step has a failing symptom. | Removed from all 13; id-set parity re-verified in both directions. |

## Root-cause pattern: prose confidence is not calibrated to verification

Five of six findings are the same defect in different clothes — **an explanatory comment stating a
property the code does not have, or a test message claiming coverage it does not provide.**

The mechanism is specific to how this changeset was produced, and worth naming because we will do
this again. An agent that cannot compile compensates by reasoning very carefully in prose. That
prose is genuinely useful — the comments in this changeset are unusually good, and the data-flow
agent explicitly noted that nearly every hypothesis it tested mapped to a deliberate design decision
with an inline comment explaining the prior bug it prevents. But **the comment's confidence is
produced by the same unverified reasoning as the code, while reading as though it were verified.**
The code then gets checked by a compiler and 6234 tests. The comment gets checked by nobody.

This is not hypothetical. **Earlier the same day, this exact class killed the game.** The #375 duty
recursion shipped because `OnTargetPartyDestroyed` carried a comment reading *"the party is already
gone at the engine level — FinishActive's DestroyParty call is a defensive no-op (the adapter checks
IsActive before acting)."* Both halves were false: `DestroyPartyAction.ApplyInternal` dispatches
`OnMobilePartyDestroyed` on line 23 and calls `RemoveParty()` on line 25, so the party still reads
`IsActive` during the callback and the adapter guard passes straight through. A reviewer who read
that comment had every reason to stop checking. That is what a false safety property buys you: it
does not merely fail to help, it actively spends the next reader's attention.

**The generalisation:** a comment asserting engine behaviour is a *claim*, and claims in comments
should be held to the same standard as claims in a commit message — cite what was read, or say what
is uncertain. `evidence-over-claims.md` §C already governs this for user-facing statements; it has
not been applied to code comments, which are the statements that survive longest and are trusted
most.

## The one finding worth reading even if you skip the rest

Finding 5 uncovered a durable engine fact that applies to every TAOM feature, not this one.
`MBTextManager.GetLocalizedText` (v1.4.7, `TaleWorlds.Localization.MBTextManager.cs:264-268`):

```csharp
if (_activeTextLanguageId == "English")
{
    text2 = _targetStringBuilder.ToString();   // the inline default
    return RemoveComments(text2);              // registered row never consulted
}
```

For the `new TextObject("{=key}English fallback")` form, **an English player always sees the C#
literal.** The English XML row is translator source material and the id registry — not a render
path. Two consequences: an English copy edit made only in XML does nothing in game, and a
template-shape change needs a new key id because the 11 translated rows *will* win for their
languages while English keeps looking correct throughout. The language you test in is the one that
cannot show you the bug.

## Why each agent missed what it missed

- **Standards, Efficiency, Data Flow, API, Codex** — all five are scoped to code correctness. None
  has a rule that reads a comment as an assertion and checks it. This is a genuine gap in the
  review harness, not agent error: the API agent *did* catch finding 5, but only because it was
  independently verifying an engine claim that happened to be written in a comment.
- **Completeness** — caught findings 1 and 6, and produced the one wrong number in the whole review
  (reported 182 localization keys; the real count was 192, re-derived by id-set comparison). Worth
  recording that the agent that checks for gaps also mis-measured, which is why the count was
  re-verified rather than repeated.
- **Tooling** — caught 1, 2 and 3. Confirms the Step 2c rule that a changeset touching
  data-mutating `tools/*.py` needs a dedicated agent: the five core agents are C#-centric and all
  five read past the same script.

## Preventive actions

1. **Applied this session.** All six findings fixed; the localization fact appended to
   `docs/reviews/lessons/localization-ui.md`.
2. **Proposed rule extension (not yet applied — needs its own commit).** Extend
   `.claude/rules/evidence-over-claims.md` §C to name **code comments and test-assertion messages**
   as claim surfaces. Concretely: a comment asserting engine behaviour should either cite what was
   read (`DestroyPartyAction.ApplyInternal:23-25`) or be phrased as an assumption. This is the
   single change that would most likely have prevented the #375 crash.
3. **Proposed deep-review addition.** A cheap check for the five core agents: *for every comment in
   the changeset that asserts engine behaviour or a safety property, verify it against the
   decompile the same way an API claim would be verified.* Today no agent owns this.
4. **Standing note for parallel-agent changesets.** The no-build constraint proved safe for code
   (0 compile errors across six agents) and unsafe for prose. Weight future review of such
   changesets accordingly.

## Verification at the time of writing

Build 0 errors · suite **6234 passing / 0 failing** · `validate_moduledata.py` PASS ·
`lint_docs.py` clean · 14 XML files parse-smoke clean · 191-key localization id-set identical
across English and all 12 languages.

**Not verified: any of it in a live game.** The six changes have never run in Bannerlord. That gate
is unchanged by this review and is tracked on #375.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
