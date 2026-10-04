Plan 032: make the two TAOM patches that run on the agent-tick worker threads cheap and thread-safe:
Patch30 (MixedFormations, Formation.GetOrderPositionOfUnit) and the Patch93 wield-index getter guards.
Behaviour-preserving for every formation and agent.

FACTS (audit 2026-10-02 at dffdf879; the writer re-reads each and quotes current code):
- Call rate, verified in the v1.5.3 dump: Formation.GetOrderPositionOfUnit is reached from
  Agent.TickParallel -> ParallelUpdateCachedAndFormationValuesForAIAgent -> HumanAIComponent.ParallelUpdateFormationMovement
  -> Agent.GetBaseFormationFrame (E:/Decompiled_Bannerlord/_shipping_build/TaleWorlds.MountAndBlade.cs:12700-12705,
  :14381-14389) behind a 0.45 + rand*0.1 s timer per agent (:12240; Timer.Check in TaleWorlds.Core.cs:21628-21650),
  so about twice a second per AI formation agent on TWParallel workers, plus bursts of one call per unit
  from Formation.Tick pending positions (:72686-72693), OnMassUnitTransferEnd (:72133-72152) and the
  player's order-drag preview (:33149, main thread). The "up to 40,000x per frame" comments in
  Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs:13-14 and :28 and the
  registry text are wrong: correct them.
- Patch30's prefix, per call: IsFieldBattle gate (:28); `new FormationAdapter` per call (:41); the service
  call (:46); for formations with a layout, scene.GetGroundHeightAtPosition (:54) and
  mission.IsFormationUnitPositionAvailable (:68), two native queries, where vanilla's Hold path reads a
  cached slot first (Formation.GetOrderPositionOfUnitAux :71521-71527).
  FormationLayoutService.cs:68 reads settings (plan 031 caches the provider; this plan only consumes it);
  :79-93 takes one global lock(_lock) for every holding non-cavalry unit even when the formation has no
  layout. FormationAdapter.cs:40-41 reads QuerySystem.IsCavalryFormation, which goes through QueryData.Value
  (:43190, :43786-43804) and re-evaluates per-class unit counts (:43426-43437) on the calling worker thread
  when its 5 s value expires, with no lock; vanilla offers ReadOnly accessors for exactly this case
  (:43104-43148). On a cache miss, LayoutPositioner.cs:32-34 runs LINQ OrderBy/Where/ToList inside the lock.
- harmony-patches.md thread table: GetOrderPositionOfUnit runs on the TWParallel worker pool; a prefix there
  may only read state prepared on the main thread, must not allocate much, must not log, and its shared
  state takes a lock or is an immutable snapshot swapped by reference. FormationLayoutService is named
  there as the lock-holding shape. MixedFormations is on by default (TaomSettings.cs:663), field battles
  only; the expensive branch applies to the player team's mixed formations in Hold.
- PatchShield wraps this target with a finalizer that binds __originalMethod (plan 034 owns that); do not
  change PatchShield here.
- Patch93: Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs:106-146, prefixes on
  Agent.GetPrimaryWieldedItemIndex, GetOffhandWieldedItemIndex and GetMissileRange, applied for all battles
  at ProcessLoad (CreatureBanditsModule.cs:25), excluded from PatchShield. Each calls CreatureBanditAgents.Is
  (CreatureBanditAgents.cs:18-23), which reads Character.StringId first and then agent.IsHuman and
  agent.RiderAgent before CreatureBanditRules.IsCreatureBandit decides (CreatureBanditRules.cs:54-55); the
  originals are trivial pointer reads (AgentHelper, :13336-13344) or one native call. These getters are
  called from dozens of engine sites on every thread (Agent.WieldedWeapon :11452-11463 among them).

CHANGES:
1. Patch30: a lock-free fast path. Formations with no layout (the common case) must not allocate, lock or
   read QueryData: keep the set of formations that have a layout as an immutable snapshot swapped by
   reference on the main thread (the writes happen where layouts are assigned or cleared; find them), and
   return to vanilla at once when the formation is not in it. Use the ReadOnly query accessors from
   worker threads (verify the exact member names with taom-src). Reuse a per-formation adapter instead of
   allocating per call, or pass the formation directly if the service can take what it needs without a
   sealed type (ADR-007: services take adapters; an adapter cached per formation is the shape). Leave the
   two native queries for mixed layouts unless an exactly-equivalent cached slot exists in vanilla's own
   data; a cache that could return a stale slot is a behaviour change (FOR-MIKE).
2. Patch93: reorder CreatureBanditAgents.Is so the cheapest discriminator that rules out a creature
   comes first (a human with no creature monster can be ruled out without reading StringId; verify which
   flag is cheapest and that the result is identical for every agent kind: human, mount, creature bandit,
   creature with rider). Keep it allocation-free.
3. Correct the wrong call-rate comments and docs/reference/harmony-patch-registry.md text.

TESTS: Patch30 service logic with fakes: no layout -> no lock taken and no adapter created (count them
through a seam), with layout -> the same positions as before for each branch; snapshot swap semantics
(a reader during a write sees the old or the new set, never a partial one); the ReadOnly accessor use
pinned by a binding test or an IL rule. Patch93: one test per (agent kind x getter) cell proving the same
decision as the old predicate. Every existing MixedFormations and CreatureBandits test stays green.

STOP conditions to include: vanilla's ReadOnly accessors are missing or differ on v1.5.3; the layout
assignment and clearing points cannot all be found (a snapshot that misses a writer is a correctness bug);
any test showing a different position or wield decision.
