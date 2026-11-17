# RCA: dwarf civilian kit (deep-review, 2026-11-17)

## Top-line

Promoted dwarf companions wore the vanilla Highland Tartan in their civilian slots (#1431). Dwarf
troop XML ended with `<EquipmentSet id="battania_troop_civilian_template_t*" equipmentType="Civilian" />`
(Erebor 17, Ered Luin 27, Fords of War 3). The engine loader turns that reference into the troop's
civilian sets (`BasicCharacterObject.Deserialize`, v1.5.7 lines 387-412), so
`HeroCommissionAdapter.CreateCompanionFromTroop` never reached its battle-kit fallback. The change
replaced the 47 references with inline dwarf tunic rosters, made `realm_troops.py` write them for
Ered Luin through a new `Realm.civilian_kits` field, and added the gate
`TAOM.Tests/Core/DwarfCivilianKitTests.cs`.

`/deep-review` ran at tier 2 (a gate test, a generator and troop data; no patch, model or live
module): three lenses in one wave at high effort (Data flow, XML, Tooling). Codex was skipped (no
usage since 2026-11-15) and is owed before merge. No CRITICAL or HIGH finding, so no second wave.
The orchestrator re-read the loader, the Civilian flag, the 131 count and the F1 and F2 probes. The
fourteen reader traces under F5 and F6 are the Data flow lens's, reasoned from code and not run.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED (Tooling) | The rule "a realm with a race sets `civilian_kits`" lived only in the lesson and a field comment. A future orc or elf realm with no kits would ship the tartan again, and `civilian_kits=()` fell back to the template without a word. | Rule written as prose, not as a check | The builder wrote the lesson before asking what in the code enforces it. `self_check` already carried a sibling rule (`MOUNTED_DWARF`) that was the pattern to copy. | Fixed: `self_check` fails a troop whose realm has a race and no kits (`NONHUMAN_CIVILIAN_TEMPLATE`). Two tests went RED first (kits `None` and `()`), and one proves the real Ered Luin realm passes. |
| F2 | LOW (Tooling) | The first realm test would pass with no troops and checked only Ered Luin and Cardolan. | Vacuous test | Written for the one realm that changed. | Fixed: it loops every generator in `GENERATORS`, branches on `REALM.race`, and asserts a non-empty troop list and that a racial realm was seen. |
| F3 | LOW (Data flow, Tooling) | The `civilian_kits` comment said "full item id", but the emitter adds `Item.`. | Misleading comment | Comment written before the emitter. | Fixed. |
| F4 | LOW (Data flow) | The C# gate reads roster references only. It would pass an inline roster holding `battania_light_armor_b`, a lowercase `equipmentSet` tag, a roster id defined twice, or a culture-less roster. No current data triggers any of these. | Fail-open edges | The gate states one claim (no borrowed roster) and the edges are other claims. | Not applied: a guard for a case no data reaches (simplicity criterion). Open for Mike if he wants the content check. |
| F5 | LOW-MED (Data flow, XML) | No dwarf tunic or light boot carries `ItemFlags.Civilian`, so civilian mode refuses to re-equip them (`SPInventoryVM.cs:4042`). The 29 earlier Erebor rosters have the same items, so this existed before. | Pre-existing data gap | Outside the diff; the Armory is unversioned. | Follow-up #1432. |
| F6 | LOW (Data flow, XML) | The Ered Luin noble line wears the plain tunic where Erebor nobles wear the noble tunic, and the new rosters drop the old template's civilian sword and gloves. | Design | One kit per realm is the simple shape, and the 29 Erebor rosters are also Body and Leg only. | Not applied. Open for Mike. |
| F7 | MED (Tooling, Data flow) | 131 references from elf, goblin, orc and uruk troops still point at vanilla human civilian templates, and human realms carry 519 more. | Same bug class, other races | The 2026-10-16 gate and this one are scoped to dwarves. | Follow-up #1433. |

## Root-cause pattern

The 2026-10-16 tunic change made the dwarf civilian look right for dresses and left the template
reference on every troop that had one. Both changes fixed the instance (a dwarf, a dress) and not
the class (a troop borrowing another culture's civilian sets). F1 and F7 have the same shape:
a rule or gate scoped to what the builder just saw. The rule that applies already exists
(CLAUDE.md "a machine can check it: write the gate"); the lesson in
`lessons/data-content-cultures.md` now names the gate and the `self_check` rule that enforce it.

## Why each lens caught what it caught

- **Tooling** found F1 and F2 by running a probe with `civilian_kits=()` and by reading what the new test asserts when the troop list is empty.
- **Data flow** traced fourteen readers of a dwarf troop's civilian sets and found none that changes a band or a page, and found F3, F4, F5 and F6.
- **XML** ran every data gate, checked ids, bytes and the `OrderEquipments` sort, and found F5 and F6.
- **Standards, Engine compatibility, Efficiency, Completeness and Design** were skipped by tier. The engine facts they would check were read from the decompile by the orchestrator and by the XML lens.

## Feedback memories to codify

None. The pattern is already a rule; the fix is to follow it.
