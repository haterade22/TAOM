# RCA: review of the orc battle voice

The orc battle voice (994b837c1, branch `fix/orc-battle-voice`) gives male adult agents of the plain `orc` race the
`uruk_01` voice through one `MissionLogic` that sets it in `OnAgentBuild`. The review ran at tier 1 (lens 2, engine
compatibility, and lens 5, data flow) and found no CRITICAL and no HIGH item. The fixes below are new commits on the same
branch. No game run has happened yet: the voice is checked only through the name read back from the agent.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Vanilla rebuilds a banner bearer's skin in place: `BannerBearerLogic.UpdateAgent` calls `Agent.UpdateSpawnEquipmentAndRefreshVisuals`, which runs `AddSkinMeshes` again and raises no new `OnAgentBuild` (`BannerBearerLogic.cs:733-756`). The native re-pick of the skin voice silently reverts the orc, and TAOM's banner bearer feature triggers the rebuild in nearly every field battle (lens 2) | Visual set once, rebuilt later | The builder read the spawn path (`Mission.SpawnAgent`, `BuildAgent`) and not the other callers of `AddSkinMeshes`. The doc noted the refresh as an unknown limit and named no trigger | First fix: the behavior subscribed to `BannerBearerLogic.OnBannerBearerAgentUpdated` in `AfterStart` and ran the idempotent service call on that agent. The maintainer review replaced it with the `IMissionListener` hook (finding M1 below). Three service tests (second apply is a no-op, a reverted agent is set again with no second probe) and one wiring test. New lesson in `adapters-taleworlds-api.md` |
| 2 | LOW | "Male" was only `!IsFemale`, so orc child townsfolk in Misty Mountain Orcs towns would get the adult Uruk voice (lens 5) | Skip rule too wide | The skip rules were written from the soldier case. A townsfolk child has the same race and sex | Fixed: the adapter reads `Agent.Age` (`Agent.cs:1527`, from the body properties) and the service skips below 18. One test. The decompile has no `BecomeAdultAge`, so 18 is the vanilla hero adult age taken from the design brief, not read from a line here |
| 3 | LOW | Wrong comments and docs: the class summary claimed a restore the code does not do; "pitch varies per soldier / stable per soldier" is false for hero agents and Long Defeat / Fords of War soldiers (seed 0); server safety was credited to the `AgentVisuals` null check; the docs said "soldiers" for every male adult orc agent; the XSLT option was dismissed without a reason; the banner bearer trigger was missing from the limits (lens 5) | Doc rot | The text described the intent, not the code path or the callers | Fixed in `OrcBattleVoiceService.cs`, `IOrcBattleVoiceAgent.cs`, `orc-battle-voice.md` and `coop-interop.md`. Server safety comes from the ADR-013 gate (no TAOM feature behaviors on a dedicated server) |
| 4a | INFO | The pitch could come from the face key (`FaceGenerationParams.VoicePitch` through `MBBodyProperties.GetParamsFromKey`) and not from the seed, which is 0 for heroes | Alternative design | Out of scope for the fix | FOLLOW-UP no issue: owner asked for PRs only; options listed for haterade in the PR body |
| 4b | INFO | A data-only XSLT over the orc adult male `voice_types` (keep 7 entries) would avoid the native unknowns. It rewrites haterade's Armory data at load, which needs his OK | Alternative design | A design choice that belongs to the data owner | FOLLOW-UP no issue: owner asked for PRs only; options listed for haterade in the PR body |
| 4c | INFO | Co-op: another player's orc hero gets `uruk_01` on the other peers too, since each peer sets the voice on the agents it spawns | Co-op behaviour | Presentation only; no state differs | FOLLOW-UP no issue: owner asked for PRs only; options listed for haterade in the PR body |

## Root-cause pattern

Finding 1 is the one that matters: a per-agent visual set at spawn assumes the spawn is the last time the visuals are built.
The engine has other callers that rebuild the skin in place, and one of them is a feature TAOM turns on in every field battle.
Findings 2 and 3 share a second cause: the rules and the text were written from the case in mind (a soldier at spawn) and not
from every agent the hook sees (any mission, any age, any seed).

## Why each lens caught or missed these

- Lens 2 found 1 by following `AddSkinMeshes` back to its callers in the decompile, not forward from `SpawnAgent`.
- Lens 5 found 2 and 3 by tracing the fields the service reads (`IsFemale`, the seed) to what they stand for on each agent kind.
- The first review pass did not name a trigger for the rebuild: the only note was "whether native picks the skin voice again is not known".

## Maintainer review (2026-11-16)

A maintainer review, checked against the v1.5.7 decompile and the native DLL by two reviewers, found these. None is
above LOW; the fixes are in the same PR.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| M1 | LOW | Native `add_skin_meshes` re-picks the skin voice on every in-place rebuild, and the banner bearer is not the only one. The hideout ambush stealth equipment, `MissionSingleplayerViewHandler.OnInventoryScreenDone`, `GauntletBodyGeneratorScreen.OnDeactivate` and Coop's `InventoryLogicInterface` and `BarberHandler` all rebuild agents and undo the orc voice. Every path ends in `Agent.EquipItemsFromSpawnEquipment` (`Agent.cs:4594`, `4599`), which reaches each `IMissionListener` (`Mission.cs:3267-3272`) | Visual set once, rebuilt later | The first fix hooked the one caller the review named, not the engine call every caller shares | Fixed: the behavior implements `IMissionListener`, registers in `AfterStart`, removes itself at the mission end and runs the idempotent apply from `OnEquipItemsFromSpawnEquipment`. `OnAgentBuild` stays, because the listener fires before `InitializeAgentRecord`. The banner bearer lookup is gone. Lesson updated |
| M2 | LOW | `OrcBattleVoiceAgent.HasVisuals` tested `AgentVisuals != null`, which never filters: the native voice getter names a definition only while the visuals are valid | Wrong guard | The null check looked like the guard and no test pinned what the native getter needs | Fixed: `_agent.AgentVisuals?.IsValid() == true`. One wiring test |
| M3 | LOW | The service passed key `""` to `IOutcomeLog.Fail` for every reason, and `IOutcomeLog` writes one line per (id, result, key) per process, so only the first Fail reason was ever written | Dedup key | The log's per-key dedup was not read when the call was written | Fixed: the reason is the key (the `CastleRecruitmentService` pattern). One test checks that two different reasons each reach the log with their own key |
| M4 | LOW | Docs said native re-picking the voice was "not known" or "inferred", used "soldiers" and "in battle" for every male adult orc AI agent in any mission, cited the wrong lines (`InitializeAgentRecord` is `Mission.cs:4054`, the `OnAgentBuild` loop `4389-4392`), and claimed the pitch is kept across peers | Doc rot | The text described intent, not the decompile or the co-op spawn path | Fixed in `orc-battle-voice.md`, `kingdom-voices.md`, `coop-interop.md`, `feature-map.md`, `INDEX.md` and the code comments: the re-pick is shown by the native decompile and only the listening check is owed |
| M5 | LOW | Co-op client pitch is always the lowest: Coop builds puppets with `new AgentBuildData(character)`, which sets `AgentEquipmentSeed` to 0, so the seed-derived pitch is 0 on every client orc | Co-op behaviour | The doc assumed the seed is synced | Not fixed. FOLLOW-UP no issue: derive the pitch from the synced face key if players notice. The docs now say the pitch is not kept |
