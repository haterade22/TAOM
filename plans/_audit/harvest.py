#!/usr/bin/env python3
"""Harvest the /improve audit results from a stalled Workflow journal.

The first whole-repo /improve audit (run wf_a32fca04-d55, 2026-06-12) stalled and
the session hit its usage limit before the workflow could aggregate. Its journal
persisted 85 agent results on disk. This script recovers them into durable,
human-readable files so no further usage can lose the work.

Run: python plans/_audit/harvest.py
Re-runnable; pure stdlib. Reads the journal, writes harvest.json + harvest.md
next to itself. NOTE: pass the journal path in the Windows C:/ form — a native
Windows Python cannot stat the Git-Bash /c/ form.
"""
import json, re, os, sys

JOURNAL = r"C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/efa725b7-589d-4d5e-a3fa-21abc727edb1/subagents/workflows/wf_a32fca04-d55/journal.jsonl"
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
DATE = "2026-06-12"

CAT_ALIAS = {  # normalize prefixes that different agent runs emitted inconsistently
    "DIR": "DIRECTION", "DEBT": "TECHDEBT", "DEP": "DEPS", "DOC": "DOCS",
    "BUG": "CORRECTNESS", "CORR": "CORRECTNESS",
}
CAT_ORDER = ["CORRECTNESS", "SEC", "PERF", "TEST", "TECHDEBT", "DEPS", "DX", "DOCS", "GAMEDATA", "DIRECTION"]
CONF_W = {"HIGH": 1.0, "MED": 0.6, "LOW": 0.3}
EFFORT_W = {"S": 1, "M": 2, "L": 3}


def norm_cat(fid):
    pre = (fid or "?").split("-")[0].upper()
    return CAT_ALIAS.get(pre, pre)


def load_results(path):
    audits, verdicts = [], []
    with open(path, encoding="utf-8") as f:
        for ln in f:
            ln = ln.strip()
            if not ln:
                continue
            try:
                o = json.loads(ln)
            except Exception:
                continue
            if o.get("type") != "result":
                continue
            r = o.get("result")
            if isinstance(r, str):
                try:
                    r = json.loads(r)
                except Exception:
                    continue
            if not isinstance(r, dict):
                continue
            if "findings" in r:
                audits.append(r)
            elif "isReal" in r:
                verdicts.append(r)
    return audits, verdicts


def sig(fd):
    ev = fd.get("evidence") or []
    first = ev[0] if ev else {}
    return (norm_cat(fd.get("id", "")), (fd.get("id") or "").upper(),
            (first.get("file", "") or "").lower().strip(), str(first.get("line", "")).strip())


def collect_findings(audits):
    seen, out, dup = {}, [], 0
    not_audited = {}
    for a in audits:
        for fd in a.get("findings", []):
            cat = norm_cat(fd.get("id", ""))
            fd["_cat"] = cat
            s = sig(fd)
            if s in seen:
                dup += 1
                # keep the richer record (more evidence rows)
                if len(fd.get("evidence", [])) > len(seen[s].get("evidence", [])):
                    out[out.index(seen[s])] = fd
                    seen[s] = fd
                continue
            seen[s] = fd
            out.append(fd)
        na = a.get("notAudited")
        if na:
            # multiple agents per cat (re-runs); keep the longest note per category
            cats = {norm_cat(f.get("id", "")) for f in a.get("findings", [])}
            for c in cats:
                if len(str(na)) > len(str(not_audited.get(c, ""))):
                    not_audited[c] = na
    return out, dup, not_audited


def match_verdict(fd, verdicts):
    """Best-effort: a verdict references the finding's evidence files in its text.
    Returns the verdict whose correctedEvidence/reasoning mentions the most of
    this finding's evidence file basenames (>=1 required)."""
    files = {os.path.basename((e.get("file") or "")).lower() for e in fd.get("evidence", []) if e.get("file")}
    files.discard("")
    best, best_score = None, 0
    title_words = set(re.findall(r"[a-z]{5,}", (fd.get("title") or "").lower()))
    for v in verdicts:
        if v.get("_used"):
            continue
        text = ((v.get("correctedEvidence") or "") + " " + (v.get("reasoning") or "")).lower()
        score = sum(1 for b in files if b and b in text)
        tw = len(title_words & set(re.findall(r"[a-z]{5,}", text)))
        score = score * 3 + (1 if tw >= 3 else 0)
        if score > best_score:
            best, best_score = v, score
    if best and best_score >= 3:  # at least one evidence-file basename matched
        best["_used"] = True
        return best
    return None


def leverage(fd):
    return CONF_W.get((fd.get("confidence") or "").upper(), 0.3) / EFFORT_W.get((fd.get("effort") or "M").upper(), 2)


def main():
    if not os.path.exists(JOURNAL):
        sys.exit(f"journal not found (pass Windows C:/ path): {JOURNAL}")
    audits, verdicts = load_results(JOURNAL)
    findings, dup, not_audited = collect_findings(audits)
    for fd in findings:
        v = match_verdict(fd, verdicts)
        fd["_verdict"] = ({"isReal": v.get("isReal"), "confidenceAdjustment": v.get("confidenceAdjustment"),
                           "correctedEvidence": v.get("correctedEvidence"), "reasoning": v.get("reasoning")}
                          if v else None)
    matched = sum(1 for f in findings if f["_verdict"])

    by_cat = {}
    for fd in findings:
        by_cat.setdefault(fd["_cat"], []).append(fd)
    for c in by_cat:
        by_cat[c].sort(key=leverage, reverse=True)

    cats_sorted = [c for c in CAT_ORDER if c in by_cat] + [c for c in sorted(by_cat) if c not in CAT_ORDER]

    with open(os.path.join(OUT_DIR, f"{DATE}-harvest.json"), "w", encoding="utf-8") as f:
        json.dump({"source_run": "wf_a32fca04-d55", "audit_results": len(audits),
                   "verdict_results": len(verdicts), "unique_findings": len(findings),
                   "duplicates_removed": dup, "verdicts_matched": matched,
                   "findings_by_category": by_cat, "not_audited": not_audited}, f, indent=1)

    lines = [f"# /improve audit harvest — {DATE}", "",
             f"Recovered from stalled Workflow `wf_a32fca04-d55` journal (session hit usage limit before aggregation).",
             f"**{len(findings)} unique findings** across {len(cats_sorted)} categories "
             f"({dup} re-run duplicates removed; {len(audits)} audit-results + {len(verdicts)} verdict-results in journal; "
             f"{matched} findings auto-matched to a verifier verdict — UNMATCHED ≠ refuted, just not auto-joined).",
             "",
             "> These are RAW subagent findings. Per the /improve skill, the advisor (Claude) must personally re-read "
             "each cited location before it enters the presented table — treat every row as a hypothesis. "
             "Verdicts shown are the in-workflow verifier's view (supporting signal), not final.", ""]
    for c in cats_sorted:
        fds = by_cat[c]
        lines.append(f"## {c} ({len(fds)})")
        na = not_audited.get(c)
        if na:
            lines.append(f"_Not audited:_ {str(na)[:400]}")
        lines.append("")
        for fd in fds:
            v = fd.get("_verdict")
            vtag = ("✓ verifier:REAL" if v and v.get("isReal") else
                    "✗ verifier:NOT-REAL" if v and v.get("isReal") is False else "· unmatched")
            lines.append(f"### [{fd.get('id','?')}] {fd.get('title','(no title)')}")
            lines.append(f"- **effort** {fd.get('effort','?')} · **risk** {fd.get('riskLevel','?')} "
                         f"({fd.get('riskWhy','')}) · **confidence** {fd.get('confidence','?')} · {vtag}")
            lines.append(f"- **impact**: {fd.get('impact','')}")
            for e in fd.get("evidence", []):
                lines.append(f"  - `{e.get('file','?')}:{e.get('line','?')}` — {e.get('what','')}")
            lines.append(f"- **fix sketch**: {fd.get('fixSketch','')}")
            if v and v.get("correctedEvidence") and "as cited" not in (v.get("correctedEvidence") or "").lower():
                lines.append(f"- **verifier evidence note**: {str(v.get('correctedEvidence'))[:300]}")
            lines.append("")
    with open(os.path.join(OUT_DIR, f"{DATE}-harvest.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"unique_findings={len(findings)} duplicates_removed={dup} verdicts_matched={matched}/{len(verdicts)}")
    print("by category:", {c: len(by_cat[c]) for c in cats_sorted})


if __name__ == "__main__":
    main()
