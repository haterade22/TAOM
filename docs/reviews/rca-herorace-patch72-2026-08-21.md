# RCA: HeroRace Patch72 tableau framing, tuner, MCM eye height (2026-08-21)

**Scope.** Wiring up the 3D tableau race-framing offsets that had been parsed and never applied,
deleting the dead service that was supposed to apply them, adding a dev-console tuner, and turning
the dwarf eye-height constant into an MCM slider.

**Review shape.** Seven parallel dimensions (standards, 1.4.8 API compatibility, efficiency,
completeness, cross-system data flow, a Harmony specialist scoped to Patch72, a console/MCM
specialist), then three adversarial refuters per finding on distinct lenses (does the source say
this / does the engine behave this way / is it reachable), then a completeness critic. 122 agents,
38 findings raised, 35 survived refutation, 5 more from the critic.

**Headline.** No CRITICAL findings, no API incompatibility, and no idempotence or scale defect: the
absolute-origin design held up against decompiled 1.4.8. Exactly **one** real correctness bug, and it
came from the part of the work that felt safest.

---

## Findings that changed code

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | Framing rows selected by PLACE, so swapping character/mount places gave the horse the rider's offsets and left a single-row race unframed | Correctness / data | Ported the deleted service faithfully and treated fidelity as correctness. Never asked whether the dead code was right, only whether the port matched it | Lessons entry below. When porting from code that was never executed, the source is a *hypothesis* |
| 2 | MED | Console accepted any string as a race, creating a live row that persisted and was then dead forever | Validation | The "Config Providers MUST Validate" rule was applied to floats and skipped for the string key, even though the key is what the lookup branches on | The rule already covers this ("especially validate any string field the CONSUMER branches on"); it was read as a float rule |
| 3 | MED | Tuner advertised `mount_` on the 2D surface, which has no mount row on any code path | Data flow | Wrote one help string and appended it to both surfaces without re-checking that both surfaces support every documented feature | Critic-class finding: shared help text is a data-flow surface |
| 4 | MED | A fourth loader of the same config survived in `Patch1_FirstTimeInit`, writing a `public static` nothing read | Dead code | Counted the loaders I deleted, not the ones that existed. `grep` for the type, not for the *file name being loaded* | Count by the resource, not by the abstraction |
| 5 | MED | Eye-height MCM hint promised an instant revert; the write-back only happens on the next dwarf visual build | Doc/promise | Wrote the hint from the design intent, not from the call graph | Hint text is a claim about the code and needs the same evidence standard |
| 6 | MED | Finiteness gate guarded only the 3D path; the 2D path reads the same live rows | Defence in depth | Added the gate where the new code was, not where the invariant was | Put a gate at the shared owner, not at one consumer |
| 7 | MED | `RacePositionTuningCheats` was 339 lines with all parsing private, so the nudge bound check was unreachable from any test | Testability / ADR-002 | Treated "console command" as inherently untestable and stopped there | Extract the pure part; only the dispatch is untestable |
| 8 | MED | Eye-height capture/restore had zero tests, justified by a false claim that `Monster` cannot be constructed | Testability | Asserted an untestability without checking it. The real blocker was different (below) | Never record an untestability claim without proving it |
| 9 | LOW | `EyeHeightAdjustment.Resolve` left two different values in its out parameter on failure | API contract | Wrote each failure path independently | One contract per method, stated in the doc comment |
| 10 | LOW | Save was non-atomic with no backup, into a file that ships | Durability | Treated the tuner as a dev tool and its target as a dev file. The target is a shipped config | Any write into the game install gets temp-swap + `.prev` |
| 11 | LOW | `LiveTableauRef.LastRace` never cleared, so `.` could resolve to a race nobody was looking at | Lifecycle | Enumerated the weak reference's lifecycle but not the *race id* stored beside it | Two fields, two lifecycles |

Also fixed: MCM display-name casing and format string diverging from 22 siblings, slider bounds
duplicated as literals instead of referencing the consts that enforce them, `WriteConfig` overload
with zero callers, a `ContainsKey`-then-indexer double lookup, and a per-refresh `WeakReference`
allocation.

## Root-cause pattern: fidelity is not correctness

Findings 1 and 4 are the same mistake pointing in two directions. The work was framed as "wire up the
thing that was already written", so the deleted service was treated as a specification. It was not: it
had **never executed**. Its place-based row selection had never framed a single character, and its
config-loading had a fourth copy nobody had noticed because nobody had ever traced what actually read
the file.

The tell, in my own commit message before review: *"This is a faithful port of the deleted
CharacterTableauService."* Faithfulness was offered as evidence of correctness. For code with a
runtime history that is a reasonable argument. For code that has never run, it is circular.

**The rule that follows:** when the source of a port has never executed in production, it is a
hypothesis about intent, not a specification. Validate it against the *data* the feature ships, which
is what exposed this one: `cave_troll` ships a plain row and no mount row, and place-based selection
sends its horse four metres out of frame.

## Why each dimension missed what it missed

- **Standards** found the 339-line console class and the untested parsing, but had no way to see that
  the four-way mapping was semantically wrong; nothing in ADR-002 is about meaning.
- **API compatibility** verified all eight field bindings, idempotence and scale preservation against
  decompiled 1.4.8, and correctly cleared them. The bug was not an engine-contract bug.
- **Efficiency** was correct to find nothing: the postfix is not per-frame.
- **Completeness** found the missing tests and the false untestability claim, but "is there a test"
  cannot reveal that the behaviour a test *would* pin is the wrong behaviour.
- **Data flow** is what caught finding 1, and it caught it exactly the way that dimension is supposed
  to: by comparing the code against the shipped JSON rather than against itself.
- **Harmony specialist** was told to assume the mapping was wrong and try to prove it. It confirmed
  the port matched both the old service *and* vanilla's frame selection, and stopped there. That is
  the miss worth noting: it verified the port, not the premise.
- **Console/MCM specialist** found the unvalidated race names and the write durability.
- **Completeness critic** found the two unpinned wiring lines and the shipped configs nothing parsed,
  which are the highest-value additions in the whole pass and were invisible to all seven dimensions
  because each was looking *at* files rather than at what guarantees the files.

## A second-order finding: an untestability claim that was wrong twice

I recorded, in two places, that the eye-height hook could not be tested because a TaleWorlds
`Monster` cannot be constructed in a test host. The review disproved it: `Monster` declares no
constructor and `MBObjectBase` has a public parameterless one.

But writing the tests then failed anyway, for a completely different reason: `ReflectionHelper`'s
**type initialiser** resolves `IReflectionService` from the DryIoc container, so the first touch from
a test host throws `TypeInitializationException`. The stated reason was wrong; a real blocker existed
one layer down and neither I nor the review had found it.

**This is the durable lesson.** An untestability claim is a load-bearing technical assertion that
silently removes a class from coverage forever, and it is almost never checked, because the artefact
it produces is an absence. Both my claim and the review's rebuttal were confidently wrong about the
mechanism. The only thing that settled it was writing the test and reading the exception.

The fix was worth having on its own terms: the hook now takes `IReflectionService` by constructor
injection, which is what the architecture rules asked for anyway. A static that resolves from the
container is service location wearing a helper's clothes, and its cost was nine missing tests.

## Preventive actions

1. **Lessons entry** in `docs/reviews/lessons/gamemodels-services.md` (fidelity-is-not-correctness)
   and `docs/reviews/lessons/testing-qa.md` (untestability claims).
2. **`HeroRaceWiringTests`** now pins the two silent-failure wiring lines. This feature had already
   shipped a registered-but-never-invoked service for months; the replacement should not be able to
   die the same way.
3. **`ShippedRacePositionConfigTests`** parses the configs players actually load. Neither the suite
   nor `validate_moduledata.py` touched `ModuleData/configs` before.
4. **No rule change proposed** for the string-validation finding: `.claude/rules/csharp-architecture.md`
   already says to validate any string field the consumer branches on. It was read as a float rule
   because every worked example in it is a float. Adding a string example there would help more than
   a new rule.

## Not fixed, deliberately

- **`PatchShield` does not exclude `TaleWorlds.MountAndBlade.View.Tableaus`**, so tableau patches pay
  the ~50 microsecond finalizer tax. Real, but it concerns the pre-existing per-frame `Patch67`, not
  Patch72 (a refresh is not per-frame). Out of scope for this changeset; worth its own issue.
- **No GitHub issue exists** for any of this work, which CLAUDE.md requires before implementation.
  The `github` MCP server is unauthenticated in this session, so it could not be created. Owed.

---

## Codex pass (independent, after the fixes above)

Dispatched on the fixed tree. **P1: 0, P2: 2, P3: 2.** It cleared every suspect the Claude review had
cleared, and did so by decompiling rather than by agreeing: origin selection against vanilla's own
four frames, idempotence, the buffer swap and visibility handoff, Patch2 interaction, native-null
handling (installed `GameEntity.operator ==` compares native pointers to `UIntPtr.Zero`), eye-height
re-entrancy, IoC ordering, all five console command shapes, and culture-sensitive formatting (the
installed launcher sets invariant culture).

It also confirmed the shared-Monster consequence explicitly, which nobody had proven before: installed
`FaceGen.GetBaseMonsterFromRace` caches into `_monstersArray`, and spawn code copies
`StandingEyeHeight` / `CrouchEyeHeight` into `AgentSpawnData`. So the eye-height offset really does
move the aim origin for newly spawned dwarves. The MCM hint already discloses that, so it stands as
intended behaviour rather than an accident.

| # | Sev | Finding | Verified how | Fix |
|---|-----|---------|-------------|-----|
| C1 | P2 | A finalized tableau stays "on screen" until GC, so `.` resolves to a dead tableau and the tuner reports success while redrawing nothing | Read `CharacterTableau.OnFinalize` on 1.4.8: it nulls every AgentVisuals but the texture provider keeps holding the managed object | `LiveTableauRef.ClearIf` from a new `OnFinalize` postfix in the Patch72 category |
| C2 | P2 | Image-surface edits cannot redraw their consumer; `RequestRedraw` only dirties the 3D tableau | `ResolveImage` has exactly one caller, inside `CharacterSpawner.InitWithCharacter`, which no command re-runs | Redraw is surface-aware, and the image path says the portrait must be reopened rather than implying a redraw |
| C3 | P3 | Two-file save is atomic per file, so it can half-commit and leave new avatar data beside old image data | Read `Save` and `WriteConfig` | Stage BOTH temp files, then swap both |
| C4 | P3 | `HeroRaceWiringTests` computed `batchEnd` and never used it, and Patch72 is listed after the marker, so the assertion was vacuous | Read the test and `SubModule.cs` line numbers | Bound by the array's own start and closing `})` |

**C4 is the one worth sitting with.** It was written *in this pass*, as a preventive control against the
exact failure mode this whole changeset existed to fix, and it did not work. A test that cannot fail is
worse than no test, because it discharges the obligation to think about the thing again. Both the seven
Claude dimensions and the completeness critic looked at that file and none of them asked the only
question that mattered: *what edit would make this test go red?*

**Preventive action:** when a review adds a guard test, verify it by breaking the thing it guards. For
a string-position assertion that means moving the string and confirming the test fails. The habit
generalises: a new assertion is not evidence until it has been observed failing at least once.

**The pattern connects to the RCA's existing theme.** Fidelity is not correctness (the port), an
untestability claim is not a fact (the eye-height hook), and now: an assertion is not coverage. All
three are the same error, which is accepting a proxy for the property you actually care about because
the proxy is cheaper to check.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
