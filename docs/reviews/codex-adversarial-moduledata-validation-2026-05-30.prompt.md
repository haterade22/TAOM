# Adversarial review: TAOM ModuleData schema-driven validator (Python tooling)

You are an adversarial code reviewer. Your job is to find real bugs and dispute weak claims with evidence. This is a PURE PYTHON tooling change -- there is no C#, no Harmony patch, no GameModel, no TaleWorlds API. Do NOT invent C#-shaped findings. Read the actual files and the real TAOM XML data before asserting anything; an unverified claim is worse than no claim.

## What this is

A schema-driven cross-reference + validation engine for TAOM's Bannerlord ModuleData XML, adopted (idea-only, MIT) from TheOldRealms/TOR_Tools. It builds id registries from the installed game + the TAOM repo, then resolves prefix-based references and runs per-schema duplicate-id / enum / civilian-type checks. The declarative JSON schemas under tools/schemas/ are the source of truth. The tool is READ-ONLY (never writes game data). It is the consolidation of several existing one-shot validators (tools/validate_all_troop_refs.py, tools/audit_item_refs.py).

It must catch these recurring TAOM bug classes WITHOUT false positives:
- BROKEN_ITEM_REF: an `id="Item.X"` (or any `="Item.X"`) ref that resolves to no defined item -- the "underwear bug" (troop spawns naked).
- BROKEN_TROOP_REF: a `NPCCharacter.X` ref (upgrade_target id=, party-template stack troop=, culture basic_troop=) to a deleted troop.
- UNKNOWN_CULTURE: a `culture="Culture.X"` where X is not a valid culture StringId (the classic "wrote rohan instead of vlandia" bug).
- DUPLICATE_NPC_ID / DUPLICATE_CULTURE_ID / DUPLICATE_ROSTER_ID: same id defined twice.
- MISSING_CIVILIAN_TYPE: a civilian EquipmentRoster whose EquipmentSet lacks equipmentType="Civilian".
- DUPLICATE_ITEM_DEF: same item id defined in >1 LOTRLOME_items folder (engine silently shadows one).
- BROKEN_PARTY_TEMPLATE_REF (warning): a `PartyTemplate.X` ref to an undefined template.

## TAOM ID CHEATSHEET (verify config IDs against this)

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla engine ids): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings, sturgia=Dale, battania=Khand
NOTE: "rohan" is NOT a valid culture id (Rohan = vlandia). "dol_guldur" is NOT valid (use dolguldur). TAOM also defines minor/bandit cultures (gondor_soldiers, erebor_warriors, mirkwood_stalkers, dunland_raiders, rhun_raiders, harad_raiders, gundabad_raiders, umbar_corsairs, shaghana, abanissa).

## READ FIRST

- tools/taom_schema.py -- the engine (Issue model, Registries, Schema, REF_KINDS, Validator, build_registries, format_report)
- tools/validate_moduledata.py -- the CLI
- tools/schemas/taom_npccharacter.json, taom_spcultures.json, taom_equipmentsets.json -- the schemas
- tools/tests/test_validate_moduledata.py -- the tests
- .claude/rules/xml-data.md -- "EquipmentRosters Schema" (the civilian equipmentType rule) + the culture-id table this tool encodes
- .claude/rules/troops.md -- troop id conventions
- tools/validate_all_troop_refs.py and tools/audit_item_refs.py -- the existing validators this tool consolidates (compare scan logic)

Then READ REAL DATA to ground every claim (do not reason from the sample in this prompt alone):
- Main/_Module/ModuleData/troops/troops_gondor.xml (NPCCharacter shape: multi-line opens, <equipment slot= id="Item.X"/>, <upgrade_target id="NPCCharacter.X"/>, <EquipmentSet id="..._civilian_..." equipmentType="Civilian"/>)
- Main/_Module/ModuleData/taom_spcultures.xml (<Culture id=.. basic_troop="NPCCharacter.X" .. villager_party_template="PartyTemplate.X" .. <item id="Item.X"/> trade goods)
- Main/_Module/ModuleData/taom_partyTemplates.xml (<partyTemplates><MBPartyTemplate id="X"><stacks><PartyTemplateStack troop="NPCCharacter.X"/>)
- Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_gondor.xml and equipmentsets/taom_lord_template_equipment.xml (standalone <EquipmentRoster id=.. culture=..><EquipmentSet [equipmentType="Civilian"]>..<Equipment slot= id="Item.X"/>)

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each, with evidence from the data

1. Attribute-agnostic ref patterns. REF_KINDS uses `="Item\.([A-Za-z0-9_.\-]+)"` (and the same for NPCCharacter./Culture./PartyTemplate.), matching the prefixed value on ANY attribute. Does this FALSE-match anything that is not a real reference -- e.g. a substring inside a free-text attribute (a `text="...{=key}...Item.foo..."` description, a comment, an example), or a longer token where the prefix appears mid-value? Conversely, does the `[A-Za-z0-9_.\-]` capture class TRUNCATE any real id that contains another character, producing a false BROKEN_*? Inspect real ids in the data.

2. Culture validity false accept/reject. The culture registry is (cultures defined in taom_spcultures.xml + vanilla SandBoxCore spcultures.xml) UNION a hardcoded VANILLA_CULTURES floor. Could a genuine typo culture be wrongly ACCEPTED because the floor set or vanilla scan contains it? Could a VALID culture ref be wrongly REJECTED (a culture defined only in code, or via XSLT output that is not in any scanned XML)? Is the floor set missing any culture that real TAOM XML references?

3. Empty-registry silent skip. The engine skips a ref kind when its registry is empty (`active = [k for k in REF_KINDS if getattr(self.reg, k.registry_attr)]`). The team already found+fixed one case (party templates were empty because the def regex looked for `<PartyTemplate` but TAOM uses `<MBPartyTemplate>`). Are there OTHER latent element-name / regex mismatches that would make a registry silently empty on real data (e.g., are NPCCharacter or Item or Culture defs ever written with a different element name or attribute form that the def regexes miss)? Run the def regexes mentally against the real files.

4. Civilian rule precision. `_civilian_rule` fires on EquipmentRoster ids containing the substring `_civ` and checks the FIRST `<EquipmentSet>` for the literal string `equipmentType="Civilian"`. Over-match: any roster id containing `_civ` that is NOT civilian (e.g. a name like `..._civic_...` or `..._civ...` used for another meaning)? Under-match: a civilian roster whose id does NOT contain `_civ`? Does the literal-string check miss single-quote or whitespace variants (`equipmentType='Civilian'`, `equipmentType = "Civilian"`)? Does checking only the first EquipmentSet miss a multi-set roster?

5. Duplicate-item-def scoping. DUPLICATE_ITEM_DEF only fires for item ids defined in >1 file whose path contains `LOTRLOME_items`. Could this FALSE-positive when the same id legitimately appears in two files that are not really duplicates (e.g. a base + an XSLT-transformed mirror, or a backup file)? Could it MISS a real duplicate across an Armory folder and a non-Armory module?

6. Entry attribution. `_entry_by_line` maps line numbers to the owning entry id for nicer messages, using an `awaiting` flag set on the entry-element open. Trace it against the multi-line NPCCharacter opens and the nested `<skill id="Athletics"/>` children in troops_gondor.xml. Does it ever attribute a ref to the wrong entry, or to a child element id?

7. Performance / ReDoS. The tool scans ~27,000 item refs across thousands of XML files and builds registries from the whole game install. Are any regexes vulnerable to catastrophic backtracking on large/pathological input? Is any pass quadratic in a way that matters at this scale?

8. Sentinels. Item.None is allow-listed. Are there OTHER engine sentinels that should be treated as always-valid (empty id, `Item._none`, culture `neutral_culture`, etc.) that, if unhandled, would produce false BROKEN_* on a clean tree?

## REQUIRED SECTIONS in your output

CROSS-REFERENCE CORRECTNESS: for each REF_KIND, state whether its pattern + registry correctly classifies refs on the real data, and give any false-positive or false-negative you can construct from actual TAOM XML (quote the line).

REGISTRY-BUILDING CORRECTNESS: audit each def regex (_ITEM_DEF_RE, _NPC_DEF_RE, _CULTURE_DEF_RE, _PARTYTEMPLATE_DEF_RE) and the root lists in build_registries against the real Bannerlord XML element/attribute conventions. Flag any element or attribute form that the regex misses or over-captures.

ISSUE-CODE MISFIRE ANALYSIS: for each of the 8 issue codes, give the precise condition under which it could fire incorrectly (false positive) or fail to fire when it should (false negative) on real TAOM data.

FINDINGS OR OBSERVATIONS: numbered, each with Severity (HIGH/MED/LOW), the file:line, a concrete repro or quoted evidence, and a suggested fix. If you find nothing for a section, say so explicitly -- do not pad.

## QUALITY GATES

- Quote real lines from the actual files/data for every confirmed finding. No hypotheticals presented as facts.
- Distinguish "I confirmed X by reading file Y line Z" from "I suspect X".
- A clean result is an acceptable outcome -- if the tool is correct, say which suspects you confirmed safe and why. Do not manufacture findings.
- The tool intentionally scopes OUT: weapon-craft piece refs, BodyProperty refs, scene refs, and inline EquipmentSet-by-id refs to vanilla rosters. Do not flag deliberate scope as a bug; you may note it under OBSERVATIONS as a coverage gap.

## Prior review lessons

SUCCESSES: config ID cross-reference catches rohan/dol_guldur mismatches; reading real data catches regex over/under-match; tracing empty-registry conditions catches silently-disabled checks.
FAILURES to avoid: assuming empire=Rohan (it is Dunland); flagging deliberate scope as a bug; asserting "missing" without grepping; reasoning from the prompt's sample instead of the real files.

Write your full review to docs/reviews/codex-adversarial-moduledata-validation-2026-05-30.md (this is where stdout is captured).
