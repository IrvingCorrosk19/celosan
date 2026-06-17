# Backup previo a cambios - StudentAssignment

Fecha local: 2026-06-17 17:29:35 (UTC-5)  
Motivo: implementación controlada para quitar una matrícula/grupo específico desde `/StudentAssignment/Index`.  
Modo: backup completo antes de modificar código. No se modificaron datos de producción.

## Ubicación local

Carpeta:

`C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935`

## Backup completo del proyecto

Archivo:

`C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935\SchoolManager_project_20260617_172935.tar.gz`

Tamaño: `307077118` bytes  
Entradas verificadas: `2191`  
Verificación: listado completo con `tar -tzf`  
SHA256:

`9EE3EBD666B5AFE42A9E26512F87F93A6A235BBB8863064D7E3D72D1270082EA`

## Backup completo de PostgreSQL producción

Archivo:

`C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935\schoolmanager_daqf_full_20260617_172935.dump`

Base de datos: `schoolmanager_daqf`  
Formato: `pg_dump -Fc` (custom)  
Tamaño: `401740` bytes  
Entradas verificadas: `589`  
Verificación: `pg_restore --list` completado correctamente  
SHA256:

`7737E5070D0787F485608B16C1319331EBC01E632AB5104C9C21955BCCBCB6C9`

## Comandos de verificación usados

```powershell
tar -tzf "C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935\SchoolManager_project_20260617_172935.tar.gz"
```

```powershell
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" --list "C:\Proyectos\EduplanerNoche\Backups\StudentAssignment_20260617_172935\schoolmanager_daqf_full_20260617_172935.dump"
```

## Resultado

Backup del proyecto y backup de base de datos generados, descargados localmente y verificados antes de iniciar cambios de implementación.
