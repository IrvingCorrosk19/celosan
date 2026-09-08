# 01 — Viabilidad general del reporte de carga horaria curricular

RESULTADO: PARCIALMENTE POSIBLE

## Qué reporte se está evaluando

Se evalúa si Celosan puede generar **hoy** un documento curricular de carga horaria similar a una malla tipo:

**BACHILLER INDUSTRIAL EN ELECTRICIDAD**

con:

- áreas académicas (Humanística, Científica, Tecnológica);
- asignaturas por área;
- columnas de grados 10°, 11° y 12°;
- subdivisiones **B1** y **B2** en cada grado;
- horas por asignatura en cada grado/bloque;
- subtotales por área;
- totales de horas y de asignaturas.

La pregunta no es “¿se podría programar?”. Es: **¿Celosan posee actualmente los datos y las relaciones para producir ese documento?**

La evidencia proviene del código del proyecto (`SchoolManager`) y de consultas de **solo lectura** a la base local restaurada `schoolmanager_daqf` (dump del 2026-09-01).

---

## Qué encontró Celosan

Celosan **sí tiene** un modelo académico operativo de escuela nocturna:

- especialidades (bachilleratos);
- áreas académicas;
- catálogo de asignaturas;
- grados 7 a 12 (incluye 10, 11 y 12);
- asignación operativa de materia a especialidad + área + grado + grupo (`subject_assignments`);
- una malla modular (`curriculum_tracks` / `curriculum_subjects`) con créditos y orden de módulo;
- periodos tipo trimestre (`1T`, `2T`, `3T`);
- horarios diarios (`time_slots` + `schedule_entries`).

Eso permite armar **una matriz de asignaturas por bachillerato, área y grado**.

Celosan **no tiene** el dato nuclear del reporte pedido: **horas curriculares por asignatura × grado × B1/B2**. Tampoco modela B1/B2 como periodo académico de la carrera.

Por eso el reporte **no se puede generar completo**. Sí se puede generar una **parte estructural** (encabezado, áreas, materias, grados, conteos).

---

## Qué información existe

| Información | Dónde está | Estado en datos reales |
|---|---|---|
| Bachillerato / carrera | Tabla `specialties` | Existe `BACHILLER EN ELECTRICIDAD` (además Autotrónica, Informática, Turismo y PRE-MEDIA) |
| Área académica | Tabla `area` | Existen exactamente `HUMANISTICA`, `CIENTÍFICA` y `TECNOLÓGICA` |
| Relación área–materia–grado–carrera | `subject_assignments` (`specialty_id`, `area_id`, `subject_id`, `grade_level_id`) | Poblada. Electricidad 10° tiene 14 materias distintas (5 humanísticas, 3 científicas, 6 tecnológicas) |
| Asignaturas | Tabla `subjects` | 83 materias. Los nombres coinciden con el ejemplo (Español, Inglés, Geografía de Panamá, Ética, Bellas Artes, Matemática, Física, Química, Talleres, etc.) |
| Grados 10 / 11 / 12 | Tabla `grade_levels` | Existen como `10`, `11`, `12` |
| Plan / malla | `curriculum_tracks` + `curriculum_subjects` | 1 malla activa (“Malla Modular Nocturna CELOSAM 2026”) y 285 filas de materias curriculares |
| Conteo de asignaturas | Calculable desde `subject_assignments` | Sí, por especialidad, área y grado |
| Infraestructura PDF | QuestPDF en otros reportes | Existe, pero no hay un reporte de esta malla |

Ejemplo real (solo lectura) para **BACHILLER EN ELECTRICIDAD**:

- 10° HUMANISTICA: Español, Inglés, Geografía de Panamá, Ética/Moral/Valores, Bellas Artes
- 10° CIENTÍFICA: Matemática, Ciencias Naturales, Educación Física y Salud Integral
- 10° TECNOLÓGICA: Dibujo I, Tecnología de la Información, Seguridad Industrial, Talleres I–III

Eso cubre el **esqueleto** del documento de referencia.

---

## Qué información no existe

1. **Horas curriculares por asignatura y grado**  
   No hay columna de horas en `subjects`, `subject_assignments` ni `curriculum_subjects`.  
   El único campo numérico de “carga” en la malla es `credits`, y **las 285 filas tienen `credits = 1.00`**. Eso no es carga horaria.

2. **B1 / B2 como columnas académicas del grado**  
   No hay entidad, catálogo ni valor de dato llamado B1/B2.  
   Los periodos reales son trimestres `1T`, `2T`, `3T`.  
   Los “Bloque 1 / Bloque 2” de `time_slots` son **franjas diarias de horario** (p. ej. 18:10–19:00), no semestres del plan de estudio.

3. **Horas oficiales recuperables desde el horario**  
   `schedule_entries` existe, pero está incompleto y es operativo. Para Electricidad 10° solo 3 de 14 materias tienen horario; las “horas reloj” calculadas son 0.83–1.67, no una carga curricular oficial.

4. **Área en el catálogo de materias**  
   `subjects."AreaId"` está **NULL en las 83 materias**. El área vive en la asignación operativa, no en la materia maestra.

5. **Malla por bachillerato**  
   `curriculum_subjects` no tiene `specialty_id`. Hay una sola malla escolar, no un plan distinto por Electricidad / Informática / Turismo.

6. **Reporte o consulta actual que arme esta grilla**  
   No existe un controlador, servicio ni vista que genere este documento.

---

## Qué partes podrían generarse hoy

Con los datos actuales se podría listar, para una especialidad (p. ej. Electricidad):

- nombre del bachillerato;
- las tres áreas;
- las asignaturas de cada área;
- en qué grado (10, 11, 12) aparece cada asignatura;
- cantidad de asignaturas por área y por grado;
- total de asignaturas distintas de la carrera.

Eso sería un **inventario curricular por grado**, no el documento de carga horaria pedido.

---

## Qué partes no podrían generarse hoy

- columnas **10° B1, 10° B2, 11° B1, 11° B2, 12° B1, 12° B2**;
- número de horas en cada celda;
- total de horas por asignatura;
- subtotal de horas Humanística / Científica / Tecnológica;
- total de horas por nivel/grado;
- total de horas de la carrera.

Sin esas celdas, el documento de referencia **no se puede completar**.

---

## Por qué

Celosan modela **qué se imparte, en qué carrera, área, grado y grupo**.  
El reporte pide **cuántas horas oficiales tiene cada materia en cada mitad del grado (B1/B2)**.

Esos dos mundos no coinciden hoy:

- hay catálogo y relaciones de oferta académica;
- no hay carga horaria curricular persistida;
- no hay B1/B2 académico;
- el campo `credits` está homogeneizado en 1 y no sustituye horas;
- el horario diario no cubre el plan ni representa horas de malla.

Por eso la viabilidad es **parcial**: la estructura de filas (áreas y asignaturas) y las columnas de grado existen; las columnas B1/B2 y todas las cifras de horas **no**.

---

## Tabla de elementos requeridos

| Elemento requerido | Existe en Celosan | Evidencia | Estado |
|---|---|---|---|
| Bachillerato/carrera | Sí | Tabla `specialties`; modelo `Specialty.cs`; dato `BACHILLER EN ELECTRICIDAD` | ✅ |
| Área académica | Sí | Tabla `area`; modelo `Area.cs`; datos HUMANISTICA, CIENTÍFICA, TECNOLÓGICA | ✅ |
| Asignatura | Sí | Tabla `subjects`; modelo `Subject.cs`; 83 materias, nombres alineados al ejemplo | ✅ |
| Grado | Sí | Tabla `grade_levels`; modelo `GradeLevel.cs`; valores `10`, `11`, `12` | ✅ |
| B1/B2 | No | Cero coincidencias en groups, grade_levels, trimester, level_name y subjects. Periodos reales: `1T/2T/3T`. `time_slots.Name` “Bloque 1/2” es horario diario | ❌ |
| Carga horaria | No | Ninguna columna `hours`/`hora`/`carga` en tablas académicas. `credits` = 1.00 en 285 filas | ❌ |
| Horas por asignatura | No | `Subject` y `CurriculumSubject` no tienen campo de horas. Horario operativo incompleto | ❌ |
| Relación materia-grado | Sí | `subject_assignments.grade_level_id` y `curriculum_subjects.grade_level_id` | ✅ |
| Relación materia-área | Sí (operativa) | `subject_assignments.area_id`. No en `subjects` (AreaId NULL) ni en `curriculum_subjects` | ⚠️ |
| Relación materia-plan de estudio | Parcial | `curriculum_subjects` liga materia+grado a una malla única, **sin especialidad** | ⚠️ |
| Subtotales (horas por área) | No calculable | Falta el dato de horas. El **conteo** de materias por área sí es calculable | ❌ / ⚠️ |
| Total por nivel (horas) | No calculable | No hay horas por grado. El conteo de materias por grado sí es calculable | ❌ / ⚠️ |
| Total de asignaturas | Calculable | `COUNT(DISTINCT subject_id)` sobre `subject_assignments` filtrado por especialidad | ✅ |

Leyenda: ✅ existe y está poblado · ⚠️ existe a medias o no en el lugar canónico · ❌ no existe el dato requerido
