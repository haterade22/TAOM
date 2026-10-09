#!/usr/bin/env python
"""
War ledger analyzer (READ-ONLY). Issue #765.

Reads the one-line-per-kingdom-per-day `[WarLedger]` records that TAOM's FileLogger writes into
    <game>/bin/Win64_Shipping_Client/Logs/taom_debug_*.log
and measures how close the AI war stays to a stalemate between the Free Peoples and the Dark Powers.
Two runs (for example a catch-up layer off versus on) can be compared side by side.

Record format (v=1). The logger adds a prefix; the tool finds the literal token "[WarLedger] " and
parses the space-separated key=value tokens after it:
    t=kingdom cid day phase k side ai war towns castles pts base loss cap str pris tier vr pe
    t=side    cid day side pts base alive
    t=share   cid day share                (free / (free + evil), "na" when both are 0)
    t=event   cid day ev=tier|destroyed|chronicle plus that event's own keys

Rules: an unknown v exits 2; a malformed line is skipped and counted (the first one is printed as a
WARNING); a save-reload re-emits days, so per campaign (cid) the LAST kingdom line per (day, k), side
line per (day, side) and share line per day win, and exact repeated events collapse to one. Logs are
read oldest first (by the taom_debug_ name stamp, else file time), so the newer process wins whatever
the argument order. A reload of an OLDER save leaves the abandoned run's later days and events in the
report: the log carries no marker for the rewind yet.

Capital target (b) has four verdicts. FAIL: a capital was seen lost before day 120. PASS: kingdom lines
with a cap of 0 or 1 cover every day 1 to 119 and none was lost earlier. NO DATA: no kingdom line
carries a capital state (also what a drifted key looks like). UNKNOWN: coverage of days 1 to 119 has a
hole, so a loss could have happened unseen. --compare reads one file per side: concatenate a run's
logs (oldest first) to compare it.

Usage:
    python tools/analyze_war_ledger.py                       # newest taom_debug_*.log
    python tools/analyze_war_ledger.py a.log b.log --cid run1
    python tools/analyze_war_ledger.py a.log --csv series.csv
    python tools/analyze_war_ledger.py --compare off.log on.log
    python tools/analyze_war_ledger.py --compare off.log:run1 on.log:run2

Exit codes: 0 success; 1 no ledger lines, a cid not found, an unreadable log or an unwritable --csv
path; 2 unknown version or a usage error.
"""
import argparse
import csv
import glob
import math
import os
import re
import sys
from datetime import datetime

from _gamedir import game_dir

TAG = '[WarLedger] '
SUPPORTED_VERSION = '1'

DEFAULT_GAME_DIR = game_dir(r'E:\Steam\steamapps\common\Mount & Blade II Bannerlord')
DEFAULT_LOG_GLOB = os.path.join(
    DEFAULT_GAME_DIR, 'bin', 'Win64_Shipping_Client', 'Logs', 'taom_debug_*.log')

KINGDOM_KEYS = ('phase', 'k', 'side', 'ai', 'war', 'towns', 'castles', 'pts', 'base', 'loss', 'cap',
                'str', 'pris', 'tier', 'vr', 'pe')
SIDE_KEYS = ('side', 'pts', 'base', 'alive')
EVENT_KEYS = {
    'tier': ('k', 'from', 'to', 'loss'),
    'destroyed': ('k',),
    'chronicle': ('id', 'outcome'),
}
SIDES = ('free', 'evil', 'neutral')

BAND_LOW, BAND_HIGH = 0.4, 0.6
LEAD_HI, LEAD_LO = 0.52, 0.48
SHARE_TARGET_FRACTION = 0.8
CAPITAL_TARGET_DAY = 120
# A new campaign's first daily tick waits one day (Campaign.CreateCampaignEvents, Campaign.cs:1024-1030,
# v1.5.4; TAOM does not override CampaignTimeModel). [Likely]: confirm in the first observer log.
FIRST_TICK_DAY = 1
SAMPLE_COUNT = 3
STAMP_RE = re.compile(r'taom_debug_(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})')


class UnknownVersion(Exception):
    pass


class Campaign:
    def __init__(self):
        self.kingdoms = {}   # (day, k) -> field dict
        self.sides = {}      # (day, side) -> field dict
        self.shares = {}     # day -> float or None (na)
        self.events = []     # de-duplicated, in read order
        self._event_keys = set()

    def days(self):
        found = {d for d, _ in self.kingdoms} | {d for d, _ in self.sides} | set(self.shares)
        return sorted(found)


class Parsed:
    def __init__(self):
        self.campaigns = {}
        self.malformed = 0
        self.malformed_samples = []   # the first few malformed lines, verbatim
        self.ledger_lines = 0

    def note_malformed(self, line):
        self.malformed += 1
        if len(self.malformed_samples) < SAMPLE_COUNT:
            self.malformed_samples.append(line.rstrip('\r\n'))

    def campaign(self, cid):
        return self.campaigns.setdefault(cid, Campaign())


def _num(text, kind):
    value = kind(text)
    if kind is float and not math.isfinite(value):
        raise ValueError(text)
    return value


def _opt_float(text):
    return None if text == 'na' else _num(text, float)


def _record_fields(line):
    """The key=value dict after the tag, or None when the line carries no ledger record."""
    idx = line.find(TAG)
    if idx < 0:
        return None
    fields = {}
    for token in line[idx + len(TAG):].split():
        key, sep, value = token.partition('=')
        if not sep or not key:
            raise ValueError(token)
        fields[key] = value
    return fields


def _ingest(parsed, fields):
    for req in ('v', 't', 'cid', 'day'):
        if req not in fields:
            raise ValueError('missing ' + req)
    if fields['v'] != SUPPORTED_VERSION:
        raise UnknownVersion(fields['v'])
    kind, day = fields['t'], _num(fields['day'], int)
    if kind == 'kingdom':
        missing = [k for k in KINGDOM_KEYS if k not in fields]
        if missing:
            raise ValueError('missing ' + missing[0])
        _num(fields['pts'], int)
        _num(fields['tier'], int)
        rec = parsed.campaign(fields['cid'])
        rec.kingdoms[(day, fields['k'])] = fields
    elif kind == 'side':
        missing = [k for k in SIDE_KEYS if k not in fields]
        if missing:
            raise ValueError('missing ' + missing[0])
        _num(fields['pts'], int)
        if fields['base'] != 'na':
            _num(fields['base'], int)
        parsed.campaign(fields['cid']).sides[(day, fields['side'])] = fields
    elif kind == 'share':
        if 'share' not in fields:
            raise ValueError('missing share')
        parsed.campaign(fields['cid']).shares[day] = _opt_float(fields['share'])
    elif kind == 'event':
        keys = EVENT_KEYS.get(fields.get('ev'))
        if keys is None or any(k not in fields for k in keys):
            raise ValueError('bad event')
        camp = parsed.campaign(fields['cid'])
        key = tuple(sorted(fields.items()))
        if key not in camp._event_keys:
            camp._event_keys.add(key)
            camp.events.append((day, fields))
    else:
        raise ValueError('unknown t')


def parse_lines(lines):
    parsed = Parsed()
    for line in lines:
        try:
            fields = _record_fields(line)
        except ValueError:
            parsed.ledger_lines += 1
            parsed.note_malformed(line)
            continue
        if fields is None:
            continue
        parsed.ledger_lines += 1
        try:
            _ingest(parsed, fields)
        except UnknownVersion:
            raise
        except ValueError:
            parsed.note_malformed(line)
    return parsed


def _log_time(path):
    """Oldest-first sort key: the FileLogger name stamp (local time), else the file's modified time."""
    match = STAMP_RE.search(os.path.basename(path))
    if match:
        return datetime.strptime(match.group(1), '%Y-%m-%d_%H-%M-%S')
    return datetime.fromtimestamp(os.path.getmtime(path))


def order_logs(paths):
    """The paths oldest first with repeats dropped, so the newer process's lines win a re-emitted day."""
    unique = {os.path.normcase(os.path.abspath(p)): p for p in paths}
    return sorted(unique.values(), key=_log_time)


def read_files(paths):
    def gen():
        for path in order_logs(paths):
            with open(path, encoding='utf-8', errors='replace') as f:
                yield from f
    return parse_lines(gen())


def _by_kingdom(camp):
    out = {}
    for (day, k), rec in sorted(camp.kingdoms.items(), key=lambda kv: (kv[0][0], kv[0][1])):
        out.setdefault(k, []).append((day, rec))
    return out


def compute_metrics(camp):
    m = {}
    days = camp.days()
    m['first_day'] = days[0] if days else None
    m['last_day'] = days[-1] if days else None
    m['day_count'] = len(days)

    shares = [(d, s) for d, s in sorted(camp.shares.items()) if s is not None]
    m['share_days'] = len(shares)
    m['na_days'] = sum(1 for s in camp.shares.values() if s is None)
    if shares:
        devs = [(abs(s - 0.5), d) for d, s in shares]
        m['mean_dev'] = sum(x for x, _ in devs) / len(devs)
        m['max_dev'], m['max_dev_day'] = max(devs, key=lambda x: (x[0], -x[1]))
        m['in_band_fraction'] = sum(1 for _, s in shares if BAND_LOW <= s <= BAND_HIGH) / len(shares)
    else:
        m['mean_dev'] = m['max_dev'] = m['max_dev_day'] = m['in_band_fraction'] = None

    changes, last_sign = 0, 0
    for _, s in shares:
        sign = 1 if s > LEAD_HI else -1 if s < LEAD_LO else 0
        if sign:
            if last_sign and sign != last_sign:
                changes += 1
            last_sign = sign
    m['lead_changes'] = changes

    per_k = _by_kingdom(camp)
    loss, loss_side = {}, {}
    churn, tiers = {}, {}
    for k, series in per_k.items():
        prev_cap, prev_pts, prev_tier = None, None, None
        t = {'tier1_days': 0, 'tier2_days': 0, 'changes': 0}
        for day, rec in series:
            cap = rec['cap']
            if cap in ('0', '1'):
                if prev_cap == '1' and cap == '0' and k not in loss:
                    loss[k] = day
                    side = rec['side']
                    if side not in loss_side or day < loss_side[side]:
                        loss_side[side] = day
                prev_cap = cap
            pts, tier = int(rec['pts']), int(rec['tier'])
            if prev_pts is not None:
                churn[day // 10] = churn.get(day // 10, 0) + abs(pts - prev_pts)
            prev_pts = pts
            t['tier1_days'] += tier == 1
            t['tier2_days'] += tier == 2
            if prev_tier is not None and tier != prev_tier:
                t['changes'] += 1
            prev_tier = tier
        tiers[k] = t
    m['capital_loss'] = loss
    m['capital_loss_side'] = loss_side
    m['earliest_capital_loss'] = min(loss.values()) if loss else None
    m['churn'] = dict(sorted(churn.items()))
    m['churn_total'] = sum(churn.values())
    m['tiers'] = tiers

    m['destroyed'] = sorted((d, f['k']) for d, f in camp.events if f['ev'] == 'destroyed')
    m['chronicle'] = sorted((d, f['id'], f['outcome']) for d, f in camp.events if f['ev'] == 'chronicle')

    net = {}
    for side in SIDES:
        rows = [(d, r) for (d, s), r in sorted(camp.sides.items()) if s == side]
        if rows:
            first, last = rows[0][1], rows[-1][1]
            start = int(first['pts']) if first['base'] == 'na' else int(first['base'])
            net[side] = int(last['pts']) - start
    m['net_change'] = net

    m['target_share_pass'] = (m['in_band_fraction'] is not None
                              and m['in_band_fraction'] >= SHARE_TARGET_FRACTION)
    early = m['earliest_capital_loss']
    observed = {d for (d, _), r in camp.kingdoms.items() if r['cap'] in ('0', '1')}
    missing = [d for d in range(FIRST_TICK_DAY, CAPITAL_TARGET_DAY) if d not in observed]
    if early is not None and early < CAPITAL_TARGET_DAY:
        verdict, why = 'FAIL', f'day {early}'
    elif not observed:
        verdict, why = 'NO DATA', 'no kingdom line carries cap=0 or 1'
    elif missing:
        verdict, why = 'UNKNOWN', (f'no capital data for {len(missing)} of days '
                                   f'{FIRST_TICK_DAY}-{CAPITAL_TARGET_DAY - 1}, first missing day {missing[0]}')
    else:
        verdict, why = 'PASS', 'no capital lost' if early is None else f'day {early}'
    m['target_capital'], m['target_capital_reason'] = verdict, why
    return m


def _fmt(value, digits=3):
    if value is None:
        return 'n/a'
    if isinstance(value, float):
        return f'{value:.{digits}f}'
    return str(value)


def format_report(cid, m):
    out = [f'=== cid {cid} ===']
    out.append(f"days covered: {_fmt(m['first_day'])} to {_fmt(m['last_day'])} ({m['day_count']} days)")
    out.append('')
    out.append('Free share (|share - 0.5|)')
    out.append(f"  days with a share: {m['share_days']}  (na days ignored: {m['na_days']})")
    out.append(f"  mean deviation: {_fmt(m['mean_dev'])}")
    out.append(f"  max deviation: {_fmt(m['max_dev'])} on day {_fmt(m['max_dev_day'])}")
    out.append(f"  fraction of days in 0.4..0.6: {_fmt(m['in_band_fraction'])}")
    out.append(f"  lead changes (dead band 0.48..0.52): {m['lead_changes']}")
    out.append('')
    out.append('First capital loss')
    if m['capital_loss']:
        for k, d in sorted(m['capital_loss'].items(), key=lambda kv: (kv[1], kv[0])):
            out.append(f'  {k}: day {d}')
        for side, d in sorted(m['capital_loss_side'].items()):
            out.append(f'  earliest {side}: day {d}')
    else:
        out.append('  none')
    out.append('')
    out.append('Kingdoms destroyed')
    if m['destroyed']:
        out.extend(f'  day {d}: {k}' for d, k in m['destroyed'])
    else:
        out.append('  none')
    out.append('')
    out.append('Fief churn per 10-day bucket (sum of |pts change|; a capture counts for both kingdoms)')
    if m['churn']:
        out.extend(f'  days {b * 10}-{b * 10 + 9}: {v}' for b, v in m['churn'].items())
    else:
        out.append('  none')
    out.append('')
    out.append('Rally tiers (days at tier 1, days at tier 2, tier changes)')
    if m['tiers']:
        for k, t in sorted(m['tiers'].items()):
            out.append(f"  {k}: {t['tier1_days']}, {t['tier2_days']}, {t['changes']}")
    else:
        out.append('  none')
    out.append('')
    out.append('Net change per side (last pts minus first base)')
    if m['net_change']:
        out.extend(f'  {s}: {v:+d}' for s, v in m['net_change'].items())
    else:
        out.append('  none')
    out.append('')
    out.append('Chronicle outcomes')
    if m['chronicle']:
        out.append('  day  id  outcome')
        out.extend(f'  {d}  {i}  {o}' for d, i, o in m['chronicle'])
    else:
        out.append('  none')
    out.append('')
    out.append('Target check')
    frac = m['in_band_fraction']
    out.append(f"  (a) share within 0.4..0.6 on >= 80% of non-na days: "
               f"{'PASS' if m['target_share_pass'] else 'FAIL'} ({_fmt(frac)} of {m['share_days']} days)")
    out.append(f"  (b) earliest capital loss on day >= {CAPITAL_TARGET_DAY}: "
               f"{m['target_capital']} ({m['target_capital_reason']})")
    return '\n'.join(out)


def flat_metrics(m):
    """The numeric metrics, in display order, for --compare."""
    rows = [
        ('day_count', m['day_count']),
        ('share_days', m['share_days']),
        ('na_days', m['na_days']),
        ('mean_dev', m['mean_dev']),
        ('max_dev', m['max_dev']),
        ('in_band_fraction', m['in_band_fraction']),
        ('lead_changes', m['lead_changes']),
        ('earliest_capital_loss', m['earliest_capital_loss']),
        ('kingdoms_destroyed', len(m['destroyed'])),
        ('churn_total', m['churn_total']),
        ('tier1_days', sum(t['tier1_days'] for t in m['tiers'].values())),
        ('tier2_days', sum(t['tier2_days'] for t in m['tiers'].values())),
        ('tier_changes', sum(t['changes'] for t in m['tiers'].values())),
    ]
    rows += [(f'net_{s}', m['net_change'].get(s)) for s in SIDES]
    return rows


def format_compare(label_a, label_b, ma, mb):
    out = [f'A: {label_a}', f'B: {label_b}', '']
    out.append(f"{'metric':<24}{'A':>12}{'B':>12}{'B-A':>12}")
    for (name, a), (_, b) in zip(flat_metrics(ma), flat_metrics(mb)):
        if a is None or b is None:
            diff = 'n/a'
        elif isinstance(a, float) or isinstance(b, float):
            diff = f'{b - a:+.4f}'
        else:
            diff = f'{b - a:+d}'
        fa = f'{a:.4f}' if isinstance(a, float) else _fmt(a)
        fb = f'{b:.4f}' if isinstance(b, float) else _fmt(b)
        out.append(f'{name:<24}{fa:>12}{fb:>12}{diff:>12}')
    out.append('')
    for label, m in (('A', ma), ('B', mb)):
        out.append(f"target {label}: share {'PASS' if m['target_share_pass'] else 'FAIL'}, "
                   f"capital {m['target_capital']} ({m['target_capital_reason']})")
    return '\n'.join(out)


def write_csv(path, campaigns):
    with open(path, 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f, lineterminator='\n')
        w.writerow(['cid', 'day', 'share', 'free_pts', 'evil_pts', 'neutral_pts'])
        for cid, camp in campaigns:
            for day in camp.days():
                if day in camp.shares:
                    s = camp.shares[day]
                    share = 'na' if s is None else repr(s)
                else:
                    share = ''
                pts = [camp.sides[(day, s)]['pts'] if (day, s) in camp.sides else '' for s in SIDES]
                w.writerow([cid, day, share, *pts])


def split_path_cid(arg):
    """'path' or 'path:cid'; a Windows drive colon or a colon followed by a path is not a cid."""
    head, sep, tail = arg.rpartition(':')
    if sep and len(head) > 1 and tail and '\\' not in tail and '/' not in tail:
        return head, tail
    return arg, None


def _pick_cid(parsed, cid, label):
    """(cid, note) for one compare side."""
    if cid is not None:
        if cid not in parsed.campaigns:
            raise KeyError(f'{label}: cid {cid} not found')
        return cid, ''
    best = max(parsed.campaigns, key=lambda c: (len(parsed.campaigns[c].days()), c))
    note = ''
    if len(parsed.campaigns) > 1:
        note = f'{label}: several cids, using cid {best} (most days)'
    return best, note


def _no_campaign_message(parsed, where):
    if parsed.ledger_lines:
        return f'{parsed.ledger_lines} [WarLedger] lines, all malformed, in {where}'
    return f'No [WarLedger] lines found in {where}'


def _malformed_warning(parsed, label=''):
    if not parsed.malformed:
        return None
    who = f'{label}: ' if label else ''
    return (f'WARNING: {who}{parsed.malformed} of {parsed.ledger_lines} [WarLedger] lines malformed '
            f'and skipped; first: {parsed.malformed_samples[0]}')


def _compare(arg_a, arg_b):
    sides = []
    notes = []
    for label, arg in (('A', arg_a), ('B', arg_b)):
        path, cid = split_path_cid(arg)
        parsed = read_files([path])
        if not parsed.campaigns:
            raise LookupError(_no_campaign_message(parsed, path))
        warning = _malformed_warning(parsed, label)
        if warning:
            notes.append(warning)
        cid, note = _pick_cid(parsed, cid, label)
        if note:
            notes.append(note)
        m = compute_metrics(parsed.campaigns[cid])
        sides.append((f"{path} (cid {cid}), days {_fmt(m['first_day'])} to {_fmt(m['last_day'])}", m))
    for note in notes:
        print(note)
    print(format_compare(sides[0][0], sides[1][0], sides[0][1], sides[1][1]))


def _default_log():
    found = glob.glob(DEFAULT_LOG_GLOB)
    return max(found, key=os.path.getmtime) if found else None


def main(argv=None):
    ap = argparse.ArgumentParser(
        description='Measure how close the AI war stays to a stalemate from [WarLedger] log lines.')
    ap.add_argument('logs', nargs='*', help='log files; default: the newest taom_debug_*.log')
    ap.add_argument('--cid', help='report only this campaign id')
    ap.add_argument('--csv', metavar='PATH', help='write the per-day series (cid,day,share,pts per side)')
    ap.add_argument('--compare', nargs=2, metavar=('A', 'B'),
                    help='compare two logs (path or path:cid); prints B minus A')
    args = ap.parse_args(argv)
    if args.compare and (args.logs or args.csv is not None or args.cid is not None):
        ap.error('--compare takes path[:cid] pairs; --csv, --cid and log paths do not apply')

    try:
        if args.compare:
            _compare(*args.compare)
            return 0
        paths = args.logs
        if not paths:
            newest = _default_log()
            if newest is None:
                print(f'No [WarLedger] lines found: no log files match {DEFAULT_LOG_GLOB}')
                return 1
            paths = [newest]
            print(f'reading {newest}')
        parsed = read_files(paths)
    except UnknownVersion as e:
        print(f'error: unknown [WarLedger] version v={e}; this tool reads v={SUPPORTED_VERSION}',
              file=sys.stderr)
        return 2
    except (OSError, LookupError, KeyError) as e:
        print(f'error: {e}', file=sys.stderr)
        return 1

    if not parsed.campaigns:
        print(_no_campaign_message(parsed, ', '.join(paths)))
        return 1
    if args.cid is not None and args.cid not in parsed.campaigns:
        print(f'error: cid {args.cid} not found; present: ' + ', '.join(sorted(parsed.campaigns)),
              file=sys.stderr)
        return 1
    chosen = [(c, parsed.campaigns[c]) for c in sorted(parsed.campaigns)
              if args.cid is None or c == args.cid]
    warning = _malformed_warning(parsed)
    if warning:
        print(warning)
        print()
    for cid, camp in chosen:
        print(format_report(cid, compute_metrics(camp)))
        print()
    print(f'ledger lines read: {parsed.ledger_lines}; malformed lines skipped: {parsed.malformed}')
    if args.csv:
        try:
            write_csv(args.csv, chosen)
        except OSError as e:
            print(f'error: cannot write {args.csv}: {e}', file=sys.stderr)
            return 1
        print(f'wrote {args.csv}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
