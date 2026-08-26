# Kingdom Voices

## Overview

Per-race combat voice sets: the grunts, pain cries, death screams, war cries and shouted formation
orders an agent emits in battle. TAOM ships three custom voice definitions (`dwarf_01`, `uruk_01`,
`uruk_hai_01`) bound to seven of the fourteen custom races, plus 436 audio files under
`Main/_Module/ModuleSounds/`.

This doc is a **system reference written from research, not from an implementation**. No TAOM C#
touches the voice API today; everything below is data wiring that already ships. It exists because
reconstructing how voice binding works took two research passes against the decompile and the
installed modules, and nothing in the repo captured it.

## Why This Exists

- **Vanilla behavior:** 24 voice definitions (`male_01` through `male_08`, `female_01` through `female_05`, and the
  animals), all pointing at compiled FMOD bank events. Human agents draw from their skin's voice
  pool; animals draw by monster sound class.
- **TAOM requirement:** dwarves, Uruk-hai, orcs, elves and trolls cannot all sound like Calradian
  levies. Middle-earth reads wrong the moment an Uruk grunts like an Imperial recruit.
- **Without this feature:** every race falls through to `male_02` through `male_08`, which is still the
  live state for seven of the fourteen races (see the binding table).

## Architecture

### Design Challenge

Three constraints shape everything here.

**1. Voice binds to race, not culture.** Human agents select a voice through
`BodyProperties.CurrentVoice`, an integer index into the `<voice_types>` list on their `<skin>`,
clamped by `MBBodyProperties.GetParamsMax(race, gender, age)`. Non-humans select through the
monster's `sound_and_collision_info_class`. Neither path consults `CultureObject`. **Gondor, Rohan
and Dol Amroth all use the vanilla human race, so no data-only change can give them separate
voices.** That needs C# (route C below).

**2. The middleware is FMOD Studio, not Wwise.** Confirmed by `fmod_output_controller.dll` in the
shipping binaries and the FMOD thread names inside `TaleWorlds.Native.dll` (zero hits for "Wwise").
Vanilla voice definitions therefore reference bank events (`path="event:/voice/combat/male/01/grunt"`)
that require FMOD Studio to author.

**3. TAOM sidesteps the bank requirement.** Our voice definitions put a `module_sounds.xml` **name**
in the `path` attribute (`path="LOTR/Dwarf/Grunts"`) instead of an FMOD event. That resolves to a
loose `.wav` or `.mp3` on disk. This is an officially supported path, documented in Native's own
`module_sounds.xml` header comment, and it is the single fact that makes voice authoring tractable
here. **No bank authoring is required.**

### Solution Approach

Pure data, no code. Registration happens through `ModuleData/project.mbproj`, **not**
`SubModule.xml` and not directory auto-discovery. `MBObjectManager.GetMergedXmlForNative` walks each
module's mbproj entries and merges by `id`, so `soln_voice_definitions` files from every loaded
module are unioned. `Module.CreateProcessedVoiceDefinitionsXMLForNative` merges
`<voice_type_declarations>` across modules and merges `<voice_definition>` elements sharing the same
`name`.

**Missing files are silently tolerated** (`GetMergedXmlForNative` substitutes an empty placeholder),
which is why a dangling mbproj entry produces no error of any kind.

### Component Diagram

```
ModuleSounds/LOTR/<Race>/Voice/*.wav      (loose audio, no FMOD bank)
        |
  module_sounds.xml   <module_sound name="LOTR/Dwarf/Grunts" sound_category="mission_voice">
        |                 <variation path="D1_Grunts1.wav" weight="1"/>
        |
  lotr_<race>_voice_def.xml   <voice type="Grunt" path="LOTR/Dwarf/Grunts" face_anim="grunt"/>
        |
  LOTRLOME_Armory/skins.xml   <race id="dwarf"><skin><voice_types><voice_type name="dwarf_01"/>
        |
  BodyProperties.CurrentVoice indexes the pool  ->  Agent.MakeVoice(SkinVoiceType, prediction)
```

## Current State (verified 2026-08-12)

### Race to voice binding

Extracted from the live `LOTRLOME_Armory/ModuleData/skins.xml`, re-measured 2026-08-25.

**Read the maturity row, not the race.** Each `<race>` holds ten `<skin>` elements, one per gender
and maturity (adult, teenager, tween, child, toddler), and the `<voice_types>` pool is per skin, not
per race. Only the two **adult** skins ever spawn as troops, so those are the only rows that decide
what a battle sounds like.

| Race | Adult male pool | Adult female pool |
|------|-----------------|-------------------|
| `dwarf`, `uruk`, `pale_uruk`, `dg_uruk`, `goblin` | custom only | custom only |
| `uruk_hai` | `uruk_hai_01` only | vanilla `female_01` to `_05` |
| `berserker` | `uruk_01` only | vanilla `female_01` to `_05` |
| `orc`, `nazghul`, `elf`, `cave_troll`, `hill_troll`, `saruman`, `sauron` | vanilla `male_02` to `_08` | vanilla `female_01` to `_05` |

**Every bound race is 100% custom on the adult male skin.** There is no dilution on any troop line.

### Correction, 2026-08-25

This section previously reported a dilution defect: `dwarf` hitting `dwarf_01` "about 1 in 7",
`uruk_hai` and `berserker` "about 1 in 11". **That was wrong, and it was wrong in a specific way
worth remembering.** The original count summed every `<voice_type>` inside the `<race>` element
without noticing that the element contains ten skins. The vanilla entries it counted live on the
teenager, tween, child and toddler skins, which never field a soldier. Both adult dwarf skins are
`dwarf_01` alone.

The one real gap the corrected table shows is narrower and different: `uruk_hai` and `berserker`
have no custom voice on their **adult female** skin. That is an unbound skin, not a diluted one.

**The trap that caused the miscount is still live.** `orc`, `nazghul` and `saruman` write the
attribute as `mesh_maturity_type ="adult"`, with a space before the `=`. A `grep` for
`mesh_maturity_type="adult"` silently skips those three races and returns a confident partial answer.
Match on `mesh_maturity_type\s*=\s*"adult"`. This is the same class of defect as the multiline
attribute gotcha recorded under Gotchas below.

### Assets and definitions

| Artifact | Count |
|----------|-------|
| Audio files under `Main/_Module/ModuleSounds/` | 436 (342 `.wav`, 93 `.mp3`, 1 `.ogg`) |
| `<module_sound>` entries in `module_sounds.xml` | 231 |
| Voice slots per TAOM voice definition | 56, all distinct types |
| Distinct audio paths referenced (dwarf / uruk / uruk_hai) | 48 / 35 / 35 |

### Known defects

| Defect | Detail |
|--------|--------|
| **Unbound adult female skins** | `uruk_hai` and `berserker` have a custom voice on the adult male skin and vanilla `female_*` on the adult female one. Narrow, since female troops are rare, but it is a real hole. Corrected 2026-08-25 from an earlier and wrong "diluted skins" reading |
| **Seven unbound races** | `elf`, `orc`, `nazghul`, `cave_troll`, `hill_troll`, `saruman`, `sauron` have no custom voice |
| **Orphaned Théoden set** | 83 clips under `ModuleSounds/LOTR/Rohan/Voice/Theoden` are registered in `module_sounds.xml` and reachable by nothing: no Rohan voice definition exists, and Rohan is the vanilla human race anyway |
| **Dangling mbproj entry** | `project.mbproj` line 9 declares `ModuleData/VoiceDefinitions/LOTR/lotr_warg_voice_def.xml`; that directory does not exist in this repo. Fails silently |
| **`uruk_01` defined twice** | Both here and in `Alliance.Wargs/ModuleData/VoiceDefinitions/LOTR/lotr_uruk_voice_def.xml`. Cross-module merge precedence is undocumented |
| **mp3 of undetermined support** | 93 `.mp3` files under `ModuleSounds/LOTR/Dwarf/`. Native's own comment documents `.ogg` and `.wav` only; whether FMOD accepts mp3 here could not be determined from the shipping-client decompile and needs a runtime test |
| **Asset provenance** | The custom voice library reads as BFME-sourced on filename convention: `Death1_Isengard.wav`, `Grunt1_Isengard.wav`, `D1_Warcry1..5`, `D1_Battlecry1..3`, Théoden's `Advance_Forth and fear no darkness.wav`, Lurtz's `Defend me you cowards.wav`. Covers dwarf, uruk, uruk_hai, Lurtz and Théoden |

**The provenance defect gates the dilution fix.** Undiluting a skin multiplies how often each of its
clips plays, so it should follow asset replacement rather than precede it.

## The three binding routes

| Route | Applies to | Mechanism | Status |
|-------|-----------|-----------|--------|
| **A. `skins.xml` `<voice_types>`** | any race with its own `<race>` entry | add the definition name to the skin's voice pool | **Proven, in use, 7 races bound.** Per-race only. Edits an unversioned external module |
| **B. monster sound class** | orcs, trolls, ents, wargs | distinct `sound_and_collision_info_class` on the `<monster>`, matched on the `<voice_definition>` | Untried here. All three TAOM definitions declare `human`. This is how vanilla does `horse` / `camel` / `bovine`; `Alliance.Wargs` uses it for `warg_01` |
| **C. `MBAgentVisuals.SetVoiceDefinitionIndex`** | anything | force a voice definition on agent spawn from C# | **Only route that can key on culture.** Needs a MissionBehavior |

**Route A is the default.** It already works and needs no code. Every one of the seven unbound races
has its own `<race>` entry, so a voice added there lands on that race alone.

**Route C is the only answer for the Mannish kingdoms**, since they share the vanilla human race.
Its API pair is public but has **zero callers anywhere in the shipping client**, so it is a modder
API with no worked examples. Budget a spike.

```csharp
int n = SkinVoiceManager.GetVoiceDefinitionCountWithMonsterSoundAndCollisionInfoClassName("human");
var indices = new int[n];
SkinVoiceManager.GetVoiceDefinitionListWithMonsterSoundAndCollisionInfoClassName("human", indices);
agentVisuals.SetVoiceDefinitionIndex(indices[k], voicePitch);
```

## Engine reference

### Voice types

62 declared types, and **it is not a C# enum.** `SkinVoiceManager.SkinVoiceType` is a struct whose
`Index` resolves at type-initialization through `MBAPI.IMBVoiceManager.GetVoiceTypeIndex(typeID)`.
Identity is a string against the merged `<voice_type_declarations>` block, so a module can declare
`<voice_type name="OrcHowl"/>` and construct `new SkinVoiceManager.SkinVoiceType("OrcHowl")` in C#
to fire it.

| Group | Types |
|-------|-------|
| Combat | `Grunt`, `Jump`, `Yell`, `Pain`, `Death`, `Stun`, `Fear`, `Climb`, `Focus`, `Debacle`, `Victory`, `HorseStop`, `HorseRally`, `Drown` |
| Identifiers | `Infantry`, `Cavalry`, `Archers`, `HorseArchers`, `Everyone`, `Mixed` |
| Generic orders | `Move`, `Follow`, `Charge`, `Advance`, `FallBack`, `Stop`, `Retreat`, `Mount`, `Dismount`, `FireAtWill`, `HoldFire`, `PickSpears`, `PickDefault`, `FaceEnemy`, `FaceDirection`, `UseSiegeWeapon`, `UseLadders`, `AttackGate`, `CommandDelegate`, `CommandUndelegate` |
| Formation orders | `FormLine`, `FormShieldWall`, `FormLoose`, `FormCircle`, `FormSquare`, `FormSkein`, `FormColumn`, `FormScatter` |
| DLC | `BoardAtWill`, `AvoidBoarding` |
| MP barks | `MpDefend`, `MpAttack`, `MpHelp`, `MpSpot`, `MpThanks`, `MpSorry`, `MpAffirmative`, `MpNegative`, `MpRegroup` |
| Mount | `Idle`, `Neigh`, `Collide` |

### Sound categories

`sound_category` is mandatory on `<module_sound>`, and **sounds with an invalid category are
silently dropped**. Voice-relevant values and their duration caps, from Native's `module_sounds.xml`
header:

| Category | Cap | Use |
|----------|-----|-----|
| `mission_voice_shout` | 8 s | war cries, shouted formation orders |
| `mission_voice` | 4 s | grunts, exertions, pain |
| `mission_voice_trivial` | 4 s | incidental |
| `alert` | 10 s | alert stingers |

### Key engine types

| Type / member | File |
|---------------|------|
| `SkinVoiceManager.SkinVoiceType`, `VoiceType.*`, the two definition-list helpers | `TaleWorlds.MountAndBlade/SkinVoiceManager.cs` |
| `Agent.MakeVoice(SkinVoiceType, CombatVoiceNetworkPredictionType)`, `Agent.GetAgentVoiceDefinition()` | `TaleWorlds.MountAndBlade/Agent.cs` |
| `MBAgentVisuals.SetVoiceDefinitionIndex(int, float)`, `MakeVoice(int, Vec3)` | `TaleWorlds.MountAndBlade/MBAgentVisuals.cs` |
| `Module.CreateProcessedVoiceDefinitionsXMLForNative()` | `TaleWorlds.MountAndBlade/Module.cs` |
| `MBObjectManager.GetMergedXmlForNative(string, out List<string>)` | `TaleWorlds.ObjectSystem/MBObjectManager.cs` |
| `FaceGenerationParams.CurrentVoice` / `VoicePitch` | `TaleWorlds.MountAndBlade/FaceGenerationParams.cs` |
| `Monster.SoundAndCollisionInfoClassName` | `TaleWorlds.Core/Monster.cs` |

`Agent.GetAgentVoiceDefinition()` is the diagnostic that settles any binding question: log it per
spawn across a full battle and the distribution tells you which definition each agent actually drew.

## Dialogue voice-over is a separate system

Do not conflate it with combat barks. Different assets, different registration, different playback.

- Assets: `ModuleData/Languages/VoicedLines/<LANG>/PC/*.ogg`, format OGG Vorbis 48 kHz mono, with a
  Rhubarb-generated lip-sync XML of the same filename beside each clip.
- Playback: `SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover", path, scene, …)` plus
  `AgentVisuals.StartRhubarbRecord`. It bypasses `module_sounds.xml` entirely.
- Selection: `VoiceOverModel.GetSoundPathForCharacter(CharacterObject, VoiceObject)` and
  `GetAccentClass(CultureObject, bool)`.
- Reference implementation: the official `NavalDLC` module.

**This is the one place culture is native**, and it is currently dead for TAOM.
`DefaultVoiceOverModel.GetAccentClass` hard-codes the vanilla cultures (`empire`, `vlandia`,
`sturgia`, `khuzait`, `aserai`, `battania`, plus bandits) and **returns `""` for everything else**,
so no TAOM culture can match a voice-over file. Fixing it is a `VoiceOverModel` override, ordinary
GameModel work per `.claude/rules/gamemodels.md`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/project.mbproj` | Registers the voice definitions and `module_sounds.xml`. **The only registration point**; `SubModule.xml` plays no part |
| `Main/_Module/ModuleData/module_sounds.xml` | 231 `<module_sound>` entries mapping names to loose audio |
| `Main/_Module/ModuleData/lotr_dwarf_voice_def.xml` | 56-slot reference definition, the model to copy |
| `docs/audio/vo-script-dwarves.html` | The written line sheet for `dwarf_01`: what each slot actually says, and which stem feeds which sound group |
| `docs/audio/vo-recording-guide.html`, `docs/audio/khuzdul-lexicon.html` | The actor-facing brief and the dwarvish reference behind that script |
| `Main/_Module/ModuleData/lotr_uruk_voice_def.xml`, `lotr_uruk_hai_voice_def.xml` | The other two definitions |
| `Main/_Module/ModuleSounds/LOTR/<Race>/Voice/` | Audio assets |
| `<game>/Modules/LOTRLOME_Armory/ModuleData/skins.xml` | **External, unversioned.** Owns `soln_skins` and every race-to-voice binding |
| `docs/reference/lotrlome-armory-snapshot/skins.xml` | Repo snapshot, byte-identical to live as of 2026-08-12 (5,678,421 bytes, 45 custom refs) |
| `<game>/Modules/Alliance.Wargs/ModuleData/VoiceDefinitions/LOTR/` | Ships `warg_01` (the route B mount example) and a duplicate `uruk_01` |

## How to add a voice for a race

1. Author the audio into `Main/_Module/ModuleSounds/LOTR/<Race>/Voice/`. Prefer OGG Vorbis.
2. Add a `<module_sound>` per logical group to `module_sounds.xml`, with a valid `sound_category`
   and `<variation>` children so one bark name randomizes across takes.
3. Copy `lotr_dwarf_voice_def.xml` to `lotr_<race>_voice_def.xml`, rename the definition, and repoint
   all 56 `path` attributes at the new module-sound names.
4. Register the new file in `project.mbproj` as `<file id="soln_voice_definitions" … type="voice_definitions" />`.
5. Add `<voice_type name="<race>_01" />` to that race's `<skin>` in **LOTRLOME_Armory's** `skins.xml`,
   and **remove the vanilla entries from that list** or the new voice is diluted.
6. Mirror the edit into `docs/reference/lotrlome-armory-snapshot/skins.xml`.
7. Field-battle smoke. A voice that fails to bind is silent, not an error.

**Step 5 is the trap.** `skins.xml` lives in an unversioned dependency module, so a reinstall
silently reverts it. Land a repo-side validator gate alongside the edit, per the dependency-module
trap in CLAUDE.md.

## Generation facts (measured 2026-08-13, not inferred)

Every line below was established by running it. They cost a session to work out and none of them are
guessable from the docs.

**Audio tags perform the direction, but only on `eleven_v3`, and you must ask for it by name.**
`[shouts] Form the line! [grunts] Hold. [groans in pain] Fall back.` renders as an actual shout, an
actual grunt and an actual pained groan. The MCP tool's `model_id` defaults to
`eleven_multilingual_v2`, which does **not** parse tags and reads them aloud as the words "shouts"
and "grunts". Always pass `model_id="eleven_v3"` explicitly.

**Voice design previews never parse tags**, whatever the model. `text_to_voice` is a different path
from `text_to_speech`. A design prompt has to be written the way it should sound (capitals,
exclamation, no brackets) and the delivery specified in the voice description. Putting tags in a
design line just gets them spoken.

**Tags beat onomatopoeia for the wordless slots.** Bare `Hnngh! Aagh! Nnaargh!` was tested as a
hedge and rejected on listening; the model reads it as literal syllables. `[grunts with effort] Hnn!`
gives the model something to act on. So the 8 wordless groups need no separate tool, and
`text_to_sound_effects` stays unused.

**Preview voice IDs cannot render.** `text_to_speech` with a `generated_voice_id` returns
`voice_not_found`. A designed voice must be saved with `create_voice_from_preview` first, so a voice
cannot be auditioned against the real workload without committing a slot.

**Two budgets, metering different things.** `text_to_voice` charges the prompt text **once** and
returns three variations (measured: 224 characters of text billed 227). Design iteration is
therefore nearly free, roughly a thousand rounds inside a Creator allowance. `create_voice_from_preview`
is what costs a voice slot **and** one of the 95 add-edits. A rejected voice can be deleted to
reclaim its slot, but the add-edit is spent, so **95 add-edits is the real ceiling, not 30 slots.**

**Prompt clause order matters.** "Refined English, near RP" placed mid-prompt drifted Scottish;
whatever leads the description is weighted hardest. Lead with the dimension you care most about.

**Say the vocal range explicitly.** An unqualified prompt produced thin, nasal results because it
asked for a "tenor". "Deep resonant baritone, heavy chest resonance" fixed it. Accent left
unspecified defaults to General American, which reads wrong for elves.

**`stability` is an expressiveness dial, inverted.** Lower means broader emotional range. 0.3 works
for shouted and non-verbal barks; 0.4 was noticeably flat.

**The MCP tool cannot name its output files.** It takes `output_directory` only and derives the
filename from the text, producing things like `tts_[shou_20260813_072539.mp3`, brackets included.
A literal `[` in the name also means any later PowerShell handling needs `-LiteralPath`, since
brackets are wildcard syntax. Rename immediately after each render, while you still know which slot
the file belongs to; a folder of `tts_[shou_*` files is unidentifiable after three rounds.

## Designed voices

| Name | Voice ID | Register | Status |
|------|----------|----------|--------|
| `elf_01` | `mdsX3iXBwidc1aFvNy64` | Battlefield shout. Deep baritone, chest resonance, cultivated Australian, cold and commanding | Superseded on listening |
| `Elf 02` | `2c9GjBi4HksB8nj0lX3u` | Author-designed | **Preferred.** Best on the tag test |

`elf_01`'s prompt, kept because it is the reproducible artifact if the library entry is ever lost:

> Ancient elven lord roaring orders across a battlefield. Deep resonant male baritone, dark and
> heavy, with powerful chest resonance and low weight beneath every word. Cultivated educated
> Australian accent, precise and slightly archaic diction. Cold, commanding, strained with effort.
> Never nasal, never thin, never bright. This is a shout torn from the chest, not the throat.

Australian was chosen on listening after trialling near-RP and General American against the same
text. Note this is a **register** arrived at by description. Prompts naming a real performer are out:
ElevenLabs' use policy prohibits impersonating a real person, and the whole point of Voice Design
here is that it invents a voice rather than copying one.

**Shouting and speaking are different instruments.** A voice tuned for `Charge!` is the wrong one
for dialogue voice-over, and vice versa. 40 of the 48 groups are shouted, so the combat voice should
be optimised for shouting; a composed variant is a second saved voice, not a compromise on one.

## Authoring tooling: MCP for design, script for render

Two tools, split by phase, because they fail in opposite directions.

| Phase | Tool | Why |
|-------|------|-----|
| **Voice design** (write a prompt, listen, adjust) | `elevenlabs` MCP server | Dozens of calls, a judgment call at each one. Interactive by nature |
| **Batch render** (every slot, every race) | `tools/generate_voices.py` (planned) | Thousands of clips. Needs a manifest, resume after failure, and reproducibility |

**The voice ID is the handoff.** Design produces it, the manifest records it, the generator consumes
it. Never run the batch through MCP: a thousand clips is a thousand model round trips, with no
record of what produced what and no way to resume from clip 700.

### MCP server config (`.mcp.json`)

```json
"elevenlabs": {
  "type": "stdio",
  "command": "uvx",
  "args": ["elevenlabs-mcp"],
  "env": {
    "ELEVENLABS_API_KEY": "${ELEVENLABS_API_KEY}",
    "ELEVENLABS_MCP_BASE_PATH": "${CLAUDE_PROJECT_DIR:-E:/repos/TAOM}/.voice-scratch",
    "ELEVENLABS_MCP_OUTPUT_MODE": "files"
  }
}
```

- **The key is a `${VAR}` reference.** `.mcp.json` is tracked; a literal key would be committed.
- **`ELEVENLABS_MCP_BASE_PATH` is a sandbox.** The server rejects any path resolving outside it, so
  pointing it at the gitignored `.voice-scratch/` means auditions cannot land in `ModuleSounds/`.
  Promotion from audition to asset is a deliberate copy, never a side effect of a generate call.
- Requires `uvx elevenlabs-mcp` installed and `ELEVENLABS_API_KEY` set in the environment. Both live
  outside the repo.
- After any `.mcp.json` edit, run `python tools/audit_claude_config.py` (`/security-scan`).

### Audio format

**ElevenLabs cannot emit OGG at any tier**, so an ffmpeg step is mandatory. Output formats are mp3
(default `mp3_44100_128`), PCM, and WAV, and the 44.1 kHz PCM/WAV options need Pro tier or above.

That makes the subscription tier a quality decision, not only a volume one: below Pro the pipeline
transcodes mp3 into ogg, lossy into lossy. Likely inaudible on a four-second combat grunt, more
questionable for dialogue voice-over, which additionally wants OGG Vorbis 48 kHz mono and so gets
resampled off 44.1 kHz regardless.

## Validation

There is no voice validator today. A check worth building should assert four things:

1. Every `<voice type=… path=X>` resolves to a real `<module_sound name=X>`, and every referenced
   file exists on disk. Catches the orphaned Théoden set and the dangling mbproj entry.
2. Every `<voice_definition>` is named in some skin's `<voice_types>`. Catches a definition that
   ships and binds to nothing.
3. **No skin mixes a custom voice with vanilla `male_*` / `female_*` entries.** Catches the dilution,
   and catches a module reinstall restoring it.
4. Live `LOTRLOME_Armory/skins.xml` still matches the repo snapshot.

Check 3 earns its keep: dilution is invisible in play because the voice does fire sometimes.

## Gotchas

- **ModuleData XML puts attributes on their own lines.** `<voice_type\n  name="dwarf_01" />` defeats
  a `grep 'voice_type name='`, which then reports only the single-line vanilla entries and looks like
  a confident zero. Use a multiline-aware match. This cost a wrong conclusion on 2026-08-12; see
  `docs/reviews/lessons/xslt-moduledata.md`.
- **`<race>` elements do carry `id` attributes** (`dwarf`, `uruk`, `nazghul`, …) in LOTRLOME_Armory's
  `skins.xml`. Race **integers** elsewhere in TAOM are positional indices into the merged race list,
  which is a different thing; see `Main/Features/HeroRace/RacePersistenceService.cs`.
- **A silent failure is the default everywhere in this system.** Missing mbproj file, invalid sound
  category, unbound definition, diluted pool: none of them log. Plan on validators, not on errors.

## Tests

No validator covers the data, and no test covers the voice API. One TAOM feature does call it:
`Main/Features/DreadAura/Hooks/DreadPulseRunner.cs:96` fires `VoiceType.Fear` on a dread pulse, gated
by `DreadAuraConfig.FearVoiceChancePerPulse` (default 0.02). `Fear` is bound only on `dwarf_01`,
`uruk_01` and `uruk_hai_01`, so on any other race that call is silent.

## Changelog

- 2026-08-25: corrected the race-to-voice table against the live `skins.xml`. The dilution defect was
  a miscount across the ten per-maturity skins and does not exist; every bound race is fully custom on
  the adult male skin. Corrected the type count from 68 to 62, recorded `DreadPulseRunner` as a live
  caller of the voice API, and linked the new `docs/audio/` recording scripts.
- 2026-08-12: doc created from a two-pass research sweep. Recorded the race-to-voice binding table,
  the dilution defect, the three binding routes, the FMOD loose-file mechanism, the voice types,
  and the seven known defects.

## GitHub Issue

None filed. The dangling mbproj entry, the unbound adult female skins, and the asset provenance each
warrant one.
