# RCA: the MithrilForge adoption's deep review (2026-09-29)

**Summary.** `/deep-review` of the MithrilForge second adoption pass (docs plus one offline gate) confirmed 19
findings, two of them HIGH. The larger one is pre-existing and was only repeated by this change: the four camp and
refuge props in `Main/_Module/AssetPackages/` have rendered for nobody since they were committed on 2026-08-22. The
editor-built releases ship only `pack0.tpac`, which holds none of them, and the dev install reads TAOM's loose
`Assets/` tree instead of `AssetPackages/`. Both facts were verified by the orchestrator: the release TOCs were read
(120 items, no Metamesh) and `rgl_log_73032.txt:203` logs `Loading packages $BASE/Modules/TAOM/Assets...`. The
first draft of the adoption docs called the props shipped and said an in-game smoke would settle whether
hand-written packages load without an RDC entry. The second HIGH was the gate: its regex could be fooled into
passing while the code asked for a mesh no package held.

Lenses: 2, 3, 4, 5 and Tooling ran; 6 (Design) failed on the weekly usage limit and its judgment was made by the
orchestrator; 1 and 7 were not in scope (no C#, harness or XML). No Codex pass.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Docs said the camp props ship and load; they render nowhere (pre-existing delivery gap, repeated) | Unverified claim | Written from where the files sit and from MithrilForge's "AssetPackages/ is auto-scanned", never from a client log or a release folder | Lesson "Trace the load path before calling an asset shipped"; docs rewritten; delivery decision owed |
| 2 | HIGH | Gate regex took the first textual match and a hand table: a commented-out old line or a dead constant passed | Test that cannot fail | The negative check covered one case (a removed package); the code-to-table link was never attacked | Line-anchored, exactly-once regex plus a `PlaceCenteredPrefab` call-site scan; three negative cases re-run |
| 3 | MED | Gate errored, not skipped or ran, without `lz4`/`xxhash` | Dependency | `tpac_clone_metamesh.py` imported both at module level although `parse` needs neither | Lazy imports; gate runs with both blocked (5 of 5) |
| 4 | MED | Nothing runs `tools/tests` on `bannerlord-1.5.x` | Unwired gate | CI's `python-tests` triggers on `bannerlord-1.4.5` only | Recorded as owed (a CI change, the maintainer's call) |
| 5 | MED | "Data alignment differs by asset kind"; "a clip carries no segment" | Over-generalisation | Four TAOM files and one Kit clip type were read as engine rules; vanilla was not opened (all 6,177 vanilla clips carry a segment; `map_icon_parts.tpac` is packed) | Repeat of the 2026-09-28 lesson "Measure the vanilla asset before building a fix by analogy"; text corrected |
| 6 | MED | "The #506 smoke settles the RDC lead" | Wrong inference | A failure has five candidate causes (tree selection, alignment, no binding segment, zero checksums, no RDC); only a success isolates anything | Recipe records the four measured differences; lead kept open |
| 7 | MED | `module-taom.md` called them "4 cooked packs" read by players | Stale doc | Pre-existing row not re-checked when the facts changed | Row corrected |
| 8 | MED | The owed render check has no tracker: #506 and #507 closed without `triage-needs-ingame` | Process | Pre-existing; the docs pointed at a closed issue | Owed item names #675 (labelled) and the label decision, on the maintainer's word |
| 9 | LOW-MED | "The two readings of `UnknownUInt2` agree" | Overstatement | Section 3 of the system map still records them as disagreeing, and the reading has uncomfortable consequences for TAOM's Loading Type 2 clips | Reworded as an unverified possibility with both counter-facts |
| 10 | LOW-MED | "A metadata-only clone plays nothing (duration 0)" | Overstatement | True of MithrilForge's out-of-Kit clips; false for TAOM's Kit-saved clones | Scoped |
| 11 | LOW | "Every `*_continue` clip carries `zero_collision`" | Overstatement | Only the flail continues do | Scoped to flail |
| 12 | LOW | Triangle budget counted only the largest LOD0 submesh; TAOM's props are 4,500 to 7,500 triangles | Missing context | Copied MithrilForge's table as given | Noted in the recipe |
| 13 | LOW | "25 bytes short" said of all four packages | Number slip | Measured one | 25, 16, 21 and 20 bytes |
| 14 | LOW | `Agent.SetActionSet` described as taking an action set | Imprecision | Paraphrased MithrilForge | Signature and `Mission.cs:4540-4541` verified on 1.5.3 |
| 15 | LOW | "Every mesh name those classes ask for" is gated | Overstatement | The classes also place five vanilla meshes | "the prefab meshes passed to `PlaceCenteredPrefab`" |
| 16 | LOW | Duplicate item names or GUIDs across packages passed | Gate gap | Not considered | Assertion added |
| 17 | LOW | `serialize`'s docstring promised a byte-for-byte round trip for any file | Stale docstring | Written before an aligned package existed in the repo | Qualified to packed files |
| 18 | LOW | Provenance row omitted the animation docs; the dead-ends table was ported in part without saying so | Record gap | Oversight | Row extended; review's Skip line accounts for the rest |
| 19 | LOW | "The fallback logs nothing" | Overstatement | The services log the placed-entity count at debug level | The count is now the smoke's objective signal |

## Root-cause pattern

Findings 1, 5, 6 and 9 to 12 share one cause: **a claim was written from the source's framing, or from a sample of
TAOM's own files, instead of from the evidence that decides it** (the client log, the release folder, vanilla's
packages, the engine's consumers). `.claude/rules/evidence-over-claims.md` C.1 names the trap (writing the summary
before its evidence exists); the review found it inside an adoption, where the source's authority made its claims
feel checked. Finding 5 repeats the 2026-09-28 lesson almost exactly: compare with vanilla before generalising.

Findings 2 and 16 are the gate's own blind spots: its one negative case attacked the package side and never the
code-to-table link, which is where the review broke it.

## Why each lens caught or missed these

- **Data flow (5)** found finding 1 by following the path past the repo, into the client log and the release folder.
  The draft's author never took that step.
- **Engine compatibility (2)** found 5, 6, 9 to 14 by measuring the whole install rather than the four props.
- **Tooling** found 2, 3 and 16 by building scratch mirrors and attacking the regex.
- **Completeness (4)** found 7, 8 and 18 from the issue tracker and the neighbouring docs.
- **Efficiency (3)** found nothing to change (26 ms per run).
- **Design (6)** did not run (usage limit). The orchestrator kept the gate in Python with the lazy imports, rather
  than porting it to TAOM.Tests, because the C# side would need a tpac TOC reader of its own.

## Process note: staging own hunks beside another session's

While committing, a filtered `git diff -U0` patch piped to `git apply --cached --unidiff-zero` put this change's
pure insertions 17 lines off in `provenance-register.md` (under the next section's heading) and one line off in
`tools/README.md`, because another session's hunks sat above them in the same files. It was caught by reading the
staged blob (`git show :<path>`) before the commit. Staging by rebuilding the index entry from the HEAD blob plus
the chosen hunks at their HEAD-side positions (`git hash-object -w`, `git update-index --cacheinfo`) placed both
correctly. Read the staged blob of every shared file before committing.

## Feedback memories to codify

None new. Finding 1's rule is the lesson "Trace the load path before calling an asset shipped"
(`lessons/animation-skeleton.md`); finding 5 is an existing lesson not followed.
