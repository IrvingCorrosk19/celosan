# -*- coding: utf-8 -*-
"""Genera checklist, matrices XLSX e informe de validacion local (instalacion limpia)."""
from __future__ import annotations

from datetime import datetime
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Documentacion" / "EduplanerNocturna"
OUT.mkdir(parents=True, exist_ok=True)

NOW = datetime.now().strftime("%Y-%m-%d %H:%M")
HEADER_FILL = PatternFill("solid", fgColor="1F4E79")
HEADER_FONT = Font(color="FFFFFF", bold=True)
OK_FILL = PatternFill("solid", fgColor="C6EFCE")
WARN_FILL = PatternFill("solid", fgColor="FFEB9C")
FAIL_FILL = PatternFill("solid", fgColor="FFC7CE")


def style_header(ws, ncols: int):
    for c in range(1, ncols + 1):
        cell = ws.cell(1, c)
        cell.fill = HEADER_FILL
        cell.font = HEADER_FONT
        cell.alignment = Alignment(wrap_text=True, vertical="center")


def autosize(ws, widths):
    for i, w in enumerate(widths, start=1):
        ws.column_dimensions[chr(64 + i) if i <= 26 else "A"].width = w
    # fallback for many cols
    from openpyxl.utils import get_column_letter

    for i, w in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = w


def write_matrix_dependencias():
    wb = Workbook()
    ws = wb.active
    ws.title = "Dependencias"
    headers = [
        "Orden",
        "Paso",
        "Ruta / accion",
        "Depende de",
        "Produce",
        "Rol minimo",
        "Validado local",
        "Notas operativas",
    ]
    rows = [
        [1, "Crear escuela + admin", "/SuperAdmin/CreateSchoolWithAdmin", "Superadmin en BD", "schools + users(admin) + academic_years auto", "superadmin", "OK", "Anio academico 2026 se crea automatico (sin /AcademicYear/Index)"],
        [2, "Jornada Noche", "/AcademicCatalog CreateShift o SaveCatalog", "Escuela", "shifts(Noche)", "admin", "OK", "SaveCatalog llama GetOrCreateBySchoolAndNameAsync('Noche')"],
        [3, "Catalogo + imparticiones", "/AcademicCatalog/SaveCatalog o pestañas", "Escuela + jornada", "subjects/groups/grades/areas/specialties + subject_assignments", "admin", "OK", "Grupos quedan con shift_id=Noche"],
        [4, "Trimestres", "/AcademicCatalog/GuardarTrimestres", "Escuela", "trimester", "admin", "OK", "Activar un trimestre NO desactiva los demas"],
        [5, "Usuarios (docente/estudiante)", "/User/CreateJson o UI", "Escuela", "users", "admin", "OK", "Roles string en users.role"],
        [6, "Asignacion docente", "POST /SaveAssignments", "subject_assignments + teacher", "teacher_assignments", "admin", "OK", "Ruta raiz /SaveAssignments (atributo HttpPost), no /TeacherAssignment/SaveAssignments"],
        [7, "Bloques nocturnos", "/ScheduleConfiguration/SaveConfiguration", "Escuela", "school_schedule_configurations + time_slots", "admin", "OK", "Genera Bloque 1-6 + Recreo desde 18:00"],
        [8, "Horario por docente", "/Schedule/ByTeacher + SaveEntry", "teacher_assignments + time_slots + academic_years", "schedule_entries", "admin", "OK", "groupName puede venir null en JSON de respuesta"],
        [9, "Periodo prematricula", "/PrematriculationPeriod/Create", "academic_years (+ trimester opcional)", "prematriculation_periods", "admin", "OK", "DTO no exige RequiredAmount en Create aunque columna exista"],
        [10, "Matricula (StudentAssignment)", "/StudentAssignment/UpdateGroupAndGrade", "grade + group(+shift)", "student_assignments", "admin", "OK", "Binding por form/query, no JSON body. NO crea automatico student_subject_assignments"],
        [11, "Inscripcion por materia", "Flujo prematricula modular / SSA", "student_assignments + subject_assignments", "student_subject_assignments", "admin/estudiante", "PARCIAL", "Tras matrícula directa SSA=0; notas/asistencia avanzadas dependen de SSA/modular"],
        [12, "Cuaderno docente", "/TeacherGradebook/Index", "teacher_assignments (+ alumnos)", "activities/scores", "teacher", "OK pantalla", "/TeacherGradebook sin Index -> 404"],
        [13, "Asistencia", "/Attendance/Index", "Escuela + alumnos", "attendance", "teacher/admin", "OK pantalla", "Puede exigir grupo/grado/shift"],
    ]
    ws.append(headers)
    for r in rows:
        ws.append(r)
        fill = OK_FILL if r[6] == "OK" or r[6].startswith("OK") else WARN_FILL
        ws.cell(ws.max_row, 7).fill = fill
    style_header(ws, len(headers))
    autosize(ws, [8, 28, 40, 32, 36, 12, 14, 55])
    wb.save(OUT / "MATRIZ_DEPENDENCIAS_CONFIGURACION.xlsx")


def write_matrix_roles():
    wb = Workbook()
    ws = wb.active
    ws.title = "RolesPermisos"
    headers = [
        "Rol",
        "Login",
        "Crear escuela",
        "Catalogo academico",
        "Asignacion docente",
        "Config horarios noche",
        "Horario ByTeacher",
        "Prematricula periodo",
        "Matricula estudiante",
        "Gradebook",
        "Asistencia",
        "Fuente",
    ]
    rows = [
        ["superadmin", "Si", "Si", "No (sin escuela)", "No", "No", "No", "No", "No", "No", "No", "Authorize Roles=superadmin"],
        ["admin", "Si", "No", "Si", "Si", "Si", "Si", "Si", "Si", "Si (ver)", "Si", "Policies + Roles string"],
        ["director", "Si", "No", "Si", "Si", "Si", "Si", "Si", "Si", "Si", "Si", "AcademicCatalog + ScheduleConfiguration"],
        ["secretaria", "Si", "No", "Si", "Si", "No tipico", "Posible", "Si", "Si", "Limitado", "Limitado", "AcademicCatalog roles"],
        ["teacher", "Si", "No", "No", "Propia", "No", "Propia", "No", "No", "Si", "Si", "TeacherGradebook / Attendance"],
        ["estudiante", "Si", "No", "No", "No", "No", "Ver propia", "Solicitar", "No", "Ver notas", "No", "Prematricula modular"],
        ["contable/contabilidad", "Si", "No", "No", "No", "No", "No", "Pago", "No", "No", "No", "Pagos"],
        ["acudiente/parent", "Si", "No", "No", "No", "No", "No", "No", "No", "Ver", "No", "ParentAcademic"],
    ]
    ws.append(headers)
    for r in rows:
        ws.append(r)
    style_header(ws, len(headers))
    autosize(ws, [18, 8, 12, 16, 16, 18, 14, 16, 16, 12, 12, 28])

    ws2 = wb.create_sheet("HallazgosRoles")
    ws2.append(["Hallazgo", "Severidad", "Estado"])
    ws2.append(["Los roles son strings en users.role (no Identity Role store completo)", "Media", "Documentado"])
    ws2.append(["SaveAssignments responde en /SaveAssignments (ruta raiz)", "Baja", "Funciona vía Url.Action"])
    ws2.append(["Authorize usa variantes de mayusculas en varios controllers", "Baja", "Mitigado por listas Roles=admin,Admin,..."])
    style_header(ws2, 3)
    autosize(ws2, [70, 12, 30])
    wb.save(OUT / "MATRIZ_ROLES_PERMISOS.xlsx")


def write_matrix_config_inicial():
    wb = Workbook()
    ws = wb.active
    ws.title = "ConfigInicial"
    headers = ["Campo/Entidad", "Obligatorio", "Valor prueba local", "Donde se carga", "Validado"]
    rows = [
        ["BD PostgreSQL", "Si", "eduplaner_nocturna_local", "appsettings.Development.json", "OK"],
        ["Usuario DB", "Si", "postgres", "connection string", "OK"],
        ["Migraciones EF", "Si", "38 aplicadas", "dotnet ef database update", "OK"],
        ["Superadmin", "Si", "superadmin@schoolmanager.com / Admin123!", "--create-initial-superadmin", "OK"],
        ["Escuela", "Si", "CELOSAM Nocturna Prueba", "CreateSchoolWithAdmin", "OK"],
        ["Admin escuela", "Si", "admin.prueba@celosam.local / Admin123!", "CreateSchoolWithAdmin", "OK"],
        ["Anio academico", "Si", "2026 (auto)", "al crear escuela", "OK"],
        ["Jornada", "Si", "Noche", "AcademicCatalog/CreateShift", "OK"],
        ["Especialidad/Area/Materia/Grado/Grupo", "Si", "Educacion Media / Ciencias / Matematica / 10 / A", "SaveCatalog", "OK"],
        ["SubjectAssignment", "Si", "4 imparticiones", "SaveCatalog", "OK"],
        ["Trimestres", "Si", "T1 activo + T2/T3", "GuardarTrimestres", "OK"],
        ["Docente", "Si", "docente.prueba@celosam.local / Teacher123!", "User/CreateJson", "OK"],
        ["Estudiante", "Si", "estudiante.prueba@celosam.local / Student123!", "User/CreateJson", "OK"],
        ["TeacherAssignment", "Si", "1 (Matematica 10-A)", "/SaveAssignments", "OK"],
        ["ScheduleConfiguration", "Si", "18:00 x 45min x 6 + recreo 15", "SaveConfiguration", "OK"],
        ["TimeSlots", "Si", "7 filas nocturnas", "generacion automatica", "OK"],
        ["ScheduleEntry", "Si", "Lunes Bloque 1 Matematica", "Schedule/SaveEntry", "OK"],
        ["PrematriculationPeriod", "Recomendado", "Periodo Prematricula Local 2026", "PrematriculationPeriod/Create", "OK"],
        ["StudentAssignment", "Si", "estudiante -> grado 10 grupo A", "UpdateGroupAndGrade form", "OK"],
        ["StudentSubjectAssignment", "Para notas por materia", "(no auto al matricular)", "flujo modular/prematricula", "PARCIAL"],
        ["Cloudinary", "Solo fotos", "placeholders -> warning/crit", "Program.cs startup", "WARN"],
        ["App URL local", "Si", "http://localhost:5172", "launch profile http", "OK"],
    ]
    ws.append(headers)
    for r in rows:
        ws.append(r)
        if r[4] == "OK":
            ws.cell(ws.max_row, 5).fill = OK_FILL
        elif r[4] == "PARCIAL":
            ws.cell(ws.max_row, 5).fill = WARN_FILL
        else:
            ws.cell(ws.max_row, 5).fill = WARN_FILL
    style_header(ws, len(headers))
    autosize(ws, [34, 18, 48, 28, 12])
    wb.save(OUT / "MATRIZ_CONFIGURACION_INICIAL.xlsx")


def write_checklist():
    path = OUT / "CHECKLIST_FINAL_IMPLEMENTACION.md"
    path.write_text(
        f"""# Checklist final de implementacion — Eduplaner Nocturna

Fecha de validacion local: {NOW}

## A. Instalacion limpia (PostgreSQL 18 local)

- [x] Crear BD `eduplaner_nocturna_local`
- [x] Connection string Development apunta a localhost / postgres
- [x] `dotnet ef database update` completo (38 migraciones)
- [x] Fix migracion: crear `shifts` antes de `time_slots`
- [x] Fix migracion: agregar `student_assignments.shift_id` y `groups.shift_id` en instalacion limpia
- [x] Crear superadmin (`dotnet run -- --create-initial-superadmin`)
- [x] App escuchando en `http://0.0.0.0:5172`
- [x] `/Auth/Login` HTTP 200

## B. Configuracion institucional (orden real validado)

- [x] Superadmin crea escuela + admin
- [x] Anio academico auto-creado (2026)
- [x] Jornada **Noche**
- [x] Catalogo SaveCatalog (especialidad/area/materia/grado/grupo + SubjectAssignment)
- [x] Trimestres T1/T2/T3 (T1 activo)
- [x] Usuarios docente y estudiante
- [x] TeacherAssignment via `/SaveAssignments`
- [x] ScheduleConfiguration nocturna -> time_slots 18:00–22:45
- [x] Schedule entry (Lunes Bloque 1)
- [x] PrematriculationPeriod creado
- [x] StudentAssignment (matricula) via UpdateGroupAndGrade
- [ ] StudentSubjectAssignment automatico al matricular (NO ocurre hoy)
- [x] Pantallas Gradebook y Attendance cargan para docente

## C. Criterios de aceptacion

| Criterio | Estado |
|---|---|
| Instalacion desde cero sin pasos SQL manuales extras | OK tras fixes de migracion |
| Flujo config sin tocar codigo | OK hasta horario + matrícula |
| Solo jornada noche en bloques generados | OK (7 slots nocturnos) |
| Documentacion alineada al sistema real | Ver informe + matrices |

## D. Credenciales de prueba local

| Rol | Email | Password |
|---|---|---|
| superadmin | superadmin@schoolmanager.com | Admin123! |
| admin escuela | admin.prueba@celosam.local | Admin123! |
| teacher | docente.prueba@celosam.local | Teacher123! |
| estudiante | estudiante.prueba@celosam.local | Student123! |

## E. Pendientes / no bloqueantes de arranque

1. Cloudinary sin credenciales reales (solo afecta upload de fotos).
2. Matricula directa no sincroniza `student_subject_assignments` (necesita flujo modular/prematricula o sync).
3. URL `/TeacherGradebook` sin `/Index` responde 404.
4. Activar trimestre no desactiva otros.
""",
        encoding="utf-8",
    )


def write_validation_report():
    path = OUT / "INFORME_VALIDACION_FINAL_LOCAL.md"
    path.write_text(
        f"""# Informe de validacion final — instalacion local

**Fecha:** {NOW}  
**Ambiente:** Windows + PostgreSQL 18 + ASP.NET Core 8  
**BD:** `eduplaner_nocturna_local` (usuario `postgres`)  
**App:** `http://localhost:5172`

## Resumen ejecutivo

Se logro una **instalacion limpia migrada**, superadmin operativo, escuela de prueba configurada con jornada nocturna, catalogo, horario y matrícula basica. Se corrigieron fallos de migracion que impedian `database update` en BD vacia.

| Metrica | Valor |
|---|---|
| Migraciones aplicadas | 38 |
| Modulos/pantallas probados (HTTP) | 14+ |
| Flujos POST validados | Escuela, Shift, Catalog, Trimestres, Users, TA, ScheduleConfig, ScheduleEntry, Periodo, Matricula |
| Incidencias criticas de migracion corregidas | 2 |
| Incidencias funcionales documentadas | 4 |
| Estado global arranque | **OPERABLE para configuracion inicial nocturna** |

## Incidencias encontradas y estado

| # | Incidencia | Severidad | Estado |
|---|---|---|---|
| 1 | Migracion `AddScheduleModule` fallaba: `shifts` no existia al crear `time_slots` | Critica | **Corregida** (CREATE IF NOT EXISTS shifts) |
| 2 | Migracion `CompletePrematriculationModule` asumia `shift_id` ya existente | Critica | **Corregida** (SQL idempotente shifts + shift_id en student_assignments/groups) |
| 3 | `SaveAssignments` vive en `/SaveAssignments` (ruta raiz) | Baja | Funcional; documentada |
| 4 | `UpdateGroupAndGrade` no acepta JSON body (form/query) | Baja | Documentada (UI real usa form) |
| 5 | Matricula no crea `student_subject_assignments` | Media | Documentada; notas por materia/modular dependen de SSA |
| 6 | Cloudinary placeholders hacen warn/crit al arrancar | Media (fotos) | Documentada |
| 7 | `/Gradebook/Index` y `/TeacherGradebook` (sin Index) 404 | Baja | Ruta correcta `/TeacherGradebook/Index` |
| 8 | `groupName` null en respuesta SaveEntry | Baja | Entrada si se guarda |

## Pantallas / procesos validados

| Modulo | Ruta | Resultado |
|---|---|---|
| Login | /Auth/Login | 200 + cookie |
| SuperAdmin | /SuperAdmin/Index, CreateSchoolWithAdmin | 200 + escuela creada |
| Home admin | /Home/Index | 200 |
| Catalogo | /AcademicCatalog/Index | 200 + SaveCatalog OK |
| SubjectAssignment | /SubjectAssignment/Index | 200 |
| Users | /User/Index + CreateJson | 200 |
| TeacherAssignment | /TeacherAssignment/Index + /SaveAssignments | 200 |
| ScheduleConfiguration | /ScheduleConfiguration/Index + Save | 200 + 7 time_slots noche |
| TimeSlot | /TimeSlot/Manage | 200 |
| Schedule | /Schedule/ByTeacher + SaveEntry | 200 + 1 entry |
| PrematriculationPeriod | /PrematriculationPeriod/Index + Create | 200 + 1 periodo |
| StudentAssignment | /StudentAssignment/Index + UpdateGroupAndGrade | 200 + 1 matrícula |
| TeacherGradebook | /TeacherGradebook/Index | 200 (rol teacher) |
| Attendance | /Attendance/Index | 200 |

## Datos locales generados

- Escuela: CELOSAM Nocturna Prueba (`25a5142d-8f5d-4463-a68d-42a56880dea0`)
- Shift: Noche
- Imparticiones: 4
- Time slots nocturnos: Bloque 1–6 + Recreo (18:00–22:45)
- Schedule entries: 1 (Lunes Matematica Bloque 1)

## Orden operativo real (validado)

1. Superadmin → escuela+admin  
2. Admin → jornada Noche  
3. Catalogo (o SaveCatalog) → SubjectAssignment  
4. Trimestres  
5. Usuarios  
6. TeacherAssignment  
7. ScheduleConfiguration (noche) → TimeSlots  
8. Schedule/ByTeacher  
9. PrematriculationPeriod  
10. StudentAssignment (matrícula)  
11. (Luego) prematricula/modular para SSA → Gradebook scores completos

## Archivos entregables

- `CHECKLIST_FINAL_IMPLEMENTACION.md`
- `MATRIZ_DEPENDENCIAS_CONFIGURACION.xlsx`
- `MATRIZ_ROLES_PERMISOS.xlsx`
- `MATRIZ_CONFIGURACION_INICIAL.xlsx`
- `ANALISIS_COMPLETO_EDUPLANER_NOCTURNA.md` (actualizado)
- `MANUAL_PREMIUM_CONFIGURACION_INICIAL_EDUPLANER_NOCTURNA.docx` (regenerar con script premium si aplica)
- este informe

## Conclusion

La instalacion limpia local **queda operable** para configurar una institucion nocturna de punta a punta hasta horario y matrícula. El punto pendiente de producto mas relevante para calificaciones por materia es la **sincronizacion/creacion de `student_subject_assignments`**, que hoy no ocurre automaticamente al crear solo `student_assignments`.
""",
        encoding="utf-8",
    )


def update_analisis():
    path = OUT / "ANALISIS_COMPLETO_EDUPLANER_NOCTURNA.md"
    extra = f"""

---

## Anexo — Validacion instalacion limpia local ({NOW})

### Fixes de migracion aplicados

1. `Migrations/20260216194827_AddScheduleModule.cs`: crea `shifts` con `IF NOT EXISTS` antes de `time_slots`.
2. `Migrations/20251115111847_CompletePrematriculationModule.cs`: ya no asume schema legacy; crea `shifts` + columnas `shift_id` en `student_assignments` y `groups` de forma idempotente.

### Evidencia operativa

- BD: `eduplaner_nocturna_local`, 38 migraciones.
- App: `http://localhost:5172`.
- Escuela prueba + jornada Noche + 7 bloques nocturnos + 1 schedule_entry + 1 student_assignment.

### Hallazgo de producto relevante

`StudentAssignment` (matrícula) **no** materializa automaticamente `student_subject_assignments`. El libro de calificaciones avanzado / modular espera SSA.

Ver tambien: `INFORME_VALIDACION_FINAL_LOCAL.md` y matrices XLSX en esta carpeta.
"""
    if path.exists():
        text = path.read_text(encoding="utf-8")
        if "Anexo — Validacion instalacion limpia local" not in text:
            path.write_text(text.rstrip() + extra, encoding="utf-8")
        else:
            # replace annex roughly by appending a fresh block marker
            idx = text.find("## Anexo — Validacion instalacion limpia local")
            path.write_text(text[:idx].rstrip() + extra, encoding="utf-8")
    else:
        path.write_text("# Analisis Eduplaner Nocturna\n" + extra, encoding="utf-8")


def main():
    write_matrix_dependencias()
    write_matrix_roles()
    write_matrix_config_inicial()
    write_checklist()
    write_validation_report()
    update_analisis()
    print("Deliverables written to", OUT)


if __name__ == "__main__":
    main()
