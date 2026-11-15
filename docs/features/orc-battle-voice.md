# Orc Battle Voice

## Overview

Soldiers of the plain `orc` race use TAOM's Uruk battle voice (`uruk_01`) for their grunts, pain cries,
death screams and yells, instead of the vanilla human male voices. The voice is set from C# once per
soldier at spawn. The Armory's `skins.xml` is not changed.

## Why This Exists

- **Vanilla behavior:** a humanoid agent's voice comes from the `<voice_types>` pool of its race's skin
  in `skins.xml`, picked by its body properties
  ([kingdom-voices.md](kingdom-voices.md), "Design Challenge").
- **TAOM requirement:** the adult male skin of race `orc` in the live `LOTRLOME_Armory/ModuleData/skins.xml`
  lists `male_02` to `male_08`, the vanilla human voices. Uruk, goblin and the other Uruk-type races list
  `uruk_01`. Players hear Mordor orcs die with human screams.
- **Without this feature:** the fix needs a `skins.xml` edit in the unversioned Armory module
  ([kingdom-voices.md](kingdom-voices.md) route A), which TAOM does not own, or an XSLT over that file.

## Architecture

### Design Challenge

- **No name lookup.** The engine hands out voice definitions only as opaque native indices
  (`SkinVoiceManager.GetVoiceDefinitionListWithMonsterSoundAndCollisionInfoClassName`). Nothing in the
  managed API turns a definition name such as `uruk_01` into its index.
- **Native picks the human voice.** No managed code sets a humanoid agent's voice: `Agent.MakeVoice` and
  `Agent.GetAgentVoiceDefinition` pass straight to native (v1.5.7 `Agent.cs:4356-4358`, `2932-2934`).
- **The one vanilla setter caller is for mounts.** `MountVisualCreator.SetVoiceDefinition`
  (TaleWorlds.MountAndBlade.View, v1.5.7) gives every mount its voice with
  `mBAgentVisuals.SetVoiceDefinitionIndex(voiceDefinitionIndex, voicePitch);`, from the
  `OnEquipItemsFromSpawnEquipment` listener. That makes the setter a working seam, but not yet proven for a
  humanoid agent, whose voice native code picks from the skin.

### Solution Approach

One `MissionLogic` (`OrcBattleVoiceMissionBehavior`) overrides `OnAgentBuild`, which v1.5.7
`Mission.SpawnAgent` raises after `BuildAgent` (`Mission.cs:4388-4399`). By then `BuildAgent` has run
`EquipItemsFromSpawnEquipment`, whose `AddSkinMeshes` builds the skin from the race and body properties
(`Agent.cs:4594`, `5453-5458`), so the voice set here comes after the engine's own pick.

`OrcBattleVoiceService` handles each agent:

1. Skips every agent that is not humanoid, is female, is player controlled, has no `AgentVisuals`, is not
   race `orc` (through `IRaceManager`), or already reports `uruk_01`.
2. On the first orc of a mission it probes: it sets each `human`-class index on that orc in turn and reads
   the name the agent then reports (`Agent.GetAgentVoiceDefinition`), until `uruk_01` answers. Each later
   orc gets the found index with one call.
3. If no index answers `uruk_01`, the probed orc goes back to the index that reported its original voice.
   If two indices report the same name, the report does not follow the setter (definitions are merged by
   name, so two indices never share one): probing stops there. Either way a `[Check]` Fail line is written
   and the feature stands down for the mission.
4. The pitch is the soldier's `BodyPropertiesSeed` mapped to [0, 1] with vanilla's mount factor
   (`num2 * 4.656613E-10f`), so pitch varies per soldier and is stable across peers.

After each set the service reads the name back. The first match writes a `[Check]` Pass line; the first
mismatch writes a Fail line with the reported name.

### Component Diagram

```
Mission.SpawnAgent -> OnAgentBuild
        |
  OrcBattleVoiceMissionBehavior (MissionLogic, never throws)
        |
  OrcBattleVoiceService (skip rules, one probe per mission, Pass/Fail lines)
       /                     \
IOrcBattleVoiceAgent        IVoiceDefinitionCatalogAdapter
(Agent: race, sex, seed,    (SkinVoiceManager: "human"-class
 GetAgentVoiceDefinition,    definition indices)
 AgentVisuals.SetVoiceDefinitionIndex)
```

## Configuration

None. The race (`orc`), the voice (`uruk_01`) and the sound class (`human`) are constants in
`OrcBattleVoiceService`. There is no MCM toggle.

## Known limits

- **Female orcs** keep the vanilla `female_*` voices of the adult female orc skin: `uruk_01` is a male voice.
- **The player's own orc** keeps the voice chosen in character creation.
- **Trolls** (`cave_troll`, `hill_troll`) are out of scope: no troll voice definition exists yet, although
  the troll clips do (`ModuleSounds/LOTR/Monsters/Troll`).
- **Equipment refresh.** `Agent.UpdateSpawnEquipmentAndRefreshVisuals` runs `AddSkinMeshes` again mid-mission.
  Whether native then picks the skin voice again is not known; an orc whose kit is refreshed may fall back
  to a human voice.
- **Not proven in game.** That the native voice of a humanoid agent follows `SetVoiceDefinitionIndex` is
  inferred from the mount caller and checked only through the name readback; no one has listened yet.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/OrcBattleVoice/OrcBattleVoiceService.cs` | Skip rules, the per-mission probe, Pass and Fail lines |
| `Main/Features/OrcBattleVoice/Hooks/OrcBattleVoiceMissionBehavior.cs` | Thin `MissionLogic`: `OnAgentBuild`, `OnEndMission`, `OnRemoveBehavior` |
| `Main/Features/OrcBattleVoice/OrcBattleVoiceModule.cs` | Feature module: the adapter registration and the mission behavior |
| `Main/Adapters/IOrcBattleVoiceAgent.cs`, `OrcBattleVoiceAgent.cs` | Short-lived agent view: race, sex, seed, voice read and set |
| `Main/Adapters/IVoiceDefinitionCatalogAdapter.cs`, `VoiceDefinitionCatalogAdapter.cs` | Definition indices by sound class |
| `Main/_Module/ModuleData/lotr_uruk_voice_def.xml` | The `uruk_01` definition (`sound_and_collision_info_class="human"`) |

## Dependencies

- `IRaceManager` (Core): race index to race name.
- `IOutcomeLog` (Core): the `[Check]` lines for `taom.orc-battle-voice.apply`.

## Tests

- `TAOM.Tests/Features/OrcBattleVoice/OrcBattleVoiceServiceTests.cs`: 17 tests and 5 data rows covering
  each skip rule, the probe order, reuse of the found index, the restore when `uruk_01` is missing, a
  readback that does not follow the setter, an empty catalog, Pass and Fail lines, and the seed-to-pitch map.
- `TAOM.Tests/Features/OrcBattleVoice/OrcBattleVoiceWiringTests.cs`: 7 tests pinning the module, the
  adapter registration, the co-op role, the entry point size and the engine calls.
- Co-op role: Local (presentation on this peer's own agents), row in [coop-interop.md](coop-interop.md).

## How to verify in game

**Preconditions:** a TAOM build with this change, any save or Custom Battle, sound on. No MCM toggle.

1. Start a field battle (Custom Battle works) against a Mordor army with plain orc troops, for example
   Mordor orc infantry, not Uruk-hai.
2. In the log (`taom_debug_*.log`) look for
   `[Check] id=taom.orc-battle-voice.apply result=pass voice=uruk_01`. A `result=fail` line names the
   reason (`uruk-voice-missing`, `readback-not-following`, `no-human-voices` or `set-not-taken`).
3. Listen: orc soldiers grunt, cry out and die with the Uruk clips, not human screams. Compare with a Gondor
   or Rohan soldier on the same field. Only a person can judge this step.
4. Orc women, if any spawn, still use the vanilla female voices (known limit).
