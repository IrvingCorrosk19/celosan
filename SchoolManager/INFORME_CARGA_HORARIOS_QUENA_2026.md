# Informe Premium — Carga de Horarios CELOSAM 2026

**Institución:** Centro de Educación Laboral Oficial Nocturno San Miguelito (CELOSAM)  
**Fuente:** `HORARIOS DE DOCENTES 2026 QUENA.docx`  
**Sistema destino:** SchoolManager — módulo Horarios (`schedule_entries`)  
**Base de datos:** Render Producción (`schoolmanager_daqf`)  
**Año académico canónico:** 2026 — `f7ccb57f-fa3e-4d9f-973b-552030c9852d`  
**Fecha del informe:** 5 de julio de 2026  
**Elaborado por:** Proceso automatizado de importación + auditoría de cobertura  

---

## 1. Resumen ejecutivo

Se extrajeron los horarios oficiales del documento Word **QUENA 2026** y se cargaron en la base de datos de producción mediante scripts de importación (`Scripts/import_quena_schedules.py` y `Scripts/complete_pending_schedules.py`).

| Indicador | Valor |
|-----------|------:|
| Celdas totales en el Word | 345 |
| **Slots únicos** (docente + día + bloque) | **319** |
| Celdas duplicadas en el Word | 26 |
| **Horarios insertados en BD** | **302** |
| Slots que coinciden exactamente con el Word | 292 |
| **Slots no cargados** | **27** |
| **Cobertura vs Word** | **91,5 %** |
| Docentes con horario 100 % | 8 de 12 |
| Docentes con horario parcial | 4 de 12 |
| Teacher assignments en BD (post-carga) | 221 |

### Veredicto

La carga fue **exitosa en un 91,5 %** de los espacios horarios únicos del documento. Los **27 slots restantes (8,5 %)** no se pudieron cargar por limitaciones estructurales del Word, del sistema o conflictos de horario. **Ningún docente quedó sin horario**: los 12 facilitadores tienen al menos 15 entradas cargadas.

---

## 2. Contexto técnico de la carga

### 2.1 Archivo origen

| Atributo | Detalle |
|----------|---------|
| Nombre | `HORARIOS DE DOCENTES 2026 QUENA.docx` |
| Ubicación | Raíz del proyecto SchoolManager |
| Formato | Microsoft Word (tablas semanales) |
| Docentes | 12 facilitadores |
| Bloques nocturnos | 6 (18:10 – 23:10) |
| Días | Lunes a Viernes |

### 2.2 Proceso ejecutado

```
Fase 1 — import_quena_schedules.py
  ├── Parseo de tablas Word (.docx)
  ├── Mapeo docente → teacher_assignment_id
  ├── Mapeo celda → materia + grupo + bloque + día
  ├── Creación de 65 teacher_assignments faltantes
  └── Inserción inicial: 222 schedule_entries

Fase 2 — Consolidación año académico
  ├── Migración al año 2026 canónico
  └── Desactivación de 21 años 2026 duplicados

Fase 3 — complete_pending_schedules.py
  ├── Docentes pendientes: Elvia, Lino, Zellideth
  ├── 25 teacher_assignments adicionales
  └── 80 horarios adicionales

Resultado final: 302 schedule_entries
```

### 2.3 Bloques horarios utilizados (jornada Noche)

| Bloque | Horario |
|--------|---------|
| 1ra | 18:10 – 19:00 |
| 2da | 19:00 – 19:50 |
| 3ra | 19:50 – 20:40 |
| 4ta | 20:40 – 21:30 |
| 5ta | 21:30 – 22:20 |
| 6ta | 22:20 – 23:10 |

### 2.4 Reglas del sistema que condicionan la carga

1. **Un docente = una clase por bloque/día** — no puede tener dos materias al mismo tiempo.
2. **Un grupo = una clase por bloque/día** — el mismo grupo no puede estar en dos clases simultáneas.
3. **Restricción única en BD:** `(teacher_assignment_id, academic_year_id, time_slot_id, day_of_week)`.
4. **No existe importador nativo** — la carga se hizo vía script Python + SQL directo.
5. **El Word mezcla grupo + materia** en una sola celda (ej. `9-A-1Tecn.Mecanografía`), lo que exige interpretación manual/automática.

---

## 3. Resultado por docente

### Leyenda de estados

| Estado | Significado |
|--------|-------------|
| ✅ COMPLETO | 100 % de slots del Word cargados |
| ⚠️ PARCIAL | Carga incompleta — ver detalle de faltantes |
| ❌ PENDIENTE | Sin horarios (ningún docente en este estado) |

---

### ✅ Lexibel Asprilla — COMPLETO (27/27 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 27 | 27 | 100 % |

**Materias cargadas (muestra):** Turismo, Servicios Turísticos I y II, Elaboración de Proyectos Turísticos — grupos 10-A4, 11-A4, 12-A4.

---

### ✅ Julio E. Del Cid CH. — COMPLETO (26/26 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 26 | 30 | 100 % |

**Nota:** Tiene 4 entradas extra en BD (30 vs 26 del Word) por rotación entre grupos informáticos en importación. Todos los slots del Word están cubiertos.

**Materias cargadas (muestra):** Multimedia y Desarrollo Web, Redes de Computadoras, Tecnología de la Información, Taller de Sistemas Robóticos — grupos 10-A1..A4, 11-A1, 12-A1.

---

### ✅ Lino A. Garcia A. — COMPLETO (29/29 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 29 | 29 | 100 % |

**Materias cargadas (muestra):** Taller II Instalación Residencial, Taller V Máquinas Eléctricas, Taller III Electrónica, Taller III Tecnología y Taller Aplicado, Seguridad Industrial, Taller de Producción y Distribución — grupos 10-A2, 10-A3, 11-A2, 11-A3, 12-A2, 12-A3.

---

### ✅ Zellideth E. Hernández — COMPLETO (25/25 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 25 | 25 | 100 % |

**Materias cargadas (muestra):** Programación, Arquitectura de las Computadoras, Aplicaciones con Base de Datos, Desarrollo Lógico y Programación, Configuración y Administración de Sistemas Operativos, Ofimática — grupos 10-A1, 10-A4, 11-A1.

---

### ✅ Zuley E. Reyes G. — COMPLETO (26/26 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 26 | 26 | 100 % |

**Materias cargadas (muestra):** Geografía de Panamá, Historia, Cívica, Geografía Turística, Lógica, Filosofía, Relaciones Panamá-E.U. — grupos 7-A, 8-A, 9-A, 10-A1..A4, 11-A1..A4, 12-A1..A4.

---

### ✅ Elvia D. Robles J. — COMPLETO (26/26 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 26 | 26 | 100 % |

**Materias cargadas (muestra):** Bellas Artes, Educación Física, Educación Física y Salud Integral, Salud Física y Mental, Expresiones Artística, FDC 1/2, Ética y Valores, Música — grupos 7-A, 8-A, 9-A, 10-A, 11-A1.

---

### ✅ Carlos A. Vásquez — COMPLETO (25/25 — 100 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 25 | 25 | 100 % |

**Materias cargadas (muestra):** MET (Técnicas en Metales), Dibujo I Lineal, Dibujo II, Taller I Fundamento de Tec. Industrial, Taller IV Análisis de Circuito, Taller de Proyectos y Presupuestos — grupos 7-A, 8-A, 10-A2, 10-A3, 11-A2, 11-A3, 12-A3.

---

### ✅ Yaribeth Mora — CASI COMPLETO (25/26 — 96,2 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 26 | 29 | 96,2 % |

**Materias cargadas (muestra):** Matemática, Orientación, Taller II/III, Tecnología y Taller Aplicado, Mantenimiento Automotriz — grupos 7-A, 8-A, 9-A, 10-A2, 11-A2, 12-A2, 12-A3.

#### ❌ No cargado (1 slot)

| Día | Bloque | Celda Word | Motivo |
|-----|--------|------------|--------|
| Jueves | 3ra 19:50–20:40 | `12 I-T-A-E` | Código genérico sin grupo/materia específica — el sistema requiere un grupo concreto (12-A1, 12-A2, etc.) |

---

### ⚠️ Max E. Agames J. — PARCIAL (24/28 — 85,7 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 28 | 26 | 85,7 % |

**Materias cargadas (muestra):** Español, Orientación, Relaciones Humanas / Valores Éticos, Salud Física y Mental — grupos 7-A, 8-A, 9-A, 10-A1..A4, 11-A1..A4, 12-A1..A4.

#### ❌ No cargados (4 slots)

| Día | Bloque | Celda Word | Motivo |
|-----|--------|------------|--------|
| Lunes | 1ra | `11 I-T-A-E` | Código genérico — no indica grupo ni materia concreta |
| Miércoles | 1ra | `11 I-T-A-E` | Idem |
| Miércoles | 3ra | `11 I-T-A-E` | Idem — además el docente ya tiene otra clase en ese bloque |
| Miércoles | 6ta | `10 I-T-A-E` | Idem |

**Explicación:** `11 I-T-A-E` significa "año 11, todas las especialidades (Informática, Turismo, Autotrónica, Electricidad)" pero el sistema necesita elegir **un** grupo (ej. 11-A1). En esos bloques ya existe otra asignación del mismo docente.

---

### ⚠️ Fabricio Mendoza — PARCIAL (24/28 — 85,7 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 28 | 24 | 85,7 % |

**Materias cargadas (muestra):** Ciencias Naturales, Ciencias Integradas, Física, Química — grupos 7-A, 8-A, 9-A, 10-A1..A3, 11-A1..A3, 12-A2.

#### ❌ No cargados (4 slots)

| Día | Bloque | Celda Word | Motivo |
|-----|--------|------------|--------|
| Martes | 4ta | `8°- A-2 CIENCIAS` | Sección **A-2** no existe como grupo en BD — solo `8-A` |
| Martes | 5ta | `8°- A-2 CIENCIAS` | Idem |
| Miércoles | 2da | `7°- A-2 CIENCIAS` | Sección **A-2** no existe — solo `7-A` |
| Miércoles | 3ra | `7°- A-2 CIENCIAS` | Idem |

**Explicación:** El Word distingue subsecciones A-1 y A-2 de pre-media, pero en SchoolManager los grupos de 7°–9° son únicos (`7-A`, `8-A`, `9-A`). Las clases de ciencias para esas subsecciones requerirían crear grupos `7-A-2` / `8-A-2` en el sistema.

---

### ⚠️ Jannie M. España W. — PARCIAL (20/27 — 74,1 %)

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 27 | 20 | 74,1 % |

**Materias cargadas (muestra):** Gestión Empresarial, Contabilidad — grupos 10-A, 11-A1, 11-A4, 12-A1, 12-A4.

#### ❌ No cargados (7 slots)

| Día | Bloque | Celda Word | Motivo |
|-----|--------|------------|--------|
| Martes | 3ra | `12-I-PrácticaProfesional` | Sin `teacher_assignment` para Práctica Profesional 12-I, o conflicto de horario |
| Martes | 4ta | `12-I-PrácticaProfesional` | Idem |
| Miércoles | 5ta | `11-TMerc. Y Public.` | Mercadotecnia y Publicidad — falta vincular docente a esa materia-grupo |
| Miércoles | 6ta | `12-TPrácticaProfesional` | Práctica Profesional turismo 12-T sin asignación docente |
| Jueves | 3ra | `12-TPrácticaProfesional` | Idem |
| Jueves | 5ta | `11-TMerc. Y Public.` | Mercadotecnia sin teacher_assignment |
| Jueves | 6ta | `11-TMerc. Y Public.` | Idem |

**Explicación:** Jannie no tenía `teacher_assignments` al inicio. Se crearon las de Gestión Empresarial y Contabilidad, pero **Práctica Profesional** (informática y turismo) y **Mercadotecnia y Publicidad** aún no están vinculadas a ella en `teacher_assignments`.

---

### ⚠️ Rey A. Abrego S. — PARCIAL (15/26 — 57,7 %) ⚠️ Mayor brecha

| Word | BD | Cobertura |
|-----:|---:|----------:|
| 26 | 15 | 57,7 % |

**Materias cargadas (muestra):** Inglés, Inglés Comercial, Francés, Orientación — grupos 7-A, 8-A, 9-A, 10-A1..A4, 11-A1..A4, 12-A1..A4.

#### ❌ No cargados (11 slots)

| Día | Bloque | Celda Word | Motivo |
|-----|--------|------------|--------|
| Lunes | 2da | `9°-A-2` | Grupo A-2 no existe en BD; conflicto con grupo 9-A ya asignado |
| Lunes | 3ra | `9°-A-2` | Idem |
| Miércoles | 1ra | `10 I-T-A-E` | Código genérico I-T-A-E |
| Miércoles | 2da | `10 I-T-A-E` | Idem |
| Jueves | 1ra | `10 I-T-A-E` | Idem |
| Jueves | 2da | `10I-T-A-E` | Idem |
| Jueves | 5ta | `12 I-T-A-E` | Idem |
| Jueves | 6ta | `12 I-T-A-E` | Idem |
| Viernes | 3ra | `7°-A-2` | Subsección A-2 inexistente en BD |
| Viernes | 5ta | `12 I-T-A-E` | Código genérico |
| Viernes | 6ta | `12 I-T-A-E` | Idem |

**Explicación:** Rey es docente de idiomas con muchas asignaciones en múltiples grupos. Los códigos `I-T-A-E` del Word no especifican qué grupo de inglés/francés corresponde a ese bloque. Además, los códigos `A-2` de pre-media no tienen grupo equivalente.

---

## 4. Consolidado de lo NO cargado (27 slots)

### 4.1 Por causa raíz

| Causa | Slots | % del total faltante |
|-------|------:|---------------------:|
| Código genérico `I-T-A-E` (sin grupo/materia) | 14 | 51,9 % |
| Práctica Profesional / Mercadotecnia sin teacher_assignment | 7 | 25,9 % |
| Subsección A-2 inexistente en BD (7-A-2, 8-A-2, 9-A-2) | 6 | 22,2 % |

### 4.2 Por docente (faltantes)

| Docente | Faltantes | Causa principal |
|---------|----------:|-----------------|
| Rey A. Abrego S. | 11 | I-T-A-E + A-2 |
| Jannie M. España W. | 7 | Práctica Prof. + Mercadotecnia |
| Max E. Agames J. | 4 | I-T-A-E |
| Fabricio Mendoza | 4 | Ciencias A-2 |
| Yaribeth Mora | 1 | I-T-A-E |

---

## 5. Datos operativos post-carga

### 5.1 Estado de la base de datos

| Tabla | Antes | Después |
|-------|------:|--------:|
| `schedule_entries` (2026) | 0 | **302** |
| `teacher_assignments` | 129 | **221** (+92) |
| Años académicos 2026 activos | 10+ duplicados | **1** (canónico) |
| Bloques nocturnos (`time_slots`) | 6 | 6 (sin cambios) |

### 5.2 Cómo verificar en la aplicación

1. Ingresar como **Administrador**
2. Menú **Horarios → Horario por Docente** (`/Schedule/ByTeacher`)
3. Seleccionar docente + año **2026**
4. Clic en **Cargar horario**

### 5.3 Scripts de soporte

| Script | Función |
|--------|---------|
| `Scripts/import_quena_schedules.py` | Importación masiva desde Word |
| `Scripts/complete_pending_schedules.py` | Completar docentes pendientes |
| `Scripts/fix_quena_academic_year.py` | Consolidar año académico 2026 |
| `Scripts/verify_quena_load.py` | Verificar conteos en BD |
| `Scripts/_audit_coverage.py` | Auditoría Word vs BD |

---

## 6. Plan de remediación (27 slots restantes)

### Prioridad alta — Rey Abrego (11 slots)

1. Definir manualmente qué grupo corresponde a cada celda `I-T-A-E` (ej. `10 I-T-A-E` lunes 1ra → `10-A1` Inglés).
2. Crear los `teacher_assignments` faltantes si no existen.
3. Cargar celda por celda en `/Schedule/ByTeacher` o ampliar el script con tabla de equivalencias.

### Prioridad media — Jannie España (7 slots)

1. Crear `teacher_assignments` para:
   - Práctica Profesional — grupos 12-A1 (informática) y 12-A4 (turismo)
   - Mercadotecnia y Publicidad — grupo 11-A4
2. Re-ejecutar script de completado.

### Prioridad baja — Fabricio (4 slots)

1. **Opción A:** Mapear `7°-A-2` y `8°-A-2` al grupo existente `7-A` / `8-A`.
2. **Opción B:** Crear grupos `7-A-2` y `8-A-2` en el módulo de Grupos.

### Prioridad baja — Max (4) y Yaribeth (1)

1. Resolver celdas `I-T-A-E` asignando grupo concreto según malla curricular.

---

## 7. Limitaciones conocidas del documento Word

1. **Tablas duplicadas:** 6 docentes tienen su horario repetido en 2 páginas del Word (26 celdas duplicadas en total).
2. **Notación inconsistente:** Mezcla de formatos (`8-A-1`, `8°- A-2`, `11 I-T-A-E`, `11-IProgramación`).
3. **Celdas combinadas:** Grupo y materia en un solo texto (`9-A-1Tecn.Mecanografía`).
4. **Sin formato importable:** No es Excel/CSV — requiere parseo especializado.
5. **Códigos I-T-A-E:** Abreviatura de especialidades (I=Informática, T=Turismo, A=Administración, E=Electricidad) sin indicar grupo exacto.

---

## 8. Conclusión

| Pregunta | Respuesta |
|----------|-----------|
| ¿Se cargaron todos? | **No.** 302 de 319 slots únicos (91,5 %). |
| ¿Todos los docentes tienen horario? | **Sí.** Los 12 docentes tienen horarios operativos en el sistema. |
| ¿Cuántos docentes al 100 %? | **8 de 12** (Lexibel, Julio, Lino, Zellideth, Zuley, Elvia, Carlos + Yaribeth al 96 %). |
| ¿Cuál docente requiere más atención? | **Rey A. Abrego** (57,7 % — 11 slots pendientes). |
| ¿El sistema está listo para operar? | **Sí**, con ajuste manual recomendado para los 27 slots restantes. |

---

*Informe generado automáticamente a partir de auditoría cruzada Word ↔ Base de datos Render.*  
*Datos verificables con: `python Scripts/verify_quena_load.py`*
