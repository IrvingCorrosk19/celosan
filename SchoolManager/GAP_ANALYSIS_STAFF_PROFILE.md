# GAP Analysis: Staff Institutional Profile

**Actual:** `C:\Proyectos\EduplanerNoche\SchoolManager`  
**Referencia:** `C:\Proyectos\EduplanerIIC\SchoolManager`  
**Fecha:** 25 de mayo de 2026

---

## Matriz comparativa

| Componente | Referencia IIC | Proyecto Noche (antes) | Proyecto Noche (después) | Gap resuelto |
|------------|----------------|------------------------|--------------------------|--------------|
| `StaffInstitutionalProfileController` | ✅ | ✅ Idéntico | ✅ | — |
| `IStaffInstitutionalProfileService` | ✅ | ✅ | ✅ | — |
| `StaffInstitutionalProfileService` | ✅ | ✅ | ✅ | — |
| `StaffInstitutionalProfileViewModel` | ✅ | ✅ | ✅ | — |
| `StaffInstitutionalProfileAccess` | ✅ | ✅ | ✅ | — |
| `StaffInstitutionalRoleFilter` | ✅ | ✅ | ✅ | — |
| `Models/StaffInstitutionalProfile` | ✅ | ✅ | ✅ | — |
| Migración tablas credencial | `20260514104759_*` | `20260601232556_*` | ✅ Equivalente | — |
| `Views/StaffInstitutionalProfile/Index.cshtml` | ✅ | ❌ | ✅ Copiada | **Sí** |
| Menú `_AdminLayout` | ✅ | ❌ | ✅ | **Sí** |
| Menú `_SuperAdminLayout` | ✅ | ❌ | ✅ | **Sí** |
| `InstitutionalCredentialController` | ✅ | ✅ (commit previo) | ✅ | — |
| `SuperAdmin/StaffDirectory` | ✅ | ✅ (commit previo) | ✅ | — |
| QR / vista pública / impresión | ✅ En `InstitutionalCredential` | ✅ En `InstitutionalCredential` | ✅ | — |

---

## Qué existía (no replicar)

- Controlador completo con mismas rutas y validaciones.
- Servicio de perfil con lógica de `users` + `staff_institutional_profiles`.
- Helpers de roles y acceso.
- Entidad EF y contexto (`SchoolDbContext.StaffInstitutionalProfiles`).
- Migración y tablas en PostgreSQL Render.
- Módulo `InstitutionalCredential` (QR, PDF, vista pública).
- `SuperAdmin/StaffDirectory` para emisión centralizada.

---

## Qué faltaba (replicado en Fase 4)

| Ítem | Acción |
|------|--------|
| `Views/StaffInstitutionalProfile/Index.cshtml` | Copiado desde IIC (namespaces ya compatibles) |
| Enlace menú admin | Añadido en `_AdminLayout.cshtml` con `showStaffInstitutionalProfileMenu` |
| Enlace menú superadmin | Añadido en `_SuperAdminLayout.cshtml` |

---

## Qué debe replicarse

**Nada adicional.** Paridad funcional y visual alcanzada con los tres archivos anteriores.

---

## Qué puede reutilizarse (ya reutilizado)

| Recurso compartido | Uso |
|--------------------|-----|
| `IUserPhotoService` | Foto de perfil / carnet |
| `StaffInstitutionalRoleFilter` | Queries de personal |
| `InstitutionalCredential/*` | QR, impresión, verificación pública |
| `UserPhotoLinks` / `/File/GetUserPhoto` | Preview de fotografía |
| Tablas `staff_institutional_profiles`, `institutional_credential_cards`, `staff_qr_tokens` | Persistencia |

---

## Qué no es necesario copiar

| Elemento | Motivo |
|----------|--------|
| Controlador / servicio / ViewModel | Ya idénticos en Noche |
| Migraciones IIC | Noche tiene migración equivalente propia |
| Documentación interna IIC (`ANALISIS_STAFF_PROFILE_CARNET_INSTITUCIONAL.md`) | Solo referencia analítica |
| Lógica QR/PDF duplicada en `StaffInstitutionalProfile` | Arquitectura correcta: separación de responsabilidades |
| Repositorios dedicados | No existen en IIC; patrón DbContext directo |

---

## Diferencias menores aceptables

| Aspecto | IIC | Noche |
|---------|-----|-------|
| Nombre migración | `20260514104759_*` | `20260601232556_*` |
| Sección menú SuperAdmin | Header "CREDENCIALES" explícito | Ítems agrupados sin header separado (misma funcionalidad) |
| Filas `staff_institutional_profiles` | Según uso en IIC | 0 en Render (lazy create al primer acceso) |

---

## Riesgos residuales

| Riesgo | Mitigación |
|--------|------------|
| Perfil vacío en primer acceso | `EnsureStaffProfileRowAsync` crea fila automáticamente |
| Usuario sin escuela asignada | Vista muestra aviso; credencial bloqueada hasta asignación |
| Emisión credencial | Solo superadmin (`CanOpenInstitutionalCredentialUi`) — igual que IIC |

---

## Conclusión

El gap era **exclusivamente de UI y navegación** (~3 archivos). El stack backend y el ecosistema de credenciales ya estaban implementados en el proyecto Noche desde el trabajo previo de `InstitutionalCredential` + `StaffDirectory`.
