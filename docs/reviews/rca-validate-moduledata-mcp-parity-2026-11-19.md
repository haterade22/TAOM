# RCA: one pass composition for the validator CLI and the MCP tool (plan 059, deep review, 2026-11-19)

## Top line

Plan 059 (#623) moves every validator pass into `validate_moduledata.run_all_passes`, which the CLI and
the MCP tool `validate_moduledata` both call, widens the art present-set to every module loaded for
art, and speeds up the mesh-ref scan. The tier 2 review (Lens 5 Data flow, Tooling correctness) and
the lead's own differential sweep found the CLI's output byte-identical on the live install and no
gap in the composition. Two LOW findings were confirmed and fixed in the review's commit. One repeats
a recorded cause (a hand-kept list), which is why this file exists. Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | `DEFAULT_TPAC_MODULES` (`validate_mesh_refs.py:103`) is kept by hand. Its comment names `SubModule.xml` as the source, but the only test pinned today's six names, so a module TAOM comes to depend on would never be scanned for art and its meshes and bodies would read MISSING. | Convention inconsistency: hand-kept list | The plan widened the list and pinned it, and nothing compared it with the file the comment cites. The same cause was recorded on 2026-10-29 for the generator realm list. | Test `test_every_module_taom_depends_on_is_scanned_for_art` reads every `DependedModule` from `Main/_Module/SubModule.xml` and fails unless each is in the list or in a named no-packs set (red on the base list: `'sandbox'` missing). Lesson in `lessons/build-tooling-workflow.md`. |
| F2 | LOW | The MCP tool docstring (`taom_mcp_server.py`), which is what an MCP client reads, said "a pass that could not run comes back as a PARTIAL_RUN WARNING". Two skips still go to stderr only: `armour_class_table_issues` with no `LOTRLOME_Armory` (`validate_moduledata.py:155-158`) and `module_tpacs` for a module with no `.tpac` (`validate_mesh_refs.py:455-459`, which fires on every desktop run for `SandBoxCore`). | Other: contract text wider than the code | The plan supplied the docstring text (plan :721 and :731) and the executor used it as written. Nobody grepped the composed passes for `file=sys.stderr` before stating the rule. | The two docstrings and the feature doc now name what comes back and what does not. Lesson in `lessons/build-tooling-workflow.md`. |

## Why each agent missed these

- **Tooling correctness:** found both (its items 2 and 3) and rated them LOW and `FOLLOW-UP`. The lead
  classed them as changed-code findings and fixed them.
- **Agent 5, Data flow:** traced the notices into the MCP result (its flow 3) and the module list into
  its consumers (flow 7). It checked that the notices that exist arrive, not that every skip is a
  notice, and it checked the list's consumers, not the list's source.
- **Agents 1, 2, 3, 4 and 6:** skipped by tier. Agent 6 (design) is the lens most likely to have
  asked where the list comes from.
- **The plan and the executor:** the plan wrote the docstring before the code it describes, and
  measured the module list on one install without leaving a check behind.

## Root-cause pattern

Both findings are a claim written once and never compared with the thing it describes: a list that
names its source file in a comment, and a docstring that states a rule for every pass. The fix in both
cases is to read the source the claim names (SubModule.xml; each pass's stderr writes) and either
derive the claim from it or narrow the claim to what the code does.

## Left for later

- FOLLOW-UP, no issue: an /improve lead files none, and the orchestrator decides at wrap-up.
  `tools/audit_armory_refs.py:355` keeps its own module list, so `/armory-audit` and the validator
  check bodies against different modules (stricter in the audit, 0 refs affected on the live install).
- FOLLOW-UP, no issue: the orchestrator asks the maintainer. Whether the two stderr-only skips in F2
  become `PARTIAL_RUN` notices (changes the MCP output), and whether the ModuleData hook answers
  `UNCHECKED` on a validator exit 2 instead of a silent `{}` (changes a gate).

## Feedback memories to codify

None beyond the two lessons entries: the hand-kept list rule already exists, and this repeat adds the
`SubModule.xml` variant of its prevention.
