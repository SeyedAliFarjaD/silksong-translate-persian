import json
from pathlib import Path
base = Path(r"C:\Click - Copy\rea\silksong-fa")
bad = 0
for p in sorted(list((base/"work"/"fa").glob("*.json")) + [base/"glossary.json"] + list((base/"work"/"en").glob("*.json"))):
    try:
        json.loads(p.read_text(encoding="utf-8"))
    except Exception as e:
        bad += 1
        print("INVALID", p.name, e)
g = json.loads((base/"glossary.json").read_text(encoding="utf-8"))
print("glossary sections:", {k: (len(v) if isinstance(v, dict) else "policy") for k, v in g.items()})
print("all JSON valid" if bad == 0 else f"{bad} invalid file(s)")
