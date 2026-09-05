# Party templates

## What this file is

`taom_partyTemplates.xml` holds every roster the engine hands a party at the moment that party is
created: lord armies, town militia, village trade parties, caravans, settlement patrols, rebel mobs,
bandit hideouts, and the troops a kingdom gives you for swearing an oath. One entry is an id plus a
list of stacks, and one stack is a troop id with a `min_value` and a `max_value`. Those two numbers
decide what a party **spawns with** and never how large it can grow, and an entry does nothing at all
until a culture or a clan names it by id.

## Where it lives and how it is registered

- **Repo file:** [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml).
  It carries 383 templates and 3,295 stacks. <!-- measured: rg -c '<MBPartyTemplate id=' Main/_Module/ModuleData/taom_partyTemplates.xml && rg -c '<PartyTemplateStack' Main/_Module/ModuleData/taom_partyTemplates.xml 2026-09-05 -->
- **Registration:** `Main/_Module/SubModule.xml:331`, `<XmlName id="partyTemplates" path="taom_partyTemplates"/>`.
  Vanilla registers the same list id at `SandBox/SubModule.xml:59` with `path="partyTemplates"`. This
  file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a
  repo-side validator gate with any fix. Both lists load into one merged document, so a TAOM entry
  that reuses a vanilla id is folded into the vanilla one before anything is read.
- **Root element:** `<partyTemplates>`. **Per-entry element:** `<MBPartyTemplate>`. **Engine class:**
  `TaleWorlds.CampaignSystem.Party.PartyTemplateObject`.
- **The root tag is load-bearing and the per-entry tag is not.** `MBObjectManager.LoadXml` matches the
  document root against the registered list name (`MBObjectManager.cs:1371`), then deserializes every
  non-comment child of it whatever that child is called (`MBObjectManager.cs:1387-1395`). Write
  `<MBPartyTemplate>` because every shipped file does, not because the engine checks.
- **A duplicate id across two files adds stacks, it does not replace them.** Every file registered
  under `partyTemplates` is merged into one document before a line of it is deserialized, and the
  schema in the game root's `XmlSchemas` folder decides how. `partyTemplates.xsd:11-16` marks
  `<stacks>` `AlwaysPreferMerge`, so the merger walks into the earlier template's stack list
  (`MBObjectManager.cs:851-855`), and `<PartyTemplateStack>` carries no unique key, so the later
  file's stacks are **appended** to what is already there (`MBObjectManager.cs:867`). One merged
  entry then reaches `Deserialize` (`MBObjectManager.cs:1387-1393`) and the template spawns both
  modules' stacks, which is how a roster quietly doubles.
- **`_replaceWhileMerging="true"` is how you take a template's place.** Put it on your
  `<MBPartyTemplate>` and the merger strips the earlier entry's attributes and children before
  yours land (`MBObjectManager.cs:804-808`, `:829-832`). TAOM does not use it anywhere today.
- **Two entries with the same id inside one file go the other way.** The merger only folds one file
  into another, so both rows survive it, `Deserialize` runs twice over the same template, and its
  first line rebuilds the stack list from scratch (`PartyTemplateObject.cs:28`). There the second
  entry wins outright.

### What binds a template

A culture attribute, a culture child list or a clan attribute. Nothing else reads this file.

<!-- engine-ref type="TaleWorlds.CampaignSystem.CultureObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CultureObject.cs" lines="270-280,485-497" -->

| Binding site | Written as | Read at |
|---|---|---|
| `default_party_template` | `PartyTemplate.kingdom_hero_party_erebor_template` | `CultureObject.cs:270` |
| `villager_party_template` | `PartyTemplate.villager_erebor_template` | `CultureObject.cs:271` |
| `militia_party_template` | `PartyTemplate.militia_erebor_template` | `CultureObject.cs:273` |
| `rebels_party_template` | `PartyTemplate.rebels_erebor_template` | `CultureObject.cs:274` |
| `vassal_reward_party_template` | `PartyTemplate.vassal_reward_troops_erebor` | `CultureObject.cs:276` |
| `settlement_patrol_template_level_1` / `_2` / `_3` | `PartyTemplate.patrol_party_erebor_template_level_1` | `CultureObject.cs:277-279` |
| `<caravan_party_templates>` and `<elite_caravan_party_templates>` child lists | `<caravan_party_template id="PartyTemplate.caravan_template_erebor" />` | `CultureObject.cs:485-496` |
| Clan `default_party_template` on a `<Faction>` | `PartyTemplate.kingdom_hero_party_erebor_erebor_1_template` | `Clan.cs:112-122` |

`Clan.DefaultPartyTemplate` returns the clan's own binding when it has one and falls back to the
culture's `default_party_template` otherwise (`Clan.cs:112-122`), which is why most named lords field
a per-clan roster and only unbound clans field the culture default.

## Attributes

<!-- engine-table type="TaleWorlds.CampaignSystem.Party.PartyTemplateObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/PartyTemplateObject.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none | The template's unique name. Everything that wants this roster writes `PartyTemplate.<id>`. On a `<ShipTemplateStack>` the same attribute name instead holds a `ShipHull.<id>` reference. | `MBObjectManager.cs:1391`, `PartyTemplateObject.cs:54` |

`Deserialize` reads no other attribute off `<MBPartyTemplate>`. There is no name, no culture, no
faction and no size field, and any attribute you invent on the element is ignored without a warning
(`PartyTemplateObject.cs:26-60`).

### `<PartyTemplateStack>` attributes

<!-- engine-table type="TaleWorlds.CampaignSystem.Party.PartyTemplateObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/PartyTemplateObject.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `troop` | dotted reference, `NPCCharacter.<id>` | in practice | null Character, which crashes at spawn instead of at load | Names the troop this stack fills with. The value must contain a dot: an undotted value throws `MBInvalidReferenceException` (`MBObjectManager.cs:1526-1527`), and an unknown id after the dot is created as a placeholder rather than rejected (`MBObjectManager.cs:713-735`). | `PartyTemplateObject.cs:39` |
| `min_value` | int | yes | `NullReferenceException` at module load | The floor this stack fills to when the party's ratio rolls 0. | `PartyTemplateObject.cs:39` |
| `max_value` | int | yes | `NullReferenceException` at module load | The value this stack fills to when the ratio rolls 1. It caps that arithmetic, not the party: a villager stack is multiplied again afterwards, and no party size limit is involved. | `PartyTemplateObject.cs:39` |

## Child elements

<!-- engine-table type="TaleWorlds.CampaignSystem.Party.PartyTemplateObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/PartyTemplateObject.cs" method="Deserialize" inert="" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<stacks>` | container | no | empty stack list, so the template spawns nobody | Holds the troop stacks. The name is case sensitive: `Stacks` is skipped in silence. | `PartyTemplateObject.cs:33` |
| `<PartyTemplateStack>` | self-closing entry | one per troop type | none | One troop plus its min and max. Listing the same troop twice is legal and the counts add. Any other child of `<stacks>` is skipped. | `PartyTemplateObject.cs:37` |
| `<ship_hulls>` | container | no | empty hull list | NavalDLC content. TAOM authors none, and a non-empty list flips gameplay switches such as `Clan.HasNavalNavigationCapability` (`Clan.cs:124`). | `PartyTemplateObject.cs:46` |
| `<ShipTemplateStack>` | self-closing entry | no | none | A `ShipHull.<id>` reference plus its own min and max. Any other child of the template is skipped without an error (`PartyTemplateObject.cs:44-49`). | `PartyTemplateObject.cs:52` |

### The spawn formula, which is the whole point of the two numbers

`DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` draws **one** ratio `r` for
the whole party, then fills every stack to `RoundRandomized(min + (max - min) * r)`
(`DefaultPartySizeLimitModel.cs:427-464`, the arithmetic at `:442`). Only stacks resolving above zero
are added (`:457`). So a party spawns somewhere between the template's min sum and its max sum, and
the **expected** roster is the midpoint of the two.

**One kind of party finishes above its own `max_value`.** If a villager party's bound town has a
governor carrying the Village Network perk, every stack is multiplied by 1.1 after the interpolation
and before the troops are counted in (`DefaultPartySizeLimitModel.cs:449-455`; the +10% is that
perk's governor bonus at `DefaultPerks.cs:2149`). Read `max_value` as the top of the arithmetic
rather than as a promise about the roster. Every other party type stays inside the band.

`r` comes from `GetInitialPartySizeRatioForMobileParty` (`DefaultPartySizeLimitModel.cs:390-413`): a
player caravan and a patrol party both get `1f` and therefore always spawn at the max sum
(`:404-411`), a land bandit gets a player-progress term, and **everything else, every kingdom lord
party included, gets `party.RandomFloat()`** (`:412`), rolled once when the party is built and fixed
for its life. Nothing in that path consults `PartySizeLimit`, which is a separate model and is the
number a party settles at afterwards. Full engine write-up:
[party-template-sizing.md](../reference/party-template-sizing.md).

The midpoint does a second job. Five systems use `(min + max) / 2` as a **pick weight** to choose
which troop to add, so widening one stack changes that troop's share of the mix and not only the
count. The one a balancer meets first is vanilla's new-game top-up, which fills a lord toward his
size limit with `MBRandom.ChooseWeighted` over those midpoints
(`HeroSpawnCampaignBehavior.cs:264-275`).

## Worked example

`caravan_template_erebor`, complete and unedited. Three stacks, min sum 20, max sum 29. A caravan the
**player** owns draws a ratio of `1f` and spawns all 29 every time; an AI caravan rolls like any
other party and lands anywhere in the band (`DefaultPartySizeLimitModel.cs:404-412`). <!-- measured: python -c "import re;s=open('Main/_Module/ModuleData/taom_partyTemplates.xml',encoding='utf-8-sig').read();b=re.search(r'id=\"caravan_template_erebor\".*?</MBPartyTemplate>',s,re.S).group(0);print(len(re.findall(r'<PartyTemplateStack',b)),sum(map(int,re.findall(r'min_value=\"(\d+)\"',b))),sum(map(int,re.findall(r'max_value=\"(\d+)\"',b))))" 2026-09-05 -->

<!-- example file="Main/_Module/ModuleData/taom_partyTemplates.xml" id="caravan_template_erebor" -->

```xml
	<MBPartyTemplate id="caravan_template_erebor">
		<stacks>
			<PartyTemplateStack min_value="12" max_value="15" troop="NPCCharacter.armed_trader_erebor" />
			<PartyTemplateStack min_value="5" max_value="9" troop="NPCCharacter.caravan_guard_erebor" />
			<PartyTemplateStack min_value="3" max_value="5" troop="NPCCharacter.veteran_caravan_guard_erebor" />
		</stacks>
	</MBPartyTemplate>
```

The three attributes you change first:

1. **`troop`** picks the unit. Keep the `NPCCharacter.` prefix, and confirm the id with
   [`python tools/validate_moduledata.py`](../../tools/README.md) rather than by eye: a misspelling
   loads clean and gives you a stack that never fills.
2. **`max_value`** raises the ceiling and, on a lord template, moves the expected roster by half of
   what you added.
3. **`min_value`** raises the floor for every party, including the unlucky low rolls. Never push it
   above the stack's own `max_value`; the engine does not check, and a negative spread fills the
   stack **below** its floor ([party-template-sizing.md](../reference/party-template-sizing.md)).

The lord template for the same culture is the same shape at a different scale.
`kingdom_hero_party_erebor_template` carries 53 stacks summing to min 103 and max 225, so an Erebor
lord spawns anywhere from 103 to 225 men with about 164 expected. <!-- measured: python -c "import re;s=open('Main/_Module/ModuleData/taom_partyTemplates.xml',encoding='utf-8-sig').read();b=re.search(r'id=\"kingdom_hero_party_erebor_template\".*?</MBPartyTemplate>',s,re.S).group(0);print(len(re.findall(r'<PartyTemplateStack',b)),sum(map(int,re.findall(r'min_value=\"(\d+)\"',b))),sum(map(int,re.findall(r'max_value=\"(\d+)\"',b))))" 2026-09-05 -->

<!-- excerpt file="Main/_Module/ModuleData/taom_partyTemplates.xml" -->

```xml
	<MBPartyTemplate id="kingdom_hero_party_erebor_template">
		<stacks>
			<!-- Erebor Regular Line -->
			<PartyTemplateStack min_value="8" max_value="10" troop="NPCCharacter.erebor_reg_miner" />
			<PartyTemplateStack min_value="6" max_value="8" troop="NPCCharacter.erebor_reg_militia" />
			<PartyTemplateStack min_value="4" max_value="6" troop="NPCCharacter.erebor_reg_skirmisher" />
```

Neither template does anything on its own. The Erebor culture entry makes 10 `PartyTemplate.`
references, 8 on attributes and 2 inside the caravan child lists: <!-- measured: python -c "import re;s=open('Main/_Module/ModuleData/taom_spcultures.xml',encoding='utf-8-sig').read();b=re.search(r'<Culture\b[^>]*id=\"erebor\".*?</Culture>',s,re.S).group(0);print(len(re.findall(r'\w+=\"PartyTemplate\.',b)))" 2026-09-05 -->

<!-- excerpt file="Main/_Module/ModuleData/taom_spcultures.xml" -->

```xml
    <caravan_party_templates>
      <caravan_party_template id="PartyTemplate.caravan_template_erebor" />
    </caravan_party_templates>
    <elite_caravan_party_templates>
      <caravan_party_template id="PartyTemplate.elite_caravan_template_erebor" />
    </elite_caravan_party_templates>
```

The child element carries `id=`, not `template=`. A `template=` attribute resolves to nothing and
appends a **null** to the culture's caravan list, and the first caravan spawn then dies inside
vanilla code (`CaravansCampaignBehavior.cs:498`). That exact mistake sat in
[kingdom-creation.md](../features/kingdom-creation.md) until 2026-08-12.

## Recipes: Add / Modify / Delete

### Add a stack to an existing template

1. Open [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml)
   and find the `<MBPartyTemplate id="...">` you want.
2. Add one `<PartyTemplateStack min_value="" max_value="" troop="NPCCharacter.<troop id>" />` inside
   its `<stacks>` block. Keep `min_value` at or below `max_value`.
3. Copy the troop id from the culture's file under `Main/_Module/ModuleData/troops/`, do not type it
   from memory. See [Troops](troops.md).
4. If the template is a `kingdom_hero_party_*` one, the new stack has just lifted its max sum off the
   culture target, so run the retarget tool in the Modify recipe below.
5. If the template belongs to a minor faction, put the cheapest recruit **first**: the engine sets
   `Clan.BasicTroop` to the lowest-`Level` troop in the template and compares with a strict `<`, so
   the first entry wins a tie (`ClanVariablesCampaignBehavior.cs:499-509`).

Check: `python tools/validate_moduledata.py --code BROKEN_PARTY_TEMPLATE_REF`
Takes effect: next save load, and only for parties spawned after it. The `partyTemplates` list is re-read on every campaign load, saved games included (`Campaign.cs:1396-1399`, `:1466-1473`); an existing lord keeps the roster he was created with.
Code: No code changes needed

### Add the twelve templates a new culture needs

1. Author all twelve per-culture kinds in the same file, using an existing culture as the pattern:
   `kingdom_hero_party_<c>_template`, `kingdom_hero_party_mercenary_<c>_template`,
   `kingdom_hero_party_outlaw_<c>_template`, `patrol_party_<c>_template_level_1` and `_2` and `_3`,
   `rebels_<c>_template`, `vassal_reward_troops_<c>`, `militia_<c>_template`,
   `villager_<c>_template`, `caravan_template_<c>`, `elite_caravan_template_<c>`. Erebor ships all of
   them, plus one per-clan template each for its seven clans, which is 21 ids carrying `erebor`.
   <!-- measured: rg -o '<MBPartyTemplate id="[^"]*erebor[^"]*"' Main/_Module/ModuleData/taom_partyTemplates.xml | wc -l 2026-09-05 -->
2. Add a `kingdom_hero_party_<c>_<clan>_N_template` for each clan you intend to give its own roster.
3. Bind the eight culture attributes and both caravan child lists in `taom_spcultures.xml`, per the
   table in "What binds a template" above. See [Cultures](cultures.md).
4. Bind each clan's `default_party_template` in `Main/_Module/ModuleData/characters/clans.xml` or
   `spclans.xslt`. See [Clans](clans.md).
5. Grep both culture files for each new id. Zero hits means the template is dead data and the culture
   quietly fields Calradians, which has shipped four times
   ([troops rule](../../.claude/rules/troops.md)).

Check: `dotnet test TAOM.Tests --filter CulturePartyTemplate -p:DisableModuleCopy=true -p:ModuleId=`
Takes effect: new campaign only
Code: No code changes needed

### Modify a culture's band with the retarget tool

1. Read the current sums before you touch anything. A dry run prints every template with its min sum,
   its current max sum and its target. In game, the console command `taom.print_party_size` prints
   the main party's size-limit chain, which is the cap these spawn numbers should sit near
   (`Main/Features/TroopWeight/Cheats/TroopWeightCheats.cs:24`).
2. Edit `CULTURE_TARGETS` at the top of
   [`tools/rebalance_party_template_maxes.py`](../../tools/rebalance_party_template_maxes.py). The
   target is an absolute max sum per culture, not a multiplier, so a re-run against an already
   retargeted file changes nothing.
3. Dry-run, read the report, then re-run with `--apply`. The tool scales each stack's spread
   (`max - min`) and never touches `min_value`, which is what keeps `max >= min` true.
4. Read the sums back out of the file afterwards, not out of a doc. Today's shipped sums are goblin
   and Blue Craig 320, the five orc and uruk cultures 260, Erebor 225, the men cultures 200, and the
   elf cultures 150. <!-- measured: python tools/rebalance_party_template_maxes.py 2026-09-05 -->
5. Erebor is the one template currently off its own target: the file sums to 225 against a target of
   220, so a dry run reports 193 templates in scope and 5 stacks that would change.
   <!-- measured: python tools/rebalance_party_template_maxes.py 2026-09-05 -->
6. The tool's scope is `kingdom_hero_party_*` minus the mercenary and outlaw variants. Militia,
   villager, caravan, patrol, rebel and vassal-reward templates are hand-authored and are never
   retargeted.

Check: `python tools/rebalance_party_template_maxes.py`
Takes effect: next save load, and only for parties spawned after it
Code: No code changes needed

### Delete a stack

1. Remove the whole `<PartyTemplateStack ... />` line. Do not blank its numbers: a stack left at
   `min_value="0" max_value="0"` can never spawn, and no later retarget restores it because the tool
   scales a spread that is already zero
   ([party-template-sizing.md](../reference/party-template-sizing.md)).
2. If you are deleting the troop as well, delete it from every template that names it. Grep the file
   for the troop id first.
3. Re-run the retarget tool for that culture so the remaining stacks absorb the removed budget.
4. On a `vassal_reward_troops_*` template, deleting a stack removes exactly that many troops from the
   gift: that path hands the player `stack.MaxValue` of every stack flat, with no ratio roll
   (`DefaultVassalRewardsModel.cs:47`).

Check: `python tools/validate_moduledata.py --code BROKEN_PARTY_TEMPLATE_REF`
Takes effect: next save load, and only for parties spawned after it
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A misspelled `troop` id does not crash, an undotted one does.** `GetPresumedObject` registers a
  placeholder for any id it has not seen, because load order is not guaranteed, so a typo loads clean
  and leaves a broken stack (`MBObjectManager.cs:713-735`); only
  `python tools/validate_moduledata.py` catches that
  ([moduledata-validation.md](../features/moduledata-validation.md)). A value with no dot at all
  throws `MBInvalidReferenceException` at load (`MBObjectManager.cs:1526-1527`).
- **A template nothing binds is dead data, in silence.** Two of the 193 lord templates in scope are
  bound by no culture and no clan today, `kingdom_hero_party_gondor_ithilien_template` and
  `kingdom_hero_party_gondor_belfalas_template`. <!-- measured: python -c "import re,os;r='Main/_Module/ModuleData';t=open(os.path.join(r,'taom_partyTemplates.xml'),encoding='utf-8-sig').read();ids={i for i in re.findall(r'<MBPartyTemplate id=\"([^\"]+)\"',t) if re.match(r'^kingdom_hero_party_(?!mercenary_|outlaw_).+_template$',i)};ref=set();[ref.update(re.findall(r'PartyTemplate\.([A-Za-z0-9_]+)',open(os.path.join(r,f),encoding='utf-8-sig',errors='replace').read())) for f in ('taom_spcultures.xml','spcultures.xslt','characters/clans.xml','spclans.xslt')];print(len(ids),sorted(ids-ref))" 2026-09-05 -->
- **A null or empty binding crashes in vanilla code with no TAOM frame on the stack.**
  `SpawnPatrolParty` dereferences the culture's patrol template immediately
  (`PatrolPartiesCampaignBehavior.cs:650`) and `SpawnCaravan` runs a predicate over each entry of the
  caravan list (`CaravansCampaignBehavior.cs:498`). Row 13 of the fourteen-row checklist in
  [culture-playability-wiring.md](../features/culture-playability-wiring.md) is this exact surface,
  and it has shipped broken nine times.
- **`max_value` is not the party's size, and reading it as one has already cost a balance pass.** The
  sum is where the spawn arithmetic tops out, and a villager party with a Village Network governor
  goes 10% past even that. `PartySizeLimit` governs the steady state, and a party spawned above its
  limit is drained by desertion within days
  ([campaign-mechanics lessons](../reviews/lessons/campaign-mechanics.md), "A ModuleData field's NAME
  is not its semantics").
- **`GetUpperTroopLimit()` and `GetLowerTroopLimit()` look authoritative and have no caller.** The
  only file in the v1.4.8 dump that mentions either name is the one that defines them, twice
  (`PartyTemplateObject.cs:62`, `:72`). Do not reason from the method names. <!-- measured: rg -l "GetUpperTroopLimit|GetLowerTroopLimit" <decompile root> -g '*.cs' 2026-09-05 -->
- **The cap side of party size is not in any XML file.** It is seven MCM knobs in the "AI Party Size"
  group, declared in `Main/Features/TaomSettings.cs`, with no JSON surface
  ([ai-party-size.md](../features/ai-party-size.md)). <!-- measured: rg -c 'SettingPropertyGroup("AI Party Size"' Main/Features/TaomSettings.cs 2026-09-05 -->
  No doc catalogues the whole MCM surface; that file is the list.
- **Four cultures field another culture's lord roster**, so retuning one retunes several: `lothlorien`
  points at Rivendell's template and `umbar`, `shaghana` and `abanissa` all point at Harad's.
  <!-- measured: python -c "import re;s=open('Main/_Module/ModuleData/taom_spcultures.xml',encoding='utf-8-sig').read();print([(re.search(r'id=\"([^\"]+)\"',b).group(1),re.search(r'default_party_template=\"PartyTemplate\.([^\"]+)\"',b).group(1)) for b in re.findall(r'<Culture\b.*?</Culture>',s,re.S) if re.search(r'default_party_template=\"PartyTemplate\.([^\"]+)\"',b) and re.search(r'id=\"([^\"]+)\"',b).group(1) not in re.search(r'default_party_template=\"PartyTemplate\.([^\"]+)\"',b).group(1)])" 2026-09-05 -->
- **A villager party's look comes from a random stack, not from the numbers**
  (`VillagerCampaignBehavior.cs:180`), so min and max there control only the party's size. A hideout
  boss stack must stay at 1/1 for the same reason in reverse: hideouts fill from a party template
  too (`BanditSpawnCampaignBehavior.cs:312-324`), so widening a boss stack multiplies the boss.
- **Whatever the tool says, the numbers on disk are the truth.** Docs have gone stale here twice: an
  older Mordor figure of 3500 and an Erebor figure of 220 both survive in prose that the file
  contradicts. Re-measure before quoting.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 383 templates, 3,295 stacks | `rg -c '<MBPartyTemplate id=' Main/_Module/ModuleData/taom_partyTemplates.xml` and `rg -c '<PartyTemplateStack' Main/_Module/ModuleData/taom_partyTemplates.xml` | 2026-09-05 |
| 193 lord templates in the tool's scope, 2 of them bound by nothing | the unbound-template one-liner in the Gotchas bullet above | 2026-09-05 |
| `kingdom_hero_party_erebor_template`: 53 stacks, min 103, max 225; `caravan_template_erebor`: 3 stacks, min 20, max 29 | the per-template one-liner in the Worked example above, once per id | 2026-09-05 |
| Erebor culture: 10 `PartyTemplate.` references, 8 attributes and 2 caravan children | the culture-block one-liner in the Worked example above | 2026-09-05 |
| Current max sums: goblin and bluecraig 320, mordor / isengard / gundabad / dolguldur / mistymountainorcs 260, erebor 225 against a 220 target, gondor / rohan / dale / dunland / rhun / harad 200, rivendell / mirkwood / lindon 150 | `python tools/rebalance_party_template_maxes.py` | 2026-09-05 |
| 193 templates in scope, 5 stacks would change on a re-run | `python tools/rebalance_party_template_maxes.py` | 2026-09-05 |
| 21 template ids carrying `erebor` | `rg -o '<MBPartyTemplate id="[^"]*erebor[^"]*"' Main/_Module/ModuleData/taom_partyTemplates.xml \| wc -l` | 2026-09-05 |
| 7 MCM knobs in the "AI Party Size" group | `rg -c 'SettingPropertyGroup("AI Party Size"' Main/Features/TaomSettings.cs` | 2026-09-05 |
| 4 cultures whose `default_party_template` is another culture's | the shared-template one-liner in the Gotchas bullet above | 2026-09-05 |
| 1 file in the whole decompile dump mentions `GetUpperTroopLimit` or `GetLowerTroopLimit`, the one defining them | `rg -l "GetUpperTroopLimit\|GetLowerTroopLimit" <decompile root> -g '*.cs'` | 2026-09-05 |

## Read next

- [party-template-sizing.md](../reference/party-template-sizing.md), [ai-party-size.md](../features/ai-party-size.md), [culture-playability-wiring.md](../features/culture-playability-wiring.md), [kingdom-creation.md](../features/kingdom-creation.md)
- [campaign-mechanics lessons](../reviews/lessons/campaign-mechanics.md), [troops rule](../../.claude/rules/troops.md), [tools README](../../tools/README.md)
- [Troops](troops.md), [Cultures](cultures.md), [Clans](clans.md), [Balance levers](balance-levers.md), [Validation and testing](validation-and-testing.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](./balance-levers.md)
- [docs/modding/clans.md](./clans.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/settlements.md](./settlements.md)
- [docs/modding/troops.md](./troops.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
