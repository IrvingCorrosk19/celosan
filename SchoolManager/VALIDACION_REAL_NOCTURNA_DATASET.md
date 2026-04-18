# Validación real — dataset nocturno (Pruebas_Nocturna_Avanzado.xlsx)

**Alcance:** validación por **ejecución real** donde fue posible, sin cambios de código ni implementación.  
**Fecha de ejecución (evidencia SQL):** 2026-04-18  
**Cliente PostgreSQL:** `C:\Program Files\PostgreSQL\18\bin\psql.exe`  
**Base de datos:** `schoolmanager_daqf` (cadena de conexión según `appsettings.Development.json` del proyecto; credenciales no reproducidas en este documento).

---

## 1. Resumen ejecutivo

| Ítem | Resultado |
|------|-----------|
| Archivo `Pruebas_Nocturna_Avanzado.xlsx` en rutas buscadas | **No localizado** (`Downloads`, repo `EduplanerNoche`) |
| Carga masiva vía UI/API autenticada | **No ejecutada** en esta sesión (requiere app en ejecución, sesión admin/secretaría y archivo Excel) |
| PostgreSQL `schoolmanager_daqf` | **Conectado**; consultas `SELECT` ejecutadas correctamente |
| Datos operativos (estudiantes, matrículas, asistencia) en la BD consultada | **Ausentes** (0 matrículas activas/inactivas, 0 filas de asistencia, 0 usuarios con rol estudiante) |
| Catálogo académico (imparticiones, grupos, niveles) | **Presente** (1142 imparticiones, 30 grupos, 6 niveles) |

**Conclusión breve:** no fue posible validar de extremo a extremo el comportamiento del sistema con el Excel indicado porque el archivo no estuvo disponible en el entorno de validación y la base consultada no contiene el volumen de datos post-carga necesario para probar horarios, asistencia, gradebook, reportes ni carnet sobre ese dataset. Sí se obtuvo **evidencia real** del estado actual de la base y de consultas de integridad (vacías por falta de filas).

---

## 2. Resultado de carga de datos (Fase 1)

### 2.1 Archivo Excel

- **Búsqueda realizada:** patrones `**/Pruebas_Nocturna_Avanzado.xlsx` en `c:\Users\irvin\Downloads` y `c:\Proyectos\EduplanerNoche`.
- **Resultado:** **0 archivos encontrados.**
- **Implicación:** **NO SE PUDO** ejecutar la carga usando el archivo nominal en este entorno.

### 2.2 Flujo real (controllers / services / DB)

- **No ejecutado:** petición HTTP autenticada a `/StudentAssignment/Upload` + `POST /StudentAssignment/SaveAssignments` con el cuerpo generado desde el Excel (no hay automatización de navegador ni credenciales proporcionadas para esta validación).
- **Implicación:** **NO SE PUDO** verificar inserciones, errores de validación del endpoint, duplicados ni mensajes de negocio tras una carga real del dataset.

### 2.3 Estado observado en BD tras intento de validación indirecta

La base `schoolmanager_daqf` **no contiene** filas en `student_assignments` ni `student_subject_assignments` en el momento de la consulta (ver sección 9). No hay evidencia en esta BD de que `Pruebas_Nocturna_Avanzado.xlsx` haya sido cargado previamente, al menos no dejando matrículas persistidas.

---

## 3. Validación por módulo

### 3.1 Matrícula (Fase 2)

| Pregunta | Estado |
|----------|--------|
| ¿Estudiantes con múltiples niveles/grupos/materias insertados correctamente con este dataset? | **NO PROBADO** (sin carga del Excel; sin estudiantes en BD) |
| ¿Duplicados / rechazos / sobrescritura? | **NO PROBADO** a nivel aplicación |

**Evidencia SQL (integridad sobre datos existentes):** no hay filas activas en `student_assignments`; las consultas de duplicados activos devolvieron **0 grupos** (ver §9).

---

### 3.2 Horarios (Fase 3)

| Pregunta | Estado |
|----------|--------|
| ¿Consolida clases con múltiples grupos? ¿Faltan, duplican o conflictúan? | **NO PROBADO** (sin estudiantes matriculados; sin ejecución de generación de horario para un alumno concreto de este dataset) |

---

### 3.3 Asistencia (Fase 4)

| Pregunta | Estado |
|----------|--------|
| ¿Duplicado / falta en listas / mezcla entre grupos? | **NO PROBADO** |

**Evidencia SQL:** `SELECT COUNT(*) FROM attendance` → **0** filas.

---

### 3.4 Gradebook (Fase 5)

| Pregunta | Estado |
|----------|--------|
| ¿Notas separadas por grupo? ¿Mezcla? ¿Promedios correctos? | **NO PROBADO** |

**Contexto:** existe tabla `student_activity_scores`; no se ejecutó validación funcional de UI ni de reglas de negocio por grupo por falta de datos y de sesión docente.

---

### 3.5 Reportes (Fase 6)

| Pregunta | Estado |
|----------|--------|
| Reporte individual, aprobados/reprobados, múltiples contextos | **NO PROBADO** (sin datos de alumnos ni notas vinculadas al dataset) |

---

### 3.6 Reasignación (Fase 7)

| Pregunta | Estado |
|----------|--------|
| Cambio de grupo, múltiples matrículas activas, duplicados/borrados | **NO PROBADO** en UI |

**Evidencia SQL:** no hay matrículas que permitan simular reasignación sobre el estado actual de esta base.

---

### 3.7 Carnet (Fase 8)

| Pregunta | Estado |
|----------|--------|
| Contexto correcto, múltiples asignaciones, ambigüedad | **NO PROBADO** (sin estudiantes en BD para el flujo de carnet con ese dataset) |

---

## 4. Problemas detectados (tabla)

| ID | Severidad | Hallazgo | Evidencia |
|----|-----------|----------|-----------|
| P1 | **Crítica (para esta validación)** | No se localizó `Pruebas_Nocturna_Avanzado.xlsx` en el entorno usado | Búsqueda de archivos: 0 coincidencias |
| P2 | **Crítica (para esta validación)** | No se ejecutó la carga masiva real por HTTP con sesión | No hay trazas de request ni capturas en esta sesión |
| P3 | **Alta** | La BD `schoolmanager_daqf` consultada no tiene usuarios con rol estudiante ni matrículas | SQL §9 |
| P4 | **Media** | Catálogo académico existe, pero no hay traslape verificado entre “datos de prueba nocturnos” y operación en alumnos | Conteos `subject_assignments` / `groups` vs `student_assignments` |

---

## 5. Errores críticos

- **Bloqueo de la validación E2E:** sin archivo Excel accesible y sin ejecución autenticada del flujo de carga, **no hay** validación real del pipeline “Excel → `SaveAssignments` → tablas”.
- **BD sin población estudiantil:** con **0** `student_assignments` y **0** `attendance`, las fases 2–8 quedan **sin evidencia ejecutable** sobre datos reales del dataset.

*(“Errores” aquí = fallos o vacíos **de la validación** o del entorno; no se atribuye fallo de producto sin prueba.)*

---

## 6. Errores medios

- Imposibilidad de contrastar “misma materia en distintos grupos” en datos reales cargados desde el Excel.
- Imposibilidad de validar consolidación de horarios y gradebook por grupo sin matrículas y sin sesión.

---

## 7. Errores bajos

- No se verificó si otra copia de la base o otra ruta del Excel existiría fuera de las rutas buscadas (limitación del entorno).

---

## 8. Evidencia (queries y resultados)

**Herramienta:** `psql` desde PostgreSQL 18, base `schoolmanager_daqf`.

```sql
SELECT current_database(), COUNT(*) AS users_total FROM users;
```
**Resultado:** `schoolmanager_daqf` | `13` usuarios.

```sql
SELECT LOWER(TRIM(role)) AS role, COUNT(*) FROM users GROUP BY 1 ORDER BY 2 DESC;
```
**Resultado:** roles `secretaria`, `admin`, `clubparentsadmin`, `teacher`, `superadmin`, `inspector` únicamente; **ningún** `estudiante` / `student`.

```sql
SELECT COUNT(*) AS active_student_assignments FROM student_assignments WHERE is_active = true;
SELECT COUNT(*) AS total_sa FROM student_assignments;
SELECT COUNT(*) AS inactive_sa FROM student_assignments WHERE is_active = false;
```
**Resultado:** `0` / `0` / `0`.

```sql
SELECT COUNT(*) AS ssa_total FROM student_subject_assignments;
```
**Resultado:** `0`.

```sql
SELECT COUNT(*) FROM attendance;
```
**Resultado:** `0`.

```sql
SELECT id, name, is_active FROM academic_years ORDER BY created_at DESC NULLS LAST LIMIT 5;
```
**Resultado:** un año `2026` activo (`is_active = true`).

```sql
SELECT COUNT(*) AS subject_assignments FROM subject_assignments;
SELECT COUNT(*) AS groups_n FROM groups;
SELECT COUNT(*) AS grade_levels_n FROM grade_levels;
```
**Resultado:** `1142` imparticiones, `30` grupos, `6` niveles.

```sql
SELECT s.name AS shift_name, COUNT(g.id) AS n
FROM groups g
LEFT JOIN shifts s ON s.id = g.shift_id
GROUP BY s.name
ORDER BY n DESC;
```
**Resultado (ejemplos):** `Tarde` 13, `Mañana` 11, `Noche` 4, `(NULL)` 2.

**Consultas de duplicados (matrícula / inscripción materia) — diseño de prueba:**

```sql
SELECT student_id, COUNT(*) AS n
FROM student_assignments
WHERE is_active = true
GROUP BY student_id
HAVING COUNT(*) > 1;
```
**Resultado:** 0 filas (sin datos para interpretar como “correcto” o “incorrecto”; solo **no hay casos**).

```sql
SELECT student_id, grade_id, group_id, shift_id, academic_year_id, COUNT(*) AS dup
FROM student_assignments
WHERE is_active = true
GROUP BY 1,2,3,4,5
HAVING COUNT(*) > 1;
```
**Resultado:** 0 filas.

```sql
SELECT student_id, subject_assignment_id, academic_year_id, COUNT(*) AS dup
FROM student_subject_assignments
WHERE is_active = true
GROUP BY 1,2,3
HAVING COUNT(*) > 1
LIMIT 20;
```
**Resultado:** 0 filas.

---

## 9. Validación de base de datos (Fase 9)

- **Solo `SELECT`:** cumplido; no se ejecutaron `INSERT`/`UPDATE`/`DELETE`.
- **Duplicados / relaciones / inconsistencias sobre el dataset cargado:** **no aplicable** con el estado actual (tablas operativas vacías).
- **Hallazgo objetivo:** la instancia consultada tiene **catálogo** y **año académico**, pero **no** tiene la población mínima (estudiantes + matrículas + asistencia) para auditar el escenario “nocturna avanzada” descrito en el Excel.

---

## 10. Regresión diurna (Fase 10)

| Área | Estado |
|------|--------|
| Matrícula tradicional / asistencia diurna / reportes diurnos | **NO PROBADO** en ejecución (sin datos de referencia “antes/después” ni comparación en runtime) |

**Observación factual:** en `groups` existen jornadas `Mañana` y `Tarde` además de `Noche`; eso solo indica **catálogo** mixto, no una prueba de regresión funcional.

---

## 11. Veredicto final (obligatorio)

**Símbolo:** ⚠ **SOPORTA PARCIAL**

**Justificación (solo con base en lo ejecutado):**

- **No** se demostró, por **ejecución real completa**, que el sistema procese correctamente `Pruebas_Nocturna_Avanzado.xlsx` hasta horarios, asistencia, gradebook, reportes, reasignación y carnet: faltó el archivo en el entorno y la carga HTTP autenticada no se ejecutó.
- **Sí** se demostró, por **ejecución real**, que la base `schoolmanager_daqf` es accesible y que, en el instante consultado, contiene **catálogo académico** y **año 2026**, pero **cero** matrículas, **cero** inscripciones por materia y **cero** asistencias, y **ningún** usuario estudiante.

### Respuesta directa a la pregunta obligatoria

**¿El sistema soporta estudiantes nocturnos con datos reales (según esta validación)?**  
**No se pudo afirmar ni refutar por prueba de extremo a extremo con `Pruebas_Nocturna_Avanzado.xlsx`.** El veredicto **⚠ SOPORTA PARCIAL** refleja: **evidencia de soporte de catálogo y BD operativa**, pero **ausencia total de evidencia ejecutable** sobre el dataset y los módulos dependientes de matrícula en esta sesión.

---

## 12. Cómo repetir la evidencia SQL (sin contraseña en documento)

1. Configurar `PGPASSWORD` o usar `.pgpass` con los valores de su `appsettings` local (no versionar secretos).
2. Ejemplo de invocación (ajustar host/usuario según su cadena):

`"C:\Program Files\PostgreSQL\18\bin\psql.exe" -h <HOST> -p 5432 -U <USUARIO> -d schoolmanager_daqf -c "<SQL aquí>"`

---

*Fin del documento. No se aplicaron correcciones al producto: solo constatación y registro.*
