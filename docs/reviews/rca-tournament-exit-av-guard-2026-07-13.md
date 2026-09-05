# RCA — Patch62 tournament-exit AV guard + correlator fix (#339), deep-review 2026-07-13

**Top-line:** The 5-agent deep-review of the #339 changeset (Patch62 `GauntletMovie.Release` AV guard + CrashReport `_PatchN` correlator resolution) returned 1 MED finding (fixed in-session), 1 LOW pre-existing-debt finding (deferred, recorded), and 1 LOW doc gap (fixed in-session). No HIGH findings. The MED finding is a repeat of the #299 apply-timing class — the rule existed and the review prompt caught it; the author didn't apply it while writing.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | Patch62 applied in `OnGameInitializationFinished`, leaving every pre-campaign `GauntletMovie.Release` (main menu, character creation, load screen, MCM) unguarded — the target is a process-wide engine method, not a campaign-scoped one | harmony-il / apply-timing (repeat of #299) | Author placed the apply next to Patch60 for file-cohesion ("the Arena block") without running the apply-timing question — *"when can the patched method first run?"* — even though `.claude/rules/harmony-patches.md` states it and `lessons/harmony-il.md` records #299. Cohesion-with-sibling overrode rule-recall. | Fixed in-session: moved to `OnSubModuleLoad` (Patch58/Patch61 precedent). No new rule needed — the existing rule + the deep-review Agent 5 rule-9 timing sub-check caught it, which is the system working. The author-side lesson: the timing question is asked from the TARGET's reachability, never from where the sibling patch happens to live. |
| 2 | LOW | `HarmonyCorrelationCollector` has no interface (registered and injected as the concrete class) — Interface Segregation rule | standards / pre-existing | Not introduced by this changeset — the class shipped that way in the original CrashReport feature; this diff only added 12 lines inside `Collect`. Verified pre-existing via `git log`/`git diff`. | **Deferred** per edit-scope discipline (a refactor doesn't ride a crash-fix PR). Recorded here; candidate for the next CrashReport-touching PR: extract `IHarmonyCorrelationCollector`, update `CrashReportIoC` + `CrashReportService`. |
| 3 | LOW | `docs/features/crash-report.md` "Harmony Correlation" row described the per-frame patch listing as if it worked for patched frames — it never did until this fix | docs | Doc predates the discovery that replacement frames resolve to `(no patches)`. | Fixed in-session: row now documents the `GetOriginalMethodFromStackframe` resolution and the #339 reference. |

## Review-verified advisories (not defects, recorded for future reviewers)

- **`GeneratedGauntletMovie.Release()` is not covered by Patch62** — verified benign on 1.4.7 (no `WidgetTemplate.OnRelease` walk, no `WidgetFactory.OnUnload` in its body, so the #339 mechanism structurally can't fire there), and shipped UIExtenderEx forces UIExtenderEx-touched movies onto the concrete `GauntletMovie` path anyway. Added to the registry entry as an engine-bump re-check item.
- **The suppressed-release event leak is provably inert in shipping** — the leaked `PrefabChange`/`BrushChange` subscriptions can never fire because their producer (`ResourceDepot.CheckForChanges`) only runs under `_uiDebugMode`. Decompile-verified by the data-flow agent; recorded in the patch doc-comment and registry so nobody re-derives it.
- **`UIContext.OnFinalize` provides no alternate re-walk route** to the corrupt widget tree (it only finalizes gamepad navigation + `EventManager`, never walks `Context.Root`) — the guard's "the re-walk never happens" claim holds on both the Patch60 and vanilla paths.

## Root-cause pattern

Finding 1 is the only systemic one: **sibling-cohesion placement beats rule recall.** When a new patch is authored "next to" an existing related patch, the sibling's lifecycle placement gets inherited implicitly. The existing rule text ("targets that fire during new-game load/main menu need `OnSubModuleLoad`") was never consulted because the placement decision didn't feel like a decision. The review layer is the designed backstop for exactly this and it fired — this RCA exists to keep the pattern visible, not to add a new rule.

## Why each agent missed / caught these

- **Agent 1 (Standards):** caught finding 2. Did not flag apply-timing (out of its rule set — correctly Agent 5's job).
- **Agent 2 (API compat):** independently identified the pre-campaign window but classified it "optional hardening" — a severity disagreement with Agent 5, resolved in favor of fixing (cheap, precedented, and the #299 lesson says this exact shape ships bugs).
- **Agent 3 (Efficiency):** nothing to catch; cold paths only.
- **Agent 4 (Completeness):** caught finding 3.
- **Agent 5 (Data Flow):** caught finding 1 via the rule-9 apply-timing sub-check added after #299 — the preventive action from that RCA demonstrably paid for itself.

## Feedback memories to codify

None new — finding 1's rule already exists in `.claude/rules/harmony-patches.md` and `lessons/harmony-il.md` (#299), and the agent prompt already enforces it. The new lesson from the *original* crash ("a fail-safe that falls back to a path re-executing the same failed operation contains nothing") was appended to `lessons/harmony-il.md` earlier this session.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
