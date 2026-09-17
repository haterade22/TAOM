# RCA: creature mount retune deep review (#615), 2026-09-17

## Top-line

A six-agent `/deep-review` of a data-only change: the ten creature mounts in the live Armory's
`LOTRAOM_horses.xml` (and the `lotraom-assets` v1.5 mirror) got new `speed`, `maneuver` and
`charge_damage` values, chosen by Mike from the mount ledger pulled the same morning. The repo
commit `0f57f29f` carries only the CHANGELOG entry and two feature-doc stat lines. Mike asked for
the review explicitly: "ensure you do a deep review, even for the xml changes".

No defect in the change. The integrity agent diffed the file byte for byte (ten `<Horse>` blocks,
+5 bytes, all from one-digit values becoming two-digit, BOM and endings intact, 54 items parse in
both copies, no duplicate id anywhere in the Armory tree). The engine agent verified on the
installed v1.5.3 DLLs that every field is an unclamped `int`, that `Difficulty` (the riding
requirement) is a separate attribute nobody touched, and that no Monster field visibly caps
`MountSpeed` in managed code (the native side stays UNVERIFIED). The tooling agent confirmed the
one-off script preserved the BOM, the LF endings and "Rhûn", is idempotent, and writes nothing
on a failure path. Standards, completeness and data flow returned clean for the diff itself.

What the review did surface is the reason a data retune gets a review at all: the consumers.
Four items, none introduced by #615, two of them worth acting on.

## Findings

| # | Sev | Finding | Category | Why missed | Disposition |
|---|---|---|---|---|---|
| 1 | balance note | Charge blow magnitude is quadratic in closing speed and linear in `charge_damage` (`MissionCombatMechanicsHelper.cs:711`, `baseMagnitude = (v x dot)^2 x dot x MountChargeDamage`; `MountChargeDamage = charge x 0.004`, `SandboxAgentStatCalculateModel.cs:1280`). Raising both on the same item compounds: a brown warg at the same angle lands about 3.2x its old magnitude (11.0^2 x 0.024 to 12.5^2 x 0.060), the chariot about 1.4x. #610's culture multiplier sits on top. | engine formula | The plan priced the change as "speed up, charge up" and never opened the charge formula. Not a defect: it is the direction Mike asked for. | Recorded here and in the CHANGELOG; the Custom Battle smoke is where the feel is judged. |
| 2 | MED, pre-existing | The career cavalry ability applies its `MountSpeedBonus` / `ChargeDamageBonus` twice to one mount when the rider is both self-buffed and inside another caster's radius: `ApplyMountStatModifiers` calls `ApplyMountBuff` for `GetBuff(riderHeroId)` and again for `GetAllyBuff(riderAgentIndex)` with no clamp (`CareerAgentStatService.cs:113-128, 287-294`, HEAD). 1.2 x 1.2 = 1.44x on speed and on charge. The higher creature bases raise the reachable ceiling. | stacking, #611 design | #611 shipped the mount hop on 2026-09-17 with a self and an ally path by design; whether self plus ally should stack was never stated. Out of #615's diff. | Reported to Mike; needs a design call (cap, or self-only), then its own issue. Not a #615 blocker. |
| 3 | LOW, pre-existing | `docs/features/warg-combat.md` `WargConfig` table disagreed with `WargConfig.cs`: `SpeedForMaxDamage` 8.0f vs 20f, `DamageToFall` 20 vs 40, `maxDistanceFromWargToRollForRage` 10.0f vs 20, and `TargetDetectionRange` 20f missing. | doc drift | The constants moved in code without the table following; `lint_docs.py` checks links and versions, not numbers. | Fixed in the review commit. |
| 4 | balance note | The spider's speed-damage term (`SpiderConfig.SpeedForMaxDamage = 15f`) now sits at its cap: the spider runs 14.7 m/s baseline. The warg's cap is 20f, so the warg term rises proportionally and does not saturate (an agent claimed it did; refuted by reading `WargAttackService.cs:29`). | velocity thresholds in C# config | The plan said "no C# moves", which was true, and stopped there; it did not list the C# thresholds that read the mount's velocity. | Recorded; the lesson below names the grep. |

Also noted, not a finding: the mirror carries the retune in its working tree only (`M` on
`LOTRAOM_horses.xml`, everything else there is untracked `*.bak-meshladder-609`), so a mirror
commit is a clean one-file commit whenever Mike wants it. The chariot's `family_type="4"` on
its `<Horse>` element is dead XML: `HorseComponent.Deserialize` never reads it, the harness fit
runs off `Monster.FamilyType` (`Monster.cs:491`), which the chariot monster also sets. Predates
this change; harmless.

## Root-cause pattern

One theme across findings 1 and 4: **a mount item's `speed` has consumers outside the item**.
The engine squares closing speed into every charge blow, and TAOM's creature attack services
read the mount's live velocity against per-creature thresholds (`SpeedForMaxDamage`,
`ChargeVelocityThreshold`). None of them needed a code change here, and none of them was in the
plan either. The retune was correct; the plan's picture of its blast radius was one layer short.

## Why each agent missed these

Nothing in the diff was missed; the four items above are consumers and pre-existing drift, and
the data-flow and engine agents are the ones that found them. The one agent error (the warg
saturation claim) was caught by re-reading the line it cited before relaying, which is the
`evidence-over-claims` §A.4 step working as intended.

## Feedback memories to codify

One lesson, appended to `docs/reviews/lessons/data-content-cultures.md`: before retuning a
mount item's `speed` or `charge_damage`, open the charge formula and grep the creature's
`*Config.cs` for velocity thresholds, and state the compounded effect in the plan. No new rule
file; the existing review already asks the question, the plan did not.
