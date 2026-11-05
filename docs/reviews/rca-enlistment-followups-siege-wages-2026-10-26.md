# RCA: Siege duty for defenders, the wage order, the column check cache (#1014, deep-review, 2026-10-26)

## Top-line

`/deep-review` on #1014 ran one reviewer across the data-flow, engine and standards lenses, then a
convergence pass. It found **no defect in the C#**: the defender siege context matches the engine's own
notion of a party in a siege, every branch of the wage book balances, and the cache removal leaves no stale
reader. It found **one regression in a tool** introduced by the change and a set of documentation errors,
all LOW, and all fixed before the push.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | LOW (Data flow) | The duty text tool's new comment masking blanked comments with spaces, so the row pattern's leading whitespace could run across a comment on the same line as a rewritten row and delete it. No comment in today's file has that shape. | A mask that changed what the next pattern matches | The mask was checked for equal length, not for what the following pattern would consume. | Comments are blanked with a non-space filler that keeps line breaks; a test proves a same-line comment survives. |
| F2 | LOW (Engine compat) | The corrected claim (the menu link rewrite runs every frame, not once per menu open) was fixed in two code comments but stayed in two docs, one of which used it as the reason a cache was dropped. | A moved fact left in other files | The issue scoped only the code comments. | All three doc sites corrected; the cache stays dropped for its real reason (stale culture colours). |
| F3 | LOW (Standards, Data flow) | A stale paragraph placement for a reset that now happens, an em-dash aside in rewritten prose, a caller list written three times, an in-method comment where a named value says it, and a third copy of a formula in a test. | Accuracy and duplication | Routine. | Each fixed; the test now asserts the compiled defaults pass the provider's own check. |

Noted, not changed: the defender term counts any party inside a besieged town, including a guest at peace
with the besiegers; vanilla's own army and AI code treat that set as "in the siege", so the rule matches it.

## Feedback memories to codify

None.
