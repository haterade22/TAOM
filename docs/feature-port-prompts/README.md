# Feature Port Session Prompts

Five self-contained session prompts for porting features 3–7 of the external-developer drop at `Downloads/Features_fixed/` into `Main/Features/`. Each prompt is pasted into a fresh Claude Code session; the session does one feature end-to-end (implement + tests + doc + CHANGELOG + `/deep-review` + `/review-codex` + fixes).

## Why separate sessions?

- **Context budget:** the SiegeDismount + MixedFormations cycle burned ~60K tokens of conversation. Doing all 7 in one session would hit the compaction boundary mid-feature.
- **Independence:** features 3–7 only depend on `IFormationAdapter` (already shipped with feature 2 in [Main/Adapters/IFormationAdapter.cs](../../Main/Adapters/IFormationAdapter.cs)). The other adapter introductions are confined to a single feature each.
- **Resumability:** if one session hits a snag, the others proceed independently.

## Recommended order

The order respects the load-bearing-adapter dependencies established in the integration plan:

| # | Feature | Reason for this slot |
|---|---|---|
| 3 | SmartCavalryAI | Reuses `IFormationAdapter` from feature 2; introduces `IBattlefieldQueryAdapter` (only this feature needs it) |
| 4 | FiefManagement | Isolated UI subsystem; no shared adapters with 5/6/7. Run any time. |
| 5 | QuickActions | Introduces `IInventoryVMAdapter` — load-bearing for feature 6 |
| 6 | EquipPresets | Reuses `IInventoryVMAdapter` from feature 5; adds Saveable presets |
| 7 | CompanionTactics | Largest (3 sub-features). Reuses `IFormationAdapter` from 2. Save for last when patterns are well-rehearsed. |

## What each prompt includes

1. **Goal** — what this feature does, in 1–2 sentences
2. **Decompiled source path** — where the developer's original C# lives
3. **Files to create** — concrete file list with one-line responsibilities each
4. **Adapters** — which existing adapters to reuse, which to extend, which to create
5. **Harmony patches** — patch numbers + targets + Prefix/Postfix
6. **MCM settings** — exact settings to append to [`TaomSettings.cs`](../../Main/Features/TaomSettings.cs)
7. **Cross-session lessons that apply** — which `feedback_*.md` memory entries are relevant
8. **Per-feature gotchas** — specific things the decompiler agent flagged during analysis
9. **Acceptance gates** — tests + build + doc + CHANGELOG + reviews
10. **Verification** — in-game golden path

## Pattern reference (read before starting)

Every prompt assumes the session has read these from the **prior two completed features** as templates:

- **SiegeDismount** (mission-state singleton, no Harmony patches, Codex caught 3 HIGH+MED bugs):
  - [Main/Features/SiegeDismount/](../../Main/Features/SiegeDismount/) — full feature folder
  - [docs/features/siege-dismount.md](../features/siege-dismount.md) — the doc as a template
  - [docs/reviews/codex-adversarial-siegedismount-2026-05-06.md](../reviews/codex-adversarial-siegedismount-2026-05-06.md) — what /review-codex output looks like

- **MixedFormations** (singleton with cache, one Harmony Prefix, dead-MCM-settings caught & removed):
  - [Main/Features/MixedFormations/](../../Main/Features/MixedFormations/) — full feature folder
  - [docs/features/mixed-formations.md](../features/mixed-formations.md) — the doc as a template
  - [Main/Adapters/IFormationAdapter.cs](../../Main/Adapters/IFormationAdapter.cs) + [FormationAdapter.cs](../../Main/Adapters/FormationAdapter.cs) — load-bearing adapter for feature 3 + 7

## Cross-session memory (auto-loaded by every session)

These three feedback memories were codified after Codex review #34 (SiegeDismount) and are auto-loaded in every Claude session for this repo. Each prompt names which apply:

- `feedback_substring_keyword_matches_external_data.md` — when feature uses substring keyword matches against engine state, grep ALL `ModuleData/*.xml` first
- `feedback_adapter_modifier_preserving_overload.md` — TaleWorlds inventory/equipment APIs have parallel `(ItemObject, int)` and `(EquipmentElement, int)` overloads; the latter preserves modifier
- `feedback_user_facing_promise_must_match_code.md` — when porting features with multiple modes, trace every MCM hint / dropdown label / tooltip to the implementation; if promise doesn't match code, fix one or the other — never ship the mismatch

## Workflow each session must follow (mandatory per CLAUDE.md)

```
1. Read the integration plan: C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md
2. Read this prompt file end-to-end before writing any code
3. TDD cycle:
   a. Read the decompiled source the prompt names
   b. Write failing tests (RED)
   c. Implement service + adapter + hook + IoC + SubModule wiring (GREEN)
   d. Build: dotnet build Main/TAOM.csproj -c Debug
   e. Test: dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "FullyQualifiedName~<FeatureName>"
   f. Full suite: dotnet test --no-build (must stay green)
4. Write docs/features/<feature-name>.md from docs/features/TEMPLATE.md
5. Add a CHANGELOG.md entry above the most recent entry (see SiegeDismount/MixedFormations entries as templates)
6. Run /deep-review <FeatureName> — fix every HIGH and MEDIUM finding in same session
7. Run /review-codex <FeatureName> — dispatch via /codex:adversarial-review --background, verify findings
8. For every confirmed Codex bug: write a regression test, fix it, RCA in the review file
9. If RCA produces a generalizable lesson: codify it as a feedback memory in C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/ and index it in MEMORY.md
10. Final: dotnet test (must be green), report files-touched + tests-count + verdict
```

NEVER skip any step. Codex catches bugs deep-review misses; deep-review catches bugs Claude's first pass misses. Both are mandatory.

## Verification per feature

Each prompt has a "Verification" section with the in-game golden path. Don't commit — leave the working tree dirty so the user can test in-game and approve before commit.

## Files this index references

| File | Purpose |
|---|---|
| [feature-3-smartcavalryai.md](feature-3-smartcavalryai.md) | Cavalry charge/passthrough/reform state machine |
| [feature-4-fiefmanagement.md](feature-4-fiefmanagement.md) | F6 hotkey opens vanilla TownManagementVM for any owned fief |
| [feature-5-quickactions.md](feature-5-quickactions.md) | "Sell All" → multi-action menu (sell damaged / low-value / unequip-all) |
| [feature-6-equippresets.md](feature-6-equippresets.md) | Per-hero equipment presets (save / load / update / delete) |
| [feature-7-companiontactics.md](feature-7-companiontactics.md) | Three sub-features: roles, formation presets, battle action bar |
