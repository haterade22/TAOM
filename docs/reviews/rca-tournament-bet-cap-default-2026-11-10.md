# RCA: tournament bet-cap default (#none), deep-review 2026-11-10

**Summary.** Mike asked to raise the default tournament bet cap per round from 0 (unlimited) to
15,000, keeping the player's ability to set it back to 0 or raise it to the slider's max. Per
`config-providers.md` and the `orientation.md` "Persisted MCM defaults" trap, the MCM property was
renamed (`TournamentMaxBetPerRound` -> `TournamentBetCapPerRound`) so the new default reaches
existing installs, with the required HintText reset notice. Tier 2 deep-review: one Claude lens
(Agent 5, Data flow, `effort: high`) plus a Codex `xhigh` pre-review, run in parallel. Both found the
same documentation defect independently; Agent 5 additionally found a second, out-of-scope doc row
the Codex pass was not given. Full suite: 38,028 / 38,037 passed, 0 failed, 9 skipped (pre-existing,
unrelated). `check_mcm_default_flips.py`: OK against all 27 reachable release tags. No P1/P2 or
CRITICAL/HIGH code defect; no second wave needed.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | `docs/features/tournament-rewards.md` "The 50,000,000 ceiling" paragraph said "At the default of 0 the tournament screen's own wager text ... shows 50,000,000." At the new default (15,000, or 30,000 with Deep Pockets) the wager text shows that number instead; 50,000,000 appears only when the player explicitly sets the slider to 0. | Docs: a default-value change with prose that restates the default outside the Configuration table | The session updated the Overview sentence, the Configuration table row and the Changelog, all surfaces that state the default directly, but did not grep the rest of the same doc for prose describing behavior "at the default" in different words. That paragraph was written when 0 was the default and was never re-read against the new one. | Fixed before commit: reworded to "With the cap set to 0 (unlimited) ...". Caught independently by Agent 5 and the Codex `xhigh` pre-review, both before commit. |
| 2 | LOW | `docs/reference/feature-map.md`'s TournamentRewards row still read "MCM bet cap per round (default unlimited)" after the rename. | Docs: a cross-reference doc outside the file list restates the same fact | The doc updates were scoped to the feature's own doc (`tournament-rewards.md`) and the MCM HintText. `csharp-patterns.md` "Renames, moves and deletes" ("grep the old identifier across docs/ ... too") was written for a renamed *identifier*; this change renamed the identifier AND changed its default, and the grep that ran (for the old property name) does not find a doc row that states the default in prose with no identifier in it. | Fixed before commit: row now reads "(default 15,000; 0 = unlimited)". Caught by Agent 5; outside the four-file scope given to the Codex pass, so Codex did not (and was not expected to) find it. |

Also reviewed, not entered as findings (disputed or optional, not bugs):
- **Fallback-literal duplication** (`TaomSettings.TournamentBetCapPerRound = 15_000` and
  `TournamentRewardsSettingsProvider`'s `SettingClamp.Clamp(..., 15_000, ...)` fallback are two
  independent literals). This was caught and fixed during the session's own `/verify` pass (the
  `Fallbacks_MatchTheCompiledDefaults` test failed RED immediately after the rename, before either
  review agent ran): the normal TDD cycle working as intended, not a missed review finding.
  Codex explicitly disputed treating the remaining duplication as a current defect (the bandit-scaling
  settings duplicate the same way, and the test guards it); a shared constant was judged **Reject**
  under `simplicity-criterion.md` (tiny win, and it would break the sibling convention other
  providers in this file use for no observable gain).
- **Test precondition hardening**: `Fallbacks_MatchTheCompiledDefaults` did not assert
  `TaomSettings.Instance == null` before relying on the no-MCM fallback path. Both agents flagged
  this as optional (PRESERVING, no behavior change); applied, matching the existing precedent at
  `HoldTheWallsSettingsTests.cs:26`. Re-run green (165/165), which also confirms the precondition
  actually holds in this test host.
- **A fact disagreement between the two reviewers, resolved by re-reading the source.** Agent 5
  cited the engine's `MAX_AMOUNT` line as `TournamentVM.cs:914`; Codex's own live decompile cited
  `:922` (the pre-existing, unchanged citation). Per `evidence-over-claims.md` ("when two lenses
  disagree ... re-read the source yourself"), the orchestrator read the project's own pinned v1.5.6
  decompile (`pwsh tools/taom-src.ps1 path TournamentVM`, the same cache the harmony-patch registry
  was built against): the three `Tournament.GetMaximumBet()` call sites are at lines 206, 914 and
  1020. Agent 5 was correct; the doc edit (already citing `:914`) stood as written. Line numbers
  differ between decompilers; both cite the same statement, so the doc follows the project's own
  pinned cache rather than either agent's number on its own.

## Root-cause pattern

Findings 1 and 2 are the third occurrence of the same category: an MCM default change leaves stale
prose somewhere other than the one surface the session remembered to update. The first two
(2026-05-29, 2026-09-11, both on the bandit-scaling settings) produced the lesson "A changed MCM
default is announced in the HintText, because the tooltip is what the player reads"
(`docs/reviews/lessons/localization-ui.md`). This time the HintText was correct on the first pass
(it states the new default and the reset notice, confirmed by both reviewers); the drift moved to
two surfaces the existing lesson does not name: a same-doc explanatory paragraph, and a separate
cross-reference doc. The general shape repeats: there is more than one reader of a stated default
(the player reads the HintText; the next developer reads the feature doc and the feature map), and
each reader's surface needs its own grep, not just the one the session happened to be editing.

## Why each agent missed or caught these

- **Agent 5 (Data flow)** caught both findings by tracing the compiled-default value forward into
  every consumer of that fact, prose included, per its brief's explicit instruction to check "whether
  any other doc or hint still states or implies the old default."
- **Codex pre-review** independently confirmed finding 1 from its own installed-DLL decompile and
  explicitly disputed two of the six "Known Suspects" the brief raised (the fallback-duplication and
  the no-issue-number date citation) with cited evidence rather than taking the brief's framing on
  faith, the intended effect of writing suspects as hypotheses to confirm or dispute, not claims.
  It did not find finding 2 because the dispatch prompt's file list did not include
  `docs/reference/feature-map.md`; that is a scoping gap in the prompt, not a miss by Codex.
- **No other lens ran.** Tier 2 (an MCM settings default and rename; no Harmony patch, GameModel,
  save data, co-op, native call, mission/campaign tick, or live-install file touched) runs only
  Agent 5 in its first wave; Agents 1/2/3/4/6/7/Tooling are SKIPPED BY TIER, and no CRITICAL or HIGH
  was found, so no second wave ran.

## Feedback memories to codify

None beyond the lessons entry below. This is the existing bandit-scaling-MCM pattern recurring with
a new specific trap (prose outside the HintText and the Configuration table); no new standing rule
is needed beyond extending that lesson's scope.

## Follow-up (pre-existing, out of scope)

`FOLLOW-UP` (no issue: cosmetic, pre-existing, unrelated to this change): `Patch96_TournamentRewards.cs`'s
doc comment and the matching `harmony-patch-registry.md` entry both cite the bet-reading UI call
sites as `TournamentVM.cs:207, 922, 1066`. Against the project's own pinned v1.5.6 decompile the
actual lines are 206, 914 and 1020, a pre-existing decompile-line citation drift, not caused by this
change (neither file was touched here) and not load-bearing (nothing executes from the comment
text). Left for whoever next touches that patch or registry entry.
