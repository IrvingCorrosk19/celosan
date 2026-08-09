"""
Completa horarios de Elvia Robles, Lino Garcia y Zellidet Hernandez.
Crea materias/asignaciones faltantes y agrega schedule_entries sin borrar los existentes.
"""
from __future__ import annotations

import re
import unicodedata
import uuid
import zipfile
from dataclasses import dataclass
from datetime import datetime, time, timezone
from pathlib import Path
from xml.etree import ElementTree as ET

import psycopg2
import psycopg2.extras

DOC = Path(__file__).resolve().parent.parent / "HORARIOS DE DOCENTES  2026 QUENA.docx"
CANONICAL_AY = "f7ccb57f-fa3e-4d9f-973b-552030c9852d"
SCHOOL = "6e42399f-6f17-4585-b92e-fa4fff02cb65"

TEACHERS = {
    "ROBLES": "2210619e-1c8c-4dc1-b1cb-db66333592e9",
    "GARCIA": "6b5e25fb-762f-4a55-833c-ab35f5b36f56",
    "HERNANDEZ": "84c339b4-61de-47e8-8411-27e5c20c1b22",
}

CONN = dict(
    host="dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com",
    database="schoolmanager_daqf",
    user="admin",
    password="iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r",
    port=5432,
    sslmode="require",
)

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
DAY_MAP = {"LUNES": 1, "MARTES": 2, "MIERCOLES": 3, "MIÉRCOLES": 3, "JUEVES": 4, "VIERNES": 5}
BLOCK_TIMES = [
    (time(18, 10), time(19, 0)), (time(19, 0), time(19, 50)), (time(19, 50), time(20, 40)),
    (time(20, 40), time(21, 30)), (time(21, 30), time(22, 20)), (time(22, 20), time(23, 10)),
]


def norm(s: str) -> str:
    s = unicodedata.normalize("NFKD", s or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    return re.sub(r"[^A-Z0-9]+", "", s.upper())


def cell_text(el) -> str:
    return "".join(t.text or "" for t in el.iter(W + "t")).strip()


def teacher_bucket(name: str) -> str | None:
    u = name.upper()
    if "ROBLES" in u or "ELVIA" in u:
        return "ROBLES"
    if "GARCIA" in u and "LINO" in u:
        return "GARCIA"
    if "HERNANDEZ" in u or "ZELLID" in u:
        return "HERNANDEZ"
    return None


def parse_pending_cells() -> list[dict]:
    with zipfile.ZipFile(DOC) as z:
        root = ET.fromstring(z.read("word/document.xml"))
    body = root.find(W + "body")
    current = None
    out = []
    for child in body:
        tag = child.tag.split("}")[-1]
        if tag == "p":
            m = re.search(r"PROFESOR[^:]*:\s*(.+)", cell_text(child), re.I)
            if m:
                current = re.sub(r"^\)\s*", "", m.group(1).strip().split("HORAS")[0].strip())
        elif tag == "tbl" and current and teacher_bucket(current):
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
                        out.append({"bucket": teacher_bucket(current), "day": day, "block_idx": bidx, "cell": r[ci].strip()})
    return out


def dedupe_cells(cells: list[dict]) -> list[dict]:
    seen: set[tuple] = set()
    unique = []
    for c in cells:
        key = (c["bucket"], c["day"], c["block_idx"])
        if key in seen:
            continue
        seen.add(key)
        unique.append(c)
    return unique


@dataclass
class SA:
    id: str
    subject: str
    group: str
    subject_norm: str
    group_norm: str


# Reglas explícitas (celda normalizada contiene clave -> subject contains, group exact)
EXPLICIT = [
    ("SALUDFISICAYMENTAL", "SALUD FÍSICA Y MENTAL", None),
    ("EXPRESART", "EXPRESIONES ARTÍSTICA", None),
    ("MUSICA", "MÚSICA", None),
    ("BELLASARTES", "BELLAS ARTES", None),
    ("FAMILIAYDESARROLLO", "FDC 1", "7-A"),
    ("TECNFAMILIA", "FDC 2", "8-A"),
    ("TECNFAMILIA", "FDC 1", "7-A"),
    ("ETICAYVALORES", "ÉTICA Y VALORES", None),
    ("ETICAYVALORES", "ÉTICA, MORAL, VALORES", None),
    ("ETICAYVALORES", "ÉTICA MORAL, VALORES", None),
    ("VALORESETICOS", "ÉTICA MORAL, VALORES", None),
    ("VALORESETICOS", "ÉTICA, MORAL, VALORES", None),
    ("EDUCACIONFISICA", "EDUCACIÓN FÍSICA", None),
    ("EDUCACIONFISICA", "EDUCACIÓN FÍSICA Y SALUD", None),
    ("PROGRAMACION", "PROGRAMACIÓN", None),
    ("ARQDELASCOMPUTADORAS", "ARQUITECTURA DE LAS COMPUTADORAS", None),
    ("APLICACIONCONBD", "APLICACIONES CON BASE DE DATOS", None),
    ("DESARROLLOLOGICO", "DESARROLLO LÓGICO", None),
    ("CONFIGYADDESIST", "CONFIGURACIÓN Y ADMINISTRACIÓN", None),
    ("OFIMATICA", "OFIMÁTICA", None),
    ("DIAGAUTOMATIZADO", "DIAGNÓSTICO AUTOMOTRIZ", None),
    ("TECYTALLERAPLICADO", "TECNOLOGÍA Y TALLER APLICADO", None),
    ("ELECELECTRAUT", "ELECTRICIDAD Y ELECTRÓNICA AUTOMOTRIZ", None),
    ("MANTAUTOMOTRIZ", "MANTENIMIENTO AUTOMOTRIZ", None),
    ("INSTRESIDENCIAL", "INSTALACIÓN RESIDENCIAL", None),
    ("MAQUINASELECTRICAS", "MÁQUINAS ELÉCTRICAS", None),
    ("SEGURIDADINDUSTRIAL", "SEGURIDAD INDUSTRIAL", None),
    ("EQUIPOYMEDICIONES", "EQUIPO Y MEDICIONES", None),
    ("ELECTRONICA", "ELECTRÓNICA", None),
    ("PRODUCCIONYDISTRIB", "PRODUCCIÓN Y DISTRIBUCIÓN", None),
    ("ANALISISDECIRCUITO", "ANÁLISIS DE CIRCUITO", None),
]


def infer_group(cell: str) -> str | None:
    c = cell.upper()
    m = re.search(r"(\d{1,2})\s*[-]?\s*([A-Z])\s*[-]?\s*(\d)?", c)
    if m:
        lvl, sec, sub = m.group(1), m.group(2), m.group(3)
        # Pre-media 7-A-1 / 7-A-2 -> grupo único 7-A en BD
        if lvl in ("7", "8", "9") and sec == "A":
            return f"{lvl}-A"
        return f"{lvl}-{sec}{sub}" if sub else f"{lvl}-{sec}"
    m = re.search(r"(\d{1,2})\s*[°O]?\s*([A-Z])\b", c)
    if m:
        return f"{m.group(1)}-{m.group(2)}2" if "12" in m.group(1) and m.group(2) == "A" else f"{m.group(1)}-{m.group(2)}"
    m = re.search(r"\b(7|8|9|10|11|12)\b", c)
    if m:
        lvl = m.group(1)
        if "A2" in norm(c):
            return f"{lvl}-A2"
        if "A3" in norm(c):
            return f"{lvl}-A3"
        if "A4" in norm(c) or "-T" in c or "TUR" in c:
            return f"{lvl}-A4"
        if "-I" in c or "IPROGRAM" in norm(c) or "IARQ" in norm(c):
            return f"{lvl}-A1"
        return f"{lvl}-A"
    return None


def match_sa(cell: str, sas: list[SA]) -> SA | None:
    cn = norm(cell)
    grp_hint = infer_group(cell)

    for key, sub_hint, grp_fixed in EXPLICIT:
        if key in cn:
            cands = [s for s in sas if sub_hint.upper() in s.subject.upper()]
            if grp_fixed:
                cands = [s for s in cands if s.group == grp_fixed]
            elif grp_hint:
                exact = [s for s in cands if s.group == grp_hint]
                pref = [s for s in cands if s.group.startswith(grp_hint.split("-")[0])]
                cands = exact or pref or cands
            if cands:
                if grp_hint and not grp_fixed:
                    exact = [s for s in cands if s.group == grp_hint]
                    if exact:
                        return exact[0]
                return sorted(cands, key=lambda s: s.group)[0]

    if grp_hint:
        cands = [s for s in sas if s.group == grp_hint or s.group_norm == norm(grp_hint)]
        if len(cands) == 1:
            return cands[0]

    best, score = None, 0
    for s in sas:
        sc = 0
        if grp_hint and s.group == grp_hint:
            sc += 100
        for tok in re.findall(r"[A-Z]{5,}", cn):
            if tok in s.subject_norm:
                sc += 50
        if sc > score:
            score, best = sc, s
    return best if score >= 50 else None


def ensure_music(cur, sas: list[SA]) -> None:
    if any(s.subject_norm == "MUSICA" for s in sas):
        return
    cur.execute("SELECT id FROM subjects WHERE name ILIKE 'MÚSICA' OR name ILIKE 'MUSICA'")
    row = cur.fetchone()
    if row:
        subj_id = row[0]
    else:
        cur.execute("SELECT area_id FROM subjects WHERE name ILIKE 'EXPRESIONES ART%' LIMIT 1")
        area = cur.fetchone()[0]
        subj_id = str(uuid.uuid4())
        cur.execute(
            "INSERT INTO subjects (id, school_id, name, status, created_at, area_id) VALUES (%s,%s,%s,true,%s,%s)",
            (subj_id, SCHOOL, "MÚSICA", datetime.now(timezone.utc), area),
        )
    cur.execute(
        """
        SELECT sa.specialty_id, sa.area_id, sa.grade_level_id, g.id
        FROM subject_assignments sa JOIN groups g ON sa.group_id=g.id
        JOIN subjects subj ON sa.subject_id=subj.id
        WHERE g.name='8-A' AND subj.name ILIKE 'EXPRESIONES ART%' LIMIT 1
        """
    )
    tpl = cur.fetchone()
    sa_id = str(uuid.uuid4())
    cur.execute(
        """
        INSERT INTO subject_assignments (id, specialty_id, area_id, subject_id, grade_level_id, group_id, created_at, status)
        VALUES (%s,%s,%s,%s,%s,%s,%s,'Active')
        """,
        (sa_id, tpl[0], tpl[1], subj_id, tpl[2], tpl[3], datetime.now(timezone.utc)),
    )
    sas.append(SA(sa_id, "MÚSICA", "8-A", "MUSICA", "8A"))
    print("  Creada materia/grupo: MÚSICA en 8-A")


def main():
    cells = dedupe_cells(parse_pending_cells())
    print(f"Celdas a procesar (únicas): {len(cells)}")

    conn = psycopg2.connect(**CONN)
    conn.autocommit = False
    cur = conn.cursor()
    now = datetime.now(timezone.utc)

    try:
        cur.execute(
            """
            SELECT sa.id, subj.name, g.name
            FROM subject_assignments sa
            JOIN subjects subj ON sa.subject_id=subj.id
            JOIN groups g ON sa.group_id=g.id
            WHERE g.name NOT IN ('A','A1','A2','A3','A4') OR g.name ~ '^\\d'
            """
        )
        sas = [SA(str(r[0]), r[1], r[2], norm(r[1]), norm(r[2])) for r in cur.fetchall()]
        print(f"Subject assignments cargadas: {len(sas)}")

        print("Verificando MÚSICA...")
        ensure_music(cur, sas)

        cur.execute(
            """
            SELECT ts.id, ts.start_time, ts.end_time FROM time_slots ts
            LEFT JOIN shifts s ON ts.shift_id=s.id
            WHERE ts.is_active=true AND (s.name ILIKE '%noche%' OR ts.start_time>='18:00')
            ORDER BY ts.display_order, ts.start_time
            """
        )
        slot_by_time = {(r[1], r[2]): str(r[0]) for r in cur.fetchall()}

        cur.execute(
            """
            SELECT ta.teacher_id, se.day_of_week, se.time_slot_id, g.name
            FROM schedule_entries se
            JOIN teacher_assignments ta ON se.teacher_assignment_id=ta.id
            JOIN subject_assignments sa ON ta.subject_assignment_id=sa.id
            JOIN groups g ON sa.group_id=g.id
            WHERE se.academic_year_id=%s
            """,
            (CANONICAL_AY,),
        )
        used_teacher = {(str(r[0]), r[1], str(r[2])) for r in cur.fetchall()}
        used_group = {(norm(r[3]), r[1], str(r[2])) for r in cur.fetchall()}

        cur.execute("SELECT teacher_id, subject_assignment_id, id FROM teacher_assignments")
        ta_map = {(str(r[0]), str(r[1])): str(r[2]) for r in cur.fetchall()}

        ta_created = se_created = skipped = 0
        skipped_items = []

        for item in cells:
            teacher_id = TEACHERS[item["bucket"]]
            sa = match_sa(item["cell"], sas)
            slot_id = slot_by_time.get(BLOCK_TIMES[item["block_idx"]])
            if not sa or not slot_id:
                skipped += 1
                skipped_items.append({**item, "reason": "sin match" if not sa else "sin bloque"})
                continue

            pair = (teacher_id, sa.id)
            if pair not in ta_map:
                ta_id = str(uuid.uuid4())
                cur.execute(
                    "INSERT INTO teacher_assignments (id, teacher_id, subject_assignment_id, created_at) VALUES (%s,%s,%s,%s)",
                    (ta_id, teacher_id, sa.id, now),
                )
                ta_map[pair] = ta_id
                ta_created += 1
            ta_id = ta_map[pair]

            t_key = (teacher_id, item["day"], slot_id)
            g_key = (sa.group_norm, item["day"], slot_id)
            if t_key in used_teacher or g_key in used_group:
                skipped += 1
                skipped_items.append({**item, "reason": "conflicto"})
                continue

            cur.execute(
                """
                INSERT INTO schedule_entries (id, teacher_assignment_id, time_slot_id, day_of_week, academic_year_id, created_at)
                VALUES (%s,%s,%s,%s,%s,%s) ON CONFLICT DO NOTHING
                """,
                (str(uuid.uuid4()), ta_id, slot_id, item["day"], CANONICAL_AY, now),
            )
            if cur.rowcount:
                se_created += 1
                used_teacher.add(t_key)
                used_group.add(g_key)
            else:
                skipped += 1

        conn.commit()

        print("\n=== COMPLETADO ===")
        print(f"Teacher assignments creadas: {ta_created}")
        print(f"Horarios insertados:       {se_created}")
        if skipped_items:
            print(f"\nOmitidos detalle ({len(skipped_items)}):")
            for u in skipped_items:
                print(f"  [{u['bucket']}] d{u['day']} b{u['block_idx']} | {u['cell']} | {u['reason']}")

        cur.execute(
            """
            SELECT u.name||' '||u.last_name docente, COUNT(*) c
            FROM schedule_entries se
            JOIN teacher_assignments ta ON se.teacher_assignment_id=ta.id
            JOIN users u ON ta.teacher_id=u.id
            WHERE se.academic_year_id=%s
              AND u.id IN %s
            GROUP BY u.name, u.last_name ORDER BY u.last_name
            """,
            (CANONICAL_AY, tuple(TEACHERS.values())),
        )
        print("\nHorarios por docente (2026):")
        for r in cur.fetchall():
            print(f"  {r[0]}: {r[1]}")

        cur.execute("SELECT COUNT(*) FROM schedule_entries WHERE academic_year_id=%s", (CANONICAL_AY,))
        print(f"\nTotal horarios 2026: {cur.fetchone()[0]}")

    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


if __name__ == "__main__":
    main()
