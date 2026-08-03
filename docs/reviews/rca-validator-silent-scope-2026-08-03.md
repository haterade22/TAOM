# RCA — silent scope loss in the validator + a mis-attributed divide site (2026-08-03)

**Scope reviewed:** the ModuleData-validator extension (`BROKEN_BODY_PROPERTY_REF` +
`extra_ref_roots` Armory sweep), the `MissionDiagnostic` campaign-context change, the
`lords.xml` BodyProperty repoint, and `audit_mount_parity.py` section F.
**Review:** 6 agents — standards, API compatibility, efficiency, completeness, cross-system data
flow, tooling correctness (added because the changeset is majority Python and the 5 core agents are
C#-centric).
**Origin:** all of it came out of `docs/reviews/investigation-rhun-dwarf-ctd-2026-08-02.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | **HIGH** | A missing/renamed `LOTRLOME_Armory` root made the new sweep drop silently; the run still printed `PASS`, identical to a real clean run | silent scope loss | I wrote the existence filter (`if Path(r).exists()`) and the success print in the same breath, and never asked what the *negative* branch prints. The tool reports what it did, not what it failed to do | `Validator.missing_ref_roots` + CLI WARNING; test `test_missing_extra_ref_root_is_recorded_not_silently_dropped` |
| 2 | MED | No size floor on any registry — a renamed vanilla `*_bodyproperties.xml` shrinks it silently; to zero it trips the empty-registry guard and skips the check entirely | silent scope loss | The empty-registry guard was designed for one cause ("no game install", already reported) and reused for a second it cannot distinguish ("the file list broke") | `Registries.suspect_registries` floors + 3 tests |
| 3 | MED | `audit_mount_parity.py` section F: an empty clip inventory made the `quad_movement` probe vacuous (`bound[r] in inv` is false for all `r`), reporting nothing regardless of ground truth | silent scope loss | Same shape as #1/#2. Masked in practice because a *different* check (`dangling`) floods on the same input — luck, not design | Section F returns early with a WARNING when the inventory is empty |
| 4 | MED | Code comments, investigation doc and CHANGELOG all named `CampaignTime.GetDayOfSeason` / `TimeTicksPerDay` as the divide-by-zero site; the real first divide is `GetYear` / `TimeTicksPerYear` | fabricated mechanism | I grepped `CampaignTime.cs` for a division, found one on line 152, and asserted it without reading `ToString()`'s body to see which getter is evaluated first | Corrected in all 5 places, with the correction recorded in the investigation doc rather than silently overwritten |
| 5 | LOW | `_read` / `_read_stripped` used plain `utf-8`, against the repo's own XML I/O convention | convention drift | Pre-existing; my change newly routes 382 Armory files (2 with a BOM) through them | Moved to `utf-8-sig` |
| 6 | LOW | `_rel()` foreign-module label degrades to a bare `/path` when a root sits at a drive root | edge case | Not reachable from the real CLI call site | `root.parent.name or root.name` |

Not fixed, recorded deliberately:
- **`MissionDiagnosticService.LogCampaignContext` has no unit test.** It reads `Campaign.Current`
  statics that cannot be substituted, and the service has never had a test file. The pure formatter
  it delegates to has 6 tests. Building a harness for the boundary would be more scaffolding than
  the one-line delegation is worth; recorded as a known limitation in CHANGELOG instead.
- **The Armory sweep is load-bearing only for `Culture.` refs today.** The other four kinds have
  nothing to match in Armory XML. The wiring is correct; the doc now says so precisely instead of
  implying all five kinds are exercised.

## Root-cause pattern: three instances of one bug

Findings 1, 2 and 3 are the **same defect in three places**, written by me in one sitting:

> A tool that widens its own coverage reports the *success* path and stays silent on the
> *degraded* path, so a run with the new coverage switched off is byte-identical to a healthy one.

The irony is exact: this changeset exists because `validate_moduledata.py` was silently
under-scoped (it never swept the Armory, which is where 28 of the 33 dangling refs lived). I fixed
the under-scoping and reintroduced the same class of blindness in the fix — three times.

The unifying rule is not "check paths exist". It is: **when a tool's coverage is data-dependent,
the absence of coverage must be louder than its presence.** A `PASS` has to mean "I checked", never
"I had nothing to check and didn't say so".

Finding 4 is a different failure — asserting a specific mechanism from a plausible grep hit rather
than reading the code path — and is the `evidence-over-claims.md` §C trap, not a scope-loss bug.

## Why each agent missed what it missed

- **Standards (Agent 1):** C#-only by construction; findings 1-3 are all Python. Correctly reported
  the C# as clean, which the other agents confirmed.
- **API compatibility (Agent 2):** found #4, the one finding that required decompiling the installed
  engine. No other agent could have caught it — the claim was about engine internals, not TAOM code.
- **Efficiency (Agent 3):** looked at the sweep's cost, not its coverage. Correctly judged the
  delegate allocations and the extra 382 files irrelevant at real call frequency.
- **Completeness (Agent 4):** found the stale feature doc (which still listed `BodyProperty.` refs as
  out of scope) and the missing CHANGELOG entry. Scope was artefacts, not runtime behaviour.
- **Data flow (Agent 5):** traced 6 flows, 0 gaps — correctly, because the *wiring* is sound; the bug
  was in what happens when an input is absent, not in how data connects. It did independently flag
  the section-F fragility (#3) as a low-priority note, one severity below the tooling agent.
- **Tooling correctness (Agent 6):** found #1, #2, #3, #5, #6 — every silent-scope finding. It was
  the only agent that asked "what does this print when the path is wrong?" and then actually ran the
  tool with a wrong path instead of reasoning about it.

**The load-bearing lesson about the review itself:** the 5 core agents are C#-centric and would have
passed this changeset. The tooling agent is not part of the default 5 — it exists only as a
conditional expansion in Step 2c. A majority-Python changeset must launch it, or the review is
inspecting the minority of the diff.

## Lessons to codify

Appended to `docs/reviews/lessons/build-tooling-workflow.md`:

### A tool that widens its own coverage must report the DEGRADED path, not just the success path
When a validator/audit gains a new data-dependent scope (an extra sweep root, a registry built from
an explicit file list, a globbed asset inventory), the run must be loud when that scope resolves to
nothing. Three instances shipped in one sitting (2026-08-03): a missing Armory root printed `PASS`,
a shrunken registry tripped the empty-registry guard and skipped its check, and an empty clip
inventory made a `quad_movement` probe vacuous. All three produced output indistinguishable from a
healthy run.
- **Why missed:** the existence filter and the success message get written together; nobody asks
  what the negative branch prints. An empty-input guard designed for one benign cause ("no game
  install") gets silently reused for a malignant one ("the file list broke").
- **Prevent:** for every new coverage input, write the "resolved to nothing" branch in the same edit
  as the filter, and add a test that asserts the tool SAYS SO. Set registry floors far below real
  counts so they catch a broken file list, not data drift. Ask of any new check: *if its input were
  empty, would this report clean?* If yes, it is not a check yet.
- **Source:** `docs/reviews/rca-validator-silent-scope-2026-08-03.md`

### Read the method body before naming a throw site
`CampaignTime.ToString()` was documented in 5 places as throwing in `GetDayOfSeason`
(`/ TimeTicksPerDay`). It evaluates `GetYear` (`/ TimeTicksPerYear`) first, and that is what throws.
The attribution came from grepping the file for a division and taking the first hit.
- **Why missed:** a grep hit that is *consistent* with the symptom reads as confirmation of it. Same
  exception type, same root cause, same fix — so nothing downstream contradicted the wrong detail.
- **Prevent:** when naming a specific line/getter as the failure site, read the enclosing method's
  evaluation order. "A division exists in this class" is not "this division is the one that ran".
- **Source:** same RCA; rule `.claude/rules/evidence-over-claims.md` §C.

### A majority-non-C# changeset needs the tooling agent, not just the core 5
The 5 core `/deep-review` agents are C#-centric. On a changeset that is mostly Python tooling they
will pass it while reviewing the minority of the diff — here they returned clean/quality-only while
the conditional tooling agent found all five real defects.
- **Prevent:** treat Step 2c's tooling-agent trigger as mandatory, not optional, whenever
  `tools/**/*.py|ps1` is more than a trivial slice of the changeset — including read-only tools,
  which the trigger's current wording ("that WRITE files") does not cover. All three findings here
  were in read-only tools.
- **Source:** same RCA.
