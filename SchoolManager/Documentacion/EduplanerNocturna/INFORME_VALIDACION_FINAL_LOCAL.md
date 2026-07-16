# Informe de validacion final — instalacion local

**Fecha:** 2026-07-15 19:06  
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

---

## Corrección SSA (2026-07-16)

**Problema:** con NocturnalAdvancedEnrollment activo, la matrícula primaria no creaba student_subject_assignments.

**Comportamiento correcto (escuelas nocturnas Panamá):**
- Matrícula **Nocturno/Regular** → auto-inscribe en todas las imparticiones activas del grado+grupo.
- Arrastre **Refuerzo/Libre** → no auto-inscribe (materia por materia).
- Al inactivar matrícula → se inactivan SSA vinculadas.

**Evidencia local:** rematrícula de estudiante.prueba@celosam.local → 3 SSA activas (MATEMATICA, FISICA, ESPAÑOL) tipo Nocturno.
