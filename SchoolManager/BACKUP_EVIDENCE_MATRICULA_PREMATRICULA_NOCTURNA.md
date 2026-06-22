# Evidencia de backup - matrícula/prematrícula nocturna

Fecha/hora local: 2026-06-22 06:18

## Alcance

Antes de implementar cambios para matrícula/prematrícula nocturna CELOSAM se creó respaldo de aplicación, respaldo completo de base de datos Render Producción y snapshots de tablas críticas. Hasta este punto no se han ejecutado `INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`, `DROP` ni migraciones contra producción.

## Backup de aplicación

Ubicación:

`C:\Proyectos\EduplanerNoche\backups\matricula_prematricula_nocturna_20260622_061825\application\SchoolManager`

Comando usado:

```powershell
robocopy "c:\Proyectos\EduplanerNoche\SchoolManager" "...\application\SchoolManager" /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NP
```

Resultado:

- Directorios copiados: 372
- Archivos copiados: 1,863
- Tamaño copiado: 711.77 MB
- Fallos: 0

## Backup de base de datos

Cliente:

`C:\Program Files\PostgreSQL\18\bin`

Base:

Render Producción `schoolmanager_daqf`

Archivos creados:

- Custom backup: `C:\Proyectos\EduplanerNoche\backups\matricula_prematricula_nocturna_20260622_061825\database\schoolmanager_render_prod_20260622_061849.backup`
- SQL plain: `C:\Proyectos\EduplanerNoche\backups\matricula_prematricula_nocturna_20260622_061825\database\schoolmanager_render_prod_20260622_061849.sql`
- Verificación TOC: `C:\Proyectos\EduplanerNoche\backups\matricula_prematricula_nocturna_20260622_061825\database\pg_restore_toc_verify.txt`

Verificación:

- `.backup`: 503,611 bytes
- `.sql`: 1,057,529 bytes
- TOC `pg_restore -l`: 797 líneas
- SQL plain: 8,075 líneas

La verificación `pg_restore -l` leyó correctamente el archivo custom y generó el listado TOC.

## Snapshots previos

Ubicación:

`C:\Proyectos\EduplanerNoche\backups\matricula_prematricula_nocturna_20260622_061825\snapshots`

Archivos:

- `students_activos.csv`
- `student_assignments_activos.csv`
- `student_subject_assignments.csv`
- `subjects.csv`
- `subject_assignments.csv`
- `grade_levels.csv`
- `groups.csv`
- `shifts.csv`
- `academic_years.csv`
- `prematriculation_periods.csv`
- `curriculum_tracks.csv`
- `curriculum_subjects.csv`
- `prematriculations.csv`
- `snapshot_before_counts.txt`

Conteos previos:

```text
academic_years                      | 13
curriculum_subjects                 | 0
curriculum_tracks                   | 0
grade_levels                        | 6
groups                              | 23
prematriculation_periods            | 0
prematriculations                   | 0
shifts                              | 3
student_assignments_activos         | 349
students_activos                    | 299
student_subject_assignments         | 319
student_subject_assignments_activos | 291
subject_assignments                 | 435
subjects                            | 83
```

## Observaciones de seguridad

- Los backups se guardaron fuera del directorio `SchoolManager` para evitar recursión.
- El respaldo SQL y el snapshot fueron creados con operaciones de lectura.
- Cualquier cambio posterior de datos debe ejecutarse con scripts transaccionales y rollback asociado.
