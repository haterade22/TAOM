# Adversarial review: Black Numenorean line (Mordor), TAOM

You are an independent adversarial reviewer. Assume this changeset contains at least one shipping
defect and find it. Do not restate what the code does. Do not praise. Report defects with file, line,
the concrete failure mode, and the minimal fix. If you cannot verify a claim, say UNVERIFIED rather
than guessing.

## What this is

TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord v1.4.8. This changeset
adds a "Black Numenorean" troop line to the Mordor culture: corrupted Men who serve Sauron. It is
almost entirely DATA, not C#. Assets were delivered by a 3D artist and were previously unreferenced.

Scope shipped:
- 78 armour items, ids `sk_md_num_*` / `sm_md_num_*`
- 22 crafting pieces, 7 `<CraftedItem>` weapons (3 one-handed swords, 3 two-handed, 1 lance), 6 shields
- 13 troops `mordor_num_*` at levels 26/31/36/41/46, three branches off a shared Initiate
- Wiring into party templates, troop weights, special-resource costs
- One C# change: a test exemption

## Where the files are

**TAOM repo** (`E:\repos\TAOM`, this project):
- `Main/_Module/ModuleData/troops/troops_mordor.xml`: the 13 new `<NPCCharacter>` blocks
- `Main/_Module/ModuleData/taom_partyTemplates.xml`
- `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`
- `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`
- `tools/generate_black_numenorean_armor.py`, `tools/generate_black_numenorean_weapons.py`,
  `tools/apply_black_numenorean_troops.py`, `tools/wire_black_numenorean_troops.py`
- `docs/features/black-numenorean.md`

**Outside this repo:** the Armory module, which ships to players and is where all items live. It
exists in TWO copies that were both written to:
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\`
- `E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory\ModuleData\`

Files touched in each: `LOTRLOME_crafting_pieces.xml`, `weapon_descriptions.xslt`,
`crafting_templates.xslt`, `LOTRLOME_items/LOTRAOM_weapons.xml`,
`LOTRLOME_items/LOTRAOM_shields.xml`, `LOTRLOME_items/mordor/{head,body,shoulder,arm,leg}_armors.xml`.

`git diff` will NOT show the Armory changes. Read those files directly.

## Known suspects, attack these first

**S1. The race decision.** The 13 troops carry NO `race=` attribute. The claim is that
`TaleWorlds.Core.BasicCharacterObject.Deserialize` sets `Race = 0` and only overwrites it when the
attribute is present, that `FaceGen.GetRaceOrDefault` returns 0 for unknown, and that Native loading
first makes index 0 equal `human`. TAOM has 15 races merged from `Native/ModuleData/skins.xml` plus
`LOTRLOME_Armory/ModuleData/skins.xml`. Is index 0 actually `human` at runtime, or is that an
assumption about module load order that a different mod list could break? What breaks if it is wrong?

**S2. Crafting-piece registration is a silent-fail class.** Every `<Piece id="X">` in a
`<CraftedItem>` must ALSO appear as `<AvailablePiece id="X"/>` in `weapon_descriptions.xslt` and
`<UsablePiece piece_id="X"/>` in `crafting_templates.xslt`. Missing from either and the weapon fails
to load with no log line. Verify all 22 pieces in BOTH Armory copies. Also verify the insertions
landed inside the correct `<xsl:template>` and BEFORE that template's trailing
`<xsl:apply-templates select="@*|node()"/>`. The passthrough must remain last.

**S3. Collision bodies hang the game, they do not crash it.** Every `body_name=` /
`shield_body_name=` must name a real packaged mesh, or `PreloadHelper.WaitForMeshesToBeLoaded` spins
the main thread forever: no crash, no log, one core pinned, the mission never loads. The real mesh
names live inside `.tpac` binaries under `LOTRLOME_Armory/Assets/mordor_props/black_num_weapons/`
and `.../Assets/Mordor/black_num_armors/`. Extract them yourself (length-prefixed ASCII: for each
`[A-Za-z0-9_]{5,}` run, the preceding 4 bytes little-endian equal the run length) and check every
ref. Note: `sm_md_num_inf_shield_med_b` and `_heavy_b` deliberately point at the `_a` collision
bodies because only `_a` shields ship hulls. Is that actually safe, or does the engine need a 1:1
body per shield?

**S4. The lance.** `sm_md_num_lance_blade_a` declares only `<Thrust>` and carries
`excluded_item_usage_features="swing"`. Its `<BuildData piece_offset="0">` with a 250-length shaft
was chosen by copying the working Dale long shaft, but the artist's spec suggested roughly 65 or -65
and was unsure of the sign. Separately: the lance is registered under `TwoHandedPolearm`,
`TwoHandedPolearm_Couchable` and `TwoHandedPolearm_Bracing` in `weapon_descriptions.xslt` but only
`TwoHandedPolearm` in `crafting_templates.xslt`. Is that asymmetry correct or a bug? Does couching
actually work with this registration?

**S5. Party-template arithmetic.** 13 stacks were added to 16 Mordor lord templates, then
`tools/rebalance_party_template_maxes.py --apply` rescaled every stack's spread so each template's
max sum returns to Mordor's 3500 target. Verify every template sums to exactly 3500, that no stack
ended with `max_value < min_value` (the tool's docstring says that makes `(max-min)*r` negative and
fills below the floor), and that no non-Mordor template was altered. Then judge: does the resulting
Black Numenorean share of a spawned lord party make sense for a rare noble line?

**S6. Balance.** The user's explicit instruction was "ensure you balance the armor and weapon stats".
The T9 troops are level 46, making them Mordor's strongest troops. Armour stats come from
`rebalance_armor.calculate_stats` with Mordor's `protection: -1` / `weight_mult: 1.10`. Read
`docs/features/armor-balance.md` for the curve, the two-tier invariant, and the #342 rule that
"Gondor leads shared kit by 1, Mordor-exclusive kit by 2". Check the invariant holds, check the
parity claim against real Gondor numbers, and compare the whole line against `dg_khamul_shadow_*`
(level 46), `gondor_da_swan_knight` (46) and `erebor_noble_royal_warden` (46). Is Mordor now
strictly the best faction?

**S7. The test exemption.** `VolunteerRecruitmentServiceTests.IsIntentionallyUnrecruited` gained
`|| troopId.StartsWith("mordor_num_")`. The design intent is a standalone AI-only line: Mordor's
`elite_basic_troop` stays `mordor_uruk_warrior` and no volunteer pool changed. Is exempting the test
the right call, or is it a test being weakened to hide a real gap? Is the prefix over-broad? And is
there genuinely any in-game path for a player to obtain these troops?

**S8. The four Python generators write XML outside the repo.** Read `tools/README.md` "XML I/O
convention". It permits exactly two idioms and forbids the mixed shape (plain `utf-8` text read plus
text-mode write) because it silently strips a BOM and normalises CRLF to LF. These scripts use
`open(..., encoding="utf-8", newline="")` for both read and write. Check the actual bytes of every
target file: does any carry a BOM, and would `encoding="utf-8"` (not `utf-8-sig`) turn it into a
literal U+FEFF that gets re-encoded? Note the targets have MIXED endings:
`LOTRLOME_crafting_pieces.xml` is LF-only, the XSLTs and shields are CRLF. Also check idempotency
(substring-containment id detection could false-positive on a longer id), dry-run gating, and that
backups use a non-`.xml` extension (the item folder is globbed `*.xml`, so an `.xml` backup would
inject duplicate item ids).

## Also worth your attention

- Two `clo_`-prefixed cloth-proxy meshes (`clo_sm_md_num_cav_pauld_cape_a`,
  `clo_sm_md_num_inf_pauld_cape_a`) were deliberately NOT authored because no `cloth_bodies.xml`
  entry exists for any Numenorean piece. Is skipping them correct, or does something else now break?
- `sm_md_num_chest_light_a` is the only chest with `has_gender_variations="false"` because it has no
  `_slim` sibling. Verify the other 18 chests all genuinely have `_slim` meshes.
- Shields must satisfy `item_usage="shield"` paired with `ForceAttachOffHandSecondaryItemBone="true"`
  and never also `ForceAttachOffHandPrimaryItemBone`. `docs/reference/armory-shield-audit.md` says
  this invariant has no automated check.
- `Main/_Module/SubModule.xml`: does it declare `<DependedModule Id="LOTRLOME_Armory"/>`? The troops
  reference items from that module.

## Verification already run (do not repeat, challenge instead)

- `python tools/validate_moduledata.py`: PASS, 6,056 items, no `DUPLICATE_ITEM_DEF`
- `python tools/validate_all_troop_refs.py`: mordor 196 armour refs, 0 missing
- `python -m unittest discover -s tools/tests`: 571 OK
- `dotnet test TAOM.Tests`: 6,655 passed, 0 failed
- `python tools/lint_docs.py`: clean

Note `tools/validate_all_troop_refs.py` only checks `sk_*` / `ar_*` prefixes by design, so weapon,
shield and horse refs are NOT covered by it. And `tools/validate_mesh_refs.py --scan-bodies` is blind
to the whole Armory because `tpac_paths_for_modules` globs
`<game>/Modules/<m>/AssetPackages/*.tpac` and `LOTRLOME_Armory` has no `AssetPackages` directory,
its 5,272 tpacs live under `Assets/**`. Both gaps are pre-existing. Confirm or refute both.

## Output format

For each finding:

```
[SEVERITY: CRITICAL | HIGH | MEDIUM | LOW]  <short title>
File: <path>:<line>
Defect: <what is wrong>
Failure mode: <what a player or developer actually experiences>
Fix: <minimal concrete change>
Evidence: <the command output or quoted code you relied on>
```

End with a verdict line: SHIP / SHIP WITH FIXES / DO NOT SHIP, and a ranked list of what must change
first. If you find nothing in a Known Suspect, say so explicitly for that suspect so the gap in
coverage is visible.
