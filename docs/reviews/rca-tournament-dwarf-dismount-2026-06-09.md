# RCA — Dwarf tournament-cavalry "inside the horse" (Patch46, 2026-06-09)

## Top-line summary

Dwarf tournament participants were sometimes spawned mounted and rendered **inside the horse mesh** — the dwarf's custom (shorter) skeleton has a misaligned rider bone (the same defect [`EyeHeightAdjustmentHook`](../../Main/Features/HeroRace/EyeHeightAdjustmentHook.cs) works around for dwarf-on-mount eye height).

Root cause is a **data-flow misattribution risk**: the override that *looks* responsible for tournament equipment — `TaomTournamentModel.GetParticipantArmor` — does not control the mount. In `SandBox.Tournaments.MissionLogics.TournamentFightMissionController`, the horse is cloned into `participant.MatchEquipment` from the settlement culture's tournament **weapon template** (`CultureObject.TournamentTeamTemplatesFor{One,Two,Four}Participant`, or the `tournament_template_empire_*` fallback) in `PrepareForMatch`. `AddRandomClothes` — which calls `GetParticipantArmor` — only copies armor slots 5–9 on top. So no edit to `GetParticipantArmor` (or to the `gear_practice_dummy_*` NPCs) could ever remove a horse.

Fix: a Harmony postfix on the public `PrepareForMatch` (`Patch46_TournamentDwarfDismount`) — the single chokepoint feeding both the visual spawn (`SpawnAgentWithRandomItems`) and the AI `Simulate` path — clears `EquipmentIndex.Horse` + `HorseHarness` for any participant whose race `ITournamentService.ShouldDismountInTournament` returns true (currently dwarves). Keyed on race, not culture, so a dwarf in any town — plus the player, if a dwarf — is dismounted.

## The originating bug

| # | Sev | Bug | Category | Why it existed |
|---|-----|-----|----------|----------------|
| 0 | MED (visual, not crashing) | Dwarf tournament participants spawned mounted clip inside the horse mesh | Custom-skeleton-on-mount + data-flow misattribution | Vanilla tournament weapon templates include mounted loadouts; assigned positionally to participants in `PrepareForMatch` regardless of race. TAOM had a culture-aware armor override but no race gate on the mount, because the mount is sourced from a *different* vanilla method than the armor. |

### Root-cause pattern: the obviously-named override isn't the one that assembles the final value

A tournament participant's spawned `Equipment` is assembled by **two** vanilla methods on `TournamentFightMissionController`:

| Method | Slots it sets | TAOM hook before this fix |
|--------|---------------|---------------------------|
| `PrepareForMatch` → `GetTeamWeaponEquipmentList` | weapons (0–4) **+ Horse (10) + HorseHarness (11)** | none |
| `AddRandomClothes` → `Campaign.Models.TournamentModel.GetParticipantArmor` | armor/clothing (5–9 only) | `TaomTournamentModel.GetParticipantArmor` override |

The override named "…ParticipantArmor" naturally *looks* like the place that controls a participant's gear, but it provably cannot touch slots 10/11 (`AddRandomClothes` loops `for i = 5; i < 10`). The mount lives on the weapon-template path, which had no TAOM hook.

**Generalisation:** when fixing a bug about a *spawned/assembled* value (equipment, stats, visuals), enumerate **every** producer that writes into the final object and confirm which slot/field each one owns — do not assume the method whose name matches the concept is the one that sets the offending field. (Sibling lessons: [`feedback_weighted_getter_in_derived_family`](../../../.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_weighted_getter_in_derived_family.md) — a derived value assembled from multiple operands; [`feedback_review_blindspots`](../../../.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_review_blindspots.md) — review files in isolation, miss the data flow.)

This pattern showed up live during exploration: the first Explore agent reviewing **TAOM's** Arena code correctly concluded "no `gear_practice_dummy_*` has a Horse slot — participants are infantry-only," which is true for the armor path and would have led to "we don't even give dwarves horses → there's no bug here." A second agent researching the **vanilla** controller found the real mount source (`PrepareForMatch` weapon template). The fix only became correct after reconciling the two and reading the exact slot ranges of `AddRandomClothes` vs the cloned template. A review scoped to TAOM source alone would have missed it.

## Fix-cycle findings (`/deep-review`, 5 agents)

| Agent | Result |
|-------|--------|
| Standards (ADR-002/003/004/005/007, IoC, category registration) | PASS — service takes a primitive `int`; patch delegates the decision; constructor injection of `IRaceManager`; `[HarmonyPatchCategory]` string matches `SubModule.cs`. |
| Compatibility (vs **installed** v1.4.5 DLLs) | **WRONG on the highest-risk item — caused a post-ship crash (see below).** It verified the field is exactly `_match` (correct) but then asserted `___match` injection was "live (not null)" — false. Harmony strips **three** underscores, so `___match` resolves to a field named `match` (nonexistent) → patch fails to apply → crash on load. Correct is `____match` (four). The other items were right: `EquipmentElement.Invalid = new EquipmentElement(null)`, `EquipmentIndex.Horse=10`/`HorseHarness=11`, `AddEquipmentToSlotWithoutAgent` public. |
| Efficiency | PASS — `PrepareForMatch` runs once per match; lazy `??=` IoC cache; no allocations/LINQ in the loop. |
| Completeness | The 3 doc/issue gaps it flagged (feature doc, GitHub issue, CLAUDE.md patch row) are **closed by this documentation pass**. Tests + IoC complete. |
| Data Flow | PASS — 6 flows, 0 gaps. The two highest-value traces both confirmed: (1) `PrepareForMatch` is the complete chokepoint, `AddRandomClothes` touches only 5–9, nothing re-adds a horse after the postfix; (3) `EquipmentElement.Invalid.Item == null` and `Mission.SpawnAgent` guards mount creation on `Item != null`, so clearing the slot produces no mount. |

### The one finding (LOW) — declined with reasoning

| # | Sev | Finding | Disposition |
|---|-----|---------|-------------|
| 1 | LOW | Static `_service` cache in `Patch46_TournamentDwarfDismount` has no `ResetForUnload()` (unlike `CrashReportPatchHelper`). | **DECLINED (recorded, not silent).** The patch mirrors [`Patch40_HideoutDescription`](../../Main/Features/BanditManagement/Hooks/Patch40_HideoutDescription.cs) exactly — identical lazy `_service ??= IoC.Resolve<>()` with no reset. `TournamentService` is a pure, stateless singleton with no disposable deps; `GetService()` is null-guarded; the only manifestation needs reload-in-same-process *and* re-patch, and the stale instance would still resolve identical logic. Per the simplicity criterion, adding reset plumbing for that edge case (which the sibling pattern doesn't handle either) isn't warranted. Revisit if `TournamentService` ever gains disposable dependencies. |

## POST-SHIP CRASH (2026-06-09 18:00) — Harmony field-injection underscore miscount

After commit `ef0c326` was pushed and deployed, the game **crashed on every campaign load**:

```
HarmonyLib.HarmonyException: Patching exception in method ...TournamentFightMissionController::PrepareForMatch()
  Inner: System.ArgumentException: No such field defined in class ...TournamentFightMissionController
         Parameter name: match
  at HarmonyLib.MethodCreatorTools.EmitCallParameter
  at TAOM.SubModule.OnGameInitializationFinished ... SubModule.cs:line 542 (PatchCategory)
```

| # | Sev | Bug | Category | Root cause |
|---|-----|-----|----------|------------|
| 2 | **CRITICAL (hard crash on load)** | Postfix parameter `___match` (three underscores) made Harmony look for a field named `match`; the field is `_match` → patch failed to apply → `HarmonyException` propagated uncaught out of `PatchCategory` → crash | Harmony field-injection convention | Harmony strips a **three-underscore** (`___`) prefix and uses the remainder as the field name. The target field's own name begins with an underscore (`_match`), so the parameter must be `____match` (**four** underscores = `___` + `_match`). `___match` strips to `match` (nonexistent). |

**Fix:** `___match` → `____match` in `Patch46_TournamentDwarfDismount.Postfix`. Proven by the crash's own message — Harmony reported `Parameter name: match`, i.e. it stripped exactly three underscores; `____match` strips to `_match`, the real field.

### Why this shipped — and why review didn't catch it

- **The deep-review Compatibility agent gave a confidently WRONG verdict on the exact highest-risk item.** It correctly decompiled the field name (`_match`) but then miscalculated the prefix ("`__` Harmony prefix + `_match`" — the prefix is `___`, not `__`) and blessed `___match`. I relied on that verdict instead of counting underscores myself or testing patch application. This is a live instance of `evidence-over-claims.md`: *"a confident subagent report is a claim, not evidence"* — and it landed on the one item the whole patch hinged on.
- **Unit tests structurally cannot catch it.** Harmony patches are not applied in the MSTest host; the 28 green Arena tests exercised `ShouldDismountInTournament` (pure logic) but never the patch wiring. A Harmony patch's *only* real verification is **applying it** — in-game, or via a patch-application smoke test.
- **The plan doc and original patch both had `___match`** from the start, so there was no point where a correct value was degraded — the wrong value was never independently checked against the three-underscore rule.

### Preventive actions (crash)

1. **Memory:** [`feedback_harmony_private_field_injection_underscore_count`](../../../.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_harmony_private_field_injection_underscore_count.md) — Harmony private-field injection is `___` (three) + the field's literal name; a field named `_x` needs `____x` (four). Count from the field name, not by eyeball. The crash message `Parameter name: <stripped>` tells you exactly how many it stripped.
2. **Process:** a Harmony patch is "verified" only when it has been **applied** (in-game load, or a dedicated patch-application test) — never on signature-decompile alone. Treat a subagent's "injection is live" as a claim to test, not a fact. (Reinforces `evidence-over-claims.md`.)
3. **Worth a follow-up grep:** audit every TAOM Harmony patch that injects a private field whose name starts with `_` for the same off-by-one underscore. (No other field-injection patch was changed in this feature, but the trap is general.)
4. **Defensive consideration (not done):** `OnGameInitializationFinished` calls `PatchCategory` with no try/catch, so one bad patch crashes the whole load. `PatchShield` Finalizers guard patch *bodies*, not patch *application*. Wrapping each `PatchCategory` (or the block) so a single failed application logs + continues would downgrade this class of bug from crash to disabled-feature. Left as a separate `SubModule.cs` decision (single-owner file; silently swallowing application failures has its own risks).

## Why each deep-review agent's scope behaved as it did

- **Standards / Efficiency** — correctly scoped to the changed files; nothing to add.
- **Compatibility** — the load-bearing risk here was a private-field name (`_match`) and a struct-clearing semantic (`EquipmentElement.Invalid`), both verified against installed DLLs. This is exactly the agent's remit and it nailed it.
- **Completeness** — surfaced the doc/issue debt, which this pass discharges. Note it also caught that `arena.md` + `tournament-armor-assignment.md` were stale (pre-#137) — fixed here.
- **Data Flow** — the decisive agent. It is the one that would have caught the *originating* bug had the feature been reviewed before shipping the original tournament model: the "which method owns slot 10" question is a data-flow trace, not a per-file check.

## Verification timeline

- **Build + tests (after the game was closed):** GREEN — `dotnet test TAOM.Tests` = 3109 passed / 2 skipped, the 28 Arena tests all pass. (7 failures in `VolunteerRecruitmentServiceTests.GetVolunteerTroopId_DolGuldur*` are pre-existing **in-flight spider + DolGuldur** work in the uncommitted tree, not this change.)
- **Committed + pushed:** `ef0c326` on `bannerlord-1.4.5`, then deployed.
- **In-game load → CRASHED** (the `____match` underscore bug above). Root-caused from the crash report + fixed in a follow-up hotfix; `TAOM.dll` rebuilt and redeployed. **The patch now needs an in-game load to confirm it no longer crashes**, then the original dismount check (dwarf-on-foot in an Erebor town + a human-culture/empire-fallback town; non-dwarf still mounted).
- **Still NOT run:** `/review-codex` adversarial pass (Phase 2/3 of the completion workflow).

Lesson reinforced: the build/tests passing said nothing about the patch *applying* — only loading the game did. Issue #277 was closed at the user's explicit instruction with status recorded; the post-ship crash + hotfix is captured here.

## Preventive action

- **Memory:** [`feedback_tournament_horse_from_weapon_template_not_armor`](../../../.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_tournament_horse_from_weapon_template_not_armor.md) — the tournament mount is sourced from the culture weapon template via `PrepareForMatch`, not from `GetParticipantArmor`; to gate it, postfix `PrepareForMatch` and clear slots 10/11. Generalises the "enumerate every producer of an assembled value" lesson. Linked to [[nonhumanoid-creature-troop-not-mount]] (the broad custom-skeleton-can't-be-mounted principle) and [[dwarf-race-npc-needs-dwarf-skeleton-armor]] (sibling custom-skeleton equipment trap).
- **No new rule file** — the existing data-flow review remit already covers "trace every producer"; this is a fresh instance, not a gap in the rules.

### Follow-up 2026-08-04 — the data-layer half

Patch46 gates the mount at *runtime*, in *one* mission type. Nothing stopped a dwarf being **authored**
as cavalry in the first place, so the same "inside the horse" render was one troop revamp or copied
roster away from returning outside the arena. `validate_moduledata.py` now carries `MOUNTED_DWARF`,
which rejects a `race="dwarf"` character tagged `Cavalry`/`HorseArcher` *or* able to reach a
`slot="Horse"` item. Audit at introduction: clean — all 185 dwarf characters were already Infantry or
Ranged, so the check pins an invariant that held rather than fixing a live defect.

The decompile behind it corrects an assumption worth stating plainly, because it is the opposite of the
intuitive one: **`default_group` does not control a lord's battlefield formation.**
`CharacterObject.GetFormationClass()` overrides the base and, when `IsHero`, ignores
`DefaultFormationClass` entirely — it reads live `BattleEquipment`. That is *why* Patch46 clears
equipment slots 10/11 rather than rewriting an attribute, and why the data check had to gate the mount
and not just the enum. Full process trace:
[`formations-and-team-ai.md`](../reference/engine/formations-and-team-ai.md) "Which formation a spawned
agent joins"; gate: [`moduledata-validation.md`](../features/moduledata-validation.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](../features/arena.md)

<!-- backlinks-end -->
