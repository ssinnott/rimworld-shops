#!/usr/bin/env python3
"""
Static sanity checks for the mod, standing in for a launch of the game.

RimWorld resolves XML into C# types and def references at load time, and reports
failures as red errors in-game rather than at build time. This checks the three
things that most often go wrong:

  1. every C# type named in XML actually exists (in this mod, or in the game);
  2. every def this mod references by name resolves, or is a known vanilla def;
  3. every .Translate() key used in C# has an English keyed string.

Usage: python3 tools/validate_defs.py [--refdump PATH]
Exit code is non-zero if anything failed.
"""

import glob
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOD_NAMESPACE = "OldWestTown"

# XML elements whose text is a C# type name.
TYPE_ELEMENTS = {"giverClass", "workerClass", "driverClass", "thingClass", "compClass",
                 "workerClass", "designatorClass", "ticketClass"}

failures = []
notes = []


def fail(msg):
    failures.append(msg)


def xml_files(*globs):
    out = []
    for g in globs:
        out.extend(glob.glob(os.path.join(ROOT, g), recursive=True))
    return sorted(out)


# --------------------------------------------------------------------- types

def collect_source_types():
    """Fully-qualified type names declared in this mod's C# sources."""
    types = set()
    for path in glob.glob(os.path.join(ROOT, "Source/**/*.cs"), recursive=True):
        text = open(path, encoding="utf-8").read()
        ns = re.search(r"^namespace\s+([\w.]+)", text, re.M)
        ns = ns.group(1) if ns else ""
        for m in re.finditer(r"^\s*(?:public|internal)\s+(?:static\s+|abstract\s+|sealed\s+)*"
                             r"(?:class|struct|enum)\s+(\w+)", text, re.M):
            types.add(f"{ns}.{m.group(1)}" if ns else m.group(1))
    return types


def find_refdump():
    """
    Locate the refdump helper that answers "does this vanilla type exist?".

    Explicit --refdump wins, then a system install, then a build of tools/refdump in this
    repo. Returning None is fine and expected on a machine that has never built it: the
    type check downgrades to a note rather than failing, exactly as it did when there was
    no such tool at all.
    """
    if "--refdump" in sys.argv:
        return sys.argv[sys.argv.index("--refdump") + 1].split()
    if os.path.exists("/usr/local/bin/refdump"):
        return ["/usr/local/bin/refdump"]

    built = glob.glob(os.path.join(ROOT, "tools/refdump/bin/*/net*/refdump.dll"))
    if built:
        return ["dotnet", sorted(built)[-1]]
    return None


def game_type_exists(name, refdump):
    """Ask the reference assemblies whether a vanilla type exists."""
    if not refdump:
        return None  # can't check
    short = name.rsplit(".", 1)[-1]
    try:
        proc = subprocess.run(refdump + ["=" + short], capture_output=True, text=True,
                              timeout=120)
    except Exception:
        return None
    # refdump exits 0 when every query resolved and 1 when one did not. Anything else
    # (a missing package, a load failure) means it could not answer, which is not the same
    # as a "no" and must not fail the build.
    if proc.returncode not in (0, 1):
        return None
    return proc.returncode == 0


def collect_xml_type_refs():
    """(file, type name) for every C# type named anywhere in the mod's XML."""
    refs = []
    for path in xml_files("Defs/**/*.xml"):
        tree = ET.parse(path)
        rel = os.path.relpath(path, ROOT)
        for el in tree.iter():
            cls = el.get("Class")
            if cls:
                refs.append((rel, cls))
            if el.tag in TYPE_ELEMENTS and el.text and el.text.strip():
                refs.append((rel, el.text.strip()))
        # inspectorTabs entries are bare <li> type names
        for tabs in tree.iter("inspectorTabs"):
            for li in tabs.findall("li"):
                if li.text and li.text.strip():
                    refs.append((rel, li.text.strip()))
    return refs


def check_types(refdump):
    declared = collect_source_types()
    for rel, name in collect_xml_type_refs():
        if name.startswith(MOD_NAMESPACE + "."):
            if name not in declared:
                fail(f"{rel}: XML names '{name}', which no C# class in Source/ declares")
        else:
            exists = game_type_exists(name, refdump)
            if exists is False:
                fail(f"{rel}: XML names '{name}', which is not a type in RimWorld's assemblies")
            elif exists is None:
                notes.append(f"could not verify vanilla type '{name}' (no refdump)")


# ---------------------------------------------------------------------- defs

# Vanilla defNames this mod leans on. Listed explicitly so a typo here is visible
# in review rather than as a red error on the player's first load.
VANILLA_DEFS = {
    "BuildingBase", "ConstructWood", "PositiveEvent", "Misc", "Map_PlayerHome",
    "Woody", "Metallic", "Stony", "Social", "Manipulation", "Talking",
    "Foods", "FoodMeals", "Manufactured", "ResourcesRaw", "Medicine",
    "Apparel", "Textiles", "Leathers", "Drugs", "Weapons",
    "ThinkNode_Priority", "JobGiver_WanderNearDutyLocation",
    "Light", "Medium", "Heavy",
    # Outlaws and the law: OWT_Stickup's <category> (IncidentDef-only) and <letterDef>.
    "ThreatBig",
}


def collect_own_defnames():
    names = set()
    for path in xml_files("Defs/**/*.xml"):
        for el in ET.parse(path).iter("defName"):
            if el.text:
                names.add(el.text.strip())
    return names


# XML elements whose text (or <li> children) are def references this mod makes.
DEF_REF_ELEMENTS = ["shopKind", "workType", "researchPrerequisites", "prerequisites",
                    "defaultStockCategories",
                    "defaultStockThings", "stuffCategories", "relevantSkills",
                    "requiredCapacities", "targetTags", "letterDef",
                    "designationCategory", "constructEffect",
                    "services", "jobDef", "colonistJobDef", "thoughtDef", "affordances"]

# Elements that name a def only under certain def types. <category>, for instance, is an
# IncidentCategoryDef on IncidentDef but the ThingCategory *enum* on ThingDef.
CONDITIONAL_DEF_REF_ELEMENTS = {"category": {"IncidentDef"}}


def check_def_refs():
    known = collect_own_defnames() | VANILLA_DEFS
    for path in xml_files("Defs/**/*.xml"):
        rel = os.path.relpath(path, ROOT)
        # Walk one def at a time so we know which def type each element sits under.
        for def_el in ET.parse(path).getroot():
            def_type = def_el.tag
            tags = list(DEF_REF_ELEMENTS)
            for tag, owners in CONDITIONAL_DEF_REF_ELEMENTS.items():
                if def_type in owners:
                    tags.append(tag)
            for tag in tags:
                for el in def_el.iter(tag):
                    lis = el.findall("li")
                    if lis:
                        values = [li.text.strip() for li in lis if li.text]
                    elif el.text and el.text.strip():
                        values = [el.text.strip()]
                    else:
                        values = []
                    for v in values:
                        if v not in known:
                            fail(f"{rel}: <{def_type}> <{tag}> references def '{v}', which this "
                                 f"mod does not define and which is not in the known-vanilla "
                                 f"list (add it to VANILLA_DEFS if it is genuinely vanilla)")


# ------------------------------------------------------------------ keyed text

def check_translation_keys():
    used = set()
    for path in glob.glob(os.path.join(ROOT, "Source/**/*.cs"), recursive=True):
        text = open(path, encoding="utf-8").read()
        used |= set(re.findall(r'"(OWT_[A-Za-z_]+)"\s*\.Translate', text))

    keyed = set()
    for path in xml_files("Languages/English/Keyed/*.xml"):
        for el in ET.parse(path).getroot():
            keyed.add(el.tag)

    for key in sorted(used - keyed):
        fail(f"Languages/English/Keyed: missing string for key '{key}' used in C#")
    referenced_anywhere = set()
    for path in (glob.glob(os.path.join(ROOT, "Source/**/*.cs"), recursive=True)
                 + xml_files("Defs/**/*.xml")):
        text = open(path, encoding="utf-8").read()
        referenced_anywhere |= set(re.findall(r"(OWT_[A-Za-z_]+)", text))

    for key in sorted(keyed - referenced_anywhere):
        notes.append(f"keyed string '{key}' is defined but never referenced")


# ---------------------------------------------------------------------- main

def main():
    refdump = find_refdump()

    for path in xml_files("Defs/**/*.xml", "Languages/**/*.xml", "About/About.xml"):
        try:
            ET.parse(path)
        except ET.ParseError as e:
            fail(f"{os.path.relpath(path, ROOT)}: malformed XML: {e}")

    if failures:
        report()
        return 1

    check_types(refdump)
    check_def_refs()
    check_translation_keys()
    return report()


def report():
    for n in notes:
        print(f"note: {n}")
    if failures:
        print()
        for f in failures:
            print(f"FAIL: {f}")
        print(f"\n{len(failures)} problem(s) found.")
        return 1
    print("\nAll checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
