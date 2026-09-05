# RCA — TroopWeight result-frame penalty fix, deep-review (2026-07-17)

**Top line:** A player reported "when you load saved game max troop seems always go over" (recruitment popup
showing **114 / 111**). Investigation found the over-cap state itself is *intended* (see below), but turned up
a real arithmetic bug in the party-size weight penalty: the surplus was computed in the **result** frame and
applied in the **base** frame, so every factor-boosted culture was over-taxed by `penalty × SumOfFactors`.
The 5-agent deep-review then found 2 more real issues on the fix — 1 HIGH (standards) and 1 MEDIUM
(data-flow). Both verified against source and fixed in-session; full suite green (4363).

## What the report actually was

Three things at three confidence levels, separated deliberately because conflating them is how the wrong fix
gets shipped:

1. **Over-cap by construction — intended, NOT a regression.** `limit = base − (weighted − raw)`. The recruit
   gate stops at `weighted = base`; each subsequent troop UPGRADE raises `weighted` without raising `raw`,
   sliding the limit under the existing party. Nothing evicts (the shed skips `MainParty` by design). Vanilla
   treats over-cap as a fully-modeled normal state — it renders "114 / 111" in red at four separate sites
   (`RecruitmentVM.cs:841-847` is literally the screenshot) and charges sqrt-scaled morale plus a *guaranteed*
   ≥1 desertion/day (`DefaultPartyDesertionModel`: `MathF.Max(1, (int)(num*0.25f))`, `useProbability:false`).
   The pre-rework design reached the identical `weighted > base` state, so the 2026-07-11 count→limit rework
   made this **visible**, not worse. **User decision 2026-07-17: the mechanic is intended and stays.**
2. **The frame bug — fixed here.** See findings table.
3. **The "on load" framing — UNPROVEN.** Leading hypothesis: `PartyBase._cachedPartyMemberSizeLimit` is
   `[CachedData]` (not saved) and invalidated ONLY by `MemberRoster.VersionNo`; if anything reads
   `PartySizeLimit` before `CareerCampaignBehavior.OnSessionLaunched` runs `RefreshCache`, the limit is
   computed without the career `+2…+6` PartySize passive and stays wrong until the roster changes.
   A temporary `[PartySizeDiag]` one-shot probe ships to answer this. **Not asserted as fact.**

## Findings

| # | Sev | Bug | Category | Why the implementation had it | Preventive action |
|---|-----|-----|----------|-------------------------------|-------------------|
| 1 | MED-HIGH | `ApplyPartySizeWeightPenalty` computed the penalty in the RESULT frame (`baseLimit = (int)limit.ResultNumber`; `raw`/`weighted` are absolute body counts) but applied it via `limit.Add(-penalty)`, which lands in the BASE frame. `ExplainedNumber` result is `BaseNumber * (1 + SumOfFactors)`, so the penalty was silently amplified: a 9-man surplus cost 11.25 slots at +25%. Also broke the documented "limit never drops below 1" floor, since `maxReducible = baseLimit - 1` is likewise result-frame. | Frame mismatch | I reasoned about `Add` vs `AddFactor` **ordering** and concluded "applied last ⇒ clamped against the boosted limit" — a comment that shipped 2026-07-11 and was factually wrong. `ExplainedNumber` SUMS factors; ordering has no effect on an `Add`. I never checked the struct's arithmetic, only its call sequence. | `SubtractResultFramePenalty` divides `(1 + SumOfFactors)` back out. Rule: **when mutating an `ExplainedNumber` you did not construct, state which frame your value is in.** An absolute count is result-frame and must be scaled before `Add`. |
| 2 | HIGH | `PartySizeLoadOrderDiagnostic` (new, temporary) called `IoC.Resolve<IModLogger>()` from a static helper invoked by a GameModel override — a service-locator violation. | Service locator | I chose it deliberately to avoid editing single-owner `SubModule.cs` for throwaway code, and rationalised it via the one-shot `_logged` guard (which makes it *cheap*, not *legal*). The rule explicitly says a guard does not launder an `IoC.Resolve` outside a boundary; "it's temporary" is not a carve-out either. | Logger injected into `TaomPartySizeModel`'s ctor and threaded to the helper; `SubModule.cs:606` resolves it at the registration boundary like every sibling model. **Temporary code follows the same architecture rules as permanent code** — it ships to players identically. |
| 3 | MED | `int baseLimit = (int)limit.ResultNumber` was unguarded against non-finite input. `(int)float.NaN` **and** `(int)float.PositiveInfinity` are both `int.MinValue` on net472/x64; `int.MinValue - 1` underflows (unchecked) to `int.MaxValue`, **silently defeating** `ComputeSizePenalty`'s `maxReducible <= 0` guard — which exists precisely to reject a degenerate baseLimit. Verified empirically on net472: `ComputeSizePenalty(10, 200, (int)NaN)` returned **190**, not 0. Blast radius: the poisoned value is cached into `_lastBaseLimit`, and `party.PartySizeLimit` performs the same cast, so the shed's `raw <= deflatedLimit` early-out never fires and `PlanShed` receives an astronomically large budget → sheds the party's entire non-hero roster. | NaN gate (5th instance) | I applied the engine-float gate rule rigorously to the code I was *writing* (`SubtractResultFramePenalty`'s `!(scale > MinFactorScale)` positive requirement) and not at all to the pre-existing line two rows above it in the same method. The rule was in my head as "guard the new gate," not "guard every float→decision path in the method I am touching." | Guard the **cast**: `if (!FiniteFloatValidator.IsFinite(limit.ResultNumber)) return;` before it, plus `if (baseLimit < 2) return 0;` in `ComputeSizePenalty` so the subtraction is unreachable for degenerate values. 2 regression tests pin both. **Widened the rule** — see below. |

Not reachable via current TAOM content (every authored party-size feat and career passive is a finite
positive constant, and no negative-factor party-size feat exists) — hence MED, not HIGH. It is a real,
verified, single point of failure that turns a graceful no-op into a roster wipe if any upstream ever
produces NaN (another mod mutating the `ExplainedNumber`, a future negative-factor feat, a config error).

## Root-cause pattern

Findings 1 and 3 are the same failure at two scales: **I reasoned about the code I was writing and not about
the frame/domain of the values flowing into it.** #1 took a result-frame number into a base-frame mutation;
#3 took a non-finite float into an int cast whose overflow semantics defeat a downstream guard. In both the
*local* logic is correct and the *seam* is wrong — which is exactly why the data-flow agent is the only one
of the five that found #3, and why it has now found the load-bearing bug in three consecutive TroopWeight
reviews (2026-07-11 GAP#1, and #3 here).

Finding 2 is a different, more uncomfortable pattern: **I knew the rule, predicted the violation, and shipped
it anyway** because the code was labelled temporary and the compliant path touched a single-owner file. The
review caught what I had already privately reasoned past.

## Why each agent missed what it missed

- **Agent 1 (Standards)** — caught #2 cleanly. Structurally could not catch #1 or #3: both are arithmetic/
  frame bugs, not architecture.
- **Agent 2 (API compat)** — independently verified all six `ExplainedNumber` semantics claims against the
  installed v1.4.7 DLL and confirmed `DefaultPartySizeLimitModel` never calls `LimitMin`/`LimitMax` (zero grep
  hits), which is what makes the fix's math exact. It validated the fix but was never scoped to hunt #3.
- **Agent 3 (Efficiency)** — verified the `_logged` guard short-circuits before the `IoC.Resolve`, correctly
  concluding there is no *performance* problem. This is a good example of two agents reaching opposite-looking
  verdicts on the same line and both being right: cheap ≠ compliant. Do not let a perf PASS retire a
  standards FAIL.
- **Agent 4 (Completeness)** — found the real doc/CHANGELOG/localize gaps. Its one wrong call: it wanted the
  diagnostic stripped before commit. It ships first, gets stripped after the trace — the agent lacked the
  session context that the probe is the whole point.
- **Agent 5 (Data Flow)** — found #3, and correctly characterised #1's blast radius across 6 engine consumers
  plus the shed-equivalence repair. Third consecutive review where this agent found the only finding the
  other four structurally could not.

## Preventive actions taken

1. **Widened the NaN-gate rule scope (5th instance).** `.claude/rules/csharp-architecture.md` already names
   two categories — config floats at load, engine floats at runtime decision gates. Neither names
   **float→int casts whose overflow value defeats a downstream integer guard**. The rule's own text says: *"if
   a 5th instance appears in a category this section doesn't name, widen the scope again rather than patching
   the instance."* Done — lessons entry below.
2. **Corrected the false ordering comment** at `TaomPartySizeModel.cs:40-41`; it had been actively teaching
   the wrong `ExplainedNumber` model to every future reader.
3. **Flagged but did NOT fix** the identical amplification in `_careerPassives.ApplyFlat` one line above
   (a "+2 party size" passive reads as +2.2 at Mordor +10%, and a +6 passive as +8.4 at Goblin +40%). Same bug
   class, but correcting it is a live balance change the user did not ask for — separate issue.

## Behavior change (flag to balance ownership)

The fix RAISES the party-size limit for every factor-boosted culture (Mordor +10%, Isengard/Gundabad/
Dol Guldur +20%, Misty Mountain Orcs +30%, Goblin +40%). Six consumers pick it up transparently and all move
in the "less punishing" direction: desertion excess, morale penalty, party speed ratio, recruitment gate,
prisoner-recruitment gate, army-attach summation. This is the intended direction of a bug fix — the previous
build was taxing those cultures' entire party-size ecosystem harder than their culture card advertises — but
it is a real, live shift.

Secondary win: the shed hook's comment claims `raw > deflatedLimit` is "equivalent to the old
`weighted > base limit`". That equivalence was **false pre-fix** for any `scale ≠ 1` and is now exact, so AI
parties of factor-boosted cultures were being over-shed and no longer are.

## Lessons codified

Appended to `docs/reviews/lessons/gamemodels-services.md`:
- "An absolute count subtracted from an `ExplainedNumber` must be scaled into the base frame."
- "Guard the float→int CAST, not the arithmetic downstream of it."

Both fold into the existing category; no new feedback-memory files. Finding #2 is a discipline failure, not a
knowledge gap — the rule was known and documented; it needs no new rule.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
