# RCA: Long Defeat recorder kinds for Phase 4 (2026-11-06)

**Change:** `taom.ld_record` gains the optional `armoury` and `yard` kinds, so the maintainer can
record the Phase 4 places in the 9 am session with the sally and the Phase 3 points (#1079).
**Review:** tier 2, lens 5 (Data flow) at high effort. 13 flows traced, nothing that breaks at run
time. Every finding below is LOW and fixed in the same commit.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The 9 am procedure in `phase3-plan.md` section 9 named neither `armoury` nor `yard`, and step 5 pasted only `towers` and `volley`, so the change could not reach its purpose | Procedure doc not updated with the feature it serves | The change was scoped as code plus the two reference docs; the procedure the maintainer follows lives in a plan file | When a change exists for a human step, update that step's text in the same commit; the lens 5 brief named the purpose, which is how it was found |
| 2 | LOW | Comments in `LongDefeatExportText.cs` and `LongDefeatRecorder.cs`, and two sentences of `dev-console.md`, still listed the old kind set | Stale enumeration in prose | A new member of a closed set was added to the arrays, and the prose copies of the set were not grepped | Grep the old set's last member (`volley`, `tower_volley`) in comments and docs when a set grows; one-off |
| 3 | LOW | No test covered the steep-look refusal for the new kinds, and the paste round-trip test did not carry them | Test coverage of a shared branch | The new tests copied the sally's happy path only | The refusal test and the round-trip test now take the new kinds as data rows |
| 4 | LOW | `clear armoury <word>` answered "A armoury record has no tower to name." | Article built into a template | The template assumed every kind starts with a consonant | The reply now reads "The <kind> kind has no tower to name.", pinned by a test |
