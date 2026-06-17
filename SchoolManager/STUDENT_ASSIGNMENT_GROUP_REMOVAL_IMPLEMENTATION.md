# Implementación controlada - Quitar matrícula/grupo en StudentAssignment

Fecha: 2026-06-17  
Módulo: `/StudentAssignment/Index`  
Modo: implementación local posterior a backup completo.  
Estado: compilado correctamente, sin commit y sin push.

## 1. Backup previo

Documento generado:

`BACKUP_STUDENT_ASSIGNMENT_BEFORE_CHANGES.md`

Backups locales:

- Proyecto: `C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935\SchoolManager_project_20260617_172935.tar.gz`
- Base de datos: `C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935\schoolmanager_daqf_full_20260617_172935.dump`

Ambos fueron verificados antes de modificar código.

## 2. Archivos modificados

- `Controllers/StudentAssignmentController.cs`
- `Views/StudentAssignment/Index.cshtml`

Archivos/documentos nuevos:

- `BACKUP_STUDENT_ASSIGNMENT_BEFORE_CHANGES.md`
- `STUDENT_ASSIGNMENT_GROUP_REMOVAL_IMPLEMENTATION.md`

## 3. API implementada

Endpoint nuevo:

`POST /StudentAssignment/RemoveEnrollment`

Parámetros:

- `studentAssignmentId`
- `removeActiveSubjects`

Validaciones implementadas:

- `studentAssignmentId` no vacío.
- Sesión válida.
- Rol permitido: `admin`, `secretaria`, `director`.
- Matrícula existente.
- Matrícula activa.
- Matrícula perteneciente a la misma escuela del usuario autenticado.

El controlador de `StudentAssignment` ahora permite acceso a:

- `admin`
- `secretaria`
- `director`

No se permite acceso a `teacher`, `student` ni `parent`.

## 4. Reglas de negocio implementadas

### Grupo sin materias activas

Solo se inactiva la matrícula:

- `student_assignments.is_active = false`
- `student_assignments.end_date = DateTime.UtcNow`

No se eliminan registros físicamente.

### Grupo con materias activas

Primera llamada sin confirmación (`removeActiveSubjects=false`):

- No modifica datos.
- Retorna `code = HAS_ACTIVE_SUBJECTS`.
- Retorna cantidad de materias activas.

Si el usuario confirma (`removeActiveSubjects=true`):

- Inactiva materias asociadas:
  - `student_subject_assignments.is_active = false`
  - `student_subject_assignments.status = "Inactive"`
  - `student_subject_assignments.end_date = DateTime.UtcNow`
- Luego inactiva la matrícula:
  - `student_assignments.is_active = false`
  - `student_assignments.end_date = DateTime.UtcNow`

Todo es soft delete.

## 5. GetGradeGroupByStudent actualizado

`GET /StudentAssignment/GetGradeGroupByStudent/{studentId}` ahora devuelve objetos enriquecidos:

- `studentAssignmentId`
- `gradeId`
- `groupId`
- `shiftId`
- `academicYearId`
- `gradeName`
- `groupName`
- `shiftName`
- `activeSubjectsCount`
- `enrollmentType`

Esto permite renderizar acciones por matrícula individual y quitar solo el grupo seleccionado.

## 6. Modal actualizado

En la sección "Asignación Actual", cada grupo se renderiza con:

- Grado y grupo.
- Badge de jornada.
- Badge de materias activas cuando aplica.
- Botón `Quitar`.

Ejemplo esperado:

- `11 - 11-A` `[Noche]` `[Quitar]`
- `10 - 10-A` `[Noche]` `[Quitar]`
- `11 - 11-A4` `[Noche]` `[Quitar]`

## 7. AJAX implementado

Nuevo flujo JavaScript:

1. Usuario presiona `Quitar`.
2. Se muestra confirmación.
3. Se llama `POST /StudentAssignment/RemoveEnrollment`.
4. Si el backend responde `HAS_ACTIVE_SUBJECTS`, se muestra segunda confirmación:

   "Este grupo posee materias activas. ¿Desea eliminar también las materias asociadas?"

5. Si confirma, se reintenta con `removeActiveSubjects=true`.
6. Al completar, se actualizan sin refrescar página:
   - Modal de matrículas actuales.
   - Celda de grupos en la tabla principal.
   - Lista de materias del modal.
   - Celda de materias de la tabla principal.
   - Datos de filtros (`data-enrollments`) de la fila.

No se usa `location.reload()` en el flujo de quitar grupo.

## 8. Auditoría

Se registra una fila en `audit_logs` con:

- Usuario.
- Rol.
- Escuela.
- Fecha UTC.
- IP.
- `StudentAssignmentId`.
- Estudiante.
- Grado.
- Grupo.
- Jornada.
- Cantidad de materias afectadas.
- IDs de materias afectadas.

Acción:

`RemoveEnrollment`

Recurso:

`StudentAssignment`

## 9. Pruebas y validación

### Ejecutado

Compilación local:

```powershell
dotnet build "C:\Proyectos\EduplanerNoche\SchoolManager\SchoolManager.csproj"
```

Resultado:

- `Build succeeded.`
- `0 Warning(s)`
- `0 Error(s)`

Linter IDE:

- Sin errores en `Controllers/StudentAssignmentController.cs`.
- Sin errores en `Views/StudentAssignment/Index.cshtml`.

### Casos cubiertos por código

- Grupo sin materias: inactiva solo `student_assignments`.
- Grupo con materias: requiere confirmación y luego inactiva materias + matrícula.
- Grupo inexistente: retorna `NOT_FOUND`.
- Matrícula ya inactiva: retorna `INACTIVE`.
- Permisos insuficientes: retorna `Forbid`.
- Múltiples grupos: opera por `studentAssignmentId`, no por estudiante completo.

### No ejecutado contra producción

No se ejecutaron pruebas que llamen `POST /StudentAssignment/RemoveEnrollment` contra la BD de Render, porque aunque son soft delete, modifican datos de producción. La validación funcional destructiva debe hacerse primero en entorno local/staging o con autorización explícita para un registro de prueba.

## 10. Resultado

La implementación local permite quitar individualmente:

- `10 - 10-A`
- `11 - 11-A`
- `11 - 11-A4`

Siempre mediante soft delete y con confirmación adicional si existen materias activas.

No se hizo commit.  
No se hizo push.  
No se ejecutaron `DELETE` físicos.  
No se modificaron datos de producción durante esta implementación.
