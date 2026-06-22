# Reporte final - matrícula/prematrícula nocturna CELOSAM

Fecha: 2026-06-22  
Rama: `fix/nocturna-matricula-prematricula-completa`  
Base: Render Producción  
Estado: implementación aplicada con backup previo, scripts transaccionales y compilación correcta.

## 1. Backup creado

Evidencia completa:

`BACKUP_EVIDENCE_MATRICULA_PREMATRICULA_NOCTURNA.md`

Ubicación del backup:

`C:\Proyectos\EduplanerNoche\backups\matricula_prematricula_nocturna_20260622_061825`

Incluye:

- Copia completa de aplicación: 1,863 archivos, 711.77 MB, 0 fallos.
- Backup PostgreSQL custom: `schoolmanager_render_prod_20260622_061849.backup`.
- Backup PostgreSQL SQL plain: `schoolmanager_render_prod_20260622_061849.sql`.
- Verificación `pg_restore -l`: 797 líneas.
- Snapshots CSV de tablas críticas antes de cambios.

## 2. Archivos modificados

Código:

- `Controllers/PrematriculationController.cs`
- `Services/Implementations/PrematriculationService.cs`
- `Services/Implementations/CelosamPrematriculationModuleService.cs`
- `Views/Prematriculation/Create.cshtml`
- `Views/Prematriculation/ModularSubjects.cshtml`

Reportes:

- `BACKUP_EVIDENCE_MATRICULA_PREMATRICULA_NOCTURNA.md`
- `ACADEMIC_YEAR_CANONICAL_FIX_REPORT.md`
- `STUDENT_DATA_PROTECTION_REPORT.md`
- `MATRICULA_PREMATRICULA_NOCTURNA_COMPLETION_REPORT.md`

Scripts SQL:

- `Scripts/FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`
- `Scripts/ROLLBACK_FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`
- `Scripts/SEED_PREMATRICULATION_PERIOD_NOCTURNA.sql`
- `Scripts/ROLLBACK_SEED_PREMATRICULATION_PERIOD_NOCTURNA.sql`
- `Scripts/SEED_CURRICULUM_NOCTURNA_FROM_SUBJECT_ASSIGNMENTS.sql`
- `Scripts/ROLLBACK_SEED_CURRICULUM_NOCTURNA.sql`

## 3. Scripts SQL creados

Todos los scripts de cambio de datos son transaccionales.

Ejecutados en producción:

- `FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`
- `SEED_PREMATRICULATION_PERIOD_NOCTURNA.sql`
- `SEED_CURRICULUM_NOCTURNA_FROM_SUBJECT_ASSIGNMENTS.sql`

Rollback disponible:

- `ROLLBACK_FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`
- `ROLLBACK_SEED_PREMATRICULATION_PERIOD_NOCTURNA.sql`
- `ROLLBACK_SEED_CURRICULUM_NOCTURNA.sql`

Los rollbacks de período y currículo tienen guardas: se detienen si ya existen prematrículas, selecciones o referencias académicas que harían inseguro revertir.

## 4. Año académico canónico seleccionado

Canónico:

`f7ccb57f-fa3e-4d9f-973b-552030c9852d`

Criterio:

- Año `2026`.
- Escuela CELOSAM.
- Rango correcto `2026-01-01` a `2026-12-31`.
- Mayor uso real:
  - 298 `student_assignments` activos.
  - 148 `student_subject_assignments` activos.

Resultado:

- Antes: 13 años activos `2026`.
- Después: 1 año activo `2026`.
- No se borraron registros.
- No se reescribieron referencias históricas.

## 5. Período creado/habilitado

Se creó un período activo:

- Nombre: `Prematrícula Nocturna CELOSAM 2026 - 2T`
- Escuela: CELOSAM
- AcademicYearId: `f7ccb57f-fa3e-4d9f-973b-552030c9852d`
- TrimesterId: `7038b0cd-581b-470c-8753-618f34929a5a` (`2T`)
- `IsActive = true`
- `MaxCapacityPerGroup = 40`
- `MaxSubjectsAllowed = 12`
- `AutoAssignByShift = true`

## 6. Curriculum track creado

Se creó un track activo:

- Nombre: `Malla Modular Nocturna CELOSAM 2026`
- Escuela: CELOSAM
- Año: canónico 2026
- Activo: sí

## 7. Curriculum subjects creados

Se sembraron desde `subject_assignments` existentes de grupos con jornada `Noche`.

Resultado:

| Nivel | Curriculum subjects |
|---|---:|
| 7 | 33 |
| 8 | 32 |
| 9 | 30 |
| 10 | 71 |
| 11 | 53 |
| 12 | 66 |

Total: 285 materias curriculares.

No se crearon materias nuevas en `subjects`. No se duplicaron `subject_assignments`.

## 8. Protección de estudiantes

Evidencia:

`STUDENT_DATA_PROTECTION_REPORT.md`

Conteos protegidos:

| Elemento | Antes | Después |
|---|---:|---:|
| Estudiantes activos | 299 | 299 |
| Matrículas activas | 349 | 349 |
| Materias activas asignadas | 291 | 291 |
| Subjects | 83 | 83 |
| SubjectAssignments | 435 | 435 |

Hallazgos preexistentes no corregidos automáticamente:

- 1 estudiante activo sin matrícula activa.
- 1 matrícula activa sin `shift_id`.
- 1 grupo con texto `Noche` sin `shift_id`.

## 9. Cambios funcionales aplicados

### Prematrícula

- Los niveles disponibles ahora salen de `subject_assignments` con grupos de jornada `Noche`.
- Ya no se muestran todos los grados globales sin relación con la oferta nocturna.
- El endpoint AJAX de grados usa la misma fuente nocturna.
- Los grupos disponibles se filtran por jornada `Noche`.
- Se agrega compatibilidad grado-grupo:
  - grupo con `group.grade` igual al grado, o
  - grupo cuyo nombre inicia con `9-`, `10-`, etc.
- Al crear prematrícula, el estudiante va directo a seleccionar materias modulares.

### Materias modulares

- La pantalla muestra mensajes claros si no hay materias o si todas están bloqueadas.
- La disponibilidad modular considera `subject_assignments` con `status NULL` como abiertos, siempre que no sean `Closed`.
- Solo se consideran grupos de jornada `Noche`.
- Se evita usar grupos legacy incompatibles con el nivel.

## 10. Pruebas ejecutadas

Automáticas:

- `dotnet build "SchoolManager.csproj"`: correcto, 0 warnings, 0 errores.
- `dotnet test "SchoolManager.csproj" --no-build`: exit code 0.
- Linter sobre archivos modificados: sin errores.

Validaciones SQL funcionales:

- `8-1084-5433@celosam.com` ve niveles disponibles: `9`, `10`.
- `5-720-332@celosam.com` ve niveles disponibles: `10`, `11`.
- Hay grupos nocturnos con `subject_assignments` para niveles 9, 10 y 11.
- Todas las materias curriculares activas tienen grupo compatible por nivel según la validación aplicada.

Limitación:

No se hizo prueba UI autenticada real porque no se proporcionaron credenciales de estudiante ni se usó navegador autenticado. La validación se hizo por compilación, linter, test y consultas funcionales contra la base.

## 11. Riesgos pendientes

- Existen materias curriculares derivadas de datos legacy; conviene revisar duplicados semánticos de `subjects` con nombres parecidos.
- Hay grupos legacy nocturnos que siguen existiendo; el código ahora los filtra si no son compatibles con el nivel.
- `schedule_entries` sigue vacío; las materias se pueden seleccionar, pero horarios pueden mostrarse como no publicados.
- No todos los `subject_assignments` tienen docente.
- Algunas matrículas históricas siguen referenciando años académicos 2026 ahora inactivos. Se conservó historial.

## 12. Cómo revertir

Orden recomendado:

1. Si no hay uso de prematrícula/malla:
   - Ejecutar `ROLLBACK_SEED_CURRICULUM_NOCTURNA.sql`.
   - Ejecutar `ROLLBACK_SEED_PREMATRICULATION_PERIOD_NOCTURNA.sql`.
2. Si se necesita volver al estado de años duplicados:
   - Ejecutar `ROLLBACK_FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`.
3. Para restauración total:
   - Usar backup custom `.backup` o SQL plain creado en la Fase 1.

Los rollback de malla/período se detienen automáticamente si ya hay datos dependientes.

## 13. Checklist final

- [x] Backup aplicación creado.
- [x] Backup DB custom creado.
- [x] Backup DB SQL creado.
- [x] Backup verificado con `pg_restore -l`.
- [x] Snapshots previos creados.
- [x] Rama Git creada.
- [x] Año académico canónico seleccionado.
- [x] Duplicados 2026 desactivados sin borrar.
- [x] Período activo creado.
- [x] Curriculum track creado.
- [x] Curriculum subjects sembrados desde `subject_assignments`.
- [x] Estudiantes y matrículas protegidos.
- [x] Código adaptado a jornada Noche.
- [x] UI con mensajes claros.
- [x] Build correcto.
- [x] Test ejecutado.
- [x] Rollback documentado.

## Estado final

La prematrícula/matrícula nocturna CELOSAM quedó conectada al modelo modular moderno usando los datos existentes como fuente inicial, sin borrar estudiantes, matrículas, materias, grupos ni `subject_assignments`.
