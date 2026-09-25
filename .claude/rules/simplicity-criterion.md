---
description: Keep-or-reject matrix for a change. A tiny gain with added complexity is rejected; a deletion that holds parity always wins.
---

# The simplicity criterion

Judge a change on two axes at once: how much it helps, and how much complexity it adds.

| Situation | Verdict | Why |
|---|---|---|
| Tiny win plus added complexity (an abstraction, a helper, a flag) | **Reject** | Readers pay forever for a benefit nobody notices |
| Equal result, simpler code | **Keep** | A pure win |
| Deletion that holds parity (tests green, behaviour unchanged) | **Always keep** | The highest-leverage change there is |
| Improvement large enough to dominate its cost | **Keep**, and state the trade-off in the PR or commit body | So the next reader can re-weigh it |
| Code "in case we need it later" | **Reject** | YAGNI |

To apply it, to your own change or inside a review: state the win in one sentence, state the cost in
one sentence, match the table. A **Reject** does not ship, however correct or idiomatic.

This asks whether code is redundant. `/improve`'s deletion test asks a different question (is an
abstraction shallow?); don't conflate them.
