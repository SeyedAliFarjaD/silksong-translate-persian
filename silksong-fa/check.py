import json
from pathlib import Path
d = json.loads(Path("work/en/MainMenu.json").read_text(encoding="utf-8"))
for k in ("PS4_SAVE_IMPORT_LABEL","PS4_SAVE_IMPORT_RESULT_FAILED","DESC_HUD_VISIBILITY","MODAL_NO_SAVE","SAVE_WARNING"):
    v = d[k]
    print(f"{k}: {v!r}")
    print("   codepoints:", [hex(ord(c)) for c in v if ord(c) > 126])
print()
z = json.loads(Path("work/en/Map Zones.json").read_text(encoding="utf-8"))
for k in ("DUSTPENS","HUNTERS_NEST","PILGRIMS_REST","BATHHOUSE","COG_CORE"):
    print(f"{k}: {z[k]!r}")
