# Body properties

## What this file is

`TAOM_bodyproperties.xml` holds the face and body presets that troops, notables, wanderers and lords point at instead of each carrying its own face. Every `<BodyProperty>` entry is a **range**, a `<BodyPropertiesMin>` node and a `<BodyPropertiesMax>` node, and the engine rolls one face per spawned soldier somewhere between them. The same entry also carries the hair, beard and tattoo tag lists that decide which meshes that roll may choose from.

## Where it lives and how it is registered

The file is [`Main/_Module/ModuleData/TAOM_bodyproperties.xml`](../../Main/_Module/ModuleData/TAOM_bodyproperties.xml), 30 `<BodyProperty>` entries with no XSLT beside it, so what you type is what loads. <!-- measured: python re.findall over TAOM_bodyproperties.xml, ls TAOM_bodyproperties.xslt 2026-09-05 -->

<!-- excerpt file="Main/_Module/SubModule.xml" -->

```xml
    <!-- TAOM body properties (character appearance templates) -->
    <XmlNode>
      <XmlName id="BodyProperties" path="TAOM_bodyproperties"/>
      <IncludedGameTypes>
        <GameType value="Campaign"/>
        <GameType value="CampaignStoryMode"/>
        <GameType value="CustomGame"/>
      </IncludedGameTypes>
    </XmlNode>
```

`SubModule.xml:166-174`. The `path` carries no extension and no `ModuleData/` prefix. The node lists three game types and not `EditorGame`, unlike its neighbour at `SubModule.xml:156-164`, so these presets are absent in the editor game type.

- **Root element** `<BodyProperties>`, **per-entry element** `<BodyProperty id="...">`, **engine class** `TaleWorlds.Core.MBBodyProperty`.
- The type is registered as element `BodyProperty` inside list `BodyProperties` at `Game.cs:319`, and the file is read in `Game.LoadBasicFiles` at `Game.cs:444`, after Monsters (`:437`) and before SkillSets (`:445`). That is long before any troop file, so a `face_key_template` on a troop always finds a populated preset.
- **Four modules feed one merged list.** TAOM contributes 30 ids, `SandBoxCore/ModuleData/sandboxcore_bodyproperties.xml` 69, `SandBox/ModuleData/sandbox_bodyproperties.xml` 14 and `NavalDLC/ModuleData/naval_bodyproperties.xml` 9, and the merged registry holds 121 because `fighter_sturgia` is defined in both TAOM and SandBoxCore and merges into one preset. Those files live in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. <!-- measured: python re.findall over the four bodyproperties files plus python tools/validate_moduledata.py registry line 2026-09-05 -->
- Neither `TAOM_Map` nor `LOTRLOME_Armory` declares a `BodyProperties` XmlName, so TAOM's file is this project's only preset source. <!-- measured: rg -n BodyProperties over both SubModule.xml files 2026-09-05 -->

**A second, unrelated file uses the same words.** [`Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`](../../Main/_Module/ModuleData/charactercreation/cc_body_properties.xml) has root `<CCBodyProperties>`, is registered under no XmlName at all, and is read by TAOM's own `Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs:43`. It sets the player's starting body per culture and is covered in its own section below.

## Attributes

The entry's own id comes from the base class, so it gets its own citation.

<!-- engine-table type="TaleWorlds.ObjectSystem.MBObjectBase" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none; the read is `node.Attributes["id"].Value` with no null check, so a missing id throws and takes the rest of the file with it | The preset's name. This is what a character points at with `value="BodyProperty.<id>"`. Must be unique across every active module, because a repeated id merges instead of adding a second preset. | `MBObjectBase.cs:61` |

`<BodyPropertiesMin>` and `<BodyPropertiesMax>` take the same four attributes. Three of them are numbers parsed by one method.

<!-- engine-table type="TaleWorlds.Core.BodyProperties" file="Core/TaleWorlds.Core/TaleWorlds.Core/BodyProperties.cs" method="FromXmlNode" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `age` | float | no | `30` (`BodyProperties.cs:59`), and `float.TryParse` turns anything non-numeric into `0` | How old the face looks at this end of the range. It is also an on/off switch: an age of zero or less on one node makes the engine copy the other node over it, which is how you make a preset produce one fixed face. A character with no `age=` of its own takes `max(20, BodyPropertiesMax age)` as its campaign age. | `BodyProperties.cs:62-65`, fixup at `MBBodyProperty.cs:97-104`, age inheritance at `BasicCharacterObject.cs:486` |
| `weight` | float | no | `0.5` (`BodyProperties.cs:60`) | Body fat, 0 gaunt to 1 heavy. Min and Max are the two ends of the roll. | `BodyProperties.cs:66-69` |
| `build` | float | no | `0.5` (`BodyProperties.cs:61`) | Muscle mass, 0 skinny to 1 bulky. Weight and build together are the silhouette; the key handles the face. | `BodyProperties.cs:70-73` |

The fourth is the face key, and it is parsed by a different class.

<!-- engine-table type="TaleWorlds.Core.StaticBodyProperties" file="Core/TaleWorlds.Core/TaleWorlds.Core/StaticBodyProperties.cs" method="FromXmlNode" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `key` | string, exactly 128 hex characters | yes | none; `node.Attributes["key"].Value` is dereferenced with no null check, so an absent key throws and kills the rest of the file | The face itself: head, skin, hair, eyes, nose, height, the whole package, exported from the in-game face editor. Read as eight blocks of 16 hex characters into `KeyPart1..KeyPart8`. Paste it whole. | `StaticBodyProperties.cs:104`, length gate at `:105-109`, the eight blocks at `:110-117` |

`version="4"` is on every shipped node and is read by nothing. It is listed here because you must keep copying it, not because it does anything on the managed side.

<!-- engine-ref type="TaleWorlds.Core.BodyProperties" file="Core/TaleWorlds.Core/TaleWorlds.Core/BodyProperties.cs" lines="155-162" -->

| Attribute | What the engine does with it |
|---|---|
| `version` | No deserializer in the v1.4.8 dump reads an attribute named `version` on a body node. The only managed read of a `version` attribute anywhere in the dump is `HotKeyManager.cs`, a different file format. `BodyProperties.ToString()` always writes `version="4"` at `BodyProperties.cs:159`, which is why every shipped node has it. Keep it; whether the native key decoder branches on a format version is undetermined. <!-- measured: rg -c over the whole v1.4.8 dump for Attributes["version"], GetAttribute("version") and ReadString(..., "version") 2026-09-05 --> |

The tag children each carry one attribute.

<!-- engine-table type="TaleWorlds.Core.MBBodyProperty" file="Core/TaleWorlds.Core/TaleWorlds.Core/MBBodyProperty.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `name` | string | yes on every `<hair_tag>`, `<beard_tag>` and `<tattoo_tag>` | none. The `?.` in `childNode2.Attributes?["name"].Value` guards a missing attribute collection, not a missing `name=`, so a bare `<hair_tag/>` throws and takes the rest of the file with it | One allowed style family. The name is matched against the `<style_tag name="...">` entries on the `<hair_mesh>`, `<beard_mesh>` and `<tattoo_material>` nodes inside that race's `<skin>` blocks in `skins.xml`. List several to widen the pool. | `MBBodyProperty.cs:75`, `:82`, `:93` |

## Child elements

<!-- engine-table type="TaleWorlds.Core.MBBodyProperty" file="Core/TaleWorlds.Core/TaleWorlds.Core/MBBodyProperty.cs" method="Deserialize" -->

| Child element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<BodyPropertiesMin>` | empty element, four attributes | in practice yes | if its age is zero or less the engine copies `BodyPropertiesMax` over it | The low end of the range. Order inside `<BodyProperty>` does not matter; the loop matches on the child's name, not its position. | `MBBodyProperty.cs:63-66`, fixup at `:101-104` |
| `<BodyPropertiesMax>` | empty element, four attributes | in practice yes | if its age is zero or less the engine copies `BodyPropertiesMin` over it | The high end of the range, and the node vanilla character creation writes onto the player when a culture is picked. | `MBBodyProperty.cs:67-70`, fixup at `:97-100` |
| `<hair_tags>` | container of `<hair_tag>` | no | `HairTags` stays `""` (`MBBodyProperty.cs:12`) | Which haircut families the roll may pick from. Append only: names are concatenated with commas and nothing ever clears the string, so a name listed twice is stored twice. | `MBBodyProperty.cs:71-77` |
| `<beard_tags>` | container of `<beard_tag>` | no | `BeardTags` stays `""` (`MBBodyProperty.cs:14`) | Which beard families the roll may pick from. Same append-only behaviour. | `MBBodyProperty.cs:78-85` |
| `<tattoo_tags>` | container of `<tattoo_tag>` | no | `TattooTags` stays `""` (`MBBodyProperty.cs:16`) | Which face-marking families the roll may pick from. Vanilla and TAOM both use `Cleanface` to mean no war paint. This is the last branch of the chain, so any child element the loop does not recognise is skipped in silence. | `MBBodyProperty.cs:87-95` |

A per-character `<face>` block may override the three tag lists but not the range: `BasicCharacterObject.cs:499-520` replaces the tags, cloning the shared preset first at `:506` so the change does not leak onto everyone else using the same template. The key, age, weight and build always come from the template when one is present, because `BasicCharacterObject.cs:472-476` only builds a private preset from inline values when no template was named.

## Worked example

<!-- example file="Main/_Module/ModuleData/TAOM_bodyproperties.xml" id="fighter_gondor" -->

```xml
  <BodyProperty
		id="fighter_gondor">
    <BodyPropertiesMin
			version="4"
			age="25.99"
			weight="0.2009"
			build="0.5502"
			key="0000040800CC030111711340522271751212856A46212521222707117111112102A536240A331224000000000000000000000000000000000000000015541000"  />
    <BodyPropertiesMax
			version="4"
			age="39.5"
			weight="0.2152"
			build="0.5720"
			key="0026EC0D80001000DD79E69067DD7DD7DD9E85D68ED46DDDBEDCC6DDBCD9BCCD02D736240DDEF7EF00000000000000000000000000000000000000003FAC0000"  />
    <hair_tags>
      <hair_tag
				name="TiedAcrossBack" />
      <hair_tag
				name="LongAndBushy" />
      <hair_tag
				name="BraidedAndLong" />
      <hair_tag
				name="HighPonytail" />
      <hair_tag
				name="Bandit Hair 3" />
      <hair_tag
				name="TiedAcrossBack" />
    </hair_tags>
    <beard_tags>
      <beard_tag
				name="MustacheAndPatch" />
      <beard_tag
				name="BeardWithShavedCheeks" />
      <beard_tag
				name="TrimmedDutch" />
      <beard_tag
				name="MediumDutch" />
      <beard_tag
				name="LightShortBeard" />
      <beard_tag
				name="HeavyShortBeard" />
      <beard_tag
				name="PointedShortBeard" />
      <beard_tag
				name="Stubble" />
      <beard_tag
				name="BushyBandholz" />
      <beard_tag
				name="BushyBandholzLong" />
    </beard_tags>
    <tattoo_tags>
      <tattoo_tag
				name="Cleanface" />
    </tattoo_tags>
  </BodyProperty>
```

`TAOM_bodyproperties.xml:612-666`. The three things a reader changes first:

1. **`age` on the two nodes, 25.99 and 39.5.** This is the spread of apparent ages in a Gondor rank. Narrow the gap for a uniform-looking unit, set the two identical for one fixed face.
2. **The two `key` values.** These are the faces themselves and there is no way to hand-edit them meaningfully; replace a whole key with one exported from the face editor. Count the characters afterwards: 128, or the preset silently falls back to the other end.
3. **The tag lists.** `TiedAcrossBack` appears twice here. Nothing clears the tag string between reads, so the duplicate is genuinely stored twice, which is harmless but tells you the lists are unions rather than sets.

**Pointing a character at it.** A troop names the preset by id with a dot-qualified reference:

<!-- example file="Main/_Module/ModuleData/troops/troops_erebor.xml" id="erebor_militia_spearman" -->

```xml
    <NPCCharacter
        id="erebor_militia_spearman"
        race="dwarf"
        default_group="Infantry"
        level="11"
        name="{=aom_er_militia_spear}[Erebor] Militia Spearman"
        occupation="Soldier"
        culture="Culture.erebor">
        <face>
            <face_key_template
                value="BodyProperty.fighter_erebor" />
        </face>
```

`troops_erebor.xml:7-18`. Two independent settings sit in that header. `race="dwarf"` selects the skeleton and mesh family; `BodyProperty.fighter_erebor` supplies the morph key, the age and weight range and the tag filters. They are passed together into one call at `BasicCharacterObject.cs:221`. The equipment must follow the race as well: this troop's gear is `sk_dwarf_erebor_*` and `sm_dwarf_erebor_*` at `troops_erebor.xml:47-63`, because human-rigged cloth on a dwarf skeleton clips and floats ([`docs/features/hero-race.md`](../features/hero-race.md) line 185).

**The character-creation file is a different shape.** It is not a `<BodyProperty>` list; it is one pasted body string per culture.

<!-- example file="Main/_Module/ModuleData/charactercreation/cc_body_properties.xml" id="vlandia" -->

```xml
  <!-- Rohan (XSLT culture id: vlandia) -->
  <Culture id="vlandia">
    <BodyProperties version="4" age="24" weight="0.5139" build="0.6435" key="0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002" />
  </Culture>
```

`cc_body_properties.xml:39-42`. Six TAOM factions ride on vanilla culture string ids and must be written as the vanilla id: `vlandia` is Rohan, `empire` is Dunland, `battania` is Khand, `aserai` is Harad, `sturgia` is Barding, `khuzait` is Rhun (`cc_body_properties.xml:23-31`). Writing `id="rohan"` produces an entry that never matches and never complains. The file currently seeds 21 cultures. <!-- measured: rg -o '<Culture id="[^"]+"' over cc_body_properties.xml, 22 hits of which one is the placeholder in the header comment 2026-09-05 -->

## Recipes: Add / Modify / Delete

### Add a preset

1. In game, open the face editor (character creation, or the face customiser), build the face you want and export the `<BodyProperties version="4" ... key="..." />` element. There is no other way to produce a key: the bit layout of `KeyPart1..KeyPart8` is decoded inside `TaleWorlds.Native.dll` and is not recoverable from the managed code, so hand-editing hex is not an option.
2. Open [`Main/_Module/ModuleData/TAOM_bodyproperties.xml`](../../Main/_Module/ModuleData/TAOM_bodyproperties.xml) and copy a nearby entry whole. The file is CRLF with no BOM; keep it that way. <!-- measured: python byte check of TAOM_bodyproperties.xml, 1213 CRLF, no BOM 2026-09-05 -->
3. Change the `id=` to something unique across every module. Grep the four merged files first if you are reusing a vanilla-sounding name.
4. Paste your exported key into `BodyPropertiesMin` and a second exported key into `BodyPropertiesMax`, or paste the same key into both for one fixed face. Count the characters: exactly 128 each.
5. Set `age`, `weight` and `build` on both nodes. Leaving one out gives you 30 / 0.5 / 0.5, not zero.
6. Set the tag lists to names that the target race actually declares. The declaration site is the `<style_tag name="...">` entries inside that race's `<skin>` blocks in `skins.xml`; see the race table in Gotchas below for how few some races declare.

Check: `python tools/validate_moduledata.py` (the `BROKEN_BODY_PROPERTY_REF` gate reads this file as its definition set, `tools/taom_schema.py:1728-1733`).
Takes effect: full game restart. Existing heroes keep the body stored in their save (`Hero.cs:169` and `Hero.cs:1813`); troops re-roll their face on every spawn.
Code: No code changes needed.

### Add a character-creation default for a culture

1. Export the body from the face editor exactly as above.
2. Open [`Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`](../../Main/_Module/ModuleData/charactercreation/cc_body_properties.xml) and add a `<Culture id="...">` block holding the pasted `<BodyProperties .../>` element.
3. Use the runtime culture id, not the LOTR name. The six rebound ids are listed in the file's own header at `cc_body_properties.xml:23-31`.
4. Note the different defaults on this path: it goes through `BodyProperties.FromString`, where an absent `weight` or `build` is `0`, not `0.5` (`BodyProperties.cs:101-103`). `age=` is parsed and then ignored, because `Hero.Age` comes from `BirthDay` and TAOM does not touch it.
5. This is separate from the vanilla per-culture hook, which is the `default_character_creation_body_property="BodyProperty.<id>"` attribute on a `<Culture>` in `taom_spcultures.xml`, read at `CultureObject.cs:339`. 16 TAOM cultures set that attribute. <!-- measured: rg -c default_character_creation_body_property over taom_spcultures.xml 2026-09-05 -->

Check: after the restart, read `taom_debug_*.log` under the game install's `bin/Win64_Shipping_Client/Logs/` for the `CCBodyPropertiesProvider: Loaded N culture body-property entries` line and any skip warning.
Takes effect: full game restart. The provider is a DryIoc singleton with a load-once cache (`CharacterCreationIoC.cs:18`, `CCBodyPropertiesProvider.cs:32-39`), so a save-load or a new campaign is not enough.
Code: No code changes needed.

### Modify: point a troop at a different preset

1. Find the character's `<face><face_key_template value="BodyProperty.<id>" /></face>` block and change the id after the dot.
2. Keep the `BodyProperty.` prefix and the dot. A bare `value="fighter_gondor"` throws `MBInvalidReferenceException` at `MBObjectManager.cs:1504-1508`.
3. For a bulk change across every troop file, [`tools/oneoff/apply_troop_bodyproperties.py`](../../tools/oneoff/apply_troop_bodyproperties.py) is the pattern to copy: it rewrites only the value string, in binary, so the BOM and CRLF survive. Dry run is the default, `--apply` writes.

Check: `python tools/validate_moduledata.py`, which reports `BROKEN_BODY_PROPERTY_REF` for a target that resolves nowhere (`tools/taom_schema.py:172-173`).
Takes effect: full game restart.
Code: No code changes needed.

### Modify: give a troop a race

1. Add `race="<race id>"` to the `<NPCCharacter>` opening tag. Omitting the attribute is not neutral in name only: it sets race 0 explicitly (`BasicCharacterObject.cs:323`), which is why every human troop in TAOM carries no `race=` at all.
2. The name must match a `<race id="...">` in some active module's `skins.xml`, case exactly. There are 15 across the merged set: `human` from `Native/ModuleData/skins.xml` and 14 from `LOTRLOME_Armory/ModuleData/skins.xml`. `TAOM_Map/ModuleData/skins.xml` is a stub and adds none. <!-- measured: rg -n '<race' over the three skins.xml files 2026-09-05 -->
3. Swap the troop's equipment to items authored for that skeleton at the same time. The `race=` attribute changes the body and nothing else.
4. If you are introducing a race that does not exist yet, three more things must exist before it is safe: a `<race>` block in `skins.xml`, an entry in [`Main/_Module/ModuleData/raceage/race_age_config.json`](../../Main/_Module/ModuleData/raceage/race_age_config.json) (15 entries today, one per race), and, if the race is playable in character creation, `as_<race>_facegen` plus `as_<race>_female_facegen` action sets in the Armory's `action_sets.xml`. See [`docs/features/character-creation.md`](../features/character-creation.md) lines 380 to 404 for the copy-the-dwarf-block recipe.
5. For a bulk stamp, [`tools/oneoff/add_race_attribute.py`](../../tools/oneoff/add_race_attribute.py) shows the shape: it inserts the attribute after `id=`, is idempotent, and re-parses every file it touched.

Check: `python tools/audit_action_set_parity.py` and `python tools/audit_civilian_action_set_coverage.py`, then `taom.print_races` in the in-game console with cheat mode on, which prints the race registry in FaceGen index order ([`docs/features/dev-console.md`](../features/dev-console.md) line 305).
Takes effect: full game restart.
Code: No code changes needed. TAOM's `RaceManager` reads the engine's list, so a race the engine already knows needs no C# ([`docs/features/troll-race.md`](../features/troll-race.md) lines 30 to 33).

### Delete a preset

1. Grep for the id first: `rg -n 'BodyProperty\.<id>' Main/_Module/ModuleData`. Every hit is a character that will lose its face.
2. Repoint those characters at a surviving preset before you remove anything.
3. Remove the whole `<BodyProperty>` element, opening tag to closing tag.
4. Nine of the 30 shipped presets are referenced nowhere in TAOM's ModuleData today, so a zero-hit grep is a normal result rather than a sign you grepped wrong. <!-- measured: python diff of the 30 ids against the rg -o BodyProperty.<id> reference counts 2026-09-05 -->

Check: `python tools/validate_moduledata.py`, which fails with `BROKEN_BODY_PROPERTY_REF` if any reference to the deleted id survives.
Takes effect: full game restart.
Code: No code changes needed.

## Gotchas: what fails silently and what crashes

- **One bad entry silently deletes every entry after it.** `MBObjectManager.LoadXML` wraps the whole entry loop in `try { ... } catch (Exception) { }` with an empty body, so any exception aborts the loop and is discarded with no log line. Entries before the bad one load; the bad one and everything below it do not, and the only symptom is a batch of characters with default faces. `MBObjectManager.cs:790-796`.
- **Three ways to trigger that.** A `<BodyProperty>` with no `id=` (`MBObjectManager.cs:1391`), a Min or Max node with no `key=` (`StaticBodyProperties.cs:104`), and a `<hair_tag>`, `<beard_tag>` or `<tattoo_tag>` with no `name=` (`MBBodyProperty.cs:75`, `:82`, `:93`). If faces go generic from one entry onwards, look above the first broken one.
- **A key of the wrong length does not throw, it evaporates.** `StaticBodyProperties.cs:105-109` returns false, `BodyProperties.cs:80-81` hands back a default struct, and `MBBodyProperty.cs:65` and `:69` ignore the false. You get an all-zero key with age 0, which then trips the age fixup and copies the other end over it. A truncated Min key therefore makes the preset use Max for both ends, quietly. Count to 128.
- **A misspelled `face_key_template` target is not an error either, it is a blank face.** `GetPresumedObject` auto-creates a missing target (`MBObjectManager.cs:713-735`, `autoCreateInstance` defaults to true at `MBObjectManager.cs:376`), so the character gets a brand-new empty preset with a zero key, no tags and age 0. Nothing is logged. This is the first thing to check when a new troop has the wrong face.
- **Omitting the `<face>` element entirely is the same failure with a worse symptom: the character renders as a toddler.** `Deserialize` declares `bodyProperties` and `bodyProperties2` as `default(BodyProperties)` (`BasicCharacterObject.cs:346-347`) and, when nothing set `BodyPropertyRange` from a `face_key_template`, registers a fresh `MBBodyProperty` from those two locals (`:472-475`). An all-zero struct has age 0, and `skins.xml` maps age 0 to `mesh_maturity_type="toddler"`, `min_scale` 0.52 against the adult 1.07. Every race in the merged file has a toddler skin, so no race is exempt. Nothing is logged, and the character is otherwise fully functional: correct name, correct equipment, correct stats, waist-high. TAOM shipped 46 such characters, the arena practice set for ten cultures, and players reported the arena fighters as children before anyone read the deserializer.
- **The engine's two age guards do not catch that, because they read a different age.** `Mission.SpawnAgent` takes `agentCharacter.Age`, forces 29 when it is exactly 0, and forces 27 for a sub-teenager in `Battle`, `Duel`, `Tournament` or `Stealth` mode (`Mission.cs:4101-4122`). But `Age` is its own property, set at deserialisation to `max(20f, BodyPropertyMax.Age)` when the XML carries no `age=` attribute (`BasicCharacterObject.cs:486`), so a faceless character reports a healthy 20 and passes both guards while its visual age stays 0. The campaign age and the visual age are separate numbers and only one of them is wrong. `CharacterFaceCoverageTests` is the gate for the missing element; the reference gate below is the gate for a typo'd one.
- **The one gate that does catch it is external.** `python tools/validate_moduledata.py` raises `BROKEN_BODY_PROPERTY_REF` against a definition set built from the four `*_bodyproperties.xml` files, so a cross-module reference such as `BodyProperty.fighter_empire` passes and a typo does not. `tools/taom_schema.py:172-173` and `:1728-1733`. TAOM leans on this: 234 of its 2,650 references resolve in vanilla files rather than in TAOM's own. <!-- measured: rg -o 'BodyProperty\.[A-Za-z0-9_]+' over Main/_Module/ModuleData compared against the id set of TAOM_bodyproperties.xml 2026-09-05 -->
- **Redefining a vanilla id merges, it does not replace.** Every module's file is concatenated and merged on `@id`, with the later module's attributes layered on top (`MBObjectManager.cs:799-818`), and the Min and Max nodes are marked always-prefer-merge, so you can override just `weight=` and inherit vanilla's key. TAOM already does this by accident with `fighter_sturgia`. To wipe the vanilla version instead, put `_replaceWhileMerging="true"` on your node; the attribute is injected into every schema at load time (`MBObjectManager.cs:1092`), so it is legal even though the XSD never lists it.
- **`race=` is the one attribute here that hard-crashes on a typo.** `FaceGen.GetRaceOrDefault` is a raw dictionary index despite its name (`FaceGen.cs:115-118`, the MountAndBlade one), so an unknown race string throws `KeyNotFoundException` out of `Deserialize` and into the same silent swallow, killing the rest of that character file.
- **Race numbers are positions in the merged `skins.xml` list, not stable ids.** Inserting a `<race>` renumbers every race after it, which is why the `sauron` block was appended at the end with an authoring comment saying exactly that (`LOTRLOME_Armory/ModuleData/skins.xml:204236`) and why TAOM saves a `_taom_raceNameLegend` beside its saved race integers ([`docs/features/hero-race.md`](../features/hero-race.md) line 32).
- **Tag names are validated by nobody on the managed side.** `MBBodyProperty` only concatenates strings. All 25 distinct tag names TAOM uses across 290 tag rows do exist somewhere in the merged `skins.xml`, but "somewhere" is not "on the race that uses the preset": the `dwarf` race declares only 7 style tags, while `fighter_erebor`, the preset every dwarf troop points at, lists `empire`, `sturgia`, `battania`, `khuzait` and `Cleanface`, none of which is among those 7. Whether a non-matching tag widens the pool or empties it is decided in native code. <!-- measured: python re.findall of style_tag names per race block in the Armory skins.xml against the tag names in TAOM_bodyproperties.xml 2026-09-05 -->

  | Race | Distinct `<style_tag>` names declared |
  |---|---|
  | `elf` | 46 |
  | `uruk`, `uruk_hai`, `berserker`, `cave_troll`, `hill_troll`, `pale_uruk`, `dg_uruk`, `goblin`, `sauron` | 45 each |
  | `dwarf` | 7 |
  | `saruman` | 3 |
  | `orc`, `nazghul` | 0 |

- **An unresolved mesh name in `skins.xml` is a native access violation with no crash bundle.** That was issue #403, female dwarves crashing the game when the camera looked at them, and the tell was the absence of a managed stack rather than its content ([`docs/reviews/lessons/misc.md`](../reviews/lessons/misc.md) lines 54 to 68). When face or body rendering breaks for one race, walk the XML first: `skins.xml`, then `monsters.xml`, then `action_sets.xml`, before touching meshes (same file, lines 23 to 27).
- **Grepping `skins.xml` will lie to you.** Each `<race>` holds ten `<skin>` elements, one per gender and maturity, and only the two adult ones ever field a soldier. Worse, three races write the attribute as `mesh_maturity_type ="adult"` with a space before the equals sign: a plain search for `mesh_maturity_type="adult"` finds 22 of the 28 adult skins and returns a confident wrong answer. Match on `mesh_maturity_type\s*=\s*"adult"`. <!-- measured: rg -c with and without the space over LOTRLOME_Armory/ModuleData/skins.xml 2026-09-05 -->
- **The engine has a real bug in the height clamp, and it is multiplayer only.** `ClampHeightMultiplierFaceKey` reads the six height bits out of `KeyPart8` at `BodyProperties.cs:194-195` and writes the corrected value into the `KeyPart7` argument slot at `:201`, leaving `KeyPart8` untouched. Singleplayer never runs that path, so TAOM is unaffected, but do not use it as evidence of where height lives.

### Questions this chapter cannot answer

- **What the native face generator does with a tag that matches no mesh, and what an empty tag string means.** `MBBodyProperty` only builds the comma-joined string and `BasicCharacterObject.cs:221` hands it straight to `MBAPI.IMBFaceGen`, which is not in the decompile. Vanilla presets always supply at least one tag, so copying that pattern is the safe move. The `fighter_erebor` case above is the live instance of the question.
- **Whether a key authored on one race reads correctly on another.** `race=` and the preset are independent inputs to one call, so nothing at the managed level treats them as agreeing or disagreeing. What the native generator makes of a human-authored key applied to a dwarf skeleton is not visible from here. Test it in game.
- **What each of the 512 key bits means.** Managed code decodes exactly one field out of the key, the six-bit height multiplier in `KeyPart8` (`BodyProperties.cs:194-195`). Everything else is unpacked in `TaleWorlds.Native.dll`. The practical answer stays "generate the key in the face editor and paste it whole".
- **Whether `version` reaches the native decoder by some other route.** Provably unread by every managed deserializer in the dump, always written as `4`. Nobody has tested a different value.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 46 `NPCCharacter` entries with no `<face>` across ten cultures, out of 3,774 in `Main/_Module/ModuleData`; 0 after the fix | ElementTree walk of every `.xml` under `Main/_Module/ModuleData`, counting `NPCCharacter` nodes whose `face` child is absent (the same check `CharacterFaceCoverageTests` runs) | 2026-09-06 |
| 24 toddler skins and 24 adult skins across the 12 races in `LOTRLOME_Armory/ModuleData/skins.xml` | re.findall of `mesh_maturity_type\s*=\s*"(toddler\|adult)"` over the file | 2026-09-06 |
| 30 `<BodyProperty>` entries in `TAOM_bodyproperties.xml`; 60 `key` attributes, every one exactly 128 characters | `python -c` re.findall of `<BodyProperty\s+id="([^"]+)"` and `key="([^"]*)"` over the file | 2026-09-05 |
| 69 in `sandboxcore_bodyproperties.xml`, 14 in `sandbox_bodyproperties.xml`, 9 in `naval_bodyproperties.xml`; 121 in the merged registry; `fighter_sturgia` the one shared id | the same re.findall per file, plus the `Registry:` line from `python tools/validate_moduledata.py` | 2026-09-05 |
| 2,650 `BodyProperty.` references across `Main/_Module/ModuleData`, 26 distinct targets, 5 of them (234 references) defined outside TAOM | `rg -o 'BodyProperty\.[A-Za-z0-9_]+' --glob '*.xml' .` piped through `sort | uniq -c`, diffed against the id set | 2026-09-05 |
| 9 of the 30 TAOM presets referenced nowhere in TAOM's ModuleData | the same reference counts diffed against the id list | 2026-09-05 |
| 290 tag rows in `TAOM_bodyproperties.xml`, 25 distinct names, all declared somewhere in the merged `skins.xml` | re.findall of `<(hair\|beard\|tattoo)_tag\s+name="([^"]+)"` checked against `<style_tag\s+name="([^"]+)"` in the Native and Armory files | 2026-09-05 |
| 15 races: 1 in `Native/ModuleData/skins.xml`, 14 in `LOTRLOME_Armory/ModuleData/skins.xml`, 0 in `TAOM_Map/ModuleData/skins.xml` | `rg -n '<race' <each skins.xml>` | 2026-09-05 |
| Style tags per race: elf 46, nine races 45, dwarf 7, saruman 3, orc and nazghul 0 | python split of the Armory `skins.xml` on `<race id="...">` then re.findall of `style_tag name=` per block | 2026-09-05 |
| 10 `<skin>` elements per race; 28 adult skins, 22 spelled `mesh_maturity_type="adult"` and 6 with a space before the equals sign | `awk` range over the dwarf block, then `rg -c` for both spellings over the whole Armory `skins.xml` | 2026-09-05 |
| `race=` usage across `Main/_Module/ModuleData`: elf 458, orc 295, goblin 289, dg_uruk 238, pale_uruk 212, dwarf 194, uruk_hai 171, uruk 163, berserker 10, human 8, cave_troll 1 | `rg -o 'race="[^"]+"' --glob '*.xml' .` piped through `sort | uniq -c` | 2026-09-05 |
| 66 `race="dwarf"` lines in `troops/troops_erebor.xml` | `rg -c 'race="dwarf"' Main/_Module/ModuleData/troops/troops_erebor.xml` | 2026-09-05 |
| 21 cultures seeded in `cc_body_properties.xml` (22 `<Culture id=` hits, one of which is the placeholder in the header comment) | `rg -o '<Culture id="[^"]+"'` over the file | 2026-09-05 |
| 16 cultures carrying `default_character_creation_body_property` in `taom_spcultures.xml` | `rg -c default_character_creation_body_property Main/_Module/ModuleData/taom_spcultures.xml` | 2026-09-05 |
| 15 entries in `race_age_config.json` | `python -c` json load of the `races` object | 2026-09-05 |
| 131 distinct `<style_tag>` names in `Native/ModuleData/skins.xml` | re.findall of `<style_tag\s+name="([^"]+)"` over the file | 2026-09-05 |
| Exactly one file in the v1.4.8 dump reads an attribute named `version`, and it is `HotKeyManager.cs` | `rg -c --glob '*.cs' 'Attributes\["version"\]\|GetAttribute("version")\|ReadString([^)]*"version"' <dump root>` | 2026-09-05 |
| `TAOM_bodyproperties.xml` is CRLF with no BOM, 1,213 line endings | python byte check of the file | 2026-09-05 |

## Read next

- [`docs/features/character-creation-body-properties.md`](../features/character-creation-body-properties.md), the per-culture starting body, its validation rules and the culture-id mapping.
- [`docs/features/hero-race.md`](../features/hero-race.md), race persistence across saves, the race-name legend and the rule that equipment must follow the race.
- [`docs/features/race-age-system.md`](../features/race-age-system.md), the per-race lifespan and fertility config and what adding a race needs.
- [`docs/features/character-creation.md`](../features/character-creation.md), the `as_<race>_facegen` requirement and the two failure modes that have shipped.
- [`docs/features/kingdom-voices.md`](../features/kingdom-voices.md), the per-skin voice pools and the maturity-row trap.
- [`docs/features/troll-race.md`](../features/troll-race.md), the race to monster to skin to action-set to preset chain in one diagram.
- [`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md), the facegen checklist and the standalone action-set parity gate.
- [`docs/reviews/lessons/misc.md`](../reviews/lessons/misc.md), the check-the-XML-first order for race rendering faults.
- [`docs/reviews/rca-elf-cc-facegen-2026-05-22.md`](../reviews/rca-elf-cc-facegen-2026-05-22.md), the write-up of the missing elf facegen pair.
- [`tools/README.md`](../../tools/README.md), the full generator and validator index.
- [troops](troops.md), [npcs-notables-and-townsfolk](npcs-notables-and-townsfolk.md), [wanderers-and-named-companions](wanderers-and-named-companions.md), [cultures](cultures.md), [recipe-add-a-race-or-creature](recipe-add-a-race-or-creature.md) and [validation-and-testing](validation-and-testing.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/moduledata-validation.md](../features/moduledata-validation.md)
- [docs/features/tournament-armor-assignment.md](../features/tournament-armor-assignment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/npcs-notables-and-townsfolk.md](./npcs-notables-and-townsfolk.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-race-or-creature.md](./recipe-add-a-race-or-creature.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
