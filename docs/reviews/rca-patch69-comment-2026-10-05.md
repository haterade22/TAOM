# RCA: Patch69's class comment, reviewed 2026-10-05

**Scope:** the class doc of `Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs`, rewritten after the v1.5.4
check found it named four callers of `FightTournamentGame.GetParticipantCharacters` (there are six on v1.5.3 and
v1.5.4) and said the retired clean-roster log line "is DEBUG". Six `/deep-review` lenses (1 to 6; XML and Tooling not
in scope) on Opus 5.5 at max effort. No lens found a defect in any engine claim: every caller, frequency and
consequence the new text named was verified against the installed v1.5.4 DLLs and the v1.5.3 dump. The findings are
about where that text lives and one quotation.

**Shipping note:** another session's commit `75b7b666` ("Refactor code structure and remove redundant sections...",
2026-10-05 12:21) staged this file from the shared working tree while the review ran, so the first rewrite reached
`origin/bannerlord-1.5.x` inside that commit, unreviewed and under a subject without the version label. The fixes
below land in a follow-up commit; pushed history is not rewritten.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The rewrite copied the registry's caller list and consequences into the class doc, and the copy was already incomplete: it left out `BettingFraudIssueBehavior`'s join menu and the "not marked known to the player" effect, both in the registry (lenses 1, 5, 6) | Duplicated knowledge | Written from the verifier's evidence list as a second full account, though `harmony-patches.md` puts a patch's rationale and history in its registry entry. Both copies had drifted before, to different wrong counts (four here, three there) | Applied lens 6's proposal: the comment states the claim in five lines and points to the registry entry. Lesson appended to `lessons/harmony-il.md` |
| 2 | NIT | The comment and the registry quoted `"{NOBLE_COUNT} lords are competing"`, which is not the engine string (`{=GuWWKgEm}` "Apparently there are {NOBLE_COUNT} lords with renowned fighting skills present...", `FightTournamentGame.cs:86`) (lens 1) | Paraphrase presented as a quotation | Carried over from the 2026-08-07 text without a grep | Registry now quotes it with its key; the comment no longer quotes it |
| 3 | LOW | `lessons/build-tooling-workflow.md` still said "four call sites" in the lesson about counting a patch target's callers (lens 1) | Stale record | The v1.5.4 doc sweep searched for the armour claim, not the caller count | Correction added in place, with the two missed callers |

**Follow-up, not applied:** `ResetForUnload()` on this class "stays until the class is next touched"
(`harmony-patches.md`); retiring it edits code and the single-owner `SubModule.cs`, so it is left out of a comment-only
change (lens 1). **Completeness:** no issue filed for a comment fix; #407 is the patch's issue. **Efficiency:** nothing
in the change has a runtime cost.

## Why each lens saw what it saw

Lenses 2 (engine) and 4 (completeness) verified every claim and found nothing wrong in it; duplication is outside
their rule sets. Lens 3 found no cost. Lenses 1, 5 and 6 compared the comment with the registry it cited, which is how
the incomplete copy surfaced.
