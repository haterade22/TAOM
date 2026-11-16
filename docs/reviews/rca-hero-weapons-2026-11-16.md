# RCA: the heroes' weapons (#1373) and a refuted fix for the story mission's churn (#1370)

Findings from the tier 1 review of the uncommitted worktree `feat/hero-companions` over a4cedd4c2. The tree held two changes: the heroes' weapons (#1373) and a fix for #1370. The first wave ran four lenses (5 Data flow, 2 Engine compatibility, 7 XML and ModuleData, Tooling) and each of them reported a HIGH against the fix for #1370; that fix was removed whole and never committed. A second wave (1 Standards, 3 Efficiency, 4 Completeness, 6 Design) read the reduced tree. The Codex pass did not run: the maintainer said to skip it while the account has no usage.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The fix for #1370 could not work, and the cause it rested on was wrong. The investigation had measured the mechanism (the engine's castle tactic consolidates the Infantry formation into the heroes' off-AI formation about once a second during the preparation) and then named a cause for why only the story mission shows it: the Long Defeat's opening split. The Long Defeat's own logs refute that: it stands through a muster of about 24 seconds with no split and shows no repair. The split also starts only after the muster, where most of the churn already happened, and it would have held the garrison for the whole one-wave battle | A cause named from the difference that was looked for, with the control case not read | The debug run was made in the failing mode only. The difference between the two modes was read from the plan code (one has a split, one has none) and never checked against a log of the mode that works, although seven such logs were on disk | The fix is removed; #1370 is open with what is measured and what is not known. The check that is red on the bug is in `tools/ingame_checks/1370.toml`. The next step is the same instrumented run in both modes. Lesson 1 |
| 2 | MED | A key added with the axe, "every swing of Gimli's rage breaks a block", was written as a replacement for the crush-through flag of his old axe. It was a new buff: the campaign's damage model never reads that flag, and the Custom Battle one reads it for an overhead swing only | A change beyond the request, justified by a parity that did not hold | The loss was read from the item data (the flag is gone) and the gain from the name of the channel, with neither damage model read | The key is removed. The feature doc states what the axe changes in each mode and names the key as the maintainer's option |
| 3 | MED | The chosen axe is above the melee ladder's class limit (swing 140 against 123.2), and the ladder's documented repair would lower its blade, which troop items share. A manifest claim pinned "swing 140" | A pick by one number, without the gates that read the same item | The pick ran the damage catalogue and not the validator | The doc names the limit and what the repair would do; the claim no longer pins the number |
| 4 | LOW | The Gimli axe rows of the new manifest failed on any failed spawn check; the row for #1370 re-matched the whole hunters line where it judges one field; the issue text named a field the code does not write; test names claimed more than the tests prove; the companion's doc did not say that an old save keeps the old axe; two manifests kept a placeholder commit | First-draft gaps | Each is a line or a pattern | Fixed in place, each with a test where a pattern changed |

Not applied:

- No forced two-handed mode for Aragorn. His sword's one-handed mode needs a shield he does not carry; the new `weapons=` field of the hunters line will show the mode he holds in a run.
- The eight row ids of `tools/ingame_checks/1373.toml` stay labels that no C# writes, as in four older manifests.

FOLLOW-UP (#1370): the cause of the churn in the story mission.

## Root-cause pattern

Finding 1 is a cause accepted because it explained the failing case. The case that works was never asked the same question. Findings 2 and 3 are the same habit in small: a claim about an effect ("this key gives back what the axe lost", "this is the best axe") made from one source, with the code or the gate that decides the effect not read.

## Why each reviewer missed these

- **The investigation** made one instrumented run, in the story mission, and read the Long Defeat from its plan code.
- **The first builder** built the fix as specified and noted honestly that it had not re-read the engine.
- **Lens 5, lens 2 and lens 7** each opened a Long Defeat log and found the muster window with no split and no repair. **The tooling lens** counted from the story log that the split would leave 20 of the 24 repairs in place. **Lens 6** read both damage models and found that the crush flag matters in one mode only.

## Lessons recorded

One entry in `docs/reviews/lessons/testing-qa.md`, dated 2026-11-16 with this file as its source.
