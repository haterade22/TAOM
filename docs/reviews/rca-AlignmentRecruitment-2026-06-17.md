# RCA — AlignmentRecruitment Codex Adversarial Review (2026-06-17)

## Top-line

Codex (`gpt-5.5` @ `xhigh`) reviewed the AlignmentRecruitment feature. Verdict: **0 critical, 1 HIGH, 1 LOW.** All 6 Known Suspects came back DISPUTED or DESIGN-QUESTION — no code defects in the runtime path. Both confirmed findings were fixed in-session; a third (meta) finding is a deep-review agent accuracy issue, not a code bug.

Prompt: `docs/reviews/codex-adversarial-AlignmentRecruitment-2026-06-17.prompt.md`. Raw output: `docs/reviews/codex-adversarial-AlignmentRecruitment-2026-06-17.md`.

## Findings

| # | Codex Sev | My Sev | Bug | Category | Why missed | Preventive action |
|---|-----------|--------|-----|----------|------------|-------------------|
| 1 | HIGH | MEDIUM | `IsRecruitmentBlocked_GoodRejectsEvilMode_MatchesMatrix` covered 6/9 (recruiterSide × sourceSide) cells — omitted (Evil,Neutral), (Neutral,Free), (Neutral,Neutral) — while `docs/features/alignment-recruitment.md` claimed "one case per cell." | Test coverage gap | The **symmetric** matrix was written as the full 3×3 (9 rows); the **second** mode (GoodRejectsEvil) was trimmed to a "representative" subset. The per-branch-dispatch rule (`feedback_per_branch_dispatch_test_enumeration.md`) was applied to the first matrix but not re-applied to the second. | When a doc claims per-cell coverage, **every** mode/branch matrix must be the full N×N, not just the primary one. Fixed: added the 3 missing `DataRow`s (now 9/9; suite 28→31). No runtime risk (all 3 cells are Neutral→no-block, behaviorally covered by the shared Neutral early-return). |
| 2 | LOW | LOW | Feature-doc How-To implied editing JSON `mode`/`applyToAi` changes runtime behavior, but in-game `TaomSettings.Instance` (MCM) shadows JSON via `?? _defaults`. | Doc accuracy | Documented the JSON knobs as live config without stating MCM precedence — despite the MCM-over-JSON pattern being deliberately designed (mirrors `CastleRecruitmentSettingsProvider`). | Fixed: How-To now states MCM is authoritative in-game and JSON is the compiled/test default. Generalizes the `csharp-architecture.md` "Config Providers MUST Validate → state the reload scope explicitly" rule to also state MCM-over-JSON precedence. |
| 3 (meta) | n/a | n/a | Deep-review Agent 2 (compatibility) asserted `MaximumIndexGarrisonCanRecruitFromHero` has "zero callers in v1.4.6." Codex found it **is** called — `GarrisonRecruitmentCampaignBehavior` invokes `VolunteerModel.n(town.Settlement, notable)` (obfuscated `MaximumIndexGarrisonCanRecruitFromHero`) at two sites. | Review accuracy / grep scope | Agent 2 grepped only the `~/.taom-src` cache (which had not decompiled `GarrisonRecruitmentCampaignBehavior`), then stated a confident "zero callers." The feature conclusion (garrison non-override is safe) survived — but for a *different* reason than Agent 2 gave. | "zero callers" / "not found" claims in reviews must grep the **full** decompile tree (`E:\Decompiled_Bannerlord`), not just the on-demand cache. Noted in `AGENTS.md`. |

## Root-cause pattern: second-instance trimming

The two real findings share one shape: the **primary** case was done right and the **secondary** case was under-specified.
- Symmetric matrix = full 9 cells; the **second** mode's matrix was trimmed.
- The **first** documented config knob's behavior was correct in spirit; the doc never stated the MCM-over-JSON precedence that governs **both** knobs at runtime.

This is the same class as the native-C++-port miss (`feedback_native_port_hot_path_audit.md`): the structural/primary work consumes the attention budget and the parallel/secondary instance inherits a gap. The fix is a deliberate "apply the rule to every instance, not just the first" pass.

Neither finding is a runtime defect. The feature's block logic, config validation, ADR compliance, API usage, and performance were all clean (Codex DISPUTED every behavioral suspect with decompiled evidence, and the `/deep-review` pass found 0 code issues).

## Why each deep-review agent missed these

- **Agent 1 (Standards):** scope is ADR/style, not test cell-count or doc wording. Correctly out of scope.
- **Agent 2 (Compatibility):** got the override signature + `-1`-blocks-all right, but produced the false "zero callers" claim (finding #3). Confident assertion from a cache-only grep.
- **Agent 3 (Efficiency):** N/A — no perf dimension to these findings.
- **Agent 4 (Completeness):** checked "is the matrix covered under both modes" and reported COMPLETE by eyeballing *presence*, not by *counting cells* against the doc's per-cell claim. The miss for finding #1.
- **Agent 5 (Data Flow):** actually *noted* the MCM-shadows-JSON mechanism (its observation #4) as "designed precedence" — but did not cross-check the feature-doc How-To wording against it. The miss for finding #2: when an agent surfaces a shadowing/precedence behavior, it should cross-check the doc's user-facing claims against that behavior.

## Feedback memories to codify (light)

- Extend `feedback_per_branch_dispatch_test_enumeration.md`: when a feature has **multiple modes** over the same axes and the doc claims per-cell coverage, each mode's matrix must be full N×N — don't trim the secondary mode to "representative" cells.
- `AGENTS.md`: Codex did well here (decompiled `Hero.MapFaction` + the mercenary `ChangeKingdomAction` chain to DISPUTE the recruiter-basis suspect with evidence; caught a deep-review agent's false "zero callers"). Recorded as a "what Codex does well" datapoint.

No new standalone rule file is warranted — both findings map onto existing rules that simply weren't applied to the second instance.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
