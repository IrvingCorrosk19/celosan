"""Genera _matricula_bulk.sql desde el Excel (sin conexión a BD)."""
import re
from pathlib import Path

import openpyxl

EXCEL = Path(r"c:\Users\irvin\Downloads\asignaciones_estudiantes_grado_grupo (25).xlsx")
OUT = Path(__file__).resolve().parent / "_matricula_bulk.sql"


def norm_nivel(v):
    if v is None:
        return ""
    if isinstance(v, (int, float)):
        return str(int(v))
    s = str(v).strip()
    return re.sub(r"[°\u00ba]", "", s)


def norm_grupo(v):
    if v is None:
        return ""
    return str(v).strip()


def esc(s: str) -> str:
    return s.replace("'", "''")


def main():
    wb = openpyxl.load_workbook(EXCEL, read_only=True, data_only=True)
    ws = wb.active
    m = {}
    for row in ws.iter_rows(min_row=2, values_only=True):
        if not row or len(row) < 7:
            continue
        em = (row[0] or "").strip().lower() if row[0] else ""
        if not em or "@" not in em:
            continue
        nv, gv = norm_nivel(row[5]), norm_grupo(row[6])
        if not nv or not gv:
            continue
        if em not in m:
            m[em] = (nv, gv)
    wb.close()

    vals = []
    for em, (nv, gv) in m.items():
        vals.append(f"  ('{esc(em)}','{esc(nv)}','{esc(gv)}')")

    values_sql = ",\n".join(vals)
    sql = f"""BEGIN;
CREATE TEMP TABLE expo_import (email text, nivel text, grupo text) ON COMMIT DROP;
INSERT INTO expo_import (email, nivel, grupo) VALUES
{values_sql};

UPDATE student_assignments sa
SET is_active = false, end_date = NOW()
WHERE sa.is_active = true
  AND EXISTS (
    SELECT 1
    FROM users u
    JOIN expo_import e ON LOWER(u.email) = LOWER(e.email)
    WHERE u.id = sa.student_id
      AND LOWER(u.role) IN ('estudiante', 'student')
  );

INSERT INTO student_assignments (
  id, student_id, grade_id, group_id, shift_id,
  is_active, academic_year_id, enrollment_type, start_date, created_at
)
SELECT
  gen_random_uuid(),
  u.id,
  gl.id,
  g.id,
  noche.id,
  true,
  ay.id,
  'Nocturno',
  NOW(),
  NOW()
FROM expo_import e
JOIN users u ON LOWER(u.email) = LOWER(e.email)
  AND LOWER(u.role) IN ('estudiante', 'student')
JOIN grade_levels gl ON LOWER(TRIM(gl.name)) = LOWER(TRIM(e.nivel))
CROSS JOIN (SELECT id FROM shifts WHERE LOWER(TRIM(name)) = 'noche' LIMIT 1) noche
CROSS JOIN (
  SELECT id FROM academic_years
  WHERE is_active = true
  ORDER BY created_at DESC NULLS LAST
  LIMIT 1
) ay
JOIN groups g ON g.school_id = u.school_id
  AND LOWER(TRIM(g.name)) = LOWER(TRIM(e.grupo))
  AND g.shift_id = noche.id
WHERE NOT EXISTS (
  SELECT 1
  FROM student_assignments x
  WHERE x.student_id = u.id
    AND x.grade_id = gl.id
    AND x.group_id = g.id
    AND x.shift_id = noche.id
    AND x.academic_year_id = ay.id
    AND x.is_active = true
);

COMMIT;
"""
    OUT.write_text(sql, encoding="utf-8")
    print(f"Estudiantes únicos: {len(m)} -> {OUT}")


if __name__ == "__main__":
    main()
