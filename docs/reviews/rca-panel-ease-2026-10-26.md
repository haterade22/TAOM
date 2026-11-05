# RCA: custom battle panel ease (#1027, 2026-10-26)

## Top-line

#1027 gives the custom battle screen's three panels vanilla's ease (`EaseType="EaseOut" EaseFunction="Quint"`, 0.45 s),
which TAOM's copy of the screen had replaced with the dead `EaseIn` attribute. The first version also eased a panel
definition on the character selection screen. The tier 2 review (Codex at `xhigh`, the data flow and XML lenses) found no
CRITICAL or HIGH. All three found the same MEDIUM: the character selection definition is used by no widget, so that edit
changed nothing. They also found 3 LOW findings. All were fixed in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The `LeftPanel` definition in `FacGen/PreBuildCharacterSelection.xml` is named by no widget in the prefab, and Gauntlet resolves a definition only from a widget in the same prefab, so the added ease did nothing; the recommendation to Mike said that panel would slide (Codex, lens 5, lens 7). | Data edited without tracing its consumer | The recommendation read the definition and assumed a widget used it; the file is a clone of vanilla `ClanMembers.xml` whose widget dropped `VisualDefinition="LeftPanel"`. | Hunk reverted; the orphaned block deleted (no visible change). Lesson in `localization-ui.md`. |
| 2 | LOW | The vanilla parity test compared three attributes, so an added `DelayOnBegin` (which makes the panel wait and then snap) would pass (Codex). | Partial oracle | Written for the attributes the change touched. | The test compares every attribute and the child `VisualState`s. |
| 3 | LOW | The attribute-name gate checked `VisualDefinition` names but not the ease values; the loader parses them case-sensitively and keeps Linear/Sine on a miss, so `EaseType="Easeout"` would pass and play linear (lens 7, lens 5). | Gate checks names, not values | The #1024 gate was scoped to names. | `EaseType` and `EaseFunction` must be defined enum names. |
| 4 | LOW | The issue and the test summary called the character selection screen TAOM-only and gave an ease census that counted TAOM's deployed prefabs (lens 7). | Claim from a quick count | The census ran over every module, TAOM included. | Corrected on the issue; the summary sentence removed. |

**Not applied:** lens 5 found three more definitions no widget names (`GameMenu.xml` `InfoPanel`, `PartyScreen.xml`
`FormationListPanel` and `FormationSeperator`, whose widgets are commented out). Follow-up closed 2026-11-04 (498d07e08) (no issue: older than this
change and each needs a trace of PrefabExtensions that could name it; Mike's word to file): delete them, and consider a
gate that fails a `VisualDefinition` no widget in its prefab names.

## Root-cause pattern

Finding 1 is the same shape as #1022 and #1024: a prefab element that looks live was never wired, and the change
reasoned from the element rather than from its consumer.

## Why each reviewer caught or missed what it did

- **Codex (`xhigh`):** followed the definition to `Widget.UpdateVisualDefinitions` and the `VisualDefinition != null`
  check, which exposed the orphan (1), and built the `DelayOnBegin` counterexample (2).
- **Data flow (5):** traced every consumer of each definition and found the orphan (1) and three older ones.
- **XML (7):** parsed definitions against uses, traced the clone to vanilla `ClanMembers.xml`, and found the value gap (3)
  and the census error (4).

## Feedback memories to codify

None. The lesson carries the rule.
