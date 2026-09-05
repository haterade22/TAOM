# RCA — race-persistence-legend (#330), 2026-07-05

**Top line:** the feature fix itself came through both review layers clean — deep-review (5 agents): **0 code findings**; Codex adversarial (gpt-5.5 xhigh): **0 P1 / 0 P2 / 0 P3, VERDICT CLEAN** (all 6 seeded Known Suspects DISPUTED with decompiled evidence). The only confirmed findings were two documentation items from the completeness agent, one of them pre-existing. Per the deep-review skill, RCA applies to every confirmed finding regardless of severity — both are recorded here.

## Findings table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | LOW | `docs/features/hero-race.md:109` described `RacePersistenceServiceTests` as verifying "CaptureHeroRaces skips race 0" — false since the #130 P2 fix (2026-05-14) inverted that behavior to capture ALL races including humans | Doc drift (stale test description) | The #130 session updated code + tests + added its own changelog bullet, but never grepped the doc's **Tests** section for prose describing the *old* behavior. Two subsequent doc edits (2026-06-24, 2026-07-02) touched other sections and didn't re-read this one. | Fixed in this session's doc update. No new rule — this is precisely what the deep-review completeness agent exists to catch, and it caught it. The existing agent prompt ("read each test file; are descriptions accurate") is the control; it worked. |
| 2 | LOW | `hero-race.md` component diagram + persistence prose didn't yet mention `_taom_raceNameLegend` at review time | Sequencing, not a miss | The plan deliberately deferred the doc update to close-out (after both reviews), so the review ran against a not-yet-updated doc. | None needed — the update landed the same session, before the closing commit, per plan. Flagged here only because the agent factually reported it. |

## Root-cause pattern

None — the two items don't share a code-defect theme. Finding 1 is the known doc-drift class (change behavior, forget the prose describing the old behavior); finding 2 is planned work observed mid-flight.

## Why each agent missed these

Not applicable in the usual sense: no agent missed a code defect (there were none to miss — corroborated independently by Codex). Finding 1 was *caught* by the completeness agent; finding 2 was correctly reported as pending.

## Feedback memories / LESSONS-LEARNED to codify

None. A single pre-existing stale doc line, caught by the existing review control, does not warrant a new cross-feature rule — manufacturing one would be prevention theater. The doc-drift class is already covered by the completeness agent's test-coverage/doc-accuracy checks.

## Process notes worth keeping

- **The original bug (the feature's motivation) was user-identified:** the persisted race int is a `skins.xml` merge-order index, and `IsValidRaceId` is an in-range check that cannot detect a shift. The codebase had already internalized the "ids shift with the module set" principle for the tableau guard (2026-07-02) but not for persistence — when a principle produces a fix in one subsystem, grep for the same pattern in its siblings (here: every consumer of race ints that outlives a process).
- **Design decision preserved for the record:** the legend (one `;`-joined string + the proven `Dictionary<string,int>`) was chosen over `Dictionary<string,string>` because the latter failed to round-trip `IDataStore` at ~1000 entries (WotR Momentum, 2026-07-03). Codex's decompile of `SaveableBasicTypeDefiner` shows `Dictionary<string,string>` *is* a registered container type — so the momentum failure remains empirically unexplained at the type-registration level. The legend design sidesteps the question entirely; if a future feature needs a big string-valued dict in SyncData, investigate that failure first.
- Codex sandbox still can't run `dotnet test` (MSBuild SDK probe access denied) — recurring; local suite green (4,120) covers it.

## Review artifacts

- Deep-review: 5 agents (standards / installed-DLL compat / efficiency / completeness / data-flow), all PASS — data-flow proved capture-before-write ordering synchronous from decompiled `SaveHandler.SaveTick`.
- Codex: `codex-adversarial-race-persistence-legend-2026-07-05.{prompt.md,md}` (1.9MB session log discarded, final message kept).
- Issue: #330. Feature doc: `docs/features/hero-race.md` (updated).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
