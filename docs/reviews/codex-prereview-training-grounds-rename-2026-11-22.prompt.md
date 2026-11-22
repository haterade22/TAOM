# Codex independent pre-review: the Training Grounds name (#1527), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, run no git command that changes anything. Report
defects with evidence (file, line, the input or state that fails). Engine facts come from the
installed game (`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you cannot prove
from the engine source or the data is UNVERIFIED, not a finding.

## What to review

The uncommitted change in this worktree (branch `feat/training-grounds`, base 3b588519c):
`git status --short` and `git diff` list it. About 45 files:

- `Main/_Module/ModuleData/taom_module_strings.xml`: three `taom_tidings_*` rows and four
  `taom_mcm_*_hint` rows reworded, and a new block of 21 rows `taom_training_grounds_<key>` at the end.
- `Main/_Module/ModuleData/characters/npcs_*.xml` (24 files): the tournament master's name.
- `Main/_Module/ModuleData/taom_character_name_strings.xml`: generated from those files by
  `tools/generate_name_localization_strings.py`.
- Three C# files whose only change is the English default of a `{=KEY}default` literal.
- Ten files under `Main/_Module/ModuleData/Languages/`: four rows each.
- `TAOM.Tests/Infrastructure/Localization/TrainingGroundsTextTests.cs` (new) and
  `TAOM.Tests/Features/SettlementServices/SettlementServicesTextsTests.cs`.
- Five docs: `docs/features/arena.md` has the new section "The Training Grounds name (#1527)".

Read `docs/features/localization-override.md` first: it is the mechanism this change relies on.

## Intent (the maintainer's decisions; do not relitigate them)

- In English the town arena is called the Training Grounds and its keeper the Training Master.
- Text only. The menu id `town_arena`, the location id `arena`, the occupation `ArenaMaster` and
  every scene keep their names.
- The four MCM hint rows that were never translated are reset to the new English in the language
  files. The Simplified and Traditional Chinese rows of those keys are real translations and stay.
- The other twelve languages keep vanilla's word for the arena.

## Evidence the builder has

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` on this tree after the last
  edit: 43,986 passed, 0 failed, 9 skipped.
- `python tools/validate_moduledata.py` and `python tools/lint_docs.py --fail-on-drift`: exit 0,
  run before the language-file rows were reset.
- No in-game check has run.

## What to try to disprove

1. That every reworded vanilla key reaches the player through the method the Localization Override
   patches. Trace at least: the location name declared as `name="{=mMU3H6HZ}Arena"` in vanilla's
   `SandBox/ModuleData/location_complex_templates.xml`; the menu title set in
   `PlayerTownVisitCampaignBehavior`; the GameText `str_occupation.ArenaMaster`; one dialogue line
   of `ArenaMasterCampaignBehavior`; one incident text (`guEjfYhO`). Name any path that resolves the
   text before the override registers, caches it, or skips the patched method.
2. That each reworded text keeps the variables, conditionals, bracket tags and XML escapes of
   vanilla's own English row (root-level `Languages/std_*.xml` of Native and SandBox in the install).
3. That the new rows do no harm as GameText: `taom_module_strings.xml` is also registered in
   `Main/_Module/SubModule.xml`. Row id collisions, a second definition of a vanilla key, a row the
   language coverage tests then demand, a wrong lookup in another language.
4. That the list of reworded keys is complete and the list of kept keys is right. Search the
   install's English text, and TAOM's own English sources, for the arena under other wording
   (plural, capitalised, "arena's", inside a longer word).
5. That the language files are consistent for a non-English player, and that the reset of the four
   hint rows follows `docs/localization/TRANSLATOR_GUIDE.md` ("Changing English Text That Is Already
   Translated"), including the translation cache under `tools/translation_cache/`.
6. That each test in `TrainingGroundsTextTests` can fail for the defect it names and cannot pass on
   an empty or partial read.
7. That the edited files keep their own line endings, BOM state and indentation.
8. That no doc now states something false, and that no other doc, tool or test still quotes the old
   player-facing names as current.

Also look for what nobody listed: other mods or tools that read these strings by text, and any place
where TAOM compares or parses one of the changed texts.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the evidence you read, and the smallest input or state that shows the defect. Then a
list of what you checked and found correct, and a list of what you could not verify and why.
