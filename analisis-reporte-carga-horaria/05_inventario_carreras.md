# 05 — Inventario de carreras (fase previa, sin implementación)

Fuente: PostgreSQL local `schoolmanager_daqf` (solo lectura) y modelo `SchoolManager/Models/Specialty.cs`.

`specialties` **no tiene** campo `is_active` / `status`. No existe estado activo/inactivo a nivel de carrera.

---

## Respuestas directas

| # | Pregunta | Respuesta |
|---|---|---|
| 1 | ¿Cuántas carreras/bachilleratos existen? | **5** |
| 2 | Nombres exactos | Ver tabla siguiente |
| 3 | ID de cada carrera | Ver tabla siguiente |
| 4 | Activas / inactivas | **No existe ese estado** en `specialties`. Las 5 están presentes y tienen asignaciones |
| 5 | Grados asociados | Autotrónica/Electricidad/Informática/Turismo: 10, 11, 12. PRE-MEDIA: 7, 8, 9 **y además 10, 11** (anómalo) |
| 6 | Materias por grado | Ver desglose. Conteo = `COUNT(DISTINCT subject_id)` por carrera y grado |
| 7 | Áreas por carrera | Las 5 usan HUMANISTICA, CIENTÍFICA y TECNOLÓGICA. PRE-MEDIA 10 y 11 **solo HUMANISTICA** |
| 8 | Materias por área | Ver desglose |
| 9 | ¿Materias sin área? | En `subject_assignments`: **0**. En catálogo `subjects."AreaId"`: **83/83 NULL** |
| 10 | ¿Materias sin grado? | En `subject_assignments`: **0** (`grade_level_id` siempre informado) |
| 11 | ¿Carreras sin materias? | **No**. Las 5 tienen asignaciones |
| 12 | ¿Estructuras distintas 10/11/12? | **Sí**, en todas las carreras de bachillerato el número y el mix de materias cambia por grado |
| 13 | ¿Duplicados / inconsistencias en `subject_assignments`? | No hay duplicado exacto (carrera+materia+grado+área+grupo). Sí hay inconsistencias de grupo/grado, secciones incompletas y PRE-MEDIA 10/11 |

---

## Tabla resumen

Materias = pares distintos carrera+grado+materia (no filas crudas de grupo).

| ID Carrera | Carrera | Grados | Cantidad materias (pares grado) | Filas `subject_assignments` | Áreas utilizadas | Estado |
|---|---|---|---:|---:|---|---|
| `a89c4c24-5e5e-4d27-90b3-08421ecfb3bb` | BACHILLER EN ELECTRICIDAD | 10, 11, 12 | 39 | 78 | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | Sin campo de estado (en uso) |
| `9ea48f6c-c93b-4ef7-9c3c-ef40ddfb7c78` | BACHILLER EN AUTOTRÓNICA | 10, 11, 12 | 36 | 72 | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | Sin campo de estado (en uso) |
| `49345fb9-e731-45d9-a854-b5438d74af31` | BACHILLER EN INFORMÁTICA | 10, 11, 12 | 40 | 84 | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | Sin campo de estado (en uso) |
| `fd0c59e0-e8c1-4195-a96d-1f87989da6fc` | BACHILLER EN TURISMO | 10, 11, 12 | 40 | 80 | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | Sin campo de estado (en uso) |
| `a649e236-ab96-4815-b5cd-2480bb8427d5` | PRE-MEDIA | 7, 8, 9, 10, 11 | 75 | 121 | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA (10/11 solo HUMANISTICA) | Sin campo de estado (en uso) |

Escuela: `6e42399f-6f17-4585-b92e-fa4fff02cb65` (todas las carreras).

---

## Desglose por carrera

Conteo de materias = distintas por grado. `sa_filas` incluye repetición por grupo.

### CARRERA: BACHILLER EN ELECTRICIDAD

**10°** — 14 materias · áreas: HUMANISTICA (5), CIENTÍFICA (3), TECNOLÓGICA (6) · sin área: 0  
Humanística: Español (Lenguaje y Comunicación), Inglés (Lenguaje y Comunicación), Geografía de Panamá, Ética/Moral/Valores, Bellas Artes  
Científica: Matemática, Ciencias Naturales, Educación Física y Salud Integral  
Tecnológica: Dibujo I (Lineal), Tecnología de la Información, Seguridad Industrial, Taller I, Taller II, Taller III

**11°** — 12 materias · áreas: HUMANISTICA (4), CIENTÍFICA (3), TECNOLÓGICA (5) · sin área: 0

**12°** — 13 materias · áreas: HUMANISTICA (5), CIENTÍFICA (3), TECNOLÓGICA (5) · sin área: 0

Grupos: 2 por grado (`10-A3`/`A3`, `11-A3`/`A3`, `12-A3`/`A3`), simétricos (14/12/13 × 2 = 28/24/26 filas). Estructura 10≠11≠12 (esperado).

---

### CARRERA: BACHILLER EN AUTOTRÓNICA

**10°** — 13 materias · HUMANISTICA 5, CIENTÍFICA 3, TECNOLÓGICA 5 · sin área: 0  
**11°** — 10 materias · HUMANISTICA 4, CIENTÍFICA 3, TECNOLÓGICA 3 · sin área: 0  
**12°** — 13 materias · HUMANISTICA 5, CIENTÍFICA 3, TECNOLÓGICA 5 · sin área: 0

11° tiene menos materias tecnológicas (3 vs 5). Grupos: 2 por grado, simétricos.

---

### CARRERA: BACHILLER EN INFORMÁTICA

**10°** — 13 materias · HUMANISTICA 7, CIENTÍFICA 3, TECNOLÓGICA 3 · sin área: 0  
Humanística 7 incluye pares casi duplicados: `ESPAÑOL` vs `ESPAÑOL (LENGUAJE Y COMUNICACIÓN)`; `ÉTICA Y VALORES` vs `ÉTICA MORAL, VALORES Y RELACIONES HUMANAS` (cada uno en un grupo distinto).  
Grupos desbalanceados: `10-A1` = 11 materias, `A1` = 10.

**11°** — 13 materias · HUMANISTICA 5, CIENTÍFICA 4, TECNOLÓGICA 4 · sin área: 0  
Grupos `11-A1` y `A1` simétricos (12+12 filas; 13 distintas en el grado).

**12°** — 14 materias · HUMANISTICA 6, CIENTÍFICA 3, TECNOLÓGICA 5 · sin área: 0  
Inconsistencia: hay 13 asignaciones de grado 12 en el grupo `11-A1` (nombre de 11°).

---

### CARRERA: BACHILLER EN TURISMO

**10°** — 13 materias · HUMANISTICA 6, CIENTÍFICA 2, TECNOLÓGICA 5 · sin área: 0  
Científica solo: Educación Física y Salud Integral, Matemática (no hay Ciencias/Física/Química en 10°).

**11°** — 13 materias · HUMANISTICA 5, CIENTÍFICA 2, TECNOLÓGICA 6 · sin área: 0

**12°** — 14 materias · HUMANISTICA 6, CIENTÍFICA 1, TECNOLÓGICA 7 · sin área: 0  
Científica 12°: solo Matemática.

Materias comerciales/turísticas están en **TECNOLÓGICA** (Contabilidad, Mercadotecnia, Tecnología Comercial, Gestión Empresarial Turística, etc.), no sin clasificar.

---

### CARRERA: PRE-MEDIA

**7°** — 17 materias · HUMANISTICA 12, CIENTÍFICA 3, TECNOLÓGICA 2 · sin área: 0  
**8°** — 17 materias · HUMANISTICA 12, CIENTÍFICA 3, TECNOLÓGICA 2 · sin área: 0  
**9°** — 16 materias · HUMANISTICA 11, CIENTÍFICA 3, TECNOLÓGICA 2 · sin área: 0

**10°** — 20 materias · **solo HUMANISTICA** · sin área: 0, pero clasificación incorrecta  
Incluye Matemática, Ciencias Naturales, Ciencias Integradas, Seguridad Industrial, Contabilidad, programación, turismo, etc., todas como HUMANISTICA. Parecen materias de bachillerato colgadas en PRE-MEDIA.

**11°** — 5 materias · **solo HUMANISTICA** · sin área: 0  
Incluye Turismo Sostenible, Matemática, Español, Historia, Lógica/Filosofía. Una fila de Historia de Panamá grado 11 está en grupo `10-A`.

Inconsistencias de grupo: grupo `A` (`groups.grade = 7`) usado también en asignaciones de 8° y 9°; 1 materia de 8° en grupo `9-A`.

---

## Análisis de áreas

Áreas actuales en `area` (las 3 activas):

| ID | Nombre | is_active |
|---|---|---|
| `7d91cd0b-c28e-4e15-bfa3-efbe9187e55b` | HUMANISTICA | true |
| `00630e6b-29d6-4122-98a1-6f3d992b50d9` | CIENTÍFICA | true |
| `fc8f8ac6-faf1-4307-b503-223f3053f255` | TECNOLÓGICA | true |

Uso: las 5 carreras usan las 3 en al menos un grado (salvo PRE-MEDIA 10/11, solo HUMANISTICA).

**¿Hay materias sin ninguna de las 3 áreas en `subject_assignments`?** No. 0 filas con `area_id` NULL.

**¿Hay que agregar áreas nuevas ahora?** No es obligatorio por hueco de datos. Candidata opcional:

Carrera: BACHILLER EN TURISMO  

Área requerida (evaluación, no creada): **COMERCIAL**  

Motivo: Contabilidad, Mercadotecnia y Publicidad, Tecnología Comercial, Gestión Empresarial Turística están agrupadas hoy en TECNOLÓGICA. No están sin clasificar; la clasificación actual puede no coincidir con una malla MEDUCA que separe comercial de tecnológica.  

Evidencia: `subject_assignments` + `subjects.name` + `area.name = TECNOLÓGICA` para esas materias en Turismo 10/11/12.

PRE-MEDIA 10 no necesita un área nueva: necesita **reclasificar** (Ciencias/Matemática no son HUMANISTICA).

Catálogo `subjects`: las 83 materias tienen `"AreaId"` NULL. El área real vive solo en la asignación.

---

## Estimación estructural de carga horaria B1/B2

Unidad: par distinto **carrera + grado + materia** (como el ejemplo de Electricidad = 39, no las 78 filas de grupo).  
Bloques = 2 (B1 y B2). **No se insertaron horas.**

| Carrera | Materias asignadas (pares grado) | Bloques | Registros potenciales de carga |
|---|---:|---:|---:|
| BACHILLER EN ELECTRICIDAD | 39 | 2 | 78 |
| BACHILLER EN AUTOTRÓNICA | 36 | 2 | 72 |
| BACHILLER EN INFORMÁTICA | 40 | 2 | 80 |
| BACHILLER EN TURISMO | 40 | 2 | 80 |
| PRE-MEDIA (todos los grados actuales, incl. 10 y 11) | 75 | 2 | 150 |
| **Total** | **230** | **2** | **460** |

Si se excluyeran PRE-MEDIA 10 y 11 (25 pares anómalos): PRE-MEDIA 7–9 = 50 pares → 100 registros; total general = **410**.

Si se usaran las 435 filas crudas de `subject_assignments` × 2 = 870; eso duplicaría secciones (`A3` y `10-A3`) y no es la unidad del reporte curricular.

---

## Totales de fase

TOTAL DE CARRERAS: **5**

TOTAL DE SUBJECT_ASSIGNMENTS: **435**

TOTAL DE ÁREAS ACTUALES: **3**

TOTAL DE MATERIAS: **83** (6 sin ninguna asignación)

TOTAL DE POSIBLES REGISTROS B1/B2: **460** (sobre 230 pares carrera-grado-materia)

CARRERAS CON PROBLEMAS DE ESTRUCTURA: **2** (INFORMÁTICA, PRE-MEDIA)

CARRERAS QUE NECESITAN ÁREAS ADICIONALES: **0 obligatorias** / **1 candidata opcional (TURISMO → COMERCIAL)**

---

## Problemas encontrados

1. `specialties` no tiene activo/inactivo.  
2. `subjects."AreaId"` vacío en 83/83; el área solo está en `subject_assignments`.  
3. 6 materias huérfanas: FDC, FDC2, MCA1, MCA2, ORIENTACION, VAL. ETICOS / RELACIONES HUMANAS (sí existen FDC 1, MCA 1, etc. asignadas a PRE-MEDIA).  
4. `subject_assignments.status`: 404 NULL y 31 `Active`.  
5. PRE-MEDIA con grados 10 y 11 (20+5 materias), típicos de bachillerato, todos en HUMANISTICA.  
6. PRE-MEDIA 10: Matemática, Ciencias, Seguridad Industrial, Contabilidad, etc. clasificadas como HUMANISTICA.  
7. PRE-MEDIA 11: Historia de Panamá grado 11 en grupo `10-A`.  
8. PRE-MEDIA: grupo `A` con `groups.grade = 7` usado en asignaciones de 8° y 9°.  
9. INFORMÁTICA 12° asignado también al grupo `11-A1` (13 materias).  
10. INFORMÁTICA 10° desbalanceado (`10-A1` 11 vs `A1` 10) y duplicados de nombre (Español / Español Lenguaje; dos Éticas).  
11. Turismo 12° científica: 1 materia (Matemática); 10° y 11° científicas: 2. Estructura distinta, posiblemente curricular.  
12. No hay duplicado exacto carrera+materia+grado+área+grupo. La repetición por dos grupos (`10-A3` y `A3`) es seccional, no B1/B2.

No se crearon tablas, migraciones, servicios ni horas.
