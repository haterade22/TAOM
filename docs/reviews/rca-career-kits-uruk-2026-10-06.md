# RCA: career kits in another kingdom's armour, ranged careers with the wrong weapon (2026-10-06, #743)

**Summary.** Mike found the Isengard career kits dressing the player as a Mordor orc. The Mordor careers
had the same fault, and so did three ranged careers named for a weapon they were not handed (Uruk
Crossbow, Crossbow Master, Pezarsani Javelineer). The Gondor kits had drifted off their rule for four
releases. All four trace to two gaps: `tools/generate_career_kits.py` (#629) checked its picks for level
and vanilla-ness but never against who the career is, and the pin that keeps the file on its rule runs only
when someone runs it by hand.

The `/deep-review` of the fix (lenses 2, 3, 4, 5, 6, 7 and tooling; no defect in the shipped picks) found
the remaining items below in the fix itself.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Isengard and Mordor careers start in `sk_md_orc_*` armour carried by their level-1 orcs | data rule | #629 judged each pick by level and vanilla-ness only; its own table listed the orc armour and its review approved it | Lesson below; `ARMOUR_LINE` restricts the pool, pinned per culture by literal-culture tests |
| 2 | MED | Uruk Crossbow, Crossbow Master and Pezarsani Javelineer start with a bow | data rule | one ranged layout (bow and arrows) for every culture; nobody read the career's name against its kit | `RANGED` table, one test per career |
| 3 | MED | Gondor career kit drifted from v2.0.31 to v2.0.34 | gate gap | `998f054c` (#669) re-kitted Gondor's low troops without regenerating; the `--verify` pin is install-gated and no hook runs it | `--verify` added to the ModuleData commit hook (separate commit) |
| 4 | LOW | First fix added two mechanisms (an own-culture tier and `armour_line`) that moved the same six picks | simplicity | the second mechanism was added for Mordor without asking whether it made the first redundant | Design lens; one pool restriction kept, the tier deleted |
| 5 | LOW | First fix's history line and #743 blamed `ea0592eb` for the Gondor drift | evidence | `git log -L` on the career file showed the last edit, not the commit that moved the rule's input | Completeness lens re-derived the pick per commit |
| 6 | LOW | Stale rule text in the career XML header, `tools/README.md`, `career-system.md`, and 4 stale `KNOWN_FAILURES` entries | doc drift | the generator rewrites slot ids only, never prose; the ratchet entries went stale when `ea0592eb` changed the polearm | Updated in the same change |

## Root-cause pattern

Findings 1 and 2 share one shape: a generated kit was checked for internal consistency (lowest level,
non-vanilla, re-equippable) and never for fitness to the career it belongs to. A rule that is right by its
own measure can still hand an uruk career orc armour or a crossbow career a bow. Finding 3 is the generator
twin of every unversioned-module lesson: an output pinned by a gate that runs only on request drifts as soon
as its input changes in another commit.

## Why each agent missed these (in #629's review)

- **Data flow and XML lenses** traced the roster to the player and every id to its definition; both were
  correct, so they passed.
- **Completeness** checked that every career had a roster, not what the roster said about the career.
- **No lens** compared the kit with the career's name, description or the race the player wears it on.

## Feedback memories to codify

None; the lesson goes to `docs/reviews/lessons/data-content-cultures.md`, and the gate to the hook.
