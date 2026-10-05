# RCA: Siege Forces (#734) and Creature Siege Role (#735), deep review 2026-10-05

## Top-line

`/deep-review` ran seven lenses in two waves on branch `feat/siege-forces`. Wave 1 was Standards, Engine compatibility, and Data flow (once per feature). Wave 2 was Completeness, Efficiency and Design. XML and Tooling were not in scope.

The review found:
- 0 incompatibilities with the installed engine (v1.5.4).
- 3 MEDIUM defects: F1 to F3.
- 9 LOW defects and gaps: F4 to F12.
- 7 KEEP proposals from the design lens, all behaviour-preserving and all applied.
- 1 LOW efficiency item, rejected under the simplicity criterion.

Every defect was fixed before the commit:
- Pass A covered the creature siege role.
- Pass B covered the siege picker and the cross-cutting items.
- One convergence review then checked both passes.

Mike decided the two behaviour-changing items:
- **F3:** cap 6, and dropped bridges stay quiet.
- **F5:** defenders hold the inner gate in long-approach scenes, and only the log changes.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | The picker could be live while `Patch102_SpawnTotalsFit` was not attached. A selection that left troops out would then stall deployment. | Coupled patches, partial apply | The design treated the two prefixes as one unit. Two engine facts went unchecked: `PatchCategoryIndex` applies classes in assembly order, and a class that throws stops the category but leaves the earlier classes patched. The picker was declared first. | The fit class is now declared first, with a wiring test that pins the order. The offer is refused unless `IsSpawnTotalsFitAttached` confirms the fit through Harmony. Lesson added to `harmony-il.md`. |
| F2 | MED | The slot fallback could script a troll to the far side of a shut gate. | Engine API misread | The design trusted the name of `Scene.DoesPathExistBetweenPositions`. In native code it only compares the navmesh island ids of two faces, and it knows nothing about the agent, so it cannot see per-agent face exclusion. Islands merge when a ladder goes up. The vanilla order (own side, far side, centre) was copied, but vanilla agents can climb. | The far-side attempts were removed. Docs now say "same navmesh island". Lesson added to `adapters-taleworlds-api.md`. |
| F3 | MED | An exclusion cap of 7 could rewrite the scene's navmesh boundary bit. | Native capacity claim | The v1.5.4 compatibility check derived the capacity from the per-face mark byte. It read the registry writer, but not the face-ability setter (0x401B20), which re-marks sets only from index 1 because bit 0 belongs to the engine. Whether registry entry 0 is pre-seeded is still unverified. | The cap is now 6, which is safe under both readings and still covers 2 tower entrances and 4 ladders. Bridges are dropped silently. Lesson: a native capacity claim reads every writer of the resource, not just the registrar. |
| F4 | LOW | The ground probe read a miss as ground at z 0. | Sentinel return | Native `GetGroundHeightAtPosition` returns 0 when it hits nothing, but the adapter assumed NaN. | Replaced with `RayCastForClosestEntityOrTerrain`, which returns a hit flag. Lesson added to `adapters-taleworlds-api.md`. |
| F5 | LOW | The activation log measured each anchor from its own gate. That hid a 119 m (Minas Morgul) or 26 m (Dale) inner-gate hold. | Diagnostic reference frame | The log was written for the per-gate anchor rule, not for the question the in-game gate RG-E asks. | The log now measures distance from the outer gate. Mike kept the inner-gate hold. |
| F6 | LOW | The gate-blow log printed a computed "after" HP that overstated what was left in campaign battles. | Stage value taken as final | `ApplyDamageScaling` is not the last stage. General modifiers (campaign damage bonuses, career buffs) and rounding apply after it. | The computed value was dropped, and the tuning note now names the later bonuses. Lesson added to `gamemodels-services.md`. |
| F7 | LOW | `TaomAgentStatCalculateModel.cs` grew past the 150-line entry-point limit (155). | Growth of a shared entry point | The builders measured the files they created, not the shared files they grew. | The history comment was trimmed (now 138 lines). Standards lens: measure every touched entry point. |
| F8 | LOW | TOR_Core was not named in the feature code, and the Siege Forces comment still called it comparison-only. | Provenance "Name it" | The register row was updated in the docs phase, after the code comments were written. | Both comments fixed. The commit body names TOR_Core, GPL-3.0 and `behavioural-port`. |
| F9 | LOW | The `DynamicNavmeshIdStart` reflection site was missing from `ReflectionSiteBindingTests` and `reflection-sites.md`. | Catalogue completeness | A feature binding test pinned it, so the central catalogue looked redundant. | A DataRow and a Category B row were added. |
| F10 | LOW | A logger fault could discard a computed spawn fit. | Fault isolation | The log call shared the try block that returns null on failure. | Compute, then log in its own guard, then return. Tested. |
| F11 | LOW | The pending fit was recorded only when the mission open did not throw. | State on a fault path | `GameStateManager` pushes the mission state before listeners run, so a throw can leave a filtered mission with no fit. | The fit is now recorded whenever anything was dropped. A token guard neutralises a stale record. |
| F12 | LOW | The ready-list filter kept the lowest-priority copies of a troop type. | Ordering assumption | The model's appended window is in ascending priority, and the filter kept the first K. | The filter now decides from the window's end. Tested. |

## Root-cause pattern: trusting a name or a signature for engine behaviour

F2, F3, F4 and F6 share one cause. A design or adapter relied on what an engine member's name or signature implies:
- **F2:** "path exists" is really an island compare.
- **F3:** "8 bits" really leaves 6 clean sets, because the engine owns bit 0.
- **F4:** a float height is really 0 on a miss.
- **F6:** a damage "scaling" stage is not the final damage.

`AGENTS.md` "Research first" already requires reading behaviour, not just signatures. The miss was applying it to signature-level facts only. The design and compatibility passes read native code for the claims they made. The gap was the behaviour that a method name implies but no claim states.

## Why each lens missed or caught them

- **Engine compatibility** caught F2 to F4. The design workflow's verifiers and the v1.5.4 compatibility pass had checked these members' signatures and call sites, but not the native bodies behind the name-implied behaviour.
- **Data flow** caught F1, F2 (independently, from the managed side), F5, F6 and F10 to F12. The cross-seam gap in F1 needs end-to-end tracing, which neither builder's checker did across the patch-apply layer.
- **Standards** caught F7 to F8.
- **Completeness** caught F9, and the extra test and doc work the cap change needed.

## Feedback to codify

The lessons appended to `docs/reviews/lessons/harmony-il.md`, `adapters-taleworlds-api.md` and `gamemodels-services.md` carry the durable rules. No new rule file is needed: the pattern is "Research first" applied to implied behaviour, and the lessons name the three engine facts concretely.
