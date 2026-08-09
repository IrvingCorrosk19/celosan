"""
Importa horarios desde HORARIOS DE DOCENTES 2026 QUENA.docx a schedule_entries.
Fase 1: crea teacher_assignments faltantes a partir de subject_assignments existentes.
Fase 2: inserta schedule_entries.
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
    (time(18, 10), time(19, 0)),
    (time(19, 0), time(19, 50)),
    (time(19, 50), time(20, 40)),
    (time(20, 40), time(21, 30)),
    (time(21, 30), time(22, 20)),
    (time(22, 20), time(23, 10)),
]

SUBJECT_HINTS: list[tuple[str, list[str]]] = [
    ("RELACIONESHUMANAS", ["REL", "HUMAN"]),
    ("ETICAYVALORES", ["ETICA", "VALOR"]),
    ("ORIENTACION", ["ORIENT"]),
    ("PROGRAMACION", ["PROGRAM"]),
    ("ARQUITECTURADECOMPUTADORAS", ["ARQ", "COMPUT"]),
    ("REDESDECOMPUTADORAS", ["REDES"]),
    ("MULTIMEDIAYDESARROLLOWEB", ["MULT", "WEB"]),
    ("DESARROLLOLOGICO", ["DESARROLLO", "LOGIC"]),
    ("CONFIGURACIONADMINISTRACIONSISTEMAS", ["CONFIG", "SIST", "OPER"]),
    ("GEOGRAFIADEPANAMA", ["GEOG", "PAN"]),
    ("GEOGRAFIATURISTICAMUNDO", ["GEOG", "TUR", "MUNDO"]),
    ("GEOGRAFIATURISTICAPANAMA", ["GEOG", "TUR", "PAN"]),
    ("HISTORIADEPANAMA", ["HISTOR", "PAN"]),
    ("HISTORIARELACIONESEU", ["REL", "PM", "EU"]),
    ("HISTORIA", ["HISTOR"]),
    ("CIVICA", ["CIVIC"]),
    ("CIVICAIII", ["CIVIC", "III"]),
    ("EDUCACIONFISICA", ["EDUC", "FISIC"]),
    ("SALUDFISICAYMENTAL", ["SALUD", "FISIC"]),
    ("BELLASARTES", ["BELLAS", "ART"]),
    ("EXPRESIONARTISTICA", ["EXPRES", "ART"]),
    ("FISICA", ["FISICA"]),
    ("QUIMICA", ["QUIM"]),
    ("CIENCIASINTEGRADAS", ["CIENC", "INTEG"]),
    ("CIENCIASNATURALES", ["CIENC", "NATUR"]),
    ("CONTABILIDAD", ["CONTAB"]),
    ("GESTIONEMPRESARIAL", ["GEST", "EMPRES"]),
    ("GESTIONEMPRESARIALTURISTICA", ["GEST", "EMPRES", "TUR"]),
    ("PRACTICAPROFESIONAL", ["PRACT", "PROF"]),
    ("TURISMOINTRODUCCION", ["TURISM", "INTRO"]),
    ("TURISMOSOSTENIBLE", ["TURISM", "SOST"]),
    ("SERVICIOSTURISTICOS", ["SERV", "TUR"]),
    ("ELABORACIONPROYECTOSTURISTICOS", ["ELAB", "PROY", "TUR"]),
    ("FRANCES", ["FRANC"]),
    ("INGLES", ["INGL"]),
    ("ESPANOL", ["ESPAN", "LENG"]),
    ("OFIMATICA", ["OFIMAT"]),
    ("LOGICA", ["LOGIC"]),
    ("FILOSOFIA", ["FILOS"]),
    ("MUSICA", ["MUSIC"]),
    ("TECNOLOGIACOMERCIAL", ["TECNO", "COMER"]),
    ("TECNOLOGIADEINFORMACION", ["TECNO", "INFORM"]),
    ("MERCADOTECNIA", ["MERCAD", "PUBLIC"]),
    ("DIBUJOLINEAL", ["DIBUJ", "LINE"]),
    ("DIBUJOII", ["DIBUJ", "II"]),
    ("SEGURIDADINDUSTRIAL", ["SEGUR", "INDUST"]),
    ("TALLER", ["TALLER"]),
    ("RELACIONESLABORALES", ["RELAC", "LABOR"]),
    ("GEOGRAFIA", ["GEOG"]),
    ("TECNICASMECANOGRAFIA", ["MECANOG"]),
    ("TECNICASFAMILIA", ["FAMIL", "DESAR"]),
    ("TECNICASMETALES", ["METAL"]),
    ("DIBUJORELACIONADO", ["DIBUJ", "REL"]),
]


def norm(s: str) -> str:
    s = unicodedata.normalize("NFKD", s or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    s = s.upper()
    return re.sub(r"[^A-Z0-9]+", "", s)


def cell_text(el) -> str:
    return "".join(t.text or "" for t in el.iter(W + "t")).strip()


def parse_docx(path: Path) -> list[dict]:
    with zipfile.ZipFile(path) as z:
        root = ET.fromstring(z.read("word/document.xml"))

    body = root.find(W + "body")
    current_teacher = None
    entries: list[dict] = []

    for child in body:
        tag = child.tag.split("}")[-1]
        if tag == "p":
            txt = cell_text(child)
            m = re.search(r"PROFESOR[^:]*:\s*(.+)", txt, re.I)
            if m and len(m.group(1).strip()) > 2:
                current_teacher = re.sub(r"^\)\s*", "", m.group(1).strip().split("HORAS")[0].strip())
        elif tag == "tbl" and current_teacher:
            rows = [[cell_text(tc) for tc in tr.findall(W + "tc")] for tr in child.findall(W + "tr")]
            hidx = next((i for i, r in enumerate(rows) if any("LUNES" in c.upper() for c in r)), None)
            if hidx is None:
                continue
            hdr = rows[hidx]
            day_cols = {
                ci: DAY_MAP[dname]
                for ci, c in enumerate(hdr)
                for dname in DAY_MAP
                if dname in c.upper().replace("É", "E")
            }
            for r in rows[hidx + 1 :]:
                if not r:
                    continue
                block_cell = r[0]
                if not re.search(r"ira|\d+(ra|da|ta|va|na)", block_cell.lower()):
                    continue
                block_idx = 0 if block_cell.lower().startswith("ira") else int(re.match(r"(\d+)", block_cell.lower()).group(1)) - 1
                for ci, day_num in day_cols.items():
                    if ci < len(r) and r[ci].strip() and r[ci].strip().upper() not in DAY_MAP:
                        entries.append(
                            {"teacher_raw": current_teacher, "day": day_num, "block_idx": block_idx, "cell": r[ci].strip()}
                        )
    return entries


def teacher_tokens(name: str) -> set[str]:
    parts = re.findall(r"[A-Za-z]+", re.sub(r"^\)\s*", "", name))
    tokens = {norm(p) for p in parts if len(p) >= 2}
    if parts:
        tokens.add(norm(parts[-1]))
        if len(parts) >= 2:
            tokens.add(norm(parts[-2] + parts[-1]))
    return tokens


def teacher_matches(raw: str, db_name: str) -> bool:
    if teacher_tokens(raw) & teacher_tokens(db_name):
        return True
    rn, dn = norm(raw), norm(db_name)
    return len(rn) >= 5 and (rn in dn or dn in rn or rn[-6:] == dn[-6:])


def parse_cell(cell: str) -> dict:
    c = cell.upper()
    cn = norm(cell)
    m = re.search(r"\b(7|8|9|10|11|12)\b", c)
    level = m.group(1) if m else None

    groups: set[str] = set()
    for gm in re.finditer(r"(\d{1,2})\s*[-]?\s*([A-Z])\s*[-]?\s*(\d)?", c):
        lvl, sec, sub = gm.group(1), gm.group(2), gm.group(3)
        groups.add(f"{lvl}-{sec}{sub}" if sub else f"{lvl}-{sec}")
        if sub:
            groups.add(f"{lvl}-{sec}-{sub}")

    if level and not groups:
        for sfx in ["A1", "A2", "A3", "A4", "A"]:
            groups.add(f"{level}-{sfx}")

    subjects: set[str] = set()
    for key, hints in SUBJECT_HINTS:
        hits = sum(1 for h in hints if norm(h) in cn)
        if hits >= min(2, len(hints)) or (len(hints) == 1 and norm(hints[0]) in cn):
            subjects.add(key)

    return {"level": level, "groups": groups, "subjects": subjects, "raw_norm": cn}


@dataclass
class Assignment:
    id: str
    teacher_id: str
    teacher_name: str
    group_name: str
    group_norm: str
    subject_name: str
    subject_norm: str
    subject_assignment_id: str
    level: str


@dataclass
class SubjectAssignmentRef:
    id: str
    group_name: str
    group_norm: str
    subject_name: str
    subject_norm: str
    level: str


def score_sa_match(parsed: dict, sa: SubjectAssignmentRef) -> int:
    score = 0
    cn = parsed["raw_norm"]
    for g in parsed["groups"]:
        gn = norm(g)
        if gn == sa.group_norm:
            score += 120
        elif sa.group_norm.startswith(gn) or gn in sa.group_norm:
            score += 70
    if parsed["level"] and parsed["level"] == sa.level:
        score += 20
    for sk in parsed["subjects"]:
        if sk in sa.subject_norm or sa.subject_norm in sk:
            score += 100
        for key, hints in SUBJECT_HINTS:
            if sk == key and any(norm(h) in sa.subject_norm for h in hints):
                score += 80
    if sa.group_norm in cn:
        score += 40
    if sa.subject_norm[:10] in cn or cn[:10] in sa.subject_norm:
        score += 35
    for token in [cn[i : i + 6] for i in range(0, len(cn) - 5, 3)]:
        if len(token) >= 5 and token in sa.subject_norm:
            score += 25
    return score


def load_all(conn):
    cur = conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor)

    cur.execute(
        """
        SELECT id FROM academic_years
        WHERE name = '2026' AND is_active = true AND school_id IS NOT NULL
        ORDER BY created_at ASC
        LIMIT 1
        """
    )
    academic_year_id = str(cur.fetchone()["id"])

    cur.execute(
        """
        SELECT ts.id, ts.start_time, ts.end_time
        FROM time_slots ts
        LEFT JOIN shifts s ON ts.shift_id = s.id
        WHERE ts.is_active = true AND (s.name ILIKE '%noche%' OR ts.start_time >= '18:00')
        ORDER BY ts.display_order, ts.start_time
        """
    )
    slot_by_time = {(r["start_time"], r["end_time"]): str(r["id"]) for r in cur.fetchall()}

    cur.execute(
        """
        SELECT sa.id AS sa_id, g.name AS group_name, subj.name AS subject_name
        FROM subject_assignments sa
        JOIN groups g ON sa.group_id = g.id
        JOIN subjects subj ON sa.subject_id = subj.id
        """
    )
    subject_assignments = []
    for r in cur.fetchall():
        gn = r["group_name"]
        lm = re.match(r"(\d+)", gn)
        subject_assignments.append(
            SubjectAssignmentRef(
                id=str(r["sa_id"]),
                group_name=gn,
                group_norm=norm(gn),
                subject_name=r["subject_name"],
                subject_norm=norm(r["subject_name"]),
                level=lm.group(1) if lm else "",
            )
        )

    cur.execute(
        """
        SELECT ta.id, ta.teacher_id, ta.subject_assignment_id,
               u.name || ' ' || u.last_name AS teacher_name,
               g.name AS group_name, subj.name AS subject_name
        FROM teacher_assignments ta
        JOIN users u ON ta.teacher_id = u.id
        JOIN subject_assignments sa ON ta.subject_assignment_id = sa.id
        JOIN groups g ON sa.group_id = g.id
        JOIN subjects subj ON sa.subject_id = subj.id
        WHERE lower(u.role) IN ('teacher', 'docente', 'profesor')
        """
    )
    assignments = []
    for r in cur.fetchall():
        gn = r["group_name"]
        lm = re.match(r"(\d+)", gn)
        assignments.append(
            Assignment(
                id=str(r["id"]),
                teacher_id=str(r["teacher_id"]),
                teacher_name=r["teacher_name"],
                group_name=gn,
                group_norm=norm(gn),
                subject_name=r["subject_name"],
                subject_norm=norm(r["subject_name"]),
                subject_assignment_id=str(r["subject_assignment_id"]),
                level=lm.group(1) if lm else "",
            )
        )

    cur.execute(
        "SELECT id, name || ' ' || last_name AS full_name FROM users WHERE lower(role) IN ('teacher', 'docente', 'profesor')"
    )
    teachers = {r["full_name"]: str(r["id"]) for r in cur.fetchall()}

    return academic_year_id, slot_by_time, subject_assignments, assignments, teachers


def resolve_teacher_id(raw: str, teachers: dict[str, str], cur) -> str | None:
    for name, tid in teachers.items():
        if teacher_matches(raw, name):
            return tid
    return None


def ensure_teacher_assignments(conn, parsed: list[dict], subject_assignments, assignments, teachers):
    cur = conn.cursor()
    now = datetime.now(timezone.utc)
    created = 0
    existing_pairs = {(a.teacher_id, a.subject_assignment_id) for a in assignments}

    teacher_cells: dict[str, list[str]] = {}
    for item in parsed:
        teacher_cells.setdefault(item["teacher_raw"], []).append(item["cell"])

    for teacher_raw, cells in teacher_cells.items():
        teacher_id = resolve_teacher_id(teacher_raw, teachers, cur)
        if not teacher_id:
            print(f"  Docente no encontrado: {teacher_raw}")
            continue

        teacher_assignments = [a for a in assignments if a.teacher_id == teacher_id]

        seen_sa: set[str] = {a.subject_assignment_id for a in teacher_assignments}
        for cell in cells:
            parsed_cell = parse_cell(cell)
            scored = [(score_sa_match(parsed_cell, sa), sa) for sa in subject_assignments]
            scored = [(s, sa) for s, sa in scored if s >= 80]
            if not scored:
                continue
            scored.sort(key=lambda x: (-x[0], x[1].group_name))
            sa = scored[0][1]
            if sa.id in seen_sa:
                continue
            seen_sa.add(sa.id)
            pair = (teacher_id, sa.id)
            if pair in existing_pairs:
                continue
            ta_id = str(uuid.uuid4())
            cur.execute(
                "INSERT INTO teacher_assignments (id, teacher_id, subject_assignment_id, created_at) VALUES (%s, %s, %s, %s)",
                (ta_id, teacher_id, sa.id, now),
            )
            existing_pairs.add(pair)
            lm = re.match(r"(\d+)", sa.group_name)
            assignments.append(
                Assignment(
                    id=ta_id,
                    teacher_id=teacher_id,
                    teacher_name=teacher_raw,
                    group_name=sa.group_name,
                    group_norm=sa.group_norm,
                    subject_name=sa.subject_name,
                    subject_norm=sa.subject_norm,
                    subject_assignment_id=sa.id,
                    level=lm.group(1) if lm else "",
                )
            )
            created += 1

    conn.commit()
    print(f"  Teacher assignments creadas: {created}")
    return assignments


def score_assignment(parsed: dict, a: Assignment) -> int:
    return score_sa_match(parsed, SubjectAssignmentRef(a.subject_assignment_id, a.group_name, a.group_norm, a.subject_name, a.subject_norm, a.level))


def pick_assignment(cell, teacher_raw, assignments, day, block_idx, used_ta_ids):
    candidates = [a for a in assignments if teacher_matches(teacher_raw, a.teacher_name)]
    if not candidates:
        return None
    parsed = parse_cell(cell)
    scored = [(score_assignment(parsed, a), a) for a in candidates]
    scored = [(s, a) for s, a in scored if s >= 50]
    if not scored:
        return None
    scored.sort(key=lambda x: (-x[0], x[1].group_name))
    top = [a for s, a in scored if s >= scored[0][0] - 20]
    fresh = [a for a in top if a.id not in used_ta_ids] or top
    return fresh[(day * 7 + (block_idx or 0)) % len(fresh)]


def main():
    print("Parseando documento...")
    parsed = parse_docx(DOC)
    print(f"  Celdas: {len(parsed)}")

    conn = psycopg2.connect(**CONN)
    conn.autocommit = False
    try:
        academic_year_id, slot_by_time, subject_assignments, assignments, teachers = load_all(conn)

        print("Fase 1: Creando teacher_assignments faltantes...")
        assignments = ensure_teacher_assignments(conn, parsed, subject_assignments, assignments, teachers)
        print(f"  Total asignaciones docente: {len(assignments)}")

        cur = conn.cursor()
        cur.execute("DELETE FROM schedule_entries WHERE academic_year_id = %s", (academic_year_id,))
        print(f"Fase 2: Entradas previas eliminadas: {cur.rowcount}")

        inserted = skipped = 0
        unmatched = []
        teacher_slot_day: set[tuple] = set()
        group_slot_day: set[tuple] = set()
        used_ta_ids: set[str] = set()
        now = datetime.now(timezone.utc)

        for item in parsed:
            slot_id = slot_by_time.get(BLOCK_TIMES[item["block_idx"]]) if item["block_idx"] is not None else None
            if not slot_id:
                skipped += 1
                continue

            ta = pick_assignment(item["cell"], item["teacher_raw"], assignments, item["day"], item["block_idx"], used_ta_ids)
            if not ta:
                skipped += 1
                unmatched.append(item)
                continue

            t_key = (ta.teacher_id, item["day"], slot_id)
            g_key = (ta.group_norm, item["day"], slot_id)
            if t_key in teacher_slot_day or g_key in group_slot_day:
                parsed_cell = parse_cell(item["cell"])
                alts = sorted(
                    [a for a in assignments if teacher_matches(item["teacher_raw"], a.teacher_name) and score_assignment(parsed_cell, a) >= 50],
                    key=lambda a: -score_assignment(parsed_cell, a),
                )
                ta = next(
                    (
                        alt
                        for alt in alts
                        if (alt.teacher_id, item["day"], slot_id) not in teacher_slot_day
                        and (alt.group_norm, item["day"], slot_id) not in group_slot_day
                    ),
                    None,
                )
                if not ta:
                    skipped += 1
                    unmatched.append(item)
                    continue

            cur.execute(
                """
                INSERT INTO schedule_entries (id, teacher_assignment_id, time_slot_id, day_of_week, academic_year_id, created_at)
                VALUES (%s, %s, %s, %s, %s, %s) ON CONFLICT DO NOTHING
                """,
                (str(uuid.uuid4()), ta.id, slot_id, item["day"], academic_year_id, now),
            )
            if cur.rowcount:
                inserted += 1
                teacher_slot_day.add((ta.teacher_id, item["day"], slot_id))
                group_slot_day.add((ta.group_norm, item["day"], slot_id))
                used_ta_ids.add(ta.id)
            else:
                skipped += 1

        conn.commit()
        cur.execute("SELECT COUNT(*) FROM schedule_entries WHERE academic_year_id = %s", (academic_year_id,))
        total = cur.fetchone()[0]

        by_teacher = {}
        for item in parsed:
            by_teacher.setdefault(item["teacher_raw"], {"total": 0, "fail": 0})
            by_teacher[item["teacher_raw"]]["total"] += 1
        for item in unmatched:
            by_teacher.setdefault(item["teacher_raw"], {"total": 0, "fail": 0})
            by_teacher[item["teacher_raw"]]["fail"] += 1

        print("\n=== CARGA COMPLETADA ===")
        print(f"Insertadas:  {inserted}")
        print(f"Omitidas:    {skipped}")
        print(f"Total en BD: {total}")
        print("\nPor docente:")
        for t, stats in sorted(by_teacher.items()):
            ok = stats["total"] - stats.get("fail", 0)
            print(f"  {t}: {ok}/{stats['total']}")

        if unmatched:
            print(f"\nCeldas no cargadas ({len(unmatched)}), muestra:")
            for u in unmatched[:20]:
                print(f"  {u['teacher_raw']} | d{u['day']} b{u['block_idx']} | {u['cell']}")

    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


if __name__ == "__main__":
    main()
