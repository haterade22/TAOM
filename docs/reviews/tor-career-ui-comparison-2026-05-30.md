# TOR_Core Career UI — reference review & comparison (2026-05-30)

Reference review of [TheOldRealms/TOR_Core](https://github.com/TheOldRealms/TOR_Core) (the Warhammer "Old Realms" total-conversion) career-screen UI, compared against TAOM's career screen after the 2026-05-30 revamp. Read-only review of a blobless shallow clone (since deleted); no TOR build/scripts were run.

## License / attribution

TOR_Core is **GPL v3**. TAOM's career system shares TOR's exact file structure (`CareerScreenVM.cs`, `CareerChoiceGroupObjectVM.cs`, `CareerChoiceObjectVM.cs`, `GUI/Prefabs/CareerSystem/CareerScreen.xml`) — it was originally **ported from TOR**. The TOR team granted TAOM explicit permission to reuse their code, and the repo is public. **Attribution:** TAOM's CareerSystem UI derives from TOR_Core (GPLv3), used with permission. The per-tier rank-title convention (`tor_career_rank{1,2,3}_name`) was adopted here; TAOM's names are authored fresh for Middle-earth.

## How TOR builds the Career UI (verified from source)

- **Screen stack:** `CareerScreenGameState → CareerScreen (ScreenBase) → CareerScreenVM → CareerObjectVM (CurrentCareer) → 3 tier lists → CareerChoiceGroupObjectVM → CareerChoiceObjectVM`. `CareerScreenVM` is thin; all screen logic lives in the `CareerObjectVM` "CurrentCareer" sub-layer.
- **Prefab layout:** split pane — left 500px (name, illustration, description, ability icon + effect lines), right 1420px (three tier rows). Tier rows stack `VerticalBottomToTop`, so **Tier 1 = bottom, Tier 3 = top**. Per-tier `VerticalAlignment` Bottom/Center/Top.
- **Each node:** `ExtendablePanel` 80px → 750px on hover; vertical pip strip (`@IconSprite` tinted brown `#7f695c` if `IsFreeToTake`, gold `#dfc395` if `IsTaken`) + per-choice descriptions clipped at 80px and revealed when the panel expands on hover; `+`/`−` buttons gated on `@ButtonsVisible` (hover) and `@IsActive`.
- **Naming (3 layers):** per-tier **rank name** (`tor_career_rank1/2/3_name` per career — e.g. *Knight Errant / Questing Knight / Grail Knight*) + **condition** + **unlock** text per tier (`GetConditionText`/`GetUnlockText` → "Required renown: N"); plus a per-node **GroupName** (*Monster Slayer*, *Master Horseman*…).
- **Gating:** `group.IsActiveForHero(hero)` — condition-based (renown etc.); a `locked_chains` sprite overlays locked tiers (`@TierNActive`).
- **Free points:** `Min(MaxPerkPoints, Hero.Level) − (CareerChoices.Count − 1)`.
- **Pip availability:** `CareerChoiceObjectVM.IsFreeToTake = !IsTaken` — **not** gated on free points (every untaken slot shows a brown pip). VMs read `Hero.MainHero` directly.

## Side-by-side

| Aspect | TOR | TAOM (post-revamp) |
|---|---|---|
| Tier order | T1 bottom → T3 top (`VerticalBottomToTop`) | T1 bottom → T3 top (reordered blocks) — same result |
| Screen VM | thin → `CareerObjectVM` sub-layer | flattened into `CareerScreenVM` |
| Locked-tier visual | `locked_chains` overlay | **"Requires Level N"** label |
| Tier header naming | per-career **rank name** + condition + unlock | per-career **rank name** (adopted) + "Requires Level N" |
| Per-node naming | GroupName (Warhammer) | GroupName — **294 web-researched LOTR lore names** |
| Gating basis | renown/condition (`IsActiveForHero`) | level (T1 / 10 / 20) |
| Pip states | 2 (gold taken / brown untaken) → **blank at 0 pts** | **3** (gold / brown affordable / **dim unavailable**) |
| Coupling | reads `Hero.MainHero` in VMs | service + adapter, unit-tested (ADR-007) |

## Where TAOM is ahead of TOR

1. **No blank-node bug.** TOR's pips vanish when `FreeCareerPoints = 0` (only looked fine in screenshots that had spare points). TAOM's `IsUnavailable` dim-pip keeps the strip always readable.
2. **Decoupled + tested.** TAOM's registry/service/adapter split is unit-tested; TOR's VMs hardwire `Hero.MainHero`.
3. **Cleaner locked state.** "Requires Level N" label vs. a chains overlay.
4. **Lore-name depth.** 294 sourced group names + 147 sourced rank titles, each with an `attested` flag.

## Adopted this pass (clean-room, LOTR-authored)

- **Per-tier rank names** — `CareerDefinition.Rank{1,2,3}Name` + `rank{1,2,3}_name` XML attrs + VM tier-header binding (fallback to "Tier N"). 147 web-researched Tolkien-grounded titles applied to all 49 careers (`tools/career_rank_names.json`, `tools/apply_career_rank_names.py`). This is the feature that gives the reference its readable "Knight Errant → Grail Knight" progression.

## Deliberately NOT adopted

- TOR's `CareerObjectVM` sub-layer (ours is flatter, tested, equivalent UX) — needless churn.
- Renown/condition gating + chains overlay — TAOM uses level gating + "Requires Level N" text by design.
- Battle-Prayers button — Warhammer-specific.
- TOR's 2-state pip + `Hero.MainHero` coupling — TAOM's 3-state + service split is strictly better.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/career-system.md](../features/career-system.md)

<!-- backlinks-end -->
