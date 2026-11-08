# RCA: Fords of War fades every riderless mount (/deep-review, 2026-11-07)

## Top-line

The change: the Fords housekeeping sweep fades every riderless mount after 20 s, not only horses and wargs (Mike, 2026-11-07,
after he saw a war elephant with no rider in a match: "it needs to include all creatures, dire wolves, spiders, elephants,
mumakil, horses, wargs, fell wargs, etc"). The log of that match had already recorded it: `riderlessOthersPeak=camel:5,
taom_war_elephant:1,taom_mumakil:1`. Worktree `E:/repos/taom-fords-creatures`, branch `fix/fords-riderless-creatures`, base
`6791dbc75`.

- **Tier 1** (an engine call, `Agent.FadeOut`, from a mission tick). First wave: Lens 5 Data flow and Lens 2 Engine compatibility
  at `max`, the Tooling lens at `high` for `tools/fords_match_review.py`, Codex at `ultra` (gpt-6-astra). No CRITICAL or HIGH, so
  no second wave: Lens 1 Standards, 3 Efficiency, 4 Completeness and 6 Design are SKIPPED BY TIER, Lens 7 XML is NOT IN SCOPE, and
  Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 0 MEDIUM, 5 LOW, 0 API incompatibilities (Lens 2: 11 members verified). Every LOW is in text or
  the report tool, and each is fixed below.
- **Engine reading (Lens 2, native decompile):** a fade routs the mount first, resets every soldier that targets it, then fades it,
  with no step that depends on the Monster. Every TAOM store that holds a creature (behaviour trees, the creature tree tracker, the
  warg behaviour, the howdah and mumakil platforms, race abilities, bone checks) drops a routed agent as it drops a dead one. No
  Fords counter holds a mount: the registry holds riders, and the beast timers count time, not deaths (Lens 5, Codex).
- **Proof:** FordsOfWar, gate, architecture and warg tests: 2,584 passed, 0 failed, 5 skipped. `tools.tests.test_fords_match_review`:
  42 passed. `lint_docs.py --fail-on-drift` exit 0. Nothing ran in game: round 4 reads the new `[Housekeep]` counts.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The report printed "Wargs faded: 0" beside the new per-kind counts on every new log, and an old log never showed its horse count. (Codex, Tooling, Lens 5) | Report text | The old fields were kept so the round 3 logs still parse, and only the parse was tested. | The old line becomes `mounts_faded` as `horses:N,wargs:N`; the separate warg count is gone; "no Housekeep line" when a log has none. Render tests for both formats and for an empty log. |
| 2 | LOW | The new test asserted that an old log which faded 42 wargs reported no mounts faded. (Tooling) | Test | The assertion pinned the default, not the meaning. | Replaced by the assertions of finding 1, plus a two-line log where the last line wins. |
| 3 | LOW | The Housekeeping bullet still opened with "fades riderless horses. Other riderless creatures stay". (Codex, Lens 2, Lens 5) | Doc accuracy | Only the second half of the bullet was rewritten. | Rewritten as one rule. |
| 4 | LOW | The stated reason was wrong: soldiers do not skip a riderless mount for lacking a weapon state; the engine takes a mount's side from its rider, so a riderless one is nobody's enemy (native `IsEnemy`). (Lens 2) | Engine assumption | The wording came from the trap index line, written for creature bandits. | The doc and `RiderlessMountRule` give the engine's reason. FOLLOW-UP: no issue: Mike waived issues for Fords of War, 2026-10-31. The trap index line "Riderless creatures" in `docs/ai-includes/orientation.md` carries the same reason; it is outside this change. |
| 5 | LOW | `IFordsBattlefieldAdapter.Housekeep`'s summary still said it removes horses. (Lens 5) | Stale comment | The interface was not in the edit list. | Updated. |

## Open, to read in round 4 (UNVERIFIED)

- **Released crew.** A riderless elephant or mumakil with crew aboard now fades 20 s after its rider dies. Its platform releases the
  seats on its next tick and sets each archer down at the beast's position (`TaomHowdahStandingPoint.cs` 291, `TaomMumakilStandingPoint.cs`
  227). That relies on the rout coming at once and the delete only after the visible fade, which Lens 2 read in native code. If a
  delete ever came first, a mumakil's archers would stay on the deck navmesh 9 to 13.8 m up. Watch for `mumakil gone: releasing seats`
  and the crew's height.
- **Where the crew go.** Released archers join the side's Ranged formation, which is lane 1's in Fords, and they are outside the unit
  registry (no bounty, no tally). That was already the case when a beast died; it now happens every time a crewed beast loses its
  rider. FOLLOW-UP: no issue: Mike waived issues for Fords of War, 2026-10-31.
- **A player's pending mount.** The guard reads `MountAgent` and the selected mount index. Whether native sets either as soon as a
  player starts to mount was not read (Codex, Lens 2); the same gap existed for horses.
