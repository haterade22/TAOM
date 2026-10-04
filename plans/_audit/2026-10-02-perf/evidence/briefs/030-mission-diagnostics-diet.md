Plan 030: cut the per-frame, per-agent and per-hit cost of always-on mission diagnostics while keeping
every line they exist to produce, and delete one dead per-agent store. Behaviour-preserving: the same
diagnostic information reaches the log when the situation it watches occurs.

Every item below comes from a read-only audit on 2026-10-02 (file:line at dffdf879); the writer re-reads
each before planning it, and drops any item whose code does not match (recording why).

A. MissionDiagnostic action-set census (always registered, Main/SubModule.cs:2128-2132, no MCM gate).
   Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs:19 (_actionSetWindowSecondsLeft = 5f),
   :41-45 and :76-91: every frame for the first 5 s of every mission, for every agent in Mission.Agents:
   agent.ActionSet.GetName() (a native string marshal), agent.Name (TextObject.ToString through
   MBTextManager), a race lookup, Character and Monster StringIds; then MissionDiagnosticService.cs:178
   builds an interpolated key (a boxed bool) and :179 HashSet.Add; only the first sighting of a combination
   logs. Change: look at each agent once in the window (identity by reference within the window, never by
   Agent.Index, which recycles), compute a cheap key from values that need no string marshal (the action
   set's index if MBActionSet exposes one, race id, female flag, monster id; verify what the engine
   offers with taom-src) and fetch names and format the line only for a key not yet seen. The logged lines
   must be identical for the same battle (same text, same first-sighting order).
B. Per-hit [CareerPerks] DEBUG lines. Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs:183,
   188, 203 (string concatenation per term) and :207-208, :249-250 (an eager interpolated LogDebug string)
   run on every damage calculation where a career passive changed the number; with the player's
   TroopDamage/TroopResistance pips that is every hit by or on the player's party troops. Added in
   7ede923b (#613) after CareerPassiveService.cs:122-131 had removed the same per-lookup logging because
   "A large battle turned that into thousands of allocations and log lines per minute". FileLogger.LogDebug
   has no level gate (Main/Core/Logging/FileLogger.cs:86-95: it formats DateTime.Now and enqueues). Change:
   log each (hero or troop-leader, passive kind) combination once per mission, as LogOnChange already does
   for stat lines in the same service, and build no string before that check.
C. Troll scans in every mission. Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs:86-88 runs
   TrollFormationSpacingTracker.Tick (O(N) Mission.Agents pass every 0.5 s with a dictionary write per agent,
   TrollFormationSpacingTracker.cs:42-64, and ConcurrentDictionary.Keys at :64, which locks and copies) and
   TrollClipTrace.Tick (O(N) scan every 0.5 s plus per-troll per-frame FindAgentWithIndex and two
   GetCurrentAction, TrollClipTrace.cs:39-56) in every mission, troll or not (registered at
   SubModule.cs:2060). Change: both do nothing until the first troll agent has been built in this mission
   (a flag set from the behaviour's OnAgentBuild with the existing troll predicate); identical behaviour
   once a troll exists. TrollClipTrace is marked temporary (:16-18); deleting it is the maintainer's call,
   not this plan's.
D. Creature-bandit diagnostics in every battle. CreatureBanditDiagnosticsBehavior is added to every
   mission (Main/Features/CreatureBandits/CreatureBanditsModule.cs:33-34, "Temporary diagnostics"); it does
   two ConcurrentDictionary SerialOf lookups on every hit in every battle (CreatureBanditDiagnosticsBehavior.cs:47-49),
   and CreatureBanditDiagTicker.cs:123-170 builds Line(...) strings before CreatureBanditDiag.Write checks
   its line cap (CreatureBanditDiag.cs:92-105). Change: early-out in the hit and removal handlers until a
   creature bandit has spawned in the mission; check the cap (TryTakeLine or equivalent) before building
   any line.
E. An empty per-frame Harmony postfix. Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs:31
   reads EnableFormationPresets from MCM every frame and the body is empty (:33-35); every Mission.OnTick
   therefore pays a Harmony detour, a settings read, and a PatchShield finalizer. Change: delete the class
   (category Patch35_CompanionTactics, applied at SubModule.cs:1816, keeps its other classes) after
   proving nothing reads state it sets (grep), and update docs/reference/harmony-patch-registry.md.
F. A dead store keyed on Agent.Index. Main/Features/BannerColorPersistence/AgentColorStore.cs:8-12 and
   IAgentColorStore.cs:8: TryGetColors has no caller anywhere (grep, tests included), Register is called
   per spawn from Mission_SpawnAgent_Patch.cs:56-78 and Agent_EquipItemsFromSpawnEquipment_Patch.cs:24-36,
   and it is cleared only at mission end (AgentColorStoreCleanupBehavior.cs:13), never on OnAgentDeleted:
   it breaks TAOM's rule never to key on Agent.Index (orientation.md trap index) and holds an entry for
   every agent of the battle. Change: delete the store, its interface, its two Register calls, its cleanup
   behaviour, and its IoC and SubModule registrations (single-owner files: the plan lists the exact edits);
   keep every colour-resolution behaviour the patches perform for the agent itself. If anything reads the
   store, STOP.

ALL ITEMS: TDD per item (a test that fails against the old behaviour where a cost or a dead path can be
observed in a unit test, for example the census fetching names once per unseen key, the career line built
once per combination, the troll trackers doing nothing before a troll is built); the full suite matches
the baseline's failure set; docs updated where a feature doc describes the changed behaviour.

OUT OF SCOPE: changing INFO lines to DEBUG or changing any MCM default (the maintainer decides those: the
spider, troll-smash and signature-hero INFO lines and EnableHowdahDiagnostics go to FOR-MIKE); FileLogger
itself.
