# Lords and heroes

## What this file is

A named lord is two XML records that share one id: an `<NPCCharacter>` that carries the body (face, race, sex, age, skill set, equipment) and a `<Hero>` that carries the life (which clan he belongs to, his parents, his spouse, whether he is alive, his encyclopedia biography). The engine staples the two together by matching the id, and it does that once, on a brand-new campaign. Every job in this chapter (add a lord, rename one, move him to another culture, re-wire his family, retire him) is really the job of keeping both halves in step.

## Where it lives and how it is registered

| Half | File | Root element | Entry element | Engine class |
|---|---|---|---|---|
| The body | [`Main/_Module/ModuleData/characters/lords.xml`](../../Main/_Module/ModuleData/characters/lords.xml) | `<NPCCharacters>` | `<NPCCharacter>` | `CharacterObject` on top of `BasicCharacterObject` |
| The life | [`Main/_Module/ModuleData/characters/heroes.xml`](../../Main/_Module/ModuleData/characters/heroes.xml) | `<Heroes>` | `<Hero>` | `Hero` |
| The family he belongs to | [`Main/_Module/ModuleData/characters/clans.xml`](../../Main/_Module/ModuleData/characters/clans.xml) | `<Factions>` | `<Faction>` | `Clan`, and see [Clans](clans.md) |
| Renames and biographies applied to vanilla lords | [`Main/_Module/ModuleData/lords.xslt`](../../Main/_Module/ModuleData/lords.xslt) and [`Main/_Module/ModuleData/heroes.xslt`](../../Main/_Module/ModuleData/heroes.xslt) | stylesheets, not data | `<xsl:template match="Hero[@id='...']">` | as above |

The stylesheets rewrite `SandBox/ModuleData/lords.xml` and `SandBox/ModuleData/heroes.xml`, the two vanilla files. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. TAOM never edits either vanilla file directly, so there is nothing to revert there: the stylesheets live in the repo and do the rewriting at load time.

Five registrations in `Main/_Module/SubModule.xml` decide the order, and the order is the whole story.
Each is one `<XmlNode>` block, of this shape:

<!-- excerpt file="Main/_Module/SubModule.xml" -->

```xml
    <!-- TAOM additional heroes -->
    <XmlNode>
      <XmlName id="Heroes" path="characters/heroes"/>
      <IncludedGameTypes>
        <GameType value="Campaign"/>
        <GameType value="CampaignStoryMode"/>
      </IncludedGameTypes>
    </XmlNode>
```

| Line in `SubModule.xml` | `XmlName id` | `path` | What it is |
|---|---|---|---|
| 96 | `NPCCharacters` | `lords` | the `lords.xslt` stylesheet |
| 106 | `Heroes` | `heroes` | the `heroes.xslt` stylesheet |
| 139 | `Factions` | `characters/clans` | TAOM's clans |
| 148 | `Heroes` | `characters/heroes` | TAOM's heroes |
| 157 | `NPCCharacters` | `characters/lords` | TAOM's lords |

<!-- measured: grep -n 'path="lords"\|path="heroes"\|path="characters/heroes"\|path="characters/lords"\|path="characters/clans"' Main/_Module/SubModule.xml 2026-09-05 -->

Everything sharing an `XmlName id` is merged into one document before any of it is deserialized, in
registration order, so the plain XML at lines 148 and 157 is applied after the stylesheets at 96 and
106. The merge is **per attribute, not per node**: `MergeElementAttributes` copies only the
attributes the later document actually declares, so an attribute `characters/lords.xml` leaves out
survives from the stylesheet's output rather than being cleared. Only `_replaceWhileMerging="true"`
wipes the element first, and TAOM never uses it.

<!-- engine-ref type="TaleWorlds.ObjectSystem.MBObjectManager" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs" lines="799-817" -->

```csharp
foreach (XAttribute item in enumerable)
    element1.SetAttributeValue(item.Name, item.Value);
```

**When the game reads these files.** `SandBoxManager.InitializeSandboxXMLs` loads `NPCCharacters`
unconditionally, then wraps `Heroes`, `Kingdoms` and `Factions` in `if (!isSavedCampaign)`
(`SandBoxManager.cs:360-374`). Characters therefore exist before heroes, which is why the id lookup
inside `Hero.Deserialize` finds something. Clans do **not** exist yet when heroes are read; a
`faction="Faction.x"` reference resolves to an auto-created placeholder that `characters/clans.xml`
fills in a moment later. That is normal, and reordering the registrations to "fix" it breaks more
than it mends. See [Load order and dependencies](load-order-and-dependencies.md).

## Attributes

### The `<Hero>` half

`Hero.Deserialize` reads eight attributes. `id` is read by the shared base class and is tabled in
[Troops](troops.md); it is repeated here because on this element it does more work than anywhere
else in the mod.

<!-- engine-table type="TaleWorlds.CampaignSystem.Hero" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `faction` | ref, written `Faction.<clan_id>` | yes in practice | null, and the next line dereferences it, so the entry throws | Which clan this person belongs to. The prefix is `Faction.`, not `Clan.`, because `Clan` registers under the element name `Faction`. Assigning it calls the clan's `OnLordAdded`, which is what puts the lord in the clan's lord list and gives him a home settlement. A clan id of literally `neutral` is skipped and the hero stays clanless. | `Hero.cs:1834-1838` |
| `alive` | string compared against the literal `false` | no | alive (`CharacterStates.NotSpawned`) | Marks a dead ancestor, the kind of row that exists only so living lords can point `father=` or `mother=` at it. The test is `xmlAttribute.Value == "false"`, so `False`, `FALSE` and `0` all silently mean alive. A dead hero gets an invented birthday and death day from his character's age. | `Hero.cs:1821-1827` |
| `father` | ref, written `Hero.<hero_id>` | no | no father | Family-tree link, used by the encyclopedia family panel and by inheritance. The setter is two-way, so it also pushes this hero into the father's children list. There is no `children` attribute; you declare parentage from the child's side. | `Hero.cs:1828` |
| `mother` | ref, written `Hero.<hero_id>` | no | no mother | Same mechanics as `father`. | `Hero.cs:1829` |
| `spouse` | ref, written `Hero.<hero_id>` | no | unmarried | Marriage. The setter is reciprocal, so writing it on one partner is enough; the attribute is only read at all when `Spouse` is still null, so if the partner's row came earlier and already married this hero, this attribute is ignored. | `Hero.cs:1830-1833` |
| `text` | string, a localizable `{=KEY}English` value | no | an empty `TextObject`, and the encyclopedia falls back to an auto-generated blurb | The encyclopedia biography. The raw attribute value is wrapped in a `TextObject` verbatim, so the `{=KEY}` form is what makes it translatable. | `Hero.cs:1839` |
| `preferred_upgrade_formation` | enum `FormationClass`, case-insensitive | no | `NumberOfAllFormations`, the "no preference" sentinel | Which branch this lord picks when one of his soldiers has more than one upgrade path. A typo neither throws nor warns; it just means no preference. Zero TAOM lords set it. | `Hero.cs:1840-1845` |
| `banner_item` | ref to an item, **bare id, no `Item.` prefix** | no | no banner is assigned, silently | A campaign banner the lord starts holding. This one attribute skips the dotted-reference reader and calls `GetObject<ItemObject>` directly, which returns null for an unknown id. Zero TAOM lords set it. | `Hero.cs:1846-1850` |

`banner_key` is legal in the schema and **is never read on a `<Hero>`**: `Hero.Deserialize` does not
look at it. Heraldry belongs on the `<Faction>` row, where `Clan.Deserialize` consumes it. Putting it
on a hero is a silent no-op. See [Banners and heraldry](banners-and-heraldry.md).

### The `<NPCCharacter>` half, the parts that only matter for a lord

The full attribute tables for `<NPCCharacter>` are in [Troops](troops.md). These are the ones a lord
uses differently from a line troop.

<!-- engine-ref type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" lines="315-527" -->

| Attribute | What it means on a lord | Read at (file:line) |
|---|---|---|
| `is_hero="true"` | Half of a matched pair. In the campaign the flag itself is overridden: `CharacterObject.IsHero` returns `_heroObject != null`, and only `Hero.SetCharacterObject` ever fills that. So the flag without a `<Hero>` row does not make a hero. | `BasicCharacterObject.cs:334`, `CharacterObject.cs:294` |
| `occupation="Lord"` | Parsed with `Enum.Parse`, so a misspelling throws and the rest of the file stops loading. All 1184 TAOM lord entries use `Lord`. | `CharacterObject.cs:539-542` |
| `skill_template` | Points at a shared `SkillSet`. If it resolves, the inline `<skills>` child is ignored outright. This is the trap that produced TAOM's SkillSet rewrite. See [Skill sets](skill-sets.md). | `BasicCharacterObject.cs:337`, `:355` |
| `is_female` | Read here, then copied onto the `Hero` once. It travels as a unit with `<beard_tags>` and the `<BodyProperties key>`; flipping the attribute alone leaves a bearded woman. | `BasicCharacterObject.cs:479` |
| `race` | Skeleton, meshes and hit points. Absent means index 0, the human convention. 525 of 1184 TAOM lords leave it off. | `BasicCharacterObject.cs:324` |
| `age` | The starting age. Overridden at runtime by the hero's own age once the campaign is running. | `BasicCharacterObject.cs:485` |
| `voice` | Bare trait id, no `Trait.` prefix: `curt`, `ironic`, `earnest`, `softspoken`. Absent means `softspoken`. | `CharacterObject.cs:572` |
| `is_obsolete` | The supported retirement switch. Its only consumer in the shipping decompile is `Hero.cs:1537`. | `BasicCharacterObject.cs:336` |

## Child elements

A `<Hero>` has **none**. `Hero.Deserialize` contains no loop over `node.ChildNodes`, so the element
is a single tag with attributes only.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Hero" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs" lines="1803-1852" -->

Everything a lord looks like lives in the `<NPCCharacter>` children, which are the same four blocks
a troop uses and are tabled in [Troops](troops.md): `<face>` (with `<BodyProperties>`, `<hair_tags>`,
`<beard_tags>`, `<tattoo_tags>`), `<skills>`, `<Traits>` and `<Equipments>`.

**What the hero copies from the character, once.** `Hero.SetInitialValuesFromCharacter` runs during
deserialization and takes trait levels, `Level`, `Name`, `Culture`, the default age, hit points,
`IsFemale`, `Occupation`, and one randomly chosen loadout each from the battle, civilian and stealth
rosters (falling back to the `neutral_culture` roster when a list is empty). None of those can be set
in `heroes.xml`.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Hero" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs" lines="2236-2276" -->

**The face is the MIN block, not a random roll.** `Hero.Deserialize` sets
`StaticBodyProperties = CharacterObject.GetBodyPropertiesMin(returnBaseValue: true).StaticProperties`
and takes weight and build from the same block (`Hero.cs:1813-1820`). A named lord's face is the
minimum `<BodyProperties>` key of his `<NPCCharacter>`. Change the face there, never in
`heroes.xml`. See [Body properties](body-properties.md).

**Skills get noise added.** `DefaultHeroCreationModel.GetDefaultSkillsForHero` returns an empty list
for anyone under `HeroComesOfAge` (18 in `DefaultAgeModel.cs:39`, and TAOM's `TaomAgeModel` does not
override it), so child heroes start with zero skills by design. For everyone else each skill above
zero is passed through `AddNoiseToSkillValue`, which does
`skillValue += MBRandom.RandomInt(5, 10)`. The number you author is a floor, not the value the
player sees.

<!-- engine-ref type="TaleWorlds.CampaignSystem.GameComponents.DefaultHeroCreationModel" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultHeroCreationModel.cs" lines="361-379" -->

## Worked example

Dáin II Ironfoot, king of Erebor. Three files, one id, plus the stylesheet pattern for a lord TAOM
inherited from vanilla rather than added.

The body, from `characters/lords.xml`:

<!-- example file="Main/_Module/ModuleData/characters/lords.xml" id="lord_E1_1" -->

```xml
    <NPCCharacter id="lord_E1_1" race="dwarf" name="{=aom_lord_E1_1_name}Dáin II Ironfoot" age="38" voice="earnest" is_hero="true" culture="Culture.erebor" occupation="Lord" default_group="Infantry" face_mesh_cache="true" skill_template="SkillSet.taom_dwarf_king_skills">
        <face>
            <BodyProperties version="4" age="25.22" weight="0.4699" build="1"  key="00000007800021C88F8870F099200F450000857877777878FFFFEFFF7000000200BF76030BFFEFFF00000000000000000000000000000000000000000F6C0140" />
            <hair_tags>
                <hair_tag name="sturgia" />
            </hair_tags>
            <beard_tags>
                <beard_tag name="sturgia" />
            </beard_tags>
            <tattoo_tags>
                <tattoo_tag name="Cleanface" />
            </tattoo_tags>
        </face>
        <skills>
            <skill id="OneHanded" value="275" />
            <skill id="TwoHanded" value="280" />
            <skill id="Polearm" value="240" />
            <skill id="Bow" value="130" />
            <skill id="Crossbow" value="180" />
            <skill id="Throwing" value="140" />
            <skill id="Riding" value="150" />
            <skill id="Athletics" value="250" />
            <skill id="Crafting" value="275" />
            <skill id="Scouting" value="180" />
            <skill id="Tactics" value="405" />
            <skill id="Roguery" value="80" />
            <skill id="Charm" value="230" />
            <skill id="Leadership" value="417" />
            <skill id="Trade" value="240" />
            <skill id="Steward" value="348" />
            <skill id="Medicine" value="170" />
            <skill id="Engineering" value="275" />
        </skills>
        <Traits>
            <Trait id="Honor" value="2" />
            <Trait id="Generosity" value="1" />
            <Trait id="Calculating" value="1" />
            <Trait id="Mercy" value="1" />
            <Trait id="Valor" value="2" />
            <Trait id="Egalitarian" value="0" />
            <Trait id="Oligarchic" value="2" />
            <Trait id="Authoritarian" value="1" />
        </Traits>
        <Equipments>
            <EquipmentSet id="dain_bat_equipment" />
            <EquipmentSet id="dain_civ_equipment" equipmentType="Civilian" />
        </Equipments>
    </NPCCharacter>
```

The life, from `characters/heroes.xml`:

<!-- example file="Main/_Module/ModuleData/characters/heroes.xml" id="lord_E1_1" -->

```xml
	<Hero
		id="lord_E1_1"
		faction="Faction.clan_erebor_1"
		text="{=dain_ironfoot_description}Dáin Ironfoot, the stalwart and unyielding King under the Mountain, stands as a paragon of dwarven resilience and valor. Renowned for his unwavering determination and tactical prowess, Dáin is a battle-hardened leader who inspires loyalty and courage in his kin. A veteran of countless conflicts, he led the dwarves of the Iron Hills with unmatched strength, bringing stability and prosperity to his people. His resolve in the face of adversity and his dedication to the defense of Erebor and its allies ensure that the legacy of Durin's folk endures through the darkest days of the Third Age." />
```

The clan that has to exist for `faction=` to mean anything, from `characters/clans.xml`:

<!-- example file="Main/_Module/ModuleData/characters/clans.xml" id="clan_erebor_1" -->

```xml
  <Faction
		id="clan_erebor_1"
		initial_home_settlement="Settlement.town_E1"
		name="{=aom_clan_erebor_1_name}Bit Durin"
		tier="6"
		owner="Hero.lord_E1_1"
		culture="Culture.erebor"
		super_faction="Kingdom.erebor"
		is_noble="true"
		color="FF153F1C"
		color2="FF964309"
		default_party_template="PartyTemplate.kingdom_hero_party_erebor_erebor_1_template"
		banner_key="11.100.75.4345.4345.764.764.1.0.0.521.172.100.51.51.652.641.0.0.267.521.172.100.45.45.583.712.0.0.267.521.172.100.35.35.602.802.0.0.267.521.172.100.51.51.877.641.0.1.87.521.172.100.45.45.944.712.0.1.87.521.172.100.35.35.925.802.0.1.87.24019.31.240.334.334.764.854.1.0.0.24510.31.240.167.167.764.589.1.0.0" />
```

And the strip-then-set shape every `heroes.xslt` template uses, for a lord whose id came from
vanilla:

<!-- excerpt file="Main/_Module/ModuleData/heroes.xslt" -->

```xml
	<xsl:template match="Hero[@id='lord_1_1']">
		<xsl:copy>
			<xsl:apply-templates select="@*[local-name() != 'text' and local-name() != 'spouse']"/>
			<xsl:attribute name="text">{=TAOM_hero_1_1}Brenin Wulf rules the wild clans of Dunland with an iron fist. Known as the Ironhand, he united the scattered hill tribes through conquest and cunning. He dreams of reclaiming the lands taken by the Horse-lords of Rohan.</xsl:attribute>
			<xsl:attribute name="spouse">Hero.lord_1_2</xsl:attribute>
			<xsl:apply-templates select="node()"/>
		</xsl:copy>
	</xsl:template>
```

The three attributes to change first:

1. **`id`** on all three records. It is the pin. It never changes after the lord ships, because saves
   store it and clans, kingdoms and other heroes point at it by string.
2. **`faction`** on the `<Hero>`, matching an `id` that exists in `characters/clans.xml`. This is
   what puts the lord in a family, on the map, and in the encyclopedia.
3. **`name`** on the `<NPCCharacter>`, in `{=key}English` form. The `<Hero>` never carries a name;
   it copies the character's.

Note what the example shows about `skill_template`: `lord_E1_1` has both
`skill_template="SkillSet.taom_dwarf_king_skills"` and an inline `<skills>` block. The inline block
is dead. The engine reads the template and the guard at `BasicCharacterObject.cs:355` skips the
child. Editing those 18 numbers changes nothing in game.

## Recipes: Add / Modify / Delete

### Add

A new named lord for an existing culture. The clan must already exist; if it does not, do
[Clans](clans.md) first, and a whole new kingdom is [Add a kingdom](recipe-add-a-kingdom.md).

1. Pick an id nothing uses yet. TAOM's pattern is `lord_{PREFIX}{CLAN_N}_{MEMBER_N}`, where the
   prefix is the culture's two-character code (`E` for Erebor in `lord_E1_1`). Confirm it is free:
   `python -c "import xml.etree.ElementTree as ET;print(any(e.get('id')=='lord_E9_1' for e in ET.parse('Main/_Module/ModuleData/characters/lords.xml').getroot()))"`.
2. Add the `<NPCCharacter>` to `Main/_Module/ModuleData/characters/lords.xml`. Copy a neighbouring
   lord of the same culture wholesale, then change `id`, `name`, `age`, `is_female`, the
   `<BodyProperties key>` and `skill_template`. Keep `occupation="Lord"` and `is_hero="true"`.
3. If you flipped `is_female`, change `<beard_tags>` and the `<BodyProperties key>` in the same edit.
   The three are one unit.
4. Point `skill_template` at a set that exists in
   [`Main/_Module/ModuleData/taom_lord_skill_sets.xml`](../../Main/_Module/ModuleData/taom_lord_skill_sets.xml),
   or add one there first. Do not hand-edit the inline `<skills>` block; it is ignored whenever the
   template resolves.
5. Point `<Equipments>` at rosters that exist under
   [`Main/_Module/ModuleData/equipmentsets/`](../../Main/_Module/ModuleData/equipmentsets), the way
   `dain_bat_equipment` does. See [Equipment rosters](equipment-rosters.md).
6. Add the matching `<Hero>` to `Main/_Module/ModuleData/characters/heroes.xml` with the **same id**
   and a `faction="Faction.<clan_id>"` that resolves. Everything else on the row is optional.
7. If the lord should appear in a clan's roster with a family, add `father`, `mother` or `spouse`
   pointing at hero ids that exist. Write `spouse` on one partner only.
8. English is done at this point, because the inline `{=key}English` literal is the English text.
   For the other twelve languages, add a `<string>` row for the name key to
   [`Main/_Module/ModuleData/taom_xslt_strings.xml`](../../Main/_Module/ModuleData/taom_xslt_strings.xml)
   and run the translator. See [Strings and localization](strings-and-localization.md).

Check: `python tools/validate_moduledata.py`
Takes effect: new campaign only (`Heroes` is loaded inside `if (!isSavedCampaign)`, `SandBoxManager.cs:363-366`)
Code: No code changes needed

### Modify

#### Rename a lord

1. Edit `name="{=key}New Name"` on the `<NPCCharacter>`. If the id is one TAOM inherited from
   vanilla and the row lives in `lords.xslt` rather than `characters/lords.xml`, edit the
   `<xsl:attribute name="name">` there instead. When the id exists in **both**, the plain XML wins,
   so edit `characters/lords.xml` or your change will not show.
2. Keep the key stable and change only the literal after `}`. Changing the key orphans twelve
   translations.
3. If the key is registered in `taom_xslt_strings.xml`, update the row there too, then run the
   translator. `tools/translate_with_claude.py` refills only rows still equal to English, so changing
   the English text under an existing key propagates to nothing on its own.
4. Grep the whole data tree for the old name, not just `characters/`: settlement flavour text and
   biographies mention lords by name.
   `grep -rn "Old Name" Main/_Module/ModuleData/`

Check: `python tools/oneoff/sync_lord_name_fallbacks.py`
Takes effect: new campaign only (`Name` is copied onto the hero once, at hero creation, `Hero.cs:2243`)
Code: No code changes needed

#### Re-culture a lord

1. Change `culture="Culture.<new>"` on the `<NPCCharacter>`.
2. Move the lord to a clan of that culture by changing `faction=` on the `<Hero>`, and check the
   clan's own `culture` and `super_faction` agree. A lord in a clan of a different culture reads as
   a defection, not a re-culture.
3. Re-point `skill_template` and `<Equipments>` at that culture's sets, and re-check `race`: the
   culture change does not move the body.
4. Confirm the new culture owns at least one settlement. A culture with no settlement makes vanilla
   `SpawnLordParty` fall through to an unguarded `Settlement.All.First(x => x.Culture == hero.Culture)`
   and crash to desktop on the daily clan tick. TAOM guards it with `Patch65`, and the validator has
   a gate for it. See [lord spawn guard](../features/lord-spawn-guard.md).

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE`
Takes effect: new campaign only (`Culture` is copied onto the hero once, `Hero.cs:2244`)
Code: No code changes needed

#### Change a family link

1. Decide which side owns the statement. `spouse` is reciprocal and `father` and `mother` both push
   the child into the parent's children list, so write each relationship on **one** row only.
2. If the row is a `heroes.xslt` template over a vanilla id, the template must **strip every family
   attribute it does not itself set**. Anything the `select="@*[...]"` filter does not exclude is
   copied from vanilla, and vanilla's value is about a different character. Eight defects hid there
   until the transform was actually run, including a lord married to his own mother.
3. To remove a family entirely (the Nazgûl case), strip `spouse`, `father` and `mother` in the
   template and add nothing back.

Check: `dotnet test TAOM.Tests --filter LordFamilyTransform -p:DisableModuleCopy=true -p:ModuleId=`
Takes effect: new campaign only (a save keeps its serialized family, see [nazgul family](../features/nazgul-family.md))
Code: No code changes needed

### Delete

Do not delete a lord who has shipped. Retire him instead, and keep both rows.

1. Add `is_obsolete="true"` to the `<NPCCharacter>`. That is the engine's supported retirement
   switch; vanilla uses it 30 times in `SandBoxCore/ModuleData/obsolete_characters.xml`.
2. Leave the `<Hero>` row in place. Deleting it while the character stays is the mirror-image
   mistake described under Gotchas, and any surviving `Hero.<id>` reference from a clan `owner=` or
   another hero's `father=` becomes a ghost.
3. Remove the lord from anything that points at him by id: his clan's `owner=`, family attributes on
   other heroes, party templates.
4. If the lord was a clan's only member, give the clan a new `owner` or retire the clan too.

Check: `python tools/validate_moduledata.py`
Takes effect: new campaign only, and existing saves keep the retired lord
Code: No code changes needed

For content retirement across the mod, see [Retire content](recipe-retire-content.md).

## Gotchas: what fails silently and what crashes

- **A `<Hero>` whose id matches no `<NPCCharacter>` truncates the rest of the file.**
  `Hero.Deserialize` looks the character up and immediately reads `characterObject.Age`, so a
  missing character throws a null reference; `MBObjectManager.LoadXML` wraps the whole parse in
  `try { ... } catch (Exception) { }` with an empty body, so heroes after the bad one are never
  created. No crash, no message, just a world missing most of its lords. The culprit is the last
  hero that did load, plus one. `Hero.cs:1806-1807`, `MBObjectManager.cs:786-796`.
- **The mirror case is soft, not hard.** An `<NPCCharacter is_hero="true">` with no `<Hero>` row is
  not a hero at all: `CharacterObject.IsHero` returns `_heroObject != null`, and nothing fills that.
  He degrades into an ordinary troop record with no encyclopedia page, no clan and no party. TAOM
  ships 23 of these today and none is referenced as `Hero.<id>`, which is why they cost nothing.
  `CharacterObject.cs:294`, `Hero.cs:1238`.
  <!-- measured: python one-liner comparing is_hero ids in characters/lords.xml against Hero ids in characters/heroes.xml and SandBox/ModuleData/heroes.xml 2026-09-05 -->
- **A typo in `father`, `mother`, `spouse` or `faction` does not error.** `Hero` and `Clan` are both
  registered with `autoCreateInstance: true`, so an unknown id invents an empty placeholder. It is
  dropped later with one log line, `Null object reference found with ID: <id>`, and the game runs;
  anything that touched it first can crash. Grep the startup log in
  `Documents\Mount and Blade II Bannerlord\logs\rgl_log_*.txt` for that string after any family
  edit. `MBObjectManager.cs:1437-1456`; and see [Load order and dependencies](load-order-and-dependencies.md).
- **The dot in a reference is mandatory and the prefix is the element name, not the class name.**
  `faction="Faction.clan_erebor_1"` is right, `faction="Clan.clan_erebor_1"` is wrong, and a value
  with no dot at all throws `MBInvalidReferenceException`. `Kingdom.` resolves to a real object that
  the `as Clan` cast turns into null, which then crashes. `MBObjectManager.cs:1517-1535`,
  `Hero.cs:1834-1835`.
- **An inline `<skills>` block on a lord is dead whenever `skill_template` resolves.** This is the
  bug that motivated TAOM's SkillSet system, and it is visible in the worked example above: Dáin has
  both, and only the template counts. `BasicCharacterObject.cs:337`, `:355`;
  [lord skills authoring](../ai-includes/lord-skills-authoring.md).
- **A vanilla id carries its sex until you change three things.** `is_female`, `<beard_tags>` and the
  `<BodyProperties key>` travel as one unit. Flipping the attribute alone gives a bearded woman;
  the biography in `heroes.xslt` saying son, daughter or wife is the cheapest spec to check against.
  `LordNameAndSexConsistencyTests` pins the female-with-beard half.
  [lord skills authoring](../ai-includes/lord-skills-authoring.md).
- **`tools/complete_lords_xslt.py --apply` restores vanilla `is_female` when you delete the
  attribute**, because an override counts only when present and the male convention is omission. The
  id must go in that script's `GENDER_OVERRIDES` with value `None`. The same script's `ATTR_ORDER`
  does not contain `race`, so `--apply` silently deletes every `race` attribute from `lords.xslt`,
  Sauron's included. `characters/lords.xml` has no regenerator and is never at risk.
  [lord identity reconciliation](../features/lord-identity-reconciliation.md).
- **The two localization tiers do not interoperate.** Names and biographies edited in the
  stylesheets touch keys that twelve languages already carry. Names and biographies edited in
  `characters/lords.xml` and `characters/heroes.xml` mostly do not: 179 of 1184 name keys and 6 of
  465 biography keys are registered in `taom_xslt_strings.xml`. There is no English `Languages/`
  folder, so the inline literal is the English text and an unregistered key simply shows English
  everywhere. [lord identity reconciliation](../features/lord-identity-reconciliation.md).
  <!-- measured: python one-liner matching {=KEY} prefixes in characters/lords.xml and characters/heroes.xml against <string id> rows in taom_xslt_strings.xml 2026-09-05 -->
- **Nothing in this chapter reaches an existing save.** `Heroes`, `Kingdoms` and `Factions` are
  loaded only when `isSavedCampaign` is false, and `Name`, `Culture`, `IsFemale` and the skills are
  copied onto the hero once, at creation. Verification needs a full application restart and a fresh
  campaign, because the game globs and registers ModuleData at process launch.
  `SandBoxManager.cs:363-374`, `Hero.cs:2243-2251`.
- **A placeholder `banner_key` on the clan is a real shipped defect class**, not a cosmetic one:
  established clans have keys hundreds of characters long, and anything under 100 characters is a
  placeholder that renders as a generic block. Compare against the clan the new one was derived from.
  [kingdom creation](../features/kingdom-creation.md).

### Not answered anywhere in TAOM

- **How to author a `banner_key` from scratch.** The number-group grammar is not decoded in any TAOM
  doc. The places to look are `Banner.cs` (`Deserialize` and `TryGetBannerDataFromCode`) in the
  decompile, [banner icon generation](../reference/banner-icon-generation.md) for the icon and colour
  id pools, and a working key such as `clan_erebor_1`'s above. Copying an existing key is the only
  documented method.
- **What the minimum viable kingdom is** in clans, lords and settlements before it stops crashing.
  `kingdom-creation.md` lists the crash classes and `culture-playability-wiring.md` has a 14-row
  fatal-or-silent checklist, but neither states a floor. See
  [kingdom creation](../features/kingdom-creation.md) and
  [culture playability wiring](../features/culture-playability-wiring.md).
- **What `tier` on a clan actually changes.** The filing order says to create the tier-6 clan first,
  and `clans.xml` ships rows from tier 1 to tier 6, but no TAOM doc states the range or the effect.
  Read `Clan.Deserialize` in the decompile, and see [Clans](clans.md) when that chapter lands.
- **Whether `preferred_upgrade_formation` reaches a formation several tiers down an upgrade tree.**
  `CharacterHelper.SearchForFormationInTroopTree` was not read for this chapter, so the search depth
  is unknown. No TAOM lord sets the attribute, so nothing depends on the answer today.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 1001 `<Hero>` rows in `characters/heroes.xml` | `python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/characters/heroes.xml').getroot();print(len(r))"` | 2026-09-05 |
| Hero attribute usage: `id` 1001, `faction` 1001, `text` 465, `spouse` 207, `father` 79, `mother` 47, `alive` 1 | `python -c "import xml.etree.ElementTree as ET;from collections import Counter;r=ET.parse('Main/_Module/ModuleData/characters/heroes.xml').getroot();print(sorted(Counter(k for e in r for k in e.attrib).items()))"` | 2026-09-05 |
| 1184 `<NPCCharacter>` rows in `characters/lords.xml`, all 1184 `occupation="Lord"`, 504 `is_female="true"` | `python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/characters/lords.xml').getroot();print(len(r),sum(1 for e in r if e.get('occupation')=='Lord'),sum(1 for e in r if e.get('is_female')=='true'))"` | 2026-09-05 |
| `race` on those 1184: absent 525, `orc` 151, `dg_uruk` 126, `pale_uruk` 100, `goblin` 80, `elf` 63, `uruk` 59, `uruk_hai` 39, `dwarf` 36, `berserker` 5 | `python -c "import xml.etree.ElementTree as ET;from collections import Counter;r=ET.parse('Main/_Module/ModuleData/characters/lords.xml').getroot();print(Counter(e.get('race') for e in r).most_common())"` | 2026-09-05 |
| Registration lines 96, 106, 139, 148 and 157 in `SubModule.xml` | `grep -n 'path="lords"\|path="heroes"\|path="characters/heroes"\|path="characters/lords"\|path="characters/clans"' Main/_Module/SubModule.xml` | 2026-09-05 |
| 145 `<Faction>` rows in `characters/clans.xml` | `python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/characters/clans.xml').getroot();print(len(r))"` | 2026-09-05 |
| 397 templates in `lords.xslt` and 400 in `heroes.xslt`, one of each being the identity template | `grep -c '<xsl:template' Main/_Module/ModuleData/lords.xslt Main/_Module/ModuleData/heroes.xslt` | 2026-09-05 |
| 397 live `<Hero>` rows in vanilla's file, and 23 `is_hero="true"` characters in `characters/lords.xml` with no `<Hero>` row in either file | `BANNERLORD_GAME_MODULES=... python -c "import os,xml.etree.ElementTree as ET;V=os.path.join(os.environ['BANNERLORD_GAME_MODULES'],'SandBox','ModuleData','heroes.xml');vh={e.get('id') for e in ET.parse(V).getroot()};h={e.get('id') for e in ET.parse('Main/_Module/ModuleData/characters/heroes.xml').getroot()};l=ET.parse('Main/_Module/ModuleData/characters/lords.xml').getroot();print(len(vh),len([e for e in l if e.get('is_hero')=='true' and e.get('id') not in h and e.get('id') not in vh]))"` | 2026-09-05 |
| 1449 `<string>` rows in `taom_xslt_strings.xml`; 179 of 1184 lord name keys registered | `python -c "import xml.etree.ElementTree as ET,re;reg=set(re.findall(r'<string id=\"([^\"]+)\"',open('Main/_Module/ModuleData/taom_xslt_strings.xml',encoding='utf-8').read()));l=ET.parse('Main/_Module/ModuleData/characters/lords.xml').getroot();k=[re.match(r'\{=([^}]+)\}',e.get('name','')).group(1) for e in l if e.get('name','').startswith('{=')];print(len(reg),len(k),sum(1 for x in k if x in reg))"` | 2026-09-05 |
| 6 of 465 biography keys registered | `python -c "import xml.etree.ElementTree as ET,re;reg=set(re.findall(r'<string id=\"([^\"]+)\"',open('Main/_Module/ModuleData/taom_xslt_strings.xml',encoding='utf-8').read()));h=ET.parse('Main/_Module/ModuleData/characters/heroes.xml').getroot();k=[re.match(r'\{=([^}]+)\}',e.get('text')).group(1) for e in h if (e.get('text') or '').startswith('{=')];print(len(k),sum(1 for x in k if x in reg))"` | 2026-09-05 |
| 30 `is_obsolete="true"` in vanilla's obsolete-characters file | `grep -o 'is_obsolete="true"' "<install>/Modules/SandBoxCore/ModuleData/obsolete_characters.xml" \| wc -l` | 2026-09-05 |
| 0 `is_obsolete` anywhere in TAOM ModuleData | `grep -rho 'is_obsolete="[^"]*"' Main/_Module/ModuleData/ \| sort \| uniq -c` | 2026-09-05 |
| `validate_moduledata.py` registry: 5,900 items, 5,291 NPCCharacters, 40 cultures, 476 party templates | `python tools/validate_moduledata.py --code LANDLESS_CULTURE` | 2026-09-05 |

## Read next

- [lord identity reconciliation](../features/lord-identity-reconciliation.md) for the two-file merge, the localization tiers and the stylesheet strip rule.
- [kingdom creation](../features/kingdom-creation.md) for the thirteen-file filing order, the naming conventions table and the known crash classes.
- [lord skills authoring](../ai-includes/lord-skills-authoring.md) for the symptom-to-layer table and the full trap list behind the SkillSet system.
- [nazgul family](../features/nazgul-family.md) for removing a predefined family and for blocking runtime marriage.
- [lord spawn guard](../features/lord-spawn-guard.md) for the landless-culture crash a re-culture can cause.
- [uncapturable heroes](../features/uncapturable-heroes.md) for the hero-set config and the `race` attribute distribution across the Nine.
- [culture playability wiring](../features/culture-playability-wiring.md) for the fatal-or-silent checklist a re-culture has to satisfy.
- [moduledata validation](../features/moduledata-validation.md) for what the validator walks and what it does not.
- [TRANSLATOR_GUIDE](../localization/TRANSLATOR_GUIDE.md) for the string schema and the `language_data.xml` registration a new key needs.
- [tools README](../../tools/README.md) for `complete_lords_xslt.py`, `sync_lord_name_fallbacks.py`, `analyze_lord_balance.py` and `author_elf_lords.py`, the copy-me pattern for generating a culture's lords.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/clans.md](./clans.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/kingdoms.md](./kingdoms.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/skill-sets.md](./skill-sets.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
