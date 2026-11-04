# Codex pre-review: Marshal's Word stages M1 and M2, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `1d5d0a561`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

Marshal's Word changes how a player's orders behave in a battle. M1 queues orders: a held key adds a
step, and each company takes its next step when its last one ends. M2 keeps a company's place in a
multi-company array when the player gives one line order, and lets the array arrive together.

The contract is `docs/features/marshals-word/build-spec.md` sections 1 to 6, section 11 and Deviations
rows 1 to 4. Sections 7 to 10 are later stages, so their absence is not a finding. The field trial steps of
section 11 come in a later commit, once the shared harness merges into this branch, so their absence is not
a finding either. Engine source for v1.5.6 is in `C:\Users\mikew\.taom-src\v1.5.6\` (for example
`TaleWorlds.MountAndBlade.OrderController.cs`); `pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a
type that is not cached yet. Mark anything you cannot prove from code as UNVERIFIED.

Files: `Main/Features/MarshalsWord/**`, `Client/Features/MarshalsWord/**`, `Client/Adapters/IMarshalScreenAdapter.cs`,
`Client/Adapters/MarshalScreenAdapter.cs`, `Main/Adapters/IMarshalOrderAdapter.cs`, `MarshalArrayAdapter.cs`,
`MarshalOrderAdapter.cs`, `MarshalOrderPayload.cs`, `TAOM.Tests/Features/MarshalsWord/**`, the Marshal hunks in
`Dependencies/Foundation/PatchShieldPolicy.cs`, `Main/Composition/FeatureModules.cs`,
`Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Main/Features/CoopInterop/Missions/CoopMissionRoles.cs` and
`Main/Features/TaomSettings.cs`, the registry's `## Patch141_MarshalsWord` section, `docs/features/marshals-word.md`
and the Marshal rows in `tools/ingame_checks/0.toml`.

## Questions to answer with evidence

- **Capture.** `MarshalOrderCapturePatch` has six prefixes. Which `OrderController` entry points reach each, and
  which reach none? When a prefix swallows a call, list what vanilla would have run and now does not:
  `BeforeSetOrder`, `OnOrderIssued`, other TAOM postfixes on the same methods, voice, the AI lift. Can a player's
  order be lost with no step and no vanilla effect? Can the queue's own re-issue be captured again?
- **The master controller path.** A queued step is issued later. Which controller issues it, with which selection?
  Does the player's live selection change, even for one frame? Does the `IsAIControlled` lift at
  `OrderController.cs:722-729` fire, and is it right for a company that was handed to the AI after the step was queued?
- **Queue rules.** Walk `MarshalOrderQueue`, `QueueStep`, `StepEnd` and `MarshalQueueDriver` for: the barrier between
  two companies of one step, the re-test of members, patience, arrival, a company that dies, a company with no units,
  a team change, the hero's fall, a direct order, a full queue, a mode change. Can a step fire twice, fire on a company
  that left the step, or wait forever? Does any list get mutated while it is iterated? Is every agent or formation write
  on the mission tick?
- **Array takeover.** Walk the conditions of 6.1 in `MarshalArrayPatch` and `MarshalArrayAdapter`, with the new
  `MarshalOrderableLinePatch` gate. Do the preview call and the commit call of one order reach the same answer? What do the
  `out` parameters hold on each return? Can the solver hand a non-finite position, an inverted width, or a position off
  the navmesh to the engine? Does the finalizer always run, and can the gate stay raised? Name any call path where the
  takeover runs and the contract says it should not.
- **The march.** `MarshalPacePatch` sits on a hot path. Which thread reads its table, and how is the table
  published? What does the patch cost a player who never uses the feature? Could the JIT inline the target, and
  what happens then? Can the pace reach zero, a negative or a non-finite value? Does a soldier stay slowed after the
  order ends, after a new direct order, or after the session ends? Check `GetDesiredSpeedInFormation`'s other
  callers.
- **Statics and teardown.** Walk `MarshalHooks` through `Begin`, `Arm`, `IsOwnedBy` and `Clear` over both teardown paths, a
  session that finishes after the next mission began, and an exception inside a prefix. Can a static leak into the next
  mission? Can one session clear another's hooks? Does a removed view keep ticking on anything it owns?
- **Flags.** Walk the flag pool and `MetaMesh.GetCopy("order_flag_small")`. Is every entity removed on each exit? Can a flag
  be touched after the scene is cleared? Does the pool grow without bound?
- **Interference.** The contract and the feature doc name the formation presets, the battle action bar, smart
  cavalry, the time acceleration patches and Patch31, Patch31b, Patch35 and Patch92. Trace a queued Move through each. Name one
  that behaves differently from a direct order and is not in the doc's limits.
- **Settings, keys and co-op.** Does each setting reach its reader, and does a hot-path one use a lazy accessor? Do the default
  keys collide with a vanilla key that a player uses in a battle? Trace the stand-down in a co-op session: can a
  capture, a takeover or a pace change run there?
- **Text against code.** Does the feature doc, the registry section or a `0.toml` claim state anything the code does not
  do? Can each `[[check]]` row in `0.toml` pass with its feature broken?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
