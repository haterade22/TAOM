# RCA: deep review of the skill-template parity gate (#626, 2026-09-19)

**Top line.** Mike: "626 - Please proceed and ensure the templates match those skills that are
inline." They already did. All 1,528 inline `<skills>` blocks beside a `skill_template` in the repo
module equal their SkillSet, because `tools/sync_lord_inline_skills.py` re-synced them at v2.0.28
(`331032a1`, 2026-09-14) and `LordInlineSkillParityTests` has gated them since. What was wrong was
the commit-time gate: `SKILL_TEMPLATE_SHADOWS_SKILLS` still enforced the 1.4.8 rule ("never declare
both") over `troops/` and `characters/npcs_*.xml` only, contradicting the C# test. The change
replaces it with `SKILL_TEMPLATE_MISMATCH` (a row must equal its template) over every XML and XSLT
file of the repo module, reusing the sync tool's detection, and removes the C# test that stated the
old rule. Seven lenses reviewed it (Standards, Engine compatibility, Data flow, Tooling; then
Efficiency, Completeness, Design; XML not in scope, no data changed). No HIGH, no CRITICAL. Six MED:
two in the new code, one in the hook that runs it, three in places that still stated the 1.4.8 rule.
Nothing is committed: Mike is collecting every session's work into one commit.

## Findings

| # | Lens | Sev | Bug | Category | Why missed | Fix and prevention |
|---|---|---|---|---|---|---|
| 1 | Tooling | MED | The sync tool's new comment skip tested only whether a character's match STARTED inside `<!-- -->`. A comment naming `<NPCCharacter` swallowed the next real character (never judged); a commented-out `<skills>` block or `<skill>` row inside a live character was judged and rewritten; SkillSet loading read commented-out rows (vanilla has four, giving two unused custom-battle sets a phantom `Shield=60`). | Comment handling by position instead of masking | The skip was written for the one shape I pictured (a whole commented-out lord). `taom_schema.py` already masks comments for the same files; I did not reuse the idea. | `_scan` matches on a length-preserving comment-blanked copy and edits the original at the same absolute offsets; the loader strips comments. Five tests (three comment shapes, a second character's line, a commented SkillSet row). Parity with the pre-#626 implementation proved on the real lords files, clean and perturbed. A repeat of the first #617 review's item 2 (comment-blind search), in new code. |
| 2 | Efficiency | MED | `_scan` counted the line of every templated character from offset 0 whether or not it drifted: about 600 of the pass's 944 ms, inside the commit hook. | Quadratic scan | Written for correctness; the hook's cost was measured for the whole validator, not the pass. | A running counter (Design); the pass is 0.17 s. Covered by `test_each_finding_carries_its_own_characters_line`. |
| 3 | Data flow | MED | The hook runs the validator only when a commit stages `Main/_Module/ModuleData/*.xml`, so a commit touching only `lords.xslt` (364 of the 1,528 blocks, and 19 of the 83 lords that drifted at the bump) ran no validator, while the new rule row, the tool docstring and the feature doc said the hook gated it. | Gate scope wider than its trigger | `CommitGateCoverageTests` pins the hook's `--code` list, not its trigger; the scope of the new code was checked, where the hook fires was not. | Trigger is `*.xml|*.xslt`; the rule, the hooks catalog and the hook comment say so. The trigger has no harness test; proved with the case pattern on five paths. Lesson: build-tooling-workflow. |
| 4 | Data flow | MED | `tools/rebalance_lords.py --apply` writes every lord's inline `<skills>` from its own curve and never touches a SkillSet. Dead output on 1.4.8; on 1.5.2+ it would change the lords, and the new gate blocks it with a repair that reverts the rebalance. `tools/README.md` still advertised it. | Writer left on the old rule | Nobody listed the writers of the field when the rule flipped at the 1.5.2 bump. | Docstring and README row retire `--apply` for skills and point at the SkillSet generator; the importable helpers stand. |
| 5 | Data flow | MED | The old code lived in the Validator, so the MCP's `validate_moduledata` reported it; the new pass sits beside the other `main()` passes the MCP never runs. | Front-end coverage shifted | Placement followed the sibling passes (the right call, per Design); the MCP consequence was not stated. | Recorded in the pass docstring and the validation doc's changelog as part of #623 (one composition function for all four passes). |
| 6 | Data flow, Completeness | MED | The 1.4.8 rule survived in the present tense in `/lord-skills`' "one critical fact", `lord-skills-authoring.md`, three modding-handbook chapters (`lords-and-heroes`, `troops`, `id-cheatsheet`: "pick one"), `lord-perk-review.md`, `analyze_lord_balance.py`'s report text and `tools/README.md`, five days after `331032a1` fixed the data for the new rule. A session following the skill would hand-edit rows and be blocked, or not know they now win. | An engine-rule flip fixed in data, not in prose | The v2.0.28 fix updated its own tool and test; nothing swept the corpus for the old sentence. This review's first draft missed the handbook too; Completeness found it. | All rewritten to name both engine versions and the gate. Lesson extended: adapters-taleworlds-api. |
| 7 | Standards | LOW | The validator's docstring catalog, the validation doc's dated changelog and one lesson line did not name the new code; no CHANGELOG entry yet. | Records | Written before the records. | All written; the CHANGELOG entry goes into the worktree file beside the other sessions' entries. |
| 8 | Engine | LOW | Three sentences: "v1.4.8 discarded the inline block beside a resolvable template" (it discarded it whenever a template attribute was present: `ReadObjectReferenceFromXml` returns null only for an absent attribute, `MBObjectManager.cs:1515-1518`, and a dangling id yields a presumed placeholder); "the character ends up at zero" (only without inline rows on 1.5.2+); the wanderers doc's placeholder "named after the character" (it carries the referenced id). | Wording | Carried from the tool's own older docstring. | Corrected in the validator, the tool, the monotonicity test, `troop-skill-balance.md`, `skill-sets.md` and the wanderers chapter. |
| 9 | Tooling | LOW | An unresolved template's ERROR advised `sync --apply`, which leaves that case alone. | Wrong repair advice | One message for two findings. | Its own message (fix the id or add the set); test asserts no `--apply`. |
| 10 | Tooling | LOW | A ModuleData file that is not UTF-8 made the pass raise, and the hook denied the commit with no detail. | Crash in a gate | Every sibling pass reads leniently; the sync tool reads strictly on purpose. | Per-file guard: one named ERROR, nothing else skipped. Test added. |
| 11 | Data flow | LOW | A run that found no templated character at all passed; the C# twin asserts `checkedBlocks > 0`. | Gate that can check nothing | The SkillSet side had the guard, the character side did not. | Zero checked is one ERROR. Test added. |

**Improvements applied (Step 4), all behaviour-preserving except where a finding above changed behaviour:**
one `Scan` / `Drift` result for the fixer and the gate (`find_drift` and `_sync_block` deleted, the
`changed` counter is `len(drifts)`, unresolved templates are their own list); absolute spans through
`pos` / `endpos`; edits joined once; the orphaned `skill_template` parameter of the tier-collapse
test helper deleted. Placement in `validate_moduledata.py` kept (Design: the MCP reach belongs to
#623's composition function, not a second registry field).

**Not built:** template resolution for `UPGRADE_SKILL_REGRESSION`, `ranged_ladder` and
`rebalance_troops`, which #626's outline listed. 0 of 700 upgrade edges have a templated side; all
1,001 templated troops-and-villagers sit in `characters/npcs_*.xml` with no inline rows and no edge.
The skip stays, with a dated comment in `taom_schema.py` and the C# test.

**Follow-ups (pre-existing code, not in this change):** the sync tool's double-quote-only regexes
and naive `>` head split; `NPC_RE` / `XSLT_TEMPLATE_RE` without the self-closing alternative the
Validator uses; no parse-before-write in the sync tool (its edit is digits inside a quoted value);
`skill_set_files` sorts a module's files alphabetically where the engine uses SubModule.xml order;
`XSLT_MATCH_RE` narrower than the C# test's prefix; the MCP's reach (#623). **Coverage trade:** the
deleted C# test ran without the install; its replacement and the Python pass both need it, so a
machine without the install checks neither (both dev machines have it).

## Convergence pass

One `deep-reviewer` on the applied fixes (the first run hit the usage limit and was rerun): no HIGH
or MED. Three LOW: `tools/README.md` still named the deleted `find_drift` and the old "did not
resolve" wording (fixed); the pass converted only a per-file decode error into a finding, so an
unreadable SkillSet file or any other failure would reach the hook as a deny with no detail (fixed in
the siblings' shape, a "NOT checked this run" ERROR, with a test); the hook trigger has no harness test
in either direction (follow-up: `tools/test_hooks.sh` has no staged-file fixture). It re-proved the
mask on CRLF and BOM files, a comment opening inside a character and closing after it, the XSLT path
and `<!---->`. Final: tools suite 1,793 passed, the two C# classes 4 of 4, `tools/test_hooks.sh`
210 of 210, the validator exit 0 with no finding, `sync_lord_inline_skills.py` 1,528 blocks and 0
drifted, no new dash.

## Root-cause pattern

Findings 4 and 6 are one shape, and #626 itself is the same shape once more: **the 1.5.2 engine
bump flipped a rule, and the fix changed the data and the one tool next to it while every other
statement of the rule kept the old one** (a validator gate, a writer, a skill, five docs, a report).
Finding 3 is the gate-level version: a gate's scope and its trigger are two statements of what it
covers, and only one was updated. Finding 1 repeats the first #617 review's comment-blind search
(item 2 there) in new code.

**This session repeated F3 of the #617 second review** in its comment on #626: "nothing has checked
whether the bump retuned the lords" was written without a `grep skill_template tools/*.py`, which
finds `sync_lord_inline_skills.py`. Corrected on the issue and in the second RCA.

## Why each lens caught what it caught

Tooling found the comment shapes by probing the functions, not by reading. Efficiency timed each
stage. Data flow found findings 3 to 6 by listing every writer and reader of the field and every
front-end of the gate; nothing else in the review asks "who else states this rule". Completeness
grepped the docs for the old sentences and found the handbook the first draft missed. Engine
re-read `ReadObjectReferenceFromXml` and caught a wording error the author had copied from the tool's
own docstring. Standards found the records. Design reshaped the code the defect fixes had to touch
anyway. The convergence pass was cut off once by the usage limit and rerun.

## Lessons codified

- `docs/reviews/lessons/build-tooling-workflow.md`: a gate's hook trigger must cover every file kind
  its scope names.
- `docs/reviews/lessons/adapters-taleworlds-api.md` (extended): when an engine rule flips, grep the
  docs, skills and tools for the old sentence, not only the data.
