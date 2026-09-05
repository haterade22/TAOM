# The TAOM Modding Handbook

## What this is, and what it is not

This is the reference for changing TAOM's **data**: the XML, JSON and XSLT files that decide what a
troop wears, how hard it hits, who rules which castle, what a culture recruits and which art a helmet
points at. Every chapter is written for someone who opens files in a text editor and never opens
Visual Studio. It covers the four modules TAOM ships, every ModuleData file in them, the attribute
tables the engine actually reads, an Add / Modify / Delete recipe per file, and the gates that catch a
mistake before the game does.

It is **not** a C# guide, not a decompile reference, not a design document and not a Modding Kit art
course. Where a job needs code, the chapter says so and stops. Where TAOM never worked something out,
the chapter says "not written down" instead of inventing a procedure. The reasons behind a decision,
the history of how a feature got its shape and everything on the code side stay in
[`docs/features/`](../features/), and the handbook links there rather than repeating it.

## I want to...

Every row points at one chapter and one heading inside it. Nothing here is a summary, so follow the
link rather than acting on the phrasing.

| I want to... | Go to |
|---|---|
| "make Rohan cavalry hit harder" | [Balance levers, Lever 2: a troop's skill values](balance-levers.md#lever-2-a-troops-skill-values) |
| "give a troop better armour without breaking the ladder" | [Balance levers, Lever 3: armour numbers](balance-levers.md#lever-3-armour-numbers) |
| "work out what a troop's level actually controls" | [Balance levers, Lever 1](balance-levers.md#lever-1-a-troops-level-and-the-four-numbers-it-decides) |
| "make AI lord parties bigger" | [Balance levers, Lever 4: party size](balance-levers.md#lever-4-party-size-which-is-two-different-numbers) |
| "make a town richer or poorer" | [Balance levers, Lever 5: settlement economy](balance-levers.md#lever-5-settlement-economy) |
| "know which numbers I cannot touch from a text editor" | [Balance levers, What you cannot change without touching code](balance-levers.md#what-you-cannot-change-without-touching-code) |
| "a helmet renders naked on the troop" | [Troubleshooting, Symptom to cause](troubleshooting.md#symptom-to-cause) |
| "the game crashed on load and I do not know which file did it" | [Troubleshooting, Read the evidence first](troubleshooting.md#read-the-evidence-first) |
| "the validator is green but the game still looks wrong" | [Validation and testing, Green validator, naked troop](validation-and-testing.md#green-validator-naked-troop) |
| "know if I broke something before I launch" | [Validation and testing, The ordered check sequence](validation-and-testing.md#the-ordered-check-sequence) |
| "know which script catches which kind of mistake" | [Validation and testing, Six safety categories](validation-and-testing.md#six-safety-categories) |
| "retire a troop without breaking saves" | [Retiring content, What a save actually holds](recipe-retire-content.md#what-a-save-actually-holds) |
| "delete an item and the art behind it safely" | [Retiring content, Three ways to remove something](recipe-retire-content.md#three-ways-to-remove-something) |
| "add a new helmet to Gondor" | [Armour items, Recipes](items-armor.md#recipes-add--modify--delete) |
| "know what `covers_legs` really does" | [Armour items, Attributes](items-armor.md#attributes) |
| "add a sword, an axe or a bow" | [Weapons and crafting, Recipes](items-weapons-and-crafting.md#recipes-add--modify--delete) |
| "understand why a crafted weapon never appears" | [Weapons and crafting, Gotchas](items-weapons-and-crafting.md#gotchas-what-fails-silently-and-what-crashes) |
| "add a shield" | [Shields, Recipes](items-shields.md#recipes-add--modify--delete) |
| "stop a troop carrying a weapon it never draws" | [Shields, Gotchas](items-shields.md#gotchas-what-fails-silently-and-what-crashes) |
| "make a creature rideable" | [Mounts and harness, Recipes](items-mounts-and-harness.md#recipes-add--modify--delete) |
| "fix barding the inventory screen refuses to show" | [Mounts and harness, Gotchas](items-mounts-and-harness.md#gotchas-what-fails-silently-and-what-crashes) |
| "add a rung to a troop tree" | [Troops, Recipes](troops.md#recipes-add--modify--delete) |
| "change what a troop is wearing" | [Equipment rosters, Recipes](equipment-rosters.md#recipes-add--modify--delete) |
| "work out which slot `Item3` is" | [Equipment rosters, Child elements](equipment-rosters.md#child-elements) |
| "add a companion who stands in a tavern" | [Wanderers and named companions, Recipes](wanderers-and-named-companions.md#recipes-add--modify--delete) |
| "add a village headman or a town merchant" | [Notables, headmen and townsfolk, Recipes](npcs-notables-and-townsfolk.md#recipes-add--modify--delete) |
| "add a named lord" | [Lords and heroes, Recipes](lords-and-heroes.md#recipes-add--modify--delete) |
| "give a lord the skills his reputation deserves" | [Skill sets, Recipes](skill-sets.md#recipes-add--modify--delete) |
| "change a lord's face, age or race" | [Body properties, Recipes](body-properties.md#recipes-add--modify--delete) |
| "add a clan, or move a lord to another one" | [Clans, Recipes](clans.md#recipes-add--modify--delete) |
| "add a whole kingdom" | [Add a kingdom, Filing order](recipe-add-a-kingdom.md#filing-order) |
| "change a kingdom's colours or its banner" | [Kingdoms, Attributes](kingdoms.md#attributes) |
| "design a banner or add a new emblem" | [Banners and Heraldry, Recipes](banners-and-heraldry.md#recipes-add--modify--delete) |
| "add a culture" | [Add a culture, Order of work](recipe-add-a-culture.md#order-of-work) |
| "stop my new culture fielding Calradians" | [Cultures, Gotchas](cultures.md#gotchas-what-fails-silently-and-what-crashes) |
| "change what a party spawns with" | [Party templates, Recipes](party-templates.md#recipes-add--modify--delete) |
| "move a town, or change who owns it" | [Settlements, Recipes](settlements.md#recipes-add--modify--delete) |
| "rename a village" | [Settlements, Gotchas](settlements.md#gotchas-what-fails-silently-and-what-crashes) |
| "add a race such as dwarves or orcs" | [Add a race or a creature, Recipes](recipe-add-a-race-or-creature.md#recipes) |
| "add a creature like the war ram" | [Add a race or a creature, Two paths](recipe-add-a-race-or-creature.md#two-paths-and-one-is-much-cheaper) |
| "change a name the player sees on screen" | [Strings and localization, Recipes](strings-and-localization.md#recipes-add--modify--delete) |
| "change starting gold, prices or a tuning number" | [Balance configs, Attributes](configs-balance.md#attributes) |
| "change who is at war with whom" | [Configs: factions and the world, Attributes](configs-factions-and-world.md#attributes) |
| "find the exact spelling of a culture id" | [Id cheatsheet, Culture ids](id-cheatsheet.md#culture-ids) |
| "work out which race number is which" | [Id cheatsheet, Race ids in merge order](id-cheatsheet.md#race-ids-in-merge-order) |
| "work out which Armory folder an item id belongs to" | [Id cheatsheet, Item id prefix to Armory folder](id-cheatsheet.md#item-id-prefix-to-armory-folder) |
| "know which ids a save file binds forever" | [Id cheatsheet, Which ids a save binds](id-cheatsheet.md#which-ids-a-save-binds) |
| "know which XML the game actually reads" | [File catalogue, TAOM: the repo module](file-catalogue.md#taom-the-repo-module) |
| "know why my new file does nothing" | [SubModule and registration, The four ways a file gets loaded](submodule-and-registration.md#the-four-ways-a-file-gets-loaded) |
| "know when my edit shows up in game" | [Load order and dependencies, F. When your change shows up](load-order-and-dependencies.md#f-when-your-change-shows-up-and-what-a-save-keeps) |
| "back up a file without the engine loading the backup" | [Editing safely, The `*.xml` glob](editing-safely.md#the-xml-glob-why-a-backups-name-is-load-bearing) |
| "know which copy of a file is the live one" | [Editing safely, Which copy of a file is live](editing-safely.md#which-copy-of-a-file-is-live) |
| "get my art into the game, FBX to tpac" | [The Armory module, The art side](module-armory.md#the-art-side-fbx-to-tpac-to-mesh) |
| "know what the Armory module holds" | [The Armory module, Folder by folder](module-armory.md#folder-by-folder) |
| "know what the map module holds" | [The campaign map module, Folder anatomy](module-map.md#folder-anatomy) |
| "know why the launcher needs TAOM.Dependencies" | [TAOM.Dependencies, The four libraries in plain words](module-dependencies.md#the-four-libraries-in-plain-words) |
| "know which module owns which data" | [Modules overview, Who owns what data](modules-overview.md#who-owns-what-data) |
| "know what a build actually copies into the game" | [Module TAOM, How build.ps1 deploys it](module-taom.md#how-buildps1-deploys-it-and-why-deploy-never-deletes) |
| "build a map from nothing" | [A new mod from zero, Stage 16](recipe-new-mod-from-zero.md#stage-16-the-map-module-and-its-settlements) |
| "start a mod from an empty folder" | [A new mod from zero, Stage 1](recipe-new-mod-from-zero.md#stage-1-make-the-folder-and-the-manifest) |

60 rows, reaching 38 of the 38 other chapters.
<!-- measured: rg -c '^. "' docs/modding/README.md, and a python count of distinct .md targets in that table 2026-09-05 -->

## Reading order for a brand-new modder

Read these once in this order, then use the routing table above forever after. The first block teaches
the machinery, and skipping it is what produces a file the game silently ignores.

1. [Modules overview](modules-overview.md), then [Module TAOM](module-taom.md),
   [TAOM.Dependencies](module-dependencies.md), [The campaign map module](module-map.md) and
   [The Armory module](module-armory.md). What the four modules are and which one owns your file.
2. [SubModule and registration](submodule-and-registration.md). Why a file that exists still does not load.
3. [Load order and dependencies](load-order-and-dependencies.md). What has to exist before what.
4. [Editing safely](editing-safely.md). How to touch a shipped file without producing a second copy of every id in it.
5. [Id cheatsheet](id-cheatsheet.md). The spellings, kept in one place so you never guess one.
6. **The file chapters**, in whatever order your work needs: items ([armour](items-armor.md),
   [weapons and crafting](items-weapons-and-crafting.md), [shields](items-shields.md),
   [mounts and harness](items-mounts-and-harness.md)), then [troops](troops.md),
   [equipment rosters](equipment-rosters.md), [notables](npcs-notables-and-townsfolk.md),
   [wanderers](wanderers-and-named-companions.md), [lords and heroes](lords-and-heroes.md),
   [skill sets](skill-sets.md), [body properties](body-properties.md), [cultures](cultures.md),
   [party templates](party-templates.md), [clans](clans.md), [kingdoms](kingdoms.md),
   [settlements](settlements.md), [banners](banners-and-heraldry.md),
   [strings](strings-and-localization.md), [balance configs](configs-balance.md) and
   [faction configs](configs-factions-and-world.md).
7. **The recipes**, which chain those chapters into one job:
   [add a culture](recipe-add-a-culture.md), [add a kingdom](recipe-add-a-kingdom.md),
   [add a race or a creature](recipe-add-a-race-or-creature.md),
   [retire content](recipe-retire-content.md), [a new mod from zero](recipe-new-mod-from-zero.md).
8. **The closing chapters**: [balance levers](balance-levers.md),
   [validation and testing](validation-and-testing.md), [troubleshooting](troubleshooting.md), and
   [the file catalogue](file-catalogue.md) as a lookup rather than a read.

[Modules overview](modules-overview.md#reading-order-for-a-brand-new-modder) carries the same order
with the reason each step sits where it does.

## The four modules, and which folder is the real one

| Module | Authoritative folder | Ships to players | What a module reinstall reverts |
|---|---|---|---|
| `TAOM` | the repo, `Main/_Module/`; the build copies it into the install | yes, 2,277 files, 0.41 GB | nothing you edited in the repo, because the next build re-deploys it |
| `TAOM.Dependencies` | the repo, `Dependencies/_Module/` | yes, 142 files, 0.04 GB | nothing, for the same reason |
| `TAOM_Map` | the game install only; there is no repo copy | yes, 2,519 files, 19.39 GB | every hand edit, `settlements.xml` included |
| `LOTRLOME_Armory` | the game install only; there is no repo copy | yes, 4,990 files, 4.10 GB | every hand edit, every item file included |

The sizes and file counts are the `package_release.py` dry run quoted in
[Modules overview, What ships to players](modules-overview.md#what-ships-to-players). The four vanilla
modules (`Native`, `SandBoxCore`, `SandBox`, `CustomBattle`) are never edited: TAOM changes vanilla
data by overriding it from its own module, which is
[Load order and dependencies, section E](load-order-and-dependencies.md#e-cross-module-merge-and-the-xslt-layer).

Two consequences to carry into every chapter:

- **For the two live modules, the edit is the deployment.** `TAOM_Map/ModuleData/settlements.xml` and
  every file under `LOTRLOME_Armory/ModuleData/LOTRLOME_items/` live in the game install, not the
  repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix
  ([CLAUDE.md](../../CLAUDE.md) Traps, "A fix in a dependency module").
- **The deploy of `TAOM` never deletes.** `CopyModule` mirrors `Main/_Module` into the install with
  `Clean="false"`, so a file you delete from the repo lives on in the game until you remove the
  installed copy by hand
  ([build and tooling lessons](../reviews/lessons/build-tooling-workflow.md), "The module deploy NEVER
  deletes"). A stale installed file is one of the two usual reasons an edit that looks right does
  nothing.

## When an edit shows up: the short version

Four buckets. The long form, with the engine citations behind each row, is
[Load order and dependencies, section F](load-order-and-dependencies.md#f-when-your-change-shows-up-and-what-a-save-keeps).

| Bucket | What lands here |
|---|---|
| **Full game restart** | any new XML file, any new `<XmlNode>`, any edit to `SubModule.xml` or `project.mbproj`, and every JSON config cached for the process |
| **New campaign only** | heroes, kingdoms and factions, settlement owners, town prosperity and building levels, hero skills, clan colours, family links |
| **Next load, existing save included** | item stats, equipment rosters, party templates, culture bindings, troop stats, settlement culture |
| **Persisted per player, outside the mod** | every MCM value, which overrides the shipped default on any install that already has it |

The rule underneath all four: **never rename an id**. A save stores ids, so a renamed settlement,
troop, clan, kingdom, hero or item is a missing object on load. Taking something out is
[Retiring content](recipe-retire-content.md).

## How to read a chapter

**Nine headings, always the same, always in this order** on a file chapter: What this file is; Where it
lives and how it is registered; Attributes; Child elements; Worked example; Recipes: Add / Modify /
Delete; Gotchas: what fails silently and what crashes; Numbers in this chapter; Read next. The concept
chapters, the recipes and the closing chapters use the same shape minus the parts that do not apply.
Jump straight to the heading you need.

**Three markers**, HTML comments you can ignore while reading and that a script checks while the
handbook is maintained:

- An **`engine-table`** comment sits above an attribute or child-element table, carrying `type=`,
  `file=` and `method="Deserialize"`, and names the engine method whose code was read to produce that
  table. Every backticked name in column one was diffed against that method, so the table carries no
  invented rows and no missing ones. Its documentation-only sibling, **`engine-ref`**, marks enum
  lists and mechanism tables that no deserializer reads.
- An **`example`** comment sits above a worked example, carrying `file=` and `id=`, and names the
  shipped file and the entry the block was copied out of, verbatim. An excerpt with no id (a
  directory listing, a manifest block) carries **`excerpt`** with just a `file=`, as the three XML
  blocks further down this page do.
- A **`measured:`** comment sits beside a number and gives the command that produced it, with the
  date. Every count in the handbook has one, or a citation to the doc it was quoted from.

**The recipe trailer.** Every Add, Modify and Delete recipe anywhere in the handbook ends with three
lines: `Check:` the command that proves it, `Takes effect:` one of full game restart / new campaign
only / next save load / live, and `Code:` either "No code changes needed" or the file a developer has
to touch. A recipe that does not end with those three lines is unfinished.

## Division of labour with the developer docs

| The handbook owns | `docs/features/` and the rest of `docs/` own |
|---|---|
| Attribute and child-element tables per file | Why a feature was built the way it was, and what was rejected |
| Add / Modify / Delete recipes | The C# side: services, adapters, Harmony patches, GameModels |
| The [id cheatsheet](id-cheatsheet.md) | Design history, RCAs and the [lessons index](../reviews/LESSONS-LEARNED.md) |
| The [file catalogue](file-catalogue.md) of every ModuleData file | Per-feature configuration detail, indexed from [feature-map.md](../reference/feature-map.md) |
| The gates table in [validation and testing](validation-and-testing.md) | The tool registry, [tools/README.md](../../tools/README.md), and the validator's own design, [moduledata-validation.md](../features/moduledata-validation.md) |

When a chapter and a dev doc disagree, the chapter was re-checked against disk on 2026-09-05 and the
dev doc may not have been. The handbook states the correction rather than dropping the claim, which is
the editorial rule the public guide already follows
([community guide README](../community/bannerlordmodding-lt/README.md), "Editorial rules these pages
follow"). To find anything else in `docs/`, start at [docs/INDEX.md](../INDEX.md) or the
task-oriented [doc-lookup.md](../reference/doc-lookup.md).

## Every chapter, one line each

**Orientation**

- [modules-overview.md](modules-overview.md): what a module is, the eight TAOM runs on, who owns what.
- [module-taom.md](module-taom.md): the repo module, its folders, its deploy and its two registration channels.
- [module-dependencies.md](module-dependencies.md): Harmony, ButterLib, UIExtenderEx and MCM, and the version pairing.
- [module-map.md](module-map.md): the campaign map scene, the settlements on it and the prefab cap.
- [module-armory.md](module-armory.md): the art and items module, FBX to tpac, races and monsters.
- [submodule-and-registration.md](submodule-and-registration.md): the four ways a file gets loaded.
- [load-order-and-dependencies.md](load-order-and-dependencies.md): the engine's load order, the existence ladder, the reload matrix.
- [editing-safely.md](editing-safely.md): backups, BOM and line endings, comments, ids, proving a value landed.
- [id-cheatsheet.md](id-cheatsheet.md): every id family with the spelling the engine reads.

**Files**

- [items-armor.md](items-armor.md): helmets, chests, boots, gloves and capes, and the cover attributes.
- [items-weapons-and-crafting.md](items-weapons-and-crafting.md): crafted weapons across four files, and single-piece items.
- [items-shields.md](items-shields.md): the single shield file, its usages and the offhand rules.
- [items-mounts-and-harness.md](items-mounts-and-harness.md): Monster, Horse item and harness, and the family type that binds them.
- [troops.md](troops.md): the recruitment tree, and a soldier's level, skills, formation, race and kit.
- [equipment-rosters.md](equipment-rosters.md): standalone and inline outfits, and the slot names.
- [npcs-notables-and-townsfolk.md](npcs-notables-and-townsfolk.md): merchants, preachers, gang leaders, headmen and scene NPCs.
- [wanderers-and-named-companions.md](wanderers-and-named-companions.md): the tavern hire blueprints and how the engine clones them.
- [lords-and-heroes.md](lords-and-heroes.md): the NPCCharacter and Hero pair that share one id.
- [skill-sets.md](skill-sets.md): the named skill blocks lords and wanderers point at.
- [body-properties.md](body-properties.md): face and body presets, and the range a face is rolled inside.
- [cultures.md](cultures.md): the hub every other file points back at, and its spawn-deciding attributes.
- [party-templates.md](party-templates.md): what a party spawns with, and why that is not its size.
- [clans.md](clans.md): the houses, companies and gangs that own lords and fiefs.
- [kingdoms.md](kingdoms.md): the map factions, their colours, capitals and day-one stances.
- [settlements.md](settlements.md): towns, castles, villages and hideouts, and the live file that holds them.
- [banners-and-heraldry.md](banners-and-heraldry.md): the icon catalogue and the banner keys that address it.
- [strings-and-localization.md](strings-and-localization.md): the key registry and the twelve language files.
- [configs-balance.md](configs-balance.md): the fifteen tuning files and the providers that read them.
- [configs-factions-and-world.md](configs-factions-and-world.md): alignment, wars, garrisons, markets and the faction screen.

**Recipes**

- [recipe-add-a-culture.md](recipe-add-a-culture.md): the ordered path, and what fails quietly if you skip a step.
- [recipe-add-a-kingdom.md](recipe-add-a-kingdom.md): a playable realm, filing order first, config fan-out second.
- [recipe-add-a-race-or-creature.md](recipe-add-a-race-or-creature.md): the five data surfaces a race needs, and two build paths.
- [recipe-retire-content.md](recipe-retire-content.md): taking a troop, item, lord or clan out without breaking a save.
- [recipe-new-mod-from-zero.md](recipe-new-mod-from-zero.md): eighteen stages from an empty folder to a total conversion.

**Closing**

- [balance-levers.md](balance-levers.md): the numbers, what consumes each one, and which are out of reach.
- [validation-and-testing.md](validation-and-testing.md): every gate, what it proves and what it cannot see.
- [troubleshooting.md](troubleshooting.md): symptom to cause, from failures TAOM actually root-caused.
- [file-catalogue.md](file-catalogue.md): every ModuleData file in the three data modules, with its loader.

## The three registrations, side by side

The same job in three modules. Each block is one `<XmlNode>` in that module's `SubModule.xml`, and the
`path` is relative to that module's own `ModuleData` folder, with no file extension.

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
    <XmlNode>
      <XmlName id="NPCCharacters" path="troops/troops_gondor"/>
      <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
```

<!-- excerpt file="LOTRLOME_Armory/SubModule.xml" -->
```xml
		<XmlNode>
			<XmlName id="Items" path="LOTRLOME_items/gondor"/>
			<IncludedGameTypes>
				<GameType value = "Campaign"/>
				<GameType value = "CampaignStoryMode"/>
				<GameType value = "CustomGame"/>
				<GameType value = "EditorGame"/>
			</IncludedGameTypes>
		</XmlNode>
```

<!-- excerpt file="TAOM_Map/SubModule.xml" -->
```xml
		<XmlNode>
			<XmlName id="Settlements" path="settlements"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
```

Three things to read off them. **The `id` is an engine registry name, not yours**: `NPCCharacters`,
`Items` and `Settlements` are fixed strings the engine understands, and a typo there loads nothing.
**The `path` may name a folder** (`LOTRLOME_items/gondor` globs every XML in it) **or a single file**
(`troops/troops_gondor` and `settlements` each name one). **The `<GameType>` list decides where the
file loads**, so a row missing `Campaign` is invisible in the campaign no matter how correct the XML
is. The mechanism in full is
[SubModule and registration](submodule-and-registration.md#mechanism-1-an-xmlnode-row-in-submodulexml).

## Owed: the audience read

**This handbook has not been walked by its reader yet.** It was written against the files, and the
routing above was checked link by link, but KEYforce has not run a recipe end to end and nobody
outside the authoring pass has followed a chapter with the game open. Until that happens the following
stay open:

- One artist walks a recipe start to finish (adding one helmet is the cheapest) and records every
  place the wording sent them to the wrong file.
- The same walk records which `Check:` command they could not run, and why.
- Any chapter whose worked example does not match what the reader sees on disk gets re-diffed against
  its file.
- The in-game half of every recipe stays unproven until someone confirms it in a campaign, which is
  what [Validation and testing, In-game smoke](validation-and-testing.md#in-game-smoke-what-no-script-can-answer)
  says no script can answer for you.

Two commands close most of the gap before that read happens: `python tools/validate_moduledata.py` for
the data and `python tools/lint_docs.py --summary` for the docs.

## Numbers in this chapter

| Number | Command or source | Date |
|---|---|---|
| 39 chapters in the handbook, this one included | `ls docs/modding/*.md \| wc -l` | 2026-09-05 |
| 60 rows in the "I want to..." table | `rg -c '^. "' docs/modding/README.md` | 2026-09-05 |
| 38 distinct chapters that table reaches | a python count of distinct `.md` link targets inside the table | 2026-09-05 |
| The four modules' ship sizes and file counts | quoted from [Modules overview, What ships to players](modules-overview.md#what-ships-to-players), which measured them with `python tools/package_release.py --dry-run` | 2026-09-05 |

## Read next

- [feature-map.md](../reference/feature-map.md): the developer's feature and component map, where each feature's own doc is indexed.
- [moduledata-validation.md](../features/moduledata-validation.md): the validator's design and the three-module coverage matrix.
- [agent-operating-manual.md](../ai-includes/agent-operating-manual.md): the tool table the gates in this handbook were drawn from.
- [build-tooling-workflow.md](../reviews/lessons/build-tooling-workflow.md): the deploy and tooling lessons, including the deploy that never deletes.
- [tools/README.md](../../tools/README.md): the full script registry.
- [docs/INDEX.md](../INDEX.md): the rest of the knowledge base.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
