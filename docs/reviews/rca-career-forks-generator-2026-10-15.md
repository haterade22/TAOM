# RCA: career forks generator, C1 (#769), 2026-10-15

## Top-line

`tools/generate_career_forks.py` writes a kingdom's forked career trees from one JSON spec into
hash-guarded regions of the two career XML files, after a balance gate. `/deep-review` ran four lenses on
it (tooling, XML, engine, data flow; the data-flow agent never reported, and the convergence review
covered its ground) with the efficiency and design questions folded into the tooling lens. Three
findings were HIGH, all latent because no spec had been applied: TroopMorale was priced as a count
though the runtime applies it as a factor; the balance gate ignored the sign of a magnitude, so a
penalty passed as a perk; and a second generation could change a shipped tree, or lower its version,
without the version bump that refunds saves. A fix round, a convergence review and a separate review of
the translator's new `--seed-only` mode followed.

The root of most findings is one pattern: **the tool kept its own copy of facts the runtime owns** (which
types are flat, class-filtered or mask-gated; which sign helps the player; which troops pay upkeep), and
each copy was already wrong or drifted while the review ran.

## Findings and root causes

| # | Sev | Finding (lens) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `TroopMorale` was priced at 2 per value unit (a count) but is a factor on base morale 50; the gate refused a sane +10% and passed +600% (XML H1, tooling F2, engine F1) | Unit copied without its consumer | Units were set by analogy with nearby types; nothing related a unit to its consumer's kind | Unit 0.05; `test_units_match_the_runtime_kind` (non-flat units below 1, flat units 1 or more, flat set read from C#) |
| 2 | HIGH | The balance gate priced `abs(magnitude)`, so a wage rise, a doubled upkeep or a smithing cost increase passed as a perk; two tests pinned the wrong sign (tooling F1, engine F3, XML M6) | Sign not modelled | The runtime's "negative is a cut" convention lived in comments, not in the rules | A `lower_is_better` list; benefit-signed pricing and caps; a penalty is refused |
| 3 | HIGH | A regenerated tree could change structure at the same `tree_version`, or lower it, so saves kept picks the new tree forbids (tooling F3, XML H2); the first fix still ignored a mask or class change, and a hand edit of the marker's recorded shape passed (convergence G1, G8) | Destructive path gated by convention only | The trap was documented for authors; the tool that writes the version did not enforce it | A versioned structure fingerprint in the marker covering types, masks and classes, hashed with the body; a changed shape needs a higher version; a lower one is refused |
| 4 | MED | Hand copies of the C# sets (`class_filtered`, `flat_types`) had drifted; the generator wrote XML the C# shipped-data tests reject (a mask on a non-mask-gated type, a mask on troop damage or resistance, an empty root, an unconsumed type) (tooling F4, engine F2, XML M1, M2) | Second source of truth | The enum was parsed from C#; the sets beside it were typed in | The sets are parsed from `PassiveEffectConsumers.cs` and `CareerPerkConsumerMap.IsFlat` at run time; the C# rules are refused up front |
| 5 | MED | Resource and upkeep were trusted from the spec; after the fix, upkeep troops were counted by a `resource_id` tag the runtime never reads, which let 12 careers take an upkeep perk the plan forbids (tooling F5, XML M7, convergence G2) | Data documented as "documentation only" used as data | The first fix derived from the wrong field of the right file | Upkeep troops are the cost rows whose troop's culture is in the career's eligible cultures |
| 6 | MED | The shipped rules refused the plan's own worked example (renown and influence at 6 VU against a tier-1 budget of 3) (tooling F6, XML M8, engine F8) | Plan and rules diverged | The units were drafted before the bands were priced | Units, bans and caps decided under the maintainer's overnight delegation and recorded in `_rules.json` with a note per unit; a worked-example test |
| 7 | MED | The localization steps after `--apply` were undocumented, and the free English seeding existed only as a session script; English changed under an existing key went stale silently (XML M4, M5, tooling F7) | Missing pipeline step | The generator was reviewed as a writer of XML, not as the first step of a pipeline | The tool prints the steps and lists keys whose English changed; `translate_with_claude.py --seed-only` seeds English without an API client |
| 8 | MED | Nothing ran the drift gate (XML M9, tooling F8) | Gate with no caller | `--check` existed; no test called it | A tools test runs `--check` for every spec |
| 9 | MED | `--seed-only` skipped the install-root check the README promises and treated `--module TAOM_Map` as success (seed-only review F1) | New entry point without the old one's guards | The mode was written as an early return before the guards | Both checks added, with tests |
| 10 | LOW | Uncaught exceptions exited 1 (the drift code); the cap search forced a pick in every fork though picks are voluntary; two writes were not atomic, then the rename phase was not either; untested guards; ShrugOff priced as a fraction though it is on/off; dead attributes written; braces accepted in text (tooling F9 to F16, XML M3, L1, L3, engine F5, convergence G3) | Tool hygiene | Each was a local shortcut | Exit 2; voluntary picks with a brute-force test; temp files plus restore on a failed rename; a refusal test per guard; ShrugOff banned; dead attributes dropped; braces refused |
| 11 | NIT | Stale doc and rules text; a summary without counts; duplicate summary line in seed-only; tests that could not fail (seed-only F2 to F4, convergence G4, G6, G7, G9, G10) | Polish | | Each corrected |

## Root-cause patterns

1. **A tool that copies runtime facts drifts from them** (1, 2, 4, 5). The generator now reads the
   enum, the type sets and the flat set from C#, and the resource and upkeep from the data. The rule for
   the next generator: a fact the runtime owns is read from the runtime's source, never typed into a rules
   file. Repeat of `build-tooling-workflow.md` "Multi-script pipelines glued by name agreement need ONE
   shared derivation".
2. **A destructive path gated only by a written rule** (3). Third occurrence of the career branch's
   pattern (the E4 to E7 RCA row 13, the 2026-10-08 RCA pattern 2): the tool that can trigger the refund
   now enforces when it must.

## Lessons

- `build-tooling-workflow.md`: "A generator reads every fact the runtime owns from the runtime's source"
  (pattern 1).
