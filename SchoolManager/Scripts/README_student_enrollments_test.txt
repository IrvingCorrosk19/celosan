Carga masiva Excel (inscripciones por materia) — /StudentAssignment/Upload
============================================================================

1) En PostgreSQL debe existir al menos UNA fila en subject_assignments
   (en su escuela o en cualquier escuela) para tomar AreaId y SpecialtyId
   al crear imparticiones nuevas automaticamente.

2) Los grupos del Excel (p. ej. N-7A, 10-A) deben existir en la tabla groups
   con school_id = escuela del usuario que carga el archivo.

3) Plantilla de ejemplo: wwwroot/descargables/asignaciones_estudiantes_grado_grupo.xlsx
   (descarga desde la misma pantalla de carga).

4) Si la base esta vacia de imparticiones, cree una desde el modulo academico
   (asignacion de materias a grupo) y vuelva a cargar el Excel.
