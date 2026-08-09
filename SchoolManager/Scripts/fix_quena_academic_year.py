import psycopg2
CANONICAL = "f7ccb57f-fa3e-4d9f-973b-552030c9852d"
LOADED = "205ba87c-842a-4c80-82c6-403fd1aafd35"
SCHOOL = "6e42399f-6f17-4585-b92e-fa4fff02cb65"

c = psycopg2.connect(
    host="dpg-d7erln5ckfvc73en9obg-a.oregon-postgres.render.com",
    database="schoolmanager_daqf",
    user="admin",
    password="iztY1ZL7WHbu2A5gtMSb1DFMhrK3Lo3r",
    port=5432,
    sslmode="require",
)
cur = c.cursor()

cur.execute(
    "UPDATE schedule_entries SET academic_year_id = %s WHERE academic_year_id = %s",
    (CANONICAL, LOADED),
)
print("Entradas migradas al año canónico:", cur.rowcount)

cur.execute(
    """
    UPDATE academic_years SET is_active = false
    WHERE school_id = %s AND name = '2026' AND id <> %s
    """,
    (SCHOOL, CANONICAL),
)
print("Años 2026 desactivados (excepto canónico):", cur.rowcount)

cur.execute("UPDATE academic_years SET is_active = true WHERE id = %s", (CANONICAL,))
cur.execute("SELECT COUNT(*) FROM schedule_entries WHERE academic_year_id = %s", (CANONICAL,))
print("Entradas en año canónico:", cur.fetchone()[0])

c.commit()
c.close()
print("Listo.")
