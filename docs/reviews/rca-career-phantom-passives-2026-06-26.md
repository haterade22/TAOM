# RCA — Career phantom-passive wiring (2026-06-26)

Root-cause analysis for the "career pip bonuses don't apply" investigation + fix. Covers the
original shipped defect and every bug the multi-agent adversarial review confirmed. Companion to
[docs/features/career-system.md](../features/career-system.md); CHANGELOG entry 2026-06-25.

## The original defect (user-reported)

Players reported that advancing career tiers and selecting pips delivered no bonus. Six
`PassiveEffectType` values — `HorseChargeDamage`, `HorseHealth`, `StealthBonus`, `TroopResistance`,
`Ammo`, `HealthRegeneration` — were authored into **~211 pips (~16% of all pip-passives)** but had
**zero runtime consumers**. `CareerPassiveService` cached their magnitudes and nothing ever read
them. Selecting the pip was a no-op.

**Root cause.** Content (the pip XML) and code (the consuming GameModels/services) evolved on
separate tracks with no cross-check that every authored `PassiveEffectType` had a consumer. The
2026-05-29 wrapper-schema fix (310 previously-unparsed `<PassiveEffects>` choices) *activated* more
of these pips into the cache — making the gap bigger, not smaller — without surfacing that the
newly-parsed types still went nowhere. A pre-existing feature-doc note even listed five of the six
as a "known limitation," so the gap was known but un-gated.

**Why it shipped silently.** Nothing failed. No exception, no warning, no test — an unconsumed
cache entry is indistinguishable from a hero who simply hasn't taken that pip. The only signal was
a player noticing their build felt weak.

## Review-confirmed bugs (introduced or adjacent to the fix)

The 6-dimension adversarial review (+ per-finding verification) confirmed 0 HIGH, 6 MED, 3 LOW. The
load-bearing ones:

| # | Bug | Root cause |
|---|-----|-----------|
| 1 | **TroopResistance under-applied on cavalry** | `GetVictimTroopLeaderHeroId` resolved the victim *identity* via `GetVictimAgent` (mount-aware → returns the rider for a horse hit) but the *party leader* from `info.VictimAgentOrigin` (mount-blind → the struck horse's `Origin`, which is **null** — battle mounts spawn via `CreateHorseAgentFromRosterElements` without an `Origin`). So on every horse-body hit of a non-hero cavalry troop, `troopLeaderHeroId` was null and the passive silently didn't apply. Fixed: `info.IsVictimAgentMount ? info.VictimRiderAgentOrigin : info.VictimAgentOrigin`. |
| 2 | **`gamemodels` rule-4 violation** | The new `TaomPartyHealingModel.GetDailyHealingHpForHeroes` mirrored the sibling `GetSurvivalChance`'s inline `IoC.Resolve<ICareerPassiveService>()` + branch instead of constructor-injecting. Propagated a pre-existing violation into net-new code. Fixed: ctor-injected the service, delegated to `ApplyFactor`. |
| 3 | **Re-tune left descriptions stale** | The magnitude-only data migration (`retune_phantom_passives.py`) changed values but not the player-facing `description=` text that embeds those values (e.g. "+5% stealth" now grants 10%; "+3 ammo" now multiplicative). Fixed (English) with a type-phrase-anchored description re-tune; 11-language re-translation deferred. |
| 4 | **Inline-in-model math untestable** | The `Ammo` (clamp/overflow) and `StealthBonus` (direction-inverting ratio) consumers put real arithmetic inline in a `MissionBehavior` / GameModel, unlike the sibling passives extracted into the testable service. Fixed: extracted to pure `CareerPassiveMath` + tests. |
| 5 | **Load-bearing fallback untested** | 5 shipped Resistance pips author `attack_type_mask="Blunt"/"Cut"`, unrepresentable in the `[Flags]` enum, relying on `ParseEnum`'s silent fallback to `All`. Pinned with a parser test + a `GetMaskedMagnitude` multi-bucket-sum test. |

## Why the review caught what implementation missed

- **#1 (mount-origin):** I verified `AttackInformation.VictimAgentOrigin` *exists* but did not trace
  that the struct stores the rider's origin in a *separate* field (`VictimRiderAgentOrigin`) for the
  mount case. Verifying a member exists is not the same as verifying it holds what you think for the
  branch you're in. The review decompiled the `AttackInformation` ctor and the horse-spawn path.
- **#2 (rule-4):** I treated the adjacent `GetSurvivalChance` as a *pattern to copy* rather than a
  *known violation to avoid*. Copying a sibling propagates its defects.
- **#3 (descriptions):** I scoped the re-tune to "change the number that's applied" and didn't model
  that the same number is *also displayed* from a second source (the description string).

## Lessons (appended to LESSONS-LEARNED.md)

1. **Authored content referencing a code-side enum needs a consumer gate** — an enum value used in
   data with no runtime consumer ships as a silent no-op. (Data, Content & Cultures)
2. **A mount-aware consumer must read the mount-aware origin** — `AttackInformation` splits
   `VictimAgentOrigin` (mount) from `VictimRiderAgentOrigin` (rider); resolve both identity and
   origin through the same mount branch. (Adapters & TaleWorlds API)
3. **Don't mirror an existing standards violation into net-new code** — copying a sibling method
   copies its rule-4 / service-locator defects. (GameModels & Services)
4. **A data re-tune that changes a displayed value must update the displayed text too** — or the
   text must not embed the value. (Data, Content & Cultures)

## Codex adversarial pass (gpt-5.5, xhigh) — CLEAN

Run 2026-06-26 after the deep-review fixes. **Verdict CLEAN: 0/0/0/0, no findings.** All 8 Known
Suspects closed — 7 DISPUTED (code correct) + 1 PARTIAL. Codex independently decompiled the
`AttackInformation` ctor (confirming the `VictimRiderAgentOrigin` mount-origin fix), the visibility
consumer (`PartyBase.cs:1078-1082`, `num <= ratio` → lower ratio = harder to spot), and the masked
cache, and self-disputed the only two candidate false-positives (the 18 skipped keystone descriptions
+ the residual `operation`/`is_percentage` XML attributes — both intentional). Output:
[codex-adversarial-career-phantom-passives-2026-06-26.md](codex-adversarial-career-phantom-passives-2026-06-26.md).

**Documented design consequence (suspect 1 PARTIAL, verified independently here).** The engine damage
pipeline is `ApplyDamageAmplifications(1) → ApplyDamageScaling(2) → ApplyDamageReductions(3) →
ApplyGeneralDamageModifiers(4)`. The old `Damage` passive rode the flat `DamageMultiplierBonus` read in
stage 4 (after armor); the masked `Damage` now applies in stage 1 (before armor, alongside
`ArmorPenetration`). Net: a `Damage` pip is now slightly less effective against heavily-armored targets
(armor mitigates the amplified base). This is **not a defect** — honoring `attack_type_mask` *requires*
the per-hit path, stage 1 is the engine's attacker-damage hook, and "a damage buff mitigated by armor
like all damage" is more consistent than the old armor-bypassing final multiplier. No fix.

## Process gap (this RCA's own meta-finding)

The mandatory completion workflow (`/verify` → `/deep-review` → fix → `/review-codex` → RCA) was
initially run only partially: a deep-review-equivalent multi-agent **Workflow** ran in place of the
`/deep-review` skill — functionally a superset, but it bypassed the skill's read-before/append-after
LESSONS-LEARNED hook (so the lessons above were not codified until the RCA), and the independent Codex
pass + the RCA were both deferred until the user asked "did you do a deep review, codex review and
rca?". All three are now complete (Codex CLEAN, above). Lesson: a custom Workflow can substitute for a
review skill's *agents* but not for its *side effects* (LESSONS-LEARNED append, REVIEW-LOG entry, the
Codex hand-off) — when substituting, run those steps explicitly, or just invoke the skill.
