# Starting Equipment Tuning

## Overview

Character-creation starting gear is deliberately low-value so a new player cannot immediately sell their kit for
a fortune. This doc records how item resale value actually works in the engine (the part that surprised us) and
how TAOM's starter gear is structured so it stays cheap.

## Why this exists

Players were selling their character-creation kit for ~20,000 denars. The instinct was "lower the armor stats,"
but the real driver was item *value*, which the engine computes in a non-obvious way.

## How item value works (the load-bearing engine facts)

- `TaleWorlds.Core.ItemObject.Deserialize` reads the XML `value=` attribute: **if present it is used verbatim;
  if absent it calls `DetermineValue()` → `ItemValueModel.CalculateValue(this)`.** So an explicit `value=` in the
  item XML *overrides* any stat-based computation.
- `DefaultItemValueModel.CalculateValue` ≈ `num2 * GetEquipmentValueFromTier(Tierf) * appearanceFactor`, where
  `num2` = 120 for Body/Hand/Leg armor, 100 for everything else, and
  **`GetEquipmentValueFromTier(t) = 2.75 ^ clamp(t, -1, 7.5)`** — *exponential in tier*.
- Armor `Tierf` (`CalculateArmorTier`) ≈ `(1.2*head + body + leg + arm) * typeMult * 0.1 - 0.4`. Because all four
  armor numbers on one item sum into the tier, a chest that also carries `leg_armor`/`arm_armor` tiers up fast.
  Example: `mkwd_inf3_chest` (body 28 + leg 25 + arm 10) → tier ~5.9 → tens of thousands of denars from one item.
- Crafted weapons (`<CraftedItem>`) compute from their pieces (`CalculateTierCraftedWeapon`) when they have no
  explicit `value=`; several TAOM starter weapons instead carried a hand-set `value="6000"`/`"4000"`.

Net: to keep an item cheap, keep its tier low (low, *single-stat* armor numbers) **and** do not give it an
explicit high `value=`.

## How TAOM starter gear is structured

Two stacked rosters build the player's kit at CC finalize (see `Main/Features/CharacterCreation/`):

1. **Culture-default** — `player_char_creation_{culture}_{background}_{m|f}` in
   `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml` (applied first).
2. **Career override** — `player_career_{culture}_{archetype}_{m|f}` in
   `taom_career_starting_equipment.xml`, merged on top via `Equipment.FillFrom`.

The career layer governs a careered player's kit. It sets weapons (Item0-2), the five armor slots
(Head/Body/Leg/Gloves/Cape) and, for cavalry, Horse/HorseHarness.

### Starter armor

Dedicated items named `starter_{archetype}_{culture}_{slot}_a` live in each culture folder's
`LOTRLOME_items/<folder>/starter_armors.xml` (external `LOTRLOME_Armory` module). They are visual clones of the
culture's own items (mesh/material/cover flags borrowed) with armor re-set to the anchors and **no `value=`**:

| Archetype | primary armor anchor |
|---|---|
| Ranged | ~5 |
| Cavalry | ~7 |
| Infantry | ~9 |

Cape/gloves sit a touch below the anchor (minor pieces; capes carry a high value multiplier).

### Starter weapons

Most starter weapons already compute from tier. Three crafted weapons that carried a hand-set high `value=` had
it removed so they compute too: `wm_gondor_spear_a`, `sm_dwarf_erebor_1h_axe_a`, `dunland_caerdh_spear_a`. These
are *shared* items, so the change lowers their resale everywhere (loot/markets/troop gear), not just for the
player — an acceptable side effect (a starter-tier weapon worth 6,000 was effectively a data bug).

## Tools

| Tool | Purpose |
|---|---|
| `tools/generate_starter_armor.py` | Author low-stat starter armor (5 slots × 3 archetypes) for the 12 non-Gondor career cultures by cloning each culture's items and stripping/re-setting stats. `--apply` writes `<folder>/starter_armors.xml`; default dry-run. Gondor is hand-tuned, excluded. |
| `tools/wire_career_starter_armor.py` | Rewire `taom_career_starting_equipment.xml` so every career roster points its five armor slots at the matching `starter_*` items, preserving weapons + mounts. Idempotent. |

Re-run both after adding a new career culture or changing the anchors (edit `TEMPLATES` in the generator).

## Key files

| File | Role |
|---|---|
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` | Career-override rosters (in repo) |
| `LOTRLOME_Armory/.../LOTRLOME_items/<culture>/starter_armors.xml` | Starter armor item defs (external module) |
| `LOTRLOME_Armory/.../LOTRLOME_items/LOTRAOM_weapons.xml` | Starter weapon defs (external; the 3 `value=` removals) |

## Residuals / follow-ups

- The culture-default layer (`taom_char_creation_equipment.xml`, used on the careerless path) and the three
  fallback cultures with no career rosters (Lothlorien/Umbar/Khand) are unchanged.
- The 180 new `{=starter_*}` item names use inline-default text; not yet harvested into the 12-language loc
  pipeline.
- Starter armor weights are the donor's (only armor stats were lowered) — a "make starters light" pass is open.
- Verify resale in-game: removing `value=` lets the engine compute, which still scales with tier, so a high-tier
  crafted weapon will not drop to zero.

## Related docs

- [career-cc-selection.md](career-cc-selection.md) — the CC career-selection stage + archetype-driven starting
  equipment system whose resale this tunes.
- [career-system.md](career-system.md) — the broader career feature (`CareerStartingEquipmentService` etc.).
- [armor-balance.md](armor-balance.md) — the armor stat-balancing tools (`rebalance_armor.py` /
  `analyze_armor_balance.py`); orthogonal to value, but the same item files.
- [../reference/engine/item-equipment-model.md](../reference/engine/item-equipment-model.md) — `ItemObject` /
  `ItemComponent` engine model (where `Value` / `Tierf` live).
- CLAUDE.md "Equipment & Armory" — the canonical LOTRLOME folder per item-ID prefix.
