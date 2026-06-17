# Fase 1 - Modelo Academico Nocturno Modular

Fecha: 2026-06-17

Alcance ejecutado: infraestructura base de esquema para el modelo academico nocturno modular. No se implementaron servicios, reglas academicas, flujos de matricula, promocion, notas, asistencia ni comportamiento modular activo.

## 1. Backup realizado

Antes de modificar codigo se genero un backup completo de PostgreSQL desde Render y se valido localmente con `pg_restore`.

| Campo | Valor |
|---|---|
| Ruta | `C:\Proyectos\EduplanerNoche\backups\fase1_modelo_nocturno_modular_20260617_184628\schoolmanager_render_full_fase1_20260617_184628.dump` |
| Directorio | `C:\Proyectos\EduplanerNoche\backups\fase1_modelo_nocturno_modular_20260617_184628` |
| Tamano | `402409` bytes |
| Fecha local | `2026-06-17 18:46:28` |
| SHA256 | `044C7381BE4E15F76A29252D31850333926F14265A1C6342C047A19FB4FB1565` |
| Validacion | `pg_restore --list OK`; `pg_restore --schema-only OK` |

No se aplicaron escrituras contra la base de datos de produccion durante el backup ni durante esta fase.

## 2. Migracion creada

Migracion EF Core creada:

`20260617235339_AddNocturnalModularAcademicFoundation`

Archivos:

- `Migrations/20260617235339_AddNocturnalModularAcademicFoundation.cs`
- `Migrations/20260617235339_AddNocturnalModularAcademicFoundation.Designer.cs`
- `Migrations/SchoolDbContextModelSnapshot.cs`

La migracion es aditiva: crea tablas nuevas y agrega columnas nullable a tablas existentes. No contiene `AlterColumn` ni elimina columnas/indices existentes en `Up`.

## 3. Tablas nuevas

Se agregaron los modelos y mapeos EF para las 7 tablas aprobadas en el plan:

1. `curriculum_tracks`
2. `curriculum_subjects`
3. `curriculum_subject_prerequisites`
4. `student_academic_period_enrollments`
5. `student_academic_credits`
6. `student_subject_equivalencies`
7. `student_subject_equivalency_items`

Archivo de modelos:

- `Models/ModularAcademicModels.cs`

DbSets agregados:

- `CurriculumTracks`
- `CurriculumSubjects`
- `CurriculumSubjectPrerequisites`
- `StudentAcademicPeriodEnrollments`
- `StudentAcademicCredits`
- `StudentSubjectEquivalencies`
- `StudentSubjectEquivalencyItems`

## 4. Columnas agregadas

### `student_subject_assignments`

Columnas nullable:

- `period_enrollment_id`
- `trimester_id`
- `curriculum_subject_id`
- `validation_status`
- `validation_message`

### `subject_promotion_records`

Columnas nullable:

- `trimester_id`
- `curriculum_subject_id`
- `academic_credit_id`

### `prematriculations`

Columnas nullable:

- `target_trimester_id`
- `entry_type`
- `requires_equivalence_review`

Nota: `requires_equivalence_review` fue dejado nullable para cumplir la regla de Fase 1 de no agregar columnas obligatorias a tablas existentes.

## 5. Indices creados

### Nuevas tablas

- `ix_curriculum_tracks_school_id`
- `ix_curriculum_tracks_academic_year_id`
- `ix_curriculum_tracks_active_school_year`
- `ix_curriculum_subjects_track`
- `ix_curriculum_subjects_subject`
- `ix_curriculum_subjects_grade_level`
- `uq_curriculum_subjects_track_subject_grade_order`
- `ix_curriculum_subject_prerequisites_subject`
- `ix_curriculum_subject_prerequisites_prerequisite`
- `uq_curriculum_subject_prerequisite_pair`
- `ix_student_academic_period_enrollments_school`
- `ix_student_academic_period_enrollments_trimester`
- `uq_student_academic_period_enrollments_active_period`
- `ix_student_academic_credits_student`
- `ix_student_academic_credits_subject`
- `uq_student_academic_credits_valid_subject`
- `ix_student_subject_equivalencies_school`
- `ix_student_subject_equivalencies_student`
- `ix_student_subject_equivalency_items_equivalency`
- `ix_student_subject_equivalency_items_curriculum_subject`

### Tablas existentes

- `ix_student_subject_assignments_period_enrollment_id`
- `ix_student_subject_assignments_trimester_id`
- `ix_student_subject_assignments_curriculum_subject_id`
- `ix_subject_promotion_records_trimester_id`
- `ix_subject_promotion_records_curriculum_subject_id`
- `ix_subject_promotion_records_academic_credit_id`
- `ix_prematriculations_target_trimester_id`

No se eliminaron indices existentes.

## 6. Feature flag

Se agrego configuracion inactiva en `appsettings.json`:

```json
"NocturnalModularEnrollment": {
  "Enabled": false
}
```

No se registro ni se uso el flag en servicios o flujos. El comportamiento modular permanece apagado.

## 7. Validaciones ejecutadas

### `dotnet restore`

Resultado:

- Exitoso.
- Todos los proyectos estaban actualizados para restore.

### `dotnet build`

Resultado final:

- `Build succeeded.`
- `0 Warning(s)`
- `0 Error(s)`

### Lints

Resultado:

- Sin errores de linter en los archivos modificados.

## 8. Riesgos detectados

1. **Indices unicos con datos futuros.** Los indices unicos nuevos no afectan datos actuales porque las tablas nuevas nacen vacias, pero deben revisarse antes de backfills historicos.
2. **Campos nullable en tablas existentes.** Es intencional para compatibilidad; fases futuras deberan validar obligatoriedad solo cuando el modo modular este activo.
3. **Filtros multi-tenant por navegacion.** Las tablas nuevas sin `school_id` directo dependen de relaciones hacia malla o equivalencia para aislamiento. Esto debe probarse cuando se creen servicios.
4. **No hay comportamiento modular activo.** Esta fase solo prepara esquema; no bloquea prerrequisitos ni habilita matricula trimestral todavia.
5. **No se aplico migracion a produccion.** La migracion queda lista en codigo; aplicar a Render debe hacerse como paso operativo separado con aprobacion y backup vigente.

## 9. Confirmacion de alcance

No se implemento Fase 2.

No se crearon:

- `CurriculumService`
- `ModularEnrollmentService`
- `AcademicPrerequisiteService`
- `AcademicCreditService`
- `EquivalencyService`
- `ModularPromotionService`

No se modificaron:

- `StudentAssignmentService`
- `PrematriculationService`
- `TeacherGradebook`
- `Attendance`
- `StudentReport`
- `AprobadosReprobados`
- carga masiva
- portal estudiante
- portal docente
- promocion
- notas
- asistencia

## 10. Resultado

Fase 1 completada como infraestructura de esquema base, sin activar reglas academicas ni modificar comportamiento funcional.
