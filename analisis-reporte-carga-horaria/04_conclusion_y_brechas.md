# 04 — Conclusión y brechas

Este documento decide, componente por componente, si Celosan puede generar **hoy** el reporte de carga horaria curricular descrito en la solicitud.

Criterio usado: existencia **actual** de datos y relaciones, no la posibilidad futura de programarlo.

---

## Componentes del reporte solicitado

### 1. Encabezado del bachillerato

⚠️ PARCIALMENTE POSIBLE

Existe `specialties.name` con `BACHILLER EN ELECTRICIDAD`.  
No está el texto exacto “BACHILLER INDUSTRIAL EN ELECTRICIDAD”.  
El encabezado de carrera se puede obtener; el nombre oficial del documento de referencia no está literalmente almacenado.

### 2. Áreas

✅ POSIBLE ACTUALMENTE

Tabla `area` con `HUMANISTICA`, `CIENTÍFICA` y `TECNOLÓGICA`.  
Modelo: `SchoolManager/Models/Area.cs`.  
Servicio: `AreaService.GetAllAsync`.

### 3. Asignaturas

✅ POSIBLE ACTUALMENTE

Tabla `subjects` (83 materias). Los nombres coinciden con el ejemplo (Español, Inglés, Geografía de Panamá, Ética, Bellas Artes, Historia, Cívica, Lógica/Filosofía, Matemática, Educación Física, Ciencias, Química, Física, Tecnología de la Información, Dibujo, Talleres, Seguridad Industrial, Gestión Empresarial).

### 4. Grados

✅ POSIBLE ACTUALMENTE

`grade_levels` contiene `10`, `11` y `12`.  
`subject_assignments` ya cruza cada materia de Electricidad con esos grados.

### 5. B1/B2

❌ NO POSIBLE ACTUALMENTE

No hay entidad, campo ni valor de dato B1/B2 en grupos, grados, trimestres, `level_name` ni materias (0 coincidencias).  
Los periodos del sistema son `1T`, `2T`, `3T`.  
Los “Bloque 1 / Bloque 2” de `time_slots` son franjas diarias de clase, no mitades académicas del grado.

### 6. Carga horaria

❌ NO POSIBLE ACTUALMENTE

No hay campo de horas en `subjects`, `subject_assignments` ni `curriculum_subjects`.  
`curriculum_subjects.credits` existe pero vale `1.00` en las 285 filas; no representa horas.  
El horario (`schedule_entries`) es operativo e incompleto (Electricidad 10°: 3 de 14 materias).

### 7. Total por asignatura

❌ NO POSIBLE ACTUALMENTE

No hay horas que sumar por materia a lo largo de 10/11/12 ni de B1/B2.  
Sí se podría contar **en cuántos grados** aparece la materia; eso no es el total de horas.

### 8. Subtotal Humanística

⚠️ PARCIALMENTE POSIBLE

**Conteo de asignaturas** del área: sí (Electricidad 10° = 5, 11° = 4, 12° = 5).  
**Subtotal de horas**: no. Falta el sumando.

### 9. Subtotal Científica

⚠️ PARCIALMENTE POSIBLE

Conteo sí (3 materias por grado en Electricidad).  
Horas no.

### 10. Subtotal Tecnológica

⚠️ PARCIALMENTE POSIBLE

Conteo sí (6 / 5 / 5 materias en 10 / 11 / 12 para Electricidad).  
Horas no.

### 11. Total de horas por nivel

❌ NO POSIBLE ACTUALMENTE

No hay horas por grado.  
El **número de asignaturas por grado** sí es calculable (Electricidad: 14, 12 y 13). Eso no sustituye el total de horas.

### 12. Total general

❌ NO POSIBLE ACTUALMENTE (horas)

No hay total de horas de la carrera.  
Un total de **asignaturas distintas** o de **celdas materia×grado** sí sería calculable; no es el total que pide el documento de referencia.

### 13. Total de asignaturas

✅ POSIBLE ACTUALMENTE

`COUNT(DISTINCT subject_id)` sobre `subject_assignments` filtrado por especialidad (y opcionalmente por grado/área).  
No hay un reporte que ya lo imprima, pero el dato existe.

---

## Brechas encontradas

Solo lo que falta para generar el documento completo. Sin propuestas de implementación.

### BRECHA 1

No existe carga horaria asociada a asignatura / grado / bloque.

EVIDENCIA:  
Columnas de `subjects`, `subject_assignments` y `curriculum_subjects` inspeccionadas. Único numérico de “carga” en malla: `credits`, siempre `1.00`. Catálogo `information_schema` sin columnas `hour`/`hora`/`carga` en tablas académicas.

IMPACTO:  
No puede calcularse el total de horas, ni las celdas del reporte, ni los subtotales de horas por área o nivel.

### BRECHA 2

No existe el concepto académico B1/B2.

EVIDENCIA:  
Búsqueda sin resultados en `groups.name`, `grade_levels.name`, `trimester.name`, `curriculum_subjects.level_name` y `subjects.name`. Periodos reales: `1T`, `2T`, `3T`. `time_slots.name` “Bloque 1/2” es horario diario.

IMPACTO:  
No se pueden construir las seis columnas 10° B1 … 12° B2 del documento de referencia.

### BRECHA 3

`credits` de la malla no está usado como horas.

EVIDENCIA:  
285/285 `curriculum_subjects.credits = 1.00`. El formulario de `Views/CurriculumTracks/Index.cshtml` carga créditos con valor por defecto 1.

IMPACTO:  
No se puede reinterpretar la malla modular como carga horaria sin falsear el documento.

### BRECHA 4

La malla curricular no está partida por bachillerato ni por área.

EVIDENCIA:  
`curriculum_subjects` no tiene `specialty_id` ni `area_id`. Hay una sola fila en `curriculum_tracks`. El área de `subjects."AreaId"` está NULL en 83/83.

IMPACTO:  
La malla modular no es una fuente fiel del documento por carrera. La fuente usable es `subject_assignments`, que tampoco tiene horas ni B1/B2.

### BRECHA 5

El horario docente no sustituye el plan de horas.

EVIDENCIA:  
Para `BACHILLER EN ELECTRICIDAD` grado `10` solo hay horario en 3 materias (Ciencias Naturales, Dibujo I, Tecnología de la Información), con 0.83–1.67 horas reloj. El resto de la malla de 10° no tiene `schedule_entries`.

IMPACTO:  
Derivar horas desde el horario produciría un documento incompleto y de naturaleza distinta (ocupación semanal vs carga curricular).

### BRECHA 6

No existe consulta ni reporte que arme esta grilla.

EVIDENCIA:  
No hay servicio/controlador de malla de horas. `CelosanReportService` cubre prematrícula y cupos. `CurriculumTracksController` es mantenimiento. `AprobadosReprobadosService.ExportarAExcelAsync` no está implementado.

IMPACTO:  
Aunque se pidiera solo el inventario de materias (sin horas), el sistema **hoy no lo emite** como reporte. Eso es un hueco de backend, no de catálogo.

### BRECHA 7

El nombre oficial del documento de referencia no está almacenado de forma literal.

EVIDENCIA:  
`specialties.name` = `BACHILLER EN ELECTRICIDAD`, no `BACHILLER INDUSTRIAL EN ELECTRICIDAD`. `description` vacío.

IMPACTO:  
Menor. El encabezado se puede obtener con un nombre cercano, no idéntico.

---

==================================================
DECISIÓN FINAL
==================================================

ES PARCIALMENTE POSIBLE GENERAR EL REPORTE ACTUALMENTE.

JUSTIFICACIÓN:

1. Celosan ya tiene especialidades equivalentes a bachillerato, incluida Electricidad.  
2. Ya existen las tres áreas Humanística, Científica y Tecnológica.  
3. Ya existe el catálogo de asignaturas y coincide con el documento de referencia.  
4. Ya existen los grados 10, 11 y 12 y la relación materia–grado–área–carrera en `subject_assignments`.  
5. Por eso se puede reconstruir el esqueleto del reporte: áreas, materias y en qué grado se dictan, más conteos.  
6. No existe B1/B2 como periodo de la carrera; los periodos reales son tres trimestres.  
7. No existe el número de horas por asignatura, grado o bloque; `credits` vale 1 en toda la malla.  
8. El horario diario no cubre el plan y no es carga curricular.  
9. No hay consulta ni PDF/Excel de esta malla; QuestPDF existe para otros reportes.  
10. Sin horas y sin B1/B2, el documento de referencia no se puede generar completo: solo una versión parcial de inventario curricular, no el reporte de carga horaria.
