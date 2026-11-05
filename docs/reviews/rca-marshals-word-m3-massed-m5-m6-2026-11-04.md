# RCA: Marshal's Word, Massed fire, stages M5 and M6 (deep review, 2026-11-04 and 2026-11-05)

**Top line:** the tier 1 `/deep-review` of Massed fire, Hold the Line, the square and spacing repairs (M5) and the order panel set (M6) found three HIGH. The Hold the Line text promised that a pushed soldier walks back to his place, which the clamp does not do. The panel set's own button drew no icon. The `hold_line_slot` step passed on about one second of contact and never reached the `hold-slot` check. All three are fixed in `ca160a544`. The second wave, which those HIGH findings required, found no HIGH; its items are owed (see the end). Seven runs on the reviewed build, `20261105-023912` to `20261105-025030`, passed every Marshal step, one step per battle. The rerun after the merge with the Seat's S6 and S7 is in [marshals-word.md](../features/marshals-word.md).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (lens 7 item 1; lens 5 T2 at MED) | The Hold the Line hint, the doc and the human row at `0.toml:730` said a soldier the crowd pushes off his place walks back to it. The clamp only replaces an input that walks him away | Docs that state more than the code does | The text came from the plan's intent. No test or step read a displaced soldier with a zero input | Text corrected in every place. The push is a named limit, and returning a soldier on a zero input is out of scope |
| 2 | HIGH (lens 7 item 2; lens 5 T8 and lens 2 at MED) | The Marshal's Word set in the order panel drew no icon for its own button, in the bar and in the radial menu | A UI id read from a guess | `GenericVisualOrderSet.IconId` returns the set's string id, and the `Order.Icons.Square` and `Order.Icons.Round` brushes have no layer for `taom_order_type_marshal`. Tests pinned the three orders but not the set's button | A one-member set class answers `IconId` with `order_type_toggle` and keeps `taom_order_type_marshal`, with a pin and the button in both movies' human rows |
| 3 | HIGH (Tooling 1; Codex 5) | `hold_line_slot` passed on about one second of contact and never reached the `hold-slot` check | A trial rule simpler than the thing it measures | The step judged its own short window, not the census the driver judges | The step reads the driver's census after the first wound for 15 s of reads, needs completed reads after the first steer, and fails when `hold-slot` failed |
| 4 | MED (lens 5 T1; Tooling 4) | The slot measure (distance from the anchor) could not separate a clamp failure from a crowd push. A line with no wounds read `far=7` in a 5 to 1 fight | A measure that reads the effect of many causes | The contract chose distance before a game run showed what moves soldiers | `SlotProof` judges direction: pending under 5 steered reads, fail when `farther > closer`. `far=` and `max=` stay as facts. The last window at hold off is written |
| 5 | MED (lens 5 T3; Tooling 9) | `hold-strike` could never read fail, and its row could pass on a build that never cleared the flag | A verdict with no fail path | The judge returned early on the state that the fail needs | The step reads the real flag (`GetAgentFlags() & CanAttack`), and `JudgeStrike` returns when the line is not disarmed. Tests in both directions |
| 6 | MED (lens 5 T4; Tooling 3; lens 2) | Blows of soldiers the rule leaves armed counted against the held line | A count wider than its rule | The counter keyed on the company, not on the soldiers the rule disarms | A non-qualifying soldier's blow is not a line blow; a control soldier still counts |
| 7 | MED (lens 5 T5; Tooling 2) | `WoundedEver` counted living soldiers, so `hold-slot` went Pending once the wounded died | A name that says more than the code | The set dropped dead agents with the rest | A latch keeps the first wound |
| 8 | MED (lens 5 T6, T7; Tooling 5, 6) | The strike window waited on one soldier, who can die; the proof window was one-shot, so a quiet control company turned a near miss into FAIL | A trial window that assumes one actor | Each window was written for the first run's shape | The strike window closes on a blow within `HoldVerdictWaitSeconds`; the proof window takes a new baseline on NoControl and stays open. Tests in both directions |
| 9 | MED (Codex 1, 2, 4) | One cleanup exception abandoned the other soldiers; a callback fault disabled steering while the driver still disarmed soldiers; removal of the view mid-mission could leave soldiers disarmed | Cleanup that stops at the first fault | Tests faulted no soldier in the middle of a list | `ReleaseAll` tries each soldier alone and rethrows the first failure after the rest; a soldier whose restore throws is not retried. After a fault latch, the hold acts as off and pays its debts on the mission tick. The search found `RemoveMissionBehavior` only at the mission's end and in `DeploymentMissionController`, so removal in a live mission is a named limit with no guard |
| 10 | MED (Codex 6; Tooling 7) | `square_side_kept` passed on `laidOut=1`, and its company's purity read the formation's class, not its soldiers | A pattern that accepts the failure value | The row's regex ended before the value | The pattern ends `laidOut=0$`, the step fails `laid-out`, and purity reads the soldiers. Fixtures in both directions |
| 11 | MED (lens 7 item 3; Codex 7; lens 5 T9) | The Enable hint said a change applies next battle, but Spacing Repair is a patch that needs Enable at game start | A hint written before a second switch | The hint predates the spacing patch | Both hints say the repair needs Enable when the game starts |
| 12 | LOW (Codex 3, 8; lens 7 items 4 and 5; lens 2; Tooling 8, 10 to 12) | Loose text ("one rider", "does not grow when a soldier joins"), the degrade logged as a warning, a callback fault that failed no row, a failing step that hid its failed check, stale names | Small gaps in lists and text | Each item came from the contract before the engine lines or the game could correct it | Applied. Codex 3 (the climbing detachment) is rejected: its only constructor found is naval, and naval paths are excluded; the transition is a named limit in row 29 |

**Not applied:** `python3` shebangs (Tooling 13; FOLLOW-UP: no issue: existing code across the repo, outside this change). A NaN input counted as a clamp (lens 5 L3) has no producer.

## Root-cause pattern

Rows 3, 4, 5, 8 and 10 share one cause: a trial verdict that cannot fail, or that passes on less than its row claims. This is a repeat. The Seat's S6 and S7 RCA row 2 (`FastRatio`) is the same category, and the lesson "A field trial step makes its own ground" covers the setup but not the verdict. The game runs found two of these (rows 4 and 8) before the lenses did.

Rows 1 and 11 share a second cause: text written from the plan's intent. The clamp and the spacing patch were narrower than the plan, and no text was re-read against them.

## Why each agent missed these

- **The builder:** it built each verdict from the contract's sentence. It tested the pass path of each new trial rule and, for rows 5 and 10, no fail path.
- **The checker:** it read the code against the same contract, so it found each rule met its sentence.
- **Lens 5:** it found rows 1 to 8 except the panel icon, but rated the walk-back text MED. Lens 7 rated it HIGH because players read it. I took the higher rating.
- **Codex:** it found the cleanup faults in row 9, which no Claude lens raised, and row 10's purity read. It had no game evidence, so it could not see row 4.
- **Lens 2:** it found the icon chain (row 2) and rated it MED, and it listed 10 text lines as wrong. It left the callback's input frame and thread as not proved. One `native_decompile.py` run showed only the setter of the callback flag, so both stay UNVERIFIED.

## Second wave (no HIGH)

The second wave ran Standards, Efficiency, Completeness and Design at `effort: "high"`. It found no HIGH, so no third wave followed. The one decision with a possible effect on proof was W1: the trend's baseline read `Agent.Frame.origin` and the steering read `Agent.Position`. Lens 2 then showed that native stores the two apart, so W1 put both reads on `Agent.Position` (Deviations rows 36 and 45). The counts of the runs before `786df77b9` used the old reads.

The sixteen decisions W1 to W16, with the separate hold-the-line driver (Standards S3, as W17) and the move of the engine reads out of the fire table (Standards S4, as W18), are applied in `786df77b9`. That closes the three items this section left open.

## Review of the second fix round (2026-11-05)

The fix round was reviewed by seven lenses with three skeptics per finding and a critic, and by Codex at `high`. 31 findings survived the skeptics (1 MEDIUM, 30 LOW), and Codex reported 2 MEDIUM and 1 LOW. All are fixed in `786df77b9`, and an independent checker read the fixes.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 13 | MED (Codex 1; lens 5 at LOW) | A soldier who left the line still added to the trend's closer and farther counts, while `loose=` counted only soldiers in the line | One read with two membership rules | Row 38 moved `loose=` to the line and no test asked the same of the trend | `SlotTrendCount.Read` counts only `inTheLine`, and drops the open interval of a soldier who leaves it. Test first (Deviations row 46) |
| 14 | MED (lens 6) | After a quiet window the control was picked again by blows of the whole mission, which never fall, so the quiet company could be chosen again | A ranking on a total that cannot decrease | Row 37 fixed the re-pick but kept the old ranking source | `HoldRun` keeps a snapshot of each company's blows at the baseline, and the re-pick takes the busiest company of the window just judged. The run `20261105-133440` shows it (Deviations row 44) |
| 15 | MED (Codex 2) | `hold_line_slot` and `hold_line_strike` could pass when the soldiers' attack flags did not come back after the lift | A cleanup with no verdict | Only `hold_line_flag` read the flags back | Both steps fail `flag-not-restored` after `RestoreWaitSeconds` and print `restored=`. Tests on a host whose flags stay off (Deviations row 48) |
| 16 | LOW (30 lens items; Codex 3) | A press on a held company did not write its last slot line; a dead stand-aside fact; text that still said the control is "chosen once"; a pass pattern that accepted fewer steered reads than the check needs; assertions that could not fail; cost items | Small gaps after a large move | The move of the hold into its own driver changed many call sites at once | Applied. One method ends a hold (row 47); the `hold-slot` pattern needs `steered=` of 5 or more; each weak assertion was shown to fail once |

**Root cause of rows 13 to 15:** a rule changed in one place and not in its twin. Rows 13 and 14 each finished half of an earlier row (38 and 37), and row 15 is row 9's cleanup with no reader in two of the three steps that use it. The lesson below names the check: when a row corrects one reader of a rule, search for the other readers in the same change.

**Game proof:** the four hold steps ran on the build of `786df77b9` (`20261105-133440` to `20261105-134235`) and passed, each with its flags restored. The runs are in [marshals-word.md](../features/marshals-word.md).

## Feedback memories to codify

None. The lessons in `docs/reviews/lessons/testing-qa.md` carry the rules.
