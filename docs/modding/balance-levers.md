# Balance levers

## What this chapter is

The file chapters say where a number lives and what the engine reads it as. This one says what changing that number actually does: the formula that consumes it, the value TAOM ships, and whether you can reach it from a text editor at all. Everything below is organised by the number you would edit, not by the file it happens to sit in.

Four surfaces carry balance values, and which one a number sits on decides who can change it:

| Surface | Example | Who can change it |
|---|---|---|
| ModuleData XML | a troop's `level=`, an item's `body_armor=` | anyone with a text editor |
| ModuleData JSON | `settlement_food_config.json` | anyone with a text editor |
| C# constant | the wage table, a culture feat's magnitude | needs a code change and a rebuild |
| MCM setting, in game | AI lord party size multiplier | the player, at runtime, no file |

TAOM's MCM surface is 432 `[SettingProperty]` declarations in `Main/Features/TaomSettings.cs`. <!-- measured: grep -c "\[SettingProperty" Main/Features/TaomSettings.cs 2026-09-05 --> Nothing in the repo catalogues them: [`docs/features/mcm.md`](../features/mcm.md) is a layout fix for one patch, not a settings list. If you need to know whether a knob exists, read `TaomSettings.cs` directly. That is the honest answer, and it has been the honest answer for as long as the file has existed.

## Lever 1: a troop's `level`, and the four numbers it decides

`level=` is the single most load-bearing number on a troop, because four separate systems derive from it and none of them reads anything else.

<!-- engine-ref type="TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultCharacterStatsModel.cs" lines="18-25" -->

| Derived value | Formula | Source |
|---|---|---|
| Tier | `clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)` | `DefaultCharacterStatsModel.cs:18-25` |
| `MaxCharacterTier` | 10 in TAOM, 6 in vanilla | `Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs:23`, `DefaultCharacterStatsModel.cs:11` |
| Daily wage | a hardcoded table keyed by **tier** | `Main/Features/TroopProgression/TroopCostService.cs:9-36` |
| Recruitment cost | a hardcoded table keyed by **level** | `TroopCostService.cs:38-60` |
| Auto-resolve power | `(2 + tier) * (8 + tier) * 0.02 * (mounted ? 1.2 : 1)` | `CharacterObject.cs:603-606`, `CharacterObject.cs:856-859` |

The two tables are keyed differently on purpose, and that is easy to trip over. Wage reads tier, so levels 21 through 25 all pay the same. Recruitment cost reads level through a banded `switch`, so it also steps in fives, but the bands are `<= 6`, `<= 11`, `<= 16` and so on, offset by one from the tier bands.

| Tier | Levels | Wage per day | Recruitment cost at the band top |
|---:|---|---:|---:|
| 0 | 1 to 5 | 1 | 10 at level 1, 20 up to 6 |
| 1 | 6 to 10 | 2 | 50 up to level 11 |
| 2 | 11 to 15 | 3 | 200 up to level 16 |
| 3 | 16 to 20 | 5 | 400 up to level 21 |
| 4 | 21 to 25 | 8 | 600 up to level 26 |
| 5 | 26 to 30 | 12 | 1000 up to level 31 |
| 6 | 31 to 35 | 15 | 1500 up to level 36 |
| 7 | 36 to 40 | 18 | 2100 up to level 41 |
| 8 | 41 to 45 | 20 | 2800 up to level 46 |
| 9 | 46 to 50 | 25 | 3600 up to level 51 |
| 10 | 51 and up | 30 | 4000 above 51 |

Two multipliers sit on top of the wage: `MountedWageMultiplier` 1.3 and `MercenaryWageMultiplier` 1.5, both `const float` at `TroopCostService.cs:5-6`, applied in that order and truncated to an int. Mercenary recruitment doubles (`MercenaryRecruitMultiplier` 2, `TroopCostService.cs:7`).

`IsMounted` is read off the equipment roster's Horse slot, not off `level` or `default_group`, so giving an existing rung a horse raises both its wage and its auto-resolve power by more than the level change would suggest. The claim in [`auto-resolve-diagnostics.md:9`](../features/auto-resolve-diagnostics.md) that level is the only attribute a simulated battle scores from is too absolute: `GetPower` takes `IsMounted` as its third argument at `CharacterObject.cs:605`.

The levels TAOM actually uses across its 16 troop files are 1, 6, 7, 11, 16, 21, 26, 31, 36, 41, 46 and 51, spread over 857 troop entries. The only off-grid one is `morannon_recruit` at level 7. <!-- measured: python -c "import glob,re,collections;c=collections.Counter();[c.update(int(m) for m in re.findall(r'level=\"(\d+)\"',open(f,encoding='utf-8-sig').read())) for f in glob.glob('Main/_Module/ModuleData/troops/troops_*.xml')];print(sum(c.values()),sorted(c))" 2026-09-05 --> That grid matters for the skill tool as well: `rebalance_troops.py` keys its baselines on `{1, 6, 11, ... 51}` and skips any troop at a level it has no row for, so `morannon_recruit` is outside the curve entirely ([troop-skill-balance.md](../features/troop-skill-balance.md), "The formula").

## Lever 2: a troop's skill values

Where they live and how they are written is [Troops](troops.md). What they buy is here.

**In a real-time battle**, a skill value reaches the agent through `SandboxAgentStatCalculateModel`, the concrete single-player model. Three relationships are worth knowing because they have hard ceilings:

<!-- engine-ref type="SandBox.GameComponents.SandboxAgentStatCalculateModel" file="Modules/SandBox/SandBox.GameComponents/SandboxAgentStatCalculateModel.cs" lines="982-1000" -->

| Skill | What it drives | Ceiling |
|---|---|---|
| The weapon's `RelevantSkill` | Ranged accuracy penalty scales by `max(0, 1 - skill / 500)` on foot | Penalty reaches zero at 500. Points past 500 buy nothing on this term (`SandboxAgentStatCalculateModel.cs:993`) |
| Riding | On a mount the same penalty gains a second factor, `1 - riding / 1800` | 1800, a value no TAOM troop approaches (`SandboxAgentStatCalculateModel.cs:999`) |
| Athletics | Knockback and knockdown resistance | Read through `DefaultSkillEffects`, not a raw divide (`SandboxAgentStatCalculateModel.cs:398-427`) |
| Riding | Dismount resistance | Same shape (`SandboxAgentStatCalculateModel.cs:429-437`) |

A separate, level-driven term is easy to mistake for a skill effect. `BasicCharacterObject.cs:74` defines `SkillFactor` as `min(Level, 32) / 32`, and `Agent.cs:5085-5086` uses it to size the random error in an agent's formation position. It saturates at level 32, so a level 36 troop and a level 51 troop hold a line equally well.

**In a simulated map battle**, skills do nothing at all. The auto-resolve path scores from tier and mount only, which is why the project's main skill tool has never touched an axis auto-resolve can see ([auto-resolve-diagnostics.md:8-27](../features/auto-resolve-diagnostics.md)).

**Where the target numbers live.** `tools/rebalance_troops.py` holds the whole curve: `GROUP_BASELINES` at line 115 (four tables, Infantry / Ranged / Cavalry / HorseArcher, keyed by the eleven grid levels) and `CULTURAL_MODS` at line 126 (21 culture keys). <!-- measured: python -c "import re;s=open('tools/rebalance_troops.py',encoding='utf-8').read();a=open('tools/rebalance_armor.py',encoding='utf-8').read();print(len(re.findall(r\"^    '[a-z_]+':\",re.search(r'^CULTURAL_MODS = \{(.*?)^\}',s,re.S|re.M).group(1),re.M)),len(re.findall(r\"^    '[a-z_]+':\",re.search(r'^CULTURAL_MODS = \{(.*?)^\}',a,re.S|re.M).group(1),re.M)))" 2026-09-05 --> No doc prints the baseline tables; open the file. The per-culture deltas are printed in [troop-skill-balance.md:68-95](../features/troop-skill-balance.md). A missing `CULTURAL_MODS` key does not error, it silently rebaselines that whole faction to the bare curve, which is how a Lindon apply nearly stripped 30 troops in one run.

After the baseline and the culture delta, two more passes run: a weapon specialisation shift of `swap_amount = 15` (`tools/rebalance_troops.py:316`) triggered by keywords in the troop's name, and a roster-wide monotonicity clamp that raises a child's skill to its parent's over the whole upgrade graph.

## Lever 3: armour numbers

Where they live is [Armour items](items-armor.md). What they buy is two independent things: damage reduction, and price.

### Armour into damage

The campaign uses `SandboxStrikeMagnitudeModel`, whose `ComputeRawDamage` is byte for byte the same arithmetic as `DefaultStrikeMagnitudeModel`. TAOM registers no `StrikeMagnitudeModel` override, so this curve is live as vanilla wrote it. <!-- measured: grep -rn "StrikeMagnitudeModel" Main/ returns no match 2026-09-05 -->

<!-- engine-ref type="SandBox.GameComponents.SandboxStrikeMagnitudeModel" file="Modules/SandBox/SandBox.GameComponents/SandboxStrikeMagnitudeModel.cs" lines="220-247" -->

Armour is applied twice in the same expression, once as a curve and once as a flat subtraction, and the damage type decides how the two are blended:

```
scaled     = magnitude * 50 / (50 + armour)
subtracted = max(0, scaled - armour * K)          K: Cut 0.5, Pierce 0.33, Blunt 0.2
result     = B * scaled + (1 - B) * subtracted    B: Blunt 0.6, Pierce 0.25, Cut 0.1
```

`B` is `GetBluntDamageFactorByDamageType` (`DefaultStrikeMagnitudeModel.cs:59-75`, mirrored at `SandboxStrikeMagnitudeModel.cs:249`). Because Cut leans almost entirely on the subtracted term and Blunt almost entirely on the scaled one, the same ten points of armour are worth very different amounts against different weapons. Damage surviving a strike of magnitude 100:

| Armour on the struck part | Cut | Pierce | Blunt |
|---:|---:|---:|---:|
| 0 | 100.0 | 100.0 | 100.0 |
| 33 | 45.4 | 52.1 | 57.6 |
| 43 | 34.4 | 43.1 | 50.3 |
| 60 | 18.5 | 30.6 | 40.7 |

<!-- measured: python -c "bf={'Blunt':0.6,'Cut':0.1,'Pierce':0.25}; sub={'Cut':0.5,'Pierce':0.33,'Blunt':0.2}; f=lambda t,ae:(lambda n2: bf[t]*n2+(1-bf[t])*max(0.0,n2-ae*sub[t]))(100*50.0/(50.0+ae)); print([(ae,{t:round(f(t,ae),1) for t in ('Cut','Pierce','Blunt')}) for ae in (0,33,43,60)])" 2026-09-05 -->

Read that as: moving a chest from 33 to 43 cuts incoming Cut damage by about a quarter and incoming Blunt by about a twelfth. It is not a small change against swords and it is close to nothing against maces, which is the reason a blanket "+10 to everything" pass never lands where the author expected.

### Armour into price

An item with no `value=` attribute has its price computed. With one, the attribute wins verbatim (`ItemObject.cs:477-484`). TAOM registers no `ItemValueModel` override, so vanilla computes it. <!-- measured: grep -rn "ItemValueModel" Main/ returns no match 2026-09-05 -->

<!-- engine-ref type="TaleWorlds.Core.DefaultItemValueModel" file="Core/TaleWorlds.Core/TaleWorlds.Core/DefaultItemValueModel.cs" lines="9-29" -->

```
armourTier = (1.2*head + body + leg + arm) * typeMult * 0.1 - 0.4
             typeMult: Leg 1.6, Hand 1.7, Head 1.2, Cape 1.8, Body 1.0
value      = base * 2.75 ^ clamp(armourTier, -1, 7.5) * (1 + 0.2*(appearance - 1))
             + 100 * max(0, appearance - 1)
             base: 120 for Body, Hand and Leg armour, 100 for everything else
```

`CalculateArmorTier` at `DefaultItemValueModel.cs:9-29`, `CalculateValue` at `:219-252`, `GetEquipmentValueFromTier` at `:264-267`. `appearance` defaults to 0.5 when the attribute is absent (`ItemObject.cs:553-554`), which is below 1 and therefore *lowers* the price.

The exponent is the part that surprises people. All four armour stats on one item sum into a single tier before the power of 2.75, so a chest that also carries `leg_armor` and `arm_armor` prices like a much heavier piece. Keeping an item cheap means keeping it single-stat, not just low.

### The curve TAOM balances against

`tools/rebalance_armor.py` is the source of truth: `SLOT_BASELINES` at line 112 (per tier, per slot, including the secondary stats that exist only in code) and `CULTURAL_MODS` at line 173, 20 culture keys, each an additive protection value and a multiplicative weight. The printed tables are in [armor-balance.md:30-104](../features/armor-balance.md). Seven cultures are hand tuned and are skipped unless you pass `--all`: `PRESERVE_CULTURES` at `tools/rebalance_armor.py:43` lists gondor, mordor, isengard, dol_guldur, gundabad, erebor and iron_hills.

There is a second, older curve. `tools/generate_gondor_armor.py` carries `STAT_TIERS`, imported by the per-culture generators, and it does not import `rebalance_armor`'s tables. The two have never been reconciled and no doc says which wins for a given item. If you are re-stating existing items, use `rebalance_armor.py`; the generators only matter when authoring a new set.

**Item modifiers change the arithmetic.** A legendary roll adds a flat bonus to every nonzero armour stat *independently*, each guarded by `num > 0` (`EquipmentElement.cs:159` for body, `EquipmentElement.cs:218` for arm). A two-stat cape takes the bonus twice; a stat of exactly 0 is immune. Which ladder an item rolls on comes from its `modifier_group`, and the legal values are the 20 group ids in `Native/ModuleData/item_modifiers_groups.xml`: sword, bow, crossbow, arrow, bolt, cheap_weapon, polearm, mace, axe, axe_throwing, knife_throwing, spear_dart_throwing, shield, plate, chain, leather, cloth, cloth_unarmoured, horse, companion. <!-- measured: grep -oE 'id="[^"]+"' Native/ModuleData/item_modifiers_groups.xml | wc -l 2026-09-05 --> This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. `legendary_plate` and its siblings are ItemModifier ids from the sibling `item_modifiers.xml`, not group names, and writing one into `modifier_group` resolves to null with no warning.

## Worked example

### A Gondor spearman, from `level` to auto-resolve power

<!-- example file="Main/_Module/ModuleData/troops/troops_gondor.xml" id="gondor_pg_spearman" -->

```xml
  <NPCCharacter
      id="gondor_pg_spearman"
      default_group="Infantry"
      level="21"
      name="{=aom_gondor_pg_spearman_name}[Gondor] Pinnath Gelin Spearman"
      occupation="Soldier"
      culture="Culture.gondor">
    <face>
      <face_key_template value="BodyProperty.fighter_gondor" />
    </face>
    <skills>
      <skill id="Athletics" value="100" />
      <skill id="Riding" value="20" />
      <skill id="OneHanded" value="120" />
      <skill id="TwoHanded" value="115" />
      <skill id="Polearm" value="150" />
      <skill id="Bow" value="15" />
      <skill id="Crossbow" value="10" />
      <skill id="Throwing" value="40" />
    </skills>
    <upgrade_targets>
      <upgrade_target id="NPCCharacter.gondor_pg_vet_spearman" />
      <upgrade_target id="NPCCharacter.gondor_pg_cavalry" />
    </upgrade_targets>
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_gondor_spear_b" />
        <equipment slot="Item1" id="Item.gond_shield_three_green" />
        <equipment slot="Head" id="Item.sk_gd_pin_spear_helmet_heavy_a" />
        <equipment slot="Body" id="Item.sk_gd_pin_inf_chest_med_b" />
        <equipment slot="Cape" id="Item.sk_gd_osg_pauld_cape_inf_elite_a" />
        <equipment slot="Gloves" id="Item.sk_gd_ano_bracer_inf_med_a" />
        <equipment slot="Leg" id="Item.sk_gd_ano_grvs_inf_med_a" />
      </EquipmentRoster>
      <EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
    </Equipments>
  </NPCCharacter>
```

1. **`level="21"`** gives tier `ceil((21 - 5) / 5) = 4`.
2. **Tier 4** gives a wage of 8 denars per day. No Horse slot, so no 1.3 multiplier; `occupation="Soldier"` is not a mercenary occupation, so no 1.5.
3. **Level 21** falls in the `<= 21` recruitment band, so 400 denars.
4. **Tier 4, on foot** gives auto-resolve power `(2+4) * (8+4) * 0.02 * 1.0 = 1.44`. <!-- measured: python -c "print((2+4)*(8+4)*0.02, (2+5)*(8+5)*0.02*1.2)" 2026-09-05 -->
5. **The skills reproduce the tool exactly.** Infantry baseline at level 21 is Athletics 95, OneHanded 125, TwoHanded 110, Polearm 130, Riding 15, Bow 15, Crossbow 10, Throwing 50 (`tools/rebalance_troops.py`, `INFANTRY_BASELINES`). Gondor's deltas are +5 / +5 / +10 / +5 / +5 and Throwing -10 ([troop-skill-balance.md:68-95](../features/troop-skill-balance.md)). The name contains "spear", so the specialisation pass shifts 15 from OneHanded into Polearm. That lands on 135 - 15 = 120 OneHanded and 135 + 15 = 150 Polearm, which is what the file says.

Its upgrade target `gondor_pg_cavalry` is level 26 with `slot="Horse"` filled: tier 5, wage `12 * 1.3 = 15`, power `(2+5) * (8+5) * 0.02 * 1.2 = 2.184`. One rung up the tree is worth 52 percent more auto-resolve power and 87 percent more wage, and the horse is doing most of both.

### One chest, from four numbers to a price

<!-- example file="LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/body_armors.xml" id="sk_gd_pin_inf_chest_med_b" -->

```xml
    <Item
        id="sk_gd_pin_inf_chest_med_b"
        name="{=aom_sk_gd_pin_inf_chest_med_b_name}[Gondor] Pinnath Gelin Armour B"
        subtype="body_armor"
        mesh="sk_gd_pin_inf_chest_med_b"
        culture="Culture.gondor"
        is_merchandise="true"
        weight="13.0"
        difficulty="0"
        appearance="3"
        Type="BodyArmor">
        <ItemComponent>
            <Armor body_armor="33" arm_armor="10" has_gender_variations="false" covers_body="true" modifier_group="chain" material_type="Chainmail" />
        </ItemComponent>
        <Flags UseTeamColor="true" />
    </Item>
```

1. **`body_armor="33"` plus `arm_armor="10"`** both feed the tier sum: `(0 + 33 + 0 + 10) * 1.0`, then `* 0.1 - 0.4` gives tier 3.9. The arm stat is not free; it raises the price as much as ten points of body armour would.
2. **No `value=` attribute**, so the engine computes: `120 * 2.75^3.9 * 1.4 + 200`, which is **8883 denars**. <!-- measured: python -c "print(int(120*2.75**((33+10)*0.1-0.4)*(1+0.2*(3-1))+100*(3-1)), round(2.75**((33+10)*0.1-0.4),2))" 2026-09-05 -->
3. **`appearance="3"`** is a price lever of its own: it contributes the `1.4` factor and the flat `+200`. Dropping it to the default 0.5 would price the same protection at 5582. <!-- measured: python -c "print(int(120*2.75**((33+10)*0.1-0.4)*(1+0.2*(0.5-1))+100*max(0,0.5-1)))" 2026-09-05 -->
4. **`modifier_group="chain"`** picks the ladder a loot roll uses. A legendary chain roll adds 9 to each nonzero stat, so this piece can reach 42 body and 19 arm in play. Balance the tier ladder against the rolled number, not the base one.

## Lever 4: party size, which is two different numbers

A lord's roster at spawn and the cap he settles at are separate systems, and confusing them is the usual reason a party-size change appears not to work.

**Spawn roster** comes from `taom_partyTemplates.xml`, 383 `<MBPartyTemplate>` blocks, each a list of `<PartyTemplateStack>` rows with `min_value` and `max_value`. The template's spawn ceiling is the sum of its stacks' `max_value`. Current sums for the base kingdom templates: goblin 320, mordor 260, isengard 260, erebor 225, gondor 200, rivendell 150. <!-- measured: python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/taom_partyTemplates.xml').getroot();t=r.findall('.//MBPartyTemplate');print(len(t));print({p.get('id').split('_')[3]:sum(int(s.get('max_value')) for s in p.findall('.//PartyTemplateStack')) for p in t if p.get('id') in ('kingdom_hero_party_goblin_template','kingdom_hero_party_mordor_template','kingdom_hero_party_isengard_template','kingdom_hero_party_erebor_template','kingdom_hero_party_gondor_template','kingdom_hero_party_rivendell_template')})" 2026-09-05 --> [`party-template-sizing.md`](../reference/party-template-sizing.md) still narrates a 3500 target for Mordor and 4500 for goblins. Those numbers were retargeted on 2026-09-01 and the doc was not updated. Sum the file, do not trust the prose. Mechanics of the two attributes are in [Party templates](party-templates.md).

**Steady-state cap** is `TaomPartySizeModel.GetPartyMemberSizeLimit`, and four writers land on the one `ExplainedNumber` vanilla hands back, in this order (`Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs:38-62`):

| Order | Contributor | Shape |
|---:|---|---|
| 1 | Culture party-size feat | `AddFactor`, a percentage, 12 cultures |
| 2 | Career `PartySize` passive | flat `Add`, authored as a body count |
| 3 | AI lord scaling (MCM) | multiplier then flat bonus, must run before 4 |
| 4 | TroopWeight elite tax | result-frame subtraction of the weight surplus |

The 12 cultures at contributor 1 are Mordor, Gundabad, Goblin, Blue Craig, Misty Mountain Orcs, Dol Guldur, Isengard, Gondor, Dunland, Rhun, Harad and Khand (`Main/Features/CulturalFeats/CulturalFeatsService.cs:245-273`). Their magnitudes are C#, not XML: `taom_spcultures.xml` only binds the feat id, and the number is the third argument to `FeatObject.Initialize` (Mordor's party size feat is `0.2f` at `Main/Features/CulturalFeats/TaomCulturalFeats.cs:912-915`). Changing a feat's strength is a code change.

Contributor 4 is the elite tax. `ComputeSizePenalty` subtracts `weightedCount - rawCount`, clamped at 0 below and at `baseLimit - 1` above (`Main/Features/TroopWeight/TroopWeightService.cs:172-187`). Weights come from `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`, which holds **105 live `<TroopWeight>` rows: 93 at weight 2.0, 10 at 3.0, one at 4.0 and one at 10.0**. A raw grep returns 106 because one `cave_troll` row sits inside a comment block. <!-- measured: python -c "import xml.etree.ElementTree as ET,collections;r=ET.parse('Main/_Module/ModuleData/TroopWeights/troop_weights.xml').getroot();w=[x.get('weight') for x in r.findall('.//TroopWeight')];print(len(w),sorted(collections.Counter(w).items()))" 2026-09-05 --> [`troop-weight-system.md:339`](../features/troop-weight-system.md) says "~80 entries" and [`gamemodels-services.md:418`](../reviews/lessons/gamemodels-services.md) says "87 live weighted ids, 75 at 2.0". Both are stale; the file above is the count. An unlisted troop weighs 1.0, stated in the file's own header comment on line 3, and there is no other default anywhere.

The 4.0 and 10.0 tiers are undocumented in every feature doc. They exist, and nothing says what they were sized against.

**MCM only.** Seven knobs under the "AI Party Size" group are the biggest party-size lever in the mod, and none of them is reachable from any file. All five numeric ones ship at vanilla: `AiLordPartySizeFactor` 1.0, `AiLordPartySizeFlatBonus` 0, `AiGarrisonSizeFactor` 1.0, `AiFoodConsumptionRelief` 0, `AiWageRelief` 0 (`Main/Features/AiPartySize/AiPartySizeService.cs:67-77`, declared at `Main/Features/TaomSettings.cs:40-79`). The reason there is no JSON twin is written down in [`ai-party-size.md:171-176`](../features/ai-party-size.md): two surfaces for one value have to enforce the same invariants at both or they drift.

## Lever 5: settlement economy

The engine numbers a balancer needs, re-read from the v1.4.8 dump because the line numbers in [`settlement-economy-food-prosperity.md`](../reference/engine/settlement-economy-food-prosperity.md) were taken against v1.4.5:

<!-- engine-ref type="TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultSettlementFoodModel.cs" lines="28-78" -->

| Quantity | Value | Source |
|---|---|---|
| Prosperity eaten per food | 40 | `DefaultSettlementFoodModel.cs:32`, used at `:47` |
| Garrison men per food | 20 | `:34`, used at `:48` |
| Food stock cap | 300 town, plus 150 for a castle | `:30`, `:36` |
| Base production | 15 town, 10 castle | `:65` |
| Per village production | `(hearthLevel + 1) * 6` | `:72` |
| Village hearth level | 2 at hearth 600, 1 at 200, else 0 | `Village.cs:320-331` |
| Town prosperity level | High at 5000, Mid at 2000, else Low | `Town.cs:738-749` |

The first five rows above are exposed as knobs in `Main/_Module/ModuleData/settlement_food/settlement_food_config.json`, seven of them once the town and castle variants are counted separately, plus a `flatFoodBonus` with no vanilla equivalent: eight keys in all, and TAOM ships every one at the vanilla value, so the file changes nothing until you edit it. <!-- measured: python -c "import json;print(sorted(json.load(open('Main/_Module/ModuleData/settlement_food/settlement_food_config.json'))))" 2026-09-05 --> The last two rows, hearth level and prosperity level, are engine thresholds with no knob. The knob names and shapes are in [Balance configs](configs-balance.md).

Prosperity and hearth themselves are per-settlement values in the live map module, retuned with `tools/rebalance_settlement_prosperity.py`. Its floor table is `tools/settlement_economy_floor.json`: town 4800, castle 950, village hearth 500, applied to eight cultures (bluecraig, dolguldur, goblin, gundabad, isengard, lindon, mirkwood, mistymountainorcs). Note where those floors sit against the engine thresholds above. A town at 4800 is just under the 5000 High band, and a village at hearth 500 is level 1, not level 2, so it produces 12 food per day rather than 18. Whether that is deliberate headroom or an accident is not written down anywhere.

Militia is a separate model (`TaomSettlementMilitiaModel`, culture feats only). Settlement fields themselves are [Settlements](settlements.md).

## What you cannot change without touching code

Worth knowing before you go looking for a file that does not exist:

- **Troop wages and recruitment costs.** Both tables are `switch` expressions in `Main/Features/TroopProgression/TroopCostService.cs:9-60`. Nothing in ModuleData reaches them.
- **Culture feat magnitudes.** The XML binds a feat id; the number is the third argument to `Initialize` in `Main/Features/CulturalFeats/TaomCulturalFeats.cs`.
- **The skill baseline curve and the armour curve.** Python constants in `tools/rebalance_troops.py` and `tools/rebalance_armor.py`. They are not read at runtime: they generate XML, and the XML is what ships.
- **Auto-resolve power, the armour damage curve and the item value curve.** All vanilla engine models that TAOM does not override.
- **The seven AI party size knobs.** MCM only, by design.

## Recipes

### Retune one troop's skills

1. Open the troop's file under `Main/_Module/ModuleData/troops/troops_<culture>.xml` and find the `<skills>` block.
2. Change the `value=` numbers you want. Keep the eight-skill shape the rest of the file uses.
3. Check the upgrade edges: if this troop has a parent, the parent must not out-skill it. The monotonicity gate is checked by the validator, not by the game.
4. If the change is meant to survive the next curve pass, put it in `SKIP_TROOP_IDS` in `tools/rebalance_troops.py` or the next `--apply` will overwrite it.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

### Retune a whole culture's troop skills

1. Read the current state first: `python tools/analyze_troop_balance.py --stdout`, then open `tools/reports/troop-balance/REPORT.html`.
2. Edit `CULTURAL_MODS['<culture>']` at `tools/rebalance_troops.py:126` for identity, or `GROUP_BASELINES` at line 115 for the whole power curve.
3. Preview: `python tools/rebalance_troops.py --dry-run`. Read the downward changes first; a big drop usually means the modifier is too weak, not that the troops were over-tuned.
4. Apply: `python tools/rebalance_troops.py --apply`.
5. Read `git diff --stat` **per file**, not just the total. A file that moved an order of magnitude more than expected is the tool doing something you did not ask for. That is how a missing `CULTURAL_MODS` key was caught mid-apply, at 478 lines against an expected 30 ([troop-skill-balance.md:229-239](../features/troop-skill-balance.md)).

Check: `python tools/validate_moduledata.py` and confirm no `UPGRADE_SKILL_REGRESSION` rows
Takes effect: full game restart
Code: No code changes needed

### Resize lord parties

1. Decide which number you are moving. The spawn roster is the party template; the cap a lord holds is the MCM knob. Moving one without the other produces the two classic failures: raise the template alone and parties shed within a day, raise the cap alone and nothing spawns big enough to fill it.
2. For the spawn roster, edit `CULTURE_TARGETS` at the top of `tools/rebalance_party_template_maxes.py`, then `python tools/rebalance_party_template_maxes.py` for the dry run and `--apply` to write. The tool holds `min_value` fixed and scales each stack's spread, which is what keeps `max >= min` for every stack.
3. For the cap, set "AI Lord Party Size Multiplier" and "AI Lord Party Size Flat Bonus" in MCM. Move "Garrison Size Multiplier" with them: vanilla will not target a settlement unless the attacker is over twice the defender's strength, and garrison plus militia is that strength, so a high garrison multiplier with vanilla-sized lord parties stops AI sieges happening at all.
4. Existing parties keep the roster they already have. The template only decides what a party is handed at spawn.

Check: `python tools/validate_moduledata.py`, then `taom.print_party_size` in the in-game console
Takes effect: full game restart
Code: No code changes needed

### Change a wage or a recruitment cost

1. Open `Main/Features/TroopProgression/TroopCostService.cs`.
2. Wages are the tier `switch` at lines 9 to 36. Recruitment costs are the level `switch` at lines 38 to 60. The three multipliers are `const float` at lines 5 to 7.
3. Update the tests that pin the tables in `TAOM.Tests` in the same change; they exist precisely so a silent table edit cannot ship.
4. Remember the two keys differ: a wage edit moves five levels at once, a cost edit moves a band whose boundaries are offset by one from the tier bands.

Check: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
Takes effect: full game restart
Code: Code changes required in `Main/Features/TroopProgression/TroopCostService.cs`

## Gotchas

- **The validator does not check ranges.** `tools/validate_moduledata.py` checks references and a handful of named invariants. Only three JSON schemas exist under `tools/schemas/` (`taom_npccharacter.json`, `taom_spcultures.json`, `taom_equipmentsets.json`), and none of them covers `taom_partyTemplates.xml`, `troop_weights.xml` or any Armory item file. A body armour of 500 passes every gate in the repo. <!-- measured: ls tools/schemas/ | wc -l 2026-09-05 -->
- **A skill above 500 buys nothing on the ranged accuracy term** and a level above 32 buys nothing on formation position error. `SandboxAgentStatCalculateModel.cs:993`, `BasicCharacterObject.cs:74`.
- **Two balance features write to one number and can cancel.** A heavy roster's elite tax can subtract more than a culture party-size feat adds, which is why the evil-culture feats carry a floor. `docs/reviews/lessons/gamemodels-services.md:408-425`.
- **An explicit `value=` on an item silences the whole price model.** `ItemObject.cs:477-484`. A hand-set `value="6000"` on a starter weapon is invisible to any armour or tier pass.
- **A `modifier_group` the engine does not know resolves to null with no warning**, and the item then never rolls any modifier. Two shipped examples exist: `modifier_group="mail"` and `modifier_group="false"`. The legal set is the 20 group ids listed above.
- **Party template prose in the docs is stale by a factor of 13.** Sum the file.
- **Troop weight doc counts are stale.** 105 live rows, not 80 and not 87.

### Questions this chapter cannot answer

- **What the full skill-to-stat mapping is.** Only the four relationships above were traced. `Modules/SandBox/SandBox.GameComponents/SandboxAgentStatCalculateModel.cs` is 1670 lines and no TAOM doc summarises it; [`agent-stats-and-driven-properties.md`](../reference/engine/agent-stats-and-driven-properties.md) covers the model plumbing and states no numbers, and its own citations are v1.4.5.
- **Which armour curve wins, `STAT_TIERS` or `SLOT_BASELINES`.** Both exist, in different tools, with different shapes, and nothing reconciles them.
- **What the 4.0 and 10.0 troop weights were sized against.** They are in the file and in no doc.
- **Whether a culture whose whole roster is heavy is meant to sit at half the party cap.** The elite tax degenerates into a flat cut for such a roster, Rivendell and Mirkwood are the two, and neither takes a party-size feat. [`troop-weight-system.md`](../features/troop-weight-system.md) raises the question and leaves it open.
- **Whether the settlement prosperity floors were chosen to sit just under the engine bands.** The floor table and the thresholds are both known; the intent is not recorded.

## Numbers in this chapter

| Number | Command | Date |
|---|---|---|
| 432 `[SettingProperty]` declarations | `grep -c "\[SettingProperty" Main/Features/TaomSettings.cs` | 2026-09-05 |
| 857 troop levels across 16 files; levels 1, 6, 7, 11, 16, 21, 26, 31, 36, 41, 46, 51 | `python -c "import glob,re,collections;c=collections.Counter();[c.update(int(m) for m in re.findall(r'level=\"(\d+)\"',open(f,encoding='utf-8-sig').read())) for f in glob.glob('Main/_Module/ModuleData/troops/troops_*.xml')];print(sum(c.values()),sorted(c))"` | 2026-09-05 |
| 21 troop culture keys, 20 armour culture keys | `python -c "import re;s=open('tools/rebalance_troops.py',encoding='utf-8').read();a=open('tools/rebalance_armor.py',encoding='utf-8').read();print(len(re.findall(r\"^    '[a-z_]+':\",re.search(r'^CULTURAL_MODS = \{(.*?)^\}',s,re.S\|re.M).group(1),re.M)),len(re.findall(r\"^    '[a-z_]+':\",re.search(r'^CULTURAL_MODS = \{(.*?)^\}',a,re.S\|re.M).group(1),re.M)))"` | 2026-09-05 |
| Damage surviving magnitude 100 at armour 0 / 33 / 43 / 60 | `python -c "bf={'Blunt':0.6,'Cut':0.1,'Pierce':0.25}; sub={'Cut':0.5,'Pierce':0.33,'Blunt':0.2}; f=lambda t,ae:(lambda n2: bf[t]*n2+(1-bf[t])*max(0.0,n2-ae*sub[t]))(100*50.0/(50.0+ae)); print([(ae,{t:round(f(t,ae),1) for t in ('Cut','Pierce','Blunt')}) for ae in (0,33,43,60)])"` | 2026-09-05 |
| 8883 denars, tier 3.9, 51.69 tier factor; 5582 at default appearance | `python -c "print(int(120*2.75**((33+10)*0.1-0.4)*(1+0.2*(3-1))+100*(3-1)), round(2.75**((33+10)*0.1-0.4),2))"` and the same with `0.5` for appearance | 2026-09-05 |
| Power 1.44 foot tier 4, 2.184 mounted tier 5 | `python -c "print((2+4)*(8+4)*0.02, (2+5)*(8+5)*0.02*1.2)"` | 2026-09-05 |
| 383 party templates; goblin 320, mordor 260, isengard 260, erebor 225, gondor 200, rivendell 150 | `python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/taom_partyTemplates.xml').getroot();t=r.findall('.//MBPartyTemplate');print(len(t))"` plus the per-template max sums | 2026-09-05 |
| 105 live `<TroopWeight>` rows: 93 at 2.0, 10 at 3.0, 1 at 4.0, 1 at 10.0 | `python -c "import xml.etree.ElementTree as ET,collections;r=ET.parse('Main/_Module/ModuleData/TroopWeights/troop_weights.xml').getroot();w=[x.get('weight') for x in r.findall('.//TroopWeight')];print(len(w),sorted(collections.Counter(w).items()))"` | 2026-09-05 |
| 20 item modifier groups | `grep -oE 'id="[^"]+"' Native/ModuleData/item_modifiers_groups.xml \| wc -l` | 2026-09-05 |
| 3 validator schemas | `ls tools/schemas/ \| wc -l` | 2026-09-05 |
| 8 keys in `settlement_food_config.json` | `python -c "import json;print(sorted(json.load(open('Main/_Module/ModuleData/settlement_food/settlement_food_config.json'))))"` | 2026-09-05 |
| No `StrikeMagnitudeModel` or `ItemValueModel` override in TAOM | `grep -rn "StrikeMagnitudeModel" Main/` and `grep -rn "ItemValueModel" Main/`, both empty | 2026-09-05 |

## Read next

- [troop-skill-balance.md](../features/troop-skill-balance.md)
- [armor-balance.md](../features/armor-balance.md)
- [starting-equipment-tuning.md](../features/starting-equipment-tuning.md)
- [troop-weight-system.md](../features/troop-weight-system.md)
- [auto-resolve-diagnostics.md](../features/auto-resolve-diagnostics.md)
- [troop-progression.md](../features/troop-progression.md)
- [ai-party-size.md](../features/ai-party-size.md)
- [cultural-feats.md](../features/cultural-feats.md)
- [party-template-sizing.md](../reference/party-template-sizing.md)
- [gamemodel-registry.md](../reference/gamemodel-registry.md)
- [settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)
- [lessons: GameModels and Services](../reviews/lessons/gamemodels-services.md)
- [tools README](../../tools/README.md)
