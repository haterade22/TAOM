# Tooling correctness lens

TOOLING CORRECTNESS REVIEW for scripts under `tools/` and `.claude/hooks/`. The C#-centric lenses
do not read scripts, and a script bug corrupts live data or reports clean while checking nothing,
silently either way.

FILES: the list in your spawn prompt. Read each script whole, its tests, and the sibling whose
conventions it copies. Read `tools/README.md` "XML I/O convention"; for hooks, read
`.claude/rules/hook-authoring.md`. Scripts spell the interpreter `python`; the 3-suffixed name is
for `.github/workflows/` only (hook-authoring.md "Never spell it": the Store alias hangs Git Bash).

FOR A SCRIPT THAT WRITES FILES (especially outside the repo, e.g. the live `TAOM_Map` or Armory):
1. Encoding and BOM preservation: detect with `read_bytes().startswith(b"\xef\xbb\xbf")`, decode
   `utf-8-sig`, write `write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))`;
   never a U+FEFF string literal, never a plain `utf-8` read of a BOM file; CRLF and non-ASCII kept.
2. Idempotency: a second run changes nothing and corrupts nothing.
3. Dry-run vs apply: nothing is written without `--apply` (or the tool's write flag).
4. Backup before a destructive or external write, never with an `.xml` extension in a globbed folder.
5. Regex over- and under-match: an attribute edit cannot partial-match a larger token.
6. Case-insensitive scene, asset and id comparison, as Windows lookup is.
7. Match-count reporting, so a human can sanity-check the run.
8. Parse the transformed XML before writing it, and refuse to write a document that no longer parses.

FOR EVERY SCRIPT, READ-ONLY GATES INCLUDED:
9. Silent clean: can it report PASS while checking nothing (a missing input folder, a wrong root,
   zero files found, an import that failed, a path the caller passed that does not exist)? A gate
   that checks nothing must say so and must not exit 0.
10. Exit-code contract: every documented code is reachable and nothing else is; an uncaught
   exception (a traceback exits 1) must not read as a validation failure.
11. Optional dependencies (lxml and the like) degrade loudly, and the tests skip rather than
   error when the dependency is absent (CI's tools-tests job installs nothing).
12. Tests: each guard tested in both directions on a synthetic tree; every test asserts what its
   name claims; run them and quote the result.
13. For hooks: the timeout is measured against the slow path, the hook fails open but never
   silent, and a hook that consumes another hook's output has a two-direction case in
   `tools/test_hooks.sh`. Run `bash tools/test_hooks.sh`.

WHY: deep-review 2026-05-28 (scene tooling): BOM handling differed across a new script family and
no core lens saw it (RCA `docs/reviews/rca-scene-tooling-2026-05-28.md`). Deep review 2026-09-18:
a new read-only XSD gate reported PASS on a nonexistent path and on a DOCTYPE the engine loads as
nothing, and the tooling lens had not fired because the script wrote nothing.

OUTPUT: each finding as file:line, severity (HIGH if a gate can pass a broken input or a writer
can corrupt data), the evidence you ran or read, the fix as a code sketch, Behaviour PRESERVING or
CHANGING, Scope APPLY or FOLLOW-UP, and the test that proves the fix. Then the tests you ran and
their result.
