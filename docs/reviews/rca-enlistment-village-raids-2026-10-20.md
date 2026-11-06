# RCA: Enlistment skips the commander's village raids (#831, deep-review, 2026-10-20)

## Top-line

`/deep-review` (seven lenses in two waves, then one convergence pass) on #831 found **no API
incompatibility and no regression**: every engine member the change reads exists on v1.5.5, and every
join and latch site gives the same answer as the planned rule. It found **one MEDIUM design gap that
is now a recorded decision for the maintainer**, one MEDIUM wrong engine claim, and a set of LOW
documentation, test and shape findings, all fixed before the first commit.

The finding that mattered: the change decided "is this a battle to join" from the map event's type and
the commander's side. A Raid event is not always a raid. A lord who comes to relieve the village
attacks the raider inside the same Raid event (`StartBattleAction.Apply` keeps the type `Raid` when the
defender is already raiding, `StartBattleAction.cs:116-118`, and puts the attacker on the existing
event's side, `:30`). The commander stays the attacker in a Raid, so the soldier sits out a real lord
battle. Joining that fight is not a one-line change: if the relief lord retreats instead of falling,
the raid can continue with the soldier inside it, which is the #831 symptom again (UNVERIFIED in
game). The gap is recorded in the feature doc and in the owed in-game checks; the follow-up waits on
the maintainer.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Engine compat, Data flow) | A relief lord attacking the raiding commander fights inside the same Raid event, so the soldier sits out a real battle. | Event type used as a proxy for "is there a fight" | The plan read how a raid starts (`ApplyStartRaid`), not every way a party can enter a running raid. | Recorded as "Known gap, decision pending" in `enlistment.md`, plus an owed in-game check. The single reader makes the later change one function. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | MEDIUM (Data flow), LOW (Engine compat) | The policy summary and the doc said a militia-defended raid "moves to looting when the militia falls". The engine ends that event and the raider opens a second raid (`MapEvent.cs:897`, `RaidEventComponent.cs:279-282`, `:316-324`). | Engine claim written from expectation | The sentence explained the outcome, which is correct, with a mechanism nobody traced. | Sentence replaced with the traced mechanism in the code summary and the doc. |
| F3 | LOW (Design) | A 12-value mirror of `MapEvent.BattleTypes`, an int cast with a value-pinning test, a second derivation of the attacker side and a three-input predicate at six sites, where every consumer read only one combined fact. | Data carried wider than its consumers need | The kind was carried "in case", and the floor derived the side a second way. | One `VillageRaidReader.IsRaiding(MobileParty)` read by both snapshots and by the join floor; mirror enum, reader, binding test and per-kind matrix deleted. |
| F4 | LOW (several) | Forced supplies and forced volunteers described as commander actions (they are player-only); "(hours)" for a raid that lasts tens of in-game hours; "no pending flag" true for Assess only (the running-event edge still arms an inert flag); the floor comment said "this event" for "any event"; 'Abandon Army' in the attached edge ends the whole raid; the registry and the exits table overclaimed "only"; the defender-side control dropped from the owed check. | Doc and comment accuracy | Written for the planned design, not re-read after the engine trace. | All corrected; the defender-side control restored in both check lists. |
| F5 | LOW (Completeness, Efficiency) | The adapter fills were unpinned; the Assess verdict for a raid while the player is in an event was unpinned; the TICK line hid the raid; two duplicate gate tests; `party?.MapEvent` read four times per snapshot; the floor ran the lookup before the cheap check; one test control varied two inputs. | Test and polish | Various. | Source pin on both snapshot constructions, an Assess row, `raiding=` on the TICK line, duplicate tests deleted, one read per method, cheap check first, single-variable control. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources. It proved that
`PartyBase.Side == Attacker` equals the old reference check against `MapEvent.AttackerSide` (both
fixed in the side's constructor, `MapEventSide.cs:234-236`, created only at `MapEvent.cs:784-785`,
saved with the event), including for army followers, and that every old call site gives the same
answer through the single bool. It found three LOW doc overclaims and two nits, all fixed. It named
one pre-existing path outside #831 (a player in someone else's map event falls through to the join and
its rollback); reachability is UNVERIFIED and it is not changed by this work.

## Root-cause pattern

F1 and F2 share a cause: the design reasoned about a map event from its type at creation, and the
engine reuses one event for several fights. A Raid can hold a militia fight, a relief battle and the
looting, each with different stakes for an attached soldier.

## Why each lens caught or missed these

- **Standards:** the binding-test pins and the test path helper; no violation.
- **Engine compatibility:** F1 and F2 from the engine source, every member verified on v1.5.5.
- **Efficiency:** no cost in the shipped diff; set the no-allocation shape for any defender read.
- **Completeness:** the undocumented gap, the owed checks, the unpinned fills and the stale registry.
- **Data flow:** F1 and F2 independently, plus the inert pending flag and the TICK line.
- **Design:** F3, and the risk that joining a relieved raid brings the symptom back.
- **XML:** no findings; all 13 files parse with one row each.

## Feedback memories to codify

None; the lesson below covers the pattern.
