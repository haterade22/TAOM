# RCA: Bannerlord v1.5.0 engine bump (2026-08-19)

## What this was

Steam force-updated the install from v1.4.8 to v1.5.0 on the beta branch. The previous three bumps were handled by `/engine-bump` with, at v1.4.8, **zero changes to `Main/`**. This one needed eight compile-level adaptations and produced **four CRITICAL runtime defects**, none of which the compiler, the full test suite, or the existing binding gate could see.

Reviewed by four independent passes: an internal standards agent (PASS, 0 violations), a TaleWorlds API agent (28 verified, 0 incompatible), a cross-system data-flow agent (9 flows, 0 hard gaps), and Codex (1 P1, 2 P2). Codex found a defect all the internal passes missed, and a false-green in a gate written during this very session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | CRITICAL | `CultureObject.Executioner` is new in v1.5.0, read from XML, and dereferenced **unguarded above its own `_useExecutioner` check**. Vanilla ships it on 6 of 16 cultures; TAOM shipped it on 0 of 24. Any execution by a TAOM-culture lord was a null dereference. | Data / new engine field | Nothing in TAOM referenced the field, so no grep, compile or test could see it. It is an ABSENCE in data. | Byte-diff `CultureObject.Deserialize` between versions every bump. Done here and it proved `executioner` was the only addition. |
| 2 | CRITICAL | `PartyNameplateWidget` gained `BloodFeudIconWidget`, dereferenced with no null guard on every late-update tick. TAOM's stale prefab clone never assigned it: a per-frame NRE on the campaign map. | Stale prefab clone | Prefabs resolve by BASENAME with last-module-wins, so a stale TAOM copy silently keeps overriding a rewritten vanilla layout. Nothing turns red. | The mtime change-set oracle scoped 73 rewritten vanilla GUI files down to the 9 TAOM actually shadows. |
| 3 | CRITICAL | `Patch53_PartyIconScale`'s people site was **relocated** to a new helper method and a third site was **newly hardcoded**. The transpiler fails safe, so the mount honoured the MCM slider while the rider stayed vanilla-size. | Fail-safe transpiler | Existing tests fed **synthetic** `CodeInstruction` lists, so they validated the matcher and proved nothing about the engine. `HarmonyPatchBindingTests` only proves the TARGET resolves. | New `TranspilerSiteBindingTests`: feeds REAL engine IL through the production helper and asserts no `LogWarning`. "No warning" is the proof the site resolved. |
| 4 | CRITICAL | ASO's Trader start gender-filters `Culture.NotableTemplates` and indexes the result unguarded. **All 22 TAOM cultures had zero female notable templates** against vanilla's 4 to 5. A female player crashed at campaign start. | Data / gender coverage | Pure data, all references resolved, and no engine code exercised the gender filter before v1.5.0. | New `NotableTemplateGenderTests` pins both genders per culture, **including the effective XSLT output**. |
| 5 | HIGH | `MobilePartyVisual.AddCharacterToPartyIcon` lost the two parameters the banner-colour postfix wrote to. Applied via `Harmony.Patch`, so it would have **thrown at startup**. | Gate hole | `HarmonyPatchBindingTests` verifies target resolution and nothing more. `TargetMethod()` resolves by name, so it stayed green. | Re-seamed as a transpiler with an IL gate. The parameter-binding hole in `HarmonyPatchBindingTests` remains OPEN, see below. |
| 6 | HIGH | v1.5.0 added `Hero.MainHero.Gold = 1000;` to `FinalizeCharacterCreationState`, which runs **after** `ApplyFinalEffects` where TAOM grants culture-keyed startup gold. An assignment, not an addition, so every new campaign started at a flat 1000. | Engine body change on an unpatched path | TAOM patches neither method. The regression is in the ORDER of two engine calls, invisible to any signature-level check. | Member-level diff ranking (P1 = bound member, body-only change) is the only pass that surfaces this class. |
| 7 | HIGH | `SpecialResourcePrefab`'s XPath matched 0 nodes after v1.5.0 restructured `MapBar.xml`. `InsertType.Replace`, so a pure silent no-op. | UIExtenderEx XPath | UIExtenderEx logs one red in-game message at movie load and throws nothing. Invisible offline. | New `PrefabExtensionBindingTests` resolves every XPath through the engine's own basename rule and reports the first step that drops to zero. |
| 8 | HIGH | 88 dwarf action sets missing `act_ghurab_captain_idle`, a v1.5.0-new action type in Native's `as_human_warrior`. The 1.3 to 1.4.6 dwarf water-CTD shape. | Data parity | The gate existed and was correct. At v1.4.8 it had nothing to catch because vanilla `ModuleData` did not move; v1.5.0 changed 88 files. | None needed. `audit_action_set_parity.py` did its job the moment there was something to find. |
| 9 | HIGH | ASO's Civil Unrest modifier was **silently inert**: it works by swinging vanilla's loyalty thresholds, and `TaomSettlementLoyaltyModel` overrode both with a hardcoded pair. | Override shadows a new engine toggle | A GameModel override that replaces a constant cannot see that vanilla later made that constant conditional. | Enumerate every override against the CURRENT vanilla body on a bump, not just the ones that fail to compile. |
| 10 | HIGH | `comment_strings.xslt` dropped `<tags>` on 23 of 35 override templates. 12 templates already carried the fix **and a comment explaining it**. | XSLT passthrough | A known defect fixed partially. Reading the stylesheet does not reveal it: the bug is an absence. | New `CommentStringTagsTests` asserts on transform OUTPUT, reading the overridden ids from the stylesheet itself so new overrides are covered automatically. |
| 11 | MED | The new `PrefabExtensionBindingTests` read prefabs from the **deployed** module, which lags the checkout under the prescribed non-deploying build. Measured: deployed file had 0 BloodFeud refs, repo had 5. | False green in a NEW gate | Written and shipped green in the same session that fixed the bug it guards. Caught by Codex. | Gate now overlays the repository prefabs last. |
| 12 | MED | `Mission.SpawnAgent` gained an `agentSpawnEquipment` parameter, weakening the premise of TAOM's `AgentOverridenSpawnEquipment` guard. | Body-only change on a bound member | Surfaced only by regenerating the API snapshot and reading the diff. | Guard hardened. Latent, not live: the new overload has zero shipped callers. |

## Root-cause pattern

**Every CRITICAL in this bump was invisible to the three gates the project trusts** (compiler, full suite, `BindingVerification`), and every one of them was found by comparing against the engine rather than by running TAOM.

The unifying shape: **an absence cannot be grepped.** A missing culture attribute, a missing prefab binding, a missing female template, a relocated IL site, and a reordered pair of engine calls all present as *nothing*. There is no symbol to search for, no signature to fail against, no assertion to trip. The only detection method that works is a differential one, and the only differential available after Steam overwrites in place is the preserved baseline.

That makes **Stage 0 of the bump plan (preserve before regenerating) the single load-bearing step**. Findings 1, 3, 5, 6 and 12 were all found by diffing v1.4.8 against v1.5.0. Had the baseline been overwritten, none of them would have been findable at all. The v1.4.7 to v1.4.8 bump already paid this bill once, when `_modules_build` did not exist and 34 module assemblies lost their baseline permanently.

A second, sharper pattern shows up in findings 4, 10 and 11: **a fix and its gate written in the same session share the author's blind spot.** The XSLT tags fix covered 23 templates because 12 were already done, and the pattern was clear. The notable-gender fix covered `taom_spcultures.xml` because that is where cultures obviously live. In both cases the gate I wrote had exactly the same scope as the fix, so it could never catch what the fix missed. Codex caught the notable-gender case precisely because it did not share the assumption, and re-derived the effective XSLT output instead of reading the XML.

## Why each pass missed what it missed

- **Compiler.** Sees signatures. Findings 1, 2, 3, 4, 6, 7, 8, 10 are data or IL, not signatures.
- **Full suite (6,699 tests).** Every one passed throughout, including while the banner-colour patch would have thrown at startup. The suite tests TAOM against TAOM, not TAOM against the engine.
- **`HarmonyPatchBindingTests`.** Structurally covers target RESOLUTION only. Finding 5 stayed green because `TargetMethod()` resolves by name and the name did not change. **This hole is still open**: the gate does not verify that a patch method's parameter names exist on the target. That remains the single highest-value untaken preventive action from this bump.
- **Standards agent.** Correctly scoped to ADR compliance. None of these are ADR violations.
- **API-compatibility agent.** Verified 28 usages, 0 incompatible, and was right. Every finding it could see, it saw. Findings 1, 2, 4, 6 are not API usages at all.
- **Data-flow agent.** Traced 9 flows with 0 hard gaps and independently produced the `CultureObject.Deserialize` byte-diff that closed finding 1's "what else changed" question. It did not catch finding 4's XSLT half because the orchestrator's brief framed the notable question around `taom_spcultures.xml`. **A brief that names a file scopes the agent to that file.**
- **Codex.** Found finding 4's XSLT half and finding 11. It shared none of the session's assumptions, which is the entire value of an independent pass.

## Preventive actions taken

Five permanent gates, all in the existing `BindingVerification` category or as plain data invariants so no new invocation has to be remembered:

| Gate | Closes |
|---|---|
| `TranspilerSiteBindingTests` | Fail-safe transpilers whose IL anchor moved. Real engine IL through the production helper; "no `LogWarning`" is the proof. |
| `PrefabExtensionBindingTests` | UIExtenderEx XPaths that no longer resolve, with a progressive-prefix probe naming the first step that drops to zero. |
| `CommentStringTagsTests` | XSLT overrides that drop children, asserted on OUTPUT, with the id list read from the stylesheet itself. |
| `NotableTemplateGenderTests` | Cultures with no template of a given gender, covering both the XML and the effective XSLT output. |
| Rewritten `PartyIconScaleTranspilerTests` | Matcher correctness for the split entry points, kept deliberately separate from the engine-IL gate. |

Plus two process facts worth carrying forward:

- **The mtime change-set oracle.** The update stamps every file it writes. Captured before anything else runs, that is an exact free list of what changed and it substitutes for the content baseline that no longer exists. It scoped 73 rewritten GUI files to the 9 that matter. **A Steam integrity verify destroys it**, so capture is now Stage 0.2 of the bump plan.
- **The assembly-level diff is worthless when every assembly changes.** At v1.4.8 it cut 56 files to 8. At v1.5.0 it cuts 56 to 56. Ranking must move to the member level, and the three snapshot files are the filter that turns a wall of diff into a work list.

## Still open

- **`HarmonyPatchBindingTests` does not verify patch-method parameter binding.** This is the hole finding 5 walked through. Roughly 40 lines in the existing class would close a whole failure family.
- ~~**Codex P2a**~~ **RESOLVED.** The relation half of the Execution feature was re-homed onto the
  Blood Feud seam rather than retired. `ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch`
  postfixes `GetBloodFeudStartRelationPenaltyToOtherClan` and routes it through the existing
  `IOnExecutionAction.GetRelationModifier`, so a cross-alignment execution costs nothing with
  observers who share the executor's side or are neutral, while the victim's own side still objects
  and the blood feud itself is untouched. Patching the model method rather than the apply loop also
  keeps the pre-execution confirmation tooltip honest. `GetRelationModifier` went from zero
  consumers back to live; `IOnExecutionAction.IsKinslaying`, dead since before this bump, was
  deleted with its two tests. Original finding, kept for the record:

- **Codex P2a**, a product decision rather than a defect: `TaomExecutionRelationModel` was removed and only the trait-penalty half of the Execution feature was ported. The 1.5x kinslaying multiplier and cross-alignment relation shaping are gone, and `IExecutionRelationService`, `ExecutionActionHook.GetRelationModifier` and `IOnExecutionAction.IsKinslaying` are now dead. Codex argues this is a gameplay regression, not neutral cleanup, and names the new seam (`GetBloodFeudStartRelationPenaltyToOtherClan`, `OnBloodFeudStateChanged`) where it could be re-homed.
- **In-game verification.** Nothing offline settles it.

## Lessons codified

Appended to `docs/reviews/lessons/`:

- `harmony-il.md`: a fail-safe transpiler needs a gate that feeds it REAL engine IL; a synthetic-instruction test validates the matcher and proves nothing about the engine.
- `xslt-moduledata.md`: when a stylesheet REPLACES a vanilla list rather than extending it, the vanilla contents are gone; assert on the transform output, never on the markup.
- `testing-qa.md`: a gate written in the same session as the fix inherits the fix's blind spot; scope the gate from the engine's consumption path, not from the file the fix touched.
- `data-content-cultures.md`: a new engine-read attribute on a shared type is an absence with nothing to grep for; byte-diff the deserializer every bump.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/advanced-start-options.md](../features/advanced-start-options.md)
- [docs/migration/TRACKING.md](../migration/TRACKING.md)

<!-- backlinks-end -->
