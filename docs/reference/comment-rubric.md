# Comment rubric and ratchet

How TAOM applies [ADR-009](../adrs/009-self-documenting-code.md) amendment rule 1 (the keep-list) to
code comments, and the gate that holds it (#1501).

## The gate

`CommentRatchetGateTests` (`TAOM.Tests/Infrastructure/`) counts the lines that hold a `//` or `/* */`
comment in each `.cs` file under `Main`, `Client`, `CoopCompat` and `Dependencies`. A `///` XML doc
comment is not counted; `////` is. The count rule is `RepoPaths.CommentLineCount`, and
`tools/comment_ratchet.py` counts with the same rule.

- A count may only fall. A higher count, a lower count and a stale entry each fail.
- The counts are in `tools/comment-ratchet-baseline.txt`. After a sweep, run
  `python tools/comment_ratchet.py --regen` and commit the baseline with the sweep.
- A new file starts at 0. A new keep-list comment raises its file's baseline in the same change, so
  review sees it (lens 9a).
- `python tools/comment_ratchet.py --report` prints comment lines per feature folder and the total
  (`--top 0` lists every folder).
- Known limit: the count is not string-literal aware, so `"http://..."` in a string counts. The
  baseline uses the same rule, so the ratchet stays consistent.

Test comments and XML or XSLT comments are outside the gate: test comments carry effect claims
(`.claude/rules/tests.md`), and `.claude/rules/xslt.md` asks for classification comments.

## Rubric

Used by the `/delete-comment` sweep and by `/deep-review` lens 9a.

| Category | Test | Action |
|---|---|---|
| Tautology | Repeats the name or the next line; section dividers | Delete |
| Stale | Names a plan, phase, review round or symbol that is gone; commented-out code | Delete |
| Excuse | Explains a workaround that TAOM could remove by changing its own code | Delete; list it in the folder's issue |
| Decision record | A non-obvious TAOM choice | Move to the feature doc, or an ADR for an architecture decision; a "must stay" becomes a test |
| Engine fact | Behaviour forced by TaleWorlds or a vendor, naming the constraint and its citation | Keep |
| Marker, licence, contract | A gate reads it (`swallow-ok`); legal text; an interface `///` summary | Keep |

An engine fact stays in the code, unlike a decision record: it guards the line under it, and a copy in a
doc would separate the fact from the code it explains. To tell an engine fact from an excuse, ask whether
TAOM could change its own code so that the comment is not needed. If it could, the comment is an excuse.
