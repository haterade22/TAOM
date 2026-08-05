# RCA — the save-definer guard told players to disable vanilla (2026-08-01)

**Feature:** CoopInterop / #370 · **Trigger:** user-reported, from collected player logs ·
**Severity:** HIGH (user-facing, shipped) · **Status:** fixed

## Top-line

`SaveDefinerCollisionGuard` emitted, at the top of **every** collected user log:

```
[SaveDefiners] SAVE ID COLLISION on base id 10000 between:
  TaleWorlds.Core::TaleWorlds.Core.SaveableCoreTypeDefiner,
  TaleWorlds.ObjectSystem::TaleWorlds.ObjectSystem.SaveableObjectSystemTypeDefiner.
  Two mods claim the same save-system id range; the game will fail to start ... Disable one of them.
```

Both named types are vanilla engine code, in a game that starts fine. TAOM was instructing players
to disable `TaleWorlds.Core`.

This is the second-worst outcome for a diagnostic. The worst is silence; this is confident noise —
it discredits every other line the same tool prints, including the real collision it exists to
catch. It was reported twice before being fixed, each time deferred as "not mine".

## Root cause: the rule the check asserts is false

The detector grouped `SaveableTypeDefiner` subclasses by **base id** and treated any shared base as
a collision. The engine does not key on the base. Verified against installed v1.4.7,
`SaveableTypeDefiner.AddClassDefinition`:

```csharp
protected void AddClassDefinition(Type type, int saveId, IObjectResolver resolver = null)
{
    TypeDefinition classDefinition = new TypeDefinition(type, _saveBaseId + saveId, resolver);
    _definitionContext.AddClassDefinition(classDefinition);
}
```

The registered key is `_saveBaseId + saveId`. A shared **base** is therefore legal whenever the
per-type offsets differ — and vanilla relies on that. Enumerating every `SaveableTypeDefiner`
subclass in the v1.4.7 dump gives **67 distinct base ids and exactly one duplicated pair**:

| Base id | Definer | Assembly |
|---|---|---|
| 10000 | `SaveableCoreTypeDefiner` | `TaleWorlds.Core` |
| 10000 | `SaveableObjectSystemTypeDefiner` | `TaleWorlds.ObjectSystem` |

Different assemblies, so the group took the `IsCrossAssembly` branch — the one carrying the
strongest wording and the instruction to disable something.

## Findings

| # | Sev | Bug | Why missed | Preventive action |
|---|-----|-----|-----------|-------------------|
| 1 | HIGH | Base-id equality treated as proof of collision; fires on a legal vanilla pair | The rule was never run against a known-good baseline. The unit tests used only synthetic records that already obeyed the assumed model, so they confirmed the code matched the theory while the theory was wrong | Test built from the REAL vanilla pair and real base id (`Detect_TwoVanillaDefinersSharingBaseId_ReportsNothing`) |
| 2 | HIGH | Reported at `LogError` with "**will** fail to start … Disable one of them" | Severity and certainty were set by how bad the *consequence* would be, not by how sure the *check* was | `LogWarning`, "may", and phrased as a lead to try — not an order |
| 3 | MED | Groups made entirely of game-shipped assemblies were reported at all | Nobody asked "what can the player actually do about this?" | Detector drops groups with no non-engine member |
| 4 | LOW | The detector's own test-file doc asserted base-id granularity was "the correct level for a warning" | Written from the same wrong model; became a second place the error looked authoritative | Corrected to state it is a heuristic, with the `_saveBaseId + saveId` reason |

## Why this survived two reports

It was correctly identified as *not caused by* the person noticing it, and there the trail ended.
Nothing routes an unowned, non-crashing, cosmetic-looking log line to anybody. The cost was
invisible precisely because it was cosmetic: no crash, no failing test, no metric — just every
support log starting with a false alarm and a player being told to break their install.

**Rule: an incorrect line in a diagnostic other people read is a bug with an owner, and the owner is
whoever notices.** "Not my file" is a routing statement, not a triage outcome.

## Why no review caught it

| Check | Why not |
|---|---|
| Unit tests (7, all passing) | Every fixture was synthetic and shaped by the same assumption the production code made. A test written from the model cannot falsify the model |
| `/deep-review`, 6 agents | Scoped to the changed files; `SaveDefinerCollisionDetector` was not being modified |
| 3 Codex passes | Same — asked to attack the co-op gating, not to audit an existing diagnostic's premise |
| In-game runs | The line is a warning during load. It never fails a build or a test, so nothing escalated it |

The generalisable gap: **no review stage validates a heuristic against a known-good baseline.** All
of them check that code does what it intends. None asks whether the intent is true.

## Preventive actions taken

1. `Detect` drops groups whose members are all game-shipped assemblies (`TaleWorlds.*`, Native,
   SandBox, SandBoxCore, StoryMode, CustomBattle, Multiplayer).
2. Wording matched to certainty: WARNING / "may" / "first two to try disabling".
3. Four new tests, two of which fail against the old code — including the real vanilla pair by name
   and the SandBox/StoryMode case.
4. Lesson recorded in `lessons/testing-qa.md`.

## What was deliberately NOT done

**True collision detection.** Getting this right means computing `_saveBaseId + saveId` per type,
which means invoking each definer's `Define*` virtuals against a synthetic `DefinitionContext` via
the internal `Initialize`. That would execute arbitrary third-party code speculatively at startup and
bind to engine internals that move between versions — a large, drift-prone risk for a diagnostic that
can never be better than the engine's own throw moments later. The heuristic stays, honestly
labelled. If it ever produces another false positive, delete it rather than deepen it.

## Cross-reference

Same feature, same day, different root cause: `rca-coop-veto-surface-2026-08-01.md` (gating the call
site rather than the rule; entry points reachable behind a gated exit).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bannerlord-together-compat.md](../features/bannerlord-together-compat.md)
- [docs/features/coop-interop.md](../features/coop-interop.md)

<!-- backlinks-end -->
