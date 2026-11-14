# Translation backlog

Feature work registers English source strings only. Per-language XMLs, translation caches and overrides belong to the project lead. Do not seed, generate or hand-edit them to clear coverage failures. See [the localization rule](../../.claude/skills/localize/SKILL.md).

Record each pending feature's module, source file, exact keys and English text, placeholders, UI context, target languages and status. Update existing entries instead of duplicating keys. The project lead marks entries complete after the translation pass.

## War Chronicle and coronation event window

- Status: pending project-lead translation pass.
- Source: [PR #1203](https://github.com/haterade22/TAOM/pull/1203).
- Module: TAOM.
- English source: `Main/_Module/ModuleData/taom_wotr_strings.xml`.
- Target languages: BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR.
- Preserve `{DAYS}` exactly; translate only the surrounding text.
- The two note strings are retained in the formatter/model but hidden by the current prefab. They are listed for source completeness, not as currently visible UI.
- The earlier language-file and cache additions have been removed from this PR. Missing-language-row checks for these keys are expected until the project lead completes the pass.

| Key | English source | Context |
| --- | --- | --- |
| `taom_event_category_chronicle` | War Chronicle | Chronicle category heading |
| `taom_event_category_coronation` | Coronation | Coronation category heading |
| `taom_event_effects` | Effects | Effects section heading |
| `taom_event_effect_days` | For {DAYS} days | Effect duration; preserve {DAYS} |
| `taom_event_side_free` | Free Peoples | Free Peoples target group |
| `taom_event_side_evil` | The Shadow | Shadow target group |
| `taom_event_effect_volunteers` | Volunteer recruitment rate | Recruitment effect label |
| `taom_event_effect_escape` | Additional escape roll | Escape effect label |
| `taom_event_effect_momentum` | War of the Ring momentum | Momentum effect label |
| `taom_event_effect_note` | Values are this event's contributions before caps, and the caps apply even to a single effect. Your own clan's fiefs and lords are never affected. | Note retained in model; currently hidden |
| `taom_event_effect_escape_note` | The additional escape roll's chance is the shown percentage of a lord's usual escape chance. | Note retained in model; currently hidden |
