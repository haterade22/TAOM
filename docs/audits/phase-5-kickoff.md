# Phase 5 Kickoff — UI / Mixin / Prefab Cluster Review

For the next session. Read this + [feature-manifest.md](feature-manifest.md) + [wiring-matrix.md](wiring-matrix.md) + the three completed cluster docs ([cluster-gamemodels.md](cluster-gamemodels.md), [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md), [cluster-harmony-patches.md](cluster-harmony-patches.md)) before doing anything else.

## Audit state at start of Phase 5

| Phase | Status | Output |
|---|---|---|
| 0 (Manifest) | Complete | [feature-manifest.md](feature-manifest.md) |
| 1 (Wiring) | Complete | [wiring-matrix.md](wiring-matrix.md) + issue #122 |
| 2 (GameModels) | Complete (2026-05-13) | [cluster-gamemodels.md](cluster-gamemodels.md) + issues #134, #135, #137, #138, #140, #142, #144, #145, #147, #148 |
| 3 (CampaignBehaviors) | Complete (2026-05-13) | [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md) + issues #123–#131 |
| 4 (Harmony patches) | Complete (2026-05-13) | [cluster-harmony-patches.md](cluster-harmony-patches.md) |
| **5 (UI / Mixin / Prefab)** | **Not started** | This phase |
| 6 (Cross-feature handshake) | Not started | `cluster-cross-feature.md` |
| 7 (Tests) | Not started | `test-coverage.md` |
| 8 (Docs) | Not started | `docs-gaps.md` |
| 9 (Triage + Fix) | Not started | issues + commits |

## Goal

Audit every feature with a `[ViewModelMixin]` / `[PrefabExtension]` / Custom Widget surface against TAOM's UI rules. Find ViewModel mutation bugs, prefab extension target drift in v1.3.15, sprite verification gaps, localization mistakes, and binding case-sensitivity issues.

Output: `docs/audits/cluster-ui.md` per the format the prior cluster docs use. P1/P2 → GitHub issues labelled `audit-impl`.

This is **semantic correctness**, NOT wiring. Phase 1 already confirmed UIExtenderEx is registered. Phase 5 reviews the mixin bodies, prefab XML, and VM bindings.

## Targets (4+ features per manifest)

| Feature | Surface | Scope |
|---|---|---|
| **CareerSystem** | `CareerScreenView`, `CareerScreenVM`, sprite atlas `ui_taom_career_system`, prefab extensions | Heaviest — career portraits 800×400, ability icons 256×256, dedicated atlas. 50 careers × 3 archetypes. Many bindings. |
| **Messengers** | `EncyclopediaHeroPage` prefab extension, conversation mission routing | Freshly wired this session — full review wanted. Cross-reference issue #123 (singleton-state-reset gap). |
| **SpecialResources** | Resource gating UI in PartyScreen / inventory upgrades, `PartyCharacterVM` overrides | Cross-feature with CulturalFeats (resource-based feats). |
| **TimeAcceleration** | TimeControlVM mixin / prefab | UIExtenderEx surface; `OnApplicationTick` lifecycle consumer. |

Also include any **Custom Widget** classes (grep `class .*Widget : .*Widget`).

## Inputs

- [.claude/rules/gui-ui.md](../../.claude/rules/gui-ui.md) — UI rules (sprite verification, UIExtenderEx safety, VM bindings)
- [.claude/rules/csharp-architecture.md](../../.claude/rules/csharp-architecture.md) — "Constructor injection only" rule applies to VMs too
- Memory: `feedback_taleworlds_vm_setter_decompile.md`, `feedback_prefer_public_setter_over_reflected_notify.md`, `feedback_localization_textobject.md`, `feedback_filter_order_and_default.md`
- `Main/_Module/GUI/SpriteParts/Config.xml` + every `<atlas>/SpriteData.xml`
- `Main/Features/<X>/UI/` (per feature)
- `Main/_Module/GUI/Prefabs/` (per feature)

## Per-feature checks (apply each via 1 `feature-dev:code-reviewer` agent per feature)

### Check 1 — Sprite name verification
1. Grep every sprite reference (`Sprite="X"`) in the feature's prefab XML.
2. Cross-reference each against the matching `SpriteData.xml` — must exist.
3. Any reference not found → P1 (UI crash on widget mount).

### Check 2 — UIExtenderEx PrefabExtension safety
1. For each `[PrefabExtension(targetVM, "@..."`)]: confirm the target VM's prefab path actually exists in `SandBox` / `SandBoxCore` / `Native` modules.
2. For each child-element XPath in the extension: decompile the target prefab and confirm the indexed element exists in v1.3.15.
3. Any drift → P1 if widget mount throws, P2 if widget silently doesn't render.

### Check 3 — VM property setter no-op early returns
1. For each `[DataSourceProperty]` setter: verify it has the `if (value == _field) return;` no-op early return.
2. Memory: `feedback_taleworlds_vm_setter_decompile.md`. Missing = P2 (perpetual `OnPropertyChanged` triggers cause perf + UI flicker).

### Check 4 — VM property notification pattern
1. Any property assignment that uses reflected field-set + `OnPropertyChangedWithValue` should be rewritten to use the public setter.
2. Memory: `feedback_prefer_public_setter_over_reflected_notify.md` (Review #33 — generic-method lookup returns null for value-types).

### Check 5 — `@PropertyName` binding case-sensitivity
1. Grep `@.*` bindings in prefab XML.
2. Confirm each maps to an actual `[DataSourceProperty]` on the VM, case-sensitive.
3. Any mismatch → P2 (binding silently no-ops).

### Check 6 — Localization `{=key}Text` via TextObject().ToString()
1. Memory: `feedback_localization_textobject.md`. String properties intended to render in UI must wrap through `new TextObject("{=key}Default").ToString()`, not assign raw strings.
2. Bare-string assignment = P3 (works on default locale; breaks on translation).

### Check 7 — IGameStateListener (for GameStateScreen subclasses)
1. Memory: `feedback_gamestate_listener.md` — `GameStateScreen` subclasses MUST implement `IGameStateListener` or crash on open.
2. Cross-check every `class .*Screen : ScreenBase` against `IGameStateListener` implementation.
3. Missing = P1.

## Output format

`docs/audits/cluster-ui.md` mirrors `cluster-gamemodels.md`:

```markdown
# UI / Mixin / Prefab Cluster Audit — Phase 5

Last updated: <date>
Scope: 4+ features, N mixin classes, M prefab extensions

## Manifest corrections (if any)

## Master findings table
| # | Severity | Feature | Component | File:Line | Finding |

## Per-feature reports
### CareerSystem
…
### Messengers
…
### SpecialResources
…
### TimeAcceleration
…

## Cross-cuts
- Sprite verification gaps
- Prefab target drift (v1.3.15)
- VM mutation anti-patterns
- Localization anti-patterns

## GitHub issues opened

## Phase 5 complete
- N surfaces reviewed
- K P1, M P2, J P3 findings
- Phase 6 kickoff written
```

## Constraint

**No code edits this phase.** Findings → issues only. Phase 9 batches the fixes.

## Done condition

Phase 5 is complete when:

1. `docs/audits/cluster-ui.md` has master findings table + per-feature sections + cross-cuts populated.
2. Every P1/P2 has a GitHub issue (`audit-impl`).
3. `docs/audits/phase-6-kickoff.md` written for the next session (Cross-feature handshake review per session-prompts.md Phase 6 template).
4. `docs/audits/README.md` phases table updated.
5. `/context-save` ran with descriptor `phase5-ui-complete`.

## Pre-flight

1. `/context-restore` to load the latest snapshot.
2. Read this brief + the 3 cluster docs from Phases 2/3/4.
3. Read `.claude/rules/gui-ui.md` end-to-end.
4. Spawn 1 `feature-dev:code-reviewer` agent per UI-bearing feature (4+) in parallel.
5. Aggregate to `cluster-ui.md`.

## What this phase will NOT cover

- Pure data XML (covered in Phase 7/8).
- Sprite atlas configuration changes (Phase 9 fix scope).
- Cross-feature UI clashes (e.g., CareerSystem screen vs FiefManagement screen z-order) — that's Phase 6.

## What still hasn't been audited at all (forward-looking)

Phases 6, 7, 8, 9 are all unstarted. After Phase 5, the audit shifts from "find bugs in code" to "find gaps between features" (Phase 6), then "find untested code" (Phase 7), "find stale docs" (Phase 8), and finally "fix everything" (Phase 9).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/cluster-ui.md](./cluster-ui.md)

<!-- backlinks-end -->
