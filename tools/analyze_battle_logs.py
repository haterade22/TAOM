#!/usr/bin/env python3
"""
Auto-resolve battle log analyzer (READ-ONLY).

Consumes the battle records emitted by the AutoResolveDiagnostics feature (one per completed map
battle) and answers the questions that decide how the counter matrix, race matrix, culture
multipliers and the two exponents get tuned.

Records are written through TAOM's shared FileLogger, so they live inside
    <game>/bin/Win64_Shipping_Client/Logs/taom_debug_*.log
tagged "[AutoResolve]" behind the usual "[timestamp] [INFO] " prefix, which this tool strips.
Untagged lines are the rest of the mod's logging and are skipped.

The log deliberately stores RAW data only — troop ids and counts. Every derived value (tier, class,
race group, power, skill residual) is computed here against Main/_Module/ModuleData/troops/*.xml,
so the analysis can change without a rebuild or a second play session.

When the log carries a troop census (v4+), the engine's OWN tier/power/formation values are used in
preference to anything derived here, and census entries supplement troops_*.xml for the looters,
villagers, caravan guards and armed traders that fight in these battles but are defined elsewhere.

What it reports:
  1. Composition divergence  — do real mid-campaign rosters differ more than the ~7 points per
                               class that starting party templates show? Decides whether the
                               counter matrix is balance or merely texture.
  2. Army sizes              — the real numbers on both sides, which calibrates CountExponent.
  3. Outcome lopsidedness    — winner survivor fraction, rounds, and how battles end. Measures the
                               threshold problem in practice rather than in the model.
  4. Matchup frequency       — which cultures actually meet, so tuning targets real matchups.
  5. Loss asymmetry by class — which classes actually die and to whom, the empirical check on any
                               counter values we ship.
  6. Sieges                  — reported separately, because GetSettlementAdvantage (3.6-6.0
                               measured) dwarfs every troop-quality term and pooling them with
                               field battles makes both unreadable.
  7. Cross-checks            — schema contract in both directions, and fielded rosters against each
                               side's independently-recorded menStart. Both are hard gates: two
                               successive versions of the logger measured winners only, and this is
                               what caught them.

And, most usefully, `--replay`: re-runs the REAL logged rosters through a faithful implementation
of the engine's simulation loop under candidate knob settings, so a proposed matrix is scored
against armies that actually fought instead of against templates.

Usage:
    python analyze_battle_logs.py                      # summary to stdout + HTML report
    python analyze_battle_logs.py --log <path>         # explicit log path
    python analyze_battle_logs.py --stdout             # summary only, no files written
    python analyze_battle_logs.py --min-men 100        # ignore skirmishes (default 40)
    python analyze_battle_logs.py --no-player          # drop player-involved battles
    python analyze_battle_logs.py --replay             # replay real rosters under candidate knobs
"""

import argparse
import glob
import json
import math
import os
import random
import re
import statistics
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
TROOPS_GLOB = os.path.join(REPO_ROOT, 'Main', '_Module', 'ModuleData', 'troops', 'troops_*.xml')
REPORT_DIR = os.path.join(REPO_ROOT, 'tools', 'reports', 'battle-logs')

# The feature writes through TAOM's shared FileLogger rather than a dedicated file, so it inherits
# rotation, synchronous INFO durability and crash-bundle inclusion for free. That means records live
# inside taom_debug_*.log behind a "[timestamp] [INFO] [AutoResolve] " prefix, which load_records
# strips. Bannerlord's CWD is its binaries folder, so Logs/ sits alongside the executable.
LOG_TAG = '[AutoResolve]'
CENSUS_TAG = '[AutoResolveCensus]'
DEFAULT_GAME_DIR = os.environ.get(
    'BANNERLORD_GAME_DIR',
    r'E:\Steam\steamapps\common\Mount & Blade II Bannerlord')
DEFAULT_LOG_GLOB = os.path.join(
    DEFAULT_GAME_DIR, 'bin', 'Win64_Shipping_Client', 'Logs', 'taom_debug_*.log')

SKILLS = ['OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing', 'Riding', 'Athletics']

# Offline troop classification. The SHIPPING classifier resolves ItemObject.WeaponClass; this is a
# deliberate id-token proxy for analysis only, and any disagreement between the two is itself a
# finding worth reporting.
POLEARM_TOKENS = re.compile(r'spear|pike|polearm|halberd|glaive|lance')

RACE_GROUPS = {
    'orc': 'Orc', 'goblin': 'Goblin', 'dwarf': 'Dwarf', 'elf': 'Elf',
    'uruk_hai': 'Uruk', 'uruk': 'Uruk', 'pale_uruk': 'Uruk', 'dg_uruk': 'Uruk',
    'berserker': 'Orc', 'cave_troll': 'Orc',
}

CLASSES = ['Sword', 'Pike', 'Archer', 'Cavalry']

# TAOM's shipped tier->power table (battle_balance_config.json), used only when the log carries no
# census. When a census IS present its measured per-troop power wins — see TroopData.adopt_census.
SHIPPED_TIER_POWER = {0: 0.40, 1: 0.66, 2: 0.96, 3: 1.30, 4: 1.68, 5: 2.10,
                      6: 2.56, 7: 2.91, 8: 3.26, 9: 3.61, 10: 3.96}
# TaomMilitaryPowerModel multiplies mounted troops by this. Omitting it understated every mounted
# troop by 20% — caught by the first census, which disagreed on exactly the 146 mounted troops and
# on nothing else.
MOUNTED_MULTIPLIER = 1.2
MAX_TIER = 10


# ---------------------------------------------------------------- troop data

class TroopData:
    """Everything derivable from a troop id, loaded once from the shipped XML."""

    def __init__(self):
        self.level = {}
        self.cls = {}
        self.race = {}
        self.skill_sum = {}
        self.culture_file = {}
        self._tier_median = {}
        # Engine ground truth, populated from a census when the log has one.
        self.census_power = {}
        self.census_formation = {}
        self.census_hp = {}

        for path in sorted(glob.glob(TROOPS_GLOB)):
            src = os.path.basename(path)[len('troops_'):-len('.xml')]
            try:
                root = ET.parse(path).getroot()
            except ET.ParseError as exc:
                print(f'  ! could not parse {src}: {exc}', file=sys.stderr)
                continue
            for node in root.iter('NPCCharacter'):
                level = node.get('level')
                if not level:
                    continue
                tid = node.get('id')
                self.level[tid] = int(level)
                self.cls[tid] = self._classify(node)
                self.race[tid] = RACE_GROUPS.get(node.get('race', ''), 'Human')
                skills = {s.get('id'): int(s.get('value', 0)) for s in node.iter('skill')}
                self.skill_sum[tid] = sum(skills.get(k, 0) for k in SKILLS)
                self.culture_file[tid] = src

        by_tier = defaultdict(list)
        for tid, lvl in self.level.items():
            by_tier[self.tier(tid)].append(self.skill_sum[tid])
        self._tier_median = {t: statistics.median(v) for t, v in by_tier.items() if v}

    @staticmethod
    def _classify(node):
        group = node.get('default_group', 'Infantry')
        items = [e.get('id', '') for e in node.iter('equipment')
                 if (e.get('slot') or '').startswith('Item')]
        if group in ('Cavalry', 'HorseArcher'):
            return 'Cavalry'
        if group == 'Ranged':
            return 'Archer'
        return 'Pike' if any(POLEARM_TOKENS.search(i) for i in items) else 'Sword'

    def tier(self, tid):
        lvl = self.level.get(tid)
        if lvl is None:
            return 0
        return max(0, min(MAX_TIER, math.ceil((lvl - 5) / 5)))

    def skill_residual(self, tid, lo=0.80, hi=1.30):
        median = self._tier_median.get(self.tier(tid)) or 0
        if median <= 0 or tid not in self.skill_sum:
            return 1.0
        return max(lo, min(hi, self.skill_sum[tid] / median))

    def known(self, tid):
        # The census supplements troops_*.xml with everything else the engine fields — looters,
        # villagers, caravan guards, armed traders. Without it ~7% of every army was silently
        # dropped from composition, which is a bias, not a rounding error.
        return tid in self.level or tid in self.census_power

    def base_power(self, tid):
        """Simulated power for one troop.

        Prefers the engine's own measured value when a census is available; falls back to the
        shipped tier table plus the mounted multiplier otherwise. Measured beats derived.
        """
        if tid in self.census_power:
            return self.census_power[tid]
        power = SHIPPED_TIER_POWER[self.tier(tid)]
        if self.cls.get(tid) == 'Cavalry':
            power *= MOUNTED_MULTIPLIER
        return power

    def adopt_census(self, census):
        """Replace derived values with the engine's, and report where the two disagreed."""
        agree_tier = disagree_tier = 0
        for rec in census:
            tid = rec.get('id')
            if not tid or rec.get('hero'):
                continue
            was_known = tid in self.level
            self.census_power[tid] = rec.get('power', 0.0)
            if tid not in self.level:
                # Not in troops_*.xml at all (vanilla or another module). Take everything from the
                # census so these men are analysed rather than discarded.
                self.level[tid] = rec.get('level', 0)
                self.cls[tid] = {'Cavalry': 'Cavalry', 'HorseArcher': 'Cavalry',
                                 'Ranged': 'Archer'}.get(rec.get('formation'), 'Sword')
                self.race[tid] = 'Human'
                self.skill_sum[tid] = 0
            self.census_formation[tid] = rec.get('formation')
            self.census_hp[tid] = rec.get('hp')
            if not was_known:
                continue                      # nothing offline to compare against
            if rec.get('tier') == self.tier(tid):
                agree_tier += 1
            else:
                disagree_tier += 1
        return agree_tier, disagree_tier


# ---------------------------------------------------------------- log loading

def load_records(paths, min_men, drop_player, keep_player_fought=False):
    """Read battle records out of the shared TAOM log.

    Records are written through FileLogger, so each line looks like
        [2026-08-08 14:32:01] [INFO] [AutoResolve] {"v":1,...}
    Non-tagged lines are the rest of the mod's logging and are skipped silently — they are not
    malformed, they are simply not ours. Only a tagged line that fails to parse counts as malformed,
    which is normally just the final line when the game exited mid-write.
    """
    records, skipped, malformed, unsupported = [], 0, 0, Counter()
    for path in paths:
        # errors='replace': a hard crash can leave a partial multi-byte sequence at the tail, and
        # one bad byte must not cost the whole file.
        with open(path, 'r', encoding='utf-8', errors='replace') as handle:
            for line in handle:
                if LOG_TAG not in line:
                    continue
                brace = line.find('{')
                if brace < 0:
                    malformed += 1
                    continue
                try:
                    rec = json.loads(line[brace:].strip())
                except json.JSONDecodeError:
                    malformed += 1
                    continue
                sides = rec.get('sides') or {}
                if 'attacker' not in sides or 'defender' not in sides:
                    malformed += 1
                    continue
                # The version gate, ENFORCED. v1-v4 read composition from a roster the engine had
                # already stripped, so a losing side came back a median 55% short; blending one of
                # those rotated logs into the corpus reproduces the exact survivorship bias this
                # tool exists to detect. It must be a drop, not a warning — the bad records look
                # perfectly well-formed. (This constant sat unreferenced until 2026-08-08, so the
                # refusal the C# doc comment promised was never actually happening.)
                version = rec.get('v')
                if version not in SUPPORTED_VERSIONS:
                    unsupported[version] += 1
                    continue
                if drop_player and rec.get('player'):
                    skipped += 1
                    continue
                # A player battle the player FOUGHT is a mission result, not an auto-resolve
                # sample: its casualties come from a live battle, not from SimulateHit. Including
                # it in a corpus used to tune auto-resolve is measuring the wrong mechanism.
                # MapEvent.IsPlayerSimulation is true only when the player auto-resolved, so
                # player && !playerSimulated is exactly the set to drop.
                if (not keep_player_fought and rec.get('player')
                        and not rec.get('playerSimulated')):
                    skipped += 1
                    continue
                smallest = min(sides['attacker'].get('menStart', 0),
                               sides['defender'].get('menStart', 0))
                if smallest < min_men:
                    skipped += 1
                    continue
                records.append(rec)
    return records, skipped, malformed, unsupported


# The contract with Main/Features/AutoResolveDiagnostics/Domain/BattleLogRecord.cs. These names are
# the [JsonProperty] values on the C# side. A drift in either direction produces a log that looks
# healthy and analyses to nothing — which is exactly what happened during development, when this
# tool read a 'losses' key the C# never wrote and reported a 0.0% loss rate for every class instead
# of erroring. Hence the check.
# v3 was the first schema that read MapEventParty.Troops. v4 added ground-truth fields
# (strength, advantage, present/participating/troopLimit, playerSimulated) and the troop census.
# Both are analysable; the v4-only fields simply go unreported for v3 records, which is why they
# are listed separately rather than as required.
# v5 fixed composition by snapshotting rosters at MapEventStarted. v6 moved the four per-side
# INPUT fields (leader, tactics, powerModifier, sideMorale) into that same start snapshot and added
# contextModifier. Both versions parse; v5's four fields are read after the engine has already
# stripped the loser's leader and zeroed its morale, so a v5 corpus reports the consequence of
# losing as though it were a cause. report_schema says so rather than silently blending the two.
SUPPORTED_VERSIONS = {5, 6}
FIXED_INPUT_CAPTURE_VERSION = 6
EXPECTED_TOP = {'v', 'session', 'id', 'day', 'hour', 'type', 'settlement', 'terrain',
                'player', 'rounds', 'winner', 'endedBy', 'sides'}
OPTIONAL_TOP = {'playerSimulated', 'siege'}
EXPECTED_SIDE = {'leaderCulture', 'kingdom', 'leader', 'tactics', 'powerModifier',
                 'sideMorale', 'menStart', 'parties'}
OPTIONAL_SIDE = {'strength', 'advantage', 'contextModifier'}
EXPECTED_PARTY = {'culture', 'fielded', 'killed', 'wounded', 'routed'}
OPTIONAL_PARTY = {'present', 'participating', 'troopLimit'}
# The siege block decides every siege outcome (settlementAdvantage measured 3.6-6.0), and until
# 2026-08-08 it had no drift protection at all — the check only ever looked at the top level and
# the side level, so a renamed siege field would have gone unnoticed exactly like the 'losses' key.
EXPECTED_SIEGE = {'settlementAdvantage', 'wallLevel', 'wallHitPoints', 'enginesBuilt',
                  'engineProgress', 'settlementOwner'}
OPTIONAL_SIEGE = set()


def parties_of(rec):
    """Every (side_name, side, party) triple in a record."""
    for side_name, side in (rec.get('sides') or {}).items():
        for party in (side.get('parties') or []):
            yield side_name, side, party


def side_troops(side, key):
    """Sum one per-party map across a side, e.g. all 'fielded' or all 'killed'."""
    out = Counter()
    for party in (side.get('parties') or []):
        for tid, n in (party.get(key) or {}).items():
            out[tid] += n
    return out


def report_schema(records, out):
    """Fail loudly on producer/consumer drift rather than silently analysing zeroes."""
    # Every record and BOTH sides, not records[0] and whichever side iterates first. Drift does not
    # have to appear in the first battle of a log: a field added to the siege path, or one that only
    # a defender carries, would sail past a single-sample check. Unions across the corpus, so a
    # field missing from ANY record is missing and one present in ANY record is seen.
    top_keys, side_keys = set(), set()
    missing_top, missing_side = set(), set()
    for rec in records:
        top_keys |= set(rec)
        missing_top |= EXPECTED_TOP - set(rec)
        for side in (rec.get('sides') or {}).values():
            side_keys |= set(side)
            missing_side |= EXPECTED_SIDE - set(side)
    extra_top = top_keys - EXPECTED_TOP - OPTIONAL_TOP
    extra_side = side_keys - EXPECTED_SIDE - OPTIONAL_SIDE

    # Party and siege levels, checked because they were not. Scan until a sample of each is found
    # rather than trusting record 0 — the first battle in a log is rarely a siege, and an absent
    # sample must read as "nothing to check", never as "every field is missing".
    party = next((p for r in records for s in (r.get('sides') or {}).values()
                    for p in (s.get('parties') or [])), None)
    missing_party = (EXPECTED_PARTY - set(party)) if party is not None else set()
    extra_party = (set(party) - EXPECTED_PARTY - OPTIONAL_PARTY) if party is not None else set()

    siege = next((r['siege'] for r in records if r.get('siege')), None)
    missing_siege = (EXPECTED_SIEGE - set(siege)) if siege is not None else set()
    extra_siege = (set(siege) - EXPECTED_SIEGE - OPTIONAL_SIEGE) if siege is not None else set()

    if (missing_top or missing_side or extra_top or extra_side
            or missing_party or extra_party or missing_siege or extra_siege):
        out('\n  ! SCHEMA DRIFT between the log and this tool:')
        if missing_top:
            out(f'      missing top-level fields: {sorted(missing_top)}')
        if missing_side:
            out(f'      missing per-side fields:  {sorted(missing_side)}')
        if extra_top:
            out(f'      unread top-level fields:  {sorted(extra_top)}')
        if extra_side:
            out(f'      unread per-side fields:   {sorted(extra_side)}')
        if missing_party:
            out(f'      missing per-party fields: {sorted(missing_party)}')
        if extra_party:
            out(f'      unread per-party fields:  {sorted(extra_party)}')
        if missing_siege:
            out(f'      missing siege fields:     {sorted(missing_siege)}')
        if extra_siege:
            out(f'      unread siege fields:      {sorted(extra_siege)}')
        out('      Fix the mismatch before trusting anything below.')
        return False
    if party is None:
        out('  (no party sample in this corpus — per-party contract unchecked)')
    if siege is None:
        out('  (no siege in this corpus — siege contract unchecked)')

    # Anything below FIXED_INPUT_CAPTURE_VERSION but still inside SUPPORTED_VERSIONS is v5: its
    # composition IS trustworthy (v5 is where the start-snapshot landed) and only the four
    # leader-derived fields are post-battle artefacts. Versions whose COMPOSITION is untrustworthy
    # never reach here — load_records drops them outright.
    stale = sum(1 for r in records if (r.get('v') or 0) < FIXED_INPUT_CAPTURE_VERSION)
    if stale:
        pct = 100.0 * stale / len(records)
        out('')
        out(f'  ! {stale} of {len(records)} records ({pct:.1f}%) predate schema '
            f'v{FIXED_INPUT_CAPTURE_VERSION}.')
        out('      For those, leader / tactics / powerModifier / sideMorale were read AFTER the '
            'battle resolved,')
        out('      so a losing side reports morale 0 and no leader as an artefact of losing.')
        out('      Composition, counts and outcomes ARE reliable in these records — treat only '
            'those four fields as missing.')
    return True


def report_reconstruction(records, out):
    """Cross-check the per-party fielded rosters against the side's authoritative menStart.

    This is the alarm that would have caught the v1 bias immediately: when composition was read
    from the end-of-battle MemberRoster, a losing side's total fell far below menStart by exactly
    the number taken prisoner. Keeping the check means the next such divergence announces itself
    instead of quietly skewing every table below.
    """
    out('\n== ROSTER CROSS-CHECK (fielded vs menStart) ==\n')
    errors, checked = [], 0
    for rec in records:
        for _name, side in (rec.get('sides') or {}).items():
            # v4 logs ParticipatingTroopCount, which is the like-for-like comparison. v3 only
            # has menStart (men PRESENT), and the engine trims the allocated roster when a troop
            # limit applies — so a v3 shortfall is expected, not a defect.
            parts = side.get('parties') or []
            participating = sum(p.get('participating', -1) for p in parts
                                if p.get('participating', -1) >= 0)
            declared = participating if participating > 0 else (side.get('menStart') or 0)
            if declared <= 0:
                continue
            counted = sum(side_troops(side, 'fielded').values())
            checked += 1
            errors.append((counted - declared) / declared)
    if not checked:
        out('  no sides with a declared menStart')
        return
    bad = [e for e in errors if abs(e) > 0.05]
    out(f'  {checked} sides checked   median error {statistics.median(errors) * 100:+.1f}%   '
        f'{len(bad)} sides off by more than 5%')
    has_v4 = any(p.get('participating', -1) >= 0
                 for rec in records for _n, _s, p in parties_of(rec))
    if not has_v4:
        out('  (v3 records: compared against menStart = men PRESENT. The engine trims the')
        out('   allocated roster under a troop limit, so a negative gap here is expected.')
        out('   v4 logs ParticipatingTroopCount for a like-for-like check.)')
    elif len(bad) > checked * 0.1:
        out('  ! More than a tenth of sides disagree with their own menStart. The fielded rosters')
        out('    are not the armies that fought — do not tune from the tables below until this is')
        out('    understood. (menStart counts HEALTHY men at start; a side that entered with')
        out('    wounded troops will read slightly high, which is expected and small.)')


def load_census(paths):
    """Engine ground truth for every troop type, emitted once per session."""
    out = []
    for path in paths:
        with open(path, 'r', encoding='utf-8', errors='replace') as handle:
            for line in handle:
                if CENSUS_TAG not in line:
                    continue
                brace = line.find('{')
                if brace < 0:
                    continue
                try:
                    out.append(json.loads(line[brace:].strip()))
                except json.JSONDecodeError:
                    pass
    return out


def report_census(census, troops, out):
    """Check every offline derivation against what the engine actually reports."""
    out('\n== ENGINE GROUND TRUTH (troop census) ==\n')
    if not census:
        out('  no census in these logs (pre-v4). Offline tier/power/class derivations are')
        out('  UNVERIFIED against the running engine.')
        return
    agree, disagree = troops.adopt_census(census)
    checked = agree + disagree
    out(f'  {len(census)} census records; {checked} are non-hero troops present in troops_*.xml')
    out(f'  tier derivation:  {agree}/{checked} match the engine'
        + ('' if disagree == 0 else f'   ! {disagree} DISAGREE — offline tier maths is wrong'))
    tiers = [c.get('tier', 0) for c in census]
    out(f'  max tier in play: {max(tiers) if tiers else 0} (offline assumes MaxCharacterTier={MAX_TIER})')
    hp = Counter(troops.census_hp.get(t) for t in troops.census_hp)
    out(f'  hit points: {dict(hp.most_common(4))}'
        + ('   -> uniform, so race/armour never reach the removal roll'
           if len(hp) == 1 else '   ! non-uniform, the removal roll is NOT tier-only'))
    mism = 0
    for tid, form in troops.census_formation.items():
        mine = troops.cls.get(tid)
        expect = {'Cavalry': 'Cavalry', 'HorseArcher': 'Cavalry', 'Ranged': 'Archer'}.get(form)
        if expect and mine != expect:
            mism += 1
    out(f'  classifier: {len(troops.census_formation) - mism}/{len(troops.census_formation)} agree with '
        f"the engine's DefaultFormationClass")
    out('  power values now taken from the engine directly, not from the shipped table.')


def composition(roster, troops, key):
    """Share of a roster by class or race group. Unknown troop ids are counted and reported."""
    tally, unknown, total = Counter(), 0, 0
    for tid, count in roster.items():
        if not troops.known(tid):
            unknown += count
            continue
        tally[key(tid)] += count
        total += count
    if not total:
        return {}, unknown
    return {k: v / total for k, v in tally.items()}, unknown


def mean_tier(roster, troops):
    total = num = 0
    for tid, count in roster.items():
        if troops.known(tid):
            total += troops.tier(tid) * count
            num += count
    return total / num if num else 0.0


# ---------------------------------------------------------------- analyses

def report_sizes(records, out):
    out('\n== ARMY SIZES (calibrates CountExponent) ==\n')
    sizes, ratios = [], []
    for rec in records:
        a = rec['sides']['attacker'].get('menStart', 0)
        d = rec['sides']['defender'].get('menStart', 0)
        sizes += [a, d]
        if min(a, d) > 0:
            ratios.append(max(a, d) / min(a, d))
    if not sizes:
        out('  no battles matched the filters')
        return
    qs = statistics.quantiles(sizes, n=10)
    out(f'  battles {len(records)}   men per side: median {statistics.median(sizes):.0f}   '
        f'p10 {qs[0]:.0f}   p90 {qs[-1]:.0f}   max {max(sizes)}')
    out(f'  size ratio bigger:smaller — median {statistics.median(ratios):.2f}   '
        f'p90 {statistics.quantiles(ratios, n=10)[-1]:.2f}')
    lopsided = sum(1 for r in ratios if r >= 1.5) / len(ratios) * 100
    out(f'  {lopsided:.0f}% of battles are already 1.5:1 or worse on numbers alone')


def report_outcomes(records, out):
    out('\n== OUTCOME LOPSIDEDNESS (measures the threshold problem) ==\n')
    frac, rounds, ended = [], [], Counter()
    for rec in records:
        winner = rec.get('winner')
        side = rec['sides'].get(winner)
        if side and side.get('menStart'):
            fielded = sum(side_troops(side, 'fielded').values())
            lost = (sum(side_troops(side, 'killed').values())
                    + sum(side_troops(side, 'routed').values()))
            if fielded > 0:
                frac.append(max(0, fielded - lost) / fielded)
        if rec.get('rounds'):
            rounds.append(rec['rounds'])
        ended[rec.get('endedBy', '?')] += 1
    if not frac:
        out('  no outcomes recorded')
        return
    out(f'  winner keeps: median {statistics.median(frac) * 100:.0f}%   '
        f'mean {statistics.mean(frac) * 100:.0f}%')
    out(f'  winner keeps >90% (a walkover): {sum(1 for f in frac if f > 0.9) / len(frac) * 100:.0f}%'
        f'    <50% (a real fight): {sum(1 for f in frac if f < 0.5) / len(frac) * 100:.0f}%')
    if rounds:
        out(f'  rounds: median {statistics.median(rounds):.0f}   max {max(rounds)}')
    out('  ended by: ' + '  '.join(f'{k} {v}' for k, v in ended.most_common()))


def report_composition(records, troops, out):
    out('\n== COMPOSITION BY CULTURE (real rosters, not templates) ==\n')
    per_culture = defaultdict(lambda: defaultdict(list))
    tiers, unknown_total = defaultdict(list), 0
    for rec in records:
        for side in rec['sides'].values():
            for party in (side.get('parties') or []):
                culture = party.get('culture') or '?'
                roster = party.get('fielded') or {}
                comp, unknown = composition(roster, troops, lambda t: troops.cls[t])
                unknown_total += unknown
                if not comp:
                    continue
                for cls in CLASSES:
                    per_culture[culture][cls].append(comp.get(cls, 0.0))
                tiers[culture].append(mean_tier(roster, troops))
    if not per_culture:
        out('  no rosters recorded')
        return
    header = f'  {"culture":20s}' + ''.join(f'{c:>9s}' for c in CLASSES) + f'{"meanTier":>10s}{"n":>6s}'
    out(header)
    means = {}
    for culture in sorted(per_culture, key=lambda c: -len(per_culture[c][CLASSES[0]])):
        row = per_culture[culture]
        n = len(row[CLASSES[0]])
        means[culture] = {c: statistics.mean(row[c]) for c in CLASSES}
        out(f'  {culture:20s}'
            + ''.join(f'{means[culture][c] * 100:8.0f}%' for c in CLASSES)
            + f'{statistics.mean(tiers[culture]):10.2f}{n:6d}')
    out('')
    for cls in CLASSES:
        vals = [m[cls] for m in means.values()]
        if len(vals) > 1:
            spread = (max(vals) - min(vals)) * 100
            mad = statistics.mean([abs(v - statistics.mean(vals)) for v in vals]) * 100
            out(f'  {cls:10s} spread {spread:5.1f} pts   mean abs deviation {mad:4.1f} pts')
    out('\n  Starting templates showed ~7 pts mean deviation per class. Materially higher here')
    out('  means the counter matrix has real compositions to bite on; similar means it is texture.')
    if unknown_total:
        out(f'\n  ! {unknown_total} men had troop ids absent from troops_*.xml '
            f'(vanilla or another module) — excluded from shares')


def report_matchups(records, out):
    out('\n== MATCHUP FREQUENCY (which fights actually happen) ==\n')
    pairs = Counter()
    wins = defaultdict(lambda: [0, 0])
    for rec in records:
        a = rec['sides']['attacker'].get('leaderCulture') or '?'
        d = rec['sides']['defender'].get('leaderCulture') or '?'
        key = tuple(sorted((a, d)))
        pairs[key] += 1
        winner_culture = rec['sides'].get(rec.get('winner'), {}).get('leaderCulture')
        if winner_culture:
            wins[key][0 if winner_culture == key[0] else 1] += 1
    if not pairs:
        out('  none recorded')
        return
    out(f'  {"matchup":40s}{"battles":>9s}{"win split":>16s}')
    for (x, y), n in pairs.most_common(25):
        w = wins[(x, y)]
        total = w[0] + w[1]
        split = f'{w[0] / total * 100:.0f}% / {w[1] / total * 100:.0f}%' if total else '-'
        out(f'  {x + " vs " + y:40s}{n:9d}{split:>16s}')


def report_losses(records, troops, out):
    """Loss rates per class.

    'killed' and 'routed' are permanent; 'wounded' men recover, so they are reported separately —
    that split is the axis the cultural survival bonuses act on (Mordor -0.20, Lothlorien +0.50),
    and conflating the two would hide it entirely.

    No reconstruction is needed: schema v3 logs 'fielded' straight from the allocated battle roster
    (MapEventParty.Troops), which the engine never strips.
    """
    out('\n== LOSSES BY CLASS (empirical check on any counter values) ==\n')
    fielded, dead, hurt = Counter(), Counter(), Counter()
    for rec in records:
        for side in rec['sides'].values():
            for tid, count in side_troops(side, 'fielded').items():
                if troops.known(tid):
                    fielded[troops.cls[tid]] += count
            for key in ('killed', 'routed'):
                for tid, count in side_troops(side, key).items():
                    if troops.known(tid):
                        dead[troops.cls[tid]] += count
            for tid, count in side_troops(side, 'wounded').items():
                if troops.known(tid):
                    hurt[troops.cls[tid]] += count

    total_fielded = sum(fielded.values())
    if not total_fielded:
        out('  no rosters recorded')
        return
    if not sum(dead.values()) and not sum(hurt.values()):
        out('  rosters present but zero casualties recorded across every battle — that is not a')
        out('  plausible campaign. Suspect a schema mismatch between the log and this tool before')
        out('  drawing any conclusion from it.')
        return

    overall = sum(dead.values()) / total_fielded
    out(f'  {"class":12s}{"fielded":>11s}{"dead":>10s}{"wounded":>10s}{"death rate":>13s}')
    for cls in CLASSES:
        f, d, w = fielded[cls], dead[cls], hurt[cls]
        rate = d / f if f else 0
        flag = ''
        if f and overall:
            rel = rate / overall
            flag = '  <-- dies more than average' if rel > 1.15 else (
                '  <-- survives more than average' if rel < 0.85 else '')
        out(f'  {cls:12s}{f:11d}{d:10d}{w:10d}{rate * 100:12.1f}%{flag}')
    out(f'  {"ALL":12s}{total_fielded:11d}{sum(dead.values()):10d}{sum(hurt.values()):10d}'
        f'{overall * 100:12.1f}%')
    out('\n  Vanilla already grants type-vs-terrain modifiers, so a skew here is not proof our')
    out('  counters are needed — cross-check against the terrain column before concluding.')


def report_sieges(records, out):
    """Sieges obey different mechanics and are dominated by one term the field battles never see.

    GetSettlementAdvantage is roughly (5 + wallLevel - 1) divided by a siege-engine factor, so an
    unbreached wall-3 town hands the defender ~7x, falling to ~3.5x once rams, towers and artillery
    are up. No troop-quality change in the simulation comes close to that, which is why siege
    balance has to be read separately from field balance rather than pooled with it.
    """
    sieges = [r for r in records if r.get('type') in ('Siege', 'SiegeOutside', 'SallyOut')]
    if not sieges:
        return
    out('\n== SIEGES (different mechanics — never pool these with field battles) ==\n')
    kinds = Counter(r.get('type') for r in sieges)
    out('  ' + '  '.join(f'{k} {v}' for k, v in kinds.most_common()))

    def men(side):
        return sum(side_troops(side, 'fielded').values())

    usable = [r for r in sieges
              if men(r['sides']['attacker']) >= 20 and men(r['sides']['defender']) >= 20]
    if usable:
        ratios = [men(r['sides']['attacker']) / max(1, men(r['sides']['defender'])) for r in usable]
        aw = sum(1 for r in usable if r.get('winner') == 'attacker')
        out(f'  {len(usable)} with both sides >=20 men   attacker wins {aw / len(usable) * 100:.0f}%'
            f'   median attacker:defender {statistics.median(ratios):.2f}:1')

    withblock = [r for r in sieges if r.get('siege')]
    if not withblock:
        out("  ! no siege telemetry in these records (v3). The defender's settlement advantage is")
        out('    the term that decides a siege, and without it an outcome cannot be explained.')
        out('    v4 logs it; re-collect to analyse siege balance.')
        return

    adv = [r['siege'].get('settlementAdvantage', 0) for r in withblock]
    walls = Counter(r['siege'].get('wallLevel') for r in withblock)
    eng = [r['siege'].get('enginesBuilt', 0) for r in withblock]
    breached = sum(1 for r in withblock if (r['siege'].get('wallHitPoints') or 0) < 1e-5)
    out(f'  settlement advantage (defender multiplier): median {statistics.median(adv):.2f}'
        f'   min {min(adv):.2f}   max {max(adv):.2f}')
    out(f'  wall levels: ' + '  '.join(f'L{k} x{v}' for k, v in sorted(walls.items(), key=lambda x: str(x[0]))))
    out(f'  siege engines built: median {statistics.median(eng):.0f}   max {max(eng)}')
    out(f'  walls already breached: {breached}/{len(withblock)}')

    # The decisive question: does attacker numbers beat the wall multiplier?
    buckets = defaultdict(lambda: [0, 0])
    for r in withblock:
        a, d = men(r['sides']['attacker']), men(r['sides']['defender'])
        if a < 20 or d < 20:
            continue
        a_adv = (a / max(1, d)) / max(0.01, r['siege'].get('settlementAdvantage', 1))
        key = ('attacker under-matched' if a_adv < 0.5
               else 'roughly matched' if a_adv < 1.0 else 'attacker over-matched')
        buckets[key][0 if r.get('winner') == 'attacker' else 1] += 1
    if buckets:
        out('  numbers ratio vs the wall multiplier:')
        for k in ['attacker under-matched', 'roughly matched', 'attacker over-matched']:
            w, l = buckets[k]
            if w + l:
                out(f'    {k:24s} {w + l:4d} sieges   attacker wins {w / (w + l) * 100:3.0f}%')


def report_confounds(records, out):
    out('\n== CONFOUNDS TO CONTROL FOR ==\n')
    terrain, morale_low, tactics = Counter(), 0, []
    leader_stale = 0
    for rec in records:
        terrain[rec.get('terrain', '?')] += 1
        # Morale and Tactics are LEADER-DERIVED. Below v6 they were read after the battle, so a
        # losing side reports morale 0 and Tactics 0 as an artefact of losing. Averaging those in
        # with v6's correctly-captured values would silently drag both statistics toward zero in
        # proportion to how much stale data happens to be in the corpus.
        if (rec.get('v') or 0) < FIXED_INPUT_CAPTURE_VERSION:
            leader_stale += 1
            continue
        for side in rec['sides'].values():
            if (side.get('sideMorale') or 100) < 30:
                morale_low += 1
            if side.get('tactics') is not None:
                tactics.append(side['tactics'])
    out('  terrain: ' + '  '.join(f'{k} {v}' for k, v in terrain.most_common(8)))
    kinds = Counter(rec.get('type', '?') for rec in records)
    out('  battle type: ' + '  '.join(f'{k} {v}' for k, v in kinds.most_common()))
    out('    (sieges, raids and hideouts obey different mechanics — segment with --type before')
    out('     drawing a conclusion that assumes a field battle)')
    sessions = Counter(rec.get('session') or '?' for rec in records)
    if len(sessions) > 1:
        out(f'  ! {len(sessions)} DIFFERENT CAMPAIGNS pooled in this dataset: '
            + '  '.join(f'{(k or "?")[:8]} {v}' for k, v in sessions.most_common()))
        out('    Balance changes between campaigns make these incomparable. Filter with --session.')
    if leader_stale:
        out(f'  (morale/Tactics below computed from {len(records) - leader_stale} v'
            f'{FIXED_INPUT_CAPTURE_VERSION}+ records; {leader_stale} older records excluded — '
            'their leader fields are post-battle artefacts)')
    out(f'  sides entering below morale 30 (power x0.7): {morale_low}')
    if tactics:
        out(f'  leader Tactics: median {statistics.median(tactics):.0f}   '
            f'min {min(tactics)}   max {max(tactics)}   '
            f'(advantage spread {(max(tactics) - min(tactics)) * 0.1:.0f}%)')


# ---------------------------------------------------------------- replay

def candidate_power(troops, tid, spread, use_skill, culture_mult, culture):
    tier = troops.tier(tid)
    power = spread ** (tier / MAX_TIER) if spread else troops.base_power(tid)
    if use_skill:
        power *= troops.skill_residual(tid)
    if culture_mult:
        power *= culture_mult.get(culture, 1.0)
    return power


def counter_matrix(cycle=1.20, extra=1.06):
    idx = {c: i for i, c in enumerate(CLASSES)}
    m = [[1.0] * len(CLASSES) for _ in CLASSES]
    for a, b in [('Sword', 'Pike'), ('Pike', 'Cavalry'), ('Cavalry', 'Archer'), ('Archer', 'Sword')]:
        m[idx[a]][idx[b]] *= cycle
        m[idx[b]][idx[a]] /= cycle
    for a, b in [('Cavalry', 'Sword'), ('Archer', 'Pike')]:
        m[idx[a]][idx[b]] *= extra
        m[idx[b]][idx[a]] /= extra
    return m, idx


def build_army(roster, troops, culture, spread, use_skill, culture_mult, idx):
    army = []
    for tid, count in roster.items():
        if not troops.known(tid):
            continue
        power = candidate_power(troops, tid, spread, use_skill, culture_mult, culture)
        army.extend([(power, idx[troops.cls[tid]])] * int(count))
    return army


def build_side(side, troops, spread, use_skill, culture_mult, idx):
    """One side's whole army, built per party so each contingent keeps its own culture."""
    army = []
    for party in (side.get('parties') or []):
        army.extend(build_army(party.get('fielded') or {}, troops, party.get('culture'),
                               spread, use_skill, culture_mult, idx))
    return army


def simulate(a, b, matrix, strike_exp, count_exp, rng):
    """Faithful re-implementation of the engine's round/tick/strike/removal loop.

    Omits morale-driven rout, retreat and pursuit rounds, so it overstates how completely a loser
    is destroyed. A reported win rate means "this side wins", not "the loser is annihilated".
    """
    a, b = list(a), list(b)
    start_a, start_b = len(a), len(b)
    for _ in range(900):
        if not a or not b:
            break
        ticks_a = max(1, round(min(len(b) * 2, len(a) ** count_exp)))
        ticks_b = max(1, round(min(len(a) * 2, len(b) ** count_exp)))
        while (ticks_a + ticks_b) > 0 and a and b:
            if rng.random() < ticks_b / (ticks_a + ticks_b):
                ticks_b -= 1
                strikers, struck = b, a
            else:
                ticks_a -= 1
                strikers, struck = a, b
            p_s, c_s = strikers[rng.randrange(len(strikers))]
            i = rng.randrange(len(struck))
            p_d, c_d = struck[i]
            dmg = int((0.5 + 0.5 * rng.random()) * 40
                      * (p_s / p_d) ** strike_exp * matrix[c_s][c_d])
            if rng.randrange(100) < dmg:
                struck[i] = struck[-1]
                struck.pop()
    if a and not b:
        return 1, len(a) / max(1, start_a)
    if b and not a:
        return 0, len(b) / max(1, start_b)
    return 0, 0.0          # both still standing at the round cap — no winner


def derive_culture_multipliers(records, troops, spread, lo=0.70, hi=1.60):
    """Per-culture power correction, derived from the armies that actually fought.

    Equalises mean power-per-man toward the median culture. This is the term that replaces
    retiering, and deriving it from logs beats deriving it from party templates because templates
    only seed a party at spawn.
    """
    per_culture = defaultdict(lambda: [0.0, 0])
    for rec in records:
        for side in rec['sides'].values():
            for party in (side.get('parties') or []):
                culture = party.get('culture')
                if not culture:
                    continue
                for tid, count in (party.get('fielded') or {}).items():
                    if not troops.known(tid):
                        continue
                    power = candidate_power(troops, tid, spread, True, None, culture)
                    per_culture[culture][0] += power * count
                    per_culture[culture][1] += count
    means = {c: total / n for c, (total, n) in per_culture.items() if n}
    if not means:
        return {}, 0.0
    target = statistics.median(means.values())
    return {c: round(max(lo, min(hi, target / v)), 3) for c, v in means.items()}, target


def report_multipliers(records, troops, out, spread=2.5):
    out('\n== DERIVED CULTURE MULTIPLIERS (paste into battle_balance_config.json) ==\n')
    mult, target = derive_culture_multipliers(records, troops, spread)
    if not mult:
        out('  no rosters to derive from')
        return {}
    out(f'  target power/man = {target:.3f} (median culture), curve spread {spread}, clamp [0.70, 1.60]\n')
    out('  "CulturePower": {')
    rows = sorted(mult.items(), key=lambda kv: -kv[1])
    for i, (culture, value) in enumerate(rows):
        comma = ',' if i < len(rows) - 1 else ''
        out(f'    "{culture}": {value}{comma}')
    out('  }')
    out('\n  Equal MEAN power is not equal outcome — (Ps/Pd)^0.7 is concave, so a roster with a')
    out('  long low-tier tail still loses to a uniform roster of the same mean. Treat these as a')
    out('  starting point and tune against the replay win rates, not against equal means.')
    return mult


def report_replay(records, troops, out, trials=12):
    out('\n== REPLAY: real logged rosters under candidate knobs ==\n')
    rng = random.Random(20260808)
    identity = [[1.0] * len(CLASSES) for _ in CLASSES]
    matrix, idx = counter_matrix()
    derived, _ = derive_culture_multipliers(records, troops, 2.5)
    scenarios = [
        ('shipped today', None, False, None, identity, 0.70, 0.60),
        ('flat 2.5 + skills', 2.5, True, None, identity, 0.70, 0.60),
        ('+ exponents', 2.5, True, None, identity, 0.55, 0.75),
        ('+ counters', 2.5, True, None, matrix, 0.55, 0.75),
        ('+ culture multipliers', 2.5, True, derived, matrix, 0.55, 0.75),
    ]
    out(f'  {"scenario":24s}{"attacker win%":>15s}{"mean winner survivors":>24s}{"n":>7s}')
    for label, spread, use_skill, mult, mat, se, ce in scenarios:
        wins = n = 0
        survivor_fracs = []
        for rec in records:
            atk, dfn = rec['sides']['attacker'], rec['sides']['defender']
            # Per party, so a mixed-culture side keeps each contingent's own multiplier.
            army_a = build_side(atk, troops, spread, use_skill, mult, idx)
            army_b = build_side(dfn, troops, spread, use_skill, mult, idx)
            if len(army_a) < 10 or len(army_b) < 10:
                continue
            for _ in range(trials):
                win, frac = simulate(army_a, army_b, mat, se, ce, rng)
                wins += win
                n += 1
                if frac > 0:
                    survivor_fracs.append(frac)
        if n:
            kept = statistics.mean(survivor_fracs) * 100 if survivor_fracs else 0.0
            out(f'  {label:24s}{wins / n * 100:14.0f}%{kept:23.0f}%{n:7d}')
        else:
            # Never omit the row. A silently-missing scenario reads as "nothing to report" when
            # it actually means every battle was skipped for want of a usable roster.
            out(f'  {label:24s}{"no usable battles — every side had under 10 resolvable troops":>46s}')
    out('\n  Compare the "shipped today" attacker win rate against the LOGGED outcomes above.')
    out('  A large gap means the replay is missing something real (morale, rout, terrain) and its')
    out('  predictions should not be trusted for tuning until the gap is understood.')


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--log', action='append', help='log file or glob (repeatable)')
    ap.add_argument('--min-men', type=int, default=40,
                    help='ignore battles where the smaller side is under this (default 40)')
    ap.add_argument('--no-player', action='store_true', help='drop ALL player-involved battles')
    ap.add_argument('--keep-player-fought', action='store_true',
                    help='keep player battles that were FOUGHT rather than auto-resolved '
                         '(dropped by default — they measure mission combat, not SimulateHit)')
    ap.add_argument('--replay', action='store_true', help='replay real rosters under candidate knobs')
    ap.add_argument('--stdout', action='store_true', help='print only, write no report files')
    args = ap.parse_args()

    patterns = args.log or [DEFAULT_LOG_GLOB]
    paths = sorted({p for pattern in patterns for p in glob.glob(os.path.expanduser(pattern))})
    if not paths:
        print('No log files found. Looked for:', file=sys.stderr)
        for pattern in patterns:
            print(f'  {pattern}', file=sys.stderr)
        print('\nTurn on "Log auto-resolved battles" in the TAOM MCM panel and play for a while,',
              file=sys.stderr)
        print('then re-run. Pass --log <path> if your Bannerlord logs live elsewhere.', file=sys.stderr)
        return 1

    lines = []

    def out(text=''):
        print(text)
        lines.append(text)

    out(f'Auto-resolve battle logs — {len(paths)} file(s)')
    for p in paths:
        out(f'  {p}  ({os.path.getsize(p) / 1024:.0f} KB)')

    records, skipped, malformed, unsupported = load_records(
        paths, args.min_men, args.no_player, args.keep_player_fought)
    out(f'\n{len(records)} battles analysed   ({skipped} filtered out, {malformed} unparseable)')
    if malformed:
        out('  a trailing unparseable line is normal — the game was writing when it exited')
    if unsupported:
        total = sum(unsupported.values())
        detail = ', '.join(f'v{k}×{n}' for k, n in sorted(unsupported.items(), key=lambda kv: (kv[0] is None, kv[0])))
        out(f'  ! dropped {total} record(s) on unsupported schema versions: {detail}')
        out(f'    supported: {sorted(SUPPORTED_VERSIONS)}. Pre-v5 logs read composition from a '
            'roster the engine had already')
        out('    stripped, so a losing side came back a median 55% short — they are dropped, not '
            'analysed.')
    if not records:
        out('\nNothing to analyse after filtering. Try --min-men 0.')
        return 1

    troops = TroopData()
    out(f'{len(troops.level)} troop definitions loaded from troops_*.xml')
    report_census(load_census(paths), troops, out)
    # Hard stop, not a warning. The whole point of the check is that drifted data produces
    # confident, wrong numbers rather than an error — printing the reports anyway would defeat it.
    if not report_schema(records, out):
        return 1
    report_reconstruction(records, out)

    report_sizes(records, out)
    report_outcomes(records, out)
    report_composition(records, troops, out)
    report_matchups(records, out)
    report_losses(records, troops, out)
    report_sieges(records, out)
    report_confounds(records, out)
    if args.replay:
        report_multipliers(records, troops, out)
        report_replay(records, troops, out)

    if not args.stdout:
        os.makedirs(REPORT_DIR, exist_ok=True)
        report_path = os.path.join(REPORT_DIR, 'REPORT.md')
        with open(report_path, 'w', encoding='utf-8') as handle:
            handle.write('# Auto-resolve battle log analysis\n\n```\n')
            handle.write('\n'.join(lines))
            handle.write('\n```\n')
        print(f'\nwrote {report_path}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
