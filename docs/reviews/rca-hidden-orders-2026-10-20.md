# RCA: Hidden party orders (deep-review, 2026-10-20)

## Top-line

`/deep-review` of hidden party orders (#848), the Hidden Hosts rule that a party or army tooltip shows "Orders unknown."
until its count is read in full, ran seven lenses in two waves. It found no CRITICAL, HIGH or MEDIUM defect. The
production behaviour was right: the data-flow lens proved at IL level, across every installed assembly, that every call
of `MobileParty.GetBehaviorText` inside an open party or army frame is on that frame's own subject, which is the
assumption the postfix rests on (it never reads `__instance`). Every finding was LOW and in what the change SAID about
itself: tests whose names and docs claimed more than they checked, a wrong engine claim, a vacuous checklist line, and
stale counts and entry points. All were fixed before the commit. The convergence pass is recorded at the end.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The binding pins that guard the postfix's one assumption were looser than their names and the docs ("the exact caller list is pinned from the IL"): callers were matched by class prefix, the first wrapper call per builder only, and the "never by SandBox UI" assertion passed if that assembly did not load. | Pin weaker than its claim | The builder wrote the pins from the design's caller list and the docs from the design's intent; nobody compared the assertion with the sentence. | Exact caller sets, exactly one wrapper call per builder, an assertion that the SandBox assembly was scanned. Lesson in `lessons/testing-qa.md`. |
| 2 | LOW | The docs said a missing-API fault in the postfix makes PatchShield return a null `TextObject`, so the wrapper's `ToString()` throws. Harmony 2.4.2 stores vanilla's result before any postfix runs and returns it after a swallow, so vanilla's orders stay. | Engine behaviour copied from a lesson (REPEAT source) | `lessons/harmony-il.md` "A patch's own try/catch cannot survive a JIT-time member-resolution failure" says the target "returns `default(T)`"; that is true for a prefix or the original body, not for a postfix, and the builder applied it as stated. | Docs corrected; the lesson gains a dated correction naming the postfix case. |
| 3 | LOW | A `TextObject` constructor pin guarded nothing (829 call sites in Main would break the build first) and its message named the wrong file. | Pin with no failure it can catch | Added from the harmony-il "pin every engine member a body references" rule without asking whether the build already pins it. | Deleted (simplicity criterion). |
| 4 | LOW | A text seam (`HiddenHostsCalls.OrdersUnknown`) existed so tests needed no game text, but a `{=key}` English default renders without the string table, and every test replaced the seam, so no test rendered the shipped string. The postfix also checked a service that no frame can exist without. | Seam and guard copied from siblings (REPEAT of `lessons/testing-qa.md` "a decision seam that tests replace with a fake gets one test of the production implementation") | The siblings' seams exist because their text reads `GameTexts.FindText`; the new text does not, and the copy was made by shape. | Seam and guard deleted; the pair tests render the shipped text. Recurrence note on the lesson. |
| 5 | LOW | The docs never said when vanilla prints the orders row at all (an inspected party; an army tooltip also when the caller passes `false`, as the map tracker does), so the checklist line "the map tracker tooltip of a far party hides them" could never fail, and "a clan party shows orders at any range" was false out of sight. | Owed check without its trigger (REPEAT of `lessons/testing-qa.md` "An in-game check names the log line it expects and traces the trigger that produces it") | The design traced the callers and the threads, not the condition under which each caller prints the row. | The row's condition is stated; the checklist hovers a far army's tracker icon instead. |
| 6 | LOW | The frozen orders row was documented in one direction only (masked to exact); the reverse (orders shown beside a count that re-masks at the next refresh) also happens. | Limit stated one way | Written from the case the design worried about. | Both directions documented; nothing new leaks (vanilla's row is just as frozen). |
| 7 | LOW | Bookkeeping: the feature doc still said six keys; `INDEX.md` and the Overview did not mention the rule; the targets table lacked the new target; two planned in-game checks (an unmerged army member, a besieger while defending) were dropped; `far-watch.md` did not say a scout-named host keeps its name but not its orders. | Docs not swept for counts and entry points | The build brief listed the docs to change, not the sentences elsewhere that the change made stale. | All fixed. One-off. |
| 8 | LOW | Test shape: branch tests ran through a real Harmony detour although a hand call reaches every branch of a postfix that binds no instance; a thread test and a wrapper IL test duplicated existing pins; one test name claimed a case it did not cover. | Test placement | The pair-test file was the nearest template. | Moved to hand calls in the hook tests, duplicates deleted, name corrected. |

## Root-cause pattern: the change described itself more strongly than it tested itself

Findings 1, 2, 3, 5 and 6 share one shape. The code was right, and the words around it (test names, doc sentences,
checklist lines, the registry entry) claimed a stronger guarantee than any check provided. Two of the claims were
inherited: the "default(T)" sentence from a lesson that was itself too broad, and the seam from siblings whose reason
did not apply. A reviewer reading code finds nothing wrong; only a reviewer comparing each sentence with its proof does.

**Preventive action.** For a pin or a doc sentence that states a guarantee ("pinned", "exactly", "never", "the only
caller"), name the assertion that would fail if the guarantee broke, in the test or next to the sentence. A guarantee
with no such assertion is reworded or gets one. When a lesson supplies an engine claim, check that the lesson's case
(prefix, postfix, original body) is the patch's case.

## Why each lens caught or missed what it did

- **Standards (1)** found the loose caller pins (1) and the empty constructor pin (3), and confirmed every category tag.
- **Engine compatibility (2)** found the wrong fault claim (2) and the row's print condition (5), resolved the design's
  two unverified items, and confirmed the 1,820-byte target cannot be inlined while the 12-byte wrapper can.
- **Efficiency (3)** found no cost on any caller path and confirmed a fresh `TextObject` per call is right (a shared
  one would carry a caller's `SetTextVariable`).
- **Completeness (4)** found the bookkeeping gaps (7) and proved the rebase conflict in the 12 language files.
- **Data flow (5)** answered the open question with an IL proof and found the vacuous checklist line (5), the one-way
  limit (6) and the one-call-per-builder gap (1).
- **Design (6)** found the unneeded seam and guard (4) and the test shape (8).
- **XML (7)** found no defect in the 13 string files.

## Follow-ups outside this change (fixed in the next commit, 2026-10-21)

The review found three problems outside hidden party orders. On Mike's "fix all issues" they were fixed in a separate
commit, `fix(tools): v2.0.35 - schema check names language files and stylesheets`, reviewed with its own seven lenses
(Standards, Engine, Data flow, Tooling, then Efficiency, Completeness, Design) and one convergence pass:

- **`PatchShield.cs` value finalizer comment.** It said a swallowed fault returns the default value. For a fault in a
  postfix, Harmony 2.4.2 returns the original's stored value; the comment now says so, and
  `PatchShieldFinalizerTests` pins it (a postfix that throws, one swallow counted, the original's 7 returned).
- **Line-ending facts.** A census of all 276 language files on 2026-10-21: 18 of 23 types end lines in `\r\r\n`, three
  mix both forms, and `std_taom_module_strings_*` and `language_data.xml` are stored as LF; the module strings files
  also carry a blank line after all but 61 of their 3,153 rows since merge `85cbd20c8`. Four docs, the `xml-data.md`
  rule, the XML I/O convention and the XML lens stated the 2026-08-14 shape; the 2026-10-19 enlistment RCA had already
  listed two of them as follow-ups.
- **`validate_xml_schemas.py`** printed NOT REGISTERED, "the engine never reads it", for files the engine loads. That
  was logged twice before as a follow-up with no issue (`rca-mouth-of-sauron-gear-2026-10-04.md` MS7-4,
  `rca-generated-lord-kits-2026-10-06.md` #11). It now names LANGUAGE FILE and STYLESHEET from the engine's own
  loaders, names a manifest the engine cannot load as a real fault (the one HIGH of that review: the first version
  called such a manifest loaded), and counts real faults on the summary line without changing the exit code.
  Fixed under #980 (2026-10-23): a by-name real fault now exits 1, and the NATIVE bucket names project.mbproj rows.

Review findings of that commit, all fixed in it: the first wording still claimed "the engine never reads it" (the
engine reads `global_strings.xml` and the scene lists by fixed name, and voice and sound files through
`project.mbproj`); a doc count of 19 that the census says is 18; the stylesheet described as applied to its own file
(it applies to the data merged before it); VoiceFile rows, id-less manifests and a leading slash in `xml_path` not
handled; the fault clause depending on how the path was spelled; a crash on a path the console cannot encode; a
rerun hint that did not work from the repo root (older than this work); stale CI claims. Left as follow-ups, each a
behaviour change: a NATIVE bucket for `project.mbproj` rows, failing the run on a real fault, and one shared
registration walk in the tool. Restoring the 12 blank-line module strings files is Mike's call (a whole-file rewrite
each). The external translation ratchet still fails on nine TAOM_Map settlement names; their translation is owed
under #763.

**Why the follow-ups recurred.** Each had been written down as "`FOLLOW-UP`, not fixed here" with no issue and no
failing test, which `lessons/localization-ui.md` already warns turns a follow-up into a note; the recurrence bullet
there records this round.

## Convergence pass

One deep-reviewer read the applied fixes against the brief and the installed v1.5.5 DLLs. It confirmed that no runtime
outcome changed: the old service check and the new gate agree on every reachable state, because the only scope opener
returns no frame without the service; the exact caller sets match the DLLs; the hand-call tests cover every branch the
deleted pair tests covered. It found three LOW wording defects, all fixed:

| # | Sev | Finding | Fix |
|---|---|---|---|
| C1 | LOW | The doc said the kingdom army's party items and the menu's party items open an army tooltip with `false` as the third argument; they open a party tooltip, whose orders row depends on inspection alone. Only the map tracker opens an army tooltip that way. | Corrected. |
| C2 | LOW | The tests list said the postfix's "catch body" only constructs a `TextObject`; it is the try body. | Corrected. |
| C3 | LOW | A pair test named "...any other frame keeps the original" checks only no frame and a vanilla frame, and the doc said "named party or army frames" where only a named party is tested. | Test renamed; doc narrowed. No branch was uncovered. |

The catch block of the postfix has no test: its try body only constructs a `TextObject`, which cannot throw at run
time, so the rethrow mutant survives as an equivalent one. This is recorded in the feature doc.

C1 is finding 5's class again, inside its own fix: the corrected sentence about when vanilla prints the row named two
callers by the argument they pass without checking which tooltip they open. The preventive action above (name the
condition under which the thing is shown) covers it.
