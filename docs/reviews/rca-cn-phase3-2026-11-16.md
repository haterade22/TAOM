# RCA: player-facing C# literals the Chinese team patched at runtime (#1385 Phase 3)

Findings from the review of #1385 Phase 3: 129 new `{=taom_*}` keys in 27 production C# files (the custom battle muster
screen, the shader pre-compile status lines, the action bar, role prefixes, layout and earning toasts, the
howdah, mumakil and warg prompts, career toasts, quick actions, the faction screen), the harvester's new refusal
of a split literal, and 57 CNs and CNt rows of the team's text. It changes a Harmony patch's hint and a mission
behavior, so it ran as tier 1: the data flow, engine compatibility, XML and tooling lenses at `max`. Codex did not
run: it has had no usage since 2026-11-15, and the maintainer said to skip it. No lens reported a CRITICAL or
HIGH, so no second wave ran. Each finding was read again against the files before it was fixed. Rows 15 and 16
were caught by the builder before the review.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | Tooling F1: the harvester's split-literal tests passed five mutants of the guard, one of which no longer caught a newline before `+`, the shape of all three truncated rows (row 15). Every `main` test mocked `split_keys`, so a dead guard also passed | Test does not pin the failure | The tests were written from the regex, not from the three real sites | Five tests (newline before `+`, verbatim tails, a later unrelated `+`, `split_keys` on a scratch tree, `--dry-run`), and `test_split_keys_RealTree_HasNone`, which keeps a registered key from being split later |
| 2 | MED | XML F1, data flow F4: nine sentence templates (`taom_res_earned`, `taom_spc_progress`, `taom_mf_layout_changed` and others) got no Chinese row while the words set into them did, so a Chinese player saw "[MixedFormations] Layout (全部编队) → 棋盘阵" where the whole line used to be English | Partial translation of one sentence | `cn_p3_rows.py` matched our English to the team's exactly; the team wrote these as `{0}` format strings or as fragments, so no template matched | The nine rows take the team's format string with its slots renamed, or its fragments in the old C# order (`cn_p3_fix.py`). `taom_spc_incomplete` has no team text for its tail and stays English |
| 3 | MED | Data flow F1: the career ready toast said "Press V" whatever key the player had bound, while the HUD chip beside it showed the bound key | Hardcoded value beside a live one | The literal predates the change; keying it copied the V into a key and two Chinese rows | `AbilityActivationResult.KeyName` carries `IAbilityInputAdapter.ActivationKeyName`; `CareerAbilityMessages.Ready` sets `{KEY}`, or says to bind a key when none is bound; both Chinese rows use `{KEY}`. Tests in `AbilityActivationControllerTests` and `CareerAbilityMessagesTests` |
| 4 | MED | Data flow F2: the Battlefield tab of the same muster screen (`TcbFieldVM.cs`) kept every label English, and its summary filled a keyed slot | Same screen, file outside the scope | The scope was the literals the team patched; the tab is newer than the team's drop. The harvester only sees literals that already carry a key | 32 new `taom_tcb_` keys; Attacker and Defender use vanilla's `KASD0tnO` and `XEVFUaFj`. The code reads `Value`, never the label |
| 5 | LOW | Data flow F3: START while over the troop limit replaced the keyed limit line with `ArmySpec.Validate`'s English one | Two paths to one message | The builder keyed the refresh path and did not follow START | START checks `IsOverLimit` first and shows the keyed line (`TooManyText`) |
| 6 | LOW | Data flow F6: `Earned` and `RunningOut` rounded with `F0`, against the class's own `FormatAmount` rule, so a 0.2 upkeep read "0 left, losing 0/day" | Inconsistent formatting | The new methods copied the old toast's format | Both use `FormatAmount`; `RunningOut_FractionalAmounts_ReadAsWritten` |
| 7 | LOW | Data flow F7, engine compatibility F5: `RoleTooltipDecorator` strips an old prefix only when the label has at most 4 characters, so a longer translation would stack prefixes | Latent limit with no gate | Every current label fits; the limit is in code, the labels are in data | `RoleLabelLengthTests` reads every language's `taom_role_*` rows |
| 8 | LOW | Data flow F8: `QuickActionsService.GetMenuOptions` had no runtime caller and wording that differed from the real menu | Dead code | The brief listed it as left English instead of asking who calls it | The method, its interface member and its test are deleted; `quick-actions.md` updated |
| 9 | LOW | Data flow F9: two docs cited `FinishRageMode.cs:36` for the controller swap, now at line 38 | Doc line drift | The edit moved the line; no gate reads doc line numbers | Both references repointed |
| 10 | LOW | XML F2: the role list on a muster unit reused the banner panel header key `taom_tcb_banner_header`, so one translation served a header and a role tag | Key reused across contexts | The subagent reused a key with the same English | `taom_tcb_banner_caps`, like its siblings `taom_tcb_captain_caps` and `taom_tcb_horn_caps` |
| 11 | LOW | Engine compatibility F4, data flow F11: the fault popup's button was a bare "OK" beside a keyed summary | Literal beside keyed text | Out of the literal list the builder worked from | Vanilla's `{=yS7PvrTD}OK`, which Native translates |
| 12 | LOW | Engine compatibility F2: the `ModuleRunner` summary said "Engine-free" after `TakeFaultSummary` began to return a `TextObject` | Doc out of date | The edit changed the return type and not the class summary | The summary says it builds only that `TextObject` |
| 13 | LOW | Tooling F2: `CONTINUED` missed `+ @$"` | Pattern gap | The regex listed `$@` and not `@$` | `(?:\$@?|@\$?)?`, with the unhandled shapes named in its comment |
| 14 | LOW | Tooling F4: the `tools/README.md` row did not name the new refusal | Doc out of date | The row lists refusals by hand | The row names it |
| 15 | MED | Builder: `harvest_literal_loc_keys.py --apply` registered three rows with half their English, because their C# literal was split with `+` and the harvester reads one literal | Generator reads part of its input | `LocalizationKeyConsistencyTests` compares the first literal only, so the half row matched it | The three literals joined into one and the rows corrected; the harvester refuses a new split key (rows 1 and 13 harden it) |
| 16 | LOW | Builder: `EntryPointLineCountGateTests` (the career behavior grew a line) and `ReflectionSiteBindingTests` (a `using` moved two labels in `RoleTooltipDecorator`) failed on the first full run; after the review fixes, `check_orphan_loc_rows.py` called `taom_tcb_no_map` unread, because its literal sat inside an interpolated string | Gates hit as designed | Expected cost of the change | Joined the line, repointed the two labels in the test and `reflection-sites.md`, and moved the no-map text into its own property |

## Not applied

- Engine compatibility F1: the faction screen's new title key never shows, because TAOM's hook copies the vanilla VM's "Culture" title first. The English "Choose your Realm" never showed either. Which title is intended is the maintainer's call. FOLLOW-UP no issue: it predates this change.
- Engine compatibility F3: a `{` inside an exception message set as a variable would clear the start-failed line. No caller produces one; a guard for it fails the simplicity rule.
- Data flow F5, engine compatibility F4: the shader item text in `{ITEM}` ("Troops batch 1/3", "Scene: <id>") and the stop reason in `{REASON}` stay English. Both are also log lines, and a scene item names a file id.
- Data flow F10: the campaign nav labels recompute only when `ItemId` changes. Whether a language switch rebuilds the map bar is UNVERIFIED and is part of the owed in-game check.
- `ArmySpec.Validate`'s other messages stay English: they name troop and faction ids from a spec file.

## Why each agent missed these

- Rows 2, 4 and 15 came from matching or reading one string at a time. The importer matched whole strings only, the scope came from the team's list instead of the screen, and the harvester read one literal of a joined default.
- Row 1 passed because the tests were written from the code under test. The tooling lens ran mutants against them.
- Rows 3 and 5 were second paths to the same text: the HUD chip beside the toast, and START beside the refresh.

## Feedback memories to codify

None. The lesson goes to `docs/reviews/lessons/localization-ui.md`.
