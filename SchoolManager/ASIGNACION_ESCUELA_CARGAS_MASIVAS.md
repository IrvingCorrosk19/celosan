# Asignación de escuela en cargas masivas

## Regla de negocio

Cada colegio tiene uno o más usuarios con rol **admin** o **secretaria** (y en catálogo también **director**) que realizan cargas masivas desde Excel. Esos usuarios tienen `users.school_id` apuntando a su institución.

**Principio:** todo lo que se crea o vincula en una carga masiva debe quedar scoped al `SchoolId` del operador logueado. El Excel **no** incluye columna de escuela.

| Operador | ¿Puede cargar masivamente? |
|----------|----------------------------|
| Admin / Secretaria / Director (con `school_id`) | Sí, solo para su colegio |
| SuperAdmin (sin `school_id`) | No — debe usar un usuario del colegio |

## Fuente del `SchoolId`

```
Usuario logueado → currentUser.SchoolId → entidades nuevas y validaciones
```

Mecanismos en código:

1. **`AuditHelper.SetSchoolIdAsync`** — asigna `SchoolId` por reflexión al crear catálogos (grado, grupo, materia, especialidad, jornada, etc.).
2. **Asignación explícita** — estudiantes y profesores nuevos reciben `SchoolId = currentUser.SchoolId` en el controlador.
3. **`CreateAsignacionAsync(..., schoolId)`** — imparticiones (`subject_assignments`) con el id de la escuela del operador.
4. **Filtros EF multi-tenant** — lecturas normales ya limitadas por escuela; la carga masiva además valida emails globalmente.

## Flujos de carga masiva

### 1. Estudiantes — `/StudentAssignment/Upload`

**POST** `SaveAssignments` → `ProcessBulkSubjectEnrollmentsAsync` (modo principal `subjects`).

| Entidad | Cómo obtiene `SchoolId` |
|---------|-------------------------|
| Estudiante nuevo | `SchoolId = currentUser.SchoolId` |
| Estudiante existente | Debe tener el mismo `SchoolId`; si el email existe en otra escuela → error |
| Grado | `GetOrCreateAsync` scoped por escuela del operador |
| Grupo | `GetByNameAndGradeAsync(..., schoolId, shiftId)` — solo grupos de la escuela |
| Jornada | `GetOrCreateBySchoolAndNameAsync(schoolId, "Noche")` |
| Materia | `GetOrCreateAsync` scoped por escuela |
| Impartición (SA) | `EnsureSubjectAssignmentForBulkAsync(schoolId, ...)` |
| Inscripción SSA | `AuditHelper.SetSchoolIdAsync` en creación |

Modo legacy `gradeGroup`: misma regla de escuela para estudiantes, grupos y jornada.

### 2. Catálogo académico — `/AcademicCatalog/Upload`

**POST** `SaveCatalog`

| Entidad | Cómo obtiene `SchoolId` |
|---------|-------------------------|
| Especialidad, grado, grupo, materia | `GetOrCreateAsync` scoped por escuela |
| Área | Global (`IsGlobal = true`) — compartida entre escuelas |
| `subject_assignments` | `CreateAsignacionAsync(..., schoolId)` |

Rechaza la petición si el operador no tiene `school_id`.

### 3. Profesores + imparticiones — `/AcademicAssignment/Upload`

**POST** `SaveAssignmentsFromExcel`

| Entidad | Cómo obtiene `SchoolId` |
|---------|-------------------------|
| Catálogo (esp/area/materia/grado/grupo) | `GetOrCreateAsync` scoped por escuela |
| Impartición | `CreateAsignacionAsync(..., user.SchoolId)` |
| Profesor nuevo | `SchoolId = user.SchoolId` |
| Profesor existente | Debe pertenecer a la misma escuela |

Rechaza la petición si el operador no tiene `school_id`.

## Matriz resumen

| Módulo | Ruta Upload | Roles | Columna Escuela en Excel | SchoolId desde |
|--------|-------------|-------|--------------------------|----------------|
| Estudiantes | `/StudentAssignment/Upload` | admin, secretaria | No | Operador |
| Catálogo | `/AcademicCatalog/Upload` | admin, secretaria, director | No | Operador |
| Profesores | `/AcademicAssignment/Upload` | admin, secretaria | No | Operador |

## Brechas detectadas (y corrección aplicada)

| # | Problema | Riesgo | Corrección |
|---|----------|--------|------------|
| 1 | `GetOrCreateAsync` buscaba por nombre sin filtrar escuela | Reutilizar grado/grupo/materia de otra escuela | Lookup + create scoped por `currentUser.SchoolId` |
| 2 | `GetByEmailAsync` con filtro tenant ocultaba usuarios de otra escuela | Intentar crear duplicado de email → error BD opaco | `GetByEmailIgnoringTenantAsync` + validación explícita de escuela |
| 3 | `AcademicAssignment` no validaba `school_id` al inicio | NullReference / datos huérfanos | BadRequest si no hay escuela |
| 4 | Profesor existente de otra escuela se asignaba igual | Fuga cross-tenant | Error por fila si `SchoolId` distinto |
| 5 | `GetOrCreateAsync` de jornada sin escuela | Jornada compartida incorrectamente | Usar escuela del operador (como `GetOrCreateBySchoolAndNameAsync`) |
| 6 | UI sin indicador de escuela | Operador no sabe para qué colegio carga | Banner “Cargando para: [Nombre colegio]” |

## Restricciones de base de datos

- `users.email` y `users.document_id` son **únicos globales** (no por escuela).
- Un email ya registrado en colegio A no puede reutilizarse en colegio B vía carga masiva.
- SuperAdmin debe operar con cuenta de admin del colegio para cargas masivas.

## Helpers

- **`SchoolTenantHelper.TryGetBulkUploadSchoolContextAsync`** — resuelve `(SchoolId, SchoolName)` para UI y validaciones.
- **`SchoolTenantHelper.UserBelongsToSchool`** — comprueba que un usuario pertenece al colegio del operador.
- **`IUserService.GetByEmailIgnoringTenantAsync`** — búsqueda global de email en cargas masivas.

## Verificación manual sugerida

1. Iniciar sesión como admin de un colegio con `school_id` definido.
2. Abrir cada pantalla Upload y confirmar banner con nombre del colegio.
3. Cargar plantilla de estudiantes: nuevos usuarios con `school_id` del admin en BD.
4. Repetir fila con email existente del mismo colegio → actualiza/inscribe sin error.
5. SuperAdmin sin escuela → mensaje claro al guardar y banner de advertencia en UI.
6. (Multi-escuela futuro) Admin B intenta email de estudiante de colegio A → error explícito por fila.
