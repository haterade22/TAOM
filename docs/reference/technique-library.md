# Technique library

This page was the first technique table, written on 2026-11-26 after the first teardown of a reference model. Its
rows and its teardown log moved the same day to the knowledge store, where a check holds them true:
[knowledge-library.md](../features/knowledge-library.md). The steps for a new teardown are the `/teardown` skill
([SKILL.md](../../.claude/skills/teardown/SKILL.md)). Nothing is added here.

## Where each row went

The teardown log row T1 (the neck bands and head shell of horse armour 5, against two image to 3D models) is the
record [2026-11-26-horse-armour-neck-and-head.md](../knowledge/teardowns/2026-11-26-horse-armour-neck-and-head.md).
It also holds the property list for a teardown and the list of what ours has and the reference does not.

| Old row | Entry in `docs/knowledge/techniques/` |
|---|---|
| L1, an even rhythm in a stack of bands | `band-stack-even-rhythm` |
| L2, a round bead on every free edge | `bead-on-every-free-edge` |
| L3, a closed lap | `band-stack-closed-lap` |
| L4, a smooth outline | `outline-geometry-where-it-shows` |
| L5, band ends that taper | `band-end-rounded-and-level` |
| L6, one head shell | `head-shell-one-piece` |
| L7, armour that closes onto the body | `liner-and-skin-view-under-moving-plates` |
| L8, one smooth field per part | `one-smooth-field-per-part` |
| L9, symmetry, measured | `centre-part-symmetry-by-number` |
| L10, ratios from a logged probe | `reference-ratios-from-a-logged-probe` |
| L11, two prototypes before a costly technique | `two-prototypes-before-a-costly-technique` |

Read an entry with `python tools/knowledge.py show <id>`. Each entry says how the technique acts today.
