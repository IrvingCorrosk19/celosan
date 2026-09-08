# Informe — prueba local reversible Farid Alain / 10-A2

Fecha: 2026-09-05  
Escuela: `6e42399f-6f17-4585-b92e-fa4fff02cb65` (Centro de Educación Laboral Oficial San Miguelito)

## Identificadores reales

| Concepto | GUID |
|---|---|
| Farid Alain (user / teacher) | `f222d4db-ea3b-43a5-8a15-300d2944c051` |
| Julio E Del Cid CH (user / teacher) | `81751dc8-ddcf-4d11-9cea-ec5bd34792d0` |
| SubjectAssignment (Autotrónica + 10° + 10-A2 + TI) | `ac3d5f8c-2a0a-4875-ae8e-3365e847e669` |
| TeacherAssignment (la que se transfiere) | `c6a89793-8264-421a-afdc-d8562a60edac` |
| AcademicYear con horarios reales | `f7ccb57f-fa3e-4d9f-973b-552030c9852d` |
| ScheduleEntry | `b48ef295-8d6a-4f40-8391-7e716f9673db` |
| TimeSlot nocturno Bloque 4 | `0809045c-73f4-43fe-acd0-f2a2c38810ce` |

**Corrección:** `c6a89793-…` no es el TeacherId de Julio. Es el `teacher_assignments.id`. El usuario de Julio es `81751dc8-…`.

## Valores originales (antes de la prueba)

| Campo | Valor |
|---|---|
| teacher_assignments.teacher_id | `81751dc8-ddcf-4d11-9cea-ec5bd34792d0` (Julio) |
| teacher_assignments.subject_assignment_id | `ac3d5f8c-2a0a-4875-ae8e-3365e847e669` |
| schedule_entries.teacher_assignment_id | `c6a89793-8264-421a-afdc-d8562a60edac` (sin cambio) |
| Día | 4 (jueves) |
| Bloque | Bloque 4 · 20:40–21:30 · Noche |
| Año | `f7ccb57f-…` / 2026 |

Farid conserva su TeacherAssignment de `A2` (`9c2a33e7-…`) y sus 6 celdas en el año `837b80b3-…`. No se tocan.

## Restauración

Ejecutar `Scripts/postgres/rollback_farid_schedule_test.sql`. Devuelve `teacher_id` a Julio. No borra `schedule_entries`.
