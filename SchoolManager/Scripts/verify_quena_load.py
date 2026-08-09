import psycopg2, psycopg2.extras

CANONICAL = "f7ccb57f-fa3e-4d9f-973b-552030c9852d"
c = psycopg2.connect(
    host="dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com",
    database="schoolmanager_daqf", user="admin",
    password="iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r", port=5432, sslmode="require",
)
cur = c.cursor(cursor_factory=psycopg2.extras.RealDictCursor)

cur.execute("SELECT COUNT(*) c FROM schedule_entries WHERE academic_year_id=%s", (CANONICAL,))
print("Total horarios 2026:", cur.fetchone()["c"])

cur.execute("""
SELECT u.name||' '||u.last_name docente, COUNT(*) entradas
FROM schedule_entries se
JOIN teacher_assignments ta ON se.teacher_assignment_id=ta.id
JOIN users u ON ta.teacher_id=u.id
WHERE se.academic_year_id=%s AND lower(u.role) IN ('teacher','docente')
GROUP BY u.name, u.last_name ORDER BY u.last_name
""", (CANONICAL,))
total = 0
for r in cur.fetchall():
    print(f"  {r['docente']}: {r['entradas']}")
    total += r["entradas"]
print("Suma docentes:", total)

cur.execute("SELECT COUNT(*) c FROM teacher_assignments")
print("Teacher assignments:", cur.fetchone()["c"])
c.close()
