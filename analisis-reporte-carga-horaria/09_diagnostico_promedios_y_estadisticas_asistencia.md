# 09 — Diagnóstico: Promedios Finales y Estadísticas de Asistencia

**Tipo de documento:** diagnóstico. Sin correcciones.  
**Fecha:** 2026-09-05.  
**Pantalla:** `http://localhost:5172/TeacherGradebook/Index`  
**Base:** PostgreSQL local `schoolmanager_daqf` (solo lectura).  
**Docente de referencia:** `faridalain30@gmail.com` (`f222d4db-ea3b-43a5-8a15-300d2944c051`).  
**Oferta de ese docente:** TECNOLOGÍA DE LA INFORMACIÓN · 10 · A2.

No se modificó código, no se cambiaron consultas, no se insertaron datos.

---

## 1. Flujo de Promedios Finales

```
Usuario abre la pestaña Promedios Finales (#resumen-tab)
        ↓
Se disparan DOS handlers de click (código duplicado):
  A) loadPromediosFinales()           → Index.cshtml ~2309 / bind ~2674
  B) loadPromediosFinalesResumen()    → Index.cshtml ~4234 / bind ~4311
        ↓
Ambos leen #selGroupResumen
valor = subjectId|groupId|gradeLevelId|shiftId
        ↓
POST /TeacherGradebook/GetPromediosFinales
JSON: { teacherId, subjectId, groupId, gradeLevelId }
(no envían trimester ni shiftId)
        ↓
TeacherGradebookController.GetPromediosFinales
  - rechaza SubjectId/GradeLevelId vacíos
  - pisa TeacherId con GetTeacherId() (sesión)
  - ValidateTeacherGradebookScopeAsync (teacher_assignments)
        ↓
StudentActivityScoreService.GetPromediosFinalesAsync
        ↓
Consultas EF (ver sección 2)
        ↓
List<PromedioFinalDto>  (una fila por estudiante × trimestre)
        ↓
JSON camelCase: { success: true, data: [...] }
        ↓
Ambas funciones escriben el mismo tbody #resumenBody
```

Escritores adicionales del mismo `#resumenBody` (no son el endpoint):

| Función | Cuándo | Qué pinta |
|---|---|---|
| `updateResumenTable()` ~1744 | Al terminar `refreshTable()` del libro de **Registrar Notas** | Solo el trimestre de `#selTrimester` (cliente) |
| `loadPromediosFinales()` | Click pestaña / change `#selGroup` | Respuesta del endpoint |
| `loadPromediosFinalesResumen()` | Click pestaña / change `#selGroupResumen` | Respuesta del endpoint |

El trimestre por defecto de Registrar Notas es el primero del catálogo: **1T** (aunque esté inactivo). 1T no tiene actividades ni notas.

### Dónde dejan de verse los promedios reales

No se pierden en SQL ni en el DTO.

Se pierden en el **renderizado de `#resumenBody`**:

1. Al cargar Index, Registrar Notas pide 1T → `refreshTable()` → `updateResumenTable()` pinta 0.0 / «Sin calificar».
2. Al abrir Promedios Finales, dos AJAX intentan pintar el promedio anual.
3. Si `GetNotasCargadas` (1T) termina **después** de esos AJAX, `updateResumenTable()` **vuelve a pisar** la tabla con ceros de 1T.

Punto exacto de pérdida: **frontend, tbody compartido `#resumenBody`**, no el servicio.

---

## 2. Consultas involucradas (Promedios)

Servicio: `GetPromediosFinalesAsync` en `StudentActivityScoreService.cs`.

Orden real:

1. `subject_assignments` donde `subject_id + group_id + grade_level_id` coinciden con el combo.
2. `student_subject_assignments` activas de esas ofertas. Filtro opcional por `shift_id` (el JS **no lo envía**).
3. `users` de esos `student_id` (lista de filas).
4. `student_activity_scores` JOIN `activities` con:
   - SSA del alcance **o** `student_assignment_id` del alcance
   - `activities.subject_id / group_id / grade_level_id / teacher_id` = request
   - trimestre solo si `notes.Trimester` viene informado (en esta pestaña **no viene** → todos)
5. En memoria, para el cálculo, **exige** `student_subject_assignment_id` no nulo y perteneciente al estudiante.

Tablas: `subject_assignments`, `student_subject_assignments`, `users`, `student_activity_scores`, `activities`.  
`teacher_assignments` solo en la validación del controlador.  
`student_assignments` no se lista como fuente de alumnos; se usa el id de matrícula para cruzar notas viejas.

Nota de diseño (no es el fallo de este dataset): si una nota solo tiene `student_assignment_id` y SSA nulo, se **trae** y luego se **descarta** en el paso 5. Hoy las 21 notas tienen SSA.

---

## 3. Datos encontrados (Promedios) — SQL

Conteos globales de la base:

| Métrica | Valor |
|---|---:|
| `student_assignments` activos | 357 |
| `student_subject_assignments` activas | 346 |
| `activities` | 14 |
| `student_activity_scores` | 21 |
| scores con nota (no NULL) | 21 |
| scores con `score` NULL | 0 |

Grupo del docente de prueba (10 · A2 · TI):

| Métrica | Valor |
|---|---:|
| Estudiantes (SSA activas) | **3** |
| Estudiantes en `student_assignments` del grupo+grado | 3 |
| Actividades del mismo `teacher_id` | 14 |
| Actividades de otro docente en esa oferta | 0 |
| Scores del mismo docente | 21 |
| Scores sin SSA | 0 |

Notas por trimestre de actividad:

| Trimestre | Scores | Con valor | Sin SSA |
|---|---:|---:|---:|
| 1T | **0** | 0 | — |
| 2T | 15 | 15 | 0 |
| 3T | 6 | 6 | 0 |

Tipos con nota: `notas de apreciación`, `ejercicios diarios`, `examen final` (coinciden con el `ToLower()` del servicio). Hay otras actividades con mayúsculas distintas y **0 scores**; no afectan el cálculo.

Simulación del filtro del servicio (misma oferta, mismo teacher):

| Chequeo | Resultado |
|---|---:|
| Estudiantes en scope | 3 |
| Notas tras filtro de actividad+teacher | 21 |
| Notas usadas en el cálculo (con SSA) | 21 |
| Notas descartadas por SSA null | 0 |
| Trimestres en esas notas | 2 (2T, 3T) |

Promedio final replicando la fórmula del servicio (promedio de los promedios por tipo, solo tipos con valor):

| Estudiante | 2T | 3T | ¿Se puede promedio anual? |
|---|---:|---:|---|
| Prueba 1 | 4.92 | 4.50 | Sí (media 2T+3T ≈ 4.71) |
| Farid Alain. | 3.32 | 3.50 | Sí (≈ 3.41) |
| Prueba2 Alain. | 3.98 | 3.50 | Sí (≈ 3.74) |

**Sí hay notas suficientes para generar Promedios Finales.**  
1T no tiene notas; eso no impide el promedio anual con 2T y 3T. El front de la pestaña trata 1T vacío como `0.0`, no como celda vacía.

---

## 4. Causa del problema de Promedios

No es (1) falta de datos, (3) endpoint sin payload, (4) lista vacía del servicio, (6) NULL que anule las 21 notas, ni (7) relación rota en este grupo.

Sí hay:

- **Frontend (principal):** `#resumenBody` compartido con el libro de Notas; `updateResumenTable()` usa solo el trimestre activo de Notas (1T por defecto, sin notas) y puede dejar 0.0 / «Sin calificar» o pisar la respuesta del endpoint. Dos funciones AJAX duplicadas escriben la misma tabla.
- **Dato real, no un bug de cálculo:** no existen notas de 1T. El encabezado de la tabla siempre muestra las tres columnas.
- **Filtros del backend:** teacher + grupo + grado + materia + SSA. En *este* dataset no vacían el resultado. Seguirían siendo restrictivos si hubiera notas de otro docente o scores sin SSA.

El endpoint, si se llama con el combo de este docente y no lo pisa el JS del libro, **debe devolver 6 filas** (`success: true`).

JSON: `Program.cs` usa camelCase. El JS lee `studentId`, `notaFinal`, `trimester`. Eso es coherente. No hay error de ViewModel/DTO de casing.

---

## 5. Flujo de estadísticas de asistencia

```
Usuario abre Asistencias → subpestaña Estadísticas
        ↓
Las tarjetas se inicializan en "-" (HTML estático).
No hay carga automática.
        ↓
Usuario pulsa «Consultar estadísticas»
        ↓
consultarEstadisticas()  (Index.cshtml ~3215)
        ↓
Lee #selGroup  (combo de Registrar Notas, NO #filtroGradoAsistencia)
Lee #trimestreEstadisticas + data-inicio / data-fin
        ↓
const shiftId está declarado DENTRO del if (combo)
JSON.stringify usa shiftId FUERA de ese bloque
        ↓
ReferenceError: shiftId is not defined
        ↓
$.ajax NUNCA se ejecuta
        ↓
POST /Attendance/Estadisticas no ocurre
        ↓
Tarjetas siguen en "-"
Tabla #tablaEstadisticasEstudiantes vacía
No hay Swal (el error no entra al error de jQuery)
```

Flujo que **sí** está implementado en backend (hoy no se alcanza):

```
POST /Attendance/Estadisticas
        ↓
AttendanceController.Estadisticas
        ↓
AttendanceService.GetEstadisticasAsync
  attendance WHERE group_id + grade_id
             AND status IS NOT NULL
             AND date BETWEEN fechaInicio AND fechaFin
  shift_id solo si viene informado
        ↓
EstadisticasAsistenciaDto (totales + porEstudiante)
        ↓
JSON camelCase → tarjetas y tabla
```

Registro de asistencia (otra subpestaña) usa `#filtroGradoAsistencia`, `SaveAttendances` y `GetAttendancesByDate`. Eso es independiente de Estadísticas.

### Dónde se pierde la información

**En el JavaScript, antes del endpoint.**  
Línea ~3224 (`const shiftId` dentro del `if`) vs ~3255 (`shiftId` fuera de alcance).

---

## 6. Datos encontrados (Asistencia) — SQL

| Métrica | Valor |
|---|---|
| Registros en `attendance` | **6** |
| Presentes | 6 |
| Ausentes | 0 |
| Tardanzas (`late`) | 0 |
| Fugas / excusas / status NULL | 0 |
| Rango de fechas | 2026-09-02 … 2026-09-05 |
| Grupo/grado | solo **A2 / 10** (el del docente) |
| `shift_id` en los 6 registros | NULL |
| Estudiantes del grupo sin ningún registro | **0** (los 3 tienen asistencia) |

Por fecha:

| Fecha | Registros | Presentes |
|---|---:|---:|
| 2026-09-02 | 3 | 3 |
| 2026-09-05 | 3 | 3 |

Catálogo `trimester` vs esas fechas:

| Trimestre | Inicio | Fin | Activo | Registros en rango |
|---|---|---|---|---:|
| 1T | 2026-03-08 | 2026-06-11 | no | **0** |
| 2T | 2026-06-22 | 2026-09-11 | sí | **6** |
| 3T | 2026-09-21 | 2026-12-18 | sí | **0** |

Con 2T, las estadísticas **deberían** mostrar 100 % asistencia, 0 % ausencias, 0 % tardanzas y 3 filas de estudiante.

El combo de Estadísticas incluye 1T como primera opción (`GetAllAsync` no excluye inactivos). Si el request llegara con 1T, el backend devolvería ceros **por filtro de fechas**, no por falta de tabla.

---

## 7. Causa del problema de estadísticas

Causa principal: **error JavaScript de alcance** (`shiftId`). El endpoint no corre. Las tarjetas no se actualizan.

Causas secundarias (no se llegan a evaluar hoy, pero existen):

- Estadísticas lee `#selGroup` (Notas), no `#filtroGradoAsistencia` (Asistencias). En este docente coinciden (una sola oferta). En un docente con varias materias, consultaría el grupo de Notas, no el de Asistencias.
- No hay consulta al abrir la subpestaña; solo al botón.
- 1T (default) no tiene asistencias. Un request correcto con 1T se vería «vacío» (0 %), distinto de las tarjetas en "-".

No es falta de registros, no es NULL de `status`, no es otra tabla, no es el servicio.

### Comparación registro vs estadística

| Pregunta | Respuesta |
|---|---|
| ¿La asistencia se está guardando correctamente? | **SÍ** (6 filas, `present`, fechas 2 y 5 sep, mismo grupo/grado, los 3 alumnos) |
| ¿Las estadísticas leen de la misma tabla? | **SÍ** (`attendance`) |
| ¿La estadística filtra correctamente? | Backend: **SÍ** (grupo + grado + rango). Frontend: **NO** (el request no sale; selector y trimestre default incorrectos) |
| ¿Hay información suficiente para mostrar resultados? | **SÍ** si el trimestre es 2T. **NO** para 1T ni 3T |

---

## 8. ¿Errores antiguos o regresiones de Carga Horaria?

Diff de Carga Horaria respecto a HEAD (lo que tocó esa implementación):

| Archivo | Qué cambió | ¿Toca promedios o estadísticas? |
|---|---|---|
| `TeacherGradebookController.cs` | DI + GET `GetAcademicPrograms` / `GetCargaHoraria` | No. `GetPromediosFinales` y `GetAttendancesByDate` iguales |
| `StudentActivityScoreService.cs` | `SchoolId` al **crear** un score en un camino de guardado | No toca `GetPromediosFinalesAsync` |
| `Index.cshtml` | pestaña Carga Horaria, partial, `colspan`, CSS `image.png`, `data-act` al guardar notas | No toca `loadPromediosFinales*`, `updateResumenTable` ni `consultarEstadisticas` |
| `SchoolDbContext.cs` / `Program.cs` / `MenuService.cs` | tablas nuevas, DI, menú admin | No |

`consultarEstadisticas` con el `const shiftId` mal scoped **ya está** en `TeacherGradebookDuplicate/Index.cshtml` (mismo patrón). Es código anterior.

`updateResumenTable` escribiendo `#resumenBody` y los dos loaders de promedios también son anteriores.

### Respuesta explícita

¿PROMEDIOS FINALES FUE AFECTADO POR CARGA HORARIA?  
**NO**

¿ESTADÍSTICAS DE ASISTENCIA FUE AFECTADA POR CARGA HORARIA?  
**NO**

No hay evidencia de causalidad. Se detectaron después; el código responsable no forma parte del diff de Carga Horaria.

---

## 9. Archivos que habría que modificar (cuando se autorice el fix)

Solo UI de esta vista. No migración, no tablas, no permisos, no fórmula de notas.

| Problema | Archivo | Qué tocar |
|---|---|---|
| Promedios | `Views/TeacherGradebook/Index.cshtml` | Dejar un solo loader; que `updateResumenTable` no escriba `#resumenBody` (o usar otro tbody); no tratar 1T vacío como 0 si se quiere celda vacía |
| Estadísticas | `Views/TeacherGradebook/Index.cshtml` | Declarar `shiftId` en el scope de la función; leer `#filtroGradoAsistencia`; opcional: trimestre activo por defecto y/o consultar al abrir |

No hace falta cambiar `StudentActivityScoreService`, `AttendanceService`, `TeacherGradebookController.GetPromediosFinales` ni `AttendanceController.Estadisticas` para estos dos síntomas.

---

## 10. Riesgo de corrección

| Cambio | Riesgo | Por qué |
|---|---|---|
| Un solo AJAX de promedios + tbody propio | Bajo | No cambia SQL ni umbral 3.0 |
| Sacar `updateResumenTable` de `#resumenBody` | Bajo / medio | Hay que confirmar que nadie más dependa de esa copia en Notas |
| Hoist de `shiftId` | Muy bajo | Arregla un ReferenceError; el historial ya hace lo mismo bien |
| Usar `#filtroGradoAsistencia` | Bajo | Alinea Estadísticas con Tomar asistencia |
| Default 2T / trimestre activo | Bajo | Solo UX; el backend ya filtra por fechas |

Corregir el servicio de promedios (filtro SSA / teacher) **no es necesario** para este dataset y sí podría cambiar otras pantallas. No hacerlo ahora.

---

## PROMEDIOS FINALES:

**DIAGNÓSTICO**

Hay 3 estudiantes y 21 notas (2T y 3T) con las que el servicio **sí calcula** promedio final. 1T no tiene notas. El endpoint y el DTO no están vacíos por consulta. Los promedios dejan de verse en el **frontend**: `#resumenBody` lo pisan el libro de Notas (`updateResumenTable`, trimestre default 1T) y dos funciones AJAX duplicadas.

**CAUSA:** renderizado / carrera en `Index.cshtml`, no ausencia de notas ni `GetPromediosFinalesAsync` vacío en este grupo.

**TIPO:** VARIOS (ERROR DE FRONTEND principal; 1T sin notas es dato real)

**¿SE PUEDE CORREGIR SIN AFECTAR OTRAS FUNCIONES?** SÍ

---

## ESTADÍSTICAS DE ASISTENCIA:

**DIAGNÓSTICO**

Hay 6 asistencias guardadas (todas `present`, 10-A2, 2 y 5 sep 2026), suficientes para estadísticas en **2T**. El registro y el historial usan `attendance`. Las estadísticas leen la misma tabla, pero el click de «Consultar estadísticas» lanza `ReferenceError` por `shiftId` fuera de alcance y **no llama** a `/Attendance/Estadisticas`. Las tarjetas se quedan en "-".

**CAUSA:** error JS de alcance + selector de grupo de Notas + trimestre default 1T sin registros.

**TIPO:** ERROR DE FRONTEND

---

## RELACIÓN CON CARGA HORARIA:

**NO**

---

NO SE REALIZARON CAMBIOS.
