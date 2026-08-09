import re, unicodedata, zipfile
from pathlib import Path
from xml.etree import ElementTree as ET
from datetime import time
import psycopg2, psycopg2.extras

DOC = Path(__file__).resolve().parent.parent / "HORARIOS DE DOCENTES  2026 QUENA.docx"
CANONICAL = "f7ccb57f-fa3e-4d9f-973b-552030c9852d"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
DAY_MAP = {"LUNES": 1, "MARTES": 2, "MIERCOLES": 3, "MIÉRCOLES": 3, "JUEVES": 4, "VIERNES": 5}
BLOCK_TIMES = [
    (time(18, 10), time(19, 0)), (time(19, 0), time(19, 50)), (time(19, 50), time(20, 40)),
    (time(20, 40), time(21, 30)), (time(21, 30), time(22, 20)), (time(22, 20), time(23, 10)),
]

# Mapeo manual docente Word -> user_id (desde BD)
TEACHER_MAP = {
    "ABREGO": "8d3f8a98-7149-4045-8dd9-e116d65c8efc",
    "AGAMES": "54fce6e6-55a9-4023-8f47-6f456729c38c",
    "ASPRILLA": "2aa514a8-b721-453f-8b7b-6d10c3923212",
    "MENDOZA": "7eb9674d-40c6-4dfd-81bb-ab9759f8ec58",
    "DEL CID": "81751dc8-ddcf-4d11-9cea-ec5bd34792d0",
    "ESPAÑA": "c06d17b0-22eb-48ac-a957-afad87f35234",
    "ESPANA": "c06d17b0-22eb-48ac-a957-afad87f35234",
    "GARCIA": "6b5e25fb-762f-4a55-833c-ab35f5b36f56",
    "HERNANDEZ": "84c339b4-61de-47e8-8411-27e5c20c1b22",
    "MORA": "d41b4e10-0a10-404a-86e9-72ab6c73d024",
    "REYES": "f29ee345-1671-469d-bbaf-23adf4f46e4f",
    "ROBLES": "2210619e-1c8c-4dc1-b1cb-db66333592e9",
    "VASQUEZ": "cee41a3f-2de5-4211-86f3-30d776e5fedb",
    "VÁSQUEZ": "cee41a3f-2de5-4211-86f3-30d776e5fedb",
}

def bucket(name):
    u = name.upper()
    for k in TEACHER_MAP:
        if k in u.replace("Á", "A"):
            return k
    return None

def cell_text(el):
    return "".join(t.text or "" for t in el.iter(W + "t")).strip()

def parse_unique():
    with zipfile.ZipFile(DOC) as z:
        root = ET.fromstring(z.read("word/document.xml"))
    body = root.find(W + "body")
    current = None
    unique = {}
    raw = 0
    for child in body:
        tag = child.tag.split("}")[-1]
        if tag == "p":
            m = re.search(r"PROFESOR[^:]*:\s*(.+)", cell_text(child), re.I)
            if m:
                current = re.sub(r"^\)\s*", "", m.group(1).strip().split("HORAS")[0].strip())
        elif tag == "tbl" and current and bucket(current):
            rows = [[cell_text(tc) for tc in tr.findall(W + "tc")] for tr in child.findall(W + "tr")]
            hidx = next((i for i, r in enumerate(rows) if any("LUNES" in c.upper() for c in r)), None)
            if hidx is None:
                continue
            hdr = rows[hidx]
            cols = {ci: DAY_MAP[d] for ci, c in enumerate(hdr) for d in DAY_MAP if d in c.upper().replace("É", "E")}
            for r in rows[hidx + 1:]:
                if not r or not re.search(r"ira|\d+(ra|da|ta)", r[0].lower()):
                    continue
                bidx = 0 if r[0].lower().startswith("ira") else int(re.match(r"(\d+)", r[0].lower()).group(1)) - 1
                for ci, day in cols.items():
                    if ci < len(r) and r[ci].strip():
                        raw += 1
                        b = bucket(current)
                        key = (b, day, bidx)
                        if key not in unique:
                            unique[key] = {"teacher": current, "bucket": b, "day": day, "block_idx": bidx, "cell": r[ci].strip()}
    return raw, list(unique.values())

raw, unique = parse_unique()
print(f"Word: {raw} celdas, {len(unique)} slots únicos\n")

conn = psycopg2.connect(
    host="dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com",
    database="schoolmanager_daqf",
    user="admin", password="iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r",
    port=5432, sslmode="require",
)
cur = conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor)

cur.execute("""
SELECT ts.id, ts.start_time, ts.end_time FROM time_slots ts
LEFT JOIN shifts s ON ts.shift_id=s.id
WHERE ts.is_active=true AND (s.name ILIKE '%noche%' OR ts.start_time>='18:00')
ORDER BY ts.display_order, ts.start_time
""")
slot_by_time = {(r["start_time"], r["end_time"]): str(r["id"]) for r in cur.fetchall()}

cur.execute("""
SELECT ta.teacher_id::text, se.day_of_week, se.time_slot_id::text, subj.name subject, g.name grp
FROM schedule_entries se
JOIN teacher_assignments ta ON se.teacher_assignment_id=ta.id
JOIN subject_assignments sa ON ta.subject_assignment_id=sa.id
JOIN subjects subj ON sa.subject_id=subj.id
JOIN groups g ON sa.group_id=g.id
WHERE se.academic_year_id=%s
""", (CANONICAL,))
loaded = cur.fetchall()
print(f"BD: {len(loaded)} horarios\n")

by_bucket_doc = {}
for u in unique:
    sid = slot_by_time.get(BLOCK_TIMES[u["block_idx"]])
    if sid:
        by_bucket_doc.setdefault(u["bucket"], {})[(u["day"], sid)] = u["cell"]

by_bucket_bd = {}
for r in loaded:
    # reverse map teacher_id to bucket
    b = next((k for k,v in TEACHER_MAP.items() if v == r["teacher_id"]), None)
    if b:
        by_bucket_bd.setdefault(b, set()).add((r["day_of_week"], r["time_slot_id"]))

total_doc = total_ok = total_miss = 0
all_missing = []

for b in sorted(set(by_bucket_doc) | set(by_bucket_bd)):
    doc_slots = by_bucket_doc.get(b, {})
    bd_slots = by_bucket_bd.get(b, set())
    missing = set(doc_slots.keys()) - bd_slots
    ok = set(doc_slots.keys()) & bd_slots
    total_doc += len(doc_slots)
    total_ok += len(ok)
    total_miss += len(missing)
    name = next((u["teacher"] for u in unique if u["bucket"] == b), b)
    print(f"{name}")
    print(f"  Word: {len(doc_slots)} | BD: {len(bd_slots)} | Cargados: {len(ok)} | Faltan: {len(missing)}")
    for day, sid in sorted(missing):
        all_missing.append((name, day, doc_slots[(day, sid)]))
        print(f"    día {day}: {doc_slots[(day,sid)]}")

print(f"\n=== RESUMEN ===")
print(f"Slots únicos Word:  {total_doc}")
print(f"Cargados:           {total_ok}")
print(f"Faltantes:          {total_miss}")
print(f"Cobertura:          {100*total_ok/total_doc:.1f}%")
print(f"Duplicados Word:    {raw - len(unique)}")
conn.close()
