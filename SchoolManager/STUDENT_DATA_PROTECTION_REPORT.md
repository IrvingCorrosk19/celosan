# Reporte de protección de datos de estudiantes

Fecha: 2026-06-22  
Fase: 6 - validación de protección de estudiantes  
Base: Render Producción

## Regla aplicada

No se borraron estudiantes, matrículas, materias, grupos ni `subject_assignments`. Las fases ejecutadas solo:

- desactivaron años académicos duplicados 2026, sin borrar registros ni referencias;
- crearon un período activo de prematrícula;
- crearon una malla curricular modular derivada de `subject_assignments` existentes.

## Conteos protegidos

| Elemento | Antes | Después | Resultado |
|---|---:|---:|---|
| Estudiantes activos | 299 | 299 | Sin pérdida |
| `student_assignments` activos | 349 | 349 | Sin pérdida |
| `student_subject_assignments` activos | 291 | 291 | Sin pérdida |
| `subjects` | 83 | 83 | Sin pérdida |
| `subject_assignments` | 435 | 435 | Sin pérdida |

## Nuevos registros funcionales

| Elemento | Antes | Después |
|---|---:|---:|
| Años académicos 2026 activos | 13 | 1 |
| Períodos activos de prematrícula | 0 | 1 |
| Curriculum tracks activos | 0 | 1 |
| Curriculum subjects activos | 0 | 285 |

## Validaciones de huérfanos

| Validación | Resultado |
|---|---:|
| Matrículas activas sin grado | 0 |
| Matrículas activas sin grupo | 0 |
| Materias activas sin `subject_assignment` | 0 |
| Prematrículas sin estudiante | 0 |
| Estudiantes activos sin matrícula activa | 1 |
| Matrículas activas sin `shift_id` | 1 |
| Grupos con `shift='Noche'` sin `shift_id` | 1 |

## Observaciones

Los tres hallazgos no bloqueantes parecen preexistentes porque los snapshots previos ya mostraban estudiantes y asignaciones activas antes del cambio. No se corrigieron automáticamente para evitar cambios no estrictamente necesarios en datos individuales.

Acciones recomendadas posteriores:

- Revisar el estudiante activo sin matrícula activa.
- Revisar la matrícula activa sin `shift_id`.
- Revisar el grupo nocturno con texto `Noche` pero sin `shift_id`.

## Conclusión

La implementación de período y malla modular no redujo conteos protegidos ni dejó registros académicos huérfanos en las tablas críticas revisadas.
