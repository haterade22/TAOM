# RCA: unmapped ActionIndexCache field names (2026-11-12)

## Top-line

All 30 kept launch logs on the desktop (read this session) show `REPAIRED 217 of 220 ... 0 already healthy, 2 unknown
to this engine build`. Two fields, `act_wreckage_death_01` and `_02`, are built by the engine's static constructor from
other action names (`act_cutscene_main_hero_battle_death_01` and `_02`), and `KnownNameOverrides` in
`ActionIndexCacheRepair` held only `act_raid_jump`. The repair looks an action up by field name, found no such action
and left both fields at -1. The cctor holds both since v1.5.3 at the latest.

The fix is two rows in the map plus a test that reads the installed cctor's IL. `/deep-review` (tier 1: data flow and
engine compatibility, at max effort) confirmed both rows against the installed DLL, the action XML and the native
lookups, and found no CRITICAL or HIGH defect. Codex did not run: the maintainer's standing instruction of 2026-11-12
is no Codex runs. The lenses found one MEDIUM gap and two LOW text errors.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Nothing checked `KnownNameOverrides` against the engine cctor. The only control was a comment, "re-diff after an engine bump", and it did not run for v1.5.3, v1.5.5, v1.5.6 or v1.5.7 | A manual step with no gate | The map came from one hand diff at v1.4.7. The log line reports "unknown to this engine build" as an ordinary count, so two permanent unknowns read as normal. No engine-bump document names `ActionIndexCache` | `ActionIndexCacheRepairEngineTests` reads the cctor IL and fails when any field's name and literal disagree with the map (a missing row or a stale one), when a field is assigned twice, or when the cctor holds a shape the walker cannot read |
| 2 | LOW | The new test name and two comments said "v1.5.7 adds" the wreckage fields. The v1.5.3 cache already has them | Wrong version attribution | The dating came from the log being read (v1.5.7), not from the older taom-src caches | Fixed in this change. Check the older caches before dating an engine fact |
| 3 | LOW | The class summary says the type is not `beforefieldinit` and counts "215 fields". The installed IL says `beforefieldinit` and v1.5.x has 220 fields | Stale comment | `lessons/animation-skeleton.md` was corrected on 2026-10-01 (#700), but the code comment was not | Listed under follow-ups: the lines are outside this change |

## Root-cause pattern

A hand-kept table that mirrors an engine table had a comment as its only guard. The failure was visible in every log,
but as a count that looked like normal behaviour. A gate that re-derives the table from the engine on each bump turns the
silent case into a failing test. The round-trip guard in the repair cannot do this job: it compares the engine's answer
with the map's own output, so a missing row passes quietly and a stale row passes as a valid but wrong index.

## Why each agent missed it

- **Both lenses found finding 1 independently.** Neither missed it.
- **Standards, efficiency, completeness and design lenses:** not run at tier 1.
- **The original repair review (2026-08-01):** it found `act_raid_jump` by diffing the v1.4.7 cctor. It had no wreckage
  fields to find, and no gate was added to repeat the diff for a later engine.

## Feedback memories to codify

None. The durable rule is the entry in `docs/reviews/lessons/animation-skeleton.md`.

## Follow-ups

- FOLLOW-UP: the class summary in `ActionIndexCacheRepair.cs` (lines 15 to 17 and 40 to 41) still says "not `beforefieldinit`" and "215-field". no issue: comment-only, already corrected in the lessons file on 2026-10-01; batched with the next edit to that file.
- FOLLOW-UP: every kept launch starts with all indices poisoned (`0 already healthy`), so the type initialises before action types load on every boot. The repair works around it. no issue: finding the early touch is a maintainer decision, `/investigate` is the route.
- In-game check owed: the next campaign launch should log `REPAIRED 219 of 220`. Whether the wreckage corpse clips look right with dwarf or troll casualties has not been seen in game (`BattleWreckageVisual` reads both fields).
