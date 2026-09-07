# RCA: TroopWeight usage-frame reframe (#545), 2026-09-06

**Change under review:** the TroopWeight "elite tax" kept deflating the party-size limit (enforcement
unchanged) while its *presentation* moved to the numerator, capacity readouts now show
`weighted-used / true-base` (19 / 20) instead of `raw / deflated` (10 / 11).

**Review:** `/deep-review`, 5 agents. **6 confirmed findings** (1 HIGH, 2 MEDIUM, 3 LOW). All were
verified against decompiled v1.4.8 before being acted on; none were taken on an agent's word.

**Top line.** Every finding traces to one root cause: **re-expressing one half of a paired value while
leaving the other half on the old pairing.** Vanilla computed a party-size label and its red
over-capacity tint from a *single shared denominator*, so they could not disagree. This change gave the
label a different denominator and left the tint alone. The same shape explains the docstring that
wrongly claimed the change was cosmetic: the properties being rewritten were half of a pair whose other
half (a confirmation prompt) was never looked at.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | **HIGH** | Party-screen header renders `30 / 100` (comfortably under) beside vanilla's still-red over-capacity tint. Vanilla drives the tint from `RightPartyMembersSizeLimit < MemberRosters[1].TotalManCount`: a live numerator over a denominator frozen at screen-open (`PartyScreenLogic.cs:491`, assigned exactly once). The label used that same frozen denominator, so the two could never disagree; swapping the label's denominator to the true base decoupled them. Worst case is reachable by the exact remediation workflow the feature exists to prompt: a party whose surplus exceeds its whole base limit has its penalty clamped, freezing the deflated limit at 1 against a true base of 100, and dragging the heavy troops off then shows a healthy fraction under a red warning. | Display pairing | I changed the label and never asked what *else* consumed the number it was paired with. I read `RefreshPartyInformation` to find the label assignment and stopped: the tint assignment is **two lines below it in the same method** (`PartyVM.cs:3069`). | Fixed: `BuildLabel` now also returns `IsOverCapacity`, and the caller **clears a stale tint (downgrade only)** so it can never fabricate a warning or bypass a vanilla mode gate. New lesson, below. |
| 2 | **MED** | `TroopWeightDisplayHook`'s docstring asserted "Nothing in this class may feed a gameplay decision." False. `RecruitmentVM.ExecuteDone` gates its "Over Limit" confirmation on `CurrentPartySize <= PartyCapacity`, and the party screen's done-path reads `IsMainTroopsLimitWarningEnabled`, all properties this hook rewrites. | False invariant in a comment | I classified the change as "display-only" from the *name* of the properties (`…Text`, `…Size`, `…WarningEnabled`) instead of grepping their consumers. A VM property is not display-only because it looks like a label. | Docstring corrected to name both prompts. `WeightedFrameIdentityTests` now pins `raw > deflated ⟺ weighted > base` across every weight in `troop_weights.xml`, so a weight-table or clamp change cannot silently move a confirmation threshold. |
| 3 | **MED** | `CampaignUIHelper_GetPartyHealthTooltip_Patch` was dead code. `GetPartyHealthTooltip(PartyBase)` never emits a `Land Troop Capacity` row at all: that row exists only in the parameterless `GetMainPartyHealthTooltip()`, and it has no caller in any shipped client assembly. The hook looped for a label that could not be present. Docs, CHANGELOG and issue #545 all advertised an "any-party" capacity rewrite that did not exist. | Dead patch / unverified claim | I recovered the patch target from the 2026-07-11 deletion set (`git show bee07b48^`) and treated "it was a live patch once" as evidence it does the job now. The deleted version rewrote *Battle Ready / Wounded* rows, which that method **does** emit; my version rewrites the capacity row, which it does not. I reused the target without re-reading the body for the row I actually needed. | Patch deleted; `TroopWeightIoC` and the interface doc record why. New lesson, below. |
| 4 | LOW | Degenerate-input divergence: when `WoundedCount > Number`, vanilla's `Sum` yields 0 for both healthy and wounded (the troop vanishes from the header); `BuildLabel` clamps and reports the entry as fully wounded. | Reimplementation fidelity | I diffed my label reconstruction against vanilla's *branches* and variable names but not its *degenerate arithmetic*. | Left as-is deliberately: the defensive clamp is the better behavior and the input violates a roster invariant. Recorded here so it is a known divergence rather than a surprise. |
| 5 | LOW | `_lastBaseLimit` (a `ConditionalWeakTable`) is empty for a `PartyBase` nothing has ever queried, so `GetTrueBaseSizeLimit` transiently falls back to the deflated limit and the surface renders the plain vanilla fraction until something first reads `PartySizeLimit`. | Cache warm-up | Known and deliberate at design time; the review confirmed it is genuinely narrow and self-healing. | No change. `DisplayLimit`'s fallback is one-way (never invents a larger limit), which is what makes this safe. Documented as a known limitation. |
| 6 | LOW | Toggling `EnableTroopWeight` off mid-session does not force a re-render, so an already-open screen can keep a rewritten label or a `×N` name suffix until its next natural refresh. | Toggle liveness | The toggle correctly gates every *new* call; I did not consider already-rendered state. | No change, self-healing on the next screen interaction. Documented as a known limitation. |

---

## Root-cause pattern: re-expressing half of a paired value

Findings 1 and 2 are the same mistake at two scales. A UI number rarely travels alone: it is paired with
a tint, a warning flag, a confirmation threshold, or a sibling label, and vanilla keeps the pair coherent
by feeding both from one source. Changing what one half means silently breaks that coherence, and the
break is invisible in the file you edited. It lives in the consumer you did not open.

The existing deep-review prompt has a "Parallel Method Consistency" check (CanAfford / Spend / Display
sharing a cost derivation). That rule is written for *methods*. Neither it nor any TAOM rule covered
**paired display values**, which is why five agents' worth of per-file review passed the change and only
the cross-system data-flow agent caught it, and it caught it by tracing the engine consumer, not the
TAOM source.

Finding 3 is a different pattern worth naming separately: **a recovered patch target is not a recovered
patch.** Reinstating a deleted patch proves the *method* exists, not that it produces the thing the new
hook is looking for.

---

## Why each agent missed these

| Agent | Result | Why it did not catch #1 |
|---|---|---|
| 1: Standards | PASS (correctly) | Its checklist is structural: ADRs, IoC, categories, nullable. Coherence between a label and a tint is not a standards question. |
| 2: API compatibility | Caught #3 and #4 | It verified every signature and diffed my label reconstruction against vanilla's, thorough on *the methods I touched*. The tint is a different property in the same method that I never touched, so it was outside the diff's scope. |
| 3: Efficiency | PASS, one UNVERIFIED | Purely a cost review. It correctly flagged tooltip cadence as unverified rather than guessing; resolved to per-hover by decompiling `BasicTooltipViewModel.ExecuteBeginHint`. |
| 4: Completeness | COMPLETE | Docs/tests/registries/localization. It cannot see semantic incoherence. It also mis-reported the CHANGELOG as nested under another feature (it is a correct sibling `###`): a reminder that agent findings are hypotheses. |
| 5: Data flow | **Caught #1, #2, #5, #6** | The only agent that opens engine consumers. Its instruction to trace a value to *everything* that reads it is what found the tint and `ExecuteDone`. |

The lesson mirrors the NavalTravel #296 and SaveTableau #299 pattern already on file: **per-file review
structurally cannot catch coupling that lives in the engine consumer.** Agent 5 remains the highest-value
agent, and its prompt should grow, not shrink.

---

## Lessons to codify

Appended to `docs/reviews/lessons/localization-ui.md`:

1. **When you change what a displayed number means, grep every consumer of the value it was paired with**:
   tint, warning flag, confirmation threshold, sibling label. Vanilla keeps pairs coherent through a
   single shared source; redefining one half breaks that silently, and the break is never in the file you
   edited.
2. **A VM property is not display-only because its name looks like a label.** Grep its consumers before
   writing "cosmetic" in a comment. `CurrentPartySize` and `IsMainTroopsLimitWarningEnabled` both gate
   confirmation dialogs.
3. **Recovering a patch target from git history is not evidence the patch does what you now want.**
   Re-read the target body for the specific thing your hook consumes: the old patch may have consumed
   something else entirely from the same method.

No new feedback memory: these are subsystem lessons about UI coupling, not facts about how the user
wants work done.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/troop-weight-system.md](../features/troop-weight-system.md)

<!-- backlinks-end -->
