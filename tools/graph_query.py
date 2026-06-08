#!/usr/bin/env python3
"""Query + audit TAOM's documentation knowledge graph.

Builds a directed graph from the literal markdown links in docs/ (via
tools/doc_graph.py, reusing lint_docs's link parser) and answers three questions
grep + INDEX.md cannot:

    explain <node>   — a doc's neighbourhood: what links to it, what it links out to
    path <a> <b>     — the shortest connection chain between two docs
    metrics          — god nodes (over-connected hubs), bridges (fragile single-edge
                       joins between clusters), component count, isolated orphans

Two ways to use it:
  - Navigate (mid-task, token-cheap): `explain X` / `path X Y` gives a ~15-line map
    instead of opening ten docs to discover how they connect.
  - Audit (KB hygiene): `metrics` flags docs that may want splitting (god nodes),
    fragile joins to reinforce (bridges), and disconnected docs (orphans).

Deterministic + offline: reads only links already on disk. No LLM, no network, no
new dependency. Nodes are `.md` files only — Serena owns the C# symbol graph and
the taom-moduledata MCP owns game-data references; this tool does neither.

Usage:
    python tools/graph_query.py explain career-system
    python tools/graph_query.py path warg-combat spider-skeleton-animation-pipeline
    python tools/graph_query.py path warg-combat spider --directed
    python tools/graph_query.py metrics [--top N] [--summary]
    # any verb accepts --json for machine-readable output

A node is named by bare slug (`career-system`), filename (`career-system.md`), or
repo-relative path (`features/career-system.md`). See docs/features/doc-graph.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doc_graph as dg  # noqa: E402
import lint_docs as ld  # noqa: E402


# --- node resolution with disambiguation --------------------------------- #
def _resolve_one(token: str, nodes) -> Path | None:
    """Resolve a token to exactly one node, or print a helpful message and return
    None (no match / ambiguous)."""
    cands = dg.resolve_node(token, nodes)
    if not cands:
        print(f"No doc matches '{token}'. Try a slug like 'career-system' or a path "
              f"like 'features/career-system.md'.", file=sys.stderr)
        return None
    if len(cands) > 1:
        print(f"'{token}' is ambiguous — {len(cands)} matches:", file=sys.stderr)
        for c in cands:
            print(f"  - {ld.rel(c)}", file=sys.stderr)
        return None
    return cands[0]


# --- renderers ----------------------------------------------------------- #
def _render_explain(data: dict) -> str:
    out = [f"# {data['node']}",
           f"in: {data['in_degree']}   out: {data['out_degree']}", ""]
    out.append(f"## Referenced by ({data['in_degree']})")
    out += [f"- {x['path']}" for x in data["inbound"]] or ["- (none — orphan)"]
    out.append("")
    out.append(f"## Links out to ({data['out_degree']})")
    out += [f"- {x['path']}" for x in data["outbound"]] or ["- (none)"]
    return "\n".join(out)


def _render_path(data: dict, a: str, b: str, directed: bool) -> str:
    if not data["found"]:
        why = ("no directed link chain — try without --directed" if directed
               else "different components — no connection exists")
        return f"No path from '{a}' to '{b}' ({why})."
    arrow = "  ->  ".join(data["path"])
    return f"{data['length']} nodes ({data['length'] - 1} hops):\n{arrow}"


def _render_metrics(m: dict, summary: bool) -> str:
    if summary:
        return "\n".join([
            "---",
            f"nodes:            {m['node_count']}",
            f"edges:            {m['edge_count']}",
            f"components:       {m['component_count']}",
            f"god_nodes:        {len(m['god_nodes'])}",
            f"bridges:          {len(m['bridges'])}",
            f"orphans:          {len(m['orphans'])}",
            "---",
            "",
        ])
    out = ["# Doc-graph metrics", "",
           f"- Nodes: **{m['node_count']}**   Edges: **{m['edge_count']}**",
           f"- Components: **{m['component_count']}** (sizes: {m['component_sizes']})",
           f"- Bridges (fragile single-edge joins): **{len(m['bridges'])}**",
           f"- Orphans (isolated docs): **{len(m['orphans'])}**", ""]
    out.append("## God nodes (highest degree — candidates to split)")
    out.append("")
    for g in m["god_nodes"]:
        out.append(f"- `{g['path']}` — total {g['total']} (in {g['in']}, out {g['out']})")
    out.append("")
    if m["bridges"]:
        out.append("## Bridges (removing the link disconnects two clusters)")
        out.append("")
        for b in m["bridges"]:
            out.append(f"- `{b['a_path']}` — `{b['b_path']}`")
        out.append("")
    if m["orphans"]:
        out.append("## Orphans (no inbound or outbound doc links)")
        out.append("")
        for o in m["orphans"]:
            out.append(f"- `{o['path']}`")
        out.append("")
    return "\n".join(out)


# --- command handlers ---------------------------------------------------- #
def cmd_explain(args) -> int:
    nodes, edges, inbound, _adj = dg.load_doc_graph()
    node = _resolve_one(args.node, nodes)
    if node is None:
        return 2
    data = dg.explain_node(node, edges, inbound)
    print(json.dumps(data, indent=2) if args.json else _render_explain(data))
    return 0


def cmd_path(args) -> int:
    nodes, edges, _inbound, adj = dg.load_doc_graph()
    a = _resolve_one(args.start, nodes)
    b = _resolve_one(args.goal, nodes)
    if a is None or b is None:
        return 2
    data = dg.shortest_path(edges if args.directed else adj, a, b)
    print(json.dumps(data, indent=2) if args.json
          else _render_path(data, a.stem, b.stem, args.directed))
    return 0 if data["found"] else 1


def cmd_metrics(args) -> int:
    nodes, edges, inbound, adj = dg.load_doc_graph()
    m = dg.graph_metrics(nodes, edges, inbound, adj, top=args.top)
    if args.json:
        print(json.dumps(m, indent=2))
    else:
        print(_render_metrics(m, summary=args.summary))
    return 0


def main(argv: list[str]) -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            try:
                stream.reconfigure(encoding="utf-8")
            except Exception:
                pass

    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    # Shared parent so --json works after the verb (e.g. `explain X --json`).
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--json", action="store_true", help="Machine-readable JSON output")
    sub = ap.add_subparsers(dest="cmd", required=True)

    p_ex = sub.add_parser("explain", parents=[common],
                          help="Show a doc's inbound/outbound neighbourhood")
    p_ex.add_argument("node")
    p_ex.set_defaults(func=cmd_explain)

    p_pa = sub.add_parser("path", parents=[common],
                          help="Shortest connection chain between two docs")
    p_pa.add_argument("start")
    p_pa.add_argument("goal")
    p_pa.add_argument("--directed", action="store_true",
                      help="Follow link direction (default: undirected)")
    p_pa.set_defaults(func=cmd_path)

    p_me = sub.add_parser("metrics", parents=[common],
                          help="God nodes, bridges, components, orphans")
    p_me.add_argument("--top", type=int, default=15, help="How many god nodes to list")
    p_me.add_argument("--summary", action="store_true",
                      help="Emit a --- delimited grep-friendly summary block")
    p_me.set_defaults(func=cmd_metrics)

    args = ap.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
