# Clans

## What this file is

A clan is one house, one mercenary company or one bandit gang: the thing that owns lords, owns fiefs, flies a banner and fields parties. The engine calls the element `<Faction>` even though the class is `Clan`, and TAOM writes 145 of them in `Main/_Module/ModuleData/characters/clans.xml` while rewriting 88 vanilla ones through `Main/_Module/ModuleData/spclans.xslt` and deleting 5. <!-- measured: python ElementTree count on characters/clans.xml, and a python diff of the Faction[@id] templates in spclans.xslt against the vanilla row ids 2026-09-05 --> After the two files are merged the world holds 235 clans, and every one of them is loaded on a new campaign only.

## Where it lives and how it is registered

TAOM writes clan data in two places and reads a third.

| File | What it holds | Registered at |
|---|---|---|
| [`Main/_Module/ModuleData/characters/clans.xml`](../../Main/_Module/ModuleData/characters/clans.xml) | 145 brand-new `<Faction>` rows, 2,112 lines | `<XmlName id="Factions" path="characters/clans"/>`, [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) line 139 |
| [`Main/_Module/ModuleData/spclans.xslt`](../../Main/_Module/ModuleData/spclans.xslt) | 98 `Faction[@id]` templates over vanilla rows, 1,166 lines | `<XmlName id="Factions" path="spclans"/>`, `SubModule.xml` line 88 |
| `SandBox/ModuleData/spclans.xml` | vanilla's 95 rows, the document the stylesheet transforms | vanilla `SandBox/SubModule.xml` |

This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. Vanilla clan data is the one case where `SandBox/ModuleData/` is the correct reference rather than `SandBoxCore/ModuleData/`, because SandBoxCore ships no clan file at all. <!-- measured: ls on both ModuleData folders filtered for clan or faction 2026-09-05 -->

- **Root element `<Factions>`, row element `<Faction>`.** `Campaign.cs:1543` registers `RegisterType<Clan>("Faction", "Factions", 18u, autoCreateInstance: true, isTemporary: true)`. The loader matches the root tag and then deserializes every non-comment child of it whatever that child is called, so a mistyped row tag becomes a clan instead of an error.
- **The engine class is `TaleWorlds.CampaignSystem.Clan`**, and everything below comes from its `Deserialize` at `Clan.cs:859-927`.
- **The `spclans` row at line 88 carries no XML.** TAOM ships no `Main/_Module/ModuleData/spclans.xml`; that registration exists purely so the engine picks up the stylesheet beside it. `CreateMergedXmlFile` (`MBObjectManager.cs:966-982`) applies file `i`'s stylesheet to the document accumulated so far and never applies the one paired with file 0, so the stylesheet has to ride on a row that comes after vanilla's. Order and merge detail: [load-order-and-dependencies](load-order-and-dependencies.md).
- **When it loads: new campaign only.** `SandBoxManager.cs:371-375` wraps `LoadXML("Kingdoms")` and `LoadXML("Factions")` in `if (!isSavedCampaign)`. A tier change, a recolour, a renamed house or a new party template is invisible in an existing save.
- **Load order:** NPCCharacters, Heroes, Kingdoms, Factions, WorkshopTypes, LocationComplexTemplates, Settlements (`SandBoxManager.cs:362-381`). Settlements load after clans, so `initial_home_settlement` is always a forward reference.

## Attributes

Two classes read attributes off one `<Faction>` element: `MBObjectBase.Deserialize` first, then `Clan.Deserialize`. Only `id` and `name` are read without a null check, so those two are the only ones whose absence crashes campaign start.

**Every object reference must be written in the dotted `Type.id` form.** `ReadObjectReferenceFromXml` splits the value on `.` and throws `MBInvalidReferenceException` when there is no dot (`MBObjectManager.cs:1517-1534`). So `culture="Culture.erebor"`, never `culture="erebor"`. The prefixes are in [id-cheatsheet](id-cheatsheet.md).

<!-- engine-table type="TaleWorlds.ObjectSystem.MBObjectBase" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none; the read is `node.Attributes["id"].Value` with no null check, so a missing id throws and takes the rest of the merged document with it | The clan's permanent codename, for example `clan_erebor_1`. Heroes, lords, kingdoms, party templates and the heraldry specs all point at this string. Once it ships in a save, changing it makes a new clan and orphans the old one. | `MBObjectBase.cs:61` |

<!-- engine-table type="TaleWorlds.CampaignSystem.Clan" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Clan.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `name` | string, localisable | yes in practice | none; the read is unguarded like `id` and crashes the load the same way | Display name on the map, in the encyclopedia and in dialogue. Written `{=aom_clan_erebor_1_name}Bit Durin`. See [strings-and-localization](strings-and-localization.md). | `Clan.cs:871` |
| `short_name` | string, localisable | no | falls back to `name` | The informal form. Used by the "X of the Y" hero-naming path, which only runs for minor-faction, sect, mafia, nomad and mercenary clans, so it matters mostly for those. | `Clan.cs:871` |
| `tier` | int | no | 1 | The single biggest balance dial on a clan. Clamped to 0..6 by the `Tier` setter (`Clan.cs:322-335`); `tier="9"` silently becomes 6. Sets starting renown, party limit and companion slots, listed below the table. | `Clan.cs:864` |
| `owner` | `Hero.<hero_id>` | no | null, a leaderless clan | The head of the house. Setting it also pushes that hero into this clan (`SetLeader`, `Clan.cs:948-955`), so membership is stated once, not twice. All 8 clans in `characters/clans.xml` without an owner are the bandit gangs. | `Clan.cs:862` |
| `super_faction` | `Kingdom.<kingdom_id>` | no | null, the clan is independent and is its own map faction | Which kingdom the clan is a vassal of at game start. Assigning it hands the clan's heroes, fiefs and parties to the kingdom and repaints its banner in the kingdom's colours. | `Clan.cs:863` |
| `culture` | `Culture.<culture_id>` | no by the parser, yes in practice | null, and `Clan.BasicTroop` and `Clan.DefaultPartyTemplate` both dereference it on the next tick | Drives the clan's fallback troops and party template, its heroes' faces, names and voices, and for a bandit clan which hideouts it can spawn from. Validator code `LANDLESS_CULTURE` fires on a `<Faction>` whose culture owns no settlement. | `Clan.cs:872` |
| `initial_home_settlement` | `Settlement.<settlement_id>` | no | skipped entirely, `InitialHomeSettlement` stays null | A seed, not a fixture: `SetInitialHomeSettlement` immediately re-picks the real home from the clan's own fiefs, then its kingdom's, then the map (`Clan.cs:957-975`). It is the literal answer only for rebel clans, bandit clans and clans that own nothing. For a bandit clan it is what puts the gang near its hideout. | `Clan.cs:866` |
| `default_party_template` | `PartyTemplate.<template_id>` | no | null, and `DefaultPartyTemplate` then falls back to `Culture.DefaultPartyTemplate` (`Clan.cs:112-122`) | What troops this house actually fields when its lords raise a party. 113 of the 145 TAOM rows name a clan-specific template, 24 name their culture's default and 8 name a raider template. Also decides whether the clan can sail: `HasNavalNavigationCapability` is true only when the template lists `ShipHulls` (`Clan.cs:124`). See [party-templates](party-templates.md). | `Clan.cs:888` |
| `color` | hex `AARRGGBB`, no prefix | no | `4291609515`, which is `FFCCC3AB`, a pale stone grey | The clan's primary colour. In TAOM it is the direct lever on its troops' battlefield armour tint, because `Patch23_BannerColorPersistence` rewrites agent clothing from the spawning party leader's clan with no `MapFaction` hop. Never write `FFFFFFFF`; see "Gotchas". | `Clan.cs:879` |
| `color2` | hex `AARRGGBB`, no prefix | no | `4291609515` | The secondary colour. Same consumers. White here is fine and 7 XSLT blocks use it. | `Clan.cs:880` |
| `banner_key` | dot-separated banner code | no | the engine rolls a deterministic single-icon banner from the clan's own id, so you get a banner but no control over it | The heraldry. Groups of ten numbers: the first group is the background, each further group is one icon layer. Whatever colours the code bakes in can be overwritten right after parsing; see "Gotchas". Grammar and icon pools: [banners-and-heraldry](banners-and-heraldry.md). | `Clan.cs:890` |
| `is_noble` | bool | no | untouched, which is `false` on a fresh clan; this is the only flag written through an `if` guard rather than assigned unconditionally | Marks an aristocratic house rather than a gang or a company. Gates noble dialogue and greetings and the encyclopedia "Noble" filter. 137 of the 145 TAOM rows set it. | `Clan.cs:874` |
| `is_bandit` | bool | no | false | Puts the clan into `Clan.BanditFactions` (`Clan.cs:438-450`), which is what the spawner iterates, and makes every non-outlaw faction hostile to it. Adding one without hideouts of its culture is a new-game crash; see "Gotchas". | `Clan.cs:881` |
| `is_minor_faction` | bool | no | false | A small independent company rather than a kingdom clan. Changes the party limit to `clamp(tier, 1, 4)`, allows mercenary contracts, and switches the hero pool to `<minor_faction_character_templates>`. No row in `characters/clans.xml` sets it; TAOM's 14 minor factions are vanilla rows the stylesheet renames. | `Clan.cs:882` |
| `is_outlaw` | bool | no | false | Exempts the faction from the automatic "everybody starts at war with bandits" rule, and lets same-culture outlaw minor factions stay neutral with kingdoms. Vanilla pairs it with `is_bandit` on gangs and uses it alone on outlaw minor factions; all 8 TAOM bandit clans set both. | `Clan.cs:883` |
| `is_sect` | bool | no | false | Flavour only, and only meaningful next to `is_minor_faction`: the encyclopedia calls the clan a religious sect, town preachers can support it, and its heroes get sect dialogue. One vanilla row uses it. | `Clan.cs:884` |
| `is_mafia` | bool | no | false | Flavour only: "a secret society". Town gang leaders can support it and its heroes demand tribute in dialogue. Five vanilla rows use it. | `Clan.cs:885` |
| `is_clan_type_mercenary` | bool | no | false | Flavour only: "a mercenary company". Changes the clan-screen presentation and keeps the clan out of ordinary vassal barters. Four vanilla rows use it. | `Clan.cs:886` |
| `is_nomad` | bool | no | false | Flavour only: "a nomadic clan", plus nomad dialogue. Four vanilla rows use it. | `Clan.cs:887` |
| `text` | string, localisable | no | an empty `TextObject` | The encyclopedia blurb. No gameplay effect. Wrap it in `{=key}` so the translator picks it up. | `Clan.cs:889` |

**There is no `renown` attribute.** `Clan.cs:865` overwrites renown at load with `CalculateInitialRenown`, a roll between this tier's floor and 40 percent of the way to the next. The floors are `{0, 50, 150, 350, 900, 2350, 6150}` (`DefaultClanTierModel.cs:11, 43-49`). Afterwards renown only pushes tier up, never down. So `tier` is the whole start-of-game power dial, and it is coarse: tier 4 rolls roughly 900 to 1,480, tier 5 roughly 2,350 to 3,870.

<!-- engine-ref type="TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultClanTierModel.cs" lines="27-33,117-140,165-168" -->

| Tier | Renown floor | Field parties (ordinary clan) | Field parties (minor faction) | Companion slots |
|---|---|---|---|---|
| 0 | 0 | 1 | 1 | 3 |
| 1 | 50 | 1 | 1 | 4 |
| 2 | 150 | 1 | 2 | 5 |
| 3 | 350 | 2 | 3 | 6 |
| 4 | 900 | 2 | 4 | 7 |
| 5 | 2,350 | 3 | 4 | 8 |
| 6 | 6,150 | 3 | 4 | 9 |

Tier 1 is the mercenary-eligible floor and tier 2 the vassal-eligible floor (`DefaultClanTierModel.cs:31-33`).

## Child elements

Both are optional. The loop that reads them is `Clan.cs:904-926`.

<!-- engine-table type="TaleWorlds.CampaignSystem.Clan" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Clan.cs" method="Deserialize" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<minor_faction_character_templates>` | wrapper, no attributes of its own | no, at most one | the list is empty | The hero pool a minor faction spawns its lords from. Missing is not a crash: the engine asserts "templates are empty!" and stops spawning that faction's lords (`HeroSpawnCampaignBehavior.cs:358-361`). Each of TAOM's 14 minor factions carries exactly 4 children. | `Clan.cs:906` |
| `@id` on each child of that block | `NPCCharacter.<character_id>` | yes on each child | the entry becomes null | Names one hero template. Vanilla writes the child as `<template id="NPCCharacter.x"/>` and the schema requires that tag name, but the inner loop reads `Attributes["id"]` off every child node without checking its tag, so an XML comment inside the block is a `NullReferenceException`. | `Clan.cs:910` |
| `<relationship>` | element, repeatable in code | no | no starting relation is declared | A starting war or peace with one other faction. Note the singular tag directly under `<Faction>`; a `<Kingdom>` wraps its own in a plural `<relationships>` block, so do not copy that shape here. | `Clan.cs:914` |
| `@clan` | `Faction.<clan_id>` | one of `clan` or `kingdom` | the engine reads `kingdom` instead | The other clan. | `Clan.cs:916` |
| `@kingdom` | `Kingdom.<kingdom_id>` | one of `clan` or `kingdom` | none | The other kingdom, read only when `clan` is absent. | `Clan.cs:916` |
| `@value` | int | yes | none; `Convert.ToInt32(...InnerText)` has no null check, so omitting it crashes campaign start | Only the sign is used: negative declares war, zero or positive sets neutral. The magnitude is thrown away. | `Clan.cs:917` |

No TAOM clan row uses either element. Both come from vanilla rows the stylesheet passes through. <!-- measured: python ElementTree child-element histogram over the 145 Faction rows in characters/clans.xml, which returns none 2026-09-05 -->

## Worked example

The ruling clan of Erebor, verbatim from the shipped file. It is the first block of 12 lines starting at line 457.

<!-- example file="Main/_Module/ModuleData/characters/clans.xml" id="clan_erebor_1" -->

```xml
  <!-- Bit Durin - The ruling clan -->
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

The three things a reader changes first:

1. **`tier`.** Six here because this is the kingdom's ruling house. It buys the renown roll, three field parties and nine companion slots, and nothing else on the row changes with it. Dropping a house from 6 to 4 is the cheapest way to make it weaker at game start.
2. **`color` and `color2`.** Eight bare hex digits, alpha first, no `0x`. These tint the party marker and, through `Patch23_BannerColorPersistence`, the armour of every troop in a party this clan's lord leads.
3. **`default_party_template`.** The roster the lords of this house raise. `kingdom_hero_party_erebor_erebor_1_template` is a clan-specific variant; point it at another id in [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml) to change what the house fields without touching a troop file.

The banner code is one background group of ten numbers followed by eight icon groups of ten. 79 of the 145 shipped rows carry the two-group minimum, a background plus a single icon, which is the shape `docs/features/kingdom-creation.md:526-534` calls a placeholder. <!-- measured: python count of the dot-separated number groups on every banner_key in characters/clans.xml 2026-09-05 -->

## Recipes: Add / Modify / Delete

### Add

A new TAOM clan is one `<Faction>` row, but it only works if five things it points at already exist. Add it after the culture and the lord, before you expect to see it in game.

1. **Copy a neighbouring row whole** in [`Main/_Module/ModuleData/characters/clans.xml`](../../Main/_Module/ModuleData/characters/clans.xml) and edit it in place. Do not start from an empty element: the file is CRLF with no BOM and tab-indented attributes, and matching the neighbour keeps all three.
2. **`id` and `name` first.** The convention is `clan_{culture_id}_{N}` and `{=aom_clan_{culture_id}_{N}_name}`, per the naming table at `docs/features/kingdom-creation.md:67-81`. Register the string key in [`Main/_Module/ModuleData/taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml) before you use it.
3. **`owner` must already exist as a `Hero`.** A clan with a dangling owner does not throw at load; the reference becomes a hollow placeholder that the engine later deletes with `Null object reference found with ID:` in the log (`MBObjectManager.cs:1455`). Author the lord first, per [lords-and-heroes](lords-and-heroes.md).
4. **`culture` must own at least one settlement.** This is the `LANDLESS_CULTURE` error and it is a real daily-tick crash, not a warning: see `docs/features/lord-spawn-guard.md`.
5. **`super_faction` if the clan is a vassal.** Leave it out for an independent house or a gang.
6. **`initial_home_settlement` must be an id in the LIVE map file** `TAOM_Map/ModuleData/settlements.xml`, not the stale shadow copy in this repo. Settlements load after clans, so a typo here is never caught at clan-load time.
7. **`default_party_template`** pointing at a real id in `taom_partyTemplates.xml`, or leave it out and inherit the culture's.
8. **`banner_key`, `color`, `color2`.** Copy a working key from a sibling clan rather than inventing one. For a bandit gang add `is_bandit="true" is_outlaw="true"`, point `initial_home_settlement` at a hideout of the clan's own culture, and read the bandit paragraph under "Gotchas" first.

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE` then `python tools/clan_registry.py` and confirm the new id appears under the right culture
Takes effect: new campaign only
Code: No code changes needed

### Modify

#### Retune or recolour a TAOM clan

1. Open [`Main/_Module/ModuleData/characters/clans.xml`](../../Main/_Module/ModuleData/characters/clans.xml) and grep for the id. It sits on its own line under `<Faction`, so grep the id, not the opening tag.
2. Change `tier`, `color`, `color2` or `default_party_template` in place. Colours are eight bare hex digits with the alpha byte first, and every one of the 145 shipped rows is written that way. The schema also permits a `0x` prefix (`Factions.xsd:80-86`) but nothing in TAOM uses one.
3. **Never write `color="FFFFFFFF"`.** That value is `uint.MaxValue`, which is the engine's own "unset" marker for cloth colour, so the colour-persistence patch reads it as "no clan colour" and bails. Use `FFFEFEFE`, which is one step off white and looks identical. `color2` is unaffected. The full chain is in [`docs/features/clan-heraldry.md`](../features/clan-heraldry.md).
4. If the clan has a heraldry spec, edit `Main/_Module/ModuleData/clan_heraldry/<culture>.json` instead and regenerate with `python tools/generate_clan_heraldry.py --spec <culture> --apply`. There are 21 spec files. Do not run the generator on `gondor` or `mordor`: their specs have drifted from the shipped stylesheet on `template_id` and a regeneration silently reverts a deliberate fix.

Check: `python tools/validate_moduledata.py`
Takes effect: new campaign only, because `Clan.Color` and `Clan.Color2` are `[SaveableProperty]` and a save keeps the colours it was made with
Code: No code changes needed

#### Override a vanilla clan through the stylesheet

Vanilla rows are not edited in place; they are rewritten by a template in [`Main/_Module/ModuleData/spclans.xslt`](../../Main/_Module/ModuleData/spclans.xslt). The pattern is fixed: copy, pass the vanilla attributes through minus the ones you are replacing, emit the replacements, then pass the children through.

<!-- excerpt file="Main/_Module/ModuleData/spclans.xslt" -->

```xml
  <xsl:template match="Faction[@id='clan_empire_west_1']">
    <xsl:copy>
      <xsl:apply-templates select="@*[local-name() != 'name' and local-name() != 'culture' and local-name() != 'color' and local-name() != 'color2' and local-name() != 'default_party_template']"/>
      <xsl:attribute name="name">{=TAOM_clan_empire_west_1}House of Húrinionath</xsl:attribute>
      <xsl:attribute name="culture">Culture.gondor</xsl:attribute>
      <xsl:attribute name="banner_key">11.149.149.1528.1528.764.764.1.0.0.10000.172.2000.580.580.765.825.0.0.0</xsl:attribute>
      <xsl:attribute name="color">FF211F1F</xsl:attribute>
      <xsl:attribute name="color2">FFFFFFFF</xsl:attribute>
      <xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_gondor_minas_tirith_template</xsl:attribute>
      <xsl:apply-templates select="node()"/>
    </xsl:copy>
  </xsl:template>
```

1. **Find the vanilla row first** in `SandBox/ModuleData/spclans.xml` and read what it already sets. Everything your filter does not exclude is inherited, and what it inherits is Calradia; the general trap is in [`.claude/rules/xslt.md`](../../.claude/rules/xslt.md).
2. **Emit `<xsl:attribute>` after the passthrough.** A later `xsl:attribute` replaces an earlier one of the same name, which is why `banner_key` above overrides the vanilla value even though the filter never names it. Transforming this stylesheet over the installed vanilla file confirms the row comes out with the TAOM name, culture, banner, colours and template while keeping vanilla's `owner`, `super_faction`, `tier`, `is_noble` and `initial_home_settlement`.
3. **End with `<xsl:apply-templates select="node()"/>`** or you drop the children. That is how the 14 minor factions keep their 4 `<template>` entries while being renamed.
4. **Register the new string** in `taom_module_strings.xml` with the same key you wrote in the `{=...}` prefix.

Check: `python tools/check_external_xslt.py` then `python tools/clan_registry.py`
Takes effect: new campaign only
Code: No code changes needed

### Delete

#### Strip a vanilla clan

There is no way to delete a row through the XML merge: a later module can overwrite attributes on a row keyed by `id`, and `_replaceWhileMerging="true"` clears a row's contents, but neither removes it. The delete is an empty XSLT template.

<!-- excerpt file="Main/_Module/ModuleData/spclans.xslt" -->

```xml
  <xsl:template match="Faction[@id='sea_raiders']"/>
  <xsl:template match="Faction[@id='mountain_bandits']"/>
  <xsl:template match="Faction[@id='forest_bandits']"/>
  <xsl:template match="Faction[@id='desert_bandits']"/>
  <xsl:template match="Faction[@id='steppe_bandits']"/>
```

1. **Add one empty template per id** to `spclans.xslt`. A template with no body matches the row and emits nothing.
2. **Check nothing else names the id.** A stripped clan that a `<Hero>`, a settlement `owner` or a kingdom row still points at leaves a hollow object behind.
3. **The five above are the vanilla hideout gangs.** TAOM replaced all 99 hideouts with LOTR bandit cultures, which left those clans with no hideouts of their own culture, and vanilla's `GetInfestedHideoutCount` reaches into `_hideouts[banditFaction.Culture]` with a hard indexer (`BanditSpawnCampaignBehavior.cs:490-499`). That is a `KeyNotFoundException` on new-game start, which is why they are deleted.
4. **`looters` is deliberately kept.** Its `StringId` is hardcoded in the bandit-density model and looters spawn on a path that never touches the hideout dictionary. `deserters` is kept for the same kind of reason: it is special-cased by name in `IsLooterFaction` (`BanditSpawnCampaignBehavior.cs:476-483`). Those two are the only vanilla rows the stylesheet leaves untouched.

Check: `python tools/check_external_xslt.py` then `python tools/clan_registry.py`, whose header line reports the removed ids
Takes effect: new campaign only
Code: No code changes needed

#### Delete a TAOM clan row

1. **Retire its lords first.** Every `<Hero>` whose `faction` names the clan, and every `<NPCCharacter>` behind those heroes, has to move or go with it. See [lords-and-heroes](lords-and-heroes.md) and [recipe-retire-content](recipe-retire-content.md).
2. **Reassign its fiefs** in the live `TAOM_Map/ModuleData/settlements.xml`. A settlement whose `owner` names a clan that no longer exists is a dangling reference.
3. **Remove any party template that existed only for it** from `taom_partyTemplates.xml`, and its entry from `clan_heraldry/<culture>.json` so the generator does not put it back.
4. **Delete the `<Faction>` block** from `characters/clans.xml`.
5. **Never do this against a shipped save.** Clans are engine-saved objects; removing one that a live campaign holds a reference to is not a supported edit. New campaign only, always.

Check: `python tools/validate_moduledata.py` then `python tools/clan_registry.py`
Takes effect: new campaign only
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A misspelled `culture`, `owner`, `super_faction`, `initial_home_settlement` or `default_party_template` does not throw.** Every type a `<Faction>` points at is registered with `autoCreateInstance`, so the loader invents an empty placeholder for an id it has not seen, then deletes it later and prints `Null object reference found with ID: <your typo>`. Grep the game log for that line after any clan edit. `MBObjectManager.cs:1437-1457`.
- **A dot-less reference does throw.** `culture="erebor"` is `MBInvalidReferenceException` at load, not a silent null. `MBObjectManager.cs:1517-1534`.
- **A vassal clan's `banner_key` colours are overwritten immediately after parsing.** `UpdateBannerColorsAccordingToKingdom` (`Clan.cs:1376-1402`) takes the kingdom's `PrimaryBannerColor` and `SecondaryBannerColor` when `super_faction` is set. An independent clan keeps its own key colours as long as one of them is non-zero; the `color`/`color2` fallback is the third branch and only fires for a minor faction whose banner encodes all zeroes. So recolouring a vassal's banner means editing the kingdom, not the clan. See [kingdoms](kingdoms.md).
- **A ruling clan shows its kingdom's banner, not its own.** TAOM's banner injection re-applies authored keys on new game and on save load but skips ruling clans deliberately, because vanilla repaints them from the kingdom every time. `clan_<kingdom>_1` therefore displays the kingdom banner while its `color`/`color2` still tint its own troops. `docs/features/banner-injection.md:24-27`.
- **`color="FFFFFFFF"` disables the clan-colour patch for that clan.** It equals `uint.MaxValue`, the engine's "unset" cloth colour, so the visuals patch reads it as absent and returns early. Use `FFFEFEFE`. `docs/features/clan-heraldry.md:52-58`.
- **A bandit clan whose culture owns no hideouts crashes the new game.** `_hideouts` is keyed by hideout settlement culture (`BanditSpawnCampaignBehavior.cs:104-114`) and `GetInfestedHideoutCount` indexes it without a lookup. Author the hideouts before the clan, which is the ordering the Wave 2 bandit cultures used (`docs/features/bandit-management.md:115`).
- **A bandit clan is quietly demoted when its party template lists ship hulls.** `IsBanditFaction` requires `!clan.HasNavalNavigationCapability` as well as the flag and `Culture.CanHaveSettlement` (`BanditSpawnCampaignBehavior.cs:560-566`), and `HasNavalNavigationCapability` comes from the party template, not the clan row (`Clan.cs:124`). A naval template on a gang stops it spawning with no error.
- **`is_bandit` on a culture does not make a clan.** The culture and the clan are two separate authoring steps, and a bandit culture with no clan is never iterated at all. `docs/features/bandit-management.md:103`.
- **Six attributes look real and are read by nothing in v1.4.8.** `settlement_banner_mesh` and `flag_mesh` are declared in `Factions.xsd:123-124` so they validate; `alternative_color`, `alternative_color2`, `label_color` and `encounterbackgroundmesh` are not even in the schema and survive only inside commented-out vanilla rows. `Clan.Deserialize` reads none of them and the literal strings return no hits across the whole v1.4.8 decompile. Eight TAOM rows carry `settlement_banner_mesh`; it is harmless and it does nothing. <!-- measured: rg -c over the decompile root for each of the six literals, and a python attribute histogram over characters/clans.xml 2026-09-05 -->
- **There is no `is_rebel_clan` attribute.** `IsRebelClan` is set at runtime when a rebellion fires and cannot be authored.
- **The schema and the deserializer disagree about what is required.** `Factions.xsd` marks `id`, `initial_home_settlement`, `tier` and `name` `use="required"`, but schema errors are printed and swallowed, so a schema-invalid row still loads. Only `id` and `name` produce a real crash.
- **An XML comment inside `<minor_faction_character_templates>` is a `NullReferenceException`.** The inner loop reads `Attributes["id"]` off every child node without skipping comments. `Clan.cs:908-912`.
- **The five strip templates appear twice in `spclans.xslt`**, at lines 27-31 and again at 1160-1164, with the same comment block above each. Editing one copy leaves the other in place. <!-- measured: rg -n on the empty Faction templates in spclans.xslt 2026-09-05 -->
- **The two clan files do not share byte conventions.** `characters/clans.xml` is CRLF with no BOM; `spclans.xslt` is LF with a BOM. Keep whichever the file you are editing already has, per [editing-safely](editing-safely.md). <!-- measured: python byte scan for the BOM and for CRLF counts on both files 2026-09-05 -->
- **`validate_moduledata.py` has no schema for `clans.xml`.** Only three schemas exist and none covers this file, so what you get on a clan row is the cross-reference sweep plus `LANDLESS_CULTURE`. Numeric ranges, a wrong `tier`, a bad colour and an unreachable `initial_home_settlement` are not checked by anything. <!-- measured: ls tools/schemas/ 2026-09-05 -->

## Numbers in this chapter

Every count below was produced on 2026-09-05 by the command beside it, run from the repo root.

| Number | Command |
|---|---|
| 145 `<Faction>` rows in `characters/clans.xml`, in 2,112 lines | a python ElementTree child count on the root, and `wc -l` |
| 235 clans in the world after the merge: 145 from XML, 88 rewritten vanilla rows, 2 vanilla passthrough | `python tools/clan_registry.py`, header line |
| 95 vanilla `<Faction>` rows, 5 deleted, 90 surviving, 88 of those rewritten, `looters` and `deserters` untouched | a python diff of the `Faction[@id]` templates in `spclans.xslt` against the row ids in the vanilla file, confirmed by running the stylesheet over it with lxml |
| 98 `Faction[@id]` templates in `spclans.xslt`, 10 of them empty (the 5 strip templates, duplicated) in 1,166 lines | `rg -c '<xsl:template match="Faction\[@id='` and `rg -n` for the self-closing form |
| Attribute coverage of the 145 rows: 145 each of `id`, `name`, `initial_home_settlement`, `tier`, `culture`, `color`, `color2`, `default_party_template`, `banner_key`; 137 each of `owner`, `super_faction`, `is_noble`; 8 each of `is_bandit`, `is_outlaw`, `settlement_banner_mesh`; 0 of `is_minor_faction` | a python ElementTree attribute histogram over the file |
| Tier spread of the 145: 26 at tier 1, 6 at 2, 54 at 3, 26 at 4, 17 at 5, 16 at 6 | the same histogram, grouped on `tier` |
| Party templates: 113 clan-specific variants, 24 culture defaults, 8 raider templates, 0 dangling | a python regex classification of `default_party_template`, differenced against the 383 ids in `taom_partyTemplates.xml` |
| 79 of the 145 `banner_key` values carry the two-group minimum (background plus one icon); the longest is 1,390 numbers | a python count of the dot-separated groups per key |
| 0 rows with `color="FFFFFFFF"`, 0 with a `0x` prefix; 7 `color2` white emissions and 1 `FFFEFEFE` in the stylesheet | the same histogram, and `rg -c` on the `xsl:attribute` forms in `spclans.xslt` |
| 15 vanilla `is_minor_faction` rows, all 15 renamed by the stylesheet, 14 of them real factions plus `player_faction`; each real one carries 4 `<template>` children | a python scan of the vanilla file crossed with the template ids in the stylesheet |
| 8 bandit clans, all tier 1, all `is_bandit` plus `is_outlaw`, none with a `super_faction`, each homed on a hideout of its own culture | the same ElementTree pass, printing the bandit rows |
| 24 attributes and 2 child elements read by `Clan.Deserialize`, 1 more (`id`) by `MBObjectBase.Deserialize` | `tools/check_handbook_attributes.py`'s own `extract_reads` run against the v1.4.8 `Clan.cs` and `MBObjectBase.cs` |
| 21 spec files under `Main/_Module/ModuleData/clan_heraldry/` | `ls Main/_Module/ModuleData/clan_heraldry/` |
| 3 schemas in `tools/schemas/`, none of them for `clans.xml` | `ls tools/schemas/` |
| `validate_moduledata.py --code LANDLESS_CULTURE` and `check_external_xslt.py` both PASS (17 stylesheets clean) | both commands, run in full |

## Read next

- [`docs/features/clan-heraldry.md`](../features/clan-heraldry.md), the colour chain from clan to armour and the per-clan party-template pipeline.
- [`docs/features/banner-injection.md`](../features/banner-injection.md), why an authored `banner_key` is re-applied on load and why ruling clans are skipped.
- [`docs/features/minor-factions.md`](../features/minor-factions.md), the 14 renamed minor factions and the override pattern.
- [`docs/features/bandit-management.md`](../features/bandit-management.md), the bandit culture and clan contract and the hideout ordering rule.
- [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md), the ordered 13-file sequence a new kingdom's clans sit inside, and the id naming table.
- [`docs/features/lord-spawn-guard.md`](../features/lord-spawn-guard.md), the landless-culture crash the `LANDLESS_CULTURE` gate exists to stop.
- [`.claude/rules/xslt.md`](../../.claude/rules/xslt.md), the passthrough and identity-transform rules every stylesheet template obeys.
- [`docs/reviews/lessons/data-content-cultures.md`](../reviews/lessons/data-content-cultures.md), the recorded failures behind several of the gotchas above.
