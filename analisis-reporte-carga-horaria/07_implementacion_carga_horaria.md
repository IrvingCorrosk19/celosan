# 07 — Implementación de carga horaria curricular

Fecha: 2026-09-05  
Entorno: **local únicamente** (`localhost` / `::1:5432`, base `schoolmanager_daqf`)  
Producción / Render: **no se tocó**

---

## 1. Verificación previa (antes de migrar)

| Comprobación | Resultado |
|---|---|
| Servidor | PostgreSQL `::1` puerto `5432` |
| Base | `schoolmanager_daqf` |
| `DefaultConnection` | `Host=localhost` (Render comentado) |
| Última migración aplicada | `20260618235045_OptimizeCelosanDatabaseQueries` |
| Pendiente | `20260905144243_AddCurriculumLoadStructure` |
| `curriculum_blocks` | no existía |
| `curriculum_load_subjects` | no existía |
| `curriculum_load_hours` | no existía |
| `users.can_edit_curriculum_load` | no existía |

---

## 2. Respaldo

No hay un job automático de respaldo local. Se usó `pg_dump` (formato custom), el mismo mecanismo de `Scripts/BackupRenderDatabase.ps1` pero apuntando a **localhost**.

Archivo:

`SchoolManager/Backups/schoolmanager_daqf_pre_AddCurriculumLoadStructure_20260905_094714.dump`

Tamaño: 535970 bytes.

Restauración (solo si hiciera falta):

```
pg_restore -h localhost -U postgres -d schoolmanager_daqf --clean --if-exists "ruta\al\dump"
```

---

## 3. Migración

Comando:

```
dotnet ef database update AddCurriculumLoadStructure
```

Resultado: **Aplicada**

`__EFMigrationsHistory` contiene `20260905144243_AddCurriculumLoadStructure`.

No se ejecutó SQL extra fuera de esa migración.

---

## 4–8. Verificación de datos

### Tablas

| Tabla / columna | Estado |
|---|---|
| `curriculum_blocks` | OK |
| `curriculum_load_subjects` | OK |
| `curriculum_load_hours` | OK |
| `users.can_edit_curriculum_load` | OK (boolean, default `false`) |

### Bloques

Exactamente 2:

| code | name | sort_order |
|---|---|---|
| B1 | Bloque 1 | 1 |
| B2 | Bloque 2 | 2 |

### `curriculum_load_subjects`

| Programa | Esperado | Encontrado |
|---|---:|---:|
| PRE-MEDIA | 50 | 50 |
| ELECTRICIDAD | 39 | 39 |
| AUTOTRÓNICA | 36 | 36 |
| INFORMÁTICA | 40 | 40 |
| TURISMO | 40 | 40 |
| **TOTAL** | **205** | **205** |

PRE-MEDIA solo 7 / 8 / 9 (17+17+16). Combinaciones PRE-MEDIA 10°/11° en la malla nueva: **0**.

### Horas

Tras la migración: **0** filas.  
Tras la prueba controlada de admin (guardar y luego borrar): **0** filas.

### Flag secretaria

334 usuarios. `can_edit_curriculum_load = true`: **0**. `false`: **334**. Secretarias con edición: **0**.

---

## 9–13. Pruebas HTTP (`http://localhost:5172`)

Aplicación levantada con perfil `http` (Development).

### Login

`GET /Auth/Login` → 200  
`admin@local.com` / `Admin123!` → OK (admin)  
`faridalain30@gmail.com` / `123456` → OK (teacher)

### TeacherGradebook

`GET /TeacherGradebook/Index` → 200

Pestañas presentes (por `id`):

1. `notas-tab` Registrar Notas  
2. `resumen-tab` Promedios Finales  
3. `asistencia-tab` Asistencias  
4. `disciplina-tab` Disciplina  
5. `consejeria-tab` Consejería  
6. `cargaHoraria-tab` Carga Horaria  

Selector `#chProgram` presente. `canEdit: false`. Sin inputs `ch-hour-input`.

`GET GetAcademicPrograms` → 200. Este docente solo tiene oferta en **AUTOTRÓNICA** (coincide con `teacher_assignments`).

Reporte AUTOTRÓNICA: tipo MEDIA, grados 10/11/12, áreas CIENTÍFICA, HUMANISTICA, TECNOLÓGICA, B1/B2 nulos (UI: —).

Los otros cuatro programas se comprobaron con **admin** (el teacher de prueba no los imparte):

| Programa | Tipo | Grados | Áreas |
|---|---|---|---|
| PRE-MEDIA | PREMEDIA | 7,8,9 | las tres |
| ELECTRICIDAD | MEDIA | 10,11,12 | las tres |
| AUTOTRÓNICA | MEDIA | 10,11,12 | las tres |
| INFORMÁTICA | MEDIA | 10,11,12 | las tres |
| TURISMO | MEDIA | 10,11,12 | las tres |

`totalSubjects = 0` porque no hay horas > 0 (correcto).

### Seguridad teacher

`POST /CurriculumLoad/SaveHours` y `SetActive` → **403 Acceso Denegado** (página de autorización). No se insertaron horas.

### Admin

`GET /CurriculumLoad/Index` → 200, `canEdit: true`.  
Los 5 programas visibles.

Prueba controlada (luego revertida a sin configurar):

- Programa: PRE-MEDIA  
- Grado: 7  
- Área: HUMANISTICA  
- Materia: ESPAÑOL  
- Id: `20da19ce-aaff-4aa3-9ca3-1daec4da370c`  
- Pasos: B1=1 (ok) → B1=0 B2=0 (ok, distinto de vacío) → ambos null (borra filas)

Horas finales: **0**.

### Secretaria

No se pudo iniciar sesión con `123456` ni `Admin123!` en las cuentas existentes.  
No se cambió ninguna contraseña ni se activó el flag.

Lo verificado: todas las secretarias quedaron en `false`. El POST de teacher sí fue rechazado por backend.

### Superadmin menú

No se autenticó `admin@correo.com` ni `superadmin@schoolmanager.com` con `Admin123!`.

Revisión de código: `_Menu.cshtml` filtra subítems por `RequiredRoles`. El único subítem de Administración que incluye `superadmin` es **Carga Horaria Curricular**. No se filtraron mal otros ítems de admin-only (`Administrar Usuarios`, `Catálogo Académico`, `Asignar Docentes`, etc.).

---

## 14. Regresión

| Área | Resultado | Nota |
|---|---|---|
| Login | OK | 200 + cookie |
| TeacherGradebook | OK | 200, 6 pestañas |
| Pestaña Notas / IDs existentes | OK | no se invocó `GuardarNotasTemp` (evitar tocar notas reales) |
| 1T / 2T / 3T | no alterados | la malla no usa trimestres |
| Asistencia / Disciplina / Consejería | pestañas presentes | no se guardaron registros |

---

## 15. Efecto colateral al arrancar (no es la migración)

El primer `dotnet run` se lanzó **sin** perfil `http` y escuchó en `:5000` (Production). `Program.cs` ejecutó `EnsureDefaultAcademicYearForSchoolAsync` y `EnsureDefaultTimeSlots`. Eso **no** forma parte de `AddCurriculumLoadStructure`.

Conteos actuales (después de ese arranque): `academic_years` = 38, `time_slots` = 142.

El respaldo previo a la migración permite comparar si hace falta.

---

## Decisión de este corte

La estructura curricular opera. **No se cargaron horas oficiales.**
