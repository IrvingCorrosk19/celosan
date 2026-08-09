"""Exporta detalle para informe premium de carga horarios QUENA."""
import re, zipfile, json
from pathlib import Path
from xml.etree import ElementTree as ET
from datetime import time
import psycopg2, psycopg2.extras

DOC = Path(__file__).resolve().parent.parent / "HORARIOS DE DOCENTES  2026 QUENA.docx"
CANONICAL = "f7ccb57f-fa3e-4d9f-973b-552030c9852d"
OUT = Path(__file__).resolve().parent / "_report_data.json"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
DAY = {1:"Lunes",2:"Martes",3:"Miércoles",4:"Jueves",5:"Viernes"}
DAY_MAP = {"LUNES":1,"MARTES":2,"MIERCOLES":3,"MIÉRCOLES":3,"JUEVES":4,"VIERNES":5}
BLOCKS = ["1ra 18:10-19:00","2da 19:00-19:50","3ra 19:50-20:40","4ta 20:40-21:30","5ta 21:30-22:20","6ta 22:20-23:10"]
BLOCK_TIMES = [(time(18,10),time(19,0)),(time(19,0),time(19,50)),(time(19,50),time(20,40)),(time(20,40),time(21,30)),(time(21,30),time(22,20)),(time(22,20),time(23,10))]
TEACHER_MAP = {
    "ABREGO":"8d3f8a98-7149-4045-8dd9-e116d65c8efc","AGAMES":"54fce6e6-55a9-4023-8f47-6f456729c38c",
    "ASPRILLA":"2aa514a8-b721-453f-8b7b-6d10c3923212","MENDOZA":"7eb9674d-40c6-4dfd-81bb-ab9759f8ec58",
    "DEL CID":"81751dc8-ddcf-4d11-9cea-ec5bd34792d0","ESPAÑA":"c06d17b0-22eb-48ac-a957-afad87f35234",
    "ESPANA":"c06d17b0-22eb-48ac-a957-afad87f35234","GARCIA":"6b5e25fb-762f-4a55-833c-ab35f5b36f56",
    "HERNANDEZ":"84c339b4-61de-47e8-8411-27e5c20c1b22","MORA":"d41b4e10-0a10-404a-86e9-72ab6c73d024",
    "REYES":"f29ee345-1671-469d-bbaf-23adf4f46e4f","ROBLES":"2210619e-1c8c-4dc1-b1cb-db66333592e9",
    "VASQUEZ":"cee41a3f-2de5-4211-86f3-30d776e5fedb","VÁSQUEZ":"cee41a3f-2de5-4211-86f3-30d776e5fedb",
}
ID_TO_NAME = {
    "8d3f8a98-7149-4045-8dd9-e116d65c8efc":"Rey A. Abrego S.",
    "54fce6e6-55a9-4023-8f47-6f456729c38c":"Max E. Agames J.",
    "2aa514a8-b721-453f-8b7b-6d10c3923212":"Lexibel Asprilla",
    "7eb9674d-40c6-4dfd-81bb-ab9759f8ec58":"Fabricio Mendoza",
    "81751dc8-ddcf-4d11-9cea-ec5bd34792d0":"Julio E. Del Cid CH.",
    "c06d17b0-22eb-48ac-a957-afad87f35234":"Jannie M. España W.",
    "6b5e25fb-762f-4a55-833c-ab35f5b36f56":"Lino A. Garcia A.",
    "84c339b4-61de-47e8-8411-27e5c20c1b22":"Zellideth E. Hernández",
    "d41b4e10-0a10-404a-86e9-72ab6c73d024":"Yaribeth Mora",
    "f29ee345-1671-469d-bbaf-23adf4f46e4f":"Zuley E. Reyes G.",
    "2210619e-1c8c-4dc1-b1cb-db66333592e9":"Elvia D. Robles J.",
    "cee41a3f-2de5-4211-86f3-30d776e5fedb":"Carlos A. Vásquez",
}

def bucket(name):
    u = name.upper().replace("Á","A")
    for k in TEACHER_MAP:
        if k in u: return k
    return None

def cell_text(el):
    return "".join(t.text or "" for t in el.iter(W+"t")).strip()

def parse_unique():
    with zipfile.ZipFile(DOC) as z:
        root = ET.fromstring(z.read("word/document.xml"))
    body = root.find(W+"body"); current=None; unique={}; raw=0
    for child in body:
        tag = child.tag.split("}")[-1]
        if tag=="p":
            m=re.search(r"PROFESOR[^:]*:\s*(.+)", cell_text(child), re.I)
            if m: current=re.sub(r"^\)\s*","",m.group(1).strip().split("HORAS")[0].strip())
        elif tag=="tbl" and current and bucket(current):
            rows=[[cell_text(tc) for tc in tr.findall(W+"tc")] for tr in child.findall(W+"tr")]
            hidx=next((i for i,r in enumerate(rows) if any("LUNES" in c.upper() for c in r)),None)
            if hidx is None: continue
            hdr=rows[hidx]; cols={ci:DAY_MAP[d] for ci,c in enumerate(hdr) for d in DAY_MAP if d in c.upper().replace("É","E")}
            for r in rows[hidx+1:]:
                if not r or not re.search(r"ira|\d+(ra|da|ta)", r[0].lower()): continue
                bidx=0 if r[0].lower().startswith("ira") else int(re.match(r"(\d+)",r[0].lower()).group(1))-1
                for ci,day in cols.items():
                    if ci<len(r) and r[ci].strip():
                        raw+=1; b=bucket(current); key=(b,day,bidx)
                        if key not in unique:
                            unique[key]={"teacher":current,"bucket":b,"day":day,"block_idx":bidx,"cell":r[ci].strip()}
    return raw, list(unique.values())

def classify_missing(cell, teacher_name):
    c = cell.upper()
    if re.search(r"\d+\s*I\s*[-]?\s*T\s*[-]?\s*A\s*[-]?\s*E", c) or "I-T-A-E" in c.replace(" ",""):
        return "Código genérico I-T-A-E (año sin grupo/materia específica)"
    if "PRACTICA" in c.upper().replace("Á","A") or "PRÁCTICA" in c.upper():
        return "Práctica Profesional — falta teacher_assignment o conflicto de horario"
    if "MERCAD" in c.upper() or "PUBLIC" in c.upper():
        return "Mercadotecnia — falta asignación docente-materia-grupo en BD"
    if "CIENCIAS" in c.upper() and ("A-2" in c or "A 2" in c):
        return "Ciencias sección A-2 — en BD solo existe grupo 7-A/8-A (sin subsección A-2)"
    if "9-A-2" in c or "7-A-2" in c:
        return "Grupo A-2 pre-media — no existe como grupo separado en el sistema"
    if re.search(r"\d+\s*[-]?\s*[A-Z]\s*[-]?\s*2", c):
        return "Subsección -A-2 del Word no mapeada a grupo en BD"
    return "Conflicto de horario (docente o grupo ya ocupado en ese bloque/día)"

raw, unique = parse_unique()
conn = psycopg2.connect(host="dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com",database="schoolmanager_daqf",user="admin",password="iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r",port=5432,sslmode="require")
cur = conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor)
cur.execute("SELECT ts.id,ts.start_time,ts.end_time FROM time_slots ts LEFT JOIN shifts s ON ts.shift_id=s.id WHERE ts.is_active AND (s.name ILIKE '%noche%' OR ts.start_time>='18:00') ORDER BY ts.display_order,ts.start_time")
slot_by_time = {(r["start_time"],r["end_time"]):str(r["id"]) for r in cur.fetchall()}
slot_to_block = {v:i for i,(k,v) in enumerate([(t,slot_by_time[t]) for t in BLOCK_TIMES if t in slot_by_time])}

cur.execute("""
SELECT ta.teacher_id::text tid, u.name||' '||u.last_name dbname, se.day_of_week, se.time_slot_id::text sid,
       subj.name subject, g.name grp, ts.start_time, ts.end_time
FROM schedule_entries se
JOIN teacher_assignments ta ON se.teacher_assignment_id=ta.id
JOIN users u ON ta.teacher_id=u.id
JOIN subject_assignments sa ON ta.subject_assignment_id=sa.id
JOIN subjects subj ON sa.subject_id=subj.id JOIN groups g ON sa.group_id=g.id
JOIN time_slots ts ON se.time_slot_id=ts.id
WHERE se.academic_year_id=%s ORDER BY u.last_name, se.day_of_week, ts.display_order
""",(CANONICAL,))
loaded_rows = cur.fetchall()
cur.execute("SELECT COUNT(*) c FROM teacher_assignments"); ta_count=cur.fetchone()["c"]
conn.close()

by_teacher = {}
for r in loaded_rows:
    name = ID_TO_NAME.get(r["tid"], r["dbname"])
    by_teacher.setdefault(name, {"loaded":[],"slots":set()})
    bidx = slot_to_block.get(r["sid"], -1)
    by_teacher[name]["loaded"].append({
        "day": DAY.get(r["day_of_week"], str(r["day_of_week"])),
        "block": BLOCKS[bidx] if 0<=bidx<6 else r["start_time"].strftime("%H:%M"),
        "subject": r["subject"], "group": r["grp"], "word_cell": None
    })
    by_teacher[name]["slots"].add((r["day_of_week"], r["sid"]))

doc_by = {}
for u in unique:
    sid = slot_by_time.get(BLOCK_TIMES[u["block_idx"]])
    if sid: doc_by.setdefault(u["bucket"], {})[(u["day"], sid)] = u["cell"]

report = {"summary":{},"teachers":[]}
total_doc=total_ok=total_miss=0
for b in sorted(set(doc_by)|set(k for k in TEACHER_MAP if k in ["ABREGO","AGAMES","ASPRILLA","MENDOZA","DEL CID","ESPAÑA","GARCIA","HERNANDEZ","MORA","REYES","ROBLES","VASQUEZ","VÁSQUEZ"])):
    tid = TEACHER_MAP.get(b) or TEACHER_MAP.get(b.replace("A","Á"))
    if not tid: continue
    tname = ID_TO_NAME[tid]
    doc_slots = doc_by.get(b,{})
    bd_slots = by_teacher.get(tname,{}).get("slots",set())
    missing=[]; matched=[]
    for (day,sid), cell in sorted(doc_slots.items()):
        item={"day":DAY[day],"block":BLOCKS[slot_to_block.get(sid,0)] if sid in slot_to_block else "?","cell":cell}
        if (day,sid) in bd_slots:
            matched.append(item)
        else:
            item["reason"]=classify_missing(cell,tname)
            missing.append(item)
    total_doc+=len(doc_slots); total_ok+=len(matched); total_miss+=len(missing)
    report["teachers"].append({
        "name": tname, "word_slots": len(doc_slots), "bd_count": len(bd_slots),
        "matched": len(matched), "missing_count": len(missing),
        "coverage_pct": round(100*len(matched)/len(doc_slots),1) if doc_slots else 100,
        "status": "COMPLETO" if not missing else ("PARCIAL" if matched else "PENDIENTE"),
        "missing": missing, "loaded_sample": by_teacher.get(tname,{}).get("loaded",[])[:5]
    })

report["summary"]={
    "source_file":"HORARIOS DE DOCENTES 2026 QUENA.docx",
    "academic_year_id": CANONICAL, "academic_year_name":"2026",
    "raw_cells": raw, "unique_slots": len(unique), "duplicates_in_word": raw-len(unique),
    "bd_total": len(loaded_rows), "matched_slots": total_ok, "missing_slots": total_miss,
    "coverage_pct": round(100*total_ok/total_doc,1), "teacher_assignments": ta_count,
    "teachers_complete": sum(1 for t in report["teachers"] if t["missing_count"]==0),
    "teachers_partial": sum(1 for t in report["teachers"] if 0<t["missing_count"]<t["word_slots"]),
}
OUT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report["summary"], ensure_ascii=False, indent=2))
