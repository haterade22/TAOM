# RCA: Duty text has one source, in every language (#845, deep-review, 2026-10-26)

## Top-line

`/deep-review` on #845 ran lenses 5 and 7 and the tooling lens in a first wave, then a convergence pass on the
fixes. The first wave proved the hard requirement: no registered key, no English row and no translation
changed. It also found **one HIGH defect older than this change**, which two lenses reached independently:
English players read "a task for the company" for every field duty and "It went well." for every field
result, because the game shows English from the inline text after `{=key}` and never looks the key up
(`MBTextManager.GetLocalizedText`, v1.5.5 `MBTextManager.cs:264-268`). The field duties' own titles and
result lines reached only the eleven other languages. The fix renders each row's own `{=key}English` string,
so English now matches the registered rows. A **MEDIUM** defect was introduced by the change itself: deleting
the old generator removed the only step that registered each duty's result keys. Both were fixed before the
first commit.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Data flow, XML; older than #845) | Field duty toasts and the status board composed `{=taom_enlist_duty_<id>_title}a task for the company` and a generic result line; English shows the inline text, so every field duty read the same in English. | A composed key with generic inline English | The key was registered and translated, so every gate passed; the English path never reads the registry. | The runtime and the board render the row's own string; tests assert the English path. Lesson appended to `lessons/localization-ui.md`. |
| F2 | MEDIUM (Tooling, Data flow, XML) | The deleted generator was the only owner of the per-duty `_success`/`_failure` keys; the harvester skipped them and no test checked them, so a new duty's result line would ship untranslated with no warning. | A guard deleted with the tool that held it | The replacement was modelled on a sibling harvester that has no composed-key family. | Field rows carry their result lines in the JSON, the harvester owns them, and `--check` and a test fail on a row without registered result rows. |
| F3 | LOW (Tooling) | Harvester hardening: unescaped ids and no parse before writing; advice that `--sync-ids` repairs a changed English row (it does not); a clean pass on empty input; write safety, exit codes and missing option objects untested or wrong; a loose text compare; raw newlines; open file handles. | Tool robustness | Written for the shipped data, not for bad input. | All fixed with tests on copies. Commented-out and duplicate rows remain follow-ups. |
| F4 | LOW (several) | The key formula was built in four places; the validator named internal suffixes, not JSON fields, and reported one problem per row; XML block comments and docs called per-duty result lines shared. | Accuracy | Copied from the old generator's vocabulary. | One `DutyText.KeyFor`; warnings name the JSON field and list every problem; comments and docs reworded. |

## Convergence pass

Two reviewers checked the fixes. The runtime pass confirmed the hard requirement against the parent (the
286 string rows identical, no language file changed), the 56 popup strings byte-identical to what English
players saw before, and no dependency cycle from the status service's new config read; verdict READY. The
tooling pass proved T1 to T9 fixed by probes on copies and found one more gate gap: the result-row check
looked only at a row's `id`, while both translation tools key on the `{=KEY}` inside the text, so a row with a
copied key passed and never reached the translators. Fixed in the harvester (each row must carry its own key)
and in C# (the two shared popup result sentences are constants, and a test pins every result row's registered
text against the English the game shows). The status board's fallback for a duty whose row is gone moved into
the service, where a test reaches it.

Noted, not changed:

- A settings file edited before this change has no duty words, so its rows are skipped with a warning and,
  if every row is old, no duty is offered; the warning names the row and the expected field.
- The harvester treats a commented-out row as registered and edits the first of two live rows with the same
  id; neither occurs in the shipped file.

## Root-cause pattern

F1 and F2 are the same blind spot seen from two sides: a key that is registered and translated looks finished
to every gate, while the English a player reads comes from somewhere else. Check the inline English of every
composed key, and give every composed-key family an owner that fails when a row is missing.

## Feedback memories to codify

None; the lesson covers it.
