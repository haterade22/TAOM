import unittest

import numpy as np

from tools.hair_engine import baking


class BakePassTests(unittest.TestCase):
    def test_all_twelve_passes_have_explicit_color_contracts(self):
        expected = {"AO", "DIFFUSE", "OPACITY", "NORMAL", "ROOT", "ID", "RANDOM_COLOR",
                    "CLUMP_COLOR", "FLOW", "DIRECTIONAL", "DEPTH", "COLOR"}
        self.assertEqual(set(baking.BAKE_PASSES), expected)
        for name in ("NORMAL", "FLOW", "DIRECTIONAL", "ROOT", "ID", "DEPTH", "AO", "OPACITY"):
            self.assertEqual(baking.BAKE_PASSES[name]["color_space"], "Non-Color")

    def test_unknown_pass_fails(self):
        with self.assertRaisesRegex(ValueError, "pass"):
            baking.pass_settings("UNKNOWN")

    def test_padding_extends_edges(self):
        image = np.array([[[1., 0., 0., 1.], [0., 1., 0., 1.]]])
        padded = baking.pad_image(image, 1)
        self.assertEqual(padded.shape, (3, 4, 4))
        np.testing.assert_allclose(padded[0, 0], image[0, 0])
        np.testing.assert_allclose(padded[-1, -1], image[0, -1])

    def test_padding_neutral_background_is_explicit(self):
        image = np.zeros((1, 1, 4))
        padded = baking.pad_image(image, 1, background=np.array([0.5, 0.5, 1., 1.]))
        np.testing.assert_allclose(padded[0, 0], [0.5, 0.5, 1., 1.])


class ChannelTests(unittest.TestCase):
    def test_separate_and_combine_round_trip(self):
        image = np.array([[[0.1, 0.2, 0.3, 0.4]]])
        channels = baking.separate_rgb(image)
        restored = baking.combine_rgba(**channels, alpha=image[:, :, 3])
        np.testing.assert_allclose(restored, image)

    def test_set_alpha_replaces_only_alpha(self):
        image = np.ones((1, 1, 4)) * 0.5
        result = baking.set_alpha(image, np.zeros((1, 1)))
        np.testing.assert_allclose(result[0, 0], [0.5, 0.5, 0.5, 0.])

    def test_mix_is_linear(self):
        first = np.zeros((1, 1, 4))
        second = np.ones((1, 1, 4))
        np.testing.assert_allclose(baking.mix_rgba(first, second, 0.25), 0.25)


class GraphTests(unittest.TestCase):
    def test_graph_executes_each_input_once_in_dependency_order(self):
        graph = baking.TextureGraph()
        graph.add_input("a", np.zeros((1, 1, 4)))
        graph.add_input("b", np.ones((1, 1, 4)))
        graph.add_mix("mixed", "a", "b", 0.5)
        graph.add_output("out", "mixed")
        result = graph.execute()
        np.testing.assert_allclose(result["out"], 0.5)
        self.assertEqual(graph.execution_counts["a"], 1)
        self.assertEqual(graph.execution_counts["b"], 1)
        self.assertLess(graph.execution_order.index("a"), graph.execution_order.index("mixed"))

    def test_graph_supports_channel_operations(self):
        graph = baking.TextureGraph()
        graph.add_input("source", np.array([[[0.2, 0.4, 0.6, 0.8]]]))
        graph.add_set_alpha("opaque", "source", 1.0)
        graph.add_output("out", "opaque")
        np.testing.assert_allclose(graph.execute()["out"], [[[0.2, 0.4, 0.6, 1.0]]])

    def test_missing_input_and_cycles_fail_closed(self):
        graph = baking.TextureGraph()
        graph.add_output("out", "missing")
        with self.assertRaisesRegex(ValueError, "missing"):
            graph.execute()
        graph = baking.TextureGraph()
        graph.add_mix("a", "b", "b", 0.5)
        graph.add_mix("b", "a", "a", 0.5)
        with self.assertRaisesRegex(ValueError, "cycle"):
            graph.execute()


if __name__ == "__main__":
    unittest.main()
