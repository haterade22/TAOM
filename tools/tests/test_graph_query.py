#!/usr/bin/env python3
"""Unit tests for the doc-graph library (tools/doc_graph.py), which backs the
graph_query CLI (tools/graph_query.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

Hermetic: every test builds a synthetic docs tree in a tempdir and asserts against
a hand-checkable graph. We never read the live docs/ tree (that would drift; see
memory feedback_mirror_table_drifts_from_production).

Fixture graph (directed):
    A -> B, A -> C, B -> D, C -> D, D -> E      (one 5-node blob)
    F -> G                                       (a 2-node pair)
    X                                            (isolated, no links)

Hand-checked facts:
    - inbound(D) = {B, C}; outbound(D) = {E}; total degree of D = 3 (the god node)
    - shortest undirected path A..E has length 4 (A-B-D-E or A-C-D-E)
    - 3 connected components: {A,B,C,D,E}, {F,G}, {X}
    - bridges: D-E and F-G (removing either splits a component); A-B is NOT a bridge
      (A still reaches B through C-D)
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import doc_graph as dg  # noqa: E402


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class DocGraphTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        _write(self.root / "A.md", "intro [B](B.md) and [C](C.md)\n")
        _write(self.root / "B.md", "see [D](D.md)\n")
        _write(self.root / "C.md", "see [D](D.md)\n")
        _write(self.root / "D.md", "leads to [E](E.md)\n")
        _write(self.root / "E.md", "leaf node\n")
        _write(self.root / "X.md", "isolated, no links here\n")
        _write(self.root / "F.md", "points at [G](G.md)\n")
        _write(self.root / "G.md", "leaf node\n")
        self.files = sorted(self.root.glob("*.md"))
        self.nodes = {p.resolve() for p in self.files}
        self.edges = dg.build_edges(self.files)
        self.inbound = dg.build_inbound(self.edges)
        self.adj = dg.undirected_adj(self.edges)

    def tearDown(self):
        self._tmp.cleanup()

    def n(self, name: str) -> Path:
        return (self.root / f"{name}.md").resolve()

    # -- edge extraction -------------------------------------------------- #
    def test_build_edges_basic(self):
        self.assertEqual(self.edges[self.n("A")], {self.n("B"), self.n("C")})
        self.assertEqual(self.edges[self.n("D")], {self.n("E")})
        # Leaves and isolated nodes have no outbound edges → absent from the map.
        self.assertNotIn(self.n("E"), self.edges)
        self.assertNotIn(self.n("X"), self.edges)

    def test_edges_ignore_fences_inline_nonmd_http_and_keep_fragments(self):
        _write(self.root / "tricky.md", "\n".join([
            "real link to [D](D.md#a-heading)",      # fragment stripped -> edge to D
            "```",
            "[E](E.md) inside a fence is ignored",   # fenced -> not an edge
            "```",
            "inline `[E](E.md)` is ignored",         # inline code -> not an edge
            "a [code ref](../Main/Features/Foo.cs)", # non-.md -> not an edge
            "an [external](https://example.com/E.md)",  # http -> not an edge
            "",
        ]))
        edges = dg.build_edges([self.root / "tricky.md"])
        self.assertEqual(edges.get((self.root / "tricky.md").resolve()), {self.n("D")})

    def test_footer_links_excluded_when_strip_footer(self):
        _write(self.root / "H.md", "\n".join([
            "body link [D](D.md)",
            "",
            dg.START_MARKER,
            "",
            "## Referenced by",
            "",
            "- [E](E.md)",
            "",
            dg.END_MARKER,
            "",
        ]))
        with_footer = dg.build_edges([self.root / "H.md"], strip_footer=False)
        without_footer = dg.build_edges([self.root / "H.md"], strip_footer=True)
        h = (self.root / "H.md").resolve()
        self.assertEqual(with_footer[h], {self.n("D"), self.n("E")})
        self.assertEqual(without_footer[h], {self.n("D")})

    def test_self_link_dropped(self):
        _write(self.root / "selfy.md", "[me](selfy.md) and [D](D.md)\n")
        edges = dg.build_edges([self.root / "selfy.md"])
        self.assertEqual(edges[(self.root / "selfy.md").resolve()], {self.n("D")})

    def test_eligible_filter_excludes_both_endpoints(self):
        # eligible=False for any node named with 'C' drops C as source AND target.
        edges = dg.build_edges(self.files, eligible=lambda p: "C" not in p.stem)
        self.assertNotIn(self.n("C"), edges)                 # C dropped as source
        self.assertEqual(edges[self.n("A")], {self.n("B")})  # A->C dropped as target

    # -- inbound / degrees ------------------------------------------------ #
    def test_build_inbound(self):
        self.assertEqual(self.inbound[self.n("D")], {self.n("B"), self.n("C")})
        self.assertEqual(self.inbound[self.n("E")], {self.n("D")})
        self.assertNotIn(self.n("A"), self.inbound)  # nothing points at A

    def test_degrees(self):
        deg = dg.degrees(self.nodes, self.edges, self.inbound)
        self.assertEqual(deg[self.n("D")], {"in": 2, "out": 1, "total": 3})
        self.assertEqual(deg[self.n("X")], {"in": 0, "out": 0, "total": 0})

    # -- explain ---------------------------------------------------------- #
    def test_explain_node_neighbourhood(self):
        out = dg.explain_node(self.n("D"), self.edges, self.inbound)
        self.assertEqual(out["in_degree"], 2)
        self.assertEqual(out["out_degree"], 1)
        self.assertEqual({x["name"] for x in out["inbound"]}, {"B", "C"})
        self.assertEqual({x["name"] for x in out["outbound"]}, {"E"})

    # -- path ------------------------------------------------------------- #
    def test_shortest_path_length_and_endpoints(self):
        out = dg.shortest_path(self.adj, self.n("A"), self.n("E"))
        self.assertTrue(out["found"])
        self.assertEqual(out["length"], 4)
        self.assertEqual(out["path"][0], "A")
        self.assertEqual(out["path"][-1], "E")

    def test_path_none_across_components(self):
        out = dg.shortest_path(self.adj, self.n("A"), self.n("X"))
        self.assertFalse(out["found"])
        self.assertEqual(out["path"], [])

    def test_path_identity_start_equals_goal(self):
        out = dg.shortest_path(self.adj, self.n("A"), self.n("A"))
        self.assertTrue(out["found"])
        self.assertEqual(out["length"], 1)
        self.assertEqual(out["path"], ["A"])

    def test_path_undirected_symmetry(self):
        fwd = dg.shortest_path(self.adj, self.n("A"), self.n("E"))
        rev = dg.shortest_path(self.adj, self.n("E"), self.n("A"))
        self.assertEqual(fwd["length"], rev["length"])

    def test_path_directed_respects_arrow(self):
        # Directed: A reaches E (A->B->D->E); E cannot reach A.
        self.assertTrue(dg.shortest_path(self.edges, self.n("A"), self.n("E"))["found"])
        self.assertFalse(dg.shortest_path(self.edges, self.n("E"), self.n("A"))["found"])

    # -- components / bridges --------------------------------------------- #
    def test_connected_components(self):
        comps = dg.connected_components(self.nodes, self.adj)
        self.assertEqual(len(comps), 3)
        self.assertEqual(sorted(len(c) for c in comps), [1, 2, 5])

    def test_bridges(self):
        br = dg.bridges(self.nodes, self.adj)
        as_sets = {frozenset((a.stem, b.stem)) for a, b in br}
        self.assertEqual(as_sets, {frozenset({"D", "E"}), frozenset({"F", "G"})})
        self.assertNotIn(frozenset({"A", "B"}), as_sets)  # A reaches B via C-D

    # -- metrics ---------------------------------------------------------- #
    def test_graph_metrics_shape_and_god_node(self):
        m = dg.graph_metrics(self.nodes, self.edges, self.inbound, self.adj, top=1)
        self.assertEqual(m["node_count"], 8)
        self.assertEqual(m["component_count"], 3)
        self.assertEqual(m["god_nodes"][0]["name"], "D")  # highest total degree
        self.assertEqual({x["name"] for x in m["orphans"]}, {"X"})  # only isolated node
        bridge_sets = {frozenset((b["a"], b["b"])) for b in m["bridges"]}
        self.assertEqual(bridge_sets, {frozenset({"D", "E"}), frozenset({"F", "G"})})

    # -- node resolution -------------------------------------------------- #
    def test_resolve_node_by_stem_name_and_relpath(self):
        self.assertEqual(dg.resolve_node("D", self.nodes), [self.n("D")])
        self.assertEqual(dg.resolve_node("D.md", self.nodes), [self.n("D")])
        self.assertEqual(dg.resolve_node("nope", self.nodes), [])

    def test_resolve_node_ambiguous_returns_all_candidates(self):
        sub = self.root / "sub"
        _write(sub / "D.md", "another D\n")
        nodes = self.nodes | {(sub / "D.md").resolve()}
        cands = dg.resolve_node("D", nodes)
        self.assertEqual(len(cands), 2)  # root/D.md and sub/D.md both match the stem

    def test_resolve_node_trailing_path_segment(self):
        # A partial repo-relative path (`features/career-system.md`) must resolve —
        # the form the CLI docstring + error message advertise. The leading "/"
        # anchor keeps it from matching a bare substring.
        target = self.root / "docs" / "features" / "career-system.md"
        _write(target, "x\n")
        nodes = {target.resolve()}
        self.assertEqual(dg.resolve_node("features/career-system.md", nodes), [target.resolve()])
        self.assertEqual(dg.resolve_node("features/career-system", nodes), [target.resolve()])
        self.assertEqual(dg.resolve_node("career-system", nodes), [target.resolve()])

    def test_mutual_links_recorded_both_directions(self):
        _write(self.root / "P.md", "[Q](Q.md)\n")
        _write(self.root / "Q.md", "[P](P.md)\n")
        edges = dg.build_edges([self.root / "P.md", self.root / "Q.md"])
        p, q = (self.root / "P.md").resolve(), (self.root / "Q.md").resolve()
        self.assertEqual(edges[p], {q})
        self.assertEqual(edges[q], {p})
        # A mutual pair is a single undirected edge — and a bridge (removing it
        # splits P from Q).
        br = {frozenset((a.stem, b.stem)) for a, b in dg.bridges({p, q}, dg.undirected_adj(edges))}
        self.assertEqual(br, {frozenset({"P", "Q"})})


if __name__ == "__main__":
    unittest.main(verbosity=2)
