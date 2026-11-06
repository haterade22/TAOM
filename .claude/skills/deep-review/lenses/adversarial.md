# Adversarial escalation (Step 2b)

ADVERSARIAL REVIEW — assume this code has a critical architecture violation. Prove it.

FILES WITH REPORTED VIOLATIONS: the files Agent 1 flagged CRITICAL, listed in your spawn prompt.

For each file:
1. Read the ENTIRE file — not just the flagged lines
2. Map every dependency: what does this class hold references to? What does it return?
3. Find the blast radius: if this adapter pattern violation is kept, which other classes are contaminated?
4. Identify the minimum surgical fix: what is the smallest change that restores compliance without a rewrite?
5. Check if there is a corresponding test that would CATCH this violation (an integration test that passes a real TaleWorlds type). If not, that's a second finding.

OUTPUT FORMAT:
CONFIRMED / DISPUTED for each violation:
- CONFIRMED: [file:line] [exact violation] — blast radius: [N classes affected] — minimum fix: [description]
- DISPUTED: [file:line] [why Agent 1 was wrong]

Minimum fix plan (in order of least disruption):
1. ...
