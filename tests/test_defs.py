"""Def cross-references and textures, checked without the game.

Names from vanilla or DLCs that our defs use are listed in VANILLA so a typo in one of ours still fails."""
import os, re, unittest, xml.etree.ElementTree as ET
from helpers import ROOT, SOURCE, concrete, defs, def_names, source_text, texture_exists, xml_files

VANILLA = {
    "GeneDef": {"Neversleep", "Ageless", "Hair_BaldOnly", "Beard_NoBeardOnly"},
    "parents": {"GeneSkinColorOverride", "AverageBase", "FactionBase", "PlayerFactionBase", "BasePlayerPawnKind", "ScenarioBase"},
    "PawnKindDef": {"Villager", "Town_Guard", "Miner", "Farmer"},
}
DIRECTIONS = ("south", "north", "east")


class DefTests(unittest.TestCase):
    def test_all_xml_parses(self):
        for path in xml_files("About", "Defs", "Patches", "Languages") + [ROOT + "/LoadFolders.xml"]:
            with self.subTest(path=path):
                ET.parse(path)

    def test_def_names_prefixed_and_unique(self):
        seen = set()
        for f, el in concrete():
            name = el.findtext("defName")
            with self.subTest(file=f, defName=name):
                self.assertTrue(name and name.startswith("Rimshi_"), "defName must start with Rimshi_")
                self.assertNotIn((el.tag, name), seen, "duplicate defName")
                seen.add((el.tag, name))

    def test_parents_exist(self):
        named = {el.get("Name") for _, el in defs() if el.get("Name")}
        for f, el in defs():
            parent = el.get("ParentName")
            if parent:
                with self.subTest(file=f, parent=parent):
                    self.assertIn(parent, named | VANILLA["parents"])

    def test_xenotype_genes_exist(self):
        genes = def_names("GeneDef") | VANILLA["GeneDef"]
        for f, x in concrete("XenotypeDef"):
            for li in x.find("genes"):
                with self.subTest(xenotype=x.findtext("defName"), gene=li.text):
                    self.assertIn(li.text, genes)

    def test_head_types_and_textures(self):
        heads = {el.findtext("defName"): el.findtext("graphicPath") for _, el in concrete("HeadTypeDef")}
        forced = [li.text for _, g in concrete("GeneDef") for li in (g.find("forcedHeadTypes") or [])]
        self.assertTrue(forced, "no gene forces head types")
        for name in forced:
            with self.subTest(head=name):
                self.assertIn(name, heads)
                for d in DIRECTIONS:
                    self.assertTrue(texture_exists(heads[name] + "_" + d), heads[name] + "_" + d + ".png missing")

    def test_required_genes_exist(self):
        genes = def_names("GeneDef") | VANILLA["GeneDef"]
        for f, el in concrete():
            for li in (el.find("requiredGenes") or []):
                with self.subTest(file=f, gene=li.text):
                    self.assertIn(li.text, genes)
        for _, el in defs():
            for li in (el.find("requiredGenes") or []):
                self.assertIn(li.text, genes)

    def test_body_textures(self):
        utility = open(SOURCE + "/Skeleton/SkeletonUtility.cs").read()
        body = re.search(r'BodyPath = "([^"]+)"', utility).group(1)
        for body_type in ("Male", "Thin", "Hulk"):
            for d in DIRECTIONS:
                with self.subTest(body=body_type, direction=d):
                    self.assertTrue(texture_exists(body + body_type + "_" + d))

    def test_own_icons_exist(self):
        for f, el in concrete():
            icon = el.findtext("iconPath")
            if icon and icon.startswith("Rimshi/"):
                with self.subTest(file=f, icon=icon):
                    self.assertTrue(texture_exists(icon))

    def test_classes_exist(self):
        code = "\n".join(source_text().values())
        files = xml_files("Defs", "Patches")
        for path in files:
            for name in re.findall(r"RimshiSkeletons\.(\w+)", open(path).read()):
                with self.subTest(file=path, cls=name):
                    self.assertRegex(code, r"class %s\b" % name)

    def test_defof_fields_exist(self):
        code = source_text()["Source/RimshiSkeletons/Skeleton/SkeletonUtility.cs"]
        defof = code[code.index("class RimshiDefOf"):code.index("static RimshiDefOf()")]
        for tag, name in re.findall(r"public static (\w+) (Rimshi_\w+);", defof):
            with self.subTest(def_type=tag, defName=name):
                self.assertIn(name, def_names(tag))

    def test_gene_classes_in_patches_names_match(self):
        """Gene ids the C# looks up by string must exist."""
        code = "\n".join(source_text().values())
        for name in re.findall(r'GetNamed(?:SilentFail)?\("(Rimshi_\w+)"\)', code):
            with self.subTest(defName=name):
                self.assertIn(name, {el.findtext("defName") for _, el in concrete()})

    def test_fuel_thought_has_seven_stages(self):
        """ThoughtWorker_NeedFood indexes stages 0 to 6 (hungry, urgently hungry, five malnutrition stages)."""
        for _, t in concrete("ThoughtDef"):
            if t.findtext("workerClass") == "ThoughtWorker_NeedFood":
                self.assertEqual(len(t.find("stages")), 7, t.findtext("defName"))

    def test_faction_pawn_kinds_exist(self):
        kinds = def_names("PawnKindDef") | VANILLA["PawnKindDef"]
        for f, faction in concrete("FactionDef"):
            for kind in [faction.findtext("basicMemberKind")] + [o.tag for g in (faction.find("pawnGroupMakers") or []) for o in (g.find("options") or [])]:
                if kind:
                    with self.subTest(faction=faction.findtext("defName"), kind=kind):
                        self.assertIn(kind, kinds)

    def test_own_references_exist(self):
        """Every Rimshi_ name used inside a def or patch (comments aside) is one of our defs or abstract parents."""
        names = {el.findtext("defName") for _, el in concrete()} | {el.get("Name") for _, el in defs() if el.get("Name")}
        for path in xml_files("Defs", "Patches"):
            text = re.sub(r"<!--.*?-->", "", open(path).read(), flags=re.S)
            for name in set(re.findall(r"\b(Rimshi_[A-Za-z0-9_]+)\b", text)):
                with self.subTest(file=path, name=name):
                    self.assertIn(name, names)

    def test_skeleton_backstories(self):
        stories = [el for _, el in concrete("BackstoryDef") if "Rimshi_Skeleton" in [li.text for li in el.find("spawnCategories")]]
        slots = {el.findtext("slot") for el in stories}
        self.assertEqual(slots, {"Childhood", "Adulthood"}, "skeletons need both childhood and adulthood backstories")

    def test_name_files_exist(self):
        for _, rp in concrete("RulePackDef"):
            for path in [li.findtext("path") for li in rp.iter("li") if li.get("Class") == "Rule_File"]:
                with self.subTest(rulePack=rp.findtext("defName"), path=path):
                    self.assertTrue(os.path.isfile(os.path.join(ROOT, "Languages", "English", "Strings", path + ".txt")))

    def test_faction_icons_exist(self):
        for _, faction in concrete("FactionDef"):
            with self.subTest(faction=faction.findtext("defName")):
                self.assertTrue(texture_exists(faction.findtext("factionIconPath")))


if __name__ == "__main__":
    unittest.main()
