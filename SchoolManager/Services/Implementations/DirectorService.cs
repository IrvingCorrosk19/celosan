using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SchoolManager.Helpers;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using System.Text;

namespace SchoolManager.Services.Implementations
{
    public class DirectorService : IDirectorService
    {
        private readonly IUserService _userService;
        private readonly IStudentReportService _studentReportService;
        private readonly ISubjectService _subjectService;
        private readonly ITrimesterService _trimesterService;
        private readonly SchoolDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IOfficialGradeService _officialGradeService;
        private readonly IAcademicYearService _academicYearService;

        public DirectorService(
            IUserService userService, 
            IStudentReportService studentReportService, 
            ISubjectService subjectService, 
            ITrimesterService trimesterService, 
            SchoolDbContext context,
            ICurrentUserService currentUserService,
            IOfficialGradeService officialGradeService,
            IAcademicYearService academicYearService)
        {
            _userService = userService;
            _studentReportService = studentReportService;
            _subjectService = subjectService;
            _trimesterService = trimesterService;
            _context = context;
            _currentUserService = currentUserService;
            _officialGradeService = officialGradeService;
            _academicYearService = academicYearService;
        }

        public async Task<DirectorViewModel> GetDashboardViewModelAsync(string trimestre = null)
        {
            var model = new DirectorViewModel();
            var trimestres = await _trimesterService.GetAllAsync();
            model.TrimestresDisponibles = trimestres;
            model.TrimestreSeleccionado = string.IsNullOrEmpty(trimestre) ? "" : trimestre;

            // Obtener datos de estudiantes, aprobados, reprobados, etc.
            var estudiantes = await _userService.GetAllAsync();
            var soloEstudiantes = estudiantes.Where(e => e.Role.ToLower() == "estudiante" || e.Role.ToLower() == "student" || e.Role.ToLower() == "alumno").ToList();
            model.TotalEstudiantes = soloEstudiantes.Count;
            int totalAprobados = 0;
            int totalReprobados = 0;
            int totalSinEvaluar = 0;

            var reportesPorEstudiante = new Dictionary<Guid, SchoolManager.Dtos.StudentReportDto>();
            foreach (var estudiante in soloEstudiantes)
            {
                try
                {
                    var reporte = await _studentReportService.GetReportByStudentIdAsync(estudiante.Id);
                    // Si no se filtra por trimestre, incluir todos los reportes
                    if (reporte != null && (string.IsNullOrEmpty(model.TrimestreSeleccionado) || reporte.Trimester == model.TrimestreSeleccionado))
                        reportesPorEstudiante[estudiante.Id] = reporte;
                }
                catch { }
            }

            foreach (var estudiante in soloEstudiantes)
            {
                if (reportesPorEstudiante.TryGetValue(estudiante.Id, out var reporte))
                {
                    var promedio = OfficialStudentAverage(reporte);
                    if (promedio.HasValue && promedio.Value >= 3.0)
                        totalAprobados++;
                    else if (promedio.HasValue && promedio.Value >= 1.0 && promedio.Value < 3.0)
                        totalReprobados++;
                    else
                        totalSinEvaluar++;
                }
                else
                {
                    totalSinEvaluar++;
                }
            }

            double porcentajeAprobados = model.TotalEstudiantes > 0 ? (totalAprobados * 100.0 / model.TotalEstudiantes) : 0;
            double porcentajeReprobados = model.TotalEstudiantes > 0 ? (totalReprobados * 100.0 / model.TotalEstudiantes) : 0;
            double porcentajeSinEvaluar = model.TotalEstudiantes > 0 ? (totalSinEvaluar * 100.0 / model.TotalEstudiantes) : 0;

            model.TotalAprobados = totalAprobados;
            model.TotalReprobados = totalReprobados;
            model.TotalSinEvaluar = totalSinEvaluar;
            model.PorcentajeAprobados = porcentajeAprobados;
            model.PorcentajeReprobados = porcentajeReprobados;
            model.PorcentajeSinEvaluar = porcentajeSinEvaluar;

            var materias = await _subjectService.GetAllAsync();
            var materiasDesempeno = new List<MateriaDesempenoViewModel>();
            foreach (var materia in materias)
            {
                int estudiantesMateria = 0;
                int aprobadosMateria = 0;
                int reprobadosMateria = 0;
                double sumaPromedios = 0;
                int totalPromedios = 0;

                foreach (var estudiante in soloEstudiantes)
                {
                    if (!reportesPorEstudiante.TryGetValue(estudiante.Id, out var reporte))
                        continue;
                    var oficial = reporte.SubjectAverages?
                        .FirstOrDefault(a => string.Equals(a.Subject, materia.Name, StringComparison.OrdinalIgnoreCase));
                    if (oficial?.SubjectAverage == null)
                        continue;

                    estudiantesMateria++;
                    var promedioMateria = (double)oficial.SubjectAverage.Value;
                    sumaPromedios += promedioMateria;
                    totalPromedios++;
                    if (promedioMateria >= 3.0)
                        aprobadosMateria++;
                    else if (promedioMateria >= 1.0 && promedioMateria < 3.0)
                        reprobadosMateria++;
                }
                double promedioFinal = totalPromedios > 0 ? sumaPromedios / totalPromedios : 0;
                materiasDesempeno.Add(new MateriaDesempenoViewModel
                {
                    Nombre = materia.Name,
                    Estudiantes = estudiantesMateria,
                    Promedio = Math.Round(promedioFinal, 1),
                    Aprobados = aprobadosMateria,
                    Reprobados = reprobadosMateria,
                    ColorBarra = promedioFinal >= 4.0 ? "#27ae60" : "#f1c40f"
                });
            }

            var profesores = await _userService.GetAllWithAssignmentsByRoleAsync("teacher");
            var profesoresDesempeno = new List<ProfesorDesempenoViewModel>();
            foreach (var prof in profesores)
            {
                var asignaciones = prof.TeacherAssignments;
                if (asignaciones == null || asignaciones.Count == 0)
                    continue;

                var materiasProfesor = asignaciones.Select(a => a.SubjectAssignment?.Subject?.Name).Distinct().Where(n => !string.IsNullOrEmpty(n)).ToList();
                int profTotalEstudiantes = 0;
                double profSumaPromedios = 0;
                int profTotalPromedios = 0;
                int profTotalAprobados = 0;
                int profTotalReprobados = 0;
                DateTime? ultimaActividad = null;

                foreach (var materiaNombre in materiasProfesor)
                {
                    foreach (var reporte in reportesPorEstudiante.Values)
                    {
                        var notas = reporte.Grades.Where(g => g.Subject == materiaNombre && g.Teacher == prof.Name).ToList();
                        if (notas.Count > 0)
                        {
                            profTotalEstudiantes++;
                            var promedio = notas.Average(g => (double)g.Value);
                            profSumaPromedios += promedio;
                            profTotalPromedios++;
                            if (promedio >= 3.0)
                                profTotalAprobados++;
                            else if (promedio >= 1.0 && promedio < 3.0)
                                profTotalReprobados++;
                            var fechaUltima = notas.Max(g => g.CreatedAt);
                            if (!ultimaActividad.HasValue || fechaUltima > ultimaActividad)
                                ultimaActividad = fechaUltima;
                        }
                    }
                }
                double promedioGeneral = profTotalPromedios > 0 ? profSumaPromedios / profTotalPromedios : 0;
                double porcentajeAprobadosProf = profTotalEstudiantes > 0 ? (profTotalAprobados * 100.0 / profTotalEstudiantes) : 0;
                double porcentajeReprobadosProf = profTotalEstudiantes > 0 ? (profTotalReprobados * 100.0 / profTotalEstudiantes) : 0;
                string estado = "Crítico";
                if (porcentajeAprobadosProf >= 80) estado = "Excelente";
                else if (porcentajeAprobadosProf >= 60) estado = "Regular";

                profesoresDesempeno.Add(new ProfesorDesempenoViewModel
                {
                    Nombre = prof.Name,
                    Materia = string.Join(", ", materiasProfesor),
                    Desempeno = Math.Round(promedioGeneral, 1),
                    Estudiantes = profTotalEstudiantes,
                    Promedio = Math.Round(promedioGeneral, 1),
                    Aprobados = profTotalAprobados,
                    PorcentajeAprobados = porcentajeAprobadosProf,
                    Reprobados = profTotalReprobados,
                    PorcentajeReprobados = porcentajeReprobadosProf,
                    UltimaActividad = ultimaActividad ?? DateTime.MinValue,
                    Estado = estado
                });
            }

            double promedioGeneralActual = 0;
            int totalPromediosGlobal = 0;
            foreach (var reporte in reportesPorEstudiante.Values)
            {
                var promedio = OfficialStudentAverage(reporte);
                if (promedio.HasValue)
                {
                    promedioGeneralActual += promedio.Value;
                    totalPromediosGlobal++;
                }
            }
            promedioGeneralActual = totalPromediosGlobal > 0 ? promedioGeneralActual / totalPromediosGlobal : 0;

            var materiasAprobacion = new List<MateriaAprobacionViewModel>();
            foreach (var mat in materiasDesempeno)
            {
                var profesor = profesoresDesempeno.FirstOrDefault(p => p.Materia.Contains(mat.Nombre));
                double porcentajeAprobacion = mat.Estudiantes > 0 ? (mat.Aprobados * 100.0 / mat.Estudiantes) : 0;
                materiasAprobacion.Add(new MateriaAprobacionViewModel
                {
                    Nombre = mat.Nombre,
                    Profesor = profesor?.Nombre ?? "-",
                    TotalEstudiantes = mat.Estudiantes,
                    Aprobados = mat.Aprobados,
                    Reprobados = mat.Reprobados,
                    PorcentajeAprobacion = porcentajeAprobacion
                });
            }

            var recomendaciones = new List<string>();
            var materiaBajo = materiasDesempeno.OrderBy(m => m.Promedio).FirstOrDefault();
            var materiaAlto = materiasDesempeno.OrderByDescending(m => m.Promedio).FirstOrDefault();
            if (materiaBajo != null)
                recomendaciones.Add($"Implementar plan de refuerzo para la materia de {materiaBajo.Nombre}");
            if (materiaAlto != null)
                recomendaciones.Add($"Extender las estrategias exitosas de {materiaAlto.Nombre} a otras materias");
            var profDestacado = profesoresDesempeno.OrderByDescending(p => p.Desempeno).FirstOrDefault();
            if (profDestacado != null)
                recomendaciones.Add($"Reconocer el desempeño destacado del profesor {profDestacado.Nombre}");

            var alertas = new List<AlertaNotificacionViewModel>();
            foreach (var mat in materiasDesempeno)
            {
                if (mat.Promedio < 3.0)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Bajo",
                        Titulo = $"Bajo rendimiento en {mat.Nombre}",
                        Mensaje = $"El promedio de calificaciones en {mat.Nombre} está por debajo del objetivo."
                    });
                }
                if (mat.Estudiantes == 0)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Critico",
                        Titulo = $"Materia sin estudiantes: {mat.Nombre}",
                        Mensaje = $"No hay estudiantes inscritos en la materia {mat.Nombre}."
                    });
                }
                if (mat.Estudiantes > 0 && mat.Reprobados * 100.0 / mat.Estudiantes > 40)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Bajo",
                        Titulo = $"Alto porcentaje de reprobados en {mat.Nombre}",
                        Mensaje = $"Más del 40% de los estudiantes reprobaron {mat.Nombre}."
                    });
                }
            }
            foreach (var prof in profesoresDesempeno)
            {
                if (prof.Estado == "Excelente")
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Excelente",
                        Titulo = $"Excelente desempeño de {prof.Nombre}",
                        Mensaje = $"El profesor {prof.Nombre} mantiene un desempeño destacado en sus materias."
                    });
                }
                if (prof.Estudiantes == 0)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Critico",
                        Titulo = $"Profesor sin asignaciones: {prof.Nombre}",
                        Mensaje = $"El profesor {prof.Nombre} no tiene estudiantes asignados actualmente."
                    });
                }
            }
            alertas.Add(new AlertaNotificacionViewModel
            {
                Tipo = "Reporte",
                Titulo = "Reporte mensual disponible",
                Mensaje = "El reporte de desempeño docente de este mes está listo para su revisión."
            });

            model.MateriasDesempeno = materiasDesempeno;
            model.Profesores = profesoresDesempeno;
            model.TasaAprobacionGeneral = porcentajeAprobados;
            model.MateriasAprobacion = materiasAprobacion;
            model.Alertas = alertas;
            model.Recomendaciones = recomendaciones;

            return model;
        }

        public async Task<PagedResult<MateriaDesempenoViewModel>> GetMateriasDesempenoAsync(int page, int pageSize, string trimestre = null)
        {
            var school = await _currentUserService.GetCurrentUserSchoolAsync();
            var materias = await _context.Subjects
                .Where(s => school == null || s.SchoolId == school.Id || s.SchoolId == null)
                .OrderBy(s => s.Name)
                .ToListAsync();
            var official = await LoadOfficialSubjectScoresAsync(trimestre);

            var materiasPage = materias.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var result = materiasPage.Select(subject =>
            {
                var scores = official.Where(x => x.SubjectId == subject.Id).Select(x => x.Score).ToList();
                var promedio = scores.Count > 0 ? scores.Average() : (decimal?)null;
                return new MateriaDesempenoViewModel
                {
                    Nombre = subject.Name,
                    Estudiantes = scores.Count,
                    Promedio = promedio.HasValue ? Math.Round((double)promedio.Value, 1) : 0,
                    Aprobados = scores.Count(x => x >= 3.0m),
                    Reprobados = scores.Count(x => x < 3.0m && x >= 1.0m),
                    ColorBarra = promedio.HasValue && promedio.Value >= 4.0m ? "#27ae60" : "#f1c40f"
                };
            }).ToList();

            return new PagedResult<MateriaDesempenoViewModel>
            {
                Items = result,
                TotalCount = materias.Count
            };
        }

        public async Task<PagedResult<ProfesorDesempenoViewModel>> GetProfesoresDesempenoAsync(int page, int pageSize, string trimestre = null)
        {
            var profesores = await _context.Users
                .Where(u => u.Role.ToLower() == "teacher")
                .OrderBy(u => u.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var totalCount = await _context.Users.CountAsync(u => u.Role.ToLower() == "teacher");

            var scoreQuery = from score in _context.StudentActivityScores
                             join activity in _context.Activities on score.ActivityId equals activity.Id
                             where (trimestre == "todos" || activity.Trimester == trimestre)
                             select new { activity.TeacherId, activity.SubjectId, activity.CreatedAt, score.Score };
            var scoreList = await scoreQuery.ToListAsync();

            var subjectDict = await _context.Subjects.ToDictionaryAsync(s => s.Id, s => s.Name);

            var result = profesores.Select(prof => {
                var scores = scoreList.Where(x => x.TeacherId == prof.Id).ToList();
                var materias = scores.Where(x => x.SubjectId.HasValue)
                                   .Select(x => subjectDict.ContainsKey(x.SubjectId.Value) ? subjectDict[x.SubjectId.Value] : "")
                                   .Distinct();
                double promedio = scores.Any() ? (double)scores.Average(x => (decimal)x.Score) : 0;
                int aprobados = scores.Count(x => x.Score >= 3.0m);
                int reprobados = scores.Count(x => x.Score < 3.0m && x.Score >= 1.0m);
                int totalEstudiantes = scores.Count;
                double porcentajeAprobados = totalEstudiantes > 0 ? aprobados * 100.0 / totalEstudiantes : 0;
                double porcentajeReprobados = totalEstudiantes > 0 ? reprobados * 100.0 / totalEstudiantes : 0;
                string estado = "Crítico";
                if (porcentajeAprobados >= 80) estado = "Excelente";
                else if (porcentajeAprobados >= 60) estado = "Regular";
                return new ProfesorDesempenoViewModel
                {
                    Nombre = prof.Name,
                    Materia = string.Join(", ", materias),
                    Desempeno = Math.Round(promedio, 1),
                    Estudiantes = totalEstudiantes,
                    Promedio = Math.Round(promedio, 1),
                    Aprobados = aprobados,
                    PorcentajeAprobados = porcentajeAprobados,
                    Reprobados = reprobados,
                    PorcentajeReprobados = porcentajeReprobados,
                    UltimaActividad = scores.Any() ? scores.Max(x => x.CreatedAt ?? DateTime.MinValue) : DateTime.MinValue,
                    Estado = estado
                };
            }).ToList();

            return new PagedResult<ProfesorDesempenoViewModel>
            {
                Items = result,
                TotalCount = totalCount
            };
        }

        public async Task<PagedResult<MateriaAprobacionViewModel>> GetMateriasAprobacionAsync(int page, int pageSize, string trimestre = null)
        {
            var school = await _currentUserService.GetCurrentUserSchoolAsync();
            var materias = await _context.Subjects
                .Where(s => school == null || s.SchoolId == school.Id || s.SchoolId == null)
                .OrderBy(s => s.Name)
                .ToListAsync();
            var official = await LoadOfficialSubjectScoresAsync(trimestre);

            var teacherBySubject = await (
                from ta in _context.TeacherAssignments.AsNoTracking()
                join sa in _context.SubjectAssignments.AsNoTracking() on ta.SubjectAssignmentId equals sa.Id
                join u in _context.Users.AsNoTracking() on ta.TeacherId equals u.Id
                select new { sa.SubjectId, u.Name }
            ).ToListAsync();

            var materiasPage = materias.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var result = materiasPage.Select(subject =>
            {
                var scores = official.Where(x => x.SubjectId == subject.Id).Select(x => x.Score).ToList();
                var profesor = teacherBySubject.FirstOrDefault(t => t.SubjectId == subject.Id)?.Name ?? "-";
                int totalEstudiantes = scores.Count;
                int aprobados = scores.Count(x => x >= 3.0m);
                int reprobados = scores.Count(x => x < 3.0m && x >= 1.0m);
                return new MateriaAprobacionViewModel
                {
                    Nombre = subject.Name,
                    Profesor = profesor,
                    TotalEstudiantes = totalEstudiantes,
                    Aprobados = aprobados,
                    Reprobados = reprobados,
                    PorcentajeAprobacion = totalEstudiantes > 0 ? aprobados * 100.0 / totalEstudiantes : 0
                };
            }).ToList();

            return new PagedResult<MateriaAprobacionViewModel>
            {
                Items = result,
                TotalCount = materias.Count
            };
        }

        public async Task<PagedResult<AlertaNotificacionViewModel>> GetAlertasAsync(int page, int pageSize, string trimestre = null)
        {
            var school = await _currentUserService.GetCurrentUserSchoolAsync();
            var materias = await _context.Subjects
                .Where(s => school == null || s.SchoolId == school.Id || s.SchoolId == null)
                .OrderBy(s => s.Name)
                .ToListAsync();
            var official = await LoadOfficialSubjectScoresAsync(trimestre);

            var alertas = new List<AlertaNotificacionViewModel>();
            foreach (var subject in materias)
            {
                var scores = official.Where(x => x.SubjectId == subject.Id).Select(x => x.Score).ToList();
                double promedio = scores.Count > 0 ? (double)scores.Average() : 0;
                int totalEstudiantes = scores.Count;
                int reprobados = scores.Count(x => x < 3.0m && x >= 1.0m);
                if (scores.Any() && promedio < 3.0)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Bajo",
                        Titulo = $"Bajo rendimiento en {subject.Name}",
                        Mensaje = $"El promedio de calificaciones en {subject.Name} está por debajo del objetivo."
                    });
                }
                if (totalEstudiantes == 0)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Critico",
                        Titulo = $"Materia sin estudiantes: {subject.Name}",
                        Mensaje = $"No hay estudiantes inscritos en la materia {subject.Name}."
                    });
                }
                if (totalEstudiantes > 0 && reprobados * 100.0 / totalEstudiantes > 40)
                {
                    alertas.Add(new AlertaNotificacionViewModel
                    {
                        Tipo = "Bajo",
                        Titulo = $"Alto porcentaje de reprobados en {subject.Name}",
                        Mensaje = $"Más del 40% de los estudiantes reprobaron {subject.Name}."
                    });
                }
            }
            // Alerta de reporte mensual (solo una vez por página)
            if (page == 1)
            {
                alertas.Add(new AlertaNotificacionViewModel
                {
                    Tipo = "Reporte",
                    Titulo = "Reporte mensual disponible",
                    Mensaje = "El reporte de desempeño docente de este mes está listo para su revisión."
                });
            }
            var pagedAlertas = alertas.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<AlertaNotificacionViewModel>
            {
                Items = pagedAlertas,
                TotalCount = alertas.Count
            };
        }

        public async Task<DirectorViewModel> GetInitialTotalsAsync()
        {
            var model = new DirectorViewModel();
            
            // Obtener trimestres (necesario para el dropdown)
            model.TrimestresDisponibles = await _trimesterService.GetAllAsync();
            model.TrimestreSeleccionado = "";

            var school = await _currentUserService.GetCurrentUserSchoolAsync();
            if (school == null)
            {
                model.TotalEstudiantes = 0;
                model.TotalAprobados = 0;
                model.TotalReprobados = 0;
                model.TotalSinEvaluar = 0;
            }
            else
            {
                var studentIds = await _context.Users
                    .Where(u => u.SchoolId == school.Id &&
                                (u.Role.ToLower() == "estudiante" ||
                                 u.Role.ToLower() == "student" ||
                                 u.Role.ToLower() == "alumno"))
                    .Select(u => u.Id)
                    .ToListAsync();
                var official = await LoadOfficialSubjectScoresAsync(null);
                var byStudent = official.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.Select(x => x.Score).ToList());

                int aprobados = 0, reprobados = 0, sinEvaluar = 0;
                foreach (var studentId in studentIds)
                {
                    if (!byStudent.TryGetValue(studentId, out var scores) || scores.Count == 0)
                    {
                        sinEvaluar++;
                        continue;
                    }

                    var promedio = scores.Average();
                    if (promedio >= 3.0m) aprobados++;
                    else if (promedio >= 1.0m) reprobados++;
                    else sinEvaluar++;
                }

                model.TotalEstudiantes = studentIds.Count;
                model.TotalAprobados = aprobados;
                model.TotalReprobados = reprobados;
                model.TotalSinEvaluar = sinEvaluar;
            }

            // Calcular porcentajes
            model.PorcentajeAprobados = model.TotalEstudiantes > 0 
                ? (model.TotalAprobados * 100.0 / model.TotalEstudiantes) 
                : 0;
            model.PorcentajeReprobados = model.TotalEstudiantes > 0 
                ? (model.TotalReprobados * 100.0 / model.TotalEstudiantes) 
                : 0;
            model.PorcentajeSinEvaluar = model.TotalEstudiantes > 0 
                ? (model.TotalSinEvaluar * 100.0 / model.TotalEstudiantes) 
                : 0;

            // Calcular tasa de aprobación general
            model.TasaAprobacionGeneral = model.PorcentajeAprobados;

            var officialTrend = await LoadOfficialSubjectScoresAsync(null);
            var materias = officialTrend
                .GroupBy(x => x.SubjectName)
                .Select(g => new
                {
                    Materia = g.Key,
                    Promedio = (double)g.Average(s => s.Score),
                    TotalEstudiantes = g.Select(s => s.StudentId).Distinct().Count(),
                    Aprobados = g.Count(s => s.Score >= 3.0m),
                    Reprobados = g.Count(s => s.Score < 3.0m)
                })
                .ToList();
            var materiasOrdenadas = materias.OrderByDescending(m => m.Promedio).ToList();

            // Generar análisis de tendencia
            var analisis = new System.Text.StringBuilder();
            analisis.Append("Se observa ");

            var materiasDestacadas = materiasOrdenadas.Where(m => m.Promedio >= 4.0).ToList();
            var materiasCriticas = materiasOrdenadas.Where(m => m.Promedio < 3.0).ToList();

            if (materiasDestacadas.Any())
            {
                analisis.Append($"un desempeño sobresaliente en {string.Join(", ", materiasDestacadas.Select(m => m.Materia))}, ");
            }

            if (materiasCriticas.Any())
            {
                analisis.Append($"mientras que {string.Join(", ", materiasCriticas.Select(m => m.Materia))} requieren atención inmediata. ");
            }

            model.AnalisisTendencia = analisis.ToString().Trim();

            // Generar recomendaciones
            var recomendaciones = new List<string>();

            // Recomendaciones basadas en materias críticas
            foreach (var materia in materiasCriticas)
            {
                recomendaciones.Add($"Implementar plan de refuerzo para la materia de {materia.Materia}");
            }

            // Recomendaciones basadas en materias destacadas
            foreach (var materia in materiasDestacadas)
            {
                recomendaciones.Add($"Extender las estrategias exitosas de {materia.Materia} a otras materias");
            }

            // Recomendaciones generales si hay materias con alto índice de reprobación
            var materiasAltoIndiceReprobacion = materias.Where(m => m.TotalEstudiantes > 0 && 
                (double)m.Reprobados / m.TotalEstudiantes > 0.4).ToList();

            foreach (var materia in materiasAltoIndiceReprobacion)
            {
                recomendaciones.Add($"Realizar seguimiento especial en {materia.Materia} por alto índice de reprobación");
            }

            model.Recomendaciones = recomendaciones;

            // Inicializar listas vacías para carga posterior vía AJAX
            model.MateriasDesempeno = new List<MateriaDesempenoViewModel>();
            model.Profesores = new List<ProfesorDesempenoViewModel>();
            model.MateriasAprobacion = new List<MateriaAprobacionViewModel>();
            model.Alertas = new List<AlertaNotificacionViewModel>();

            return model;
        }

        private static double? OfficialStudentAverage(SchoolManager.Dtos.StudentReportDto reporte)
        {
            if (reporte.TrimesterAverage.HasValue)
                return (double)reporte.TrimesterAverage.Value;

            var official = (reporte.SubjectAverages ?? new List<SchoolManager.Dtos.SubjectTrimesterAverageDto>())
                .Where(a => a.SubjectAverage.HasValue)
                .Select(a => a.SubjectAverage!.Value)
                .ToList();
            return official.Count == 0 ? null : (double)official.Average();
        }

        private async Task<List<OfficialSubjectScore>> LoadOfficialSubjectScoresAsync(string? trimestre)
        {
            var school = await _currentUserService.GetCurrentUserSchoolAsync();
            if (school == null)
                return new List<OfficialSubjectScore>();

            var year = await _academicYearService.GetActiveAcademicYearAsync(school.Id);
            var yearId = year?.Id;
            var trimesterCode = string.IsNullOrWhiteSpace(trimestre) || trimestre == "todos"
                ? null
                : OfficialGradeService.NormalizeTrimester(trimestre);

            var studentIds = await _context.Users.AsNoTracking()
                .Where(u => u.SchoolId == school.Id &&
                            (u.Role.ToLower() == "estudiante" ||
                             u.Role.ToLower() == "student" ||
                             u.Role.ToLower() == "alumno"))
                .Select(u => u.Id)
                .ToListAsync();
            if (studentIds.Count == 0)
                return new List<OfficialSubjectScore>();

            var enrollments = await (
                from ssa in _context.StudentSubjectAssignments.AsNoTracking()
                join sa in _context.SubjectAssignments.AsNoTracking() on ssa.SubjectAssignmentId equals sa.Id
                join sub in _context.Subjects.AsNoTracking() on sa.SubjectId equals sub.Id
                where ssa.IsActive && studentIds.Contains(ssa.StudentId)
                select new { ssa.Id, ssa.StudentId, sa.SubjectId, SubjectName = sub.Name }
            ).ToListAsync();

            var imported = yearId.HasValue
                ? await _officialGradeService.GetImportedForStudentsAsync(studentIds)
                : Array.Empty<ImportedOfficialGradeRow>();

            var activityRows = await (
                from sas in _context.StudentActivityScores.AsNoTracking()
                join a in _context.Activities.AsNoTracking() on sas.ActivityId equals a.Id
                where studentIds.Contains(sas.StudentId)
                select new
                {
                    sas.StudentId,
                    sas.StudentSubjectAssignmentId,
                    sas.AcademicYearId,
                    a.SubjectId,
                    a.Trimester,
                    a.Type,
                    sas.Score
                }
            ).ToListAsync();

            if (yearId.HasValue)
                activityRows = activityRows.Where(x => x.AcademicYearId == yearId.Value || x.AcademicYearId == null).ToList();
            if (trimesterCode != null)
                activityRows = activityRows.Where(x => OfficialGradeService.NormalizeTrimester(x.Trimester) == trimesterCode).ToList();

            var contextActivities = await _context.Activities.AsNoTracking()
                .Select(a => new { a.SubjectId, a.Trimester, a.Type })
                .ToListAsync();

            var result = new List<OfficialSubjectScore>();
            foreach (var enrollment in enrollments)
            {
                decimal? OfficialFor(string code)
                {
                    var importedHit = yearId.HasValue
                        ? imported.FirstOrDefault(r =>
                            r.StudentId == enrollment.StudentId &&
                            r.StudentSubjectAssignmentId == enrollment.Id &&
                            r.AcademicYearId == yearId.Value &&
                            r.TrimesterCode == code)
                        : null;
                    if (importedHit != null)
                        return importedHit.Score;

                    var pairs = activityRows
                        .Where(x =>
                            x.StudentId == enrollment.StudentId &&
                            OfficialGradeService.NormalizeTrimester(x.Trimester) == code &&
                            ((x.StudentSubjectAssignmentId.HasValue && x.StudentSubjectAssignmentId.Value == enrollment.Id)
                             || (!x.StudentSubjectAssignmentId.HasValue && x.SubjectId == enrollment.SubjectId)))
                        .Select(x => ((string?)x.Type, x.Score));
                    var contextTypes = contextActivities
                        .Where(a => a.SubjectId == enrollment.SubjectId
                                    && OfficialGradeService.NormalizeTrimester(a.Trimester) == code)
                        .Select(a => (string?)a.Type);
                    return _officialGradeService.CalculateFromActivities(pairs, year?.Name, code, contextTypes).Score;
                }

                decimal? score = trimesterCode == null
                    ? OfficialTrimesterAverageCalculator.ComputeFinalAverage(OfficialFor("1T"), OfficialFor("2T"), OfficialFor("3T"))
                    : OfficialFor(trimesterCode);

                if (score.HasValue)
                {
                    result.Add(new OfficialSubjectScore
                    {
                        StudentId = enrollment.StudentId,
                        SubjectId = enrollment.SubjectId,
                        SubjectName = enrollment.SubjectName,
                        Score = score.Value
                    });
                }
            }

            return result;
        }

        private sealed class OfficialSubjectScore
        {
            public Guid StudentId { get; set; }
            public Guid SubjectId { get; set; }
            public string SubjectName { get; set; } = string.Empty;
            public decimal Score { get; set; }
        }
    }
} 