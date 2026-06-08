#!/usr/bin/env python3
"""TAOM doc-link graph library.

Builds a directed graph of markdown documents from the literal `[label](path.md)`
links already present in docs/, then offers BFS / degree / component / bridge
analysis over it. Pure stdlib, deterministic, offline — it reads only links that
exist on disk and never calls an LLM or the network.

This is the shared core behind the graph_query CLI (tools/graph_query.py). It
deliberately reuses the link-parsing primitives from tools/lint_docs.py (the
single source of the markdown-link regex, code-fence skipping, link resolution,
and exemption rules) so the three doc tools agree on what a "link" is.

Scope (and explicit non-goals — keep this tight):
  - Nodes are `.md` files only. Docs may *link to* `.cs`, but source code is never
    a node — Serena owns the C# symbol graph.
  - Not a game-data graph — tools/taom_schema.py + the taom-moduledata MCP own
    item/troop/culture references.
  - No LLM calls, no embeddings, no inferred/semantic edges — edges are literal
    links only.
  - No HTML/D3/Obsidian/Neo4j export and no on-disk graph cache — ADR-010 rejected
    viz + wikilinks; build_backlinks.py owns the only persisted graph artifact
    (the "Referenced by" footers).

See docs/features/doc-graph.md.
"""
from __future__ import annotations

import sys
from collections import deque
from pathlib import Path
from typing import Callable, Iterator

# Reuse the link-parsing core from the sibling lint_docs.py (same pattern as
# build_backlinks.py) so all three tools share one definition of a "link".
sys.path.insert(0, str(Path(__file__).resolve().parent))
import lint_docs as ld  # noqa: E402

# --- backlinks-footer markers -------------------------------------------- #
# Copied (once) from build_backlinks.py rather than imported, to avoid a circular
# import when build_backlinks is later retrofitted to import the edge walk from
# this module (plan Commit 4). This is the single sanctioned copy.
# TODO(dedupe): on the build_backlinks retrofit, move these + strip_backlinks_region
# here and have build_backlinks import them from doc_graph.
START_MARKER = "<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->"
END_MARKER = "<!-- backlinks-end -->"
START_MARKER_PREFIX = "<!-- backlinks-start"

Eligible = Callable[[Path], bool]


def strip_backlinks_region(text: str) -> str:
    """Remove the auto-generated backlinks footer (if any) before scanning links,
    so a doc's reverse-link footer doesn't get counted as outbound edges. Mirrors
    build_backlinks.strip_backlinks_region (rfind so a fenced footer-shaped example
    earlier in the file doesn't shadow the real trailing footer)."""
    start = text.rfind(START_MARKER_PREFIX)
    end = text.rfind(END_MARKER)
    if start == -1 or end == -1 or end <= start:
        return text
    return text[:start] + text[end + len(END_MARKER):]


# --- edge extraction ----------------------------------------------------- #
def iter_outbound_links(f: Path, *, strip_footer: bool = False) -> Iterator[Path]:
    """Yield the resolved, on-disk `.md` link targets of f (absolute Paths).

    Skips: code fences, inline code, external/anchor-only links, non-.md targets,
    and dangling links (a target that doesn't exist on disk — that's lint_docs's
    dead-link job, not a graph edge). Fragments/queries are stripped by resolve_link.
    """
    try:
        text = f.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return
    if strip_footer:
        text = strip_backlinks_region(text)
    for _lineno, line in ld.iter_lines_outside_code_fences(text):
        scrubbed = ld.INLINE_CODE_RE.sub("", line)
        for match in ld.LINK_RE.finditer(scrubbed):
            target = match.group(2).strip()
            if not ld.looks_like_path_target(target):
                continue
            resolved = ld.resolve_link(f, target)
            if resolved is None or not resolved.exists():
                continue
            if resolved.suffix.lower() != ".md":
                continue
            yield resolved


def build_edges(files, *, strip_footer: bool = False,
                eligible: Eligible | None = None) -> dict[Path, set[Path]]:
    """Directed adjacency `source -> {targets}` over the given md files. Both
    endpoints are resolved absolute Paths; self-edges are dropped. When `eligible`
    is given, both endpoints must satisfy it (used by the CLI to exclude archived /
    transcript docs via lint_docs.is_dead_link_exempt)."""
    edges: dict[Path, set[Path]] = {}
    for f in files:
        src = f.resolve()
        if eligible and not eligible(src):
            continue
        for tgt in iter_outbound_links(f, strip_footer=strip_footer):
            if tgt == src:
                continue
            if eligible and not eligible(tgt):
                continue
            edges.setdefault(src, set()).add(tgt)
    return edges


def build_inbound(edges: dict[Path, set[Path]]) -> dict[Path, set[Path]]:
    """Transpose: `target -> {sources that link to it}`."""
    inbound: dict[Path, set[Path]] = {}
    for src, tgts in edges.items():
        for t in tgts:
            inbound.setdefault(t, set()).add(src)
    return inbound


def undirected_adj(edges: dict[Path, set[Path]]) -> dict[Path, set[Path]]:
    """Undirected adjacency (both directions added). Isolated nodes are absent —
    callers pass an explicit node set to the component/bridge functions so degree-0
    docs are still counted."""
    adj: dict[Path, set[Path]] = {}
    for src, tgts in edges.items():
        for t in tgts:
            adj.setdefault(src, set()).add(t)
            adj.setdefault(t, set()).add(src)
    return adj


# --- metrics primitives -------------------------------------------------- #
def degrees(nodes, edges, inbound) -> dict[Path, dict]:
    """Per-node {in, out, total} degree over the full node set."""
    return {
        n: {
            "in": len(inbound.get(n, ())),
            "out": len(edges.get(n, ())),
            "total": len(inbound.get(n, ())) + len(edges.get(n, ())),
        }
        for n in nodes
    }


def connected_components(nodes, adj) -> list[set[Path]]:
    """Union-find over `nodes` using undirected `adj`. Nodes absent from adj form
    singleton components."""
    parent: dict[Path, Path] = {n: n for n in nodes}

    def find(x: Path) -> Path:
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a: Path, b: Path) -> None:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    for a, neighbours in adj.items():
        if a not in parent:
            continue
        for b in neighbours:
            if b in parent:
                union(a, b)

    groups: dict[Path, set[Path]] = {}
    for n in nodes:
        groups.setdefault(find(n), set()).add(n)
    return list(groups.values())


def bridges(nodes, adj) -> list[tuple[Path, Path]]:
    """Undirected bridges: edges whose removal raises the component count — a
    single-edge join holding two clusters together. Naive remove-and-recount;
    O(E·(V+E)) — ~1s at TAOM's current ~315 nodes / ~500 edges, growing roughly
    linearly in E. (TODO: Tarjan low-link bridge-finding is O(V+E) if this ever
    gets slow enough to matter.)"""
    base = len(connected_components(nodes, adj))
    seen: set[frozenset] = set()
    edge_list: list[tuple[Path, Path]] = []
    for a in adj:
        for b in adj[a]:
            key = frozenset((a, b))
            if key not in seen:
                seen.add(key)
                edge_list.append(tuple(sorted((a, b), key=ld.rel)))
    result: list[tuple[Path, Path]] = []
    for a, b in edge_list:
        adj[a].discard(b)
        adj[b].discard(a)
        if len(connected_components(nodes, adj)) > base:
            result.append((a, b))
        adj[a].add(b)
        adj[b].add(a)
    result.sort(key=lambda ab: (ld.rel(ab[0]), ld.rel(ab[1])))
    return result


def shortest_path(adj, start: Path, goal: Path) -> dict:
    """BFS shortest path over adjacency `adj` (pass the undirected adj for an
    undirected path, or the directed `edges` map to respect link direction).
    Returns {found, length (node count), path (list of stems)}."""
    if start == goal:
        return {"found": True, "length": 1, "path": [start.stem]}
    # Only `start` must have outgoing edges; a valid `goal` may be a pure sink
    # (absent from a directed adjacency's keys). BFS handles unreachability.
    if start not in adj:
        return {"found": False, "length": 0, "path": []}
    q: deque[Path] = deque([start])
    parent: dict[Path, Path | None] = {start: None}
    while q:
        cur = q.popleft()
        for nxt in sorted(adj.get(cur, ()), key=ld.rel):  # deterministic
            if nxt in parent:
                continue
            parent[nxt] = cur
            if nxt == goal:
                chain = [nxt]
                while parent[chain[-1]] is not None:
                    chain.append(parent[chain[-1]])
                chain.reverse()
                return {"found": True, "length": len(chain), "path": [p.stem for p in chain]}
            q.append(nxt)
    return {"found": False, "length": 0, "path": []}


# --- high-level dict builders (what the CLI renders / dumps as --json) ---- #
def _neighbours(node: Path, mapping: dict[Path, set[Path]]) -> list[dict]:
    return [{"name": p.stem, "path": ld.rel(p)}
            for p in sorted(mapping.get(node, ()), key=ld.rel)]


def explain_node(node: Path, edges, inbound) -> dict:
    """A node's neighbourhood: inbound (who links here) + outbound (what it links)."""
    ins = _neighbours(node, inbound)
    outs = _neighbours(node, edges)
    return {
        "node": ld.rel(node),
        "name": node.stem,
        "in_degree": len(ins),
        "out_degree": len(outs),
        "inbound": ins,
        "outbound": outs,
    }


def graph_metrics(nodes, edges, inbound, adj, top: int = 15) -> dict:
    """God nodes (top-N by total degree), bridges, component count + sizes, and
    isolated orphans — the KB-hygiene signal."""
    deg = degrees(nodes, edges, inbound)
    ranked = sorted(nodes, key=lambda n: (-deg[n]["total"], ld.rel(n)))
    comps = connected_components(nodes, adj)
    br = bridges(nodes, adj)
    orphans = sorted((n for n in nodes if deg[n]["total"] == 0), key=ld.rel)
    return {
        "node_count": len(nodes),
        "edge_count": sum(len(v) for v in edges.values()),
        "component_count": len(comps),
        "component_sizes": sorted((len(c) for c in comps), reverse=True),
        "god_nodes": [{"name": n.stem, "path": ld.rel(n), **deg[n]} for n in ranked[:top]],
        "bridges": [{"a": a.stem, "b": b.stem, "a_path": ld.rel(a), "b_path": ld.rel(b)}
                    for a, b in br],
        "orphans": [{"name": n.stem, "path": ld.rel(n)} for n in orphans],
    }


# --- node resolution ----------------------------------------------------- #
def resolve_node(token: str, nodes) -> list[Path]:
    """Resolve a user token to matching node Paths. Accepts a bare slug
    (`career-system`), a filename (`career-system.md`), a full repo-relative path
    (`docs/features/career-system.md`), or any trailing path segment of one
    (`features/career-system.md`). Returns all matches (>1 ⇒ ambiguous; caller
    disambiguates); [] if none."""
    t = token.strip().replace("\\", "/").lower()
    t_noext = t[:-3] if t.endswith(".md") else t
    matches: list[Path] = []
    for n in nodes:
        # ld.rel only normalizes separators for paths under REPO_ROOT; normalize
        # here too so matching is robust for any node (and matches `t`'s slashes).
        relp = ld.rel(n).replace("\\", "/").lower()
        relp_noext = relp[:-3] if relp.endswith(".md") else relp
        keys = {relp, relp_noext, n.name.lower(), n.stem.lower()}
        if t in keys or t_noext in keys:
            matches.append(n)
        elif relp.endswith("/" + t) or relp_noext.endswith("/" + t_noext):
            # path-aligned trailing-segment match: the leading "/" anchor makes
            # `features/career-system.md` match but stops `system` matching
            # `filesystem` (a bare substring would).
            matches.append(n)
    return sorted(set(matches), key=ld.rel)


# --- whole-graph loader (real docs/ tree) -------------------------------- #
def default_eligible(p: Path) -> bool:
    """Exclude archived docs + Codex/lint transcripts + TEMPLATE.md from the graph
    (same set lint_docs/build_backlinks skip) so metrics reflect the curated KB."""
    return not ld.is_dead_link_exempt(p)


def load_doc_graph(*, strip_footer: bool = True, eligible: Eligible = default_eligible):
    """Build (nodes, edges, inbound, adj) over the live docs/ tree. `nodes` is the
    full eligible `.md` set (so isolated docs are counted); footers are stripped so
    reverse-link footers don't masquerade as forward edges."""
    files = list(ld.iter_markdown(ld.DOCS_DIR))
    nodes = {p.resolve() for p in files if eligible(p.resolve())}
    edges = build_edges(files, strip_footer=strip_footer, eligible=eligible)
    inbound = build_inbound(edges)
    adj = undirected_adj(edges)
    return nodes, edges, inbound, adj
