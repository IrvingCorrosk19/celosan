# Análisis de preparación para producción — Eduplaner (multi-tenant)

**Alcance:** ASP.NET Core MVC (.NET 8), PostgreSQL, seguridad web, multi-tenant por escuela (`SchoolId`), arquitectura y performance.  
**Metodología:** revisión estática del código en `SchoolManager/`, introspección de esquema en PostgreSQL vía `psql` (cliente `C:\Program Files\PostgreSQL\18\bin`), y lectura de patrones de servicios/controladores.  
**Regla aplicada:** no se modificó código de aplicación; este documento es solo auditoría.

---

## Nota sobre la base de datos (Fase 1)

- **Solicitud:** conexión a `localhost`.  
- **Hecho:** `psql -h localhost` falló (cliente exige SSL; instancia local típica no aceptó el handshake en el entorno probado).  
- **`appsettings.json` / `appsettings.Development.json`:** ambos apuntan al **mismo host gestionado (Render)**, no a `localhost`.  
- **Introspección ejecutada:** contra la instancia definida en `ConnectionStrings:DefaultConnection` (PostgreSQL remoto), **52 tablas** `public` listadas, columnas clave e índices muestreados en tablas críticas (`student_assignments`, `subject_assignments`, `users`, etc.).

Para un informe 100 % alineado con “solo localhost”, hace falta una cadena local válida y `psql` con `sslmode=disable` o ajuste de `pg_hba.conf` / túnel.

---

## 1. Resumen ejecutivo

| Pregunta | Respuesta |
|----------|-----------|
| **¿Listo para producción enterprise?** | **NO** (con matices operativos: el sistema puede estar **en producción de facto**, pero **no** cumple estándar SaaS enterprise sin remediación). |
| **Nivel de riesgo global** | **ALTO** en **seguridad y aislamiento lógico**; **MEDIO** en **consistencia multi-tenant en datos** y **performance**; **MEDIO-BAJO** en **integridad relacional base** (EF + migraciones). |

**En una frase:** la aplicación mezcla piezas maduras (autenticación cookie, roles en muchos controladores, índices útiles en matrícula) con **fallos estructurales típicos de deuda técnica** (endpoints sin `[Authorize]`, consultas sin filtro de tenant, secretos en repositorio) que en auditoría enterprise se clasifican como **bloqueantes o condicionados**.

---

## 2. Hallazgos críticos

### 2.1 Controladores sin `[Authorize]` (superficie de ataque anónima)

ASP.NET Core **no** exige autenticación salvo `[Authorize]` o política global. En `Program.cs` existe `app.UseAuthentication()` / `app.UseAuthorization()` **sin** `FallbackPolicy` que exija usuario autenticado.

**Controladores observados sin atributo `[Authorize]` a nivel de clase** (acceso anónimo a acciones GET/POST salvo acciones individuales protegidas):

- `AttendanceController` — listado/creación/edición de asistencia.
- `GroupController` — incluye `ListJson` y `Create` JSON **públicos** si no hay otro filtro global.
- `SchoolController` — CRUD de escuelas desde MVC clásico.
- `ActivityController` — catálogo de actividades.
- `DisciplineReportController` — reportes disciplinarios.
- `SecuritySettingController` — configuración de seguridad por escuela.
- `SubjectAssignmentController` — lógica sensible (contexto + asignaciones).
- `AuditLogController` — listado de auditoría.
- `GradeLevelController` — niveles académicos (ruta `GradeLevel`).
- `AreaController` / `SubjectController` — catálogo académico.

**Impacto:** cualquier cliente HTTP puede invocar rutas MVC típicas (`/Attendance/Index`, `/Group/ListJson`, etc.) **sin cookie de sesión**, sujeto solo a validaciones internas (muchas rutas no validan tenant ni usuario).

**Veredicto auditoría:** **bloqueante para producción multi-tenant** hasta aplicar **autorización por defecto denegada** (`FallbackPolicy = RequireAuthenticatedUser`) y revisar cada excepción explícita con `[AllowAnonymous]`.

### 2.2 Fuga lógica de tenant en servicios clave

Ejemplo verificado en código:

- `AttendanceService.GetAllAsync()` ejecuta `_context.Attendances.ToListAsync()` **sin** `Where(a => a.SchoolId == …)` ni join a usuario/grupo acotado por escuela.

Con un usuario autenticado **débil** o combinado con **2.1**, el riesgo es exposición o manipulación de datos **entre escuelas** (violación del modelo mental multi-tenant).

**Veredicto:** **bloqueante** si el producto se vende como SaaS con varias instituciones en una sola base.

### 2.3 Secretos y cadena de conexión en repositorio

`appsettings.json` (y plantillas) contienen **credenciales reales o placeholders** de servicios (PostgreSQL, Cloudinary, claves de firma QR, etc.). En GitHub público o CI sin secretos, esto es **incidente de seguridad** y **cumplimiento** (rotación, responsabilidad, exposición de PII vía DB).

**Veredicto:** **bloqueante** para estándar enterprise; mitigación: **solo** variables de entorno / secret manager, rotación, `.gitignore` de overrides locales, escaneo de secretos en CI.

### 2.4 Ausencia de Row-Level Security (RLS) en PostgreSQL

El aislamiento multi-tenant es **solo aplicación** (LINQ/servicios). No hay **RLS** por `school_id` a nivel motor.

**Impacto:** cualquier bug de filtro, SQL ad-hoc, script operativo o conexión BI con mal `WHERE` puede cruzar tenants.

**Veredicto:** **importante / crítico** según contrato de datos (HIPAA/GDPR contractual → casi bloqueante).

---

## 3. Hallazgos importantes

### 3.1 Modelo de datos: `SchoolId` inconsistente entre tablas

Introspección: **35** tablas con columna `school_id` / `"SchoolId"` (nombres mixtos según migraciones EF). Varias tablas de negocio **no** tienen columna directa de escuela, p. ej.:

- `student_assignments` — el tenant se infiere por `student_id` → `users.school_id` y `groups.school_id`.
- `activity_attachments`, `schedule_entries`, `teacher_assignments`, tablas de unión `user_*`, etc.

**Riesgo:** consultas y mantenimiento requieren **disciplina** en cada join; un `Include` mal armado puede arrastrar entidades globales.

### 3.2 Superadmin sin `SchoolId` vs UX multi-tenant

Patrones en servicios (`GetAllStudentsAsync`, etc.): si `currentUser.SchoolId == null` se devuelve lista vacía o comportamiento degradado.

**Riesgo operativo:** soporte y superadmin ven pantallas “vacías” o usan atajos peligrosos; no es bug de seguridad directo, pero **rompe operación** y empuja a “fixes” ad hoc.

### 3.3 Endpoints `[AllowAnonymous]` en carnet / QR / archivos

`StudentIdCardController` y `FileController` documentan `AllowAnonymous` para flujos móviles / plantillas.

**Riesgo:** depende **totalmente** de validación de token, rate limiting (`Program.cs` menciona rate limiter en API) y de que no exista enumeración. Requiere **revisión de amenazas** (replay, brute force de tokens, IDOR por IDs predecibles).

### 3.4 Roles en string y casing mixto

`[Authorize(Roles = "admin,secretaria")]` vs `"Admin,Teacher"` en otros controladores. Funciona si los claims están normalizados, pero aumenta **riesgo de error humano** y bypass accidental por claim mal emitido.

### 3.5 “Clean Architecture” declarada vs realidad

No hay separación formal **Domain / Application / Infrastructure** como paquetes o capas estrictas: predominan **Controllers + Services + `SchoolDbContext`**. Es un **monolito MVC clásico**, no incorrecto, pero **no** coincide con el nivel “Harvard SaaS” pedido en el brief sin documentar límites y tests de arquitectura.

---

## 4. Hallazgos menores

- Mezcla **snake_case** en columnas SQL y propiedades Pascal en algunas tablas (`"SchoolId"` en `subject_assignments`), coherente con EF pero **fricción** para DBAs.
- `Console.WriteLine` en controladores/servicios (ruido, fuga de PII a logs en producción si no se filtra).
- Comentarios y scripts `--apply-*` en `Program.cs` útiles para operaciones, pero **superficie** de mantenimiento y posible ejecución errónea en entorno incorrecto si no hay guardarraíles.

---

## 5. Problemas de multi-tenant

| Tema | Evaluación |
|------|--------------|
| **¿Todas las tablas críticas tienen `SchoolId`?** | **No**; muchas dependen de **cadenas FK** (usuario, grupo, año académico). Es válido si las consultas **siempre** filtran; no es válido si no filtran (véase asistencia). |
| **¿Tablas sin aislamiento explícito?** | Sí: p. ej. `grade_levels`, `subjects` pueden ser globales o por escuela según datos; la lógica debe estar en servicios — **alto acoplamiento**. |
| **¿Fuga entre escuelas?** | **Sí, plausible** por: (1) controladores anónimos, (2) servicios tipo `GetAllAsync()` sin `SchoolId`, (3) ausencia de RLS. |
| **Relaciones cruzadas sin filtro** | Cualquier acción que reciba `Guid` por ruta/cuerpo sin verificar `school_id` del dueño vs `CurrentUser` → **IDOR** clásico (riesgo a auditar endpoint por endpoint). |

---

## 6. Problemas de performance

- **Includes profundos:** servicios como `ScheduleService`, `TeacherAssignmentService`, `PrematriculationService`, `PaymentService` concentran muchos `.Include` / `.ThenInclude` — riesgo de **consultas cartesianas** y **memoria** en listados grandes.
- **`AttendanceService.GetHistorialAsync`:** múltiples `Include` sobre asistencias por rango de fechas — sensible a volumen sin paginación estricta en todas las rutas.
- **Índices:** en `student_assignments` existen índices compuestos y parciales razonables (`uq_student_assignments_active_enrollment`, `ix_student_assignments_active_student_created_at`). No se ejecutó un barrido completo “FK sin índice” en todas las 52 tablas en este informe; se recomienda **script de auditoría** `pg_stat_user_indexes` + revisión de FKs frecuentes en logs de producción.

---

## 7. Problemas de seguridad (consolidado)

1. **Autorización por omisión permisiva** (sin política global) + controladores sin `[Authorize]`.  
2. **Filtrado de tenant ausente** en servicios puntuales (`AttendanceService.GetAllAsync` verificado).  
3. **Secretos en configuración versionada**.  
4. **Superficie `[AllowAnonymous]`** en flujos de carnet/archivo — debe ir acompañada de **threat model** documentado.  
5. **Cookies 24h + sliding** — revisar alineación con política de sesión y revocación (logout, robo de cookie).

---

## 8. Recomendaciones prioritarias (orden sugerido)

1. **Política de autorización por defecto:** `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();` y marcar solo `AuthController` + endpoints públicos con `[AllowAnonymous]`.  
2. **Barrido de controladores:** añadir `[Authorize]` mínimo por rol o `[Authorize(Roles=…)]` en **todos** los controladores restantes; eliminar confianza en “nadie conocerá la URL”.  
3. **Estándar de consultas:** regla de equipo: **ningún** `DbSet` de datos de negocio sin `Where` explícito por `SchoolId` o por subconsulta acotada al tenant del usuario actual (incluyendo superadmin con reglas explícitas).  
4. **Secretos:** migrar a variables de entorno / Azure Key Vault / Render secrets; rotar credenciales expuestas en historial Git.  
5. **RLS (opcional pero fuerte):** políticas por `school_id` en tablas con columna directa; vistas o funciones para tablas indirectas.  
6. **Pruebas automatizadas de tenant:** tests de integración que fallen si una consulta devuelve filas de `school_id` distinto al claim.  
7. **Performance:** paginación obligatoria en listados administrativos; revisión de `Include` en servicios más pesados; `AsNoTracking` en lecturas.

---

## 9. Veredicto final

| Dimensión | Estado |
|-----------|--------|
| **Funcional / MVP producción** | Puede operar si el riesgo es aceptado contractualmente. |
| **Producción SaaS multi-tenant “enterprise”** | **NO listo** sin cerrar brechas de **autorización por defecto**, **filtrado sistemático por tenant** y **gestión de secretos**. |
| **Base de datos PostgreSQL** | Esquema **coherente con EF**, ~52 tablas, FKs e índices en zonas críticas revisadas; **no sustituye** controles en aplicación ni RLS. |

**Conclusión del auditor:** tratar el estado actual como **PARCIAL / alto riesgo**: apto solo con **plan de remediación priorizado**, **monitoreo**, y **acuerdo explícito** con el cliente sobre límites de responsabilidad de datos entre escuelas.

---

## Anexo A — Tablas `public` (instancia analizada)

Listado obtenido con `information_schema.tables` (52 relaciones base; incluye metadatos EF):

`EmailConfigurations`, `__EFMigrationsHistory`, `academic_years`, `activities`, `activity_attachments`, `activity_types`, `area`, `attendance`, `audit_logs`, `counselor_assignments`, `discipline_reports`, `email_api_configurations`, `email_configurations`, `email_jobs`, `email_queues`, `grade_levels`, `groups`, `id_card_template_fields`, `messages`, `orientation_reports`, `payment_concepts`, `payments`, `prematriculation_histories`, `prematriculation_periods`, `prematriculations`, `scan_logs`, `schedule_entries`, `school_id_card_settings`, `school_schedule_configurations`, `schools`, `security_settings`, `shifts`, `specialties`, `student_activity_scores`, `student_assignments`, `student_id_cards`, `student_payment_access`, `student_qr_tokens`, `student_subject_assignments`, `students`, `subject_assignments`, `subjects`, `teacher_assignments`, `teacher_work_plan_details`, `teacher_work_plan_review_logs`, `teacher_work_plans`, `time_slots`, `trimester`, `user_grades`, `user_groups`, `user_subjects`, `users`.

**Nota Fase 1 (detalle PK/FK/columna por tabla):** no se adjunta matriz tabla-a-tabla en este entregable por tamaño; el modelo canónico está en **EF Core** (`SchoolDbContext`, migraciones y snapshot). Para auditoría DB exhaustiva se recomienda exportar `pg_dump --schema-only` y/o diagrama ER automatizado.

---

*Documento generado como entrega solicitada. No refleja certificación formal; para compliance regulatorio se requiere pentest externo y revisión legal.*
