---
paths:
  - "tools/**/*.py"
  - "tools/**/*.ps1"
---

## Writing a data-mutating script? Read `tools/README.md` "XML I/O convention" FIRST

This rule now loads on `tools/**/*.py` and `tools/**/*.ps1` for one reason: the byte-faithful XML I/O
convention lives in `tools/README.md`, which **nothing auto-loads**, and the paths above previously
covered only *repo* ModuleData — so a script editing `Modules/<Mod>/ModuleData/*.xml` in the game
install loaded no convention at all. That gap has now produced the same defect **three times**
(scene tooling 2026-05-28, a scratchpad one-off, and `fix_uruk_hai_hands_teamcolor.py` 2026-08-06).

**The two sanctioned idioms — pick one, never mix them:**

```python
# A. utf-8-sig decode + explicit BOM re-prepend
had_bom = path.read_bytes().startswith(b"\xef\xbb\xbf")
text = path.read_text(encoding="utf-8-sig")
path.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))

# B. full binary round-trip (BOM survives inside the string)
text = open(path, "rb").read().decode("utf-8")
open(path, "wb").write(text.encode("utf-8"))
```

**Forbidden:** the mixed shape — plain `utf-8` text read plus a text-mode write. It silently strips a
BOM *and* normalises CRLF→LF, turning a two-attribute edit into a whole-file rewrite.

Also mandatory for any script that writes outside the repo: a backup before the destructive write
(**never** a `*.xml` extension — these folders are globbed, and an `.xml` backup injects duplicate
item ids), a dry-run default with an explicit `--apply`, idempotency on re-run, and exact-token
comparison rather than substring containment when deriving a target set from a report or index.

**And parse the result before writing it.** Any script that transforms XML must run the transformed
text through `ElementTree` and refuse to write a document that no longer parses. On 2026-08-28 a
swap script masked XML comments by byte offset and restored them at those offsets; the swap changed
the text length, so comments were spliced into the middle of item ids and 8 ModuleData files were
written malformed. Nothing detected it until a hand-check afterwards, and the directory-wide revert
used to undo it destroyed a concurrent session's uncommitted work. The parse costs microseconds and
makes the whole class unshippable. Corollary for masking: restore by TOKEN (an indexed sentinel),
never by recorded offset, because any length-changing edit between mask and restore invalidates
every offset.

> **A blocking lint was evaluated and rejected (2026-08-06):** 92 of 124 XML-writing scripts trip a
> naive mixed-shape heuristic, so a build gate would fail on pre-existing debt and the heuristic
> false-positives on read-only analyzers. Loading the convention at authoring time is the effective
> control; the deep-review Tooling Correctness agent (`.claude/skills/deep-review/SKILL.md` Step 2c)
> remains the review-time backstop.
