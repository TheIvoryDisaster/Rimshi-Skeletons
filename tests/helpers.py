"""Shared helpers for the offline tests: repo paths, XML def loading, and the test log reader module."""
import glob, importlib.util, os, xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "Source", "RimshiSkeletons")
TEXTURES = os.path.join(ROOT, "Textures")


def xml_files(*folders):
    files = []
    for folder in folders:
        files += glob.glob(os.path.join(ROOT, folder, "**", "*.xml"), recursive=True)
    return sorted(files)


def defs():
    """Every def element in Defs/, as (file, element). Abstract parents are included; check the Abstract attribute."""
    out = []
    for path in xml_files("Defs"):
        for el in ET.parse(path).getroot():
            out.append((os.path.relpath(path, ROOT), el))
    return out


def concrete(tag=None):
    return [(f, el) for f, el in defs() if el.get("Abstract") != "True" and (tag is None or el.tag == tag)]


def patch_names():
    """Name attributes our patches give to vanilla defs (PatchOperationAttributeAdd), so our defs can inherit from them."""
    names = set()
    for path in xml_files("Patches"):
        for op in ET.parse(path).getroot().iter("Operation"):
            if op.get("Class") == "PatchOperationAttributeAdd" and op.findtext("attribute") == "Name":
                names.add(op.findtext("value"))
    return names


def def_names(tag):
    return {el.findtext("defName") for _, el in concrete(tag)}


def texture_exists(path):
    """True when a texture path (as a def writes it, no extension) has a PNG under Textures/."""
    return os.path.isfile(os.path.join(TEXTURES, path + ".png"))


def source_text():
    texts = {}
    for path in glob.glob(os.path.join(SOURCE, "**", "*.cs"), recursive=True):
        if os.sep + "obj" + os.sep in path or os.sep + "bin" + os.sep in path:
            continue
        texts[os.path.relpath(path, ROOT)] = open(path, encoding="utf-8").read()
    return texts


def testlog_module():
    spec = importlib.util.spec_from_file_location("rimshi_testlog", os.path.join(ROOT, "tools", "rimshi_testlog.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod
