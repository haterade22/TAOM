# RCA: co-op player capture fix (#1139), 2026-11-13

Tier 1 deep review of the worktree `E:/repos/taom-coop-pvp` (branch `fix/coop-pvp-capture`, base 4cafca4ab): Lens 5 Data flow and Lens 2 Engine compatibility, one wave, no HIGH, so no second wave. Codex did not run (out of usage). The defect the change fixes (F0) is the second shipping of a known pattern. The review then found three MEDIUM and eight LOW items, all fixed or documented in the same change except three follow-ups.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F0 | HIGH (the issue) | Both Uncapturable Heroes seams meant "the player" as `== Hero.MainHero`. On the Coop server that is the server's own hero, so a defeated player who is Sauron or a Nazgûl was made a fugitive (which can destroy his party) and Coop's captivity flow ran only half. | co-op identity | The lesson of 2026-08-03 ("`Hero.MainHero` EXISTS but is nobody's character") was written for Special Resources; nothing applied it to other features' existing comparisons. | Fixed through `IPlayerControlQuery`. Audit of the other comparisons: #1141. |
| F1 | MED | The new doc section and the issue said two players of the same side cannot go to war. Coop's hostile demand calls `DeclareWarAction` (which TAOM refuses) and then sets the war stance itself with `VillageHostileFactionStanceHelper.ApplyWarStance`, so any two players in different factions can fight. | doc claim | The orchestrator relayed an audit agent's claim, then corrected it by reading TAOM's veto only. A veto proves what TAOM refuses, not what another mod can still write. | Doc and issue corrected; the enforcement question is #1140. Lesson in `campaign-mechanics.md`. |
| F2 | MED | Four of the six new tests passed with the fix reverted. Without a game `Hero.MainHero` throws, and each seam's catch defers, so "the seam deferred" was the default outcome. | test strength | The tests asserted the outcome of a fail-open seam, and no mutation run was made before the review. | Both deferral tests now assert `_players.Received(1).OfHero(hero)`; a mutation run (old comparison put back) failed all 6 tests. Repeat of the "prove the gate with a mutation" lesson (#1086): lesson in `testing-qa.md`. |
| F3 | MED | The new test class lacked `[TestCategory("RequiresGame")]` although `NamedHero` runs the engine's `StringId` setter. | test hygiene | The `NamedHero` helper was copied from `CoopUpgradeChargePatchTests` without its class attribute. | Added. The rule is `.claude/rules/tests.md`; one-off. |
| F4 | LOW | The docs described `Unknown` per hero; the registry answers it for every hero at once, so such a session protects no lore hero. | doc precision | Read as a per-call value, not a per-session state. | Docs and the `Unknown` table row in `coop-interop.md` now say so. |
| F5 | LOW | The doc understated the old symptom: `MakeHeroFugitiveAction.ApplyInternal` destroys a party its fugitive leads. | doc precision | The engine action was not read. | Doc corrected. |
| F6 | LOW | "A player of an uncapturable race" and "Coop's flow never ran" were imprecise: only Sauron's race is on the race list (the Nine come by hero set), and the besieger-camp clear still ran. | doc precision | Paraphrase of the config instead of a read. | Doc corrected. |
| F7 | LOW | `uncapturable-heroes.md`, the patch registry row and the ReviewedSafe reason still described the player guard as `MainHero` only. | stale text | The change edited the new paragraph only. | Updated in the same change. |
| F8 | LOW | The escape message's "player saw it" test reads the local `Hero.MainHero`, so on the server a guest never sees it. | pre-existing | Outside the diff's purpose. | Documented as a known limit. FOLLOW-UP no issue: the message is cosmetic, and the server shows nobody any message. |
| F9 | LOW | The helper's null-query fallback could never change an outcome. | simplification | Written defensively. | Simplified to `players == null \|\| OfHero(hero) != NotPlayer`. |
| F10 | LOW | The doc named towns and villages for `LocationPvpBlockPatch`; it covers castle courtyards too and any blow to or from another player's agent. | doc precision | Summary from the patch's first line. | Doc corrected. |
| F11 | LOW | The `MapEvent.cs` and `Hero.cs` line numbers in both patch comments are from an older engine (v1.5.7: `:1816`, `:1825`, `:1835`, `:1846-1849`; `Hero.cs:2077-2080`). | pre-existing | Comment-only drift after engine bumps. | FOLLOW-UP no issue: comment line numbers only, the behaviour they describe is unchanged; refresh them when the file is next edited for behaviour. |

## Root-cause pattern

F0 and F1 share one theme: a check of TAOM's own code answered a question about the co-op world. F0 read "the player" from the local process; F1 read "who can go to war" from TAOM's veto alone. In both, the answer depends on what Coop does on its server, and only Coop's code shows that.

## Why each agent missed these

- **Lens 5 Data flow** found F2, F3, F4, F6, F7, F8, F9 and F11. It did not find F1 because it checked the veto's own wiring (correct) and did not follow Coop's hostile-demand path.
- **Lens 2 Engine compatibility** found F1, F5, F10 and F11, and confirmed the reorder in `IsPlayerCapturing` is behaviour-identical. It did not find F2 because test strength is outside its lens.
- **The pre-commit audit agent** found F0 and stated the F1 claim; the orchestrator relayed the claim without reading Coop's war path.

## Feedback memories to codify

None: both lessons go to the repo's lessons files.
