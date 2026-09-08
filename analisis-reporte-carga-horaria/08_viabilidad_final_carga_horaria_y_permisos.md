# 08 — Viabilidad final: carga horaria curricular y permisos

**Tipo de documento:** análisis.  
**Fecha:** 2026-09-05.  
**Alcance:** no se ejecutaron migraciones, SQL, seeds, cambios de roles, Policies, TeacherGradebook ni endpoints.

Fuentes: modelos y `SchoolDbContext`, `AuthService`, `Program.cs`, `User`, `UserRole`, `MenuService`, `AuditHelper`, `TeacherGradebookController` / `Index.cshtml`, `EnrollmentTypeConstants`, inventario en `05` y `06`.

---

## 1. Resumen ejecutivo

La malla de horas **sí puede** vivir al lado de Celosan sin tocar notas, matrícula, asistencia ni horarios.

`subject_assignments` es oferta **por grupo**. Por eso B1/B2 no deben colgarse de esa tabla. Las tres tablas propuestas (`curriculum_blocks`, `curriculum_load_subjects`, `curriculum_load_hours`) son el lugar correcto.

PREMEDIA y MEDIA se distinguen por el **número de grado** (`ParseGradeNumber` y conjuntos `{7,8,9}` / `{10,11,12}`). No hace falta un campo `education_level` en esta etapa. PRE-MEDIA 10°/11° se ignoran en la malla nueva; no se borran.

La sexta pestaña **Carga Horaria** en `/TeacherGradebook/Index` es viable si es aditiva (HTML/JS/GET nuevos) y no reescribe las acciones de notas ni asistencia.

El DTO de matriz es viable si `B1`/`B2` son **nullable**, para no confundir “sin configurar” con `0`.

Los nombres conceptuales `CurriculumLoad.View` / `Edit` / `Manage` **no existen** en Celosan. Hoy la autorización es **un rol por usuario** (`users.role` + cookie `ClaimTypes.Role` + `[Authorize(Roles = ...)]`). No hay tabla de permisos, ni policies por claim de capacidad, ni `IAuthorizationHandler`.

**¿Se puede dar un permiso extra a una secretaria concreta sin crear otro rol?**  
**NO**, con la arquitectura actual.

La alternativa de menor impacto: un flag en `users` (mismo estilo que `Disciplina` / `Orientacion`), comprobado en el **backend** de edición, no solo ocultando botones. No crear rol `secretaria_curricular` (obligaría a tocar `users_role_check`).

| Pregunta | Respuesta |
|---|---|
| 1. ¿Arquitectura curricular viable? | **Sí** |
| 2. ¿Tres tablas correctas? | **Sí** (`curriculum_load_*`, no reutilizar `curriculum_subjects`) |
| 3. ¿Mantener `subject_assignments` separado? | **Sí, obligatorio** |
| 4. ¿PREMEDIA/MEDIA por grados? | **Sí** |
| 5. ¿Tabla dinámica? | **Sí** (una vista, columnas según `Grades[]`) |
| 6. ¿Integrar a TeacherGradebook? | **Sí**, pestaña al final + GET |
| 7. ¿Política de permisos viable? | **Sí, con ajuste**: mapear a roles; no implementar RBAC completo |
| 8. ¿Edición opcional de secretaria? | **Requiere ajuste** (flag o tabla chica) |
| 9. ¿Qué cambiaría autorización? | Atributos en controladores **nuevos**; menú; opcionalmente un flag. No las policies de notas |
| 10. ¿Riesgos? | Ver sección 13 |
| 11. ¿Qué modificar? | Ver sección 12 |
| 12. ¿Qué no modificar? | Notas, scores, 1T/2T/3T, matrícula, asistencia, login, horarios, `subject_assignments` |

---

## 2. Viabilidad de base de datos

### 2.1 Conflicto de nombres

Ya existe `curriculum_subjects` (malla modular / prerrequisitos / créditos). No tiene `specialty_id` ni `area_id`. **No reutilizarla.**

Los nombres `curriculum_load_subjects` y `curriculum_load_hours` **no chocan** con el esquema actual.

### 2.2 Las tres tablas

| Tabla | Veredicto | Motivo |
|---|---|---|
| `curriculum_blocks` | Viable | Catálogo B1/B2. No es `trimester` ni `time_slots`. |
| `curriculum_load_subjects` | Viable | Una fila = escuela + programa + grado + área + materia. **Sin grupo.** Unique evita duplicar la celda curricular. |
| `curriculum_load_hours` | Viable | Una hora por (celda, bloque). Unique + `hours >= 0` cuando el valor existe. |

Relaciones previstas:

```
specialties + grade_levels + area + subjects
        ↓
curriculum_load_subjects     (sin group_id)
        ↓
curriculum_load_hours
        ↓
curriculum_blocks (B1, B2)
```

`subject_assignments` y `teacher_assignments` quedan **fuera** de esta cadena. Solo se leen, si acaso, para:

- sembrar la estructura (distinct curricular, sin grupo);
- filtrar programas del docente.

### 2.3 Horas no configuradas: A vs B

El plan anterior ponía `hours = 0` como placeholder. Eso **ya no sirve**: `0` debe significar “0 horas confirmadas”.

| | A — no crear fila hasta tener valor | B — `hours` nullable y siempre 2 filas |
|---|---|---|
| Sin configurar | No hay fila | Fila con `NULL` |
| 0 confirmado | Fila con `0` | Fila con `0` |
| Valor > 0 | Fila con N | Fila con N |
| Semilla inicial | Solo 205 celdas, **0 filas de horas** | 410 filas `NULL` |
| Riesgo | `SUM` de filas existentes no cuenta huecos (correcto) | Alguien puede tratar `NULL` como `0` en C# (`?? 0`) |
| Borrar configuración | Borrar la fila | Poner `NULL` |
| Tres estados por accidente | No (ausencia / 0 / N) | Sí, si también se permiten filas faltantes |

**Recomendación: opción A.**

- No insertar `curriculum_load_hours` en la carga inicial.  
- Crear la fila solo cuando un admin/superadmin (o secretaria con edición) guarda un valor, incluido `0`.  
- En el DTO: `decimal? B1`, `decimal? B2`. `null` = sin configurar.  
- En UI: “—” / “Sin configurar”, no un `0` automático.  
- Totales: sumar solo valores **no nulos**. Un área con todo sin configurar no debe mostrar `0` como si estuviera validado; conviene etiquetar “parcial / sin configurar”.

No implementar todavía.

### 2.4 Carga inicial de estructura (cuando se apruebe)

| Programa | Grados de malla | Celdas ≈ |
|---|---|---:|
| PRE-MEDIA | 7, 8, 9 | 50 |
| ELECTRICIDAD | 10, 11, 12 | 39 |
| AUTOTRÓNICA | 10, 11, 12 | 36 |
| INFORMÁTICA | 10, 11, 12 | 40 |
| TURISMO | 10, 11, 12 | 40 |
| **Total** | | **205** |

No insertar B1/B2 ni horas. PRE-MEDIA 10/11 fuera de este seed.

### 2.5 Tenant

`SchoolDbContext.Tenant.cs` ya filtra `Specialty` y `SubjectAssignment` por `school_id`. Las tablas nuevas con `school_id` deben tener **su propio** query filter. No alterar los filtros de `StudentActivityScore`, `Attendance`, etc.

### 2.6 Separar `subject_assignments`

**Es seguro y necesario.** 435 filas operativas vs ~230 combinaciones curriculares (205 si se excluyen PRE-MEDIA 10/11). Colgar horas del SA duplicaría por grupo.

---

## 3. Viabilidad PRE-MEDIA / MEDIA

Celosan **no** tiene `education_level`, `program_type` ni `academic_level`.

`Specialty` y `GradeLevel` solo tienen nombre + escuela + auditoría.

Ya hay dos piezas estructurales:

- `EnrollmentTypeConstants.ParseGradeNumber` (dígitos de `"7°"`, `"10"`, …).  
- `AprobadosReprobadosService`: Premedia = nombre que empieza por 7/8/9; Media = 10/11/12.

Regla recomendada (sin `Name.Contains("PRE")`):

```
Si el programa tiene algún grado ∈ {7,8,9} → PREMEDIA → columnas 7, 8, 9.
Si no, y tiene algún grado ∈ {10,11,12} → MEDIA → columnas 10, 11, 12.
```

PRE-MEDIA en datos reales: 7, 8, 9, **10, 11**. La regla lo clasifica PREMEDIA y **descarta** 10/11 solo para la malla nueva.

Internamente se sigue usando `specialty_id`. En UI: **Programa Académico**.

No agregar columna de nivel en esta migración.

---

## 4. Viabilidad de la matriz visual

Una sola vista reutilizable es viable.

```
programType + Grades[]  →  columnas
PREMEDIA → [7,8,9]
MEDIA    → [10,11,12]
```

No hardcodear “INFORMÁTICA” ni “ELECTRICIDAD” en la vista.

Áreas: las tres existentes (`HUMANISTICA`, `CIENTÍFICA`, `TECNOLÓGICA`), orden `DisplayOrder`. No crear `COMERCIAL`.

Vistas:

- **Programa completo:** matriz oficial (grados agrupados, B1/B2, TOTAL HORAS, subtotales, totales).  
- **Por grado:** `Área | Asignatura | B1 | B2 | Total`.

Celdas sin fila de horas: “sin configurar”, no `0` inventado.

PDF posterior: el mismo DTO puede alimentar una plantilla; no hace falta otra fuente de datos.

---

## 5. Viabilidad TeacherGradebook

### Estado actual

| Pieza | Hecho |
|---|---|
| URL | `/TeacherGradebook/Index` |
| Auth | `[Authorize(Roles = "teacher")]` |
| Vista | `Views/TeacherGradebook/Index.cshtml` (~4400 líneas, tabs + JS en el mismo archivo) |
| VM | `Teacher`, `Trimesters`, `Groups`, `Types`, `TeacherId`, `StudentAverages` |
| Menú | Portal Docente → solo `teacher` |
| `Index()` | Carga docente, trimestres, grupos, tipos. **No** carga notas ni asistencia hasta AJAX |

Tabs actuales: Notas, Promedios, Asistencias, Disciplina, Consejería.

### Qué se puede añadir sin romper

- Botón de tab al **final** (`#cargaHoraria-tab` → `#cargaHorariaTab`). `notas-tab` sigue `active`.  
- Partial o bloque nuevo; IDs que no choquen con `#notasBody`, `#resumenBody`, `#asistenciaTab`.  
- JS propio en `shown.bs.tab` / click del tab nuevo.  
- `GET GetAcademicPrograms` y `GET GetCargaHoraria` en el **mismo** controlador (rol `teacher`, solo lectura).  
- Inyectar `ICurriculumLoadService` en el constructor: no cambia la lógica de `Index()`.

`TeacherGradebookViewModel` **no necesita** la matriz en el GET inicial. El tab puede cargar por AJAX (igual que promedios/asistencia).

### Qué no se toca

`GuardarNotasTemp`, `GetNotasCargadas`, `CreateActivity`, `UpdateActivity`, `SaveScores`, `DeleteActivity`, `GetPromediosFinales`, `SaveAttendances`, `GetAttendancesByDate`, consejería, disciplina, `ValidateTeacherGradebookScopeAsync` (sigue siendo para notas/grupo/materia).

Carga horaria **no** usa grupo ni trimestre. No debe pasar por `ValidateTeacherGradebookScopeAsync`.

### Riesgo residual (bajo)

El `Index.cshtml` es grande. El riesgo es colisión de IDs o un selector jQuery demasiado genérico. Se mitiga con prefijo `ch-` / `cargaHoraria`.

secretaria, inspector, estudiante y clubparentsadmin **no** entran a este controlador hoy. No hay que abrir TeacherGradebook a secretaria.

---

## 6. Viabilidad del DTO

La forma propuesta es viable y encaja con el resto de DTOs del proyecto (clases C# en `SchoolManager/Dtos/`).

**Ajuste obligatorio:** `B1` y `B2` deben ser `decimal?`, no `decimal`.

| Valor en DTO | Significado |
|---|---|
| `null` | Sin configurar (no hay `curriculum_load_hours`) |
| `0` | 0 horas confirmadas |
| `> 0` | Horas configuradas |

`Total` de una fila: suma de no-nulos, o `null` si ambos bloques de todos los grados están sin configurar.

`ViewMode` / `SelectedGrade` pueden ir en el request (`fullProgram` | `byGrade` + grado) y repetirse en la respuesta. No hace falta persistirlos.

`ProgramName` = `specialties.name`.  
`HeaderTitle` = PREMEDIA → “PRE-MEDIA”; MEDIA → nombre del bachiller.  
`ProgramType` = `"PREMEDIA"` \| `"MEDIA"` calculado, no columna.

### Total de asignaturas

Las hojas oficiales **no están en el repositorio**. No se puede afirmar que el pie del PDF sea `COUNT(DISTINCT subject_id)`.

Lectura estructural habitual de esas matrices:

- una **fila** = una asignatura del programa;  
- B1 con horas y B2 en 0 **sí** cuenta como asignatura;  
- la misma materia en 7/8/9 es **una** fila, no tres;  
- el “total de asignaturas” del documento suele ser el **número de filas**, no la suma 7+8+9.

Regla **recomendada**, no implementada, pendiente de cotejo con las hojas:

1. `SubjectCountsByGrade`: materias con al menos un bloque **configurado y > 0** en ese grado.  
2. Conteo por bloque: materias con ese bloque configurado y `> 0`.  
3. `TotalSubjects` del programa: filas distintas (`subject_id`) que tengan **alguna** hora configurada `> 0` en cualquier grado oficial.  
4. Materia solo en B1: cuenta 1 en el grado y 1 en el programa.  
5. Materia con ambos bloques `null`: no cuenta.  
6. Materia con `0` y `0` confirmados: no cuenta como asignatura impartida (0 horas).

Hasta validar el PDF: **no fijar** `TotalSubjects = DISTINCT subject_id` de todas las celdas sembradas (incluiría filas sin horas).

---

## 7. Viabilidad de permisos

### Cómo autoriza Celosan hoy

| Mecanismo | ¿Existe? | Uso real |
|---|---|---|
| `users.role` (un string) | Sí | Fuente de verdad. Constraint `users_role_check` |
| Cookie + `ClaimTypes.Role` | Sí | Un claim de rol en login (`AuthService`) |
| `[Authorize(Roles = "...")]` | Sí | Patrón dominante en controladores |
| Policies en `Program.cs` | Sí, 6 | Solo alias de `RequireRole`. Casi no se usan en atributos |
| Claims de permiso | No | Login no emite `CurriculumLoad.*` |
| Tabla `permissions` / `user_permissions` | No | — |
| ASP.NET Identity roles / UserRoles | No | No hay `AspNetUserRoles` |
| `IAuthorizationHandler` | No | — |
| Flags en `users` | Sí | `Disciplina`, `Orientacion` (docente, menú); `Inclusivo` (estudiante). **No** entran al cookie ni a `[Authorize]` |

Roles del constraint: `superadmin`, `admin`, `director`, `teacher`, `parent`, `student`, `estudiante`, `acudiente`, `contable`, `contabilidad`, `secretaria`, `clubparentsadmin`, `qlservices`, `inspector`.

Un usuario tiene **un** rol. No hay rol secundario.

### Mapeo conceptual (sin implementarlo)

| Capacidad | Quién | Dónde |
|---|---|---|
| View | `teacher` | pestaña TeacherGradebook (GET) |
| View | `admin`, `superadmin`, `secretaria` | pantalla Administración |
| Edit / Manage | `admin`, `superadmin` | POST/PUT admin |
| Edit opcional | `secretaria` **solo si** hay ajuste (flag) | mismos POST/PUT |
| Nada | `inspector`, `estudiante`/`student`, `clubparentsadmin` | sin menú, Forbid en backend |

No hace falta crear policies `CurriculumLoad.View` ahora. Se pueden documentar como nombres lógicos y resolverlas con roles + un flag.

TeacherGradebook no debe tener POST de horas.

Administración → **Carga Horaria Curricular**: fuera del portal docente.

superadmin **no** ve el menú Administración hoy (`RequiredRoles = admin, secretaria`). Si debe administrar horas, hay que **añadir** un ítem visible para `superadmin` (o incluirlo en Administración). El controlador sí puede autorizar `admin,superadmin,secretaria` aunque el menú actual no lo muestre.

---

## 8. Análisis específico de secretaria

### Pregunta crítica

**¿ACTUALMENTE SE PUEDE DAR UN PERMISO ADICIONAL A UNA SECRETARIA ESPECÍFICA SIN CREAR OTRO ROL?**

**NO.**

Motivos:

1. Autorización = el string `users.role`.  
2. Login pone un solo `ClaimTypes.Role`.  
3. No hay permisos por usuario.  
4. `Disciplina` / `Orientacion` son flags de **docente** para mostrar menú extra; `OrientationReportController` sigue siendo `[Authorize(Roles = "teacher")]`. Un teacher sin el flag aún podría llamar la URL. Eso **no** es un sistema de permisos granulares.  
5. Crear `secretaria_curricular` **sí** daría edición a unas y no a otras, pero es **otro rol**, exige `users_role_check` + enum + menús, y saca a esa persona del rol `secretaria` (un usuario no puede ser los dos).

### Alternativa de menor impacto (no implementar ahora)

**Flag en `users`**, p. ej. `CanEditCurriculumLoad` (`bool`, default `false`), visible en Usuarios solo si el rol es `secretaria`.

- Secretaria A: flag `true` → POST/PUT admin permitidos.  
- Secretaria B: flag `false` → GET sí, POST/PUT 403.  
- No se crea rol.  
- No se toca `users_role_check`.  
- Mismo patrón de columna que `Disciplina`, pero **con chequeo en el controlador**, no solo en el layout.

Alternativa un poco más limpia (más archivos): tabla `user_capabilities (user_id, code)` y un claim en login. Más flexible, mayor alcance. No hace falta para un solo permiso.

**No recomendado ahora:** motor RBAC (`CurriculumLoad.View/Edit/Manage` como filas de permisos + asignación masiva). Excede el problema.

secretaria **no** debe editar roles, usuarios, notas, matrícula, periodos ni configuración global. El controlador de carga horaria no debe reutilizar `UserController` ni `StudentAssignmentController`.

Por defecto: todas las secretarias = solo consulta (View). El flag nace en `false`.

---

## 9. Seguridad backend

Ocultar botones **no basta**. Celosan ya tiene ese hueco en Disciplina/Orientación.

Diseño viable con lo existente:

| Endpoint | Método | Autorización |
|---|---|---|
| `/TeacherGradebook/GetAcademicPrograms` | GET | `[Authorize(Roles = "teacher")]` |
| `/TeacherGradebook/GetCargaHoraria` | GET | `[Authorize(Roles = "teacher")]` |
| `/CurriculumLoad/Index` (admin UI) | GET | `admin, superadmin, secretaria` |
| `/CurriculumLoad/GetReport` | GET | igual |
| `/CurriculumLoad/SaveHours` (etc.) | POST/PUT | `admin, superadmin` **o** `secretaria` + flag |

Un teacher que llame a `SaveHours` recibe 403: el controlador de escritura no incluye `teacher`.

Filtro extra: `school_id` del usuario (tenant + `ICurrentUserService`). El docente no edita; solo ve programas de su escuela (opcionalmente los de sus `teacher_assignments`).

No modificar las policies `Teacher`, `Admin`, `SuperAdmin` de `Program.cs` para notas o login.

---

## 10. Auditoría

**Sí hay infraestructura.** Reutilizarla.

`AuditHelper.SetAuditFieldsForCreateAsync` / `SetAuditFieldsForUpdateAsync` escribe por reflexión:

- `CreatedAt`, `CreatedBy`  
- `UpdatedAt`, `UpdatedBy`  

`ICurrentUserService.GetCurrentUserIdAsync()` es el autor.

`Specialty`, `GradeLevel` y muchas entidades ya usan ese patrón. Las tablas nuevas deben exponer las mismas propiedades.

`AuditLog` (acción, recurso, detalle, IP) existe como bitácora genérica. **No es obligatorio** para saber quién cambió una hora: bastan las columnas en `curriculum_load_hours`. Un `AuditLog` extra es opcional y no bloquea.

Borrado físico: no. `is_active` en `curriculum_load_subjects` + auditoría.

---

## 11. Archivos que sería necesario crear

*(Inventario; no creados en esta evaluación.)*

| Archivo | Propósito |
|---|---|
| `Models/CurriculumBlock.cs` | B1/B2 |
| `Models/CurriculumLoadSubject.cs` | Celda curricular |
| `Models/CurriculumLoadHours.cs` | Horas por bloque |
| `Dtos/CurriculumLoadReportDto.cs` (+ tipos anidados) | Matriz y totales |
| `Dtos/AcademicProgramOptionDto.cs` | Selector “Programa Académico” |
| `Services/Interfaces/ICurriculumLoadService.cs` | Contrato |
| `Services/Implementations/CurriculumLoadService.cs` | Tipo por grados, matriz, guardar horas |
| `Views/TeacherGradebook/_CargaHoraria.cshtml` | Partial del tab |
| `Controllers/CurriculumLoadController.cs` | Admin: consulta + edición |
| `Views/CurriculumLoad/Index.cshtml` | Administración |
| Migración `AddCurriculumLoadStructure` | Solo tras aprobación |

Si se aprueba el flag de secretaria: campo en `User` + checkbox en `Views/User/Index.cshtml` (solo rol secretaria). Eso **no** se hace en este análisis.

---

## 12. Archivos que sería necesario modificar

| Archivo | Cambio previsto | ¿Afecta lógica actual? |
|---|---|---|
| `SchoolDbContext.cs` | DbSets + mapeo de tablas **nuevas** | No, si no se tocan entidades viejas |
| `SchoolDbContext.Tenant.cs` | Query filter **nuevo** | No, si no se editan filtros existentes |
| `Program.cs` | `AddScoped` del servicio | No |
| `TeacherGradebookController.cs` | Constructor + 2 GET | No, si no se editan acciones existentes |
| `Views/TeacherGradebook/Index.cshtml` | 6.ª pestaña + include + JS aislado | No, si IDs nuevos |
| `MenuService.cs` | Ítem admin (+ superadmin) | Solo menú |
| `User.cs` + `UserController` + vista Usuarios | **Solo si** se aprueba el flag | No cambia login ni roles |

### Qué NO modificar

- `StudentActivityScoreService` / `GuardarNotasTemp` / `SaveBulkFromNotasAsync`  
- 1T, 2T, 3T / `Trimester`  
- `student_assignments`, `student_subject_assignments`  
- Asistencia, disciplina, consejería (servicios y JS actuales)  
- `AuthService` login (salvo que más adelante se elija emitir un claim del flag)  
- `users_role_check` / `UserRole` enum  
- Policies existentes  
- `subject_assignments` (ni horas ni B1/B2)  
- Horarios (`schedule_entries`, `time_slots`)  
- `curriculum_subjects` / `curriculum_tracks`  
- Áreas (no crear COMERCIAL)

---

## 13. Riesgos

| Riesgo | Nivel | Mitigación |
|---|---|---|
| Confundir `0` con “sin dato” | Medio si se ignora A | Opción A + `decimal?` |
| Sembrar PRE-MEDIA 10/11 | Medio | Filtro de grados oficiales |
| Duplicar horas por grupo | Alto si se usa SA | No FK a `subject_assignments` |
| Chocar con `curriculum_subjects` | Alto si se reutiliza | Nombres `curriculum_load_*` |
| JS del gradebook pisa IDs | Bajo | Prefijo propio |
| Secretaria POST sin flag | Medio | Chequeo backend, no solo UI |
| superadmin sin menú | Bajo | Ítem de menú explícito |
| Informática: Español/Ética duplicados | Bajo (datos) | No unificar en esta fase |
| `TotalSubjects` distinto al PDF | Bajo | No cerrar la fórmula hasta cotejar |
| Filtro tenant mal copiado | Medio | Filter solo en entidades nuevas |
| Rol nuevo para una secretaria | Alto (impacto) | No hacerlo; usar flag |

**Riesgo para la lógica actual (notas, matrícula, login, horarios): BAJO**, si las tablas y endpoints son aditivos.

---

## 14. Recomendaciones

1. Seguir con las tres tablas `curriculum_load_*`.  
2. Horas: **opción A** (sin fila = sin configurar).  
3. No sembrar horas. Sembrar solo 205 celdas cuando se apruebe.  
4. Tipo de programa por grados; UI “Programa Académico”.  
5. Una vista dinámica; pestaña al final del gradebook.  
6. Teacher: solo GET. Admin: otra pantalla.  
7. No implementar `CurriculumLoad.*` como policies reales en esta fase.  
8. Secretaria: View por rol; Edit solo con flag (tras aprobar ese ajuste).  
9. Reutilizar `AuditHelper`. Soft-delete con `is_active`.  
10. No crear áreas nuevas.  
11. Dejar `TotalSubjects` calculado y marcado como pendiente de PDF.  
12. No tocar `users_role_check`.

---

## 15. Plan definitivo propuesto

*(Sigue siendo un plan. No ejecutar.)*

1. **Migración** `AddCurriculumLoadStructure`: tres tablas, FKs, uniques, check `hours >= 0`, seed de bloques B1/B2. **Sin** filas de horas. Seed de 205 celdas: paso aparte, aprobable.  
2. **Servicio** que clasifica PREMEDIA/MEDIA, arma el DTO con `decimal?`, calcula totales solo sobre valores presentes.  
3. **TeacherGradebook:** pestaña Carga Horaria + `GetAcademicPrograms` + `GetCargaHoraria`.  
4. **Admin:** `CurriculumLoadController` + menú Administración (y superadmin).  
5. **Permisos:** `[Authorize(Roles)]` + comprobación de flag para POST de secretaria **si** se aprueba el flag.  
6. **No** cambiar notas, scores, matrícula, asistencia, login, horarios, roles del CHECK.

Orden sugerido tras aprobación: esquema → seed de celdas (sin horas) → consulta docente → pantalla admin de horas → flag de secretaria.

---

## Decisión final

**VIABILIDAD DE CARGA HORARIA:**  
VIABLE CON AJUSTES

Ajustes: no persistir `0` como “pendiente”; DTO con `B1`/`B2` nulos; no reutilizar `curriculum_subjects`; no fijar aún la fórmula oficial de total de asignaturas.

**VIABILIDAD DE PERMISOS:**  
VIABLE CON AJUSTES

Ajustes: mapear View/Edit/Manage a roles existentes; no construir un motor de permisos; teacher solo GET; edición fuera del gradebook.

**PERMISO OPCIONAL DE SECRETARIA:**  
REQUIERE AJUSTE

Hoy no hay permiso por usuario. Menor impacto: flag en `users` + validación backend. No crear otro rol.

**RIESGO PARA LA LÓGICA ACTUAL:**  
BAJO
