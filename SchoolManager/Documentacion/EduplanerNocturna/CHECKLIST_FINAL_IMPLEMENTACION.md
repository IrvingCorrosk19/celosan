# Checklist final de implementacion — Eduplaner Nocturna

Fecha de validacion local: 2026-07-15 19:06

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
