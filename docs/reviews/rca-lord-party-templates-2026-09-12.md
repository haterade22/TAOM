# RCA: LordPartyTemplates (#580) deep review, 2026-09-12

Five agents over the per-hero party template feature (Patch88: a postfix on the
`Clan.DefaultPartyTemplate` getter, live only inside two spawn scopes, plus a JSON map, a pure
decision and two templates). Standards, completeness and data flow (11 flows traced, 0 gaps) came
back clean. Compatibility verified 19 engine members on the installed 1.4.8 DLLs and, by decompiling
the vendored 0Harmony, confirmed the transparent-finalizer claim against Patch65's co-located
finalizer. Efficiency reported two items. One was real and is fixed; one was refuted on evidence.
Two design notes from the compatibility and data-flow agents went into the code comment and the
feature doc.

## Findings

| # | Sev | Finding | Category | Verdict | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | MEDIUM | The postfix built a lambda capturing the service on every read that reached the decision, so every in-scope read of the getter allocated a closure. | Harmony patch overhead | CONFIRMED, fixed: `LordPartyTemplateResolution.Resolve` takes `ILordPartyTemplateService` and the patch passes the field through. Tests use a small fake instead of a delegate. | The delegate parameter was chosen to keep the decision "pure and testable", and a pure function taking a `Func` read as the textbook shape. The cost is small (two reads per lord spawn, only while a scope is open) and the rule that names it (`.claude/rules/harmony-patches.md`, closure creation in a patch body) was applied only to per-frame targets. An interface parameter is just as pure and allocates nothing. | None new. The rule exists and the fix is the shape the rule already prescribes. |
| 2 | LOW | The `_warned` set of hero ids grows without a cap over a long campaign. | Static state growth | REFUTED. `WarnOnce` is reached only when `Resolve` returned a template id, which happens only for a hero present in `lord_party_templates.json`, so the set is bounded by the JSON's entry count (two today) and is cleared on `ResetForUnload`. No change. | The agent read the warn path without tracing what gates it. | None. Recorded so the next reader does not re-open it. |
| 3 | note | The ambient owner is set for the whole of `SpawnLordParty` and `InitializeLordPartyProperties`, not for the one statement that reads the template, so any synchronous reader of the owner's clan template inside that window sees the swap. | Design surface | Documented: the compatibility agent decompiled the chain and found no such reader in 1.4.8 (`MobilePartyCreated` fires inside `MobileParty.CreateParty` while the scope is open; none of its three listeners reads the getter; `LordPartyComponent.CanHaveNavalNavigationCapability` is overridden to `true`). Added to the Patch88 doc comment and the feature doc as a thing to re-check on an engine bump. | Not a miss. The window was a deliberate trade for not writing a transpiler; the review made the boundary explicit. | Re-check on engine bumps alongside the binding tests. |
| 4 | note | Two lords of the same clan spawning on one call stack would let the inner read match the outer mapping. | Re-entrancy residual | Documented. The data-flow agent traced `RemoveGovernorOf` and the `MobilePartyCreated` listeners and found no path that nests one lord spawn inside another. `__state` save and restore keeps the ambient owner correct for any nesting depth; only the clan-match heuristic would be fooled, and only for same-clan nesting. | Not a miss. | None. |

## Root-cause pattern

Nothing systemic. The one real finding is the closure-in-a-patch-body shape the Harmony rule
already names, applied by me to a target I had costed as cold. The reasoning "this only runs
twice per spawn" was correct about the cost and still wrong about the shape: the interface
parameter is the same purity with no allocation, so the delegate bought nothing.

## Why each agent missed the confirmed finding

- Standards (Agent 1): allocation cost is outside its rule set.
- Compatibility (Agent 2): signatures and engine semantics only.
- Efficiency (Agent 3): found it. Its frequency table over-reached in one place: it costed the
  closure against every read of `Clan.DefaultPartyTemplate` game-wide (the naval-capability
  reads on AI ticks), but the postfix returns before the decision whenever no ambient owner is set,
  so those reads never reached the lambda. The finding stood on the in-scope reads alone, which is
  why it was fixed rather than argued.
- Completeness (Agent 4): presence of tests, docs and wiring only.
- Data flow (Agent 5): traces what is connected, not what it costs.

## Feedback memories to codify

None. The one confirmed finding is covered by an existing rule, and the two notes are design
surface that the feature doc now carries.

## Verification after the fix

`dotnet test TAOM.Tests --filter FullyQualifiedName~LordPartyTemplate`: 46 passed. Full suite rerun
before the commit; the number is quoted in the commit and CHANGELOG.
