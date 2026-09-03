"""Audit embedded Blender text blocks without executing them."""

import hashlib
import json
import re

import bpy


SUSPICIOUS_PATTERN = re.compile(
    r"\b(os\.|subprocess|socket|requests|urllib|http[s]?://|open\s*\(|"
    r"exec\s*\(|eval\s*\(|shutil|pathlib|ctypes|winreg|powershell|cmd\.exe)",
    re.IGNORECASE,
)

report = []
for text in bpy.data.texts:
    source = text.as_string()
    matches = []
    for line_number, line in enumerate(source.splitlines(), start=1):
        if SUSPICIOUS_PATTERN.search(line):
            matches.append({"line": line_number, "text": line.strip()[:300]})
    report.append(
        {
            "name": text.name,
            "characters": len(source),
            "lines": len(source.splitlines()),
            "sha256": hashlib.sha256(source.encode("utf-8")).hexdigest(),
            "suspicious_matches": matches,
        }
    )

print("===BLENDER_EMBEDDED_SCRIPT_AUDIT===")
print(json.dumps(report, ensure_ascii=False, indent=2))
print("===END_BLENDER_EMBEDDED_SCRIPT_AUDIT===")
