# RCA: MCM settings text keyed for translation (#1385 Phase 4)

Findings from the review of #1385 Phase 4: a one-off rewriter put a `{=taom_mcm_*}` key on every MCM setting name,
hint, button text and group-path part in the four settings classes (1,130 new English rows); `McmText.Plain` strips
the keys where the co-op fingerprint and the server settings schema name a group; the harvester and its C# twin
split a group path into one row per part; a new gate (`McmSettingsTextIsKeyedTests`); and 1,102 CNs and CNt rows of
the Chinese team's text. It changes what the co-op settings fingerprint reads, so it ran as tier 1: the data flow,
engine compatibility, XML and tooling lenses at `max`. Codex did not run: it has had no usage since 2026-11-15, and
the maintainer said to skip it. No lens reported a CRITICAL or HIGH, so no second wave ran. Each finding was read
again against the files before it was fixed. Rows 12 and 13 were caught by the builder before the review.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | Tooling F1: the new key gate walked `ServerSettingsSet.Classes`, a list of four types kept by hand, so a fifth settings class with bare English would pass it | Gate scope from a hand list | The builder reused the list the server schema already walks | `ServerSettingsSet_ListsEveryMcmSettingsClassInTaomDll` compares the list with every `AttributeGlobalSettings<>` type in TAOM.dll |
| 2 | MED | Tooling F2, XML F2, engine compatibility F1, data flow 16: MCM shows a child group as `Localize(child).Replace(Localize(parent), "")`. A translated child that holds its parent's text shows cut, and an empty parent text throws, so MCM hides the whole TAOM page. The guide stated the rule and no test checked it | Documented rule with no gate | The builder checked the current data once by script and wrote the rule into the guide only | `NoChildGroupText_ContainsItsParentsText_InAnyLanguage` reads English and every language file; the guide names the test |
| 3 | MED | XML F1: the builder dropped 7 team hints from CNs and CNt because `LanguageTextIntegrityTests` flagged them. Six of the flagged words were code names the English row also holds (`taom.tactic_status`, `event:/ui/transfer`, `campaign.toggle_information_restrictions`, `visuals.maxGlowing`, `taom_debug.log`), so the gate was wrong, not the text | Gate too strict for a new text shape | The gate's identifier pattern knew file names, `snake_case` and `camelCase`, not dotted commands, sound paths or `.log` files; the builder trusted the gate | The identifier pattern masks those shapes when the English carries them (four clean rows and one damaged row added to its data tests); the six rows are restored. `EnableEnlistmentTrace_hint` stays English: its "ok和RESTORE" glues a Latin word to Chinese |
| 4 | LOW | XML F3: "Dale" had no CN row, because the drop holds two texts for it (河谷邦 and 河谷城) | Ambiguous source text | The exact-match script skipped every English with two team texts | 河谷邦, the text the team's own Dale colour hint uses |
| 5 | LOW | XML F4: the guide said every keyed text has a row the team translates; 35 MCM keys had no CN row, and the importer never adds a row | Doc overstates | Written before the row counts were final | The sentence says a missing row shows English and must be added first |
| 6 | LOW | Tooling F3: a nested group part could register a key with no TAOM prefix; in `taom_module_strings.xml` a vanilla key overrides the vanilla text | Generator accepts more than its input rule | `KEYED_PART` copied the test's key pattern, not the harvester's prefix list | `KEYED_PART` takes `LOC_KEY_PREFIXES`; `test_group_parts_NonTaomNestedKey_IsNotDeclared` |
| 7 | LOW | Tooling F4: the `/{=` guard gave a new parent key with an unkeyed child the row "A/B", a text MCM never looks up | Guard with a wrong edge case | Written for the paths that existed, which were all keyed | The guard is gone in both twins; "A/B" and three-part cases in both test sets |
| 8 | LOW | Tooling F6: two of the gate's three tests passed on zero texts | Test without a floor | Only the first test got the floor | `McmTexts()` and `GroupPaths()` assert the floor for every caller |
| 9 | LOW | Tooling F7, data flow note: the gate's summary quoted a count that matched no measurement, and claimed every text on the MCM screen while dropdown options and page titles stay unkeyed | Unsourced count, overclaim | Written from memory of an early scan | The count is gone; the summary names what stays unkeyed and why |
| 10 | LOW | Tooling F8: the `tools/README.md` row did not say the harvester splits a group path | Doc out of date | The row lists behaviour by hand | The row says so |
| 11 | LOW | Data flow F15: removing `McmText.Plain` from `GroupOf` or `Describe` passed every test | Fix with no test | The fingerprint and schema tests use fake or count-only data | `TheRealSettings_GroupByTheirPlainEnglishRoot` and `DescriptorGroups_AreThePlainEnglishPath` read the real settings |
| 12 | LOW | Engine compatibility F2: `SettingsFingerprint` reads `GroupName`, `SelectedValue` and `SelectedIndex` by name with no `reflection-sites.md` row; older than this change | Registry gap | The read predates the registry row for its twin in `McmSettingsCollector` | A Category C row beside the collector's |
| 13 | LOW | Builder: the rewriter keyed two commented-out shader settings, and the harvester registered 6 rows no code reads | Generator reads comments | The rewriter and the harvester match text, not code | The comments are back to their old text and the rows are gone; `check_orphan_loc_rows.py` reports none |
| 14 | LOW | Builder: 67 feature tests failed after the rewrite. A general pass then wrapped name reads in tests that assert a feature's own key, and three "plain English" asserts still checked the old state | Mechanical test edit too wide | The pass matched a read, not the assertion around it | Those reads unwrapped; the asserts check for a `taom_mcm_` key; full suite 2 red, both expected |

## Not applied

- Tooling F5: the harvester and `UnregisteredLocalizationKeyBaselineTests` count a key in a comment as declared, while `check_orphan_loc_rows.py` and `LocalizationKeyConsistencyTests` ignore comments (row 13 is the result). FOLLOW-UP no issue: one rule for four tools changes a C# gate outside this phase.
- Data flow F14: the co-op "settings differ" notice names the English group roots inside a Chinese sentence. That is the state before this change; mapping a root back to its keyed text needs a public helper. FOLLOW-UP no issue: cosmetic, co-op only.
- Data flow F13: 28 MCM keys have no CN row (27 with no team text, one held back by row 3), and 21 older rows (`taom_hostile_peace_mcm_*`, `taom_realms_mcm_*`, `taom_cheat_mcm_*`, `taom_wc_mcm_story*`) hold English. All go to the team in the hand-off.
- Data flow note, engine compatibility: the 8 dropdowns and the 4 page titles stay unkeyed. MCM 5.12.3 shows a page title without localizing it. Keying a dropdown needs `Plain` in `SettingsFingerprint.Render`, `ServerSettingsReader.IndexOf` and the defaults reference. FOLLOW-UP no issue: no team text names them.

## Why each agent missed these

- Rows 1, 2 and 11 are gates that checked today's data once instead of the rule. Each lens asked what a new class, a new language or a deleted call would do.
- Row 3: the builder treated a gate failure as a fault in the data. The XML lens read the English rows and found the same words there.
- Rows 6 and 7 came from writing the Python twin from the C# test, not from the harvester's own input rule.

## Feedback memories to codify

None. The lesson goes to `docs/reviews/lessons/localization-ui.md`.
