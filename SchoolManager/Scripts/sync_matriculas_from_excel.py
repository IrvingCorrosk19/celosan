"""
Lee asignaciones_estudiantes_grado_grupo (25).xlsx y crea student_assignments activas
(nivel/grado, grupo, jornada Noche, año 2026) en Render según appsettings.json.
"""
import json
import re
import sys
from pathlib import Path

import openpyxl
import psycopg2

EXCEL = Path(r"c:\Users\irvin\Downloads\asignaciones_estudiantes_grado_grupo (25).xlsx")
APPSETTINGS = Path(__file__).resolve().parents[1] / "appsettings.json"


def parse_conn(s: str) -> dict:
    parts = {}
    for p in s.split(";"):
        p = p.strip()
        if not p or "=" not in p:
            continue
        k, v = p.split("=", 1)
        parts[k.strip().lower()] = v.strip()
    return {
        "host": parts["host"],
        "dbname": parts["database"],
        "user": parts["username"],
        "password": parts["password"],
        "port": parts.get("port", "5432"),
        "sslmode": "require",
    }


def norm_nivel(v) -> str:
    if v is None:
        return ""
    if isinstance(v, (int, float)):
        return str(int(v))
    s = str(v).strip()
    s = re.sub(r"[°º]", "", s)
    return s


def norm_grupo(v) -> str:
    if v is None:
        return ""
    return str(v).strip()


def main() -> int:
    if not EXCEL.is_file():
        print(f"No existe el Excel: {EXCEL}", file=sys.stderr)
        return 1

    with APPSETTINGS.open(encoding="utf-8") as f:
        cfg = json.load(f)
    conn_kw = parse_conn(cfg["ConnectionStrings"]["DefaultConnection"])

    wb = openpyxl.load_workbook(EXCEL, read_only=True, data_only=True)
    ws = wb.active
    rows = list(ws.iter_rows(min_row=2, values_only=True))

    # email -> (nivel, grupo) primera fila válida
    by_email: dict[str, tuple[str, str]] = {}
    for row in rows:
        if not row or len(row) < 7:
            continue
        email = (row[0] or "").strip().lower() if row[0] else ""
        nivel = norm_nivel(row[5])
        grupo = norm_grupo(row[6])
        if not email or "@" not in email:
            continue
        if not nivel or not grupo:
            continue
        if email not in by_email:
            by_email[email] = (nivel, grupo)

    wb.close()
    print(f"Estudiantes únicos en Excel: {len(by_email)}")

    conn = psycopg2.connect(**conn_kw)
    conn.autocommit = False
    cur = conn.cursor()

    cur.execute(
        """
        SELECT id::text, school_id::text
        FROM academic_years
        WHERE is_active = true
        ORDER BY created_at DESC NULLS LAST
        LIMIT 1
        """
    )
    ay = cur.fetchone()
    if not ay:
        print("No hay año académico activo.", file=sys.stderr)
        return 1
    academic_year_id, school_id = ay[0], ay[1]

    cur.execute(
        "SELECT id::text FROM shifts WHERE LOWER(name) = 'noche' LIMIT 1"
    )
    r = cur.fetchone()
    if not r:
        print("No existe jornada Noche.", file=sys.stderr)
        return 1
    shift_id = r[0]

    inserted = 0
    skipped_no_user = 0
    skipped_no_grade = 0
    skipped_no_group = 0
    deactivated = 0

    for email, (nivel, grupo) in by_email.items():
        cur.execute(
            """
            SELECT id::text FROM users
            WHERE LOWER(email) = %s AND LOWER(role) IN ('estudiante', 'student')
            LIMIT 1
            """,
            (email,),
        )
        ur = cur.fetchone()
        if not ur:
            skipped_no_user += 1
            continue
        student_id = ur[0]

        cur.execute(
            "SELECT id::text FROM grade_levels WHERE LOWER(TRIM(name)) = LOWER(TRIM(%s)) LIMIT 1",
            (nivel,),
        )
        gr = cur.fetchone()
        if not gr:
            skipped_no_grade += 1
            continue
        grade_id = gr[0]

        cur.execute(
            """
            SELECT id::text FROM groups
            WHERE school_id = %s::uuid
              AND LOWER(TRIM(name)) = LOWER(TRIM(%s))
              AND shift_id = %s::uuid
            LIMIT 1
            """,
            (school_id, grupo, shift_id),
        )
        grr = cur.fetchone()
        if not grr:
            cur.execute(
                """
                SELECT id::text FROM groups
                WHERE school_id = %s::uuid AND LOWER(TRIM(name)) = LOWER(TRIM(%s))
                LIMIT 1
                """,
                (school_id, grupo),
            )
            grr = cur.fetchone()
        if not grr:
            skipped_no_group += 1
            continue
        group_id = grr[0]

        cur.execute(
            """
            UPDATE student_assignments
            SET is_active = false, end_date = NOW()
            WHERE student_id = %s::uuid AND is_active = true
            """,
            (student_id,),
        )
        deactivated += cur.rowcount

        cur.execute(
            """
            INSERT INTO student_assignments (
                id, student_id, grade_id, group_id, shift_id,
                is_active, academic_year_id, enrollment_type, start_date, created_at
            )
            VALUES (
                gen_random_uuid(), %s::uuid, %s::uuid, %s::uuid, %s::uuid,
                true, %s::uuid, 'Nocturno', NOW(), NOW()
            )
            """,
            (student_id, grade_id, group_id, shift_id, academic_year_id),
        )
        inserted += cur.rowcount or 1

    conn.commit()
    cur.close()
    conn.close()

    print(f"Año académico: {academic_year_id} | escuela: {school_id}")
    print(f"Matrículas insertadas (intentos): {inserted}")
    print(f"Asignaciones previas desactivadas (filas): {deactivated}")
    print(
        f"Omitidos — sin usuario: {skipped_no_user}, sin nivel en BD: {skipped_no_grade}, sin grupo: {skipped_no_group}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
