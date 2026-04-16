using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Dtos;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SchoolManager.Services.Implementations
{
    public class StudentReportService : IStudentReportService
    {
        private readonly SchoolDbContext _context;
        private readonly IDisciplineReportService _disciplineReportService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAcademicYearService _academicYearService;
        private readonly ILogger<StudentReportService> _logger;

        public StudentReportService(SchoolDbContext context, IDisciplineReportService disciplineReportService, ICurrentUserService currentUserService, IAcademicYearService academicYearService, ILogger<StudentReportService> logger)
        {
            _context = context;
            _disciplineReportService = disciplineReportService;
            _currentUserService = currentUserService;
            _academicYearService = academicYearService;
            _logger = logger;
        }

        // Método auxiliar para verificar si una columna existe (compatibilidad hacia atrás)
        private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = {0} AND column_name = {1}";
                var result = await _context.Database.SqlQueryRaw<int>(sql, tableName.ToLower(), columnName.ToLower()).ToListAsync();
                return result.FirstOrDefault() > 0;
            }
            catch
            {
                // Si falla la consulta, asumir que la columna no existe
                return false;
            }
        }

        public async Task<StudentReportDto> GetReportByStudentIdAsync(Guid studentId)
        {
            try
            {
                _logger.LogInformation("=== INICIO GetReportByStudentIdAsync - StudentId: {StudentId} ===", studentId);
                Console.WriteLine($"=== INICIO GetReportByStudentIdAsync - StudentId: {studentId} ===");

                // Obtener el Grado y Grupo del estudiante PRIMERO para saber su escuela
                _logger.LogInformation("Buscando asignación del estudiante: {StudentId}", studentId);
                Console.WriteLine($"Buscando asignación del estudiante: {studentId}");

                var studentUser = await _context.Users
                    .Where(u => u.Id == studentId)
                    .Select(u => new { u.SchoolId, u.Name, u.LastName })
                    .FirstOrDefaultAsync();

                if (studentUser == null || !studentUser.SchoolId.HasValue)
                {
                    _logger.LogWarning("No se encontró el usuario o no tiene escuela asignada: {StudentId}", studentId);
                    Console.WriteLine($"No se encontró el usuario o no tiene escuela asignada: {studentId}");
                    return null;
                }

                // Obtener TODOS los trimestres de la escuela del estudiante (desde la tabla Trimesters)
                _logger.LogInformation("Buscando trimestres disponibles para la escuela: {SchoolId}", studentUser.SchoolId);
                Console.WriteLine($"Buscando trimestres disponibles para la escuela: {studentUser.SchoolId}");

                var trimesters = await _context.Trimesters
                    .Where(t => t.SchoolId == studentUser.SchoolId && t.IsActive)
                    .OrderBy(t => t.Order)
                    .Select(t => t.Name)
                    .ToListAsync();

                _logger.LogInformation("Trimestres encontrados en la configuración: {Trimesters}", string.Join(", ", trimesters));
                Console.WriteLine($"Trimestres encontrados en la configuración: {string.Join(", ", trimesters)}");

                if (!trimesters.Any())
                {
                    _logger.LogWarning("No hay trimestres configurados en la escuela: {SchoolId}", studentUser.SchoolId);
                    Console.WriteLine($"No hay trimestres configurados en la escuela: {studentUser.SchoolId}");
                    // Aún así, intentar obtener trimestres de las actividades como fallback
                    trimesters = new List<string> { "1T", "2T", "3T" }; // Trimestres por defecto
                }

                // Seleccionar SIEMPRE el primer trimestre disponible (por orden: 1T, 2T, 3T)
                var selectedTrimester = trimesters.FirstOrDefault(t => t == "1T") ??
                                        trimesters.FirstOrDefault(t => t == "2T") ??
                                        trimesters.FirstOrDefault(t => t == "3T");

                _logger.LogInformation("Trimestre seleccionado: {SelectedTrimester}", selectedTrimester);
                Console.WriteLine($"Trimestre seleccionado: {selectedTrimester}");

                // Obtener las actividades del estudiante con la calificación para el trimestre seleccionado
                _logger.LogInformation("Buscando calificaciones para StudentId: {StudentId}, Trimester: {Trimester}", studentId, selectedTrimester);
                Console.WriteLine($"Buscando calificaciones para StudentId: {studentId}, Trimester: {selectedTrimester}");

                // MEJORADO: Obtener año académico activo para filtrar notas
                var activeAcademicYear = await _academicYearService.GetActiveAcademicYearAsync(studentUser.SchoolId);

                // MEJORADO: Verificar si la columna academic_year_id existe antes de usarla
                var academicYearColumnExists = await ColumnExistsAsync("student_activity_scores", "academic_year_id");

                var scoresBaseQuery = _context.StudentActivityScores
                    .Where(s => s.StudentId == studentId);

                // Filtrar por año académico solo si existe la columna y hay un año académico activo
                if (activeAcademicYear != null && academicYearColumnExists)
                {
                    scoresBaseQuery = scoresBaseQuery.Where(s => s.AcademicYearId == activeAcademicYear.Id);
                }

                var scoresQuery = scoresBaseQuery
                    .Join(_context.Activities,
                          score => score.ActivityId,
                          activity => activity.Id,
                          (score, activity) => new
                          {
                              ActivityId = activity.Id,
                              activity.GradeLevelId,
                              activity.GroupId,
                              activity.SubjectId,
                              activity.Name,
                              activity.Trimester,
                              activity.TeacherId,
                              score.Score,
                              score.CreatedAt
                          })
                    .Where(a => a.Trimester == selectedTrimester);

                var studentScores = await scoresQuery
                    .GroupBy(a => new { a.ActivityId, a.SubjectId, a.TeacherId, a.Name })
                    .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                    .ToListAsync();

                _logger.LogInformation("Calificaciones encontradas: {ScoresCount}", studentScores?.Count ?? 0);
                Console.WriteLine($"Calificaciones encontradas: {studentScores?.Count ?? 0}");

                // Obtener matrículas activas y asignaturas activas del estudiante (modelo flexible nocturno)
                var studentAssignment = await _context.StudentAssignments
                    .Where(sa => sa.StudentId == studentId && sa.IsActive)
                    .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
                    .Join(_context.GradeLevels,
                          sa => sa.GradeId,
                          gl => gl.Id,
                          (sa, gl) => new { sa.GroupId, sa.GradeId, GradeName = gl.Name, sa.Id })
                    .Join(_context.Groups,
                          sa => sa.GroupId,
                          g => g.Id,
                          (sa, g) => new { sa.Id, sa.GroupId, GradeName = sa.GradeName, GroupName = g.Name })
                    .FirstOrDefaultAsync();

                var activeSubjectAssignments = await _context.StudentSubjectAssignments
                    .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
                    .Join(_context.SubjectAssignments,
                          ssa => ssa.SubjectAssignmentId,
                          sa => sa.Id,
                          (ssa, sa) => new { ssa.Id, ssa.SubjectAssignmentId, sa.GroupId, sa.GradeLevelId })
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("Asignación encontrada: {Assignment}", studentAssignment != null ? $"Grado: {studentAssignment.GradeName}, Grupo: {studentAssignment.GroupName}" : "NULL");
                Console.WriteLine($"Asignación encontrada: {(studentAssignment != null ? $"Grado: {studentAssignment.GradeName}, Grupo: {studentAssignment.GroupName}" : "NULL")}");

                var name = $"{studentUser.Name} {studentUser.LastName}";
                var gradeName = studentAssignment != null ? $"{studentAssignment.GradeName} - {studentAssignment.GroupName}" : "Sin asignación";

                // Si no hay calificaciones, devolver reporte vacío pero con trimestres disponibles
                if (studentScores == null || !studentScores.Any())
                {
                    _logger.LogWarning("No se encontraron calificaciones para StudentId: {StudentId}, Trimester: {Trimester}", studentId, selectedTrimester);
                    Console.WriteLine($"No se encontraron calificaciones para StudentId: {studentId}, Trimester: {selectedTrimester}");
                    
                    return new StudentReportDto
                    {
                        StudentId = studentId,
                        StudentName = name,
                        Grade = gradeName,
                        Grades = new List<GradeDto>(),
                        AttendanceByTrimester = new List<AttendanceDto>(),
                        AttendanceByMonth = new List<AttendanceDto>(),
                        Trimester = selectedTrimester,
                        AvailableTrimesters = trimesters.Select(t => new AvailableTrimesters { Trimester = t }).ToList(),
                        DisciplineReports = new List<DisciplineReportDto>(),
                        PendingActivities = new List<PendingActivityDto>(),
                        AvailableSubjects = new List<string>()
                    };
                }

            // Obtener datos adicionales en una sola consulta para evitar duplicaciones
            var subjectIds = studentScores.Select(s => s.SubjectId).Distinct().ToList();
            var teacherIds = studentScores.Select(s => s.TeacherId).Distinct().ToList();
            var activityIds = studentScores.Select(s => s.ActivityId).Distinct().ToList();

            var subjects = await _context.Subjects
                .Where(s => subjectIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name);

            var teachers = await _context.Users
                .Where(u => teacherIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.Name ?? "Nombre Desconocido"} {u.LastName ?? "Apellido Desconocido"}");

            var activities = await _context.Activities
                .Where(a => activityIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => new { a.Type, a.PdfUrl });

            var grades = studentScores.Select(a => new GradeDto
            {
                Subject = a.SubjectId.HasValue ? subjects.GetValueOrDefault(a.SubjectId.Value, "Desconocida") : "Desconocida",
                Teacher = a.TeacherId.HasValue ? teachers.GetValueOrDefault(a.TeacherId.Value, "Desconocido") : "Desconocido",
                ActivityName = a.Name,
                Type = activities.GetValueOrDefault(a.ActivityId)?.Type ?? "SinTipo",
                Value = a.Score,
                CreatedAt = a.CreatedAt.ToUniversalTime(),
                FileUrl = activities.GetValueOrDefault(a.ActivityId)?.PdfUrl,
                Trimester = a.Trimester
            }).ToList();

            // --- ASISTENCIA POR TRIMESTRE ---
            var trimesterConfig = await _context.Trimesters.FirstOrDefaultAsync(t => t.Name == selectedTrimester);
            var attendanceByTrimester = new List<AttendanceDto>();
            if (trimesterConfig != null)
            {
                var startDate = DateOnly.FromDateTime(trimesterConfig.StartDate);
                var endDate = DateOnly.FromDateTime(trimesterConfig.EndDate);

                var asistencias = await _context.Attendances
                    .Where(a => 
                        a.StudentId == studentId && 
                        a.Date >= startDate && 
                        a.Date <= endDate)
                    .ToListAsync();

                attendanceByTrimester.Add(new AttendanceDto
                {
                    Month = trimesterConfig.Name,
                    Present = asistencias.Count(a => a.Status == "present"),
                    Absent = asistencias.Count(a => a.Status == "absent"),
                    Late = asistencias.Count(a => a.Status == "late"),
                    Trimester = trimesterConfig.Name
                });
            }

            // --- ASISTENCIA POR MES ---
            var attendanceByMonth = new List<AttendanceDto>();
            if (trimesterConfig != null)
            {
                var startDate = DateOnly.FromDateTime(trimesterConfig.StartDate);
                var endDate = DateOnly.FromDateTime(trimesterConfig.EndDate);

                var attendanceByMonthRaw = await _context.Attendances
                    .Where(a => 
                        a.StudentId == studentId && 
                        a.Date >= startDate && 
                        a.Date <= endDate)
                    .GroupBy(a => new { a.Date.Year, a.Date.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        MonthNumber = g.Key.Month,
                        Present = g.Count(a => a.Status == "present"),
                        Absent = g.Count(a => a.Status == "absent"),
                        Late = g.Count(a => a.Status == "late")
                    })
                    .OrderBy(g => g.Year).ThenBy(g => g.MonthNumber)
                    .ToListAsync();

                attendanceByMonth = attendanceByMonthRaw
                    .Select(g => new AttendanceDto
                    {
                        Month = new DateTime(g.Year, g.MonthNumber, 1).ToString("MMMM", new CultureInfo("es-ES")),
                        Present = g.Present,
                        Absent = g.Absent,
                        Late = g.Late,
                        Trimester = trimesterConfig.Name
                    })
                    .ToList();
            }

            // Obtener los reportes de disciplina
            var disciplineReports = await _disciplineReportService.GetByStudentDtoAsync(studentId, selectedTrimester);

            // Obtener las actividades pendientes
            List<PendingActivityDto> pendingActivities = new();

            if (activeSubjectAssignments.Any())
            {
                try
                {
                    var scopedSubjectAssignmentIds = activeSubjectAssignments.Select(x => x.SubjectAssignmentId).Distinct().ToList();
                    pendingActivities = await _context.Activities
                        .Where(a => a.Trimester == selectedTrimester &&
                            _context.SubjectAssignments.Any(sa => scopedSubjectAssignmentIds.Contains(sa.Id)
                                && sa.SubjectId == a.SubjectId
                                && sa.GroupId == a.GroupId
                                && sa.GradeLevelId == a.GradeLevelId) &&
                            !_context.StudentActivityScores.Any(s =>
                                s.ActivityId == a.Id &&
                                s.StudentId == studentId &&
                                ((s.StudentSubjectAssignmentId.HasValue && activeSubjectAssignments.Select(x => x.Id).Contains(s.StudentSubjectAssignmentId.Value))
                                    || _context.StudentAssignments.Any(sa => sa.Id == s.StudentAssignmentId && sa.StudentId == studentId))))
                        .Select(a => new PendingActivityDto
                        {
                            ActivityId = a.Id,
                            Name = a.Name,
                            SubjectName = a.Subject.Name,
                            CreatedAt = a.CreatedAt ?? DateTime.UtcNow,
                            FileUrl = a.PdfUrl,
                            TeacherName = $"{a.Teacher.Name} {a.Teacher.LastName}",
                            Type = a.Type ?? "SinTipo"
                        })
                        .OrderByDescending(a => a.CreatedAt)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error obteniendo actividades pendientes");
                    pendingActivities = new List<PendingActivityDto>();
                }
            }

                _logger.LogInformation("Construyendo reporte final - Grades: {GradesCount}, Attendance: {AttendanceCount}, Discipline: {DisciplineCount}, Pending: {PendingCount}", 
                    grades?.Count ?? 0, attendanceByTrimester?.Count ?? 0, disciplineReports?.Count ?? 0, pendingActivities?.Count ?? 0);
                Console.WriteLine($"Construyendo reporte final - Grades: {grades?.Count ?? 0}, Attendance: {attendanceByTrimester?.Count ?? 0}, Discipline: {disciplineReports?.Count ?? 0}, Pending: {pendingActivities?.Count ?? 0}");

                // Obtener lista de materias únicas
                var availableSubjects = grades
                    .Select(g => g.Subject)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();

                var result = new StudentReportDto
                {
                    StudentId = studentId,
                    StudentName = name,
                    Grade = gradeName,
                    Grades = grades,
                    AttendanceByTrimester = attendanceByTrimester,
                    AttendanceByMonth = attendanceByMonth,
                    Trimester = selectedTrimester,
                    AvailableTrimesters = trimesters
                        .Select(t => new AvailableTrimesters { Trimester = t })
                        .ToList(),
                    DisciplineReports = disciplineReports,
                    PendingActivities = pendingActivities,
                    AvailableSubjects = availableSubjects
                };

                _logger.LogInformation("=== FIN GetReportByStudentIdAsync - Reporte construido exitosamente ===");
                Console.WriteLine("=== FIN GetReportByStudentIdAsync - Reporte construido exitosamente ===");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetReportByStudentIdAsync para StudentId: {StudentId} - {Message}", studentId, ex.Message);
                Console.WriteLine($"ERROR en GetReportByStudentIdAsync para StudentId: {studentId} - {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<StudentReportDto> GetReportByStudentIdAndTrimesterAsync(Guid studentId, string trimester)
        {
            // Obtener la escuela del estudiante
            var studentUser = await _context.Users
                .Where(u => u.Id == studentId)
                .Select(u => new { u.SchoolId })
                .FirstOrDefaultAsync();

            // MEJORADO: Obtener año académico activo para filtrar notas
            var activeAcademicYear = studentUser?.SchoolId.HasValue == true
                ? await _academicYearService.GetActiveAcademicYearAsync(studentUser.SchoolId)
                : null;

            // MEJORADO: Verificar si la columna academic_year_id existe antes de usarla
            var academicYearColumnExists = await ColumnExistsAsync("student_activity_scores", "academic_year_id");

            var scoresBaseQuery = _context.StudentActivityScores
                .Where(s => s.StudentId == studentId);

            // Filtrar por año académico solo si existe la columna y hay un año académico activo
            if (activeAcademicYear != null && academicYearColumnExists)
            {
                scoresBaseQuery = scoresBaseQuery.Where(s => s.AcademicYearId == activeAcademicYear.Id);
            }

            // Obtener las actividades del estudiante con las calificaciones para el trimestre seleccionado
            var scoresQuery = scoresBaseQuery
                .Join(_context.Activities,
                      score => score.ActivityId,
                      activity => activity.Id,
                      (score, activity) => new
                      {
                          activity.Id,
                          activity.GradeLevelId,
                          activity.GroupId,
                          activity.SubjectId,
                          activity.Name,
                          activity.Trimester,
                          activity.TeacherId,
                          activity.Type,
                          activity.PdfUrl,
                          activity.CreatedAt,
                          activity.Subject,
                          activity.Teacher,
                          Score = score.Score,
                          ScoreCreatedAt = score.CreatedAt
                      })
                .Where(a => a.Trimester.Trim().ToLower() == trimester.Trim().ToLower());

            var studentScores = await scoresQuery
                .GroupBy(a => new { a.Id, a.SubjectId, a.TeacherId, a.Name })
                .Select(g => g.OrderByDescending(x => x.ScoreCreatedAt).First())
                .ToListAsync();

            if (studentScores == null || !studentScores.Any())
            {
                return null;
            }

            // Obtener el Grado y Grupo principal del estudiante (activa más reciente)
            var studentAssignment = await _context.StudentAssignments
                .Where(sa => sa.StudentId == studentId && sa.IsActive)
                .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
                .Join(_context.GradeLevels,
                      sa => sa.GradeId,
                      gl => gl.Id,
                      (sa, gl) => new { sa.GroupId, sa.GradeId, GradeName = gl.Name, sa.Id })
                .Join(_context.Groups,
                      sa => sa.GroupId,
                      g => g.Id,
                      (sa, g) => new { sa.Id, sa.GroupId, GradeName = sa.GradeName, GroupName = g.Name })
                .FirstOrDefaultAsync();

            if (studentAssignment == null)
            {
                return null;
            }

            // Obtener los datos del estudiante
            var studentData = await _context.Users
                .Where(u => u.Id == studentId)
                .Select(u => new { u.Name, u.LastName })
                .FirstOrDefaultAsync();

            if (studentData == null)
            {
                return null;
            }

            var name = $"{studentData.Name} {studentData.LastName}";

            var grades = studentScores.Select(a => new GradeDto
            {
                Subject = a.Subject?.Name ?? "Desconocida",
                Teacher = a.Teacher != null ? $"{a.Teacher.Name} {a.Teacher.LastName}" : "Desconocido",
                ActivityName = a.Name,
                Type = a.Type ?? "SinTipo",
                Value = a.Score,
                CreatedAt = a.CreatedAt ?? DateTime.UtcNow,
                FileUrl = a.PdfUrl,
                Trimester = a.Trimester
            }).ToList();

            // --- ASISTENCIA POR TRIMESTRE ---
            var trimesterConfig = await _context.Trimesters.FirstOrDefaultAsync(t => t.Name == trimester);
            var attendanceByTrimester = new List<AttendanceDto>();
            if (trimesterConfig != null)
            {
                var startDate = DateOnly.FromDateTime(trimesterConfig.StartDate);
                var endDate = DateOnly.FromDateTime(trimesterConfig.EndDate);

                var asistencias = await _context.Attendances
                    .Where(a => 
                        a.StudentId == studentId && 
                        a.Date >= startDate && 
                        a.Date <= endDate)
                    .ToListAsync();

                attendanceByTrimester.Add(new AttendanceDto
                {
                    Month = trimesterConfig.Name,
                    Present = asistencias.Count(a => a.Status == "present"),
                    Absent = asistencias.Count(a => a.Status == "absent"),
                    Late = asistencias.Count(a => a.Status == "late"),
                    Trimester = trimesterConfig.Name
                });
            }

            // --- ASISTENCIA POR MES ---
            var attendanceByMonth = new List<AttendanceDto>();
            if (trimesterConfig != null)
            {
                var startDate = DateOnly.FromDateTime(trimesterConfig.StartDate);
                var endDate = DateOnly.FromDateTime(trimesterConfig.EndDate);

                var attendanceByMonthRaw = await _context.Attendances
                    .Where(a => 
                        a.StudentId == studentId && 
                        a.Date >= startDate && 
                        a.Date <= endDate)
                    .GroupBy(a => new { a.Date.Year, a.Date.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        MonthNumber = g.Key.Month,
                        Present = g.Count(a => a.Status == "present"),
                        Absent = g.Count(a => a.Status == "absent"),
                        Late = g.Count(a => a.Status == "late")
                    })
                    .OrderBy(g => g.Year).ThenBy(g => g.MonthNumber)
                    .ToListAsync();

                attendanceByMonth = attendanceByMonthRaw
                    .Select(g => new AttendanceDto
                    {
                        Month = new DateTime(g.Year, g.MonthNumber, 1).ToString("MMMM", new CultureInfo("es-ES")),
                        Present = g.Present,
                        Absent = g.Absent,
                        Late = g.Late,
                        Trimester = trimesterConfig.Name
                    })
                    .ToList();
            }

            // Obtener los reportes de disciplina
            var disciplineReports = await _disciplineReportService.GetByStudentDtoAsync(studentId, trimester);

            // Obtener las actividades pendientes
            List<PendingActivityDto> pendingActivities = new();

            var activeSubjectAssignments = await _context.StudentSubjectAssignments
                .Where(ssa => ssa.StudentId == studentId && ssa.IsActive)
                .Join(_context.SubjectAssignments,
                      ssa => ssa.SubjectAssignmentId,
                      sa => sa.Id,
                      (ssa, sa) => new { ssa.Id, ssa.SubjectAssignmentId, sa.GroupId, sa.GradeLevelId })
                .Distinct()
                .ToListAsync();

            try
            {
                var scopedSubjectAssignmentIds = activeSubjectAssignments.Select(x => x.SubjectAssignmentId).Distinct().ToList();
                pendingActivities = await _context.Activities
                    .Where(a => a.Trimester == trimester &&
                                _context.SubjectAssignments.Any(sa => scopedSubjectAssignmentIds.Contains(sa.Id)
                                    && sa.SubjectId == a.SubjectId
                                    && sa.GroupId == a.GroupId
                                    && sa.GradeLevelId == a.GradeLevelId) &&
                                !_context.StudentActivityScores.Any(s =>
                                    s.ActivityId == a.Id &&
                                    s.StudentId == studentId &&
                                    ((s.StudentSubjectAssignmentId.HasValue && activeSubjectAssignments.Select(x => x.Id).Contains(s.StudentSubjectAssignmentId.Value))
                                     || _context.StudentAssignments.Any(sa =>
                                            sa.Id == s.StudentAssignmentId && sa.StudentId == studentId))))
                    .Select(a => new PendingActivityDto
                    {
                        ActivityId = a.Id,
                        Name = a.Name,
                        SubjectName = a.Subject.Name,
                        CreatedAt = a.CreatedAt ?? DateTime.UtcNow,
                        FileUrl = a.PdfUrl,
                        TeacherName = $"{a.Teacher.Name} {a.Teacher.LastName}",
                        Type = a.Type ?? "SinTipo"
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                pendingActivities = new List<PendingActivityDto>();
            }

            return new StudentReportDto
            {
                StudentId = studentId,
                StudentName = name,
                Grade = $"{studentAssignment.GradeName} - {studentAssignment.GroupName}",
                Grades = grades,
                AttendanceByTrimester = attendanceByTrimester,
                AttendanceByMonth = attendanceByMonth,
                Trimester = trimester,
                AvailableTrimesters = new List<AvailableTrimesters> { new AvailableTrimesters { Trimester = trimester } },
                DisciplineReports = disciplineReports,
                PendingActivities = pendingActivities
            };
        }

        public async Task<List<DisciplineReportDto>> GetDisciplineReportsByStudentIdAsync(Guid studentId)
        {
            try
            {
                _logger.LogInformation("=== INICIO GetDisciplineReportsByStudentIdAsync - StudentId: {StudentId} ===", studentId);
                Console.WriteLine($"=== INICIO GetDisciplineReportsByStudentIdAsync - StudentId: {studentId} ===");

                var reports = await _context.DisciplineReports
                    .Where(dr => dr.StudentId == studentId)
                    .Include(dr => dr.Teacher)
                    .OrderByDescending(dr => dr.Date)
                    .Select(dr => new DisciplineReportDto
                    {
                        Date = dr.Date,
                        Time = dr.Date.ToString("HH:mm"), // Usar la hora de la fecha
                        Type = dr.ReportType ?? "Sin tipo",
                        Status = dr.Status ?? "Sin estado",
                        Description = dr.Description ?? "Sin descripción",
                        DisciplineActionsJson = dr.DisciplineActionsJson,
                        Teacher = dr.Teacher != null ? $"{dr.Teacher.Name} {dr.Teacher.LastName}" : "Sin profesor"
                    })
                    .ToListAsync();

                _logger.LogInformation("Reportes de disciplina encontrados: {Count}", reports.Count);
                Console.WriteLine($"Reportes de disciplina encontrados: {reports.Count}");

                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetDisciplineReportsByStudentIdAsync: {Message}", ex.Message);
                Console.WriteLine($"ERROR en GetDisciplineReportsByStudentIdAsync: {ex.Message}");
                return new List<DisciplineReportDto>();
            }
        }
    }
}


