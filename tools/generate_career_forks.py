#!/usr/bin/env python3
"""Write one kingdom's forked career trees into the career XML from a JSON spec (#769 C1).

The career redesign replaces the old six-group, five-choice tree of each career with six FORKS: two
or three options each, one pick per fork. Authoring 71 careers of XML by hand invites typos and
balance drift, so the design lives in one reviewable JSON spec per kingdom and this tool turns it into
the two career files.

Usage (run from the repo root):

    python tools/generate_career_forks.py <spec-name>                 # dry run: validate, summarise
    python tools/generate_career_forks.py <spec-name> --apply         # write the two XML files
    python tools/generate_career_forks.py <spec-name> --check         # drift gate: exit 1 when the XML differs
    python tools/generate_career_forks.py <spec-name> --table PATH    # also write a Markdown review table

Exit codes: 0 clean, 1 drift (--check only; a hand-edited region or a refused change counts as
drift), 2 an error: an invalid spec, a refusal to write, a bad file, any unexpected exception. Nothing
is written when any error exists, and every error is listed, not just the first.

Inputs
------
    tools/career_design/_rules.json        balance numbers, bans, caps (every number is a draft)
    tools/career_design/<spec-name>.json   one kingdom's spec
    Main/Features/CareerSystem/            read at run time, never copied into the rules:
        Domain/PassiveEffectType.cs          effect names
        Domain/AttackTypeMask.cs             attack_type_mask names
        Domain/PassiveEffectConsumers.cs     Consumed, ClassFilteredTypes, MaskGatedTypes
        Diagnostics/CareerPerkConsumerMap.cs IsFlat (a count, not a factor); its body must be a chain of
                                             `type == PassiveEffectType.X` joined by `||`, else exit 2
        CareerSystemIoC.cs                   BuildCareerArchetypeMap, careerId -> archetype, the map the ability
                                             executor registry reads; it must be nothing but one returned
                                             `new Dictionary<string, CareerArchetype> { ["id"] = CareerArchetype.X, }`
                                             (no statement after it, no call chained on it), else exit 2
    Main/_Module/ModuleData/special_resources/   special_resources_config.xml, troop_resource_costs.xml
    Main/_Module/ModuleData/troops/, characters/ the NPCCharacter culture of each upkeep troop
    Main/_Module/ModuleData/Languages/*/std_taom_*.xml  the existing translation rows, read only to warn about
                                         an added key that already has one; the folder must hold a
                                         std_taom_career_data_strings_*.xml, else exit 2

Spec schema (one kingdom)
-------------------------
    kingdom         display name
    culture_ids     the kingdom's culture ids; must equal each career's EligibleCultures
    careers[]       id (a career in taom_careers.xml), archetype (infantry|ranged|cavalry, and it must equal
                    the one CareerSystemIoC.cs maps the career to), tree_version, point_levels (6 ascending
                    positive ints), root {description, effects[1..2]}, forks[6]
    fork            slot 1-6, name, max_picks (default 1), options[2..3]; the tier follows the slot
    option          name, description, and either effects[0..2] (a Passive choice) or
                    mutations[1..2] plus an explicit vu (a Keystone choice)
    effect          type, magnitude, attack_type_mask?, troop_class? (a class list, or All for every class)
    mutation        property, calculator, value (> 0), operation? (target is the career's ability template;
                    the loader defaults operation to Add, so it is written only when the spec states it)

The special resource a career may use, and whether it has upkeep troops, are NOT spec fields: they
come from the two special_resources files (kingdom row first, then culture row, as
SpecialResourceService.ResolveResource does, over the career's EligibleCultures). Upkeep troops are the
troop_resource_costs rows with a daily_upkeep > 0 whose troop's NPCCharacter culture (troops/*.xml and
characters/*.xml) is one of the career's EligibleCultures; the row's resource_id is documentation only, the
runtime charges every cost-row troop in the party in the player's own resolved resource. An upkeep row whose
troop is found nowhere is an error, and so is a duplicate resource id. A spec field would be a second source
of truth that can disagree with the data.

What it writes
--------------
Per career, inside guarded regions:

    taom_career_choices.xml   the root <Choice> and six <ChoiceGroup max_picks=...>
        <!-- career-forks:begin <career> sha256=<hash of the marker's attributes and the body> -->  ...
        <!-- career-forks:end <career> -->
    taom_careers.xml          the <ChoiceGroups> list, in the same kind of markers, plus the
                              tree_version and point_levels attributes on <Career>; the begin marker
                              records both and `shape=v2-<hash>`, a fingerprint of the tree structure

Ids and keys: group <career>_f<slot>, choice <career>_f<slot>_<a|b|c>, the root keeps the career's
root_choice_id; descriptions {=taom_cf_<choice id>}text, group names
{=taom_career_grp_<career>_f<slot>}name. The headline effect of an option goes first: the career
screen draws the icon of an option's first effect (a keystone uses keystone_icon). The option "name"
is for the review table only. No icon_sprite, is_percentage, PassiveEffect operation or root group_id is
written: the loader never reads them. A Mutation's operation is parsed into MutationDefinition.Operation (default
Add) and nothing consumes it: the calculator decides the arithmetic (flat adds the value), so the attribute is
inert either way. It is written exactly when the spec states it, so a new spec may omit it and gondor.json keeps
stating it only so its shipped regions stay byte-identical.

First adoption (no markers yet) replaces the career's existing root choice and its existing groups
(ids from taom_careers.xml), which must form one contiguous span separated only by whitespace and
comments; otherwise the tool refuses. It also refuses unless the spec's tree_version is above the
career's live one (default 1), because saves migrate only when the stored version is lower.

Later runs verify the region's hash (it covers the marker's recorded attributes together with the body)
and the recorded attributes against the live ones first, and REFUSE when either differs (someone
hand-edited generated text or the marker). They also gate the tree version: a lower tree_version is
refused, and so is a changed shape (root id, point levels, per fork the tier and max_picks, per option
each effect as `type@mask#class` or the mutation properties) unless tree_version rose. A mask or class
is normalised first: an omitted mask, All, or a list covering Melee and Ranged is "All", an omitted
class, All or all three classes is "All", so a respelling is not structural. The `v2-` prefix is the
version of that definition; a marker written under another one is refused with its own message, not
read as a structural edit. A text-only or magnitude-only edit keeps the version. Every byte outside the replaced regions and the two attributes stays identical,
including each file's BOM and newline style. The output is parsed with ElementTree before anything is
written, both files go to a .gcf-new temp file first (not .xml: the localization tool globs
career_system/*.xml) and are then renamed into place; when the second rename fails the first file is
written back to its original bytes (exit 2).

After an apply the tool prints the next steps: the strings tool when the run added, re-worded or removed a loc
key (the English source goes stale otherwise), the free --sync-ids --seed-only --apply translator pass (one command per language)
when an added key has no language row yet, then the Python and C# tests. On an update it lists every key whose
English changed, because the translation pipeline never re-translates a row under a changed English.

Validation (rules in _rules.json)
---------------------------------
 1 spec schema   2 career and cultures   3 effect types, masks, classes   4 resource rules
 5 point levels and tree version   6 balance (value units per tier)   7 caps over every build
 8 generated ids and keys unique against both files

Balance: _rules.json "vu_unit" gives the benefit worth one value unit (VU); check_rules refuses a priced type
that belongs to no cap group, so no passive pip can stack without limit (keystone mutations are outside every
group; only CooldownReduction has a runtime floor, min_cooldown_seconds). The benefit of an effect
is its magnitude, except for the "lower_is_better" cost types (TroopWages, RecruitCost, ...), whose
runtime multiplies by (1 + magnitude), so the benefit is the NEGATIVE magnitude; an effect with a
benefit below 0 is a penalty and is refused. An effect narrowed by troop_class is worth 1/class_bonus
as much per unit, so class-only effects may be 50% larger. An option is the sum of benefit / unit over
its effects (a keystone carries an explicit "vu"), must sit within budget_tolerance of its tier budget,
and the options of one fork within fork_spread_tolerance of each other. The root pip is priced the same way and
is refused above root_budget plus budget_tolerance (an upper bound only; the review table shows the root's VU). Caps sum the benefit per cap
group over the root plus, per fork, the best max_picks positive options (a fork may stay empty), which
is the worst build; attack_type_mask and troop_class are not discounted. The upgrade-cost types
(TroopUpgradeCost, SpecialResourceUpgradeCostModifier) are capped at 0.5 of benefit: TroopUpgradeCost has no
runtime floor, so a cut past -1 would give a negative upgrade price.

A class-filtered effect in fork slots 1, 5 and 6 may name only the career's own archetype class (infantry ->
Infantry, ranged -> Ranged, cavalry -> Cavalry); slots 2 to 4 and the root take any class. troop_class "All"
reads as every class, the same as omitting it (it is not narrowed, and it is written as All).

Warnings, printed after the summary and never blocking: an update lists every key whose English changed; every
option whose effects changed in meaning while its description (key and English) did not, because the tooltip may
state an old number (a respelled mask or class and a reordered effect list are no change; a first adoption has
nothing to compare, but a re-adoption over a generated tree, after its markers were deleted, warns like an
update); and every key the run ADDS that already has a row in a std_taom_*.xml file of a language (a removed
option letter that comes back would otherwise revive its old translation, since seeding fills only missing ids,
and the translator skips a key a sibling std_taom file carries), naming the file when it is not the career data
file, with the reset step from docs/localization/TRANSLATOR_GUIDE.md. A commented-out row is not a row. A missing
Languages folder is an error only when a key was added.

tools/career_design/gondor.json is the first real spec; the tests use temporary fixtures.
"""
import argparse
import hashlib
import json
import math
import os
import re
import sys
import traceback
import xml.etree.ElementTree as ET
from collections import namedtuple
from pathlib import Path
from xml.sax.saxutils import escape, unescape

ROOT = Path(__file__).resolve().parents[1]
CAREER_DIR = ROOT / "Main" / "_Module" / "ModuleData" / "career_system"
RESOURCE_DIR = ROOT / "Main" / "_Module" / "ModuleData" / "special_resources"
DESIGN_DIR = ROOT / "tools" / "career_design"
SYSTEM_DIR = ROOT / "Main" / "Features" / "CareerSystem"
LANGUAGES_DIR = ROOT / "Main" / "_Module" / "ModuleData" / "Languages"

CAREERS_NAME = "taom_careers.xml"
CHOICES_NAME = "taom_career_choices.xml"
BOM = b"\xef\xbb\xbf"
EPS = 1e-9
LETTERS = "abc"
TEMP_SUFFIX = ".gcf-new"
# The version of the shape fingerprint's definition, written as `shape=<version>-<hash>` in the careers marker.
# Change it whenever structure_fingerprint decides on different things, so an older marker is told apart from
# a real edit of the tree.
SHAPE_VERSION = "v2"
DEFAULT_TROOP_CLASSES = ("Infantry", "Ranged", "Cavalry")
# Fork slots whose class-filtered effects must name only the career's own archetype class (infantry ->
# Infantry, ranged -> Ranged, cavalry -> Cavalry). Slots 2 to 4 and the root take any class.
ARCHETYPE_CLASS_SLOTS = (1, 5, 6)
GUIDE = "docs/localization/TRANSLATOR_GUIDE.md"
# The twelve language folders under Languages/, as translate_with_claude.LANGUAGES keys it (a test pins this).
TRANSLATOR_LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]

CareerInfo = namedtuple("CareerInfo", "id ability root_choice cultures groups")
CsSets = namedtuple("CsSets", "effect_types attack_masks consumed class_filtered mask_gated flat archetypes")
CareerCtx = namedtuple("CareerCtx", "resource upkeep problem cultures")
Splice = namedtuple("Splice", "status detail changed added stale removed", defaults=([], [], []))


class GeneratorError(Exception):
    """A problem that stops the run before anything is written."""


class Refusal(GeneratorError):
    """Existing generated text was changed by hand, or the old tree is not where it should be."""


# --------------------------------------------------------------------------- #
# Rules                                                                        #
# --------------------------------------------------------------------------- #
REQUIRED_RULE_KEYS = ["banned", "archetypes", "slot_tiers", "tier_budget", "budget_tolerance",
                      "fork_spread_tolerance", "class_bonus", "root_budget", "troop_classes", "lower_is_better",
                      "mask_forbidden", "resource_types", "upkeep_types", "mutation_properties",
                      "mutation_calculators", "mutation_operations", "vu_unit", "caps"]
STRING_LIST_KEYS = ["archetypes", "troop_classes", "lower_is_better", "mask_forbidden", "resource_types",
                    "upkeep_types", "mutation_properties", "mutation_calculators", "mutation_operations"]


def is_number(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v)


def is_int(v):
    return isinstance(v, int) and not isinstance(v, bool)


def is_str_list(v, allow_empty=True):
    return isinstance(v, list) and (allow_empty or bool(v)) and all(isinstance(s, str) and s for s in v)


def check_rules(rules):
    """Shape and numbers of _rules.json. Raises GeneratorError listing every problem."""
    if not isinstance(rules, dict):
        raise GeneratorError("_rules.json must hold a JSON object")
    missing = [k for k in REQUIRED_RULE_KEYS if k not in rules]
    if missing:
        raise GeneratorError(f"_rules.json is missing: {', '.join(missing)}")
    problems = []
    for key in STRING_LIST_KEYS:
        if not is_str_list(rules[key]):
            problems.append(f"{key} must be a list of non-empty strings")
    if not (isinstance(rules["banned"], dict) and all(isinstance(k, str) and isinstance(v, str)
                                                      for k, v in rules["banned"].items())):
        problems.append("banned must map an effect type to a reason string")
    tiers = rules["slot_tiers"]
    if not (isinstance(tiers, list) and tiers and all(is_int(t) and t > 0 for t in tiers)):
        problems.append("slot_tiers must be a non-empty list of positive integers")
    else:
        budget = rules["tier_budget"]
        if not isinstance(budget, dict):
            problems.append("tier_budget must be an object keyed by tier")
        else:
            for tier in sorted(set(tiers)):
                if str(tier) not in budget:
                    problems.append(f"tier_budget has no entry for tier {tier}")
            for tier, value in budget.items():
                if not (is_number(value) and value > 0):
                    problems.append(f"tier_budget['{tier}'] must be a positive number")
    for key in ("budget_tolerance", "fork_spread_tolerance"):
        if not (is_number(rules[key]) and rules[key] >= 0):
            problems.append(f"{key} must be a number of 0 or more")
    if not (is_number(rules["class_bonus"]) and rules["class_bonus"] >= 1):
        problems.append("class_bonus must be a number of 1 or more")
    if not (is_number(rules["root_budget"]) and rules["root_budget"] > 0):
        problems.append("root_budget must be a positive number")
    units = rules["vu_unit"]
    if not isinstance(units, dict):
        problems.append("vu_unit must be an object")
    else:
        for name, unit in units.items():
            if not (is_number(unit) and unit > 0):
                problems.append(f"vu_unit['{name}'] must be a positive number")
    caps = rules["caps"]
    if not isinstance(caps, dict):
        problems.append("caps must be an object")
    else:
        for name, cap in caps.items():
            if not (isinstance(cap, dict) and is_str_list(cap.get("types"), allow_empty=False)):
                problems.append(f"cap '{name}' needs types[] (a non-empty list of effect types) and limit")
            elif not (is_number(cap.get("limit")) and cap["limit"] > 0):
                problems.append(f"cap '{name}': limit must be a positive number")
    if isinstance(units, dict) and isinstance(caps, dict):
        capped = {t for cap in caps.values() if isinstance(cap, dict) and isinstance(cap.get("types"), list)
                  for t in cap["types"]}
        uncapped = sorted(name for name in units if name not in capped)
        if uncapped:
            problems.append(f"vu_unit prices {', '.join(uncapped)}, which belongs to no cap group: an uncapped type "
                            f"can stack without limit, so give it a cap group in caps")
    if problems:
        raise GeneratorError("_rules.json: " + "; ".join(problems))


def check_rules_against_cs(rules, cs):
    """Every effect type the rules name must be a PassiveEffectType member: a typo in the rules would
    otherwise be a unit, cap, ban or sign that silently matches nothing."""
    named = {}
    for section in ("banned", "vu_unit"):
        for name in rules[section]:
            named.setdefault(name, []).append(section)
    for section in ("lower_is_better", "mask_forbidden", "resource_types", "upkeep_types"):
        for name in rules[section]:
            named.setdefault(name, []).append(section)
    for cap_name, cap in rules["caps"].items():
        for name in cap["types"]:
            named.setdefault(name, []).append(f"cap '{cap_name}'")
    return [f"_rules.json names '{name}' in {', '.join(where)}, which is not a PassiveEffectType member"
            for name, where in sorted(named.items()) if name not in cs.effect_types]


def read_json(path, what):
    if not path.is_file():
        raise GeneratorError(f"{what} not found: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (ValueError, UnicodeDecodeError) as exc:
        raise GeneratorError(f"{path.name} is not valid JSON: {exc}")


# --------------------------------------------------------------------------- #
# C# sets, read at run time                                                    #
# --------------------------------------------------------------------------- #
CS_TOKEN = re.compile(r'"(?:\\.|[^"\\\n])*"|//[^\n]*|/\*.*?\*/', re.S)


def strip_cs_comments(text):
    """Drop // and /* */ comments but leave string literals alone, so prose cannot add names and a
    string holding '//' cannot swallow code."""
    return CS_TOKEN.sub(lambda m: m.group(0) if m.group(0).startswith('"') else "", text)


def read_cs(path):
    path = Path(path)
    if not path.is_file():
        raise GeneratorError(f"{path.name} not found at {path}")
    try:
        return strip_cs_comments(path.read_text(encoding="utf-8-sig"))
    except UnicodeDecodeError as exc:
        raise GeneratorError(f"{path.name}: not valid UTF-8: {exc}")


def parse_enum_names(cs_text, enum_name):
    """Member names of a C# enum, in order. Comments are stripped first so prose cannot add names."""
    m = re.search(rf"enum\s+{re.escape(enum_name)}\b[^{{]*\{{([^}}]*)\}}", strip_cs_comments(cs_text))
    if not m:
        raise GeneratorError(f"enum {enum_name} not found")
    names = []
    for token in m.group(1).split(","):
        nm = re.match(r"\s*(?:\[[^\]]*\]\s*)*([A-Za-z_]\w*)", token)
        if nm:
            names.append(nm.group(1))
    return names


def effect_names(body):
    return set(re.findall(r"\bPassiveEffectType\s*\.\s*(\w+)", body))


def parse_type_set(cs_text, set_name, source):
    """The PassiveEffectType members listed in `<set_name> = new HashSet<PassiveEffectType> { ... };`."""
    m = re.search(rf"\b{re.escape(set_name)}\s*=\s*new\s+HashSet<\s*PassiveEffectType\s*>\s*(?:\(\s*\)\s*)?\{{(.*?)\}}\s*;",
                  cs_text, re.S)
    if not m:
        raise GeneratorError(f"{source}: the set {set_name} was not found (expected "
                             f"`{set_name} = new HashSet<PassiveEffectType> {{ ... }};`)")
    names = effect_names(m.group(1))
    if not names:
        raise GeneratorError(f"{source}: the set {set_name} lists no PassiveEffectType member")
    return names


def parse_is_flat(cs_text, source):
    """The members IsFlat(PassiveEffectType) tests. The body must be a chain of `<param> == PassiveEffectType.X`
    joined by `||` (an expression body, or a block holding one `return` of such a chain); anything else is an
    error, because reading every type named in a more complex body could take an excluded type for a flat one."""
    m = re.search(r"\bbool\s+IsFlat\s*\(\s*PassiveEffectType\s+(\w+)\s*\)\s*(=>|\{)", cs_text)
    if not m:
        raise GeneratorError(f"{source}: the method IsFlat(PassiveEffectType) was not found")
    param = m.group(1)
    if m.group(2) == "=>":
        end = cs_text.find(";", m.end())
        body = cs_text[m.end():end] if end >= 0 else ""
    else:
        depth, i = 1, m.end()
        while i < len(cs_text) and depth:
            depth += {"{": 1, "}": -1}.get(cs_text[i], 0)
            i += 1
        ret = re.fullmatch(r"\s*return\s+(.*?)\s*;\s*", cs_text[m.end():i - 1], re.S)
        body = ret.group(1) if ret else ""
    test = rf"{re.escape(param)}\s*==\s*PassiveEffectType\s*\.\s*(\w+)"
    if not re.fullmatch(rf"\s*{test}(?:\s*\|\|\s*{test})*\s*", body):
        raise GeneratorError(f"{source}: IsFlat must be a chain of `{param} == PassiveEffectType.X` joined by `||` "
                             f"(an expression body, or one `return` of that chain); the generator reads "
                             f"nothing else. Found: {' '.join(body.split())[:90]!r}")
    return set(re.findall(test, body))


ARCHETYPE_ENTRY = re.compile(r'\[\s*"([^"]+)"\s*\]\s*=\s*CareerArchetype\s*\.\s*(\w+)')


def parse_archetype_map(cs_text, source):
    """careerId -> archetype name (lower case) from the dictionary initialiser BuildCareerArchetypeMap returns, the
    one source the ability executor registry and ICareerArchetypeService both read. The body must be nothing but
    `["id"] = CareerArchetype.X,` entries (comments are stripped first, so a disabled entry is absent, as it is at
    run time); anything else is an error, because a map the generator half-reads would approve a wrong archetype.
    The initialiser must be the whole body, as in parse_is_flat: an expression body (`=> new Dictionary<...> {...};`)
    or a block holding one `return new Dictionary<...> {...};`. A statement after it, a call chained on it, or a
    builder that delegates elsewhere would make the run time map differ from the initialiser, so each is an error."""
    m = re.search(r"\bBuildCareerArchetypeMap\s*\(\s*\)\s*(=>|\{)", cs_text)
    if not m:
        raise GeneratorError(f"{source}: the method BuildCareerArchetypeMap() was not found (the generator reads the "
                             f"career archetype map there)")
    expression = m.group(1) == "=>"
    start = re.compile((r"\s*" if expression else r"\s*return\s+")
                       + r"new\s+Dictionary\s*<\s*string\s*,\s*CareerArchetype\s*>\s*(?:\(\s*\)\s*)?\{").match(
        cs_text, m.end())
    if not start:
        raise GeneratorError(f"{source}: BuildCareerArchetypeMap must be nothing but "
                             f"`{'=>' if expression else '{ return'} new Dictionary<string, CareerArchetype> "
                             f"{{ ... }};`; the generator reads nothing else")
    depth, i = 1, start.end()
    while i < len(cs_text) and depth:
        depth += {"{": 1, "}": -1}.get(cs_text[i], 0)
        i += 1
    if not re.compile(r"\s*;" if expression else r"\s*;\s*\}").match(cs_text, i):
        raise GeneratorError(f"{source}: BuildCareerArchetypeMap does more than return its initialiser; the "
                             f"generator reads nothing else")
    body = cs_text[start.end():i - 1]
    entries = ARCHETYPE_ENTRY.findall(body)
    leftover = " ".join(re.sub(r"[\s,]+", " ", ARCHETYPE_ENTRY.sub("", body)).split())
    if leftover:
        raise GeneratorError(f"{source}: BuildCareerArchetypeMap holds something other than "
                             f"`[\"id\"] = CareerArchetype.X,` entries; the generator reads nothing else. "
                             f"Found: {leftover[:90]!r}")
    if not entries:
        raise GeneratorError(f"{source}: BuildCareerArchetypeMap lists no career")
    ids = [cid for cid, _ in entries]
    duplicated = sorted({cid for cid in ids if ids.count(cid) > 1})
    if duplicated:
        raise GeneratorError(f"{source}: BuildCareerArchetypeMap lists {', '.join(duplicated)} more than once "
                             f"(the index initialiser keeps the last one at run time, silently)")
    return {cid: name.lower() for cid, name in entries}


def load_cs_sets(system_dir):
    system_dir = Path(system_dir)
    domain, diagnostics = system_dir / "Domain", system_dir / "Diagnostics"
    effect_types = set(parse_enum_names(read_cs(domain / "PassiveEffectType.cs"), "PassiveEffectType"))
    attack_masks = set(parse_enum_names(read_cs(domain / "AttackTypeMask.cs"), "AttackTypeMask")) - {"None"}
    consumers = read_cs(domain / "PassiveEffectConsumers.cs")
    sets = {name: parse_type_set(consumers, name, "PassiveEffectConsumers.cs")
            for name in ("Consumed", "ClassFilteredTypes", "MaskGatedTypes")}
    sets["IsFlat"] = parse_is_flat(read_cs(diagnostics / "CareerPerkConsumerMap.cs"), "CareerPerkConsumerMap.cs")
    archetypes = parse_archetype_map(read_cs(system_dir / "CareerSystemIoC.cs"), "CareerSystemIoC.cs")
    for name, members in sets.items():
        unknown = sorted(members - effect_types)
        if unknown:
            raise GeneratorError(f"{name} names {', '.join(unknown)}, which PassiveEffectType.cs does not declare")
    return CsSets(effect_types, attack_masks, sets["Consumed"], sets["ClassFilteredTypes"],
                  sets["MaskGatedTypes"], sets["IsFlat"], archetypes)


# --------------------------------------------------------------------------- #
# Career info and special resource data                                        #
# --------------------------------------------------------------------------- #
def load_career_info(careers_text):
    root = ET.fromstring(careers_text.encode("utf-8"))
    infos = {}
    for el in root.iter("Career"):
        cultures = [c.get("id") for c in el.findall("./EligibleCultures/Culture") if c.get("id")]
        groups = [g.get("id") for g in el.findall("./ChoiceGroups/Group") if g.get("id")]
        infos[el.get("id")] = CareerInfo(el.get("id"), el.get("ability_template_id") or "",
                                         el.get("root_choice_id") or "", cultures, groups)
    return infos


class ResourceData:
    """Which special resource a career's cultures resolve to, and how many upkeep troops belong to them."""

    def __init__(self, by_kingdom, by_culture, upkeep):
        self.by_kingdom = by_kingdom
        self.by_culture = by_culture
        self.upkeep = upkeep            # culture id -> number of cost rows with a daily_upkeep

    def resource_for(self, ids):
        """Kingdom rows first, then culture rows, like SpecialResourceService.ResolveResource. None when
        nothing matches; GeneratorError when the career's ids reach two different resources."""
        pool = {self.by_kingdom[i] for i in ids if i in self.by_kingdom}
        if not pool:
            pool = {self.by_culture[i] for i in ids if i in self.by_culture}
        if len(pool) > 1:
            raise GeneratorError(f"the eligible ids {sorted(ids)} reach two special resources ({sorted(pool)}); "
                                 f"the generator cannot tell which one the player has")
        return next(iter(pool)) if pool else None

    def upkeep_troops(self, cultures):
        """Cost rows with a daily_upkeep whose troop's culture is one of `cultures`. The row's resource_id is
        documentation only (SpecialResourceService charges in the player's own resolved resource)."""
        return sum(self.upkeep.get(c, 0) for c in set(cultures))


def read_xml_root(path):
    if not path.is_file():
        raise GeneratorError(f"{path.name} not found at {path}")
    try:
        return ET.fromstring(path.read_bytes())
    except ET.ParseError as exc:
        raise GeneratorError(f"{path.name} does not parse as XML: {exc}")


def load_npc_cultures(npc_dirs):
    """NPCCharacter id -> culture id (the `Culture.` prefix dropped; None when the row has no culture) over
    every *.xml in the given folders (Main/_Module/ModuleData/troops and characters)."""
    cultures = {}
    for folder in npc_dirs:
        folder = Path(folder)
        if not folder.is_dir():
            raise GeneratorError(f"NPCCharacter folder not found: {folder}")
        for path in sorted(folder.glob("*.xml")):
            for npc in read_xml_root(path).iter("NPCCharacter"):
                npc_id = npc.get("id")
                if npc_id:
                    culture = npc.get("culture") or ""
                    cultures[npc_id] = (culture[len("Culture."):] if culture.startswith("Culture.") else culture) or None
    return cultures


def load_resource_data(resource_dir, npc_dirs=None):
    resource_dir = Path(resource_dir)
    if npc_dirs is None:
        npc_dirs = [resource_dir.parent / "troops", resource_dir.parent / "characters"]
    config = read_xml_root(resource_dir / "special_resources_config.xml")
    by_kingdom, by_culture = {}, {}
    resources = 0
    seen = set()
    for res in config.iter("Resource"):
        rid = res.get("id")
        if not rid:
            continue
        if rid in seen:
            raise GeneratorError(f"special_resources_config.xml: duplicate Resource id '{rid}' (the game reads the "
                                 f"rows last-wins, so the generator would not know which one applies)")
        seen.add(rid)
        resources += 1
        for el in res.findall("Kingdom"):
            by_kingdom.setdefault(el.get("id"), rid)
        for el in res.findall("Culture"):
            by_culture.setdefault(el.get("id"), rid)
    if not resources:
        raise GeneratorError("special_resources_config.xml holds no <Resource> row: nothing to derive from")
    costs = read_xml_root(resource_dir / "troop_resource_costs.xml")
    upkeep_ids, rows = [], 0
    for troop in costs.iter("Troop"):
        rows += 1
        try:
            daily = float(troop.get("daily_upkeep", "0") or 0)
        except ValueError:
            raise GeneratorError(f"troop_resource_costs.xml: troop '{troop.get('id')}' has a daily_upkeep "
                                 f"that is not a number")
        if daily > 0:
            upkeep_ids.append(troop.get("id"))
    if not rows:
        raise GeneratorError("troop_resource_costs.xml holds no <Troop> row: nothing to derive from")
    npc_cultures = load_npc_cultures(npc_dirs)
    upkeep, problems = {}, []
    for troop_id in upkeep_ids:
        if troop_id not in npc_cultures:
            problems.append(f"troop_resource_costs.xml: the upkeep troop '{troop_id}' is no NPCCharacter in "
                            f"troops/ or characters/")
        elif npc_cultures[troop_id] is None:
            problems.append(f"troop_resource_costs.xml: the upkeep troop '{troop_id}' has no culture attribute")
        else:
            upkeep[npc_cultures[troop_id]] = upkeep.get(npc_cultures[troop_id], 0) + 1
    if problems:
        raise GeneratorError("; ".join(problems))
    return ResourceData(by_kingdom, by_culture, upkeep)


def career_ctx(info, data):
    if info is None:
        return CareerCtx(None, 0, None, [])
    try:
        resource = data.resource_for(info.cultures)
    except GeneratorError as exc:
        return CareerCtx(None, 0, str(exc), info.cultures)
    return CareerCtx(resource, data.upkeep_troops(info.cultures) if resource else 0, None, info.cultures)


# --------------------------------------------------------------------------- #
# Balance maths                                                                #
# --------------------------------------------------------------------------- #
def split_tokens(text):
    return [t for t in re.split(r"[,|\s]+", text.strip()) if t]


def benefit(effect, rules):
    """What the effect is worth to the player: the magnitude, or its negative for a cost type whose
    runtime multiplies by (1 + magnitude)."""
    return -effect["magnitude"] if effect.get("type") in rules["lower_is_better"] else effect["magnitude"]


def narrowed(effect, rules):
    """True when troop_class limits an effect to fewer than every class. All names every class, so it never narrows."""
    classes = effect.get("troop_class")
    if not classes:
        return False
    every = {c.lower() for c in rules["troop_classes"]}
    named = {t.lower() for t in split_tokens(classes)}
    if "all" in named:
        named = (named - {"all"}) | every
    return named != every


def effect_vu(effect, rules):
    unit = rules["vu_unit"].get(effect["type"])
    if not unit:
        raise GeneratorError(f"effect type '{effect['type']}' has no vu_unit in _rules.json, "
                             f"so it cannot be balanced")
    vu = benefit(effect, rules) / unit
    return vu / rules["class_bonus"] if narrowed(effect, rules) else vu


def option_vu(option, rules):
    if "mutations" in option and not option.get("effects"):
        return float(option["vu"])
    return sum(effect_vu(e, rules) for e in option.get("effects", []))


def tier_of(fork, rules):
    return rules["slot_tiers"][fork["slot"] - 1]


def analyze_caps(career, rules):
    """Per cap group: the largest sum of benefit any build reaches and that build. Forks are independent
    and picks are voluntary (a fork may stay empty), so the worst build is the root plus, per fork, its
    top max_picks POSITIVE options, with no enumeration."""
    results = {}
    for name, cap in rules["caps"].items():
        types = set(cap["types"])

        def contribution(effects):
            return sum(benefit(e, rules) for e in effects if e.get("type") in types)

        total = contribution(career["root"].get("effects", []))
        build = {}
        for fk in career["forks"]:
            contribs = [contribution(o.get("effects", [])) for o in fk["options"]]
            order = sorted(range(len(contribs)), key=lambda i: (-contribs[i], i))
            picks = [i for i in order[:fk.get("max_picks", 1)] if contribs[i] > 0]
            total += sum(contribs[i] for i in picks)
            build[fk["slot"]] = [LETTERS[i] for i in sorted(picks)]
        results[name] = {"max": total, "limit": cap["limit"], "build": build}
    return results


# --------------------------------------------------------------------------- #
# Spec validation (rules 1 to 7)                                               #
# --------------------------------------------------------------------------- #
def check_keys(obj, where, required, optional, errors):
    """Required keys present, no unknown keys (a typo must not be ignored). Returns True when obj
    is an object."""
    if not isinstance(obj, dict):
        errors.append(f"{where}: must be an object")
        return False
    for key in required:
        if key not in obj:
            errors.append(f"{where}: required field '{key}' is missing")
    for key in obj:
        if key not in required and key not in optional and key != "notes":
            errors.append(f"{where}: unknown key '{key}'")
    return True


def check_text(value, where, label, errors):
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{where}: {label} must be a non-empty string")
        return False
    if re.search(r"[\x00-\x1f\x7f]", value):
        errors.append(f"{where}: {label} holds a control character")
        return False
    if "{" in value or "}" in value:
        errors.append(f"{where}: {label} holds a brace ({{ or }}), which the engine reads as a text variable "
                      f"or a loc key")
        return False
    return True


def canonical(tokens, valid):
    lookup = {v.lower(): v for v in valid}
    return [lookup[t.lower()] for t in tokens if t.lower() in lookup]


def is_full_mask(tokens):
    """True when the tokens name every delivery and no kind: the mask the C# side calls All."""
    expanded = set()
    for token in tokens:
        low = token.lower()
        expanded |= {"melee", "ranged"} if low == "all" else {low}
    return expanded == {"melee", "ranged"}


def check_mask(effect, where, etype, rules, cs, errors):
    mask = effect["attack_type_mask"]
    tokens = split_tokens(mask) if isinstance(mask, str) else []
    if not tokens:
        errors.append(f"{where}: attack_type_mask must be a comma list of names")
        return
    valid = {m.lower() for m in cs.attack_masks}
    bad = [t for t in tokens if t.lower() not in valid]
    for tok in bad:
        errors.append(f"{where}: attack_type_mask token '{tok}' is not valid (use {', '.join(sorted(cs.attack_masks))})")
    if bad or not (isinstance(etype, str) and etype in cs.effect_types):
        return
    if etype not in cs.mask_gated:
        errors.append(f"{where}: attack_type_mask is read only on mask-gated types "
                      f"({', '.join(sorted(cs.mask_gated))}), not '{etype}'; the loader would ignore it")
    elif etype in rules["mask_forbidden"] and not is_full_mask(tokens):
        errors.append(f"{where}: attack_type_mask on {etype} must be All or omitted: CareerChoicesIntegrationTests "
                      f"bans a narrower mask there until the Active Effects panel can show masks")


def check_effect(effect, where, rules, cs, ctx, errors):
    start = len(errors)
    if not check_keys(effect, where, ["type", "magnitude"], ["attack_type_mask", "troop_class"], errors):
        return False
    etype = effect.get("type")
    if not isinstance(etype, str):
        errors.append(f"{where}: type must be a string")
    elif etype not in cs.effect_types:
        errors.append(f"{where}: unknown effect type '{etype}' (not in PassiveEffectType.cs)")
    elif etype in rules["banned"]:
        errors.append(f"{where}: effect type '{etype}' is banned ({rules['banned'][etype]})")
    elif etype not in cs.consumed:
        errors.append(f"{where}: effect type '{etype}' is not consumed at runtime (not in "
                      f"PassiveEffectConsumers.Consumed): the pip would be a phantom")
    if "magnitude" in effect:
        if not is_number(effect["magnitude"]) or effect["magnitude"] == 0:
            errors.append(f"{where}: magnitude must be a non-zero number")
        elif isinstance(etype, str) and benefit(effect, rules) < 0:
            hint = ("a cost type: the runtime multiplies by (1 + magnitude), so a cut is negative"
                    if etype in rules["lower_is_better"] else "a negative magnitude lowers the stat")
            errors.append(f"{where}: {etype} magnitude {effect['magnitude']:g} is a penalty ({hint})")
    if "attack_type_mask" in effect:
        check_mask(effect, where, etype, rules, cs, errors)
    if "troop_class" in effect:
        classes = effect["troop_class"]
        tokens = split_tokens(classes) if isinstance(classes, str) else []
        if not tokens:
            errors.append(f"{where}: troop_class must be a comma list of names")
        for tok in tokens:
            if tok.lower() not in {c.lower() for c in rules["troop_classes"]} | {"all"}:
                errors.append(f"{where}: troop_class '{tok}' is not valid "
                              f"(use {', '.join(rules['troop_classes'])}, All)")
        if isinstance(etype, str) and etype in cs.effect_types and etype not in cs.class_filtered:
            errors.append(f"{where}: troop_class is only allowed on class-filtered types "
                          f"({', '.join(sorted(cs.class_filtered))}), not '{etype}'")
    if isinstance(etype, str):
        if etype in rules["resource_types"]:
            if ctx.problem:
                errors.append(f"{where}: {etype}: {ctx.problem}")
            elif ctx.resource is None:
                errors.append(f"{where}: {etype} needs a special resource, but the career's cultures "
                              f"{sorted(ctx.cultures)} resolve to none in special_resources_config.xml")
            elif etype in rules["upkeep_types"] and ctx.upkeep == 0:
                errors.append(f"{where}: {etype} needs upkeep troops, but no troop of the career's cultures "
                              f"{sorted(ctx.cultures)} has a daily_upkeep in troop_resource_costs.xml")
    return len(errors) == start


def check_effects(effects, where, rules, cs, ctx, errors, at_least_one=False):
    """Returns True when every effect is individually valid."""
    if not isinstance(effects, list):
        errors.append(f"{where}: effects must be a list")
        return False
    ok = True
    if at_least_one and not effects:
        errors.append(f"{where}: needs at least one effect (a Passive choice with none is inert)")
        ok = False
    if len(effects) > 2:
        errors.append(f"{where}: at most 2 effects per option, got {len(effects)}")
        ok = False
    for i, effect in enumerate(effects, 1):
        ok = check_effect(effect, f"{where} effect {i}", rules, cs, ctx, errors) and ok
    return ok


def check_mutation(mutation, where, rules, errors):
    start = len(errors)
    if not check_keys(mutation, where, ["property", "calculator", "value"], ["operation"], errors):
        return False
    for key, valid, label in (("property", rules["mutation_properties"], "property"),
                              ("calculator", rules["mutation_calculators"], "calculator"),
                              ("operation", rules["mutation_operations"], "operation")):
        if key in mutation and mutation[key] not in valid:
            errors.append(f"{where}: mutation {label} '{mutation[key]}' is not one of {', '.join(valid)}")
    if "value" in mutation and not (is_number(mutation["value"]) and mutation["value"] > 0):
        errors.append(f"{where}: mutation value must be a number greater than 0 (a keystone makes the ability better)")
    return len(errors) == start


def check_option(option, where, rules, cs, ctx, errors):
    """Returns True when the option is well formed enough to be balanced."""
    start = len(errors)
    if not check_keys(option, where, ["name", "description"], ["effects", "mutations", "vu"], errors):
        return False
    for key in ("name", "description"):
        if key in option:
            check_text(option[key], where, key, errors)
    keystone = "mutations" in option
    if keystone:
        if option.get("effects"):
            errors.append(f"{where}: an option is either passive (effects) or a keystone (mutations), not both")
        mutations = option["mutations"]
        if not (isinstance(mutations, list) and 1 <= len(mutations) <= 2):
            errors.append(f"{where}: a keystone option needs 1 or 2 mutations")
        else:
            for i, mutation in enumerate(mutations, 1):
                check_mutation(mutation, f"{where} mutation {i}", rules, errors)
        if "vu" not in option:
            errors.append(f"{where}: a keystone option needs an explicit 'vu'")
        elif not (is_number(option["vu"]) and option["vu"] > 0):
            errors.append(f"{where}: vu must be a positive number")
    else:
        if "vu" in option:
            errors.append(f"{where}: 'vu' belongs to keystone options only")
        check_effects(option.get("effects", []), where, rules, cs, ctx, errors)
    return len(errors) == start


def check_archetype_class(option, slot, archetype, where, rules, errors):
    """Slots 1, 5 and 6 may narrow an effect to the career's own class only."""
    if slot not in ARCHETYPE_CLASS_SLOTS or not isinstance(archetype, str):
        return
    for i, effect in enumerate(option.get("effects", []), 1):
        if narrowed(effect, rules) and {t.lower() for t in split_tokens(effect["troop_class"])} != {archetype}:
            errors.append(f"{where} effect {i}: troop_class '{effect['troop_class']}' in slot {slot} must be "
                          f"only the career's archetype class ({archetype}); slots "
                          f"{', '.join(str(n) for n in range(1, 7) if n not in ARCHETYPE_CLASS_SLOTS)} "
                          f"and the root take any class")


def check_budget(vu, tier, where, rules, errors):
    budget = rules["tier_budget"][str(tier)]
    tolerance = rules["budget_tolerance"]
    if abs(vu - budget) > tolerance + EPS:
        errors.append(f"{where}: {vu:.2f} VU is outside the tier {tier} budget of {budget} +/- {tolerance}")


def check_root_budget(root, where, rules, errors):
    """The root pip is priced like an option and may not pass root_budget plus the tolerance."""
    try:
        vu = option_vu(root, rules)
    except GeneratorError as exc:
        errors.append(f"{where} root: {exc}")
        return
    budget, tolerance = rules["root_budget"], rules["budget_tolerance"]
    if vu > budget + tolerance + EPS:
        errors.append(f"{where} root: {vu:.2f} VU is above the root limit of {budget + tolerance:g} "
                      f"(root_budget {budget:g} plus budget_tolerance {tolerance:g})")


def check_archetype_source(archetype, cid, where, rules, cs, errors):
    """The spec's archetype must be the one CareerSystemIoC.cs maps the career to (its ability executor)."""
    mapped = cs.archetypes.get(cid)
    if mapped is None:
        errors.append(f"{where}: CareerSystemIoC.cs maps no archetype for this career, so it has no ability "
                      f"executor; add it to BuildCareerArchetypeMap first")
    elif mapped not in rules["archetypes"]:
        errors.append(f"{where}: CareerSystemIoC.cs maps this career to '{mapped}', which is not one of "
                      f"{', '.join(rules['archetypes'])}")
    elif mapped != archetype:
        errors.append(f"{where}: archetype '{archetype}' disagrees with CareerSystemIoC.cs, which maps this career "
                      f"to '{mapped}' (the ability executor it gets)")


def validate_career(career, spec_cultures, rules, careers, cs, data, errors):
    cid = career.get("id") if isinstance(career, dict) else None
    where = f"career '{cid}'"
    start = len(errors)
    if not check_keys(career, where, ["id", "archetype", "tree_version", "point_levels", "root", "forks"], [], errors):
        return
    if not (isinstance(cid, str) and cid):
        errors.append(f"{where}: id must be a non-empty string")
        return
    if career.get("archetype") not in rules["archetypes"]:
        errors.append(f"{where}: archetype '{career.get('archetype')}' is not one of "
                      f"{', '.join(rules['archetypes'])}")
    else:
        check_archetype_source(career["archetype"], cid, where, rules, cs, errors)
    info = careers.get(cid)
    if info is None:
        errors.append(f"{where}: not in taom_careers.xml")
    elif spec_cultures is not None and set(spec_cultures) != set(info.cultures):
        errors.append(f"{where}: spec culture_ids {sorted(spec_cultures)} differ from the "
                      f"career's EligibleCultures {sorted(info.cultures)}")
    ctx = career_ctx(info, data)
    if "tree_version" in career and not (is_int(career["tree_version"]) and career["tree_version"] >= 2):
        errors.append(f"{where}: tree_version must be an integer >= 2")

    slots = len(rules["slot_tiers"])
    levels = career.get("point_levels")
    if "point_levels" in career:
        if not (isinstance(levels, list) and all(is_int(v) for v in levels)):
            errors.append(f"{where}: point_levels must be a list of integers")
        else:
            if len(levels) != slots:
                errors.append(f"{where}: point_levels needs {slots} entries (one per fork), got {len(levels)}")
            if any(v <= 0 for v in levels) or any(b <= a for a, b in zip(levels, levels[1:])):
                errors.append(f"{where}: point_levels must be strictly ascending positive integers")

    root = career.get("root")
    if "root" in career and check_keys(root, f"{where} root", ["description", "effects"], [], errors):
        if "description" in root:
            check_text(root["description"], f"{where} root", "description", errors)
        if "effects" in root:
            if check_effects(root["effects"], f"{where} root", rules, cs, ctx, errors, at_least_one=True):
                check_root_budget(root, where, rules, errors)

    forks = career.get("forks")
    if "forks" not in career:
        return
    if not isinstance(forks, list):
        errors.append(f"{where}: forks must be a list")
        return
    if len(forks) != slots:
        errors.append(f"{where}: needs {slots} forks, got {len(forks)}")
    fork_slots = [f.get("slot") for f in forks if isinstance(f, dict)]
    if sorted(s for s in fork_slots if is_int(s)) != list(range(1, slots + 1)):
        errors.append(f"{where}: fork slots must be 1-{slots}, each once")

    all_options_ok = True
    for fk in forks:
        slot = fk.get("slot") if isinstance(fk, dict) else None
        wf = f"{where} slot {slot}"
        if not check_keys(fk, wf, ["slot", "name", "options"], ["max_picks"], errors):
            all_options_ok = False
            continue
        expected_tier = rules["slot_tiers"][slot - 1] if is_int(slot) and 1 <= slot <= slots else None
        check_text(fk.get("name"), wf, "name", errors)
        options = fk.get("options")
        if not isinstance(options, list) or not 2 <= len(options) <= 3:
            errors.append(f"{wf}: needs 2 or 3 options, got "
                          f"{len(options) if isinstance(options, list) else 'none'}")
            all_options_ok = False
            continue
        picks = fk.get("max_picks", 1)
        if not (is_int(picks) and 1 <= picks <= len(options)):
            errors.append(f"{wf}: max_picks must be an integer from 1 to {len(options)}")
        vus = []
        for i, option in enumerate(options):
            wo = f"{wf} option {LETTERS[i]}"
            ok = check_option(option, wo, rules, cs, ctx, errors)
            all_options_ok = all_options_ok and ok
            if ok:
                check_archetype_class(option, slot, career.get("archetype"), wo, rules, errors)
            if not ok or expected_tier is None:
                continue
            try:
                vu = option_vu(option, rules)
            except GeneratorError as exc:
                errors.append(f"{wo}: {exc}")
                all_options_ok = False
                continue
            vus.append(vu)
            check_budget(vu, expected_tier, wo, rules, errors)
        if len(vus) == len(options) and max(vus) - min(vus) > rules["fork_spread_tolerance"] + EPS:
            errors.append(f"{wf}: option VU spread {max(vus) - min(vus):.2f} exceeds "
                          f"{rules['fork_spread_tolerance']} ({', '.join(f'{v:.2f}' for v in vus)})")

    if len(errors) == start and all_options_ok:
        for name, result in analyze_caps(career, rules).items():
            if result["max"] > result["limit"] + EPS:
                errors.append(f"{where}: cap '{name}' exceeded: the worst build reaches "
                              f"{result['max']:g} against a limit of {result['limit']:g} "
                              f"({describe_build(result['build'])})")


def describe_build(build):
    text = " ".join(f"f{slot}:{''.join(letters)}" for slot, letters in sorted(build.items()) if letters)
    return text or "no picks"


def validate_spec(spec, rules, careers, cs, data):
    """Rules 1 to 7. Returns every error found; an empty list means the spec may be generated."""
    errors = []
    if not check_keys(spec, "spec", ["kingdom", "culture_ids", "careers"], [], errors):
        return errors
    if "kingdom" in spec:
        check_text(spec["kingdom"], "spec", "kingdom", errors)
    cultures = spec.get("culture_ids")
    cultures_ok = is_str_list(cultures, allow_empty=False)
    if "culture_ids" in spec and not cultures_ok:
        errors.append("spec: culture_ids must be a non-empty list of culture id strings")
    entries = spec.get("careers")
    if "careers" in spec and not (isinstance(entries, list) and entries):
        errors.append("spec: careers must be a non-empty list")
        return errors
    seen = set()
    for career in entries or []:
        cid = career.get("id") if isinstance(career, dict) else None
        if isinstance(cid, str) and cid in seen:
            errors.append(f"career '{cid}': duplicate career id in the spec")
            continue
        if isinstance(cid, str):
            seen.add(cid)
        validate_career(career, cultures if cultures_ok else None, rules, careers, cs, data, errors)
    return errors


# --------------------------------------------------------------------------- #
# Rendering                                                                    #
# --------------------------------------------------------------------------- #
def esc(text):
    return escape(text, {'"': "&quot;"})


def fmt(value):
    if isinstance(value, int):
        return str(value)
    text = f"{value:.6f}".rstrip("0").rstrip(".")
    return "0" if text in ("", "-0") else text


def effect_xml(effect, rules, cs):
    parts = [f'type="{effect["type"]}"', f'magnitude="{fmt(effect["magnitude"])}"']
    if "attack_type_mask" in effect:
        parts.append(f'attack_type_mask="{",".join(canonical(split_tokens(effect["attack_type_mask"]), cs.attack_masks))}"')
    if "troop_class" in effect:
        tokens = split_tokens(effect["troop_class"])
        names = "All" if any(t.lower() == "all" for t in tokens) else ",".join(canonical(tokens, rules["troop_classes"]))
        parts.append(f'troop_class="{names}"')
    return "<PassiveEffect " + " ".join(parts) + " />"


def choice_xml(ind, choice_id, kind, text, children):
    """One <Choice>. children is a list of child element lines (already indented relative to ind)."""
    pad = ind + " " * 8
    head = f'{ind}<Choice id="{choice_id}" type="{kind}"\n{pad}description="{{=taom_cf_{choice_id}}}{esc(text)}"'
    if not children:
        return head + " />\n"
    return head + ">\n" + "".join(children) + f"{ind}</Choice>\n"


def render_choices_body(career, info, rules, cs, ind):
    root_id = info.root_choice
    cid = career["id"]
    out = []
    root_children = [f"{ind}  {effect_xml(e, rules, cs)}\n" for e in career["root"].get("effects", [])]
    out.append(choice_xml(ind, root_id, "Passive", career["root"]["description"], root_children))
    for fk in sorted(career["forks"], key=lambda f: f["slot"]):
        gid = f"{cid}_f{fk['slot']}"
        out.append("\n")
        out.append(f'{ind}<ChoiceGroup id="{gid}" display_name="{{=taom_career_grp_{gid}}}{esc(fk["name"])}" '
                   f'career_id="{cid}" tier="{tier_of(fk, rules)}" max_picks="{fk.get("max_picks", 1)}">\n')
        for i, option in enumerate(fk["options"]):
            choice_id = f"{gid}_{LETTERS[i]}"
            inner = ind + "  "
            if "mutations" in option:
                kids = [f"{inner}  <Mutations>\n"]
                for m in option["mutations"]:
                    operation = f' operation="{m["operation"]}"' if "operation" in m else ""
                    kids.append(f'{inner}    <Mutation target_id="{info.ability}" property="{m["property"]}" '
                                f'calculator="{m["calculator"]}" value="{fmt(m["value"])}"{operation} />\n')
                kids.append(f"{inner}  </Mutations>\n")
                out.append(choice_xml(inner, choice_id, "Keystone", option["description"], kids))
            else:
                kids = [f"{inner}  {effect_xml(e, rules, cs)}\n" for e in option.get("effects", [])]
                out.append(choice_xml(inner, choice_id, "Passive", option["description"], kids))
        out.append(f"{ind}</ChoiceGroup>\n")
    return "".join(out)


def render_careers_body(career, ind):
    lines = [f"{ind}<ChoiceGroups>\n"]
    for slot in sorted(f["slot"] for f in career["forks"]):
        lines.append(f'{ind}  <Group id="{career["id"]}_f{slot}" />\n')
    lines.append(f"{ind}</ChoiceGroups>\n")
    return "".join(lines)


def levels_text(career):
    return ",".join(str(v) for v in career["point_levels"])


def normalise_mask(text):
    """The attack_type_mask an effect reads as, spelled one way: an omitted mask, All, or a list that covers
    Melee and Ranged (and names no kind) is "All"; anything else is its sorted names. Case, order and
    separators do not matter."""
    tokens = {t.lower() for t in split_tokens(text)} if text else set()
    if "all" in tokens:
        tokens = (tokens - {"all"}) | {"melee", "ranged"}
    if not tokens or tokens == {"melee", "ranged"}:
        return "All"
    return "+".join(sorted(t.capitalize() for t in tokens))


def normalise_classes(text, classes=DEFAULT_TROOP_CLASSES):
    """The troop_class an effect reads as: an omitted class, All, or every class is "All"; anything else is
    its sorted names."""
    every = {c.lower() for c in classes}
    tokens = {t.lower() for t in split_tokens(text)} if text else set()
    if "all" in tokens:
        tokens = (tokens - {"all"}) | every
    if not tokens or tokens == every:
        return "All"
    return "+".join(sorted(t.capitalize() for t in tokens))


def effect_shape(effect, classes=DEFAULT_TROOP_CLASSES):
    return (f"{effect['type']}@{normalise_mask(effect.get('attack_type_mask'))}"
            f"#{normalise_classes(effect.get('troop_class'), classes)}")


def structure_fingerprint(career, info, rules):
    """`<SHAPE_VERSION>-<short hash>` of what decides whether a saved pick is still valid: the root id, the
    point levels, per fork the group id, tier and max_picks, per option its letter and its effects as
    `type@mask#class` (normalised, see effect_shape) or its mutation properties. Not text, not magnitudes."""
    parts = [f"root={info.root_choice}", f"levels={levels_text(career)}"]
    classes = rules["troop_classes"]
    for fk in sorted(career["forks"], key=lambda f: f["slot"]):
        options = []
        for i, option in enumerate(fk["options"]):
            if "mutations" in option:
                shape = "k:" + "+".join(sorted(m["property"] for m in option["mutations"]))
            else:
                shape = "p:" + "+".join(sorted(effect_shape(e, classes) for e in option.get("effects", [])))
            options.append(f"{LETTERS[i]}={shape}")
        parts.append(f"{career['id']}_f{fk['slot']};tier={tier_of(fk, rules)};max={fk.get('max_picks', 1)};"
                     + ",".join(options))
    return f"{SHAPE_VERSION}-" + hashlib.sha256("|".join(parts).encode("utf-8")).hexdigest()[:16]


def region_hash(extra, body):
    """What the begin marker signs: the recorded attributes (the text after the career id and hash) and the body."""
    return hashlib.sha256(f"{extra}\n{body}".encode("utf-8")).hexdigest()


def render_region(cid, body, ind, extra=""):
    sha = region_hash(extra, body)
    return (f"{ind}<!-- career-forks:begin {cid} sha256={sha}{extra} -->\n{body}"
            f"{ind}<!-- career-forks:end {cid} -->")


def generated_ids(career, info):
    """(ids, loc keys) the career's region writes."""
    cid = career["id"]
    ids = {info.root_choice}
    keys = {f"taom_cf_{info.root_choice}"}
    for fk in career["forks"]:
        gid = f"{cid}_f{fk['slot']}"
        ids.add(gid)
        keys.add(f"taom_career_grp_{gid}")
        for i in range(len(fk["options"])):
            ids.add(f"{gid}_{LETTERS[i]}")
            keys.add(f"taom_cf_{gid}_{LETTERS[i]}")
    return ids, keys


# --------------------------------------------------------------------------- #
# Byte-faithful files                                                          #
# --------------------------------------------------------------------------- #
class CareerFile:
    """A career XML read byte-faithfully: BOM and newline style are remembered, the text is held
    with LF endings, and to_bytes puts both back (tools/README.md "XML I/O convention")."""

    def __init__(self, path):
        self.path = Path(path)
        self.name = self.path.name
        if not self.path.is_file():
            raise GeneratorError(f"{self.path} not found")
        raw = self.path.read_bytes()
        self.bom = raw.startswith(BOM)
        try:
            text = (raw[len(BOM):] if self.bom else raw).decode("utf-8")
        except UnicodeDecodeError as exc:
            raise GeneratorError(f"{self.name}: not valid UTF-8: {exc}")
        crlf = text.count("\r\n")
        self.newline = "\r\n" if crlf > text.count("\n") - crlf else "\n"
        self.text = text.replace("\r\n", "\n")
        if "\r" in self.text or self.text.replace("\n", self.newline) != text:
            raise GeneratorError(f"{self.name}: mixed line endings; refusing to guess which to keep")
        self.original = self.text

    def to_bytes(self, text=None):
        body = (self.text if text is None else text).replace("\n", self.newline).encode("utf-8")
        return (BOM if self.bom else b"") + body


def ensure_parses(text, name):
    try:
        ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as exc:
        raise GeneratorError(f"{name}: the output no longer parses as XML ({exc}); nothing written")


def write_atomically(files):
    """Write every changed file to <name>.gcf-new first, then rename each into place. A failure while
    writing leaves every original untouched and removes the temp files; the suffix is not .xml, so the
    localization tool's career_system/*.xml glob never sees one. A rename that fails after another file
    was already replaced writes that file's original bytes back (CareerFile.original round-trips exactly)."""
    temps = []
    try:
        for cf in files:
            tmp = cf.path.with_name(cf.path.name + TEMP_SUFFIX)
            temps.append((tmp, cf))
            tmp.write_bytes(cf.to_bytes())
        done = []
        for tmp, cf in temps:
            try:
                os.replace(tmp, cf.path)
            except OSError as exc:
                raise GeneratorError(put_back(done, cf, exc)) from exc
            done.append(cf)
    finally:
        for tmp, _ in temps:
            if tmp.exists():
                tmp.unlink()


def put_back(replaced, failed, exc):
    """Restore the files already renamed into place; returns the error text."""
    stuck = []
    for cf in replaced:
        try:
            cf.path.write_bytes(cf.to_bytes(cf.original))
        except OSError as restore_exc:
            stuck.append(f"{cf.name} ({restore_exc})")
    head = f"could not replace {failed.name} ({exc})"
    if stuck:
        return (f"{head}, and could not put back {', '.join(stuck)}: that file now holds the new text while "
                f"{failed.name} holds the old, and a re-run cannot repair it (the old group list is gone). Restore "
                f"{', '.join(cf.name for cf in replaced)} to its previous content from your version control or a "
                f"backup before running anything else")
    if replaced:
        return f"{head}; {', '.join(cf.name for cf in replaced)} was put back to its original bytes, so nothing changed"
    return f"{head}; nothing changed"


# --------------------------------------------------------------------------- #
# Regions and splicing                                                         #
# --------------------------------------------------------------------------- #
BEGIN_RE = re.compile(r"^(?P<indent>[ \t]*)<!-- career-forks:begin (?P<id>\S+) sha256=(?P<sha>[0-9a-f]{64})"
                      r"(?P<extra>(?: \w+=\S*)*) -->[ \t]*\n", re.M)
TAG_REST = re.compile(r"""(?:[^>"']|"[^"]*"|'[^']*')*>""")
GAP_OK = re.compile(r"(?:\s|<!--.*?-->)*", re.S)
LOC_PAIR = re.compile(r'\{=([^}"]+)\}([^"]*)"')

Region = namedtuple("Region", "start body_start body_end end indent sha extra body")


def find_region(text, cid, file_name):
    begins = [m for m in BEGIN_RE.finditer(text) if m.group("id") == cid]
    end_re = re.compile(rf"^[ \t]*<!-- career-forks:end {re.escape(cid)} -->", re.M)
    if not begins:
        if end_re.search(text):
            raise Refusal(f"{file_name}: career '{cid}': an end marker without a begin marker (hand-edited)")
        return None
    if len(begins) > 1:
        raise Refusal(f"{file_name}: career '{cid}': more than one begin marker (hand-edited)")
    b = begins[0]
    e = end_re.search(text, b.end())
    if not e:
        raise Refusal(f"{file_name}: career '{cid}': a begin marker without an end marker (hand-edited)")
    return Region(b.start(), b.end(), e.start(), e.end(), b.group("indent"), b.group("sha"),
                  b.group("extra"), text[b.end():e.start()])


def verify_region(region, cid, file_name):
    actual = region_hash(region.extra, region.body)
    if actual != region.sha:
        raise Refusal(f"{file_name}: career '{cid}': the generated region or its marker attributes were "
                      f"hand-edited (sha256 {actual[:12]} does not match the marker's {region.sha[:12]}). "
                      f"Revert the edit, or delete both markers to let the tool re-adopt the career")


def element_span(text, match, tag, file_name, cid):
    """[line start, end) of the element whose opening `<tag id="..."` the match found."""
    tag_end = TAG_REST.match(text, match.end())
    if not tag_end:
        raise Refusal(f"{file_name}: career '{cid}': unterminated <{tag}> tag")
    if text[tag_end.end() - 2:tag_end.end()] == "/>":
        return match.start(), tag_end.end()
    close = text.find(f"</{tag}>", tag_end.end())
    if close < 0:
        raise Refusal(f"{file_name}: career '{cid}': <{tag}> has no closing tag")
    return match.start(), close + len(f"</{tag}>")


def find_one(text, tag, element_id, file_name, cid):
    rx = re.compile(rf'^[ \t]*<{tag}\s+id="{re.escape(element_id)}"(?=[\s/>])', re.M)
    found = list(rx.finditer(text))
    if len(found) != 1:
        raise Refusal(f"{file_name}: career '{cid}': expected exactly one <{tag} id=\"{element_id}\">, "
                      f"found {len(found)}")
    return found[0]


def set_attrs(tag, items):
    """Set attributes on an opening tag, in place when present, else appended on a continuation line."""
    missing = []
    for name, value in items:
        rx = re.compile(rf'(\s{name}=")[^"]*(")')
        if rx.search(tag):
            tag = rx.sub(lambda m: m.group(1) + value + m.group(2), tag, count=1)
        else:
            missing.append(f'{name}="{value}"')
    if missing:
        cont = re.search(r"\n([ \t]+)\S", tag)
        sep = "\n" + cont.group(1) if cont else " "
        tag = tag[:-1] + sep + " ".join(missing) + ">"
    return tag


def tag_attr(tag, name):
    m = re.search(rf'\s{name}="([^"]*)"', tag)
    return m.group(1) if m else None


def as_version(value, what, cf_name, cid):
    if value is None:
        return None
    if not re.fullmatch(r"\d+", value):
        raise Refusal(f"{cf_name}: career '{cid}': {what} is '{value}', not an integer (hand-edited)")
    return int(value)


def gate_adoption(cf_name, cid, live, new):
    if new <= live:
        raise Refusal(f"{cf_name}: career '{cid}': first adoption must raise tree_version above the career's live "
                      f"{live}, but the spec says {new}; saves stored at {live} would never migrate to the new tree")


def gate_update(cf_name, cid, recorded, new, old_shape, new_shape):
    if new < recorded:
        raise Refusal(f"{cf_name}: career '{cid}': tree_version {new} is lower than the recorded {recorded}; "
                      f"a shipped tree version never goes down")
    if new_shape != old_shape and new == recorded:
        raise Refusal(f"{cf_name}: career '{cid}': the tree structure changed (shape {old_shape} -> {new_shape}) "
                      f"but tree_version is still {recorded}; raise it above {recorded} so saves migrate, "
                      f"or revert the structural edit")


def splice_careers(cf, career, info, rules):
    """Edit one <Career> in taom_careers.xml."""
    cid = career["id"]
    text = cf.text
    opens = list(re.finditer(rf'^[ \t]*<Career\s+id="{re.escape(cid)}"(?=[\s>])', text, re.M))
    if len(opens) != 1:
        raise Refusal(f"{cf.name}: career '{cid}': expected exactly one <Career>, found {len(opens)}")
    tag_end = TAG_REST.match(text, opens[0].end())
    if not tag_end or text[tag_end.end() - 2:tag_end.end()] == "/>":
        raise Refusal(f"{cf.name}: career '{cid}': <Career> must be an open tag with children")
    close = text.find("</Career>", tag_end.end())
    if close < 0:
        raise Refusal(f"{cf.name}: career '{cid}': <Career> has no closing tag")
    close += len("</Career>")
    tag = text[opens[0].start():tag_end.end()]
    block = text[tag_end.end():close]

    version, levels = str(career["tree_version"]), levels_text(career)
    shape = structure_fingerprint(career, info, rules)
    region = find_region(block, cid, cf.name)
    if region:
        verify_region(region, cid, cf.name)
        recorded = dict(re.findall(r"(\w+)=(\S*)", region.extra))
        for name in ("tree_version", "point_levels"):
            live = tag_attr(tag, name)
            if name not in recorded or live != recorded[name]:
                raise Refusal(f"{cf.name}: career '{cid}': attribute {name} is '{live}' but the marker "
                              f"recorded '{recorded.get(name)}' (hand-edited)")
        if "shape" not in recorded:
            raise Refusal(f"{cf.name}: career '{cid}': the marker records no shape fingerprint (hand-edited, or "
                          f"written by an older generator); delete both markers to re-adopt the career")
        if not recorded["shape"].startswith(f"{SHAPE_VERSION}-"):
            raise Refusal(f"{cf.name}: career '{cid}': the marker's shape '{recorded['shape']}' comes from another "
                          f"shape definition (this tool writes {SHAPE_VERSION}-...), so the tree structure cannot "
                          f"be compared; delete both markers to re-adopt the career, or run the tool version "
                          f"that wrote it")
        old_version = as_version(recorded["tree_version"], "the recorded tree_version", cf.name, cid)
        gate_update(cf.name, cid, old_version, career["tree_version"], recorded["shape"], shape)
        indent = region.indent
        span = (region.start, region.end)
        status, detail = "update", f"tree_version {old_version} -> {version}"
    else:
        found = list(re.finditer(r"^([ \t]*)<ChoiceGroups\b[^>]*>.*?</ChoiceGroups>", block, re.M | re.S))
        if len(found) != 1:
            raise Refusal(f"{cf.name}: career '{cid}': expected exactly one <ChoiceGroups>, found {len(found)}")
        live = as_version(tag_attr(tag, "tree_version"), "the live tree_version", cf.name, cid) or 1
        gate_adoption(cf.name, cid, live, career["tree_version"])
        indent = found[0].group(1)
        span = (found[0].start(), found[0].end())
        status, detail = "adopt", f"tree_version {live} -> {version}"

    new_region = render_region(cid, render_careers_body(career, indent), indent,
                               f" tree_version={version} point_levels={levels} shape={shape}")
    new_block = block[:span[0]] + new_region + block[span[1]:]
    new_tag = set_attrs(tag, [("tree_version", version), ("point_levels", levels)])
    new_text = text[:opens[0].start()] + new_tag + new_block + text[close:]
    if new_text == text:
        return Splice("unchanged", "", [])
    cf.text = new_text
    return Splice(status, detail, [])


def strip_xml_comments(text):
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def tree_counts(text):
    """(groups, choices outside the root) of a root plus groups span."""
    clean = strip_xml_comments(text)
    return len(re.findall(r"<ChoiceGroup\b", clean)), max(len(re.findall(r"<Choice\b", clean)) - 1, 0)


def english_changes(old_text, new_text):
    """(key, old English, new English) for every loc key present in both whose text differs."""
    def pairs(text):
        return {k: unescape(v, {"&quot;": '"'}) for k, v in LOC_PAIR.findall(strip_xml_comments(text))}
    old, new = pairs(old_text), pairs(new_text)
    return [(k, old[k], new[k]) for k in new if k in old and old[k] != new[k]]


def loc_keys(text):
    return {key for key, _ in LOC_PAIR.findall(strip_xml_comments(text))}


def number_text(text):
    """A numeric attribute spelled one way (0.050 and 0.05 agree); anything that is not a number stays as written."""
    try:
        return repr(float(text))
    except (TypeError, ValueError):
        return text or ""


def choice_effects(text):
    """choice id -> (raw description attribute, readable effect list, meaning) for every <Choice> in a generated
    span. The list keeps the written order and spelling for printing; the meaning is the sorted multiset the
    effects amount to (mask and class normalised, so a respelling or a reorder is no change). A mutation's
    operation is left out: it is parsed and never consumed, so respelling it changes nothing. Its calculator is
    in, because the calculator decides the arithmetic."""
    try:
        root = ET.fromstring(f"<r>{text}</r>".encode("utf-8"))
    except ET.ParseError as exc:
        raise GeneratorError(f"a generated region does not parse as XML ({exc})")
    found = {}
    for choice in root.iter("Choice"):
        effects, meaning = [], []
        for el in choice.iter():
            if el.tag == "PassiveEffect":
                label = f"{el.get('type')} {el.get('magnitude')}"
                extras = [el.get(a) for a in ("attack_type_mask", "troop_class") if el.get(a)]
                effects.append(label + (f" ({', '.join(extras)})" if extras else ""))
                meaning.append(("effect", el.get("type") or "", number_text(el.get("magnitude")),
                                normalise_mask(el.get("attack_type_mask")), normalise_classes(el.get("troop_class"))))
            elif el.tag == "Mutation":
                effects.append(f"{el.get('property')} {el.get('calculator')} {el.get('value')}")
                meaning.append(("mutation", el.get("property") or "", el.get("calculator") or "",
                                number_text(el.get("value")), ""))
        found[choice.get("id")] = (choice.get("description"), effects, sorted(meaning))
    return found


def stale_tooltips(old_text, new_text):
    """(choice id, old effects, new effects, description) for each option present in both whose effects changed
    in meaning while its description attribute (key and English) stayed the same: the tooltip may state an old
    number. Needing the same id and the same description is what keeps a first adoption quiet: the old tree
    keeps neither, while a re-adoption over a generated tree keeps both."""
    old, new = choice_effects(strip_xml_comments(old_text)), choice_effects(strip_xml_comments(new_text))
    return [(cid, old[cid][1], new[cid][1], new[cid][0]) for cid in new
            if cid in old and old[cid][2] != new[cid][2] and old[cid][0] == new[cid][0]]


def describe_tree(status, counts):
    groups, choices = counts
    return f"root + {groups} {'forks' if status == 'update' else 'groups'} ({choices} choices)"


def splice_choices(cf, career, info, old_groups, rules, cs):
    cid = career["id"]
    text = cf.text
    region = find_region(text, cid, cf.name)
    if region:
        verify_region(region, cid, cf.name)
        indent, span, status = region.indent, (region.start, region.end), "update"
    else:
        if not old_groups:
            raise Refusal(f"{cf.name}: career '{cid}': taom_careers.xml lists no groups to replace")
        spans = []
        root = find_one(text, "Choice", info.root_choice, cf.name, cid)
        spans.append((element_span(text, root, "Choice", cf.name, cid), info.root_choice))
        for gid in old_groups:
            m = find_one(text, "ChoiceGroup", gid, cf.name, cid)
            spans.append((element_span(text, m, "ChoiceGroup", cf.name, cid), gid))
        spans.sort(key=lambda s: s[0][0])
        for (a, a_id), (b, b_id) in zip(spans, spans[1:]):
            gap = text[a[1]:b[0]]
            if b[0] < a[1] or not GAP_OK.fullmatch(gap):
                raise Refusal(f"{cf.name}: career '{cid}': the old root and groups are not contiguous "
                              f"('{a_id}' to '{b_id}' has other content between them: "
                              f"{gap.strip()[:60]!r})")
        indent = re.match(r"[ \t]*", text[root.start():]).group(0)
        span = (spans[0][0][0], spans[-1][0][1])
        status = "adopt"

    new_body = render_choices_body(career, info, rules, cs, indent)
    new_region = render_region(cid, new_body, indent)
    old_text = text[span[0]:span[1]]
    new_text = text[:span[0]] + new_region + text[span[1]:]
    if new_text == text:
        return Splice("unchanged", "", [])
    cf.text = new_text
    detail = (f"{describe_tree(status, tree_counts(old_text))} -> "
              f"{describe_tree('update', tree_counts(new_body))}")
    return Splice(status, detail, english_changes(old_text, new_body),
                  sorted(loc_keys(new_body) - loc_keys(old_text)), stale_tooltips(old_text, new_body),
                  sorted(loc_keys(old_text) - loc_keys(new_body)))


def strip_regions(text, cids):
    for cid in cids:
        while True:
            region = None
            for m in BEGIN_RE.finditer(text):
                if m.group("id") == cid:
                    region = find_region(text, cid, "")
                    break
            if not region:
                break
            text = text[:region.start] + text[region.end:]
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def find_collisions(files, spec_careers, infos):
    """Rule 8: generated ids and loc keys against everything else already in the two files."""
    ids, keys = set(), set()
    for career in spec_careers:
        c_ids, c_keys = generated_ids(career, infos[career["id"]])
        ids |= c_ids
        keys |= c_keys
    errors = []
    cids = [c["id"] for c in spec_careers]
    for cf in files:
        rest = strip_regions(cf.text, cids)
        for found in set(re.findall(r'(?<![\w-])id="([^"]*)"', rest)) & ids:
            errors.append(f"generated id '{found}' collides with an existing id in {cf.name}")
        for found in set(re.findall(r"\{=([^}]+)\}", rest)) & keys:
            errors.append(f"generated loc key '{found}' collides with an existing key in {cf.name}")
    return sorted(errors)


def verify_output(careers_text, choices_text, spec_careers):
    """Parse both outputs and check each generated career is reachable the way the C# loader reads it."""
    problems = []
    careers = ET.fromstring(careers_text.encode("utf-8"))
    choices = ET.fromstring(choices_text.encode("utf-8"))
    by_group = {g.get("id"): g for g in choices.findall("ChoiceGroup")}
    top_choices = {c.get("id") for c in choices.findall("Choice")}
    for career in spec_careers:
        cid = career["id"]
        el = [c for c in careers.iter("Career") if c.get("id") == cid]
        if len(el) != 1:
            problems.append(f"{cid}: Career element not found after generation")
            continue
        want = [f"{cid}_f{f['slot']}" for f in sorted(career["forks"], key=lambda f: f["slot"])]
        if [g.get("id") for g in el[0].findall("./ChoiceGroups/Group")] != want:
            problems.append(f"{cid}: ChoiceGroups list is not the six fork groups")
        if el[0].get("tree_version") != str(career["tree_version"]):
            problems.append(f"{cid}: tree_version attribute is missing or wrong")
        if el[0].get("point_levels") != levels_text(career):
            problems.append(f"{cid}: point_levels attribute is missing or wrong")
        if el[0].get("root_choice_id") not in top_choices:
            problems.append(f"{cid}: root choice '{el[0].get('root_choice_id')}' is missing")
        for fk in career["forks"]:
            group = by_group.get(f"{cid}_f{fk['slot']}")
            if group is None or group.get("career_id") != cid:
                problems.append(f"{cid}: group f{fk['slot']} missing from the choices file")
            elif len(group.findall("Choice")) != len(fk["options"]):
                problems.append(f"{cid}: group f{fk['slot']} has the wrong number of choices")
    return problems


# --------------------------------------------------------------------------- #
# Review table                                                                 #
# --------------------------------------------------------------------------- #
def split_camel(name):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", name).lower()


def signed(value):
    text = f"{round(value, 4):g}"
    return text if text.startswith("-") else "+" + text


def effect_words(effect, flat):
    amount = signed(effect["magnitude"]) if effect["type"] in flat else signed(effect["magnitude"] * 100) + "%"
    words = f"{amount} {split_camel(effect['type'])}"
    if "attack_type_mask" in effect:
        words += f" ({effect['attack_type_mask'].lower().replace('|', ',')})"
    if "troop_class" in effect:
        words += f" [{effect['troop_class'].lower()}]"
    return words


def option_words(option, flat):
    if "mutations" in option:
        return "Keystone: " + "; ".join(f"{m['property']} {signed(m['value'])}" for m in option["mutations"])
    return "; ".join(effect_words(e, flat) for e in option.get("effects", [])) or "(none)"


def cell(text):
    return str(text).replace("|", "\\|")


def render_table(spec, rules, analyses, spec_name, flat):
    out = [f"# Career fork review table: {spec['kingdom']}", "",
           f"Generated from `tools/career_design/{spec_name}.json` with the rules in `_rules.json`. "
           f"Every balance number is a draft; approve or change them here before the first apply.",
           "", "VU means value units: the benefit worth one unit is the `vu_unit` of the effect type (a cost "
           "type's benefit is its negative magnitude). "
           "Class-only troop effects are worth 1/%s as much per unit." % fmt(rules["class_bonus"]), ""]
    for career in spec["careers"]:
        out += [f"## {career['id']} ({career['archetype']})", "",
                f"Tree version {career['tree_version']}, a point at levels {levels_text(career).replace(',', ', ')}.",
                "", f"**Root:** {career['root']['description']} "
                    f"({'; '.join(effect_words(e, flat) for e in career['root'].get('effects', [])) or 'no effects'}), "
                    f"{option_vu(career['root'], rules):.1f} VU of at most "
                    f"{fmt(rules['root_budget'] + rules['budget_tolerance'])} "
                    f"(root_budget {fmt(rules['root_budget'])} plus budget_tolerance {fmt(rules['budget_tolerance'])})",
                "", "| Fork | Tier | Option | Effects | VU | Description |", "|---|---|---|---|---|---|"]
        for fk in sorted(career["forks"], key=lambda f: f["slot"]):
            for i, option in enumerate(fk["options"]):
                label = f"{fk['slot']} {fk['name']}" if i == 0 else ""
                if fk.get("max_picks", 1) > 1 and i == 0:
                    label += f" (up to {fk['max_picks']} picks)"
                out.append("| " + " | ".join([
                    cell(label), str(tier_of(fk, rules)) if i == 0 else "", cell(f"{LETTERS[i]} {option['name']}"),
                    cell(option_words(option, flat)), f"{option_vu(option, rules):.1f}",
                    cell(option["description"])]) + " |")
        out += ["", "### Caps", "", "| Cap | Highest any build reaches | Limit | Headroom |", "|---|---|---|---|"]
        analysis = analyses[career["id"]]
        for name, result in analysis.items():
            out.append(f"| {name} | {result['max']:g} | {result['limit']:g} | "
                       f"{result['limit'] - result['max']:g} |")
        out.append("")
        for name, result in analysis.items():
            if result["max"] > 0:
                out.append(f"- Worst build for {name}: {describe_build(result['build'])}")
        out.append("")
    return "\n".join(out)


# --------------------------------------------------------------------------- #
# Run                                                                          #
# --------------------------------------------------------------------------- #
def transform(files, spec, rules, cs, infos, old_groups):
    """Apply every career to the in-memory texts. Returns (results, refusals)."""
    results, refusals = [], []
    careers_file, choices_file = files
    for career in spec["careers"]:
        info = infos[career["id"]]
        for cf, step in ((careers_file, lambda: splice_careers(careers_file, career, info, rules)),
                         (choices_file, lambda: splice_choices(choices_file, career, info,
                                                               old_groups[career["id"]], rules, cs))):
            try:
                results.append((career["id"], cf.name, step()))
            except Refusal as exc:
                refusals.append(str(exc))
    return results, refusals


def print_english_changes(results):
    changed = [(cid, k, old, new) for cid, _, sp in results for k, old, new in sp.changed]
    if not changed:
        return
    print("English changed under an existing key. The translation pipeline never re-translates a row whose key "
          "is unchanged, so all 12 languages keep the old wording until each row is redone; follow the checklist "
          f"in {GUIDE} (\"The checklist when you edit an existing English string\"):")
    for cid, key, old, new in changed:
        print(f"  {cid}: {key}: \"{old}\" -> \"{new}\"")


def print_stale_tooltips(results):
    stale = [(cid, option, old, new, text) for cid, _, sp in results for option, old, new, text in sp.stale]
    if not stale:
        return
    print("warning: these options changed their effects but their description reads unchanged, so the tooltip may "
          "state an old number; edit the text, or confirm it is still true:")
    for cid, option, old, new, text in stale:
        print(f"  {cid}: {option}: {'; '.join(old)} -> {'; '.join(new)}; description {text!r}")


CAREER_DATA_PREFIX = "std_taom_career_data_strings_"


def language_rows(languages_dir, keys):
    """key -> sorted places that already hold a translation row for it: a language folder (its career data file)
    or "<lang> in <file>" (another std_taom_*.xml of that language). The seeding step fills only missing ids, and
    translate_with_claude skips any key a sibling std_taom file carries, so a row that survives a removal revives
    its old translation. A commented-out row is not a row (the seeder reads the XML)."""
    folder = Path(languages_dir)
    if not folder.is_dir():
        raise GeneratorError(f"Languages folder not found: {folder} (needed to check the added loc keys against "
                             f"the existing translation rows)")
    files = sorted(folder.glob("*/std_taom_*.xml"))
    if not any(path.name.startswith(CAREER_DATA_PREFIX) for path in files):
        raise GeneratorError(f"no {CAREER_DATA_PREFIX}*.xml under {folder}: the added loc keys cannot be "
                             f"checked against the existing translation rows")
    found = {}
    for path in files:
        try:
            text = strip_xml_comments(path.read_text(encoding="utf-8-sig"))
        except UnicodeDecodeError as exc:
            raise GeneratorError(f"{path.name}: not valid UTF-8: {exc}")
        place = path.parent.name if path.name.startswith(CAREER_DATA_PREFIX) else f"{path.parent.name} in {path.name}"
        for key in set(keys) & set(re.findall(r'<string\s+id="([^"]*)"', text)):
            found.setdefault(key, []).append(place)
    return {key: sorted(places) for key, places in found.items()}


def print_revived_rows(revived):
    if not revived:
        return
    print("warning: these added loc keys already have a row in the language files (any std_taom_*.xml of a "
          "language). The seeding step fills only missing ids, so each row keeps its old translation (a removed "
          "option letter that comes back revives the old wording). Reset the rows before seeding, as the checklist in "
          f"{GUIDE} (\"The checklist when you edit an existing English string\") says: write the new English into "
          "every listed row, drop the key from tools/translation_cache/<lang>.json, and check "
          "tools/translation_overrides/<lang>.json:")
    for key, langs in sorted(revived.items()):
        print(f"  {key} ({', '.join(langs)})")


CSHARP_TESTS = ["CareerChoicesIntegrationTests", "CareerChoiceIntegrityTests", "CareerConfigProviderTests",
                "LanguageFileCoverageTests"]
CSHARP_TEST_FILTER = '--filter "' + "|".join(f"FullyQualifiedName~{name}" for name in CSHARP_TESTS) + '"'


def print_next_steps(results, revived):
    """The localization steps, in commands that run unchanged in bash and PowerShell. The English source needs the
    strings tool whenever a loc key was added, re-worded or removed (rows reset to new English would otherwise
    differ from a stale source and read as translated). The language files only need new rows for an added key
    that has none yet: `revived` holds the added keys that do, which print_revived_rows covers. A run that edited
    numbers alone prints the tests step (print_english_changes covers the changed English)."""
    added = sorted({key for _, _, sp in results for key in sp.added})
    fresh = [key for key in added if key not in revived]
    touched = bool(added) or any(sp.changed or sp.removed for _, _, sp in results)
    steps = []
    print(f"Next steps ({len(fresh)} new loc keys exist in no language file yet):" if fresh else "Next steps:")
    if touched:
        steps.append("python tools/generate_name_localization_strings.py --apply --category career_data")
    if fresh:
        steps.append("seed the English rows into every language file, one command each (free: it writes "
                     "English rows only and never builds an API client; a paid translation run needs the "
                     "maintainer's word):\n"
                     + "\n".join(f"       python tools/translate_with_claude.py --lang {lang} --module TAOM --sync-ids --seed-only --apply"
                                 for lang in TRANSLATOR_LANGS))
    steps.append("python -m unittest discover -s tools/tests -t .")
    steps.append("dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= " + CSHARP_TEST_FILTER
                 + (" (LanguageFileCoverageTests stays red until the seeding step)" if fresh else ""))
    for number, step in enumerate(steps, 1):
        print(f"  {number}. {step}")


def run(args):
    design_dir = Path(args.design_dir)
    rules = read_json(design_dir / "_rules.json", "rules file")
    check_rules(rules)
    spec = read_json(design_dir / f"{args.spec}.json", f"spec {args.spec}.json")
    career_dir = Path(args.career_dir)
    careers_file = CareerFile(career_dir / CAREERS_NAME)
    choices_file = CareerFile(career_dir / CHOICES_NAME)
    try:
        infos = load_career_info(careers_file.text)
    except ET.ParseError as exc:
        raise GeneratorError(f"{careers_file.name}: does not parse as XML ({exc})")
    cs = load_cs_sets(args.system_dir)
    rule_problems = check_rules_against_cs(rules, cs)
    if rule_problems:
        raise GeneratorError("; ".join(rule_problems))
    data = load_resource_data(args.resources_dir)

    errors = validate_spec(spec, rules, infos, cs, data)
    if errors:
        for e in errors:
            print(f"error: {e}", file=sys.stderr)
        print(f"{len(errors)} error(s) in {args.spec}.json; nothing written", file=sys.stderr)
        return 2

    old_groups = {c["id"]: list(infos[c["id"]].groups) for c in spec["careers"]}
    files = (careers_file, choices_file)
    results, refusals = transform(files, spec, rules, cs, infos, old_groups)

    problems = [] if refusals else find_collisions(files, spec["careers"], infos)
    if not problems and not refusals:
        for cf in files:
            ensure_parses(cf.text, cf.name)
        problems = verify_output(careers_file.text, choices_file.text, spec["careers"])
    if problems:
        for p in problems:
            print(f"error: {p}", file=sys.stderr)
        print(f"{len(problems)} problem(s); nothing written", file=sys.stderr)
        return 2

    added = sorted({key for _, _, sp in results for key in sp.added})
    revived = language_rows(args.languages_dir, added) if added else {}

    analyses = {c["id"]: analyze_caps(c, rules) for c in spec["careers"]}
    print(f"spec {args.spec}: {spec['kingdom']}, {len(spec['careers'])} career(s)")
    for cid, name, sp in results:
        print(f"  {cid}: {name} {sp.status}" + (f": {sp.detail}" if sp.detail else ""))
    for career in spec["careers"]:
        reached = ", ".join(f"{n} {r['max']:g}/{r['limit']:g}" for n, r in analyses[career["id"]].items()
                            if r["max"] > 0)
        print(f"  {career['id']}: caps {reached or 'none reached'}")
    print_english_changes(results)
    print_stale_tooltips(results)
    print_revived_rows(revived)

    if args.table:
        table_path = Path(args.table)
        table_path.parent.mkdir(parents=True, exist_ok=True)
        table_path.write_bytes(render_table(spec, rules, analyses, args.spec, cs.flat).encode("utf-8"))
        print(f"review table written: {table_path}")

    if refusals:
        for r in refusals:
            print(f"refused: {r}", file=sys.stderr)
        print("generated regions or old trees are not as expected; nothing written", file=sys.stderr)
        return 1 if args.check else 2

    changed = [cf for cf in files if cf.text != cf.original]
    if args.check:
        if changed:
            print("DRIFT: " + ", ".join(cf.name for cf in changed) + " differ from the spec")
            return 1
        print("OK: the career XML matches the spec")
        return 0
    if not changed:
        print("unchanged: nothing to write")
        return 0
    if not args.apply:
        print("Dry run: nothing written; would change " + ", ".join(cf.name for cf in changed)
              + ". Re-run with --apply to write.")
        return 0
    write_atomically(changed)
    print("applied: " + ", ".join(cf.name for cf in changed))
    print_next_steps(results, revived)
    return 0


def build_parser():
    ap = argparse.ArgumentParser(description="Generate one kingdom's forked career trees from a JSON spec.")
    ap.add_argument("spec", help="spec name: tools/career_design/<spec>.json")
    ap.add_argument("--apply", action="store_true", help="write the career XML (default: dry run)")
    ap.add_argument("--check", action="store_true", help="exit 1 when the XML differs from the spec; no writes")
    ap.add_argument("--table", metavar="PATH", help="write a Markdown review table")
    ap.add_argument("--career-dir", default=str(CAREER_DIR), help=argparse.SUPPRESS)
    ap.add_argument("--design-dir", default=str(DESIGN_DIR), help=argparse.SUPPRESS)
    ap.add_argument("--system-dir", default=str(SYSTEM_DIR), help=argparse.SUPPRESS)
    ap.add_argument("--resources-dir", default=str(RESOURCE_DIR), help=argparse.SUPPRESS)
    ap.add_argument("--languages-dir", default=str(LANGUAGES_DIR), help=argparse.SUPPRESS)
    return ap


def main(argv=None):
    args = build_parser().parse_args(argv)
    if args.apply and args.check:
        print("error: --apply and --check are mutually exclusive", file=sys.stderr)
        return 2
    try:
        return run(args)
    except GeneratorError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:  # exit 1 means drift; an unexpected failure must never look like it
        traceback.print_exc()
        print(f"error: unexpected {type(exc).__name__}: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
