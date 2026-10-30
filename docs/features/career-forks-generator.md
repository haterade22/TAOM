# Career forks generator

## Overview

`tools/generate_career_forks.py` writes one kingdom's forked career trees into the two career XML
files from a JSON spec. A fork is a choice group with `max_picks="1"` (a wide fork may allow more):
two or three options, one pick. Each career gets a root choice and six forks (slots 1 to 6, tiers
1, 1, 2, 2, 3, 3 by slot), plus `tree_version` and `point_levels` attributes on its `<Career>`. The
design lives in `tools/career_design/<spec-name>.json`; the generator validates it against
`tools/career_design/_rules.json` and the C# the game runs, and refuses to write on any error.

## Why This Exists

The career redesign ([#769](https://github.com/haterade22/TAOM/issues/769)) replaces the old
six-group, five-choice trees with forks. Hand-writing that XML for every career invites typos and
balance drift, and a hand edit would be indistinguishable from a regeneration.

- **Vanilla behavior:** none; the career system is TAOM's own (`docs/features/career-system.md`).
- **TAOM requirement:** one reviewable source per kingdom, a balance gate that runs before any XML
  exists, a drift gate afterwards, and a guard that a changed tree cannot ship under an old
  `tree_version`.
- **Without this tool:** each tree is authored by hand against caps nobody checks until play.

## Architecture

### Solution Approach

A textual splice, not an XML re-serialise, so every byte outside the generated regions stays
identical (the same approach as `tools/insert_new_faction_careers.py`). Per career:

| File | What is written |
|---|---|
| `taom_career_choices.xml` | the root `<Choice>` and six `<ChoiceGroup max_picks=...>`, between `<!-- career-forks:begin <career> sha256=<hash of the marker's attributes and the body> -->` and `<!-- career-forks:end <career> -->` |
| `taom_careers.xml` | the `<ChoiceGroups>` list between the same kind of markers, plus `tree_version` and `point_levels` on `<Career>`; the begin marker records both values and `shape=v2-<hash>` |

**First adoption** (no markers yet) replaces the career's existing root choice and its existing
groups (ids from `taom_careers.xml`). They must form one contiguous span with only whitespace and
comments between; otherwise the tool refuses and names the career. It also refuses unless the
spec's `tree_version` is above the career's live one (default 1): saves migrate only when the
stored version is lower, so an adoption that does not raise it would strand old picks.

**Later runs** check the region's hash, which covers the marker's recorded attributes together
with the body, and compare the recorded attributes with the live ones. Any difference means someone
edited generated text or its marker by hand (a forged `shape=` included), and the tool refuses for
that career, naming the career and file. To adopt a hand edit, put it in the spec; to discard it, delete both markers and
the tool re-adopts the old tree it finds (which then needs a higher `tree_version`).

**The tree version gate.** The `shape` in the careers marker is a version and a short hash,
`v2-<hash>`, of what decides whether a saved pick is still valid: the root id, the point levels,
per fork the group id, tier and `max_picks`, per option its letter and either its effects, each
as `type@mask#class`, or its mutation properties (sorted, so reordering effects is not
structural). It ignores text and magnitudes. A mask or class is normalised first, so respelling
it is not structural: an omitted `attack_type_mask`, `All`, or a list covering Melee and Ranged
(and no kind) all read `All`, and an omitted `troop_class`, `All` or all three classes all read
`All`; case, order and separators do not matter. A run refuses a `tree_version` lower than the
recorded one, and refuses a changed shape unless `tree_version` rose. The `v2-` prefix names the
shape definition: a marker written under another definition is refused with its own message
(the structure cannot be compared; delete both markers to re-adopt, or run the tool version that
wrote it), so a later change to the definition is not mistaken for an edit of the tree. A text-only or magnitude-only edit keeps the version. Point levels are in the shape because
`point_levels` shrinks or grows the point budget, which only a version bump refunds.

**I/O:** each file keeps its BOM and newline style (majority vote; a file with mixed endings is
refused). The output is parsed with ElementTree before anything is written, and a post-check
confirms the loader will find the six groups, the root and the attributes. Both files are written
to `<name>.gcf-new` first (not `.xml`, because the localization tool globs `career_system/*.xml`)
and then renamed into place, so a failed write leaves both originals untouched. If the second
rename fails after the first file was replaced, the first file is written back from the bytes read
at the start (they round-trip exactly) and the run exits 2; if that restore fails too, the message
names the file to restore. Any unexpected exception exits 2: exit 1 is drift only.

### What the loader reads

The generator writes only what `CareerConfigProvider` uses. It no longer writes `operation="Add"`
or `is_percentage` on a `PassiveEffect` (parsed in an older loader, never read), `icon_sprite` on a
`Choice` (parsed, never drawn: `CareerChoiceObjectVM` uses the first effect's icon, or
`keystone_icon` for a keystone), or the root's empty `group_id`. The career screen therefore shows
an option's icon from its **first effect**, so write the headline effect first.

A `<Mutation>` is a half case: the loader parses its `operation` into `MutationDefinition.Operation`
and defaults it to `Add` (`CareerConfigProvider`, `ParseEnum<OperationType>(mutEl, "operation",
OperationType.Add)`), but no code in `Main` reads `.Operation`. `MutationService.ApplyMutation` takes the
calculator's result as final, and the calculator decides the arithmetic (flat adds the value), so the
attribute is inert either way. The spec field is optional and is written exactly when the spec states
it. **Trade-off:** dropping the attribute everywhere would rewrite the three shipped Gondor regions, and a
regeneration of shipped XML is not wanted for a cosmetic gain, so `gondor.json` keeps stating
`"operation": "Add"` only so the shipped XML stays byte-identical; a new spec may leave it out, and either
spelling reads the same (the shape ignores it, so switching is not a structural change). The stale-tooltip
comparison ignores `operation` and tracks `calculator`, the real lever if `mutation_calculators` ever
grows. A C# follow-up should delete or wire `MutationDefinition.Operation`.

### Ids and keys written

| Item | Pattern |
|---|---|
| group | `<career>_f<slot>` |
| choice | `<career>_f<slot>_<a\|b\|c>` |
| root choice | the career's existing `root_choice_id` |
| description | `{=taom_cf_<choice id>}text` (root: `{=taom_cf_<root id>}text`) |
| group name | `{=taom_career_grp_<career>_f<slot>}name` |

The option `name` appears in the review table only. A description, name or kingdom may not hold
`{` or `}`: the engine reads braces as a text variable or a loc key.

### Read at run time, never copied

| Source | What the generator takes |
|---|---|
| `Domain/PassiveEffectType.cs` | the effect names |
| `Domain/AttackTypeMask.cs` | the `attack_type_mask` names |
| `Domain/PassiveEffectConsumers.cs` | `Consumed`, `ClassFilteredTypes`, `MaskGatedTypes` |
| `Diagnostics/CareerPerkConsumerMap.cs` | `IsFlat` (a count, shown as "+6", not "+600%"); its body must be a chain of `type == PassiveEffectType.X` joined by `\|\|` (an expression body, or one `return` of that chain), anything else is an error rather than a guess |
| `CareerSystemIoC.cs` | `BuildCareerArchetypeMap`, the careerId to archetype map that the ability executor registry and `ICareerArchetypeService` both read; comments are stripped first, so a disabled entry is absent as at run time. The method must be nothing but one returned `new Dictionary<string, CareerArchetype> { ["id"] = CareerArchetype.X, }` (an expression body, or a block holding one `return` of it), anchored like `IsFlat`: a statement after the initialiser, a call chained on it, a builder that delegates elsewhere or a dictionary in another method is an error, because it would make the run time map differ from the one read. No entries, a duplicate id (the index initialiser keeps the last one at run time, silently) or any other content in the initialiser is an error too, and the file or method missing is exit 2 |
| `Languages/*/std_taom_*.xml` | only the string ids (comments stripped), to warn about an added key that already has a row (see "After an apply"); the folder must hold a `std_taom_career_data_strings_*.xml` or the run exits 2 |
| `special_resources/special_resources_config.xml` | the resource a career's `EligibleCultures` resolve to: kingdom rows first, then culture rows, as `SpecialResourceService.ResolveResource` does; a duplicate resource id is an error (the game reads the rows last-wins) |
| `special_resources/troop_resource_costs.xml` | the rows with a `daily_upkeep` above 0 |
| `troops/*.xml`, `characters/*.xml` | the `NPCCharacter` culture of each upkeep row's troop |

**Upkeep troops** are the cost rows with a `daily_upkeep` whose troop's culture is one of the
career's `EligibleCultures`. The row's `resource_id` is not read: it is documentation only, and
`SpecialResourceService` charges every cost-row troop in the party in the player's own resolved
resource. An upkeep row whose troop is found in neither folder (or has no `culture`) is an error.
Kingdoms with no resource in the data (Lindon, Goblin-town, the Misty Mountain Orcs, Blue Craig)
resolve to none, so they cannot use a `SpecialResource*` type.

A set that is missing, empty or names a type the enum lacks is an error (exit 2), never an empty
pass. `_rules.json` holds only the numbers and the rules that are not in C#; a type it names that
the enum lacks is an error too. The spec has no `resource`, `upkeep_troops` or `is_percentage`
field: a spec field could disagree with the data, so the data decides.

## Configuration

### CLI

```
python tools/generate_career_forks.py <spec-name> [--apply] [--check] [--table PATH]
```

| Flag | Effect |
|---|---|
| none | dry run: validate, print a summary, write nothing |
| `--apply` | write the two XML files, then print the localization steps |
| `--check` | drift gate: exit 1 when the XML differs from the spec (a hand-edited region or a refused change counts as drift); no writes |
| `--table PATH` | also write a Markdown review table (written even on a dry run, never for an invalid spec) |

Exit codes: 0 clean, 1 drift, 2 error. The summary prints counts per career, for example
`adopt: root + 6 groups (30 choices) -> root + 6 forks (14 choices)`.

### Spec

```
kingdom, culture_ids[]
careers[]: id, archetype (infantry|ranged|cavalry; must equal CareerSystemIoC.cs), tree_version,
           point_levels[6], root {description, effects[1..2]}, forks[6]
fork:      slot 1-6, name, max_picks (default 1), options[2..3]     (the tier follows the slot)
option:    name, description, effects[0..2]            (a Passive choice)
        or name, description, vu, mutations[1..2]      (a Keystone choice)
effect:    type, magnitude, attack_type_mask?, troop_class? (a class list, or All)
mutation:  property, calculator, value (> 0), operation?  (target is the career's ability template;
           operation defaults to Add in the loader, so it is optional)
```

Unknown keys are errors, so a typo cannot pass silently, and so are the retired fields.

### Validation

Every error is listed; nothing is written if any exist.

| # | Rule |
|---|---|
| 1 | Schema: required fields and types, six forks, slots once each, 2 or 3 options, at most 2 effects, a root with one or two effects, keystone mutations from the rules' property list with a value above 0, an explicit `vu` on a keystone |
| 2 | The career exists in `taom_careers.xml` and the spec's `culture_ids` equal its `EligibleCultures` (any order); ids and lists are type-checked; the career's `archetype` equals the one `CareerSystemIoC.cs` maps it to (case-blind), and a career the map lacks is refused because it has no ability executor |
| 3 | The type is in `PassiveEffectType.cs`, is `Consumed`, and is not banned; `attack_type_mask` names exist, sit only on mask-gated types, and on `TroopDamage` and `TroopResistance` (the rules' `mask_forbidden`) may only be `All`, because `CareerChoicesIntegrationTests` bans a narrower mask there until the Active Effects panel can show masks; `troop_class` is a class list or `All` (every class, the same as omitting it) and only on class-filtered types; in fork slots 1, 5 and 6 a class-filtered effect may name only the career's own archetype class (infantry: Infantry, ranged: Ranged, cavalry: Cavalry), while slots 2 to 4 and the root take any class; no effect is a penalty |
| 4 | A `SpecialResource*` type needs a resource derived from the data; `SpecialResourceUpkeepModifier` also needs an upkeep troop of the career's own cultures (see "Upkeep troops") |
| 5 | `point_levels` has one entry per fork, strictly ascending and positive; `tree_version` is at least 2 |
| 6 | Balance: option value units (VU) within `budget_tolerance` of the tier budget, options of one fork within `fork_spread_tolerance` of each other; the root pip priced the same way and no higher than `root_budget` plus `budget_tolerance` |
| 7 | Caps over every build stay under each cap group |
| 8 | Generated ids and loc keys are unique and collide with nothing else in the two files (comments ignored) |

**Sign.** The runtime multiplies a cost by `(1 + magnitude)` for the `lower_is_better` types
(`TroopWages`, `TroopUpgradeCost`, `RecruitCost`, `FoodConsumption`, `TradePrices`,
`SpecialResourceUpkeepModifier`, `SpecialResourceUpgradeCostModifier`), so the **benefit** of those
is the negative magnitude and of every other type the magnitude. An effect with a benefit below 0
is refused as a penalty. VU and the caps are both priced on the benefit.

**Balance maths.** `vu_unit[type]` is the benefit worth one VU. An option is the sum of
`benefit / unit` over its effects. A troop effect narrowed by `troop_class` to fewer than all three
classes is worth `1 / class_bonus` (1.5) as much per unit, so class-only effects may be 50% larger.
A keystone carries an explicit `vu`.

**Root budget.** The root had no limit before. It is priced with the same `option_vu` as an option and
refused above `root_budget` (1.5 VU) plus `budget_tolerance` (0.5), so 2.0 VU. Only the upper bound is
checked. The reviewer's case, a root of +100% `MovementSpeed` and +300 `Health`, prices at about 63 VU and is
refused. The shipped Gondor roots are one +5% damage pip, 1.0 VU, and the review table prints each root's VU
against the limit.

**Cavalry roots.** Priced with the shipped rules, 63 of the 70 existing roots fit. The other seven are
`MountChargeDamage` 0.10 (3.03 VU, over 2): `eotheod_windrider`, `clanguard_rider`, `chariot_warlord`,
`mahud_beast_rider`, `balchoth_kan`, `shaghana_beast_rider` and `abanissa_ivory_rider`, so a straight port is
refused. Decided on 2026-10-15 under the maintainer's delegation: when a batch regenerates one of them, its
root shrinks to `MountChargeDamage` 0.05 (1.5 VU), because the root stays a small identity perk. The
maintainer can still re-price the unit or raise `root_budget` instead (noted on `MountChargeDamage` in
`_rules.json`).

**Caps.** Forks are independent and picks are voluntary (a fork may stay empty), so the worst build
is the root plus each fork's top `max_picks` options with a positive benefit. The report names that
build. Masks and class filters are not discounted: a melee and a ranged damage pip both count.
`TroopUpgradeCost` and `SpecialResourceUpgradeCostModifier` each have a cap group of 0.5 on the
benefit (the negative of the cut): `TroopUpgradeCost` has no runtime floor, so a total cut past -1
would give a negative upgrade price.

**Every priced type is capped.** `check_rules` refuses a `vu_unit` type that no cap group lists, so a
new unit cannot ship without a limit. Nine types had a unit and no group; they now have one each, all
benefit-signed drafts noted in `cap_notes`: `Ammo` 0.5, `Health` 50, `HeroHealing` 1.0,
`MountChargeDamage` 0.45, `MountHealth` 0.5, `MovementSpeed` 0.15, `PartyMovementSpeed` 0.15,
`PartySpottingRange` 0.5 and `SwingSpeed` 0.15. Of these only `MountChargeDamage` is used in the Gondor spec (0.1 for the knight).

The coverage rule limits **passive** pips only. Keystone mutations sit outside every cap group and add
per pick (`MutationService` applies every chosen keystone, and the flat calculator adds to the running
base), so `Duration` and `Radius` keystones in several forks accumulate; only `CooldownReduction` has a
runtime floor (`min_cooldown_seconds`). Gondor uses one keystone per career. A `mutation_caps` group in
`_rules.json`, summed over the worst build like `analyze_caps`, would close this if a spec ever needs it.

A cap's size in VU is its limit over the type's unit, and that decides which options can pass. `SwingSpeed`
0.15 is only 3 VU at its 0.05 unit, so one tier 1 pick fits and a `SwingSpeed`-only option can never pass
in slots 3 to 6; every other new cap is 5 VU or more (`MovementSpeed` and `PartyMovementSpeed` 0.15 are
5 VU at 0.03). Raising `SwingSpeed` to 0.25 would match them; it is noted in `cap_notes` for the
maintainer.

### Current values

`tools/career_design/_rules.json` holds the bans, tier budgets 3, 4 and 5, the root budget 1.5, tolerances 0.5, the
`lower_is_better` and `mask_forbidden` lists, a `vu_unit` per type with a note, and the cap groups
(the full list is in the file; `Damage` and `ArmorPenetration` share one cap because armour
penetration is a second damage multiplier). **Every number is a draft** the maintainer approves
through the review table. The 2026-10-15 balance decisions changed `TroopMorale` to a factor
(0.05), `Resistance` to 0.04, `RenownGain` to 0.125, `InfluenceGain` to 0.25,
`SpecialResourceUpkeepModifier` to 0.333, added units for the ten E11 types, and banned
`ShrugOff`, `CompanionLimit`, `InventoryCapacity` and `SmithingCostReduction` next to
`StealthBonus`, `Special` and `BuffDuration`. Banned types carry no unit. A flat unit (a count, per
`IsFlat`) is 1 or more and every other unit is below 1; a test pins both.

## After an apply: localization

After `--apply` the generator prints these steps. Step 1 appears when the run added, re-worded or
removed a loc key: the English source `taom_career_data_strings.xml` goes stale otherwise, and rows
reset to the new English would then read as translated and never be sent. Step 2 appears, under the
header "new loc keys", only for added keys that have no language row yet (the count leaves out the keys
the revived-row warning lists). A numbers-only edit prints the tests steps alone. Every command runs
unchanged in bash and PowerShell:

1. `python tools/generate_name_localization_strings.py --apply --category career_data` (the tools
   test for that file fails until it runs).
2. One command per language code, twelve in all: `python tools/translate_with_claude.py --lang <XX>
   --module TAOM --sync-ids --seed-only --apply`. It writes the English rows each language file lacks,
   keeps the file's line terminators, and returns before discovery and translation, so no API call is
   made (`--dry-run` in place of `--apply` reports what it would seed). A paid translation run needs
   the maintainer's word.
3. `python -m unittest discover -s tools/tests -t .`
4. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` with a `--filter` that joins
   `FullyQualifiedName~<name>` by `|` for `CareerChoicesIntegrationTests`,
   `CareerChoiceIntegrityTests`, `CareerConfigProviderTests` and `LanguageFileCoverageTests` (the
   last stays red until step 2).

On an **update**, a key keeps its id while its English may change (keys follow the option's
position), and no step of the pipeline re-translates a row under a changed English. The generator
lists every key whose English changed and points to
[the translator guide's checklist](../localization/TRANSLATOR_GUIDE.md) (substitute the 12 rows,
fix the cache, check the override files).

Two more warnings print after the summary, in every mode (dry run, `--apply`, `--check`), and never
block:

- **An option whose effects changed under an unchanged description.** For each option or root present
  before and after, the generator compares the effects (type, magnitude, mask and class; a
  keystone's property, calculator and value) and the description attribute (key and English). The
  comparison is on meaning: mask and class are normalised as the shape does and the effects are
  compared as a sorted list, so a respelled mask (`Melee,Ranged` to `All`), a class list written as
  `All`, or a reordered effect list does not warn. When the effects differ in meaning and the description
  does not, it names the option with the old and new effects (in written order) and the text, because
  the tooltip may still state the old number. A first adoption cannot warn: the old tree keeps neither
  the choice ids nor the description keys. A re-adoption can, because deleting both markers (the
  recovery both refusals prescribe) leaves a previously generated tree whose ids and descriptions the
  new text keeps.
- **An added key that already has a row.** Seeding fills only missing ids, and the translator skips
  every key another `std_taom_*.xml` file of the same language carries, so a key that comes back (a
  removed option letter returning under a higher `tree_version`) would keep its old translation.
  When a run adds a key that has a row in any `Languages/*/std_taom_*.xml` (comments stripped), it
  lists the key with the languages holding the row, naming the file when it is not the career data
  file, and names the reset step from the translator guide: write the new English into each listed
  row, drop the key from `tools/translation_cache/<lang>.json`, and check
  `tools/translation_overrides/<lang>.json`. The Languages folder is read only when a key was added;
  if it, or a `std_taom_career_data_strings_*.xml` in it, is missing, the run exits 2 with nothing
  written. **Not covered:** a removed key's row is dropped by `generate_translation_template.py --apply`
  while its cache entry survives, so a key that returns later has no row to warn about and the
  translator serves the cached translation of the old English. Reading the caches would need two more
  directory flags and 26 MB of JSON, and no cache holds a career-fork key today; if a paid run changes
  that, check `tools/translation_cache/<lang>.json` for a returning key by hand.

## Key Files

| File | Purpose |
|---|---|
| `tools/generate_career_forks.py` | the generator; its docstring is the CLI reference |
| `tools/career_design/_rules.json` | balance numbers, bans, caps, the sign list |
| `tools/career_design/<spec-name>.json` | one kingdom's spec (`gondor.json` is the first, since C2) |
| `tools/tests/test_generate_career_forks.py` | unittest suite on temporary fixtures and a synthetic CareerSystem folder |
| `tools/tests/test_translate_caps_and_seed.py` | the translator's `--sync-ids --seed-only` seeds without translating |
| `Main/_Module/ModuleData/career_system/taom_careers.xml`, `taom_career_choices.xml` | the generated files |

## Tests

`tools/tests/test_generate_career_forks.py` builds fixtures in temporary directories (CRLF and LF,
with and without a BOM, a synthetic `CareerSystem` folder of tiny `.cs` files and a synthetic
`special_resources` folder) and never touches the shipped XML. It pins the byte-exact splice, the
no-op second run, each refusal, the tree version gate, the atomic write, `--check`, one test per
validation rule, the sign, a brute-force check of the cap search with empty forks allowed, the
review table, the root budget and the cap-group coverage of every priced type, the archetype map read
from `CareerSystemIoC.cs`, the two warnings above, the optional mutation `operation`, `troop_class`
`All`, the shape's mask and class normalisation, the signed marker hash, the rollback of a
failed second rename, the upkeep count by culture, the `IsFlat` grammar, the printed next steps, junk
input that must produce errors and not exceptions, and the plan's worked example
(`captain_of_osgiliath`, priced with the shipped rules, the real C# and the real resource data).
Read-only classes check the shipped rules against the real C# (every consumed type has a unit,
flat units are counts, the rules name no unknown type) and run `--check` for every spec in
`tools/career_design/` against the shipped XML, plus: every marker career id belongs to a spec and
no career is in two specs (these run on `gondor.json` since C2). Run
`python -m unittest discover -s tools/tests -t .`.

## How to write a kingdom

1. Copy the shape above into `tools/career_design/<kingdom>.json`, headline effect first.
2. `python tools/generate_career_forks.py <kingdom> --table out.md`, fix the listed errors, and
   review the table with the maintainer.
3. `--apply`, then the printed steps above. The check of the generated files is the C# test filter
   (`CareerChoicesIntegrationTests`, `CareerChoiceIntegrityTests`, `CareerConfigProviderTests`,
   `LanguageFileCoverageTests`).
4. Later: `--check` is the drift gate (and a tools test runs it for every spec). Change the spec,
   never the generated region; a structural change needs a higher `tree_version`.

`CareerChoicesIntegrationTests` pins the shipped kind-masked pip ids (a `Cut`, `Pierce` or `Blunt`
`attack_type_mask`). Adopting a career that holds one (`black_uruk_captain`, `olog_hai_warchief`,
`avanc_luth_raider`, `watchman_of_stangard`) replaces those pips, so update that test in the same
batch.

**Retired at the merge (2026-10-30).** C2 replaced the three Gondor careers' choice text with `taom_cf_*` keys, which left 111 old Gondor keys that nothing reads: 93 descriptions in `taom_career_strings.xml` and 18 group names in `taom_module_strings.xml`. Trunk's orphan-row gate (`tools/check_orphan_loc_rows.py`) fails on such rows, so the merge into trunk removed them together: the English registrations, their rows in the 24 language files (`std_taom_career_strings_*.xml` and `std_taom_module_strings_*.xml`), the 12 translation caches and the 18 `tools/career_group_names.json` entries.

## Changelog

- 2026-10-14: first version (C1 of the #769 career redesign), tests only, no kingdom spec yet.
- 2026-10-15: review fixes: benefit sign, C# sets and resource data read at run time, tree version
  gate, atomic write, localization steps, `--seed-only`, the balance decisions.
- 2026-10-15: convergence-review fixes: the shape sees `attack_type_mask` and `troop_class` and is
  versioned (`v2-`), the marker hash covers the recorded attributes, upkeep troops are counted by
  NPCCharacter culture, the second rename rolls back, `IsFlat` has a strict grammar, duplicate resource
  ids are refused, upgrade-cost caps (0.5), and next steps that work in both shells.
- 2026-10-15: C2, the Gondor pilot (`tools/career_design/gondor.json`, the first spec), and the archetype class rule for
  fork slots 1, 5 and 6. Text edits after the review keep `tree_version`; the 15 changed keys were substituted by
  hand in the 12 language files, which still held the old English seed.
- 2026-10-15: late review fixes (H1, H3, H4, H5 and a nit): a root budget (1.5 VU), a cap group for all
  nine priced types that had none and a rule that every priced type has one, each spec archetype
  checked against `CareerSystemIoC.cs`, warnings for an added key that already has a language row and
  for effects that changed under an unchanged description, an optional mutation `operation`
  (`gondor.json` keeps it, so the shipped XML is byte-identical), and `troop_class` `All`.
- 2026-10-15: review of those fixes: the archetype reader is anchored to the builder's body (a trailing
  statement, a chained call or a dictionary elsewhere is an error), the stale-tooltip warning compares
  meaning (no warning for a respelling or a reorder) and also fires on a re-adoption, the revived-row
  warning reads every `std_taom_*.xml` and ignores comments, the strings-tool step prints after any
  added, re-worded or removed key, the table shows the root's VU, a `CsSets` without the archetype map
  is a `TypeError`, and the notes now state that keystones are uncapped, that `SwingSpeed` is 3 VU and
  which 7 cavalry roots the root limit refuses.
- 2026-10-30: merged into trunk. The printed seeding command is `--sync-ids --seed-only --apply`, the form trunk's
  translator accepts (the branch's own `--seed-only` mode and its test were dropped for trunk's; its two guards,
  TAOM_Map refused and the install checked before an Armory or all sync writes, were ported to trunk's translator),
  a test now runs the printed line through the translator, and the 111 old Gondor keys were retired (C5's string
  cleanup).

## GitHub Issue

- **Issue:** #769, career redesign
- **Status:** Open
