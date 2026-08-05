# RCA — Career UX Arc (#377–#384) Deep Review, 2026-08-05

**Top-line:** the 5-agent deep review of the adopted-external career UX arc returned 7 findings
(0 ship-blocking at runtime, 2 silently-broken-visuals, 1 stale-stats gap, 4 minor). All confirmed
findings were verified against source before fixing (evidence-over-claims §A). Fixes landed same
session; suite 5506 green after. A Codex adversarial pass ran in parallel — its findings are
verified and appended in the Codex section below when complete.

Scope context: the arc was adopted from an external reference module via `/adopt-external`
(verdict + provenance: `~/.claude/plans/review-this-and-identify-bubbly-moon.md`); all reviewed
code is TAOM-authored.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | Hero death clears the ally-buff dictionary AND `_activeContexts` (killing the scheduled restores), but never calls `UpdateAgentProperties()` on the buffed allies — their baked-in speed/damage/draw-speed stats persist for the rest of the mission (engine recomputes only on equipment/ammo events, decompile-verified). | Stale state / lifecycle | #377's state matrix asked "when does the tracker ENTRY die?" — not "when are the CONSUMERS of the entry's side effects refreshed?" The dictionary is TAOM state; the baked stats live engine-side and need an explicit recompute. Pre-existing shape, but #377 took ownership of buff lifecycle. | FIXED: `GetBuffedAllyIndices()` snapshot before the clear + `Mission.FindAgentWithIndex` refresh loop (verified public on installed v1.4.7). Lesson appended to `lessons/state-lifecycle-save.md`. |
| 2 | MED | `Brush="ButtonBrush1.Text"` on the new badge TextWidget references a brush that exists in NO brush file — `BrushFactory.GetBrush` returns null silently (decompile-verified), so the text renders unstyled/possibly invisible. Copied from the now-deleted `AbilityHUD.xml`, which carried the same dead reference. | Convention inconsistency (silent asset miss) | `gui-ui.md` "Sprite References" mandates verifying every `Sprite="X"` against the sprite registry — but said NOTHING about `Brush="X"`, which fails the same silent way. The bad string looked legitimate because a shipped prefab used it. | FIXED: registered `CareerSystem.Badge.Text` in `Brushes/CareerSystem.xml` + repointed the badge. RULE WIDENED: gui-ui.md sprite-verification rule now covers brush names. Lesson appended to `lessons/localization-ui.md`. |
| 3 | MED (pre-existing, DEFERRED with record) | `CharacterDeveloper.SkillNameText` (9×) and `CharacterDeveloper.DescriptionText` (5×) in `CareerScreen.xml` — plus `ButtonBrush1.Text` in `PresetsOverlay.xml:26` — are equally unregistered. The career screen has been rendering those labels with DEFAULT brush styling since May, and the user visually approved the screen in that state (2026-05-31 screenshots). | Convention inconsistency (pre-existing) | Same blind spot as #2, predating this arc. | DEFERRED deliberately (no-silent-deferrals rule): changing 14 text styles on a visually-approved screen needs its own in-game pass. Recorded here + CHANGELOG known-limitation. Candidate: repoint at `CharacterDeveloper.MainSkill.Name.Text` / `.MainSkill.Description.Text` or register TAOM brushes matching the current (default) look. |
| 4 | LOW | `OnScoreHit`'s career-hero identity gate dropped the `IsActive()` conjunct that `OnMissionTick` and the AgentStatus mixin share — three copies of one predicate, already drifted within a single session. | Convention inconsistency | The predicate was written three times in one arc; nothing bound them. | FIXED: extracted `CareerHeroIdentityGate.IsCareerHeroAgent`, all three sites now call it. |
| 5 | LOW (latent, not reachable) | `min_cooldown_seconds` floor (5s) sits below every ability duration (8–10s); a future `CooldownReduction` retune ≥22s total would let a second activation start inside a live window (HUD restart + notice re-arm; buffs compose correctly via refcounting). Current data maxes at 15s reduction → 15s effective. | Logic error (latent invariant) | The floor and the durations live in different XML files; no invariant connected them. | FIXED (prevention): real-XML invariant test `RealTuningXml_WorstCaseCooldown_NeverBelowAbilityDuration` in `CareerChoicesIntegrationTests` — a future retune fails CI. |
| 6 | LOW | `CareerAbilityBuffTracker.Add[Ally]Contribution` double-looked-up the dictionary (`GetBuff` + indexer). | Perf micro | Written for symmetry with the read paths. | FIXED: `TryGetValue` single lookup. |
| 7 | MED perf (REJECTED fix) | `RebuildChoiceGroups` calls `KeystoneExclusivityRule.IsLocked` per choice → ~1–2k dictionary lookups per screen click. | Perf (per-click) | Not missed — a deliberate trade. | REJECTED per `simplicity-criterion.md`: per-click microseconds vs a memoization layer + its tests. Verdict recorded here so it isn't re-litigated. |

**ADR-002 note:** `CareerPerkMissionBehavior` is 207 lines (pre-arc: 166 — already over the
150 ceiling before this work). The OnScoreHit body was extracted to
`AbilityDamageAttributionReporter` (boundary reporter, per-mission lifetime); every remaining
handler is guards + delegation. The residual overage is a pre-existing condition recorded here
for a future decomposition pass — not smuggled into this arc (edit-scope discipline).

## Root-cause pattern

Two of the three real code findings (#2, #3) share one theme: **Gauntlet fails silently on
unregistered ASSET NAMES of every kind — sprites AND brushes — and the existing rule only
policed sprites.** The rule scope was one asset category narrower than the failure class
(the same shape as the NaN-gate scope-gap history). Fixed by widening the rule, not just the
instance.

Finding #1's theme: **TAOM-side cache clears are not engine-side refreshes.** Any cached
combat-stat state consumed via `AgentDrivenProperties` needs an explicit `UpdateAgentProperties`
on every agent that baked it in, on EVERY clear path (expiry restores had it; the death path
didn't).

## Why each agent missed what the others caught

- **Standards (Agent 1):** rule set covers ADRs/IoC/naming — no asset-name registry checks, no
  cross-path lifecycle tracing. Passed everything in its scope; its 190-line measurement of the
  behavior undercounted (207 actual) but the ADR question was handled in the fix pass anyway.
- **Compatibility (Agent 2):** caught #2/#3 (brush names) — the only agent that greps asset
  registries. Could not see #1 (lifecycle, not API).
- **Efficiency (Agent 3):** caught #6/#7. Not scoped for asset names or lifecycle.
- **Completeness (Agent 4):** checks artifacts exist, not whether references resolve.
- **Data Flow (Agent 5):** caught #1/#4/#5 — the lifecycle/parallel-consistency lens. Did not
  grep brush registries (its sprite check verified the keystone sprite names, which were fine —
  the brush gap sat outside its rule 7 wording too).

## Feedback memories / lessons codified

- `lessons/localization-ui.md`: "Verify Brush= names like Sprite= names — BrushFactory nulls silently."
- `lessons/state-lifecycle-save.md`: "Clearing cached agent-stat state must refresh every agent that baked it in, on every clear path."
- `gui-ui.md` "Sprite References" section widened to brushes (same commit).

## Codex adversarial pass (completed same session)

Raw: `docs/reviews/raw/codex-adversarial-career-ux-arc-2026-08-05.md` (gpt-5.5, xhigh, 292k tokens).
**0 P1 / 2 P2.** All six architecture Known Suspects DISPUTED with decompiled evidence —
independently confirming the Claude compatibility agent's verdict on the energy-bar design
(datasource chain, `[ViewModelMixin("Tick")]` transpiler mechanism, filename-keyed prefab
patching, insert-index semantics, clock unity, careers-XML integrity).

| # | Codex Sev | Verified Sev | Agree? | Verdict |
|---|-----------|--------------|--------|---------|
| C1a | P2 | P2 | YES | `isSiegeEngineHit` not filtered — siege-missile hits arrive with the operating player as affector (verified `Mission.OnAgentHit`: `isSiegeEngineHit = missile.MissionObjectToIgnore != null`), and the agent-stat buff does not drive siege damage, so attribution would be a FALSE claim. **FIXED**: early return on `isSiegeEngineHit`. |
| C1b | P2 | DISPUTED | NO | "Vanilla normalizes mount→rider before OnScoreHit" — the quoted normalization does NOT exist in the installed v1.4.7 `Mission.cs` (`OnAgentHit` passes the raw affector; for charge blows the attacker IS the mount). Normalizing locally would ATTRIBUTE on charge hits, but whether `DamageMultiplierBonus` amplifies mount-charge damage is unverified — same false-claim risk class as C1a. Keeping attribution scoped to direct hero hits is the honest behavior; recorded here, revisit only with evidence the buff applies to charge damage. |
| C2 | P2 | P2 | YES | Olog Hai overlap: Duration mutations (+4/+2, verified in `taom_career_choices.xml`) reach a 16s window vs a 15s cooldown floor — 1s where `IsReady && IsActive`, a recast double-stacks contributions. **FIXED**: `AbilityActivationController` now treats a live window as not-ready (`IsAbilityActive` gate — blocks activation AND defers the ready toast); unit tests added. |

**RCA for the Codex-caught bugs:**

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| C1a | Siege hits attributed | Missing vanilla gate | The `OnScoreHit` signature was pinned character-for-character — but nobody asked what `isSiegeEngineHit` MEANS for the feature. A parameter can be verified and still unhandled. | Lesson appended to `lessons/adapters-taleworlds-api.md`: for every parameter of an overridden engine callback, state what each flag/edge means for the feature before shipping the override. |
| C2 | Overlap window recast | Logic error (latent invariant) | My own same-session invariant test summed only `CooldownReduction` mutations — one PROPERTY narrower than the mutation space (`Duration` mutations exist). The recurring "rule scoped one category narrower than the bug" motif, this time inside a freshly-written test. The Claude data-flow agent's reachability check had the identical gap. | Runtime gate replaces the data invariant entirely (structural, mutation-independent). The XML invariant test was REMOVED — extended to duration mutations it fails on shipped (valid) data; a gate that makes the state unrepresentable beats a test that constrains data. |

Suite after Codex fixes: **5509 green**.
