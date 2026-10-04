# RCA: battle equipment memory audit (plan 038), deep review 2026-10-02

**Summary.** Plan 038's offline tool (`tools/audit_battle_equipment_memory.py`) shipped its byte
totals right on today's data. The review found the defects in three other places. First, metadata
the tool reports: textures resolved by guid to an `AssetPackages` stub instead of their pixel twin.
Second, fallbacks it never reported: 82 loose textures counted at 0 bytes with no reason line, an
empty selection that exited 0, and troops-file errors that never reached `run.log`. Third, engine
rules it modelled from the preload entry point but not from the callees: the crafted holster
template flag, and the whole-id `EquipmentSet` lookup. One HIGH (silent texture size fallbacks,
which the plan had made a STOP), three MEDIUM, and a LOW tail. All were fixed on the branch or
named in the tool's APPROXIMATIONS, with 22 new tests. The deep review report is
`docs/reviews/deep-review-038-battle-equipment-memory-audit-2026-10-02.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | HIGH | A texture sized by the header formula, by its stub or not at all wrote no reason line; 82 loose textures counted 0 bytes; the plan named an undecoded loose header a STOP | Missing fallback reporting | The executor treated the map tool's texture flags (`PIXELS_NOT_IN_PACKS`, `HEADER_UNDECODED`, `NO_PIXEL_DATA`) as reporting; a flag sits in one TSV column and never reaches the run log. Step 9.4's STOP check read one sampled row (`t_gd_ano_chainmail_a1_d`), not the whole output | Reason codes `TEXTURE_SIZE_FROM_HEADER` and `TEXTURE_SIZE_UNKNOWN`; lesson in `lessons/build-tooling-workflow.md` |
| R2 | MEDIUM | Textures reached through a material resolved by guid to the first copy (an `AssetPackages` stub), bypassing the name index's same-guid pixel twin: wrong pack, false flags on 338 rows | Convention inconsistency (two maps, two precedence rules) | `AssetIndex.add_item` swaps `items` to the pixel twin but `by_guid.setdefault` keeps the first copy; the map tool resolves by name, so the gap never showed there. Bytes matched by coincidence (formula equals segment), which hid it from every byte check | Resolve through the name entry when it carries the same guid; lesson in `lessons/build-tooling-workflow.md` |
| R3 | MEDIUM | Crafted weapons on a `use_weapon_as_holster_mesh` template counted the Blade's holster mesh (13 items, 11 MB each in their own rows) | Missing vanilla gate | The model was read from `PreloadHelper.AddItemObject` and the `GetHolsterMeshIfExists` names, not from `CraftedDataView`, where the template flag returns null | `WEAPON_AS_HOLSTER_TEMPLATES`; lesson in `lessons/adapters-taleworlds-api.md` (a recurrence of its approximation rule) |
| R4 | MEDIUM | An empty selection exited 0: case-sensitive module match, silent missing `SubModule.xml`, only sides checked for emptiness | Missing null guard (empty input) | The success path was tested; "nothing selected" was not a case anyone wrote down | `abort reason=NO_TROOPS_SELECTED`, `SUBMODULE_MISSING`; covered by the R1 lesson (every way the tool degrades says so) |
| R5 | MEDIUM | Skin eyebrow meshes, face and mouth textures and tattoo materials uncounted and unnamed | Missing vanilla gate (unnamed approximation) | `_read_skins` copied the eight body attributes from `validate_mesh_refs.py`, which checks references, not cost | Named in APPROXIMATIONS; modelling is Mike's call; R3's lesson |
| R7 | LOW | Troops-file BOM or missing file crashed with exit 1; exit-2 causes never reached `run.log` | Missing null guard | The exit-2 paths used `print`, like the folder checks that run before the log opens, though the log was already open there; the file read was never wrapped | `abort` lines, `utf-8-sig`, troop files read before indexing; R1's lesson |
| R8, R14 | LOW | The report claimed the real cost "lies between" the bounds, and attributed race meshes and horse materials to the preload | Other: prose claim beyond the evidence | Written from the plan's framing, not from `PreloadHelper`'s code | Text fixed; R3's lesson |
| R9 | LOW | Unnamed engine gaps (hero sets, slot fit, last `ItemComponent`, `Type` override, `.xsl`, banner materials, `BuildOrders`); `EquipmentSet` ids split at a dot where the engine looks them up whole | Missing vanilla gate | `_object_id` (right for `Equipment.DeserializeNode`) was reused for a different engine call site without reading it | Whole-id lookup; the rest named; R3's lesson |
| R10 | LOW | The map tool's `--loose-assets` runs flagged every loose mesh as `EDITOR_STREAM_BLOAT` and said "both pack trees" | Stale claim after a change | The option was proven only to leave the default output unchanged | Loose-only guards; test |
| R13, R17 | LOW | Run-log order differed from the documented contract; the report had no battle row | Convention inconsistency | Tests pinned each line's format, not their order, and the report's headings, not its rows | Order test, battle-row test |
| R15 | LOW | Mesh records whose material guid is in no pack were dropped silently | Missing fallback reporting | The guid window scan finds only indexed materials, so a missing one leaves no trace | `MATERIAL_GUID_UNRESOLVED` for records whose counts decode (a plain check gave 816 misread rows from cloth records); R1's lesson |
| R18 | LOW | Dead constants and a dead field | Dead / no-op code | Written for a design that changed during the build | Deleted |

## Root-cause patterns

**Silent degradation (R1, R4, R7, R15).** The tool had a reason-code table and a fallback line per
code, and a test that every emitted code has a consequence. That test proves the table is
complete for the codes the source emits; it cannot see a branch that degrades without emitting
one. Each silent branch was a fallback inherited from the map tool (a flag, a guid scan) or an
input edge (empty, unreadable). D6 asks for an explicit reason line whenever anything disables
itself; an offline tool meets it only when every degraded branch emits a row.

**The model stops at the entry point (R3, R5, R9, R14).** The ENGINE RULES section cites the right
methods, and each rule it states matches the decompile. The gaps are in the methods those call
(`CraftedDataView`, `BasicCharacterObject`'s roster lookup) and in assets loaded outside the
preload entirely. This recurs: `lessons/adapters-taleworlds-api.md` already holds "An approximation
of an engine gate must say so, or it becomes a false authority" (Enlistment, 2026-08-08).

## Why each agent missed these

- **Executor (implementation):** wrote and verified against the plan's oracle rows, which were
  right; the STOP check in Step 9.4 sampled one texture. The R2 metadata error produced correct
  bytes, so every byte oracle passed.
- **Standards lens:** found R2, R13, R14, R15, R17, R18 and most of R9; it did not rate R1 HIGH
  (it listed the texture fallbacks as a D6 gap without the 82 zero-byte rows) and missed R4
  (empty selection), which sits in control flow rather than in a rule it checks.
- **Completeness lens:** found R2, R8, R9, R14, R17, R19 and the unlogged exit-2 half of R7; it
  checked the `--loose-assets` run only through one texture row, so it missed R1's zero-byte rows,
  and it did not probe empty or unreadable inputs (R4, the crashes in R7).
- **Tooling lens:** found R1, R3 to R12 and R17; it missed R2, because its byte checks matched (the
  formula equals the pixel segment), R13, because it checked each line's format against the plan,
  not their order, and the R14 prose.
- **Not launched:** Efficiency, Data flow and Design. A Data flow lens would most likely have
  traced R2 (two maps over one item set).

## Feedback memories to codify

None beyond the lessons entries: both patterns are tool-authoring rules that belong in the lessons
files, which every tooling review reads.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `65f640fa..666d5249` | C1: R15's filter kept three misread cloth-record guids (their counts decode as a plausible 4,161,536), so the report still listed them as unresolved materials | `9546c167` | The filter was tested on a fixture with counts 0, not on the live record shape | Build the fixture from a real record the live run printed |
| 1 | same | C2: the EXIT CODES section missed that argparse also exits 2 before any run.log exists | `9546c167` | The section was written from the tool's own exits, not argparse's | List every exit path, the argument parser's included |
| 1 | same | C3: R10 exempted loose Assets meshes from EDITOR_STREAM_BLOAT, but the report and README still defined the flag by the bare ratio | `9546c167` | The behaviour change did not sweep the flag's definitions | A rule change greps every place that defines the rule |
| 2 | `666d5249..9546c167` | none (clean) | | | |

This section and the REVIEW-LOG entry's convergence line were written by the orchestrator after round 2.

## Codex pass (2026-10-03)

Codex reviewed `ba3f2a57..072d46dc` after the convergence rounds and raised 3 P2 and 1 P3. All four held up against the
code, the v1.5.3 decompile and the real data; none was a regression of an earlier fix. The deep review report's
"Review pass of 2026-10-03" holds the evidence.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| X1 | P2 | The audit counted equipment the engine refuses: a BodyArmor in a Cape slot on two Uruk-hai troops | Missing vanilla gate | The first review named slot fit (R9) and left it to the maintainer without running the rule over the data, so nobody knew it touched two troops; the model filled a dict and never judged each assignment | Run a deferred engine rule over the real data and write the count beside the deferral; model a gate per assignment, in engine order (`lessons/adapters-taleworlds-api.md`) |
| X2 | P2 | Plan 038's Step 9.4 STOP (an undecoded loose header) was reached and then read as met once the fallback was reported | Other: a STOP condition closed by visibility | R1 added the reason rows and left the decode as a follow-up, while the report and every total still read as a finished attribution | A STOP that is reached gets an explicit disposition in the output itself: the report now opens with the lower-bound statement whenever a texture has no size |
| X3 | P2 | A reused output folder kept another run's `sides.tsv`, and an aborted run left every earlier table beside its abort log | Missing null guard (a rerun) | Every test built its fixture in a fresh folder, so only first-run transitions were exercised | For a tool that writes into a configurable folder, test the second run: fewer outputs, and an abort (`lessons/build-tooling-workflow.md`) |
| X4 | P3 | `--loose-assets` kept packed copies and cooked-only names, against the engine's rule | Convention inconsistency (a plan choice against the repository's engine record) | Plan 038 Step 2 fixed packed-first, the executor built it as written, and the first review filed the conflict as a LOW decision for the maintainer | Before building a plan's precedence rule for an engine model, grep `docs/reference` for the engine's rule and model its unit of choice, a module's tree, not a lookup order (`lessons/build-tooling-workflow.md`) |

The slot-fit change also showed what the first build's tests did not: five fixture assignments (a Goods item in `Body`
and in `Leg`, a BodyArmor in `Leg`, a HeadArmor in `Body`, the battle fixture's second troop) were slots the engine
refuses, valid test data only because nothing checked them.

## Convergence of the Codex pass (2026-10-04)

A convergence reviewer read the fix, `072d46dc..27d3e486`, and raised one MEDIUM and one LOW, both about the item type
rule it added. Both held up against the code, the v1.5.3 decompile and the real data. The deep review report's
"Convergence of the Codex pass" holds the evidence.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| Y1 | MEDIUM | 27 of the weapon-class table's 30 rows, and the case-insensitive flag read, had no test: 82 of 90 per-row mutants survived | Other: weak test oracle | The 36 mutants recorded for the fix pass name the fit tables, each flag and type rule and the assembly points, but no per-row mutant of the weapon-class table, and the tests drove three of its rows, so a green mutation run said nothing about the other 27 | Generate the mutants from the table, one per row and per way a row can break, and write one test row per table row against a hand-written copy of the engine's switch (`lessons/testing-qa.md`) |
| Y2 | LOW | The type rule did not treat a `<Banner>` component as a weapon component, though `BannerComponent` derives from `WeaponComponent` | Missing vanilla gate | The model keyed on the XML element name (`Weapon`) where the engine keys on a type test (`WeaponComponent != null`), and the real data hid it: all 46 banners carry the Type their class gives, so nothing disagreed | When a model mirrors an engine type test, list the type's subclasses and the XML elements that build each, and give each a fixture; a census that shows agreement proves a gap latent, not absent (`lessons/adapters-taleworlds-api.md`) |

Neither finding was a regression of an earlier fix, and neither moves an output on the real data: the table was right,
and no troop of the audited populations reaches a banner item.
