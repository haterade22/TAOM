# RCA: Quartermaster refusal answers (#829, deep-review, 2026-10-19)

## Top-line

`/deep-review` (seven lenses in two waves) on the #829 change found **no runtime defect** and
**1 MEDIUM-if-unpaired plus 8 LOW** findings, mostly in the tests. The change gives the
quartermaster one answer per kit-request outcome instead of one unconditioned "already drawn" line.
Engine compatibility and data flow confirmed the design: the player line's consequence sets the answer
before the reply is picked, lines are tried by descending priority with stable ties, and a co-op client
never reaches the issuer. Every finding was fixed in the same session before the first commit; one
convergence reviewer then checked the applied fixes (see "Convergence pass").

The finding class worth recording repeats an existing lesson: two of the new tests could not fail. One
asserted `Enum.IsDefined` on values the switch could only ever return as named members; the other
matched the substring `null,`, which the null consequence satisfied on its own, so a non-null condition
would still have passed.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM if unpaired (Completeness) | Moving the co-op client decision out of the policy would have deleted the only test of "a co-op client gets the host-only line". | Test coverage moves with logic | The simplification was proposed without its replacement test. | A source pin checks the authority branch sets HostOnly and never calls the issuer. |
| F2 | LOW (Standards, Engine, Data flow, Design) | The client branch passed a placeholder `PartyUnavailable` the policy ignored, and re-made the authority decision the caller had already made. | Placeholder argument | The plan mapped "each result and a co-op client" in one function. | The branch sets HostOnly; the policy takes only the result. |
| F3 | LOW (Data flow, Standards) | `For_EveryResult_MapsToADefinedAnswer` could not fail. | Vacuous test (repeat of `lessons/testing-qa.md` "A test must be able to fail") | Written to the plan's wording, not to a mutation. | Replaced with an undefined-result test that pins the default. |
| F4 | LOW (Standards) | The fallback pin matched `null,`, which the null consequence satisfied alone. | Vacuous test (same repeat) | Substring check. | The pin requires null condition, null consequence and the lower priority. |
| F5 | LOW (Completeness) | Nothing pinned the catch-all line's priority below the answers, or which line each answer used. | Missing pins | Counted conditions, not their lines. | A priority pin and an answer-to-line table test. |
| F6 | LOW (Design) | The cannot-load sentence was registered twice (a conditioned line and a fallback that could never fire). | Redundant registration | The fallback was added as insurance without checking reachability. | One null-condition cannot-load line below the conditioned answers; text and keys unchanged. |
| F7 | LOW (Standards) | Two top-level types in one file. | File convention | Small enum plus policy written together. | `QuartermasterAnswerPolicy.cs` holds the policy. |
| F8 | LOW (Engine, Data flow, Standards) | Comments and docs said the fallback answers "a future result"; a new result maps to cannot load through the policy default. | Wording | The fallback's purpose was described from intent. | Reworded to "an answer without its own line". |
| F9 | LOW (Completeness) | The owed check named `/localize` (already run) instead of the paid translation run, and omitted why the cannot-load case has no manual recipe. | Doc accuracy | Copied from the earlier template. | Reworded. |

Follow-ups, outside this change: the rename of `IEnlistmentDialogGateService`'s discharge-named
check to `IsServingUnder`, since done (it gates the kit and reassignment lines too, not only
discharge); at the top rank (Sergeant) "Earn the next stripe first" promises a rank that does not
exist, which the seven-rank ladder (#936) fixes with a top-tier answer. #835 still lists a refusal bullet this
change completes.

## Convergence pass

One reviewer checked the applied fixes and confirmed the selected line for every answer on v1.5.5.
It found two LOW test gaps by mutating the source in memory: nothing pinned the policy call on the
authority path (replacing it with a constant left the suite green), and the client pin did not prove
the issuer runs only after the client branch returns. Both are pinned now, plus the answer-token
spelling; replacing the policy call with a constant was confirmed to turn the new test RED in the real
suite, and green again once restored.

## Root-cause pattern

F3 and F4 repeat a class the #828 review found the same day: its wiring test accepted an inverted
gate because a substring check matched both forms. The existing lesson bans the syntax of vacuous
assertions; these passed it because each assertion was well formed and only the input made it unable
to fail. The RED step would have caught both, and it was skipped: the production code was written in
the same pass as the tests, so the first run was green.

## Why each lens caught or missed these

- **Standards:** F2, F3, F4, F7, F8.
- **Engine compatibility:** confirmed the dialog selection order and F2; no defect.
- **Data flow:** confirmed every flow, plus F3 and F8.
- **XML:** all gates pass; the one failing coverage gate is the unrelated TAOM_Map translation backlog.
- **Efficiency:** no findings; conditions run once per click.
- **Completeness:** F1, F5, F9 and the #835 cross-reference.
- **Design:** F6, and agreed the shape of F2 to F4.

## Feedback memories to codify

None. The lesson exists; the gap was skipping RED, which `.claude/rules/tests.md` already requires.
