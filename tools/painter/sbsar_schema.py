#!/usr/bin/env python3
"""Read the parameter schema of a Substance .sbsar file without Painter.

An .sbsar is a 7-Zip archive. Inside it, one XML file (the sbsdescription) lists every input of the
graph: identifier, label, group, widget, default, range, enum items, and the outputs each input
changes. This is the data behind the parameter panel in Painter, so a recipe can know the legal
names and ranges before it calls SourceSubstance.set_parameters.

    python tools/painter/sbsar_schema.py --work E:/path/to/scratch \\
        "C:/Program Files/Adobe/Adobe Substance 3D Painter/resources/starter_assets/generators/mg_dirt.sbsar"

Prints one JSON object per file (a list when you pass several files). Nothing is written except the
extracted XML in --work. Painter is not called.

Facts used (first seen in the 12.2.0 starter assets, 2026-11-24):
- an input type is 0 float, 1 float2, 2 float3 (colour), 3 float4, 4 int (also the toggle and the
  combobox), 5 image, 8 int2 (the $outputsize pair, as log2);
- `alteroutputs` holds the uids of the outputs that an input changes;
- `$outputsize` and `$randomseed` exist on nearly every graph and have no widget;
- a material graph can carry `physicalSize="w,h,d"` in centimetres.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

TYPE_NAMES = {0: "float", 1: "float2", 2: "float3", 3: "float4", 4: "int", 5: "image", 8: "int2"}
SEVEN_ZIP_CANDIDATES = (r"C:\Program Files\7-Zip\7z.exe", r"C:\Program Files (x86)\7-Zip\7z.exe")


def _numbers(text, cast=float):
    return [cast(x) for x in text.split(",")]


def _value(type_id, text):
    """Turn a default or preset string into a number, a list, or None."""
    if text is None:
        return None
    if type_id == 0:
        return float(text)
    if type_id == 4:
        return int(float(text))
    if type_id in (1, 2, 3):
        return _numbers(text)
    if type_id == 8:
        return _numbers(text, int)
    return text


def _number_or_list(text):
    """A slider bound or step: one number, or a comma list for a vector input."""
    values = _numbers(text)
    return values[0] if len(values) == 1 else values


def pick_description(names):
    """The XML description among the archive members (the icon and the .sbsasm are not it)."""
    for name in names:
        if name.lower().endswith(".xml"):
            return name
    raise FileNotFoundError("no .xml description in the archive: " + ", ".join(names))


def parse_description(xml_text):
    """Parse an sbsdescription into a plain dict (see the tests for the shape)."""
    if "<!ENTITY" in xml_text:
        raise ValueError("entity declarations are not allowed in a description")
    root = ET.fromstring(xml_text)
    graph = root.find("./graphs/graph")
    if graph is None:
        raise ValueError("no graph in the description")
    schema = {
        "identifier": graph.get("pkgurl", "").replace("pkg://", ""),
        "label": graph.get("label"),
        "graphtype": graph.get("graphtype"),
        "category": graph.get("category"),
        "keywords": (graph.get("keywords") or "").split(";") if graph.get("keywords") else [],
        "primary_input": graph.get("primaryinput"),
        "physical_size_cm": _numbers(graph.get("physicalSize")) if graph.get("physicalSize") else None,
    }
    groups = {g.get("identifier"): g.get("label") for g in graph.findall("./guigroups/guigroup")}
    schema["groups"] = [{"identifier": k, "label": v} for k, v in groups.items()]
    outputs, uid_to_output = [], {}
    for out in graph.findall("./outputs/output"):
        gui = out.find("outputgui")
        entry = {"identifier": out.get("identifier"),
                 "label": gui.get("label") if gui is not None else out.get("identifier"),
                 "group": gui.get("group") if gui is not None else None,
                 "usertag": out.get("usertag"),
                 "channels": [c.get("names") for c in out.findall("./outputgui/channels/channel")]}
        outputs.append(entry)
        uid_to_output[out.get("uid")] = entry["identifier"]
    schema["outputs"] = outputs
    params, images = [], []
    schema["has_random_seed"] = False
    schema["output_size_default_log2"] = None
    for inp in graph.findall("./inputs/input"):
        ident = inp.get("identifier")
        type_id = int(inp.get("type"))
        if ident == "$randomseed":
            schema["has_random_seed"] = True
            continue
        if ident == "$outputsize":
            schema["output_size_default_log2"] = _value(8, inp.get("default"))
            continue
        gui = inp.find("inputgui")
        if type_id == 5:
            channels = [c.get("names") for c in inp.findall("./inputgui/guiimage/channels/channel")]
            image = inp.find("./inputgui/guiimage")
            images.append({"identifier": ident,
                           "label": gui.get("label") if gui is not None else ident,
                           "channels": channels,
                           "colortype": image.get("colortype") if image is not None else None})
            continue
        param = {"identifier": ident,
                 "label": gui.get("label") if gui is not None else ident,
                 "group": groups.get(gui.get("group"), gui.get("group")) if gui is not None else None,
                 "widget": gui.get("widget") if gui is not None else None,
                 "type": TYPE_NAMES.get(type_id, "type%d" % type_id),
                 "default": _value(type_id, inp.get("default")),
                 "affects": [uid_to_output[u] for u in (inp.get("alteroutputs") or "").split(",")
                             if u in uid_to_output]}
        if gui is not None and gui.get("visibleif"):
            param["visible_if"] = gui.get("visibleif")
        slider = None if gui is None else (gui.find("guislider") if gui.find("guislider") is not None
                                          else gui.find("guiangle"))
        if slider is not None:
            param["min"] = _number_or_list(slider.get("min", "0"))
            param["max"] = _number_or_list(slider.get("max", "1"))
            if slider.get("step") is not None:
                param["step"] = _number_or_list(slider.get("step"))
            param["clamp"] = slider.get("clamp") == "on"
        items = [] if gui is None else gui.findall("./guicombobox/guicomboboxitem")
        if items:
            param["enum"] = [{"value": int(i.get("value")), "text": i.get("text")} for i in items]
        params.append(param)
    schema["params"] = params
    schema["image_inputs"] = images
    presets = []
    for preset in graph.findall("./sbspresets/sbspreset"):
        values = {p.get("identifier"): _value(int(p.get("type")), p.get("value"))
                  for p in preset.findall("presetinput")}
        presets.append({"label": preset.get("label"), "values": values})
    schema["presets"] = presets
    return schema


def _find_seven_zip():
    found = shutil.which("7z") or next((p for p in SEVEN_ZIP_CANDIDATES if os.path.isfile(p)), None)
    if not found:
        raise FileNotFoundError("7z.exe not found: put it on PATH (an .sbsar is a 7-Zip archive)")
    return found


def read_sbsar(path, work_dir):
    """Extract the XML members of an .sbsar into work_dir/<name> and parse the description."""
    name = os.path.splitext(os.path.basename(path))[0]
    target = os.path.join(work_dir, name)
    os.makedirs(target, exist_ok=True)
    result = subprocess.run([_find_seven_zip(), "x", "-y", "-o" + target, path, "*.xml", "-r"],
                            capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError("7z failed on %s: %s" % (path, result.stdout[-300:] + result.stderr[-300:]))
    members = []
    for folder, _, files in os.walk(target):
        members += [os.path.join(folder, f) for f in files]
    with open(pick_description(members), encoding="utf-8", errors="replace") as handle:
        schema = parse_description(handle.read())
    schema["source"] = path
    return schema


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("sbsar", nargs="+")
    parser.add_argument("--work", required=True, help="scratch folder for the extracted XML (on E:)")
    parser.add_argument("--out", help="write the JSON here instead of stdout")
    opts = parser.parse_args(argv)
    schemas = [read_sbsar(p, opts.work) for p in opts.sbsar]
    text = json.dumps(schemas[0] if len(schemas) == 1 else schemas, indent=1)
    if opts.out:
        with open(opts.out, "w", encoding="utf-8") as handle:
            handle.write(text)
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
