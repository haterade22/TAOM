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
| Compatibility (vs **installed** v1.4.5 DLLs) | PASS — 10/10 APIs verified. Highest-risk item: the private field is exactly `_match`, so the `___match` injection is live (not null). `EquipmentElement.Invalid = new EquipmentElement(null)`; `EquipmentIndex.Horse=10`/`HorseHarness=11`; `AddEquipmentToSlotWithoutAgent(EquipmentIndex, EquipmentElement)` confirmed public. |
| Efficiency | PASS — `PrepareForMatch` runs once per match; lazy `??=` IoC cache; no allocations/LINQ in the loop. |
| Completeness | The 3 doc/issue gaps it flagged (feature doc, GitHub issue, CLAUDE.md patch row) are **closed by this documentation pass**. Tests + IoC complete. |
| Data Flow | PASS — 6 flows, 0 gaps. The two highest-value traces both confirmed: (1) `PrepareForMatch` is the complete chokepoint, `AddRandomClothes` touches only 5–9, nothing re-adds a horse after the postfix; (3) `EquipmentElement.Invalid.Item == null` and `Mission.SpawnAgent` guards mount creation on `Item != null`, so clearing the slot produces no mount. |

### The one finding (LOW) — declined with reasoning

| # | Sev | Finding | Disposition |
|---|-----|---------|-------------|
| 1 | LOW | Static `_service` cache in `Patch46_TournamentDwarfDismount` has no `ResetForUnload()` (unlike `CrashReportPatchHelper`). | **DECLINED (recorded, not silent).** The patch mirrors [`Patch40_HideoutDescription`](../../Main/Features/BanditManagement/Hooks/Patch40_HideoutDescription.cs) exactly — identical lazy `_service ??= IoC.Resolve<>()` with no reset. `TournamentService` is a pure, stateless singleton with no disposable deps; `GetService()` is null-guarded; the only manifestation needs reload-in-same-process *and* re-patch, and the stale instance would still resolve identical logic. Per the simplicity criterion, adding reset plumbing for that edge case (which the sibling pattern doesn't handle either) isn't warranted. Revisit if `TournamentService` ever gains disposable dependencies. |

## Why each deep-review agent's scope behaved as it did

- **Standards / Efficiency** — correctly scoped to the changed files; nothing to add.
- **Compatibility** — the load-bearing risk here was a private-field name (`_match`) and a struct-clearing semantic (`EquipmentElement.Invalid`), both verified against installed DLLs. This is exactly the agent's remit and it nailed it.
- **Completeness** — surfaced the doc/issue debt, which this pass discharges. Note it also caught that `arena.md` + `tournament-armor-assignment.md` were stale (pre-#137) — fixed here.
- **Data Flow** — the decisive agent. It is the one that would have caught the *originating* bug had the feature been reviewed before shipping the original tournament model: the "which method owns slot 10" question is a data-flow trace, not a per-file check.

## Honesty / verification status (what did NOT run this session)

- **No `dotnet build` / `dotnet test` run.** Bannerlord was running (PID 67828) and held `TAOM.dll` / `0Harmony.dll` / `Bannerlord.ButterLib.dll`; the Bannerlord.BuildResources post-build deploy (`CopyBinariesWindows` / `CopyModule`, gated only by game-folder existence, not by `DisableModuleCopy`) cannot overwrite locked files. So the new tests' GREEN state is **unconfirmed** — the code is written and the logic reviewed, but no build evidence exists yet. (Per `evidence-over-claims.md`, this is stated, not papered over.)
- **No Codex adversarial pass.** `/review-codex` was not run this session. The completion workflow's Phase 2/3 Codex steps remain outstanding.
- **No in-game verification.** Dwarf-on-foot in an Erebor town + a human-culture town (empire-template fallback), and non-dwarf-still-mounted regression, all remain to be observed in-game.

These three are the outstanding gates before this fix is "done" by the full completion workflow. Issue #277 was closed at the user's explicit instruction with this status recorded in its close comment.

## Preventive action

- **Memory:** [`feedback_tournament_horse_from_weapon_template_not_armor`](../../../.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_tournament_horse_from_weapon_template_not_armor.md) — the tournament mount is sourced from the culture weapon template via `PrepareForMatch`, not from `GetParticipantArmor`; to gate it, postfix `PrepareForMatch` and clear slots 10/11. Generalises the "enumerate every producer of an assembled value" lesson. Linked to [[nonhumanoid-creature-troop-not-mount]] (the broad custom-skeleton-can't-be-mounted principle) and [[dwarf-race-npc-needs-dwarf-skeleton-armor]] (sibling custom-skeleton equipment trap).
- **No new rule file** — the existing data-flow review remit already covers "trace every producer"; this is a fresh instance, not a gap in the rules.
