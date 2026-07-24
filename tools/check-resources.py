"""Checks the localization resource file for the two mistakes the compiler will not catch.

Run from the repository root:  python tools/check-resources.py

1. Case-insensitive duplicate names. MSBuild matches resource names without regard to case,
   so "Views" and "views" are the same resource — it keeps one, drops the other with a
   warning buried in the build output, and the dropped string renders in English forever.
   That is invisible in review and hard to spot on a page that is otherwise translated.

2. Placeholder mismatches. A value using an index the name doesn't supply throws
   FormatException when that page renders; a value dropping one silently loses information.
"""
import collections
import re
import sys
import xml.etree.ElementTree as ET

PATH = "RealEstatePortal.Web/Resources/SharedResource.tr.resx"

placeholder = re.compile(r"\{(\d+)\}")
problems = []
names = []

try:
    root = ET.parse(PATH).getroot()
except ET.ParseError as error:
    # Usually an unescaped " or & in a name attribute — a key is English text, so it can contain
    # either. They have to be written as &quot; and &amp;.
    print("%s is not well-formed XML: %s" % (PATH, error))
    sys.exit(1)

for data in root.findall("data"):
    name = data.get("name")
    value_el = data.find("value")
    if name is None:
        continue
    names.append(name)

    if value_el is None or not (value_el.text or "").strip():
        problems.append(("empty value", name))
        continue
    value = value_el.text

    in_name = set(placeholder.findall(name))
    in_value = set(placeholder.findall(value))

    if in_value - in_name:
        problems.append(("value uses {%s}, which the name does not supply"
                         % ",".join(sorted(in_value - in_name)), name))
    if in_name - in_value:
        problems.append(("value drops {%s}" % ",".join(sorted(in_name - in_value)), name))

    # A brace that is not part of a placeholder also breaks string.Format.
    if "{" in placeholder.sub("", value) or "}" in placeholder.sub("", value):
        problems.append(("stray brace", name))

by_lower = collections.defaultdict(list)
for name in names:
    by_lower[name.lower()].append(name)

for variants in by_lower.values():
    if len(variants) > 1:
        problems.append(("names differ only by case: " + " / ".join(variants), variants[0]))

print("checked %d entries in %s" % (len(names), PATH))
for why, name in problems:
    print("  FAIL [%s]: %s" % (why, name))
print("OK" if not problems else "%d problem(s)" % len(problems))
sys.exit(1 if problems else 0)
