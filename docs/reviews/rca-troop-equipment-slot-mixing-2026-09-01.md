# RCA: troop equipment sets are mixed per slot (bow without arrows)

**Date:** 2026-09-01
**Issues:** #529 (the fix), #531 (the deferred cross-set shield class)
**Change under review:** four ModuleData XML files, no C#.

## Top line

A player reported Lindon and Noldor recruits carrying a bow with no arrows, or arrows with no bow.
Every equipment set on those troops was individually valid. The engine does not apply a set whole:
`Equipment.GetRandomEquipmentElements` fills each of the 12 slots from an independently chosen set,
so three ranged sets among thirteen produced a working archer 5% of the time.

The fix was correct and is verified. The review's value was elsewhere: it showed that the sweep
proving the fix was weaker than claimed, and it surfaced a second, larger instance of the same
root cause that is shipping today and escapes the one committed gate meant to catch it.

## The reported premise was wrong in a way that mattered

The report said mixing begins above three equipment sets. There is no set-count comparison anywhere
in the method. Mixing begins at **two**. Had the fix been built on the reported premise, the
obvious remedy would have been "reduce the recruit to three sets", which would have changed nothing.

The number three is real but is a slot-bucket count that applies only on the unseeded path. Campaign
battles never take it, because `CharacterHelper.GetPartyMemberFaceSeed` returns `abs(...) % 2000`.
Correction recorded during review: the `-1` path is not dead engine-wide, `GuardsCampaignBehavior`
takes it for settlement guard visuals, and on that path `Weapon0` and `Weapon1` do share a draw.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | HIGH | 14 troops can draw a shield against a `requires_no_shield` weapon with **neither set malformed**. Four are `Polearm`, the type `audit_polearm_shield_parity.py` ratchets on and promises to fail the build for. It exits 0 | Data + gate | The gate flattens ids **within one `<EquipmentRoster>` element**, and in `troops_*.xml` one element is one set. It asks "is this set valid", which is not the question the engine asks | Deferred to #531 with measurements. Fix is a union-of-sets comparison per troop, keeping per-set for hero rosters |
| 2 | HIGH | The verifying sweep prints CLEAN with an **empty item registry**. `build_item_class_registry` silently returns `{}` when the install is absent, so nothing is classified, no check fires, exit 0. Reproduced with one env var | Tooling | The tool reported findings but never its own reach. "1,463 items, clean" and "0 items, saw nothing" were byte-identical output | Fixed in-session: `ensure_exists` on the modules root, hard refusal on an empty registry, and the registry size is now printed above the results |
| 3 | HIGH | The fixer's `ROSTER_RE` lacked `\b`, so it matched the root `<EquipmentRosters>` and consumed the first roster of the file. `npc_companion_equipment_template_gondor` was never examined | Tooling | The `assert changed == 33` passed, because 33 was the count of what the regex could see. A self-consistent tool cannot detect its own blind spot | Verified benign by measurement (gondor has 0 bow sets), not by argument. Neither script is promoted to `tools/` |
| 4 | MED | The fixer writes file 1 before asserting file 2, so a failure leaves a cross-file partial edit. Its docstring claims "fails closed" | Tooling | True per file, false per run. The claim was written from the per-file structure | Stage-all-then-write-all if either script is ever promoted |
| 5 | MED | The civilian exclusion is a literal substring test (`'civilian="true"' in attrs`) and fix 3 has no civilian guard at all | Tooling | It worked, so it was not examined. Backstopped only accidentally, by `assert changed == 3` | Parse the attribute, do not match its text |
| 6 | MED | The hero exemption derives occupations from one reference form within one module glob. A non-hero consumer reached another way would silently widen the exemption | Tooling | The exemption turned FINDINGS into CLEAN and deserved the hardest scrutiny; it got the least, because its output looked obviously right | Empty-consumer case already fails safe (audited, not exempted). Measured 0 out-of-scope consumers today |
| 7 | MISSING | No GitHub issue existed. CLAUDE.md requires one before implementation | Process | The work started from a direct player report and went straight to research | Filed retroactively as #529, the sanctioned repair path |
| 8 | MISSING | The engine fact was recorded nowhere in `docs/` or `.claude/rules/` | Docs | It was discovered mid-task and written into the CHANGELOG, which is not where authoring rules are looked up | Recorded in `.claude/rules/troops.md`, path-scoped to the troop XMLs so it loads for exactly the edit that would reintroduce this |

## Root cause pattern

**Every validator TAOM owns asks "is this row valid?" The engine reads the table column-wise.**

Findings 1 and the original bug are the same defect at different scales. A troop's battle sets look
like a list of alternative loadouts and are authored that way. The engine treats them as a menu it
orders from once per slot. Any property that must hold for the assembled troop therefore has to be
checked **across** sets at a fixed slot index, never within a set.

Both gates that should have caught something here are row-wise. `audit_polearm_shield_parity.py`
validates one set at a time. `audit_enlistment_roster_coverage.py` implements the exact
launcher/ammo rule this bug needed, correctly, and scans one file. Neither is wrong; both are
scoped to the shape the author had in mind.

This is the third consecutive instance of one lesson already in this category file: **a gate that
excludes the shape the bug lives in reports zero forever.** #526 (closed 2026-09-01) was the gate
that never opened standalone roster files. #531 is the gate that never considers two sets together.
The escalation is that the excluded shape is no longer a file or a category but a *combination*,
which is harder to notice because every input to it is present and valid.

## Why each review agent missed what it missed

The five standard `/deep-review` agents were substituted, because the changeset has zero C# and
agents 1 and 3 (ADR compliance, hot paths) had no surface. That substitution is itself worth
recording: running the standard five here would have produced five clean reports and found none of
the above.

- **Standards / Efficiency (not run):** no C#. Running them would have manufactured confidence.
- **Engine verification:** found the corrections and confirmed finding 1's mechanism. It could not
  measure exposure, because it reads engine code, not mod data.
- **Data flow:** 9/9 clean, and correctly so. It traced the *changed* data outward. Finding 1 lives
  in data that was not changed, so a diff-scoped review structurally cannot reach it.
- **Tooling correctness:** found 2 and 3. These were invisible to every other agent because the
  scripts are in a scratchpad, outside the repo, and outside every rule glob.
- **Adversarial completeness:** found 1. It was the only agent asked to attack the *predicate*
  rather than the change, which is why it was the only one that could.

The generalisable point: **a review scoped to the diff cannot find a bug in the data the diff did
not touch**, no matter how many agents run. Finding 1 was reachable only because one agent was
pointed at the question "what can this check not see?"

## Lessons codified

Appended to `docs/reviews/lessons/data-content-cultures.md`:

- "A troop's equipment sets are a per-slot menu, not a set of alternatives"
- "A 'CLEAN' result describes the predicate, not the data"

Authoring rule added to `.claude/rules/troops.md` ("Equipment sets are mixed PER SLOT, not chosen
whole"), covering the launcher/ammo pairing, the at-least-one-weapon-per-slot-index rule, the
cross-set shield case, `Horse`/`HorseHarness` pairing, the hero exemption, and the fact that no UI
surface can reveal any of it.

No new feedback memory. The durable content is an authoring invariant, and it belongs in the
path-scoped rule that loads when someone opens a troop file, not in session memory.

## Still owed

An in-game battle against a Lindon or Rivendell party after a full game restart. The deployed module
already carries the new data. No UI surface can confirm this fix, so the encyclopedia and party
screen are not acceptable substitutes.
