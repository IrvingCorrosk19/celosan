# 06 — Cotejo de planes oficiales vs Celosan

Fase de análisis únicamente. No se crearon tablas, migraciones, endpoints ni se modificaron datos.

Punto de partida: `05_inventario_carreras.md`.  
Fuentes: PostgreSQL `schoolmanager_daqf` (solo lectura), `SubjectAssignment`, `AcademicCatalogController`, `AcademicAssignmentService`, `SubjectAssignmentController`, `StudentAssignmentService`, `SEED_CURRICULUM_NOCTURNA_FROM_SUBJECT_ASSIGNMENTS.sql`.

Los PDFs/hojas oficiales **no están en el repositorio**. El cotejo de cantidades exactas de materias vs documento MEDUCA **no se pudo confirmar con un archivo del proyecto**. Sí se cotejó la estructura esperada que indicó esta fase (grados, áreas, B1/B2, horas).

---

## 1. Resumen ejecutivo

Celosan tiene los 5 programas. Los cuatro bachilleratos sí están en 10°, 11° y 12°. PRE-MEDIA debería ser 7°–9°; en la base también tiene 10° y 11°.

`subject_assignments` **no es un plan de estudio**. Es una oferta operativa: carrera + área + materia + grado + **grupo**. Por eso hay 435 filas y solo 230 combinaciones curriculares.

Consecuencia: **no es seguro** colgar B1/B2/horas en `subject_assignment_id`. Se duplicaría la carga por paralelo (`A3` y `10-A3`, `A1` y `10-A1`, etc.).

`curriculum_subjects` tampoco sirve tal cual: no tiene especialidad ni área, y se sembró desde todas las asignaciones nocturnas (incluye PRE-MEDIA 10/11).

**Decisión: OPCIÓN B.**

---

## 2. Cotejo de las 5 carreras/programas

### Grados esperados vs actuales

| Programa | Grados esperados | Grados en Celosan | Estado |
|---|---|---|---|
| PRE-MEDIA / Básica General | 7, 8, 9 | 7, 8, 9, **10, 11** | ❌ |
| BACHILLER EN ELECTRICIDAD | 10, 11, 12 | 10, 11, 12 | ✅ |
| BACHILLER EN AUTOTRÓNICA | 10, 11, 12 | 10, 11, 12 | ✅ |
| BACHILLER EN INFORMÁTICA | 10, 11, 12 | 10, 11, 12 | ✅ grados; ⚠️ grupos |
| BACHILLER EN TURISMO | 10, 11, 12 | 10, 11, 12 | ✅ |

Nombres en `specialties`: no dicen “Industrial” ni “Básica General”. Equivalencia por uso, no por título literal.

### Cantidades actuales (pares carrera+grado+materia)

| Programa | 7 | 8 | 9 | 10 | 11 | 12 | Total pares | Filas SA |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Electricidad | — | — | — | 14 | 12 | 13 | 39 | 78 |
| Autotrónica | — | — | — | 13 | 10 | 13 | 36 | 72 |
| Informática | — | — | — | 13 | 13 | 14 | 40 | 84 |
| Turismo | — | — | — | 13 | 13 | 14 | 40 | 80 |
| PRE-MEDIA | 17 | 17 | 16 | 20 | 5 | — | 75 | 121 |

10≠11≠12 en todos los bachilleratos: coherente con un plan que cambia por año. No implica error por sí solo.  
No se pudo contrastar el número oficial de asignaturas de cada hoja (no hay el documento en el repo).

### Matrices conceptuales (punto 10)

#### PROGRAMA: BACHILLER EN ELECTRICIDAD

| Elemento | Documento esperado | Celosan actual | Estado |
|---|---|---|---|
| Grados | 10, 11, 12 | 10, 11, 12 | ✅ |
| Áreas | 3 | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | ✅ |
| Materias | plan 10/11/12 | 14 / 12 / 13 pares | ⚠️ cantidades no cotejadas vs PDF |
| B1/B2 | Sí | No existe | ❌ |
| Horas | Sí | No existe | ❌ |
| Subtotales | Calculables | No (faltan horas) | ❌ |
| Total nivel | Calculable | No (faltan horas) | ❌ |
| Total asignaturas | Calculable | Sí (39 pares) | ✅ |

Estructura de grupos: 2 por grado (`*-A3` y `A3`), simétrica. La más limpia de las cinco.

#### PROGRAMA: BACHILLER EN AUTOTRÓNICA

| Elemento | Documento esperado | Celosan actual | Estado |
|---|---|---|---|
| Grados | 10, 11, 12 | 10, 11, 12 | ✅ |
| Áreas | 3 | 3 | ✅ |
| Materias | plan 10/11/12 | 13 / 10 / 13 | ⚠️ 11° más corto (10 materias); no cotejado vs PDF |
| B1/B2 | Sí | No | ❌ |
| Horas | Sí | No | ❌ |
| Subtotales | Calculables | No | ❌ |
| Total nivel | Calculable | No | ❌ |
| Total asignaturas | Calculable | Sí (36) | ✅ |

#### PROGRAMA: BACHILLER EN INFORMÁTICA

| Elemento | Documento esperado | Celosan actual | Estado |
|---|---|---|---|
| Grados | 10, 11, 12 | 10, 11, 12 | ✅ |
| Áreas | 3 | 3 | ✅ |
| Materias | plan 10/11/12 | 13 / 13 / 14 con duplicados de nombre | ⚠️ |
| B1/B2 | Sí | No | ❌ |
| Horas | Sí | No | ❌ |
| Subtotales | Calculables | No | ❌ |
| Total nivel | Calculable | No | ❌ |
| Total asignaturas | Calculable | Inflado por dos Español y dos Ética | ⚠️ |

#### PROGRAMA: BACHILLER EN TURISMO

| Elemento | Documento esperado | Celosan actual | Estado |
|---|---|---|---|
| Grados | 10, 11, 12 | 10, 11, 12 | ✅ |
| Áreas | 3 (Contabilidad etc. en TECNOLÓGICA) | 3; comerciales en TECNOLÓGICA | ✅ |
| Materias | plan 10/11/12 | 13 / 13 / 14; científica 12° = 1 (Matemática) | ⚠️ vs PDF no confirmado |
| B1/B2 | Sí | No | ❌ |
| Horas | Sí | No | ❌ |
| Subtotales | Calculables | No | ❌ |
| Total nivel | Calculable | No | ❌ |
| Total asignaturas | Calculable | Sí (40) | ✅ |

#### PROGRAMA: PRE-MEDIA

| Elemento | Documento esperado | Celosan actual | Estado |
|---|---|---|---|
| Grados | 7, 8, 9 | 7, 8, 9, **10, 11** | ❌ |
| Áreas | 3 | 7–9: 3 áreas. 10–11: solo HUMANISTICA | ❌ en 10/11 |
| Materias | plan básica | 17 / 17 / 16 + 20 + 5 anómalas | ❌ |
| B1/B2 | Sí (en la hoja) | No | ❌ |
| Horas | Sí | No | ❌ |
| Subtotales | Calculables | No | ❌ |
| Total nivel | Calculable | No | ❌ |
| Total asignaturas | Calculable en 7–9 | 10/11 no deberían entrar | ⚠️ |

---

## 3. Inconsistencias por programa

### Electricidad

- Correcto: grados, 3 áreas, materias sin área nula, 2 grupos simétricos.
- Sobrante curricular: no detectado a nivel de grado.
- Faltante vs PDF: NO SE PUDO CONFIRMAR.
- Riesgo B1/B2: bajo si se usa par carrera+grado+materia (39), alto si se usa cada SA (78).

### Autotrónica

- 11° tiene 10 materias vs 13 en 10° y 12°. Puede ser plan real o carga incompleta. Sin PDF no se afirma faltante.
- Grupos simétricos. Riesgo igual que Electricidad (36 vs 72).

### Informática

Ver sección 5. Riesgo alto: duplicados de catálogo y grupo `11-A1` con grado 12.

### Turismo

- Científica muy delgada (2, 2 y 1 materia). Si el PDF trae Física/Química/Ciencias, estarían faltantes. NO SE PUDO CONFIRMAR el PDF.
- Comerciales en TECNOLÓGICA: alineado con la instrucción de esta fase (no crear COMERCIAL).

### PRE-MEDIA

Ver sección 4.

---

## 4. Problemas de PRE-MEDIA

| Grado | Materias | Grupos asociados | Áreas | ¿Coherente con Pre-Media? |
|---|---:|---|---|---|
| 7° | 17 | `7-A`, `A` | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | Sí (estructura) |
| 8° | 17 | `8-A`, `9-A` (1 fila), `A` | las 3 | Parcial: grupo `A` es de 7°; 1 materia en `9-A` |
| 9° | 16 | `9-A`, `A` | las 3 | Parcial: grupo `A` (`groups.grade = 7`) |
| 10° | 20 | `10-A` | solo HUMANISTICA | **No** |
| 11° | 5 | `11-A`, `10-A` (1 fila) | solo HUMANISTICA | **No** |

**10° y 11°: POSIBLE ERROR DE DATOS** (no hay evidencia de estructura intencional de Básica General).

### Qué son esos registros

20 filas de 10° se crearon el **2026-06-08 18:42:11–18:42:39** (28 segundos).  
6 filas de 11° entre **2026-06-08** y **2026-06-16**.  
7°–9° son anteriores (desde 2026-05-29).

Eso coincide con una **carga masiva** (`AcademicCatalogController`: Excel → especialidad + área + materia + grado + grupo), no con un seed.

No hay migración ni seed que inserte PRE-MEDIA 10/11.  
`SEED_CURRICULUM_NOCTURNA_FROM_SUBJECT_ASSIGNMENTS.sql` **lee** `subject_assignments` nocturnas; no las crea. Si se corrió después, **copió** estas filas a la malla modular.

### Materias de 10°/11° PRE-MEDIA

Son las mismas `subjects.id` que ya existen en bachilleratos (Informática, Turismo, Electricidad, Autotrónica): Contabilidad, Francés, Turismo, Seguridad Industrial, Desarrollo Lógico, Ciencias Integradas, etc.

Todas colgadas de especialidad `PRE-MEDIA` (`a649e236-…`), área HUMANISTICA, grupo `10-A` / `11-A`.

### ¿Pertenecen a un bachiller?

Por nombre y por `subject_id`, **sí parecen materias de 10°/11° de bachiller**, mal etiquetadas como PRE-MEDIA.  
No son un tercer plan de Básica General.

### Estudiantes

`student_assignments` no tiene `specialty_id`. Hay 129 estudiantes en grupo `10-A` grado 10 y 62 en `11-A` grado 11. Son alumnos de **grado/grupo**, no “alumnos PRE-MEDIA”.

Riesgo operativo: `StudentAssignmentService` matricula **todas** las `subject_assignments` del par grado+grupo (`Status` null o no Closed). Un 10° en `10-A` puede heredar las 20 materias PRE-MEDIA de 10° además de las del bachiller del mismo grupo, si coexisten.

### Grupos 7–9

Grupo `A` tiene `groups.grade = 7` y se usa también en asignaciones de 8° y 9°. Eso es inconsistencia de grupo, no prueba que 10/11 sean válidos.

---

## 5. Problemas de Informática

No hay duplicado exacto (misma carrera+materia+grado+área+grupo).  
Sí hay **dos materias de catálogo** usadas como si fueran la misma asignatura, en **dos oleadas de carga** (31-may grupos `A1` vs 02-jun grupos `10-A1`/`11-A1`/`12-A1`).

### Español (materias distintas, mismo rol)

| subject_id | Nombre | Grado | Grupo | Área | subject_assignment_id | created_at |
|---|---|---|---|---|---|---|
| `f5b18db5-cca3-4c6e-b5df-30323d856cdd` | ESPAÑOL | 10 | A1 | HUMANISTICA | `3317883a-…` | 2026-05-31 |
| `f5b18db5-…` | ESPAÑOL | 11 | A1 | HUMANISTICA | `a44c7ea2-…` | 2026-05-31 |
| `f5b18db5-…` | ESPAÑOL | 12 | A1 | HUMANISTICA | `fa55ac32-…` | 2026-05-31 |
| `37791f8a-954a-457c-8b8e-b391112b0243` | ESPAÑOL (LENGUAJE Y COMUNICACIÓN) | 10 | 10-A1 | HUMANISTICA | `da71c947-…` | 2026-06-02 |
| `37791f8a-…` | ESPAÑOL (LENGUAJE Y COMUNICACIÓN) | 11 | 11-A1 | HUMANISTICA | `88d72001-…` | 2026-06-02 |
| `37791f8a-…` | ESPAÑOL (LENGUAJE Y COMUNICACIÓN) | 12 | 11-A1 | HUMANISTICA | `cba6612c-…` | 2026-06-02 |
| `37791f8a-…` | ESPAÑOL (LENGUAJE Y COMUNICACIÓN) | 12 | 12-A1 | HUMANISTICA | `c314c16d-…` | 2026-06-02 |

Conclusión: **no es el mismo ID**. Son dos registros de `subjects`. Para un plan curricular de Informática 10° deberían ser **una** asignatura de Español, no dos.

### Ética (materias distintas, solo 10°)

| subject_id | Nombre | Grado | Grupo | Área | subject_assignment_id |
|---|---|---|---|---|---|
| `89627a16-42bf-4690-bf05-5be22db9ad07` | ÉTICA Y VALORES | 10 | A1 | HUMANISTICA | `62a932a3-…` |
| `60f26e6d-ac33-4687-8f05-960c2ed8312d` | ÉTICA MORAL, VALORES Y RELACIONES HUMANAS | 10 | 10-A1 | HUMANISTICA | `0969cbfb-…` |

### Grupo 11-A1 con grado 12

13 `subject_assignments` de Informática grado **12** apuntan al grupo **`11-A1`**.  
Las mismas materias existen también en `12-A1` y `A1`.

Ejemplos de SA grado 12 / grupo 11-A1: Matemática `6d944a71-…`, Español (Lenguaje) `cba6612c-…`, Práctica profesional `c7e8d8b0-…`.

`11-A1` además tiene 12 materias de grado 11 (correcto para el nombre del grupo).

### Desbalance 10°

| Grupo | Materias |
|---|---:|
| 10-A1 | 11 |
| A1 | 10 |

Cada grupo tiene “su” Español y “su” Ética; no es el mismo set.

### Tabla de inconsistencias Informática

| Tipo | Evidencia | Riesgo si se implementa B1/B2 sobre SA |
|---|---|---|
| Dos Español | 2 `subject_id` | Dos cargas horarias para la misma celda curricular |
| Dos Ética en 10° | 2 `subject_id` | Igual |
| 12° en grupo 11-A1 | 13 SA | Horas atadas a un paralelo de 11° |
| Tres grupos en 12° | `A1`, `11-A1`, `12-A1` | Triplete de horas si FK = SA |
| 10° incompleto por grupo | 11 vs 10 | Un paralelo sin la materia “oficial” |

---

## 6. Unidad correcta de carga horaria

Clave lógica curricular:

**especialidad (carrera) + grado + materia (+ área)**

No incluye grupo, docente, trimestre ni horario.

`subject_assignments` **sí incluye grupo**. Alta:

```
AcademicAssignmentService.CreateAsignacionAsync(
  specialtyId, areaId, subjectId, gradeLevelId, groupId, schoolId)
```

Clave de duplicado en carga Excel:

`specialty.Id|area.Id|subject.Id|grade.Id|groupEntity.Id`

(`AcademicCatalogController`)

Por tanto representa **B) asignación operativa ligada a grupos/secciones**, no A) fila curricular única.

Varias filas para la misma combinación curricular se deben a:

| Causa | ¿Aplica? | Evidencia |
|---|---|---|
| Grupo / paralelo / sección | **Sí (causa principal)** | 181 combos × 2 grupos = 362 filas; 12 combos × 3 grupos = 36; 37 combos × 1 grupo = 37 |
| Docente | No duplica SA | `teacher_assignments` cuelga de un SA ya existente (221 TA / 190 SA con docente) |
| Periodo / trimestre | No | SA no tiene `trimester_id` |
| Año académico | No en SA | No hay `academic_year_id` en `subject_assignments` |
| Horario | No duplica SA | `schedule_entries` → `teacher_assignments` |

**Ejemplo esperado:** Informática + 10° + Matemática = **una** definición B1/B2.  
En Celosan Matemática 10° Informática tiene **2** SA (`10-A1` y `A1`), mismo `subject_id` `548c501b-…`.

---

## 7. Por qué 435 vs 230

230 = `COUNT(DISTINCT specialty_id, grade_level_id, subject_id)`.

| Grupos por combo curricular | Combinaciones | Filas SA |
|---|---:|---:|
| 1 | 37 | 37 |
| 2 | 181 | 362 |
| 3 | 12 | 36 |
| **Total** | **230** | **435** |

37 + 362 + 36 = 435.  
La diferencia **205** es repetición por grupo, no 205 materias nuevas.

Los 12 triples son sobre todo Informática 12° (`A1` + `11-A1` + `12-A1`) y PRE-MEDIA 8° (grupos extra).

### ¿ES SEGURO relacionar `subject_assignment_hours` directamente con `subject_assignments`?

**NO.**

Si la FK es `subject_assignment_id`, Informática 12° Matemática tendría 3 filas de horas (una por grupo) para el mismo plan. Electricidad 10° tendría el doble (A3 y 10-A3). El reporte B1/B2 sumaría de más.

Entidad más apropiada (solo diseño, no creada): una fila curricular estable

`carrera + grado + materia + área`

sea tabla nueva (`study_plan_subjects` / `curriculum_plan_subjects`) o `curriculum_subjects` **ampliada** con `specialty_id` y `area_id`. Sobre esa fila: B1, B2, horas.

`curriculum_subjects` actual **no** sirve sin cambio de modelo: no tiene especialidad ni área; 285 filas con `credits = 1.00`; nació de SA nocturnas mezclando carreras y PRE-MEDIA 10/11.

---

## 8. Status NULL vs Active

| Valor | Filas |
|---|---:|
| NULL | 404 |
| Active | 31 |
| Inactive / Closed | 0 |

`CreateAsignacionAsync` **no asigna Status** → las cargas Excel quedan NULL.

Código:

- UI: `Status = sa.Status ?? "Active"` (`SubjectAssignmentController` Index).
- Toggle: si no es Active ni Inactive, escribe `Active` (`ChangeStatus`).
- Matrícula: `(sa.Status == null \|\| sa.Status != "Closed")` (`StudentAssignmentService`).
- Prematrícula modular: igual, NULL permitido (`CelosamPrematriculationModuleService`).
- Prematrícula grados nocturnos: `sa.Status != "Closed"` (`PrematriculationController`). En SQL, `<> 'Closed'` **puede excluir NULL** (lógica ternaria). Riesgo menor de inconsistencia entre consultas.

**NULL equivale implícitamente a activo** en la UI y en la matrícula. **No** equivale a `status = 'Active'` si alguien filtra `Status == "Active"`: perdería 404 de 435 filas.

31 `Active`: 29 PRE-MEDIA + 2 Autotrónica (probablemente tocadas con ChangeStatus).

**Riesgo para carga horaria:** si el query de horas usa `Status == "Active"`, se ignoraría casi todo el plan. Hay que tratar NULL como vigente, o no filtrar por ese campo en una entidad curricular nueva.

---

## 9. Materias sin asignación

| ID | Nombre | Área catálogo | Posible carrera | Posible motivo |
|---|---|---|---|---|
| `e6a550e6-fd7a-4270-8447-3377408c370c` | FDC | ninguna (`AreaId` NULL) | PRE-MEDIA | Alias de `FDC 1` (sí asignada en 7°) |
| `17b399d1-02ca-4ceb-b89d-a0c091133a1e` | FDC2 | ninguna | PRE-MEDIA | Alias de `FDC 2` (8°) |
| `5f3390d0-b330-456b-ba4d-6a58c6294385` | MCA1 | ninguna | PRE-MEDIA | Alias de `MCA 1` (9°) |
| `6d5b6d62-830d-4e8f-88a0-b65070ecdc64` | MCA2 | ninguna | PRE-MEDIA | Alias de `MCA 2` (9°) |
| `4ac2212f-8774-4110-a5b7-88672e0ccdc6` | ORIENTACION | ninguna | PRE-MEDIA | Alias de `ORIENTACIÓN` (con tilde, asignada 7–9) |
| `87305b41-fbff-46df-8afa-b9e4dfbed671` | VAL. ETICOS / RELACIONES HUMANAS | ninguna | PRE-MEDIA | Alias de `VAL. ÉTICOS / REL. HUMANAS` |

No se asignaron. Son catálogo huérfano por variación de nombre, no planes extra.

---

## 10. Cotejo de áreas

Las 3 áreas oficiales existen y cubren el 100 % de `subject_assignments` (`area_id` NOT NULL).  
**No se propone COMERCIAL.** Turismo comerciales permanecen en TECNOLÓGICA.

Registros dudosos (solo inconsistentes):

| Carrera | Grado | Materia | Área actual | Observación |
|---|---|---|---|---|
| PRE-MEDIA | 10 | MATEMÁTICA | HUMANISTICA | En bachilleres está en CIENTÍFICA |
| PRE-MEDIA | 10 | CIENCIAS NATURALES | HUMANISTICA | Debería CIENTÍFICA |
| PRE-MEDIA | 10 | CIENCIAS INTEGRADAS | HUMANISTICA | En Informática es CIENTÍFICA |
| PRE-MEDIA | 10 | EDUCACIÓN FÍSICA | HUMANISTICA | En Informática es CIENTÍFICA |
| PRE-MEDIA | 10 | CONTABILIDAD | HUMANISTICA | En Turismo es TECNOLÓGICA |
| PRE-MEDIA | 10 | SEGURIDAD INDUSTRIAL | HUMANISTICA | En Electricidad es TECNOLÓGICA |
| PRE-MEDIA | 10 | TECNOLOGÍA DE LA INFORMACIÓN | HUMANISTICA | En bachilleres es TECNOLÓGICA |
| PRE-MEDIA | 10 | CONFIGURACIÓN Y ADMINISTRACIÓN DE SISTEMAS OPERATIVOS | HUMANISTICA | En Informática es TECNOLÓGICA |
| PRE-MEDIA | 10 | DESARROLLO LÓGICO Y PROGRAMACIÓN | HUMANISTICA | En Informática es TECNOLÓGICA |
| PRE-MEDIA | 10 | GESTIÓN EMPRESARIAL TURÍSTICA | HUMANISTICA | En Turismo es TECNOLÓGICA |
| PRE-MEDIA | 10 | TURISMO (INTRODUCCIÓN…) | HUMANISTICA | En Turismo es TECNOLÓGICA |
| PRE-MEDIA | 11 | MATEMÁTICA | HUMANISTICA | Misma distorsión |
| PRE-MEDIA | 11 | TURISMO SOSTENIBLE | HUMANISTICA | En Turismo es TECNOLÓGICA |

Bachilleratos: no se listan aquí Contabilidad/Mercadotecnia en TECNOLÓGICA (es el criterio de esta fase).  
Educación Física en CIENTÍFICA en los bachilleres se deja como está (coincide con muchas mallas MEDUCA).

---

## 11. Viabilidad de usar `subject_assignments` para B1/B2

No es viable como FK de horas:

1. Multiplicidad por grupo (435 ≠ 230).  
2. Informática 12° repetido en tres grupos.  
3. Dos IDs de Español / Ética.  
4. PRE-MEDIA 10/11 contaminaría el plan de Básica.  
5. Status NULL vs Active.  
6. No hay año ni bloque curricular en SA.

Sigue siendo la **fuente operativa** para saber qué se dicta en un grupo. El plan de horas debe ser otra entidad.

---

## 12. Arquitectura recomendada (sin crear)

```
Specialty (carrera)
    → grado (GradeLevel)
        → materia + área   [ENTIDAD CURRICULAR ÚNICA]
            → curriculum_blocks (B1, B2)
            → horas por bloque
```

`subject_assignments` permanece para: grupo, docente, horario, matrícula.

Opciones de entidad curricular:

| Opción | Pros | Contras |
|---|---|---|
| Ampliar `curriculum_subjects` con `specialty_id` + `area_id` | Ya existe malla | Hoy está mezclada, sin especialidad, credits=1, incluye PRE-MEDIA 10/11 |
| Tabla nueva `study_plan_subjects` (o equivalente) | Clave limpia carrera+grado+materia+área | Hay que crearla después; no ahora |

Recomendación de diseño: **entidad curricular intermedia** (nueva o `curriculum_subjects` redefinida). Horas y B1/B2 cuelgan de ella. Un job/consulta puede **derivar** las 230 combinaciones desde `subject_assignments` con `DISTINCT ON (specialty_id, grade_level_id, subject_id, area_id)`, **excluyendo** PRE-MEDIA grados 10 y 11 hasta que se decida depurar.

Bloques: catálogo `curriculum_blocks` (B1, B2) reutilizable por todas las carreras. Una sola estructura, no una tabla por carrera.

---

## 13. Riesgos antes de implementar

1. Colgar horas en SA duplica por paralelo.  
2. Incluir PRE-MEDIA 10/11 genera un plan de Básica falso.  
3. Contar `subject_id` distintos en Informática duplica Español y Ética.  
4. Filtrar `Status == "Active"` omite 404 filas.  
5. Reusar `curriculum_subjects` sin `specialty_id` mezcla las 5 carreras.  
6. Seed de malla nocturna volvería a copiar basura si se ejecuta igual.  
7. Matrícula por grado+grupo ya puede mezclar SA PRE-MEDIA 10 con bachiller en `10-A`.  
8. No hay PDF en el repo: no se puede validar faltantes oficiales (p. ej. Turismo científica).

No se corrigió ninguno de estos puntos en esta fase.

---

## Decisión final

OPCIÓN B:

NO ES SEGURO IMPLEMENTAR LA CARGA HORARIA DIRECTAMENTE SOBRE subject_assignments; SE RECOMIENDA UNA ENTIDAD CURRICULAR INTERMEDIA.

Justificación: `subject_assignments` está modelada y cargada como oferta por **grupo** (`CreateAsignacionAsync` + Excel). 230 planes distintos se inflan a 435 filas por 1–3 paralelos. Informática y PRE-MEDIA demuestran que esas filas no son una malla canónica. B1/B2 y horas deben vivir en una fila única `carrera + grado + materia + área`, y los grupos seguir usando `subject_assignments` para la operación diaria.
