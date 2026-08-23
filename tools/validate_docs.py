#!/usr/bin/env python3
"""
Checks that the wiki in docs/ has not drifted away from the mod.

Documentation that lives beside the code still rots, because nothing fails when someone
adds a def and forgets the page describing it. This is that failure. It cannot check that
the prose is *true* — nothing can — but it can check that nothing was added without anyone
thinking about the docs, which is the failure mode that actually happens.

Five checks:

  1. every defName in Defs/ is mentioned somewhere under docs/;
  2. every .cs file under Source/ is named in the code map;
  3. every English translation key is listed in the reference tables;
  4. every relative link between wiki pages resolves, anchors included;
  5. the changelog still has an Unreleased section to write the next line under.

Usage: python3 tools/validate_docs.py
Exit code is non-zero if anything failed.
"""

import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(ROOT, "docs")

CODE_MAP = "architecture.md"
REFERENCE = "reference.md"
CHANGELOG = "changelog.md"
KEYED = "Languages/English/Keyed/OldWestTown.xml"

failures = []


def fail(msg):
    failures.append(msg)


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def doc_pages():
    """Every Markdown page in the wiki, as {filename: text}."""
    pages = {}
    for path in sorted(glob.glob(os.path.join(DOCS, "*.md"))):
        pages[os.path.basename(path)] = read(path)
    return pages


def mentions(text, token):
    """Whole-token match, so OWT_Saloon isn't satisfied by OWT_SaloonBar."""
    return re.search(r"(?<![\w-])" + re.escape(token) + r"(?![\w-])", text) is not None


# ------------------------------------------------------------------ 1. defs

def check_defs(pages):
    everything = "\n".join(pages.values())
    seen = set()

    for path in sorted(glob.glob(os.path.join(ROOT, "Defs/**/*.xml"), recursive=True)):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as err:
            fail(f"{os.path.relpath(path, ROOT)}: could not be parsed ({err})")
            continue

        for node in root.iter("defName"):
            name = (node.text or "").strip()
            if not name or name in seen:
                continue
            seen.add(name)
            if not mentions(everything, name):
                rel = os.path.relpath(path, ROOT)
                fail(f"def {name} ({rel}) is not mentioned anywhere in docs/ — "
                     f"document it on the page it belongs to and in {REFERENCE}")


# ------------------------------------------------------------- 2. source map

def check_source_map(pages):
    code_map = pages.get(CODE_MAP)
    if code_map is None:
        fail(f"docs/{CODE_MAP} is missing — it is the code map every source file is checked against")
        return

    for path in sorted(glob.glob(os.path.join(ROOT, "Source/**/*.cs"), recursive=True)):
        # Build intermediates are gitignored but may exist in a working tree.
        rel = os.path.relpath(path, ROOT)
        if f"{os.sep}obj{os.sep}" in path or f"{os.sep}bin{os.sep}" in path:
            continue
        name = os.path.basename(path)
        if name not in code_map:
            fail(f"{rel} is not listed in docs/{CODE_MAP} — add a row saying what it does")


# ------------------------------------------------------- 3. translation keys

def check_translation_keys(pages):
    reference = pages.get(REFERENCE)
    if reference is None:
        fail(f"docs/{REFERENCE} is missing — it is where translation keys are catalogued")
        return

    path = os.path.join(ROOT, KEYED)
    if not os.path.exists(path):
        fail(f"{KEYED} is missing")
        return

    for node in ET.parse(path).getroot():
        if not isinstance(node.tag, str):   # comments
            continue
        if not mentions(reference, node.tag):
            fail(f"translation key {node.tag} is not listed in docs/{REFERENCE}")


# --------------------------------------------------------------- 4. links

def slugify(heading):
    """Kramdown's auto_id, which is also GitHub's for everything this wiki uses."""
    text = re.sub(r"`|\*\*|\*|_", "", heading).strip()
    text = re.sub(r"^[^a-zA-Z]+", "", text)
    text = re.sub(r"[^a-zA-Z0-9 -]", "", text)
    return text.replace(" ", "-").lower()


def anchors_in(text):
    out = set()
    for line in text.split("\n"):
        m = re.match(r"^(#{1,6})\s+(.*?)\s*$", line)
        if m:
            out.add(slugify(m.group(2)))
    return out


def check_links(pages):
    anchors = {name: anchors_in(text) for name, text in pages.items()}

    # Markdown links, plus the raw <a href> the home page's cards use.
    patterns = [r"\]\(([^)\s]+)\)", r'href="([^"]+)"']

    for name, text in pages.items():
        targets = []
        for pattern in patterns:
            targets.extend(re.findall(pattern, text))

        for target in targets:
            if target.startswith(("http://", "https://", "mailto:", "data:", "{{")):
                continue

            page, _, anchor = target.partition("#")

            if not page:                       # same-page anchor
                page_name = name
            else:
                # The site serves .html; the source is .md. Both spellings appear —
                # relative links between pages, and the card links on the home page.
                page_name = page[:-5] + ".md" if page.endswith(".html") else page
                if page_name == "/":
                    page_name = "index.md"
                page_name = os.path.basename(page_name)

            if page_name not in pages:
                fail(f"docs/{name}: link to '{target}' — no such page")
                continue

            if anchor and anchor not in anchors[page_name]:
                fail(f"docs/{name}: link to '{target}' — {page_name} has no heading '#{anchor}'")


# ----------------------------------------------------------- 5. changelog

def check_changelog(pages):
    text = pages.get(CHANGELOG)
    if text is None:
        fail(f"docs/{CHANGELOG} is missing")
        return
    if not re.search(r"^##\s+Unreleased\s*$", text, re.M):
        fail(f"docs/{CHANGELOG} has no '## Unreleased' section — "
             f"there must always be somewhere to write the next line")


# ------------------------------------------------------------------- nav

def check_nav(pages):
    """Every page is reachable from the sidebar, and every sidebar entry exists."""
    config = os.path.join(DOCS, "_config.yml")
    if not os.path.exists(config):
        fail("docs/_config.yml is missing")
        return

    listed = set(re.findall(r"\{\s*page:\s*([\w.-]+)\s*,", read(config)))
    for name in pages:
        if name[:-3] not in listed:
            fail(f"docs/{name} is not in the wiki_nav list in docs/_config.yml — "
                 f"a page nobody can navigate to is a page nobody reads")
    for page in sorted(listed):
        if f"{page}.md" not in pages:
            fail(f"docs/_config.yml lists '{page}' in wiki_nav, but docs/{page}.md does not exist")


def main():
    if not os.path.isdir(DOCS):
        sys.exit("docs/ is missing — this script checks the wiki that lives there")

    pages = doc_pages()
    check_defs(pages)
    check_source_map(pages)
    check_translation_keys(pages)
    check_links(pages)
    check_changelog(pages)
    check_nav(pages)

    if failures:
        print(f"The wiki has drifted from the mod — {len(failures)} problem(s):\n")
        for msg in failures:
            print(f"  FAIL  {msg}")
        print("\nSee docs/contributing.md for what each check means.")
        return 1

    print(f"Wiki checks passed: {len(pages)} pages.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
