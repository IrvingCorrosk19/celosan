Carga masiva student_enrollments_test.xlsx (UploadSubjectEnrollments)
=====================================================================

1) En PostgreSQL debe existir al menos UNA fila en subject_assignments
   (en su escuela o en cualquier escuela) para tomar AreaId y SpecialtyId
   al crear imparticiones nuevas automaticamente.

2) Los grupos del Excel (N-7A, N-8B, N-9A, N-10C, N-11A) deben existir en
   la tabla groups con school_id = escuela del usuario que carga el archivo.

3) El archivo de ejemplo esta en wwwroot/samples/student_enrollments_test.xlsx
   y se puede descargar desde la pantalla de carga.

4) Si la base esta vacia de imparticiones, cree una desde el modulo academico
   (asignacion de materias a grupo) y vuelva a cargar el Excel.
