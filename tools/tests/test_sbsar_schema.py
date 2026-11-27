#!/usr/bin/env python3
"""Unit tests for tools/painter/sbsar_schema.py, without Painter.

Run:  python -m unittest tools.tests.test_sbsar_schema

The parser reads the XML description that ships inside an .sbsar archive. A small synthetic
description stands in for the vendor files, so the tests need no Painter and no 7-Zip.
"""
import importlib.util
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "painter", "sbsar_schema.py")
_spec = importlib.util.spec_from_file_location("sbsar_schema", SCRIPT)
ss = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ss)

XML = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE sbsdescription SYSTEM "sbsdescription.dtd">
<sbsdescription formatversion="2.1" asmuid="1" content="full">
 <graphs count="1">
  <graph pkgurl="pkg://demo_stone" label="Demo Stone" graphtype="material" category="Stone"
         physicalSize="100,100,6" primaryinput="curvature" keywords="a;b">
   <sbspresets count="1">
    <sbspreset label="Dry">
     <presetinput identifier="stone_color" type="2" uid="9" value="0.5,0.25,0.125"></presetinput>
     <presetinput identifier="rough" type="0" uid="8" value="0.87"></presetinput>
    </sbspreset>
   </sbspresets>
   <guigroups>
    <guigroup identifier="Pattern" label="Pattern Group"/>
   </guigroups>
   <outputs count="2">
    <output uid="1" identifier="basecolor" type="5" format="0">
     <outputgui label="Base Color" group="Material" typegui="image"><channels><channel names="baseColor"/></channels></outputgui>
    </output>
    <output uid="2" identifier="normal" usertag="disable=(basecolor)" type="5" format="64">
     <outputgui label="Normal" typegui="image"><channels/></outputgui>
    </output>
   </outputs>
   <inputs count="7">
    <input uid="10" identifier="$outputsize" type="8" default="8,8" alteroutputs="1,2" alternodes="0"/>
    <input uid="11" identifier="$randomseed" type="4" default="0" alteroutputs="1" alternodes="0"/>
    <input uid="12" identifier="stone_color" type="2" default="0.35,0.36,0.37" alteroutputs="1">
     <inputgui widget="color" label="Stone Color" showas="tweak"><channels/></inputgui>
    </input>
    <input uid="13" identifier="rough" type="0" default="0.52" alteroutputs="2">
     <inputgui widget="slider" label="Rough" showas="tweak" group="Pattern">
      <guislider min="0" max="1" clamp="on" step="0.01"/><channels/>
     </inputgui>
    </input>
    <input uid="14" identifier="pattern_type" type="4" default="2" alteroutputs="1,2">
     <inputgui widget="combobox" label="Type" group="Pattern" visibleif="input[&quot;rough&quot;]!=1">
      <guicombobox>
       <guicomboboxitem value="1" text="Custom"/>
       <guicomboboxitem value="2" text="Square"/>
      </guicombobox><channels/>
     </inputgui>
    </input>
    <input uid="15" identifier="invert" type="4" default="1" alteroutputs="1">
     <inputgui widget="togglebutton" label="Invert" showas="tweak"><guibutton/><channels/></inputgui>
    </input>
    <input uid="16" identifier="curvature" type="5" alteroutputs="1">
     <inputgui widget="image" label="Curvature" showas="pin">
      <guiimage colortype="grayscale" format="fixed"><channels><channel names="curvature"/></channels></guiimage>
      <channels><channel names="curvature"/></channels>
     </inputgui>
    </input>
   </inputs>
  </graph>
 </graphs>
</sbsdescription>
"""


class ParseDescriptionTests(unittest.TestCase):
    def setUp(self):
        self.schema = ss.parse_description(XML)
        self.params = {p["identifier"]: p for p in self.schema["params"]}

    def test_graph_header(self):
        s = self.schema
        self.assertEqual(s["identifier"], "demo_stone")
        self.assertEqual(s["label"], "Demo Stone")
        self.assertEqual(s["graphtype"], "material")
        self.assertEqual(s["category"], "Stone")
        self.assertEqual(s["physical_size_cm"], [100.0, 100.0, 6.0])
        self.assertEqual(s["primary_input"], "curvature")

    def test_slider_has_range_group_and_affected_outputs(self):
        p = self.params["rough"]
        self.assertEqual((p["type"], p["widget"]), ("float", "slider"))
        self.assertEqual((p["min"], p["max"], p["step"], p["clamp"]), (0.0, 1.0, 0.01, True))
        self.assertEqual(p["default"], 0.52)
        self.assertEqual(p["label"], "Rough")
        self.assertEqual(p["group"], "Pattern Group")
        self.assertEqual(p["affects"], ["normal"])

    def test_vector_slider_range_is_a_list(self):
        xml = XML.replace('<guislider min="0" max="1" clamp="on" step="0.01"/>',
                          '<guislider min="0,0" max="1,2" step="0.5,0.5"/>')
        p = {q["identifier"]: q for q in ss.parse_description(xml)["params"]}["rough"]
        self.assertEqual((p["min"], p["max"], p["step"]), ([0.0, 0.0], [1.0, 2.0], [0.5, 0.5]))

    def test_color_default_is_a_float_triple(self):
        p = self.params["stone_color"]
        self.assertEqual(p["type"], "float3")
        self.assertEqual(p["widget"], "color")
        self.assertEqual(p["default"], [0.35, 0.36, 0.37])

    def test_combobox_values_and_visibility(self):
        p = self.params["pattern_type"]
        self.assertEqual(p["widget"], "combobox")
        self.assertEqual(p["enum"], [{"value": 1, "text": "Custom"}, {"value": 2, "text": "Square"}])
        self.assertEqual(p["visible_if"], 'input["rough"]!=1')

    def test_toggle_is_an_int_flagged_boolean(self):
        p = self.params["invert"]
        self.assertEqual((p["type"], p["widget"], p["default"]), ("int", "togglebutton", 1))

    def test_image_inputs_are_split_from_params(self):
        self.assertNotIn("curvature", self.params)
        self.assertEqual(self.schema["image_inputs"],
                         [{"identifier": "curvature", "label": "Curvature",
                           "channels": ["curvature"], "colortype": "grayscale"}])

    def test_seed_and_size_are_flagged_not_listed(self):
        self.assertNotIn("$randomseed", self.params)
        self.assertNotIn("$outputsize", self.params)
        self.assertTrue(self.schema["has_random_seed"])
        self.assertEqual(self.schema["output_size_default_log2"], [8, 8])

    def test_outputs_and_presets(self):
        self.assertEqual([o["identifier"] for o in self.schema["outputs"]], ["basecolor", "normal"])
        self.assertEqual(self.schema["outputs"][1]["usertag"], "disable=(basecolor)")
        self.assertEqual(self.schema["outputs"][0]["label"], "Base Color")
        self.assertEqual(self.schema["outputs"][0]["channels"], ["baseColor"])
        self.assertEqual(self.schema["outputs"][1]["channels"], [])
        self.assertEqual(self.schema["presets"],
                         [{"label": "Dry", "values": {"stone_color": [0.5, 0.25, 0.125], "rough": 0.87}}])

    def test_affects_names_outputs_not_uids(self):
        self.assertEqual(self.params["stone_color"]["affects"], ["basecolor"])

    def test_params_keep_file_order(self):
        self.assertEqual(list(self.params), ["stone_color", "rough", "pattern_type", "invert"])


class SafetyTests(unittest.TestCase):
    def test_entity_declarations_are_refused(self):
        bomb = XML.replace("<sbsdescription", '<!ENTITY a "b"><sbsdescription', 1)
        with self.assertRaises(ValueError):
            ss.parse_description(bomb)

    def test_no_graph_is_an_error(self):
        with self.assertRaises(ValueError):
            ss.parse_description("<sbsdescription><graphs count='0'/></sbsdescription>")


class FindXmlTests(unittest.TestCase):
    def test_picks_the_description_not_the_icon(self):
        files = ["a/icon.png", "a/x.sbsasm", "a/x.xml"]
        self.assertEqual(ss.pick_description(files), "a/x.xml")

    def test_no_xml_raises(self):
        with self.assertRaises(FileNotFoundError):
            ss.pick_description(["a/x.sbsasm"])


if __name__ == "__main__":
    unittest.main()
