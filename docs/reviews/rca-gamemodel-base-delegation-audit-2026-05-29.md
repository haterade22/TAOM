# RCA — GameModel base-delegation audit: dropped buyer-hero recruitment perks

**Date:** 2026-05-29
**Trigger:** Open-web research session → self-directed audit of all 37 `Taom*Model : Default*Model` overrides for "decorator/base-delegation" gaps (an override that fully replaces a vanilla method and silently drops vanilla behavior it didn't intend to remove).
**Outcome:** 85 overrides OK, 6 flagged RISK. On verification against the **installed v1.4.5 decompile (ilspycmd)** — not the web, not the `E:\Decompiled_Bannerlord` dump — **5 were false positives** and **1 was a genuine MED**, now fixed under TDD + a clean 5-agent `/deep-review`.

---

## Top-line summary

The audit's value was almost entirely in its *verification* phase, not its *flagging* phase. The flagging agents speculated about vanilla safety-gates that do not exist in the actual engine code (5 of 6 flags). Only one flag survived contact with the decompile: `TaomPartyWageModel.GetTroopRecruitmentCost` fully replaces vanilla's cost computation and never re-applies the `if (buyerHero != null)` personal skill-perk discount block — so a player's HeadHunter / ChinkInTheArmor / ShowOfStrength / HardyFrontline / RenownedArcher / Piercer / Frugal / SwordForBarter / SlickNegotiator perks silently did nothing toward recruitment cost.

This is the textbook **full-replacement GameModel** failure mode: replacing a vanilla method's *headline* behavior (here: the extended T0–T10 cost table + LOTR cultural feats) while inadvertently discarding an *orthogonal* sub-behavior buried in the same method (here: per-hero weapon-skill perk discounts).

---

## Findings table

| # | Sev | Finding | Category | Verdict | Why missed (originally) |
|---|-----|---------|----------|---------|--------------------------|
| 1 | **MED** | `TaomPartyWageModel.GetTroopRecruitmentCost` drops vanilla `buyerHero` perk discounts (9 perks) | Full-replacement drops orthogonal sub-behavior | **CONFIRMED → FIXED** | The override was written to *extend the cost table + add cultural feats*; the author replaced the whole method and never noticed the separate `if (buyerHero != null)` perk block in the vanilla body. The earlier (#173/#148) refactor that extracted the service preserved the *existing* TAOM behavior faithfully — but the bug predated the extraction, so the refactor carried it forward unflagged. |
| 2 | LOW→none | `TaomPregnancyModel.GetDailyChanceOfPregnancyForHero` drops the Virile (Charm) perk factor | (speculated) | **FALSE POSITIVE** | TAOM already applies Virile identically to vanilla (checks both `hero` and `hero.Spouse`, via `PrimaryBonus`) — `TaomPregnancyModel.cs:54-57`. The flag was speculation; ilspy confirmed parity. |
| 3 | (RISK)→none | `TaomKingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms` drops peace-status / cooldown gates | (speculated) | **FALSE POSITIVE** | Vanilla `DefaultKingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms` is literally `{ reason = null; return true; }`. There are no gates to drop. |
| 4 | (RISK)→none | `TaomMilitaryPowerModel` drops vanilla troop-power formula | (speculated) | **FALSE POSITIVE** | TAOM delegates to `base.GetDefaultTroopPower` when its MCM toggle is off; the override only diverges for the configured T7–T10 band, by design. |
| 5 | (RISK)→none | `TaomSiegeEventModel` drops vanilla defender-engine perk gating | (speculated) | **FALSE POSITIVE** | TAOM replicates vanilla's Ballista/Catapult + Fire-variant perk gating and *adds* Trebuchet — the perk gates are preserved, not dropped. |
| 6 | (RISK)→none | `TaomPartyWageModel.GetCharacterWage` drops vanilla wage logic | (speculated) | **FALSE POSITIVE** | The vanilla method is a plain tier switch (0→1…6→17, _→23, ×1.5 mercenary); TAOM intentionally replaces it with an extended T0–T10 table. Intentional design, documented in CHANGELOG (#180 / 6279). |

---

## The fix (finding #1)

Restored the vanilla buyer-hero perk discounts **without** re-introducing vanilla's cost table (which TAOM intentionally replaces) and **without** re-adding `KhuzaitRecruitUpgradeFeat` (TAOM intentionally replaces the mounted-recruit cultural feat with Isengard/Rohan mounted-cost feats):

- **New primitive struct** `RecruitmentPerkInputs` (in `IWageModifierService.cs`) — pre-resolved perk bonus floats + troop/hero facts, mirroring the existing `WageFeatInputs` / `MountedCostFeatInputs` pattern. Keeps the service free of TaleWorlds sealed types (ADR-007).
- **Boundary** `TaomPartyWageModel.ResolveBuyerRecruitmentPerks` resolves each `buyerHero.GetPerkValue(perk) ? perk.{Secondary|Primary}Bonus : 0f` from the sealed `Hero`/`CharacterObject` (allowed — it's the entry point).
- **Service** `WageModifierService.SumBuyerPerkFactors` owns the troop-type / tier / leader / mercenary gating (unit-testable) and applies the summed factor + vanilla's `LimitMin(1f)`.
- **Numeric equivalence:** verified via the `ExplainedNumber` decompile that `AddFactor` accumulates linearly (`SumOfFactors += value`), so summing the active perk factors and applying once is identical to vanilla's sequential `AddFactor` calls. With `includeDescriptions:false` there are no per-perk display lines in either path.

**Tests:** 14 new gate tests (skip-guard exhaustion — one per perk gate + the no-buyer / no-active-perk / deep-discount-clamp / compose-with-mounted-feats cases). Full suite: 2650 passed, 0 failed. `/deep-review` (5 agents): all PASS, zero parity drift (Agent 2 verified all 9 perks + Primary/Secondary selection + else-if structure against the installed v1.4.5 DLL).

---

## Root-cause pattern: full-replacement GameModel overrides silently drop orthogonal sub-behavior

TAOM has 37 `Taom*Model : Default*Model` overrides. Many **fully replace** a vanilla method body rather than decorating it (call `base`, then adjust). Full replacement is sometimes necessary (e.g. TAOM's extended cost table can't be expressed as a factor on the vanilla table). But every full replacement carries the risk that the vanilla method body contains a *second, unrelated* sub-behavior the author isn't thinking about — and replacing the method drops it with no compile error, no test failure, and no runtime warning.

Here, the vanilla `GetTroopRecruitmentCost` does **two** independent things: (a) compute a level/horse/mercenary cost (the part TAOM meant to extend), and (b) apply the buyer hero's weapon-skill perk discounts (the part TAOM didn't notice). The fix is to decompose: replace (a), preserve (b).

**Generalizable rule (added to `gamemodels.md` candidate):** *Before fully replacing a vanilla GameModel method, read the entire vanilla method body and enumerate every distinct sub-behavior — especially `if (hero != null)` / `HasPerk` / `HasFeat` / `LimitMin/Max` blocks. For each, decide explicitly: replace, preserve, or intentionally drop (with a documented reason). A full replacement that doesn't enumerate is the bug.*

---

## Why each deep-review agent originally let #1 ship (it predates this session)

The fix's own `/deep-review` was clean — but #1 had shipped in earlier work. Why prior reviews missed it:

- **Standards / rule-4 agents** only check override-body *purity* (no inline branching), not *behavioral completeness* vs vanilla. A perfectly thin override that drops behavior passes every standards check.
- **Compatibility agent** verifies the API *exists*, not that all of the vanilla method's *effects* are reproduced.
- **Data-flow agent** traces declared data to consumers *within the changeset* — it can't flag a vanilla sub-behavior that was never declared in TAOM at all.
- **The gap:** none of the 5 standing agents compares the TAOM override against the **full vanilla method body** to find dropped sub-behaviors. That comparison is exactly what this audit (belatedly) performed.

---

## Why the audit's flagging phase was 5/6 wrong

The flagging agents **speculated** about vanilla gates instead of reading the decompile. Every false positive (#2–#6) was an imagined vanilla safety-gate. This is the failure mode `evidence-over-claims.md` A.4 was written for: *a subagent's claim about vanilla behavior is a hypothesis until verified against the decompile.* The audit only became trustworthy after the per-flag ilspy verification pass — which the user explicitly mandated ("check using ilspy instead of relying on anything on the web"). **Lesson reinforced, already codified** — no new rule needed, but this is a second real instance to cite.

---

## Preventive actions

| Action | Status |
|--------|--------|
| Fix #1 via TDD + clean `/deep-review` | ✅ Done this session |
| Record #2 (Pregnancy Virile) as a verified non-bug so future audits don't re-flag | ✅ This RCA |
| Generalizable rule: "enumerate every sub-behavior before fully replacing a vanilla GameModel method" | Candidate for `gamemodels.md` — propose in a follow-up (touches `.claude/`, batched with other community-config edits per the user's commit-separation rule) |
| Reinforce `evidence-over-claims.md` A.4 (subagent vanilla claims → verify against decompile) | ✅ Already codified; this is a second citation |

No new feedback-memory entry is manufactured for the false-positives (they're a re-instance of an already-codified rule). The one durable new lesson — *enumerate-before-replace* — is captured here and proposed for `gamemodels.md`.
