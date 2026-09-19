# RCA: career kits take the lowest troop gear (#629), deep review 2026-09-19

**Summary.** #629 replaced the 78 career rosters' `starter_` twins with the gear each culture's lowest
troops carry, regenerated the vanilla override from them and taught the two starter tools to leave the
career file alone. The eight-lens deep review (Fable, max effort, two waves of four) found no critical or
high defect in the shipped XML: every id resolves, every gate passed, the override is byte-identical to
its generator's output. It found six medium and a dozen low problems around the change: an engine equip
gate the rule ignored, an early decision the later rule contradicted, two scripts that could undo the
change, retention logic that leaned on the unversioned install, a verification gate that had silently
narrowed itself, and a latent engine merge trap in the wiring tool. All were fixed the same day, four of
them after behaviour decisions from Mike.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Ladder bows in the new kits ask up to 100 Bow (`difficulty`) to re-equip; `CharacterHelper.CanUseItem` refuses below it; the twins asked 0 | Engine gate on player items | The rule and the audit compared level, mesh and power (damage, HP, armour); nobody asked what the engine demands before a player may equip an item | `generate_career_kits.py` picks bows by lowest `difficulty` first (Gundabad moved to t2). Lesson below |
| 2 | MED | The orc careers' borrowed Isengard arrows (40 x 3) beat anything Mordor, Gundabad or Dol Guldur troops carry, against the rule's own premise | Stale decision | "Borrow a kin culture" was answered before Mike's "lowest troop gear" directive and never re-derived under it | Rule 3 now; the test's allowlist deleted. Lesson below |
| 3 | MED | `wire_career_starter_armor.py` and `oneoff/generate_career_starter_rosters.py` could still rewrite the career file with the old kits, every validator green | Superseded writer left live | The docs sweep marked one "do not run" in the README and never found the other; nothing enumerated every writer of the file | Both deleted with their registry row. Lesson below |
| 4 | MED | `retained_donors` rescanned the live install: a reinstall would silently lose the 18 twins, it re-planned the kite shield into a different folder (red `--verify`), and it sorted ids (a re-apply would reorder files) | Save-compat set derived from unversioned state | The fixture's on-disk folder equalled the mapped one and the test compared sorted ids, so neither folder moves nor order churn could show | Committed `RETIRED_DONORS` list, on-disk folder wins, test asserts a re-apply is all `noop` |
| 5 | MED | `generate_starter_kit.py` named the deleted `lotraom-assets\v1.4` mirror; every run printed "not present; live Armory only" and verified one copy while the plan promised both | Silently narrowed gate (pre-existing) | The orchestrator read the "live Armory only" line as information, not as the gate covering half of what it claims | Default is `_gamedir.ASSET_REPO` (`v1.5`); test pins it. Five sibling tools carry the same literal: #630 |
| 6 | MED (latent) | An override roster whose id vanilla lacks merges into the FIRST vanilla roster (`MBObjectManager.cs:846`, `:857-860`, read from the installed 1.5.3 DLL), not appends; a new youth title would trigger it with every test green | Engine merge semantics (pre-existing) | The #569 design documented the replace branch for a matching id only; nothing recorded the no-match branch | `wire_starter_kit_rosters.py` refuses such an id; the fact is in `docs/modding/equipment-rosters.md` |
| 7 | MED | No pin that the committed override equals the tool's output; the "unresolvable donor is not retained" branch untested; issue #629 not updated | Missing pins | Tests covered the builder on fixtures, not the committed artefact; retention had only its positive test | Both pins added; issue #629 updated by comment |
| 8 | LOW | Test read troop gear more narrowly than the engine (no `<Equipments>/<equipment>` overrides); no pin on `culture=` or `_m`/`_f` equality; imports inside a test; docstrings ("file order" over a sort; a reflow that made the career file the subject of the Horse sentence); `SWORD_SLOT` holding an axe; a one-caller wrapper; a duplicated prefix helper; header overclaim; stale test counts and SubModule line numbers; CHANGELOG entry without Save-compat/Not-tested | Hygiene | Written fast after scope changes mid-task | Fixed in place; no rule needed |

The pick computation itself held: `generate_career_kits.py`, written from the documented rule, reproduced
382 of the 390 hand-applied ids, and the other 8 are exactly findings 1 and 2.

**Convergence pass** (one reviewer on the applied diff): no behaviour-parity or standards defect. It re-proved
that the plan puts 127 distinct twins in their on-disk folders and order on both Armory copies, that the
retired list equals the pre-#629 career-only twins in set and order, and that `apply_kits` cannot reach a
mount slot or another roster. Four LOW items, all fixed: the new tool swallowed an unparsable Armory file
(now reported, with a test), the drift bullet omitted the crafting-pieces file, a stale `SubModule.xml` line
in an adjacent catalogue row, and a present-tense mention of a deleted script in `open-questions.md`.

**Left for #630** (pre-existing, out of #629's reach): five more tools naming the `v1.4` mirror, the
generator's id-only `--verify` against twin content that has drifted since #569, a live-before-mirror apply
order, and override civilian sets built from non-civilian items.

## Root-cause pattern

Three findings (1, 2, 4) share one cause: **the task's governing rule changed twice mid-session** ("vanilla
replacements", then "lower than regular gear", then "the lowest troops' equipment, nothing new"), and each
change was applied to the new work but not re-run against what earlier steps had already decided or built.
The kin-borrow arrows and the retention design were both correct for the rule in force when they were
made. Finding 3 is the same pattern at file level: the change made two writers wrong, and the sweep looked
for docs that described them rather than for code that could still run.

## Why each agent missed nothing and the author missed these

The lenses found every item above; the misses were the author's, before the review:

- **Finding 1**: the per-slot rule was stated in terms of level and vanilla-ness because that was the
  user's question. Stats were compared in the audit (damage, HP, armour) but never the equip requirement,
  which only matters for a hero.
- **Finding 5**: the generator's "asset repo not present" line appeared in every run and was reported
  upward as "mirror not on this machine", an environment fact, when it was a stale constant in the tool.
- **Finding 6**: the engine fact was already half-documented in `starting-equipment-tuning.md`; the half
  that is dangerous lived only in the decompile.

## Feedback memories to codify

None new. Findings 1 to 3 go into `docs/reviews/lessons/data-content-cultures.md`; finding 5 is an
instance of the existing `evidence-over-claims.md` §B ("a gate that cannot run has not passed").
