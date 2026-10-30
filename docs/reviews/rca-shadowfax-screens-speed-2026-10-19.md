# RCA: Shadowfax Screens screen open speed (deep review, 2026-10-19)

## Top-line

`/deep-review` of the screen open speed work (#851 clan, #852 inventory and trade, #853 party, #854
quest log, plus a shared base: the `[ScreenOpen]` timer, the "Screen Performance" MCM group and
`ShadowfaxScreensModule`) ran eight lenses in two waves over `f48ef74dc..ddf097ba1` (78 files). No
CRITICAL or HIGH finding and no incompatible engine API. Seven MEDIUM and about twenty LOW findings
were confirmed and fixed in the same session (`59213ad71..bd17aca5b`), and one convergence pass
checked the fixes. The four screens were built in parallel by four builders from one plan; most
findings are divergence between those builders, which repeats the class recorded in
`rca-combat-mechanics-2026-07-02.md`.

Every finding below was confirmed by re-reading the code or the installed v1.5.5 DLLs. The
orchestrator re-checked the load-bearing engine claims itself: the triple party-list build
(`ClanManagementVM.cs:1166/1269/1212`, `ClanPartiesVM.cs:414-428`), the header tail
(`ClanPartiesVM.cs:500-503`), `Concept.All` = `Campaign.Current.Concepts`, and the portrait renderer
rebuilding from the code string (`CharacterImageTextureProvider.OnCreateImageWithId` line 28).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MEDIUM | Five binding tests read vanilla IL without `[TestCategory("RequiresGameIL")]`, and one test class built engine objects without `RequiresGame`, so the no-game gates would fail. Clan and Inventory tagged correctly. | Parallel builder divergence (repeat) | The brief pinned the timer API and the settings provider, not the test conventions. | `agent-teams.md` pre-dispatch checklist now lists conventions; lesson in `build-tooling-workflow.md`. |
| 2 | MEDIUM | Unreadable-setting policy differed: the provider returned the shipped default (on); Clan and Inventory treated a throw as off; Party as on. | Parallel builder divergence (repeat) | Same as 1. | Provider contract "never throws" on the interface; the services' unreachable catches deleted. |
| 3 | MEDIUM | The quest sort fallback boxed an argument on every call and walked the journal iterator per comparison: about 2.5 to 3.2 ms per sort at 1,000 finished quests, against vanilla's 0.15 ms. | Efficiency | The `object` parameter was chosen because the enum is inaccessible; nobody priced the box on a sort comparer. | `int logIndex` (no box); first and last log stored on the mark. |
| 4 | MEDIUM | The inventory concept memo saved about 0.7 microseconds per row for a transpiler, a memo and about 300 lines. | Simplicity | The plan called it a "clean win" before any measurement; the spec said measure first. | Deleted. Lesson: price a per-call cache with a micro-benchmark before building it. |
| 5 | MEDIUM | The party item-category count cache saved 0.1 to 1 ms per open for two hooks and a window protocol. | Simplicity | Same as 4. | Deleted. |
| 6 | MEDIUM | The feature-map row still said "no Harmony patch, no MCM, Always on"; the four issue bodies described a design that was not built. | Docs | The row and the bodies were written before the build; no gate compares them. | Rewritten. |
| 7 | MEDIUM | `patch-targets.md` was not regenerated for the 17 new hooks. | Docs | The snapshot README asks for it; no builder owned the shared file. | Regenerated (the generator also drops the backlinks footer; restored by hand). |
| 8 | LOW | Comment, registry and doc claimed Refuge's postfix ran "only for a build that runs". Harmony 2.4.2 runs postfixes even when a prefix returns false. Safe only because Refuge dedups. | Harmony semantics | The builder reasoned from the prefix's intent, not Harmony's emitted IL. | The clan skip became a call swap inside `RefreshValues`, so postfixes run only for real builds; lesson in `harmony-il.md`. |
| 9 | LOW | The quest log checked its four guard patches once, at game start; PatchShield can strip one later, leaving finished quests empty. | Startup fact vs runtime | `HookHealth`'s rule ("the install is a startup fact; Harmony patches are not") was not read. | Re-check on every open. |
| 10 | LOW | The inventory open latch closed after an adapter read inside one try. | State transition order | Latent: the adapter caught everything. | Adapter deleted; latch closes first. |
| 11 | LOW | Placeholder category strings repeated in 15+ places. | Parallel builder divergence | Not pinned. | One constant per screen. |
| 12 | LOW | Doc said row order is unchanged; `List<T>.Sort` is unstable, so fully tied rows can reorder (57 such name groups in the live Armory). | Engine claim | Stability assumed. | Doc corrected. |
| 13 | LOW | The four `[ScreenOpen]` lines timed different spans (two included the movie load). | Parallel builder divergence | Not pinned. | All four time the view model build. |
| 14 | LOW | Docs: party session end, `CreateFrom` attribution, which rows walk the roster, the clan headers "bare". | Engine claims | Written from the plan, not the decompile. | Corrected. |
| 15 | LOW | Missing reflection-site rows (`SetSelectedItem`; `_gauntletLayer` and `Concept.All` until deleted). | Catalogue | No builder owned the shared catalogue. | Row added. |
| 16 | LOW | Merged module broke two wiring tests; one cleanup left a facade wired. | Parallel builder divergence | Four copies of one wiring setup. | One wiring test class with a shared setup and a leak assertion. |
| 17 | LOW | Several builders did not watch a RED run before the code. | TDD | Batch-written tests and code. | Mutations run in review; noted. |

## Root-cause pattern: conventions are shared sub-problems

Findings 1, 2, 11, 13 and 16 share one cause. The plan pinned the shared API (timer, settings
provider, module, MCM group), and each builder was correct against it, but nothing pinned how the
siblings do the same thing: test tags, failure policy, category constants, the timing span, the
wiring tests. Per-file review cannot see this; the design lens found it by comparing the four
screens. This is the second occurrence of the parallel-builder seam class
(`rca-combat-mechanics-2026-07-02.md` findings 1 and 6), so the preventive action changes the
pre-dispatch checklist itself, not only the lessons file.

## Why each lens caught or missed these

- **Standards:** found 1, 11, the feature-map row and the doc gaps; did not price runtime cost.
- **Engine compatibility (A, B):** found 8, 12, 14 and 15 from the installed IL and Harmony's
  `MethodCreator`; confirmed every signature.
- **Data flow (A, B):** found 9 and the inconsistent fallback policy; traced every caller of the
  skipped and deferred methods and found no data gap.
- **Completeness:** found 6, 7 and the missing removal checklists.
- **Efficiency:** measured 3, 4 and 5 on the game's runtime.
- **Design:** found the convention divergence (2, 11, 13, 16) and proposed the call swap behind 8.

## Feedback memories to codify

None. The lessons go to `docs/reviews/lessons/build-tooling-workflow.md` and `harmony-il.md`, and the
checklist change to `docs/ai-includes/agent-teams.md`.
