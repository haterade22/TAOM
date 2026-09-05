# RCA: armoury dead-mesh wave 2 (2026-09-01)

Nine review agents across two rounds, over a changeset that deleted 83 Armory item definitions,
re-pointed 7, and gated 3. Final tally: **one confirmed data defect** (deferred by decision), one
tooling gap (fixed), three wrong counts (fixed), and **two findings that were themselves wrong** -
a critical save-corruption claim from two agents, and a data fix I made and then had to revert.
The round-2 pass also removed 13 further items, on a correct reading of the same engine code that
refuted my own change. No finding blocked the work; the instructive part is how much of the first
round's output did not survive verification.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | ~~MED~~ **REFUTED** | Reported as: `ar_ardunian_elite_armour` kept `has_gender_variations="false"` after its mesh was re-pointed to one that ships a `_slim` variant, so a female renders a male-shaped chest. I changed it to `"true"`, then a round-2 adversarial pass showed the reasoning was wrong and the change was a small regression. **`_slim` is not the female variant.** It is the slim-BUILD variant, and the engine appends it on the NON-female branch. See "The gender-variation finding was wrong" below. | False positive I acted on | I accepted a plausible mechanism (flag mentions gender, mesh has a variant, therefore related) without reading the code that consumes the flag. Two review agents and I all assumed it. | **Reverted** in both trees. The real mechanism is now decompiled and recorded. |
| 2 | MED | All 6 re-pointed crafting pieces kept their old `length` and `BuildData` while carrying a new mesh (spear handle 138 vs canonical 203; sword blade 111 vs 80 with `BuildData` dropped entirely). | Data / mesh-dependent attribute | Same root cause as #1, second instance, same session. I checked the mesh existed and stopped there. | Deferred by explicit decision (below), recorded as a Known limitation in CHANGELOG. |
| 3 | MED | `audit_deleted_mesh_impact.py` reported all six `easterling_*` crafting pieces as ORPHAN. They are referenced by `<UsablePiece piece_id=>` and `<Piece id=>`, neither of which its `="Item\.(...)"` matcher can see. `easterling_spear` is player career starting equipment. | Tooling blind spot | The matcher was documented as "attribute-agnostic by design", which reads as complete. It is agnostic about the *attribute* but not about the *namespace*: a crafting piece is not an `Item.`. | Both shapes added as a `crafting_piece` ref kind, swept in the Armory root too, 5 tests. |
| 4 | HIGH | "17 moriaorc meshes" was wrong; it is 15. Propagated into a source comment, the CHANGELOG, and doc prose, where it contradicted that same doc's own table. Also "38" easterling (39) and "88 items" (87 items + 6 crafting pieces). | Fabricated count | I took the number from an exploration subagent's report and never re-derived it. `evidence-over-claims.md` §A.4 names exactly this: a confident subagent report is a claim, not evidence. | All corrected. Rule below. |
| 5 | n/a | Two independent agents reported that item references in saves use a positional `MBGUID`, so deleting 83 items would silently make old saves resolve to the wrong items. REFUTED. | False positive | Both read `ISerializableObject.DeserializeFrom`, which is real code but not the campaign save path. | Recorded below so the next reader does not re-derive it. |

## The gender-variation finding was wrong, and the way it was wrong is the lesson

The engine's mesh resolution, `BasicCharacterTableau.cs:531-537` on the installed v1.4.8:

```csharp
bool flag3 = flag && _equipmentHasGenderVariations[i];              // isFemale && the flag
MetaMesh val4 = MetaMesh.GetCopy(flag3 ? (text + "_female") : (text + "_male"), false, true);
if (val4 == null) {
    string text2 = text;
    text2 = ((!flag3) ? (text2 + (flag2 ? "_slim" : ""))            // flag2 = slim BUILD
                      : (text2 + (flag2 ? "_converted_slim" : "_converted")));
    val4 = MetaMesh.GetCopy(text2, false, true) ?? MetaMesh.GetCopy(text, false, true);
}
```

`_slim` sits on the `!flag3` branch. It is the slim-**build** variant, appended for any character
whose body slider is slim, and it has nothing to do with gender. The female suffixes are `_female`,
`_converted` and `_converted_slim`.

Measured against the live asset set for the mesh in question:

| suffix | present |
|---|---|
| `sm_md_num_inf_chest_elite_a` | yes |
| `..._slim` | yes |
| `..._female`, `..._male`, `..._converted`, `..._converted_slim` | no |

So with the flag `"true"` a female falls through every branch to the bare mesh. With `"false"` she
takes the non-female branch and gets `_slim` when slim-built. **The "fix" removed an option and
added nothing**, which is why it was reverted. Across the whole module only 5 LOTR-authored
`_converted` meshes exist and one `_converted_slim`, so `has_gender_variations` is close to inert
here: the real gap is missing female art, not a wrong attribute.

**The lesson is not "check the donor's attributes".** It is that I inferred a mechanism from a name.
`has_gender_variations` mentions gender, the mesh had a variant, the neighbouring item set the flag,
and three plausible-looking facts assembled into a conclusion nobody had read the code for. The
`Verify Before Reference` rule already covers this and I applied it to mesh NAMES while skipping it
for mesh SEMANTICS.

**What survives from the original pattern.** Finding #2 is still real and still the same shape:
re-pointing `mesh=` while leaving `length` and `<BuildData>` describing the old geometry. The
durable form is narrower than I first wrote it: **before relying on any attribute to carry a
re-point, read the engine code that consumes it.** A neighbouring item agreeing with you is not
evidence; it may be repeating the same guess.

## Why each agent missed what it missed

- **Tooling correctness** verified the write path was byte-faithful, idempotent and parse-checked,
  which it was. It was scoped to whether the script wrote what it intended, not to whether the
  intent was right. Correct scope; #1 and #2 were out of it by construction.
- **XML data integrity** found #1 by comparing my item against the canonical item using the same
  mesh. This is the only agent that did a donor comparison, and it is the one that found the defect.
  It did not find #2 because crafting pieces have no `<Armor>` block, so its cover-attribute check
  had nothing to fire on.
- **Cross-system data flow** found #2 by tracing the piece geometry to the assembled weapon, and
  independently confirmed #3. It was the highest-yield agent, as the skill predicts.
- **Test quality / completeness** found #4 by recomputing every number in the CHANGELOG instead of
  reading it. No other agent attempted arithmetic.
- **Engine load path** and **adversarial deletion safety** both produced #5. Two agents reaching
  the same wrong conclusion independently is not corroboration when they read the same file.
- **The gap none of them had:** no agent was asked "does this re-point carry every attribute the
  mesh implies?" The XML-integrity agent got there via a donor comparison it invented. That is luck,
  and the rule below removes the luck.

## Preventive actions

1. **Read the consuming code before trusting an attribute to carry a re-point.** The original
   wording here said to reconcile against a neighbouring item that ships the donor mesh. That is
   what produced the wrong gender fix: the neighbour agreed with me and we were both guessing.
   Attribute semantics come from the engine, not from a sibling row. Confirmed shapes worth
   carrying: `_slim` is the slim-BUILD suffix on the non-female branch; `_female` /
   `_converted` / `_converted_slim` are the female ones, gated on `has_gender_variations`;
   `<CraftingPiece>` `length` feeds `CalculatePivotDistances` and becomes the weapon's REACH.
2. **Lessons entry** in `docs/reviews/lessons/data-content-cultures.md`, since the class is data
   authoring rather than tooling.
3. **Mechanised half of #4:** counts that appear in more than one artifact should be derived once
   and reused. The three-way contradiction (comment / CHANGELOG / doc table) existed because the
   number was typed three times from memory.
4. **#3 is fixed in code**, which is the strongest form: `extract_piece_refs_from_text` plus tests
   that state why the shapes exist.

## The refuted finding, recorded so nobody re-derives it

Two agents concluded that deleting items scrambles existing saves, because `ItemRosterElement` and
`EquipmentElement` implement `ISerializableObject.DeserializeFrom`, which reads a raw
`MBGUID` whose `SubId` is a sequential counter assigned in XML document order.

The code they read is real. It is not the campaign save path:

- `TaleWorlds.SaveSystem` references `ISerializableObject` **zero** times, against 52 `Saveable*`
  tokens of its own. (An earlier version of this line said 271. That was the result of a grep whose
  pattern also counted `AutoGenerated*` collectors, so the number did not measure what the sentence
  claimed. Corrected on review, in the section about verifying numbers, which is the joke telling
  itself.)
- `ISerializableObject` is declared in `TaleWorlds.Library` over `IWriter`/`IReader`, implemented by
  `BinaryWriter` / `BinaryReader` / `StringWriter` / `StringReader`. It is a general-purpose
  serializer used by things like `PartyScreenLogic`.
- The save path is `ItemRoster.[SaveableField(0)] _data` to
  `ItemRosterElement.[SaveableProperty(21)] EquipmentElement` to
  `EquipmentElement.[SaveableProperty(1)] ItemObject Item`, with
  `AutoGeneratedInstanceCollectObjects` adding `Item` to the save's object graph, and `ItemObject`
  registered by `SaveableCoreTypeDefiner` as `AddClassDefinition(typeof(ItemObject), 32)`.

So item references in a save resolve through the save's own object graph, not through a runtime
positional id, and deleting items does not shift other items' saved references.

**The generalisable lesson: two agents agreeing is not corroboration when they read the same
source.** Independence has to be in the evidence, not just in the process. The tell here was that
both quoted the same method. `evidence-over-claims.md` §A already says to re-run the decompiler when
agents disagree; this is the mirror case, where they agreed and were both wrong, and it needs the
same treatment.

## Deferred by explicit decision

Finding #2 is deferred, and the original framing of it here was wrong in the user's favour.
`length` is not a cosmetic positioning input sitting beside a stat: `WeaponDesign.CalculatePivotDistances`
turns it into `CraftedWeaponLength`, which `CraftingStats.FillWeapon` rounds into the weapon's live
combat REACH. So the trade is not reach-versus-appearance; the visible mesh extent and the hitbox
are the same quantity, and leaving `length=138` on a handle whose mesh reads closer to 203 means a
spear that LOOKS longer than it hits. Attacks that appear to land can whiff. The decision to keep
the stats stands, since 203 would add roughly 47% reach to a career starting weapon, but it is a
gameplay trade, not a cosmetic one. No crash risk: the pivot maths is float arithmetic over a
fixed-size array keyed on the piece-type enum, so a stale length cannot throw or index out of range. Recorded as a
`Known limitation:` in the CHANGELOG entry, per the no-silent-deferrals rule.

## Still owed

- In-game verification. Item XML loads only at process launch, so this needs a full restart.
- One load test of a pre-2026-09-01 save. 82 of the 83 deleted items were `is_merchandise="true"`,
  so a save can hold one. The engine reading says an unresolvable id resolves to null and empties
  the slot; that should be seen once rather than trusted.
- No GitHub issue exists for this wave. TAOM policy wants one before implementation; this is
  retroactive repair.
- `docs/reference/bannerlord-animation-clip-flags.md:167` and
  `docs/reference/lotrlome-warg-changes.md` cite `LOTRLOME_Armory/AssetPackages/warg.tpac`, which
  does not exist. Predates this change, surfaced by it.
- `LOTRAOM_shields.xml` differs between the live install and the versioned copy by 14 Gondor shield
  ids. Proven to predate this change; needs its own reconciliation.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/recipe-retire-content.md](../modding/recipe-retire-content.md)

<!-- backlinks-end -->
