# Plan de implementación controlada – Educación nocturna

**Proyecto:** SchoolManager (EduplanerNoche)  
**Base:** `ANALISIS_COMPATIBILIDAD_EDUCACION_NOCTURNA.md`  
**Respaldo previo:** `C:\Backups\SchoolManager\` (2026-06-06)  
**Fecha del plan:** 2026-06-06  
**Estado:** **COMPLETO AL 100%** — ver `REPORTE_FINAL_IMPLEMENTACION_EDUCACION_NOCTURNA.md`

### Cierre 2026-06-06
- Flag activo escuela `6e42399f-6f17-4585-b92e-fa4fff02cb65`
- Migración `AddSubjectPromotionRecords` aplicada en producción
- Menús, vistas y checkbox consolidado completados
- `dotnet build` final: 0 errores

---

## 18. Registro de implementación (2026-06-06)

### Compilación
- `dotnet build` final: **0 errores, 0 warnings**

### Infra
- Flag `NocturnalAdvancedEnrollment` en `appsettings.json` + `NocturnalEnrollmentSettingsService`
- Registro DI en `Program.cs`

### Fase A
- `AddSubjectEnrollmentAsync` / `EnsureEnrollmentBaseAsync` en `StudentAssignmentService`
- Sync selectivo (omitido si flag avanzado)
- Eliminado fallback a matrícula más reciente
- UI arrastre en `StudentAssignment/Index.cshtml`

### Fase B
- `GetAvailableSubjectCatalogAsync` con todos los grados en modo avanzado
- `StudentActivityScoreService` ya resolvía SSA por grado+grupo (sin cambio estructural)

### Fase C
- `UpdateGroupAndGrade`: default additive si escuela avanzada
- `ActiveStudentAssignmentHelper`: `GetPrimaryEnrollment`, `GetAllActiveOrdered`

### Fase D
- Entidad `SubjectPromotionRecord` + migración `20260606110441_AddSubjectPromotionRecords`
- `SubjectPromotionService`, `SubjectPromotionController`
- Rollback: `Scripts/Rollback_AddSubjectPromotionRecords.sql`

### Fase E
- `StudentReportService`: `CarryOverGrades`, badges `IsCarryOver` / `LevelContext`
- `AprobadosReprobadosService`: parámetro `consolidatedByStudent`

### Fase F
- `StudentProfileService` + vista: todas las matrículas activas

### Fase G
- `ParentAcademicController` + vistas read-only para acudientes

### Riesgos en implementación
- Migración Fase D **no aplicada** a producción en esta sesión
- Pruebas manuales T-A1…T-G1 pendientes con flag activo

### Decisión requerida antes de codificar

Aprobar explícitamente:

- [x] Enfoque **sin rediseño** reutilizando SSA + `EnrollmentType`.
- [x] Feature flag por escuela antes de cambiar flujos default.
- [x] Orden A → B → C → D → E → F → G.
- [x] Migración única en Fase D (`subject_promotion_records`).
- [x] Respaldo `C:\Backups\SchoolManager\` vigente antes de Fase A.

---

## 17. Referencias

La arquitectura actual **no requiere rediseño completo**. Las tablas `student_assignments`, `student_subject_assignments`, `subject_assignments` y `teacher_assignments` ya soportan multi-matrícula e inscripción por materia. El trabajo consiste en:

1. **Formalizar semántica de negocio** (arrastre, nivel principal vs pendiente).
2. **Corregir flujos que destruyen o ignoran multi-contexto** (replace de matrícula, `FirstOrDefault`, sync masivo).
3. **Extender reportes y portales** sin romper colegios regulares (1 grado, 1 grupo, materias del grado).
4. **Agregar promoción por materia** (única área que probablemente requiera migración mínima).

**Principio rector:** todo cambio debe ser **opt-in por escuela** o **retrocompatible por defecto** para que colegios regulares sigan operando igual que hoy.

---

## 2. FASE 2 – Validación del modelo actual (reutilización)

### 2.1 ¿Se puede reutilizar sin rediseño?

| Tabla | Reutilizar | Justificación |
|-------|:----------:|---------------|
| `student_assignments` | **SÍ** | Matrícula por grado+grupo+jornada+año. UNIQUE parcial ya permite multi-matrícula. `EnrollmentType` incluye `Regular`, `Nocturno`, `Refuerzo`, `Libre`. |
| `student_subject_assignments` | **SÍ** | Inscripción por oferta (`subject_assignment_id`). UNIQUE por estudiante+oferta+año. Permite materias de distintos grados/grupos. |
| `subject_assignments` | **SÍ** | Oferta académica materia+grado+grupo. No cambiar; arrastre = inscribir en oferta del grado pendiente. |
| `teacher_assignments` | **SÍ** | Ya compatible multinivel. Sin cambios estructurales. |
| `student_activity_scores` | **SÍ** | Ya tiene `StudentAssignmentId` + `StudentSubjectAssignmentId`. |
| `attendance` | **SÍ (parcial)** | Clave por grupo+grado; suficiente si arrastre usa grupo del nivel pendiente. Mejora futura opcional (Fase E+). |
| `activities` | **SÍ** | Clase por grado+grupo+materia; coherente con arrastre en otro grupo. |

**Conclusión Fase 2:** **No se propone rediseño de esquema core.** Se extiende uso de campos existentes y se añade **como máximo 1 tabla nueva** (historial de promoción) en Fase D, justificada abajo.

### 2.2 Campos existentes a formalizar (sin migración inicial)

| Campo | Tabla | Uso propuesto |
|-------|-------|---------------|
| `EnrollmentType` | `student_assignments`, `student_subject_assignments` | `Regular` / `Nocturno` = nivel principal; `Refuerzo` = arrastre/pendiente; `Libre` = optativa especial |
| `StudentAssignmentId` | `student_subject_assignments` | FK obligatoria coherente grado+grupo de la oferta (eliminar fallback ambiguo) |
| `IsActive` / `EndDate` | ambas tablas de matrícula | Cierre de materia promovida o matrícula cerrada |
| `Status` | `student_subject_assignments` | `Active`, `Approved`, `Failed`, `Withdrawn` (valores en string; ampliar sin migración si columna ya es varchar) |

---

## 3. Cambios requeridos (visión global)

### 3.1 Cambios funcionales

| # | Cambio | Fases |
|---|--------|-------|
| C1 | Flujo explícito “Agregar materia pendiente (arrastre)” | A |
| C2 | Vincular SSA a matrícula correcta grado+grupo (sin fallback ciego) | A, B |
| C3 | Matrícula principal vs arrastre en UI y servicios | A, B |
| C4 | Sync selectivo de materias (no inscribir todo el grado al matricular si modo nocturno avanzado) | A |
| C5 | Multi-matrícula additive como flujo seguro por defecto | C |
| C6 | Promoción/cierre por materia al final de trimestre/año | D |
| C7 | Etiquetas arrastre/nivel en boletín y reportes | E, F |
| C8 | Perfil estudiante multi-contexto | F |
| C9 | Portal padres académico (solo lectura) | G |
| C10 | Feature flag por escuela `NocturnalAdvancedEnrollment` | A (infra) |

### 3.2 Cambios que NO se harán (explícito)

- Eliminar tablas o columnas existentes.
- Cambiar UNIQUE constraints actuales (romperían prod).
- Obligar multi-matrícula a colegios regulares.
- Reemplazar trimestres por semestres en esta iteración.
- Modelo Estudiante → Materia sin grado/grupo.

---

## 4. Tablas afectadas

| Tabla | Fase | Tipo de cambio |
|-------|------|----------------|
| `student_assignments` | A, C | Solo lógica/aplicación; sin ALTER inicial |
| `student_subject_assignments` | A, B, D | Lógica; posible ampliación `Status`; opcional columna `IsCarryOver` en Fase D |
| `subject_assignments` | A | Solo lectura/catálogo; validar ofertas existen por grado+grupo |
| `teacher_assignments` | — | Sin cambios |
| `student_activity_scores` | B | Validar resolución SSA al guardar notas |
| `attendance` | C, E | Sin ALTER en fases iniciales |
| `activities` | B | Sin ALTER |
| **`subject_promotion_records`** (propuesta) | **D** | **Nueva tabla** – historial promoción por materia (justificada) |
| `schools` o `security_settings` | A | Flag configuración escuela (reutilizar tabla existente si hay JSON settings) |

### 4.1 Migraciones propuestas (solo si Fase D avanza)

| Migración | Justificación | ¿Obligatoria? |
|-----------|---------------|---------------|
| `AddSubjectPromotionRecords` | Registrar aprobación/reprobación/promoción por materia con auditoría; no se puede inferir solo de SSA sin historial | **Sí para promoción parcial formal** |
| `AddIsCarryOverToStudentSubjectAssignment` | Desnormalización opcional; puede evitarse usando solo `EnrollmentType=Refuerzo` | **No** (Fase A–C) |

**Regla:** Fases A, B, C, F inicial → **cero migraciones** usando `EnrollmentType` y `Status` existentes.

---

## 5. Servicios afectados

| Servicio | Fases | Cambio principal |
|----------|-------|------------------|
| `StudentAssignmentService` | A, B, C | Sync selectivo; helper resolver matrícula base por grado+grupo |
| `StudentAssignmentController` (orquestación) | A, B, C | UI arrastre; quitar fallback; confirmaciones multi-matrícula |
| `StudentService` | B | `GetBySubjectGroupAndGradeAsync` – ya usa SSA; validar edge cases |
| `StudentActivityScoreService` | B | Resolver SSA obligatorio cuando hay múltiples matrículas |
| `StudentReportService` | E, F | Etiqueta `EnrollmentType`, agrupar por nivel; sección pendientes |
| `StudentProfileService` | F | Todas las matrículas activas, no `FirstOrDefault` |
| `AprobadosReprobadosService` | E | Modo consolidado por estudiante (opcional bajo flag) |
| `AttendanceService` | C, E | Documentar uso por grupo de arrastre; sin cambio obligatorio Fase A |
| `ScheduleService` | C | Ya multi-matrícula; validar con arrastre |
| `ClubParentsPaymentService` | G | Multi-matrícula display (Fase G académica separada) |
| **`SubjectPromotionService`** (nuevo) | D | Cierre y promoción por materia |
| `AcademicYearService` | D | Contexto año al promover |
| `ActiveStudentAssignmentHelper` | C, F | Distinguir “principal” vs “todas”; no ocultar arrastre |

---

## 6. Controladores afectados

| Controlador | Fases |
|-------------|-------|
| `StudentAssignmentController` | A, B, C |
| `TeacherGradebookController` | B (validación roster arrastre) |
| `StudentReportController` | E, F |
| `StudentProfileController` | F |
| `AprobadosReprobadosController` | E |
| `AttendanceController` | E (opcional) |
| `ClubParentsController` | G |
| **`SubjectPromotionController`** (nuevo, admin) | D |

**Sin cambios previstos:** `TeacherAssignmentController`, `AcademicAssignmentController`, `InstitutionalCredentialController`.

---

## 7. Vistas / ViewModels afectados

| Archivo | Fases |
|---------|-------|
| `Views/StudentAssignment/Index.cshtml` | A, B, C |
| `ViewModels/StudentAssignmentIndexViewModel.cs` | A, B, C |
| `Views/StudentReport/Index.cshtml` | E, F |
| `Views/StudentProfile/Index.cshtml` | F |
| `Views/AprobadosReprobados/Index.cshtml` | E |
| `Views/ClubParents/Students.cshtml` | G (si académico) |
| Nuevas vistas promoción | D |

---

## 8. Reportes afectados

| Reporte | Impacto | Fase |
|---------|---------|------|
| Boletín / Mis calificaciones | Agrupar por nivel; badge “Arrastre” | E, F |
| AprobadosReprobados | Riesgo doble conteo multi-grupo; modo consolidado | E |
| PDF AprobadosReprobados | Misma lógica | E |
| Carnet estudiantil | `PickForDisplay` vs matrícula principal documentada | C (documentación) |
| Dashboard director | Métricas por grupo siguen válidas; alerta pendientes | E (opcional) |

---

## 9. Riesgos y mitigaciones

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| Romper colegio regular (298 matrículas 1:1) | **Alto** | Feature flag por escuela; default = comportamiento actual |
| Fallback `AddSubjectEnrollment` vincula mal SSA | **Alto** | Fase A: exigir matrícula grado+grupo o crear mini-matrícula arrastre |
| `UpdateGroupAndGrade` inactiva todo | **Alto** | Fase C: default additive para escuelas nocturnas avanzadas; confirmación explícita en replace |
| Sync masivo inscribe todas las materias | **Medio** | Fase A: flag desactiva sync automático; inscripción manual/selectiva |
| AprobadosReprobados cuenta mal multi-grupo | **Medio** | Fase E: deduplicar por estudiante en vista consolidada |
| Promoción parcial sin historial | **Medio** | Fase D: tabla `subject_promotion_records` |
| Regresión gradebook | **Medio** | Pruebas con Pedro Pérez (escenarios del análisis) |
| Migración en producción | **Medio** | Solo Fase D; backup obligatorio antes (`C:\Backups\SchoolManager\`) |

---

## 10. Orden recomendado de implementación

```
Infra (flag escuela) ──► Fase A ──► Fase B ──► Fase C ──► Fase D ──► Fase E ──► Fase F ──► Fase G
                              │           │           │
                              └───────────┴───────────┴──► Compilar + pruebas manuales tras cada fase
```

| Orden | Fase | Entregable | Migración BD |
|-------|------|------------|--------------|
| 0 | Infra | Flag `NocturnalAdvancedEnrollment` en settings escuela | No |
| 1 | **A** | Arrastre (Historia 10°, Inglés 9° para 11°) | No |
| 2 | **B** | Multi-nivel coherente (notas + SSA + gradebook) | No |
| 3 | **C** | Multi-grupo (Grupo A / Grupo B) | No |
| 4 | **D** | Promoción parcial por materia | **Sí** (1 tabla nueva) |
| 5 | **E** | AprobadosReprobados + reportes institucionales | No |
| 6 | **F** | Portal estudiante completo | No |
| 7 | **G** | Portal padres académico | No |

**Criterio de paso entre fases:** compilación exitosa + escenario de prueba del análisis validado manualmente + colegio regular sin flag no afectado.

---

## 11. Detalle por fase (FASE 3 – plan de implementación)

### Fase A – Materias pendientes (arrastre)

**Objetivo:** Estudiante de 11° con Historia 10° e Inglés 9° como pendientes.

| Tarea | Archivo | Acción |
|-------|---------|--------|
| A1 | `StudentAssignmentController` | Acción `AddCarryOverSubjectEnrollment` o extender `AddSubjectEnrollment` con `enrollmentType=Refuerzo` |
| A2 | `StudentAssignmentController` | Eliminar fallback a “matrícula más reciente”; error claro si falta matrícula coherente |
| A3 | `StudentAssignmentService` | Método `EnsureEnrollmentBaseAsync(student, gradeId, groupId)` – crea matrícula arrastre additive si no existe |
| A4 | `StudentAssignmentService` | `SyncStudentSubjectAssignmentsAsync` – omitir sync total si escuela tiene flag avanzado |
| A5 | `Views/StudentAssignment/Index.cshtml` | Panel “Materias pendientes (arrastre)” con catálogo filtrado por grado |
| A6 | ViewModel | Lista SSA con badge `Refuerzo` / `Libre` |
| A7 | Docs | Manual operativo para secretaría |

**Datos de prueba (Pedro Pérez):**

- Matrícula principal: 11° + grupo nocturno.
- SSA arrastre: `SubjectAssignment` Historia 10° + grupo X; Inglés 9° + grupo Y.
- `EnrollmentType = Refuerzo` en SSA.

**Compatibilidad regular:** sin flag, sync y flujos actuales intactos.

---

### Fase B – Materias de múltiples niveles

**Objetivo:** Matemática 11°, Historia 10°, Inglés 9° simultáneas con notas correctas.

| Tarea | Archivo | Acción |
|-------|---------|--------|
| B1 | `StudentActivityScoreService` | Al guardar nota, resolver SSA por `(student, activity.subject, activity.group, activity.grade)` |
| B2 | `TeacherGradebookController` | Roster siempre vía `GetBySubjectGroupAndGradeAsync` cuando hay subjectId |
| B3 | `StudentAssignmentController` | `GetAvailableSubjectCatalog` – mostrar todos los grados, marcar nivel principal vs arrastre |
| B4 | Validación | Estudiante aparece en gradebook de cada clase donde tiene SSA activa |

**Sin migración.**

---

### Fase C – Múltiples grupos

**Objetivo:** Matemática en Grupo A, Inglés en Grupo B.

| Tarea | Archivo | Acción |
|-------|---------|--------|
| C1 | `StudentAssignmentController` | Escuelas avanzadas: `UpdateGroupAndGrade` default additive |
| C2 | `StudentAssignment/Index.cshtml` | Listar todas las matrículas activas; acción “Agregar matrícula” visible |
| C3 | `ActiveStudentAssignmentHelper` | `GetPrimaryEnrollment` vs `GetAllActive` – carnet usa primary; reportes usan all |
| C4 | `StudentIdCardController` | Documentar que carnet usa matrícula principal (sin cambio funcional obligatorio) |
| C5 | `AttendanceService` | Validar asistencia independiente por grupo (ya soportado) |

**Sin migración.**

---

### Fase D – Promoción parcial por materia

**Objetivo:** Aprueba 9 de 11 materias; 2 pendientes; puede avanzar de nivel manteniendo arrastre.

| Tarea | Acción |
|-------|--------|
| D1 | Nueva entidad `SubjectPromotionRecord` (student_id, subject_id, grade_level_id, academic_year_id, trimester, outcome, final_score, promoted_at) |
| D2 | Migración `AddSubjectPromotionRecords` **justificada** – auditoría legal académica |
| D3 | `SubjectPromotionService.PromoteSubjectAsync` / `CloseYearAsync` |
| D4 | UI admin: pantalla cierre por materia / estudiante |
| D5 | Al promover materia: SSA `IsActive=false`, `Status=Approved`; matrícula principal puede subir grado en flujo separado |
| D6 | Materias no aprobadas: mantener SSA `Refuerzo` activo o crear nueva en año siguiente |

**Única migración estructural prevista del plan.**

---

### Fase E – Reportes y AprobadosReprobados

| Tarea | Acción |
|-------|--------|
| E1 | `StudentReportService` – sección “Materias de arrastre” filtrada por `EnrollmentType=Refuerzo` |
| E2 | `AprobadosReprobadosService` – parámetro `consolidatedByStudent`; evitar doble conteo |
| E3 | Etiquetas PDF: grado de la oferta vs grado principal |
| E4 | Dashboard director – contador estudiantes con pendientes (opcional) |

---

### Fase F – Portal del estudiante

| Tarea | Archivo | Acción |
|-------|---------|--------|
| F1 | `StudentProfileService` | Cargar todas las matrículas activas (como `StudentReportService`) |
| F2 | `Views/StudentProfile/Index.cshtml` | Mostrar grado principal + otras matrículas / arrastre |
| F3 | `StudentReportService` | Badge nivel en cada fila de calificación |
| F4 | `StudentScheduleController` | Ya OK; verificar con multi-grupo arrastre |

---

### Fase G – Portal de padres

| Tarea | Acción |
|-------|--------|
| G1 | Nuevo `ParentAcademicController` o extender rol `acudiente` en `StudentReportController` |
| G2 | Reutilizar `StudentReportService` con `studentId` del hijo vinculado |
| G3 | Vista read-only calificaciones + pendientes |
| G4 | `ClubParents` permanece para pagos/carnet (no mezclar) |

---

## 12. Estrategia de rollback

### 12.1 Antes de cada fase

1. Verificar respaldo en `C:\Backups\SchoolManager\` (código + BD).
2. Crear rama Git `feature/nocturnal-phase-X` (cuando se implemente).
3. Tag opcional `pre-nocturnal-phase-X` en commit estable.

### 12.2 Rollback de código

```powershell
git checkout main
git revert <commit-range>   # preferir revert sobre reset en main compartido
dotnet build
```

Restaurar ZIP si es necesario:

```powershell
Expand-Archive C:\Backups\SchoolManager\Codigo\SchoolManager_Source_Backup_20260606_0551.zip -DestinationPath C:\Restaurar
```

### 12.3 Rollback de base de datos

**Solo si Fase D migró producción:**

- Restaurar en instancia de staging desde `schoolmanager_prod.backup` (validar primero).
- **Nunca** restaurar sobre Render sin ventana de mantenimiento.
- Migración down: eliminar solo tabla `subject_promotion_records` si no hay datos críticos.

### 12.4 Rollback funcional sin BD

- Desactivar flag `NocturnalAdvancedEnrollment` en escuela → comportamiento regular inmediato (Fases A–C).

---

## 13. Compatibilidad con educación regular

| Comportamiento actual | Preservación |
|----------------------|--------------|
| 1 matrícula por estudiante | Default sin flag |
| Sync todas las materias al matricular | Default sin flag |
| `UpdateGroupAndGrade` replace | Default sin flag |
| AprobadosReprobados por grupo | Sin cambio si no se activa modo consolidado |
| Gradebook por clase | Sin cambio |
| Trimestres 3T | Sin cambio |

**Colegios regulares:** no activar flag → **cero impacto esperado**.

---

## 14. Plan de pruebas (por fase – a ejecutar al implementar)

| ID | Escenario | Resultado esperado |
|----|-----------|-------------------|
| T-A1 | Pedro 11° + arrastre Historia 10° | SSA Refuerzo vinculada a matrícula 10°+grupo |
| T-A2 | Colegio regular sin flag | Sync completo igual que hoy |
| T-B1 | Nota en gradebook Historia 10° | Score con SSA correcta |
| T-C1 | Dos matrículas activas | Horario y boletín muestran ambas |
| T-D1 | Promover 9 materias, 2 pendientes | Records + SSA cerradas/aprobadas |
| T-E1 | AprobadosReprobados consolidado | Sin doble conteo |
| T-F1 | Perfil estudiante | Muestra todas las matrículas |
| T-G1 | Acudiente ve boletín hijo | Solo lectura |

**Compilación:** `dotnet build` tras cada fase (0 errores).

---

## 15. Entregables de implementación (cuando se apruebe)

| # | Entregable | Estado |
|---|------------|--------|
| 1 | `IMPLEMENTACION_EDUCACION_NOCTURNA.md` | ✅ Este documento |
| 2 | Lista de cambios realizados | Pendiente implementación |
| 3 | Archivos modificados | Pendiente |
| 4 | Migraciones creadas | Pendiente (solo Fase D) |
| 5 | Riesgos encontrados | Documentados en §9 |
| 6 | Resultado compilación | Pendiente |
| 7 | Resultado pruebas | Pendiente |

---

## 16. Decisión requerida antes de codificar

Aprobar explícitamente:

- [ ] Enfoque **sin rediseño** reutilizando SSA + `EnrollmentType`.
- [ ] Feature flag por escuela antes de cambiar flujos default.
- [ ] Orden A → B → C → D → E → F → G.
- [ ] Migración única en Fase D (`subject_promotion_records`).
- [ ] Respaldo `C:\Backups\SchoolManager\` vigente antes de Fase A.

---

## 17. Referencias

- `ANALISIS_COMPATIBILIDAD_EDUCACION_NOCTURNA.md`
- `C:\Backups\SchoolManager\Reportes\README_RESTORE.md`
- `C:\Backups\SchoolManager\Reportes\DATABASE_INVENTORY.md`
- `Docs/ANALISIS_ESTUDIANTES_NOCTURNOS.md`
- `Docs/prematriculation_apply_academic_year_changes_analysis.md`

---

**NO SE HA MODIFICADO CÓDIGO, BASE DE DATOS NI MIGRACIONES.**  
**Siguiente paso:** revisión y aprobación de este plan antes de iniciar Fase A.
