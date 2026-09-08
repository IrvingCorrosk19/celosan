# Supuestos provisionales — Carga horaria curricular nocturna

**Estado:** pendiente de ejecución. Los scripts existen y **no se han aplicado**.  
**Pantalla:** `/CurriculumLoad/Index`  
**Escuela:** Centro de Educación Laboral Oficial San Miguelito (`6e42399f-6f17-4585-b92e-fa4fff02cb65`)

---

## 1. B1 y B2 no estaban definidos funcionalmente

En Celosan no existía una definición institucional persistida de qué son B1 y B2.

Quedó demostrado que **no** son:

- grupos de estudiantes;
- los trimestres `1T`, `2T` o `3T`;
- los bloques de `time_slots` (Bloque 1–8 de 50 minutos).

---

## 2. Interpretación provisional

Hasta recibir otra definición institucional:

**B1 y B2 se tratan como divisiones del plan de estudios nocturno** (columnas de horas oficiales por materia y grado).

Viven en `curriculum_blocks` + `curriculum_load_hours`.  
No se relacionan con A, A1, A2, A3 ni A4.

---

## 3. No existen grupos B1/B2 en la base

`groups.name` no contiene B1 ni B2 como secciones.  
La unique curricular es `(school_id, specialty_id, grade_level_id, area_id, subject_id)`: **sin grupo**.

---

## 4. Grupos reales (operativos, fuera de esta tabla)

Deben conservarse y **no** entrar como columnas de la malla:

| Programa | Grupos oficiales de consulta |
|---|---|
| Premedia | `7-A`, `8-A`, `9-A` |
| Informática | `10-A1`, `11-A1`, `12-A1` |
| Autotrónica | `10-A2`, `11-A2`, `12-A2` |
| Electricidad | `10-A3`, `11-A3`, `12-A3` |
| Turismo | `10-A4`, `11-A4`, `12-A4` |

También existen `A`, `A1`–`A4`, `10-A`, `11-A`, `12-A`. No alteran la estructura de esta tabla.

---

## 5. Las horas no existían previamente

La investigación local, Git, seeds, respaldos, créditos y Schedule **no** contenían horas oficiales B1/B2.

Solo había dos filas de prueba en `curriculum_load_hours`:

| Celda | Valor de prueba | Destino MEDUCA |
|---|---|---|
| Premedia 7.º Ciencias Naturales B1 | `0.00` (`ae8c2ddc-…`, CLS `1fb32f05-…`) | B1 vacío; **B2 = 4** |
| Autotrónica 10.º TI B1 | `2.00` (`b1165ed7-…`, CLS `e4f8c4d0-…`) | B1 vacío; **B2 = 4** |

---

## 6. Fuente inicial autorizada

Las cinco hojas MEDUCA (imágenes) son la **fuente inicial** de las horas.

No se usaron créditos, `schedule_entries` ni duración de `time_slots`.  
Celda vacía en la hoja = **sin fila** en `curriculum_load_hours` (no se guarda 0).

---

## 7. Schedule no es el plan oficial

`schedule_entries` y el documento Quena son **horas programadas** (día + franja nocturna).  
No definen B1/B2 curriculares.

---

## 8. Preguntas pendientes de confirmación institucional

1. Premedia tecnológica: ¿`FDC 1`/`MET`, `FDC 2`/`D.L.`, `MCA 1`/`MCA 2` corresponden a B1=2 y B2=2? **Supuesto provisional aplicado.**
2. Electricidad: el subtotal tecnológico **impreso 46** no coincide con la suma de celdas **50**. Se cargó celda por celda (total general 120). ¿Se confirma?
3. Informática: se cargó `ESPAÑOL (LENGUAJE Y COMUNICACIÓN)` y `ÉTICA Y VALORES`. Las filas cortas `ESPAÑOL` y `ÉTICA MORAL…` quedan **sin horas**.
4. Premedia: se cargó `VAL. ÉTICOS / REL. HUMANAS`. `ÉTICA MORAL…` de 7.º/8.º queda **sin horas**.
5. Autotrónica: `Taller II` de 11.º permanece **sin horas** (MEDUCA solo lo pone en 12.º).
6. Electricidad: `Taller III` de 10.º permanece **sin horas** (celda vacía en la hoja).
7. ¿El plan debe versionarse por año académico? Hoy no hay relación segura; no se añadió selector de año.

---

## 9. Cómo revertir

Solo en la base **local**, tras una ejecución previa del apply:

```
psql -h localhost -U postgres -d schoolmanager_daqf -f SchoolManager/Scripts/postgres/rollback_meduca_curriculum_hours.sql
```

El rollback:

- borra las horas de esta escuela;
- restaura las dos filas de prueba;
- devuelve las 7 filas de `curriculum_load_subjects` a su grado anterior (GUID verificado);
- no toca otros módulos.

---

## Correspondencias y movimientos incluidos en el apply

**Grados (solo `curriculum_load_subjects`, no `subject_assignments`):**

| Plan | Materia | De | A |
|---|---|---|---|
| Turismo | Historia de Panamá | 11 | 10 |
| Turismo | Geografía de Panamá | 10 | 11 |
| Turismo | Geografía Turística de Panamá | 12 | 11 |
| Turismo | Bellas Artes | 10 | 11 |
| Turismo | Geografía Turística del Mundo | 11 | 12 |
| Autotrónica | Taller III (12.º) | 12 | 11 |
| Autotrónica | Taller V (12.º) | 12 | 11 |

**Premedia tecnológica (provisional):** 7.º FDC 1 B1=2 / MET B2=2; 8.º FDC 2 B1=2 / D.L. B2=2; 9.º MCA 1 B1=2 / MCA 2 B2=2.

**Totales esperados tras el apply:** Premedia 99, Informática 120, Turismo 120, Autotrónica 119, Electricidad 120. **General 578.**
