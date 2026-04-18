using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using System.Text;

namespace SchoolManager.Services.Implementations
{
    public class AprobadosReprobadosService : IAprobadosReprobadosService
    {
        private readonly SchoolDbContext _context;
        private readonly ILogger<AprobadosReprobadosService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private const decimal NOTA_MINIMA_APROBACION = 3.0m; // Escala 0-5, nota mínima para aprobar es 3.0

        public AprobadosReprobadosService(
            SchoolDbContext context,
            ILogger<AprobadosReprobadosService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Retorna los IDs de los turnos cuyo nombre indica jornada nocturna.
        /// Punto único de detección para evitar duplicación de la lógica de nombre de turno.
        /// Si en el futuro se renombra el turno, solo hay que ajustar este método.
        /// </summary>
        private async Task<List<Guid>> GetNightShiftIdsAsync()
        {
            return await _context.Shifts
                .AsNoTracking()
                .Where(s => s.Name.ToLower().Contains("noche"))
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<AprobadosReprobadosReportViewModel> GenerarReporteAsync(
            Guid schoolId,
            string trimestre,
            string nivelEducativo,
            string? gradoEspecifico = null,
            string? grupoEspecifico = null,
            Guid? especialidadId = null,
            Guid? areaId = null,
            Guid? materiaId = null)
        {
            try
            {
                _logger.LogInformation("📊 Generando reporte de aprobados/reprobados - School: {SchoolId}, Trimestre: {Trimestre}, Nivel: {Nivel}",
                    schoolId, trimestre, nivelEducativo);

                // Obtener información de la escuela
                var school = await _context.Schools.FindAsync(schoolId);
                if (school == null)
                    throw new Exception("Escuela no encontrada");

                // Resolver trimestre por nombre para filtrar por TrimesterId (actividades del 3T, etc.)
                var trimesterId = await _context.Trimesters
                    .Where(t => t.SchoolId == schoolId && t.Name == trimestre)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync();

                var estadisticas = await ConstruirEstadisticasPorNivelAsync(
                    schoolId,
                    trimestre,
                    trimesterId,
                    nivelEducativo,
                    gradoEspecifico,
                    grupoEspecifico,
                    especialidadId,
                    areaId,
                    materiaId);

                if (!estadisticas.Any())
                {
                    _logger.LogWarning("No se encontraron estadísticas para los filtros aplicados - School: {SchoolId}, Trimestre: {Trimestre}, Nivel: {Nivel}", 
                        schoolId, trimestre, nivelEducativo);
                }

                // Calcular totales generales
                var totales = CalcularTotalesGenerales(estadisticas);

                // Obtener año lectivo actual (usar UTC para consistencia)
                var fechaActual = DateTime.UtcNow;
                var anoLectivo = fechaActual.Year.ToString();

                var reporte = new AprobadosReprobadosReportViewModel
                {
                    InstitutoNombre = school.Name,
                    LogoUrl = school.LogoUrl ?? "",
                    ProfesorCoordinador = "", // Se llenará desde el controlador con el usuario actual
                    Trimestre = trimestre,
                    AnoLectivo = anoLectivo,
                    NivelEducativo = nivelEducativo,
                    FechaGeneracion = fechaActual,
                    Estadisticas = estadisticas.OrderBy(e => e.Grado).ThenBy(e => e.Grupo).ToList(),
                    TotalesGenerales = totales,
                    TrimestresDisponibles = await ObtenerTrimestresDisponiblesAsync(schoolId),
                    NivelesDisponibles = await ObtenerNivelesEducativosAsync(schoolId)
                };

                _logger.LogInformation("✅ Reporte generado exitosamente con {Count} grupos", estadisticas.Count);
                return reporte;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generando reporte de aprobados/reprobados");
                throw;
            }
        }

        private async Task<(int Total, int Aprobados, decimal PorcentajeAprobados,
            int Reprobados, decimal PorcentajeReprobados,
            int ReprobadosHastaLaFecha, decimal PorcentajeReprobadosHastaLaFecha,
            int SinCalificaciones, decimal PorcentajeSinCalificaciones,
            int Retirados, decimal PorcentajeRetirados)>
            CalcularEstadisticasGrupoAsync(Guid grupoId, string trimestre, Guid? trimesterId, Guid? materiaId = null, Guid? areaId = null, Guid? especialidadId = null)
        {
            _logger.LogInformation("Calculando estadísticas para grupo {GrupoId}, trimestre {Trimestre}", grupoId, trimestre);

            var subjectAssignmentIdsDelGrupo = await _context.SubjectAssignments
                .Where(sa => sa.GroupId == grupoId)
                .Select(sa => sa.Id)
                .ToListAsync();

            var estudiantesDelGrupo = await _context.StudentSubjectAssignments
                .Where(ssa => ssa.IsActive && subjectAssignmentIdsDelGrupo.Contains(ssa.SubjectAssignmentId))
                .Select(ssa => ssa.StudentId)
                .Distinct()
                .ToListAsync();

            if (!estudiantesDelGrupo.Any())
            {
                // Compatibilidad hacia atrás: si aún no hay inscripciones por asignatura, usar matrícula por grupo.
                estudiantesDelGrupo = await _context.StudentAssignments
                    .Where(sa => sa.GroupId == grupoId && sa.IsActive)
                    .Select(sa => sa.StudentId)
                    .Distinct()
                    .ToListAsync();
            }

            int total = estudiantesDelGrupo.Count;
            if (total == 0)
            {
                return (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            // Una sola consulta: estados de usuarios (retirados), comparación insensible a mayúsculas
            var usuariosConStatus = await _context.Users
                .Where(u => estudiantesDelGrupo.Contains(u.Id))
                .Select(u => new { u.Id, u.Status })
                .ToListAsync();
            var setRetirados = usuariosConStatus
                .Where(u => string.Equals(u.Status, "inactive", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(u.Status, "retirado", StringComparison.OrdinalIgnoreCase))
                .Select(u => u.Id)
                .ToHashSet();

            // Una sola consulta: todas las calificaciones del trimestre para estos estudiantes (con Activity)
            var queryScores = _context.StudentActivityScores
                .Include(sas => sas.Activity)
                    .ThenInclude(a => a!.Subject)
                        .ThenInclude(s => s!.Area)
                .Where(sas => estudiantesDelGrupo.Contains(sas.StudentId) &&
                    sas.Activity != null &&
                    sas.Activity.GroupId == grupoId &&
                    (trimesterId.HasValue
                        ? (sas.Activity.TrimesterId == trimesterId || sas.Activity.Trimester == trimestre)
                        : sas.Activity.Trimester == trimestre));
            if (materiaId.HasValue)
                queryScores = queryScores.Where(sas => sas.Activity!.SubjectId == materiaId.Value);
            if (areaId.HasValue)
                queryScores = queryScores.Where(sas => sas.Activity!.Subject!.AreaId == areaId.Value);
            if (especialidadId.HasValue)
            {
                var subjectIdsEspecialidad = await _context.SubjectAssignments
                    .Where(sa => sa.SpecialtyId == especialidadId.Value)
                    .Select(sa => sa.SubjectId)
                    .ToListAsync();
                queryScores = queryScores.Where(sas => sas.Activity!.SubjectId.HasValue && subjectIdsEspecialidad.Contains(sas.Activity.SubjectId.Value));
            }
            var todasCalificaciones = await queryScores.ToListAsync();

            int aprobados = 0, reprobados = 0, reprobadosHastaLaFecha = 0, sinCalificaciones = 0, retirados = 0;
            var calificacionesPorEstudiante = todasCalificaciones.GroupBy(c => c.StudentId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var estudianteId in estudiantesDelGrupo)
            {
                if (setRetirados.Contains(estudianteId))
                {
                    retirados++;
                    continue;
                }
                if (!calificacionesPorEstudiante.TryGetValue(estudianteId, out var calificaciones) || !calificaciones.Any())
                {
                    sinCalificaciones++;
                    continue;
                }
                var materias = calificaciones
                    .GroupBy(c => c.Activity!.SubjectId)
                    .Select(g => new { SubjectId = g.Key, PromedioMateria = g.Average(c => c.Score ?? 0) })
                    .ToList();
                if (!materias.Any())
                {
                    sinCalificaciones++;
                    continue;
                }
                var promedioGeneral = materias.Average(m => m.PromedioMateria);
                var materiasReprobadas = materias.Count(m => m.PromedioMateria < NOTA_MINIMA_APROBACION);
                if (materiasReprobadas > 0)
                {
                    reprobadosHastaLaFecha++;
                    if (materiasReprobadas >= 3) reprobados++;
                }
                else if (promedioGeneral >= NOTA_MINIMA_APROBACION)
                {
                    aprobados++;
                }
            }

            decimal porcentajeAprobados = total > 0 ? (aprobados * 100m / total) : 0;
            decimal porcentajeReprobados = total > 0 ? (reprobados * 100m / total) : 0;
            decimal porcentajeReprobadosHastaLaFecha = total > 0 ? (reprobadosHastaLaFecha * 100m / total) : 0;
            decimal porcentajeSinCalificaciones = total > 0 ? (sinCalificaciones * 100m / total) : 0;
            decimal porcentajeRetirados = total > 0 ? (retirados * 100m / total) : 0;

            _logger.LogInformation("Estadísticas calculadas - Total: {Total}, Aprobados: {Aprobados}, Reprobados: {Reprobados}, Sin Calificaciones: {SinCalificaciones}, Retirados: {Retirados}",
                total, aprobados, reprobados, sinCalificaciones, retirados);

            return (total, aprobados, porcentajeAprobados,
                    reprobados, porcentajeReprobados,
                    reprobadosHastaLaFecha, porcentajeReprobadosHastaLaFecha,
                    sinCalificaciones, porcentajeSinCalificaciones,
                    retirados, porcentajeRetirados);
        }

        private TotalesGeneralesDto CalcularTotalesGenerales(List<GradoEstadisticaDto> estadisticas)
        {
            var total = estadisticas.Sum(e => e.TotalEstudiantes);
            var aprobados = estadisticas.Sum(e => e.Aprobados);
            var reprobados = estadisticas.Sum(e => e.Reprobados);
            var reprobadosHastaLaFecha = estadisticas.Sum(e => e.ReprobadosHastaLaFecha);
            var sinCalificaciones = estadisticas.Sum(e => e.SinCalificaciones);
            var retirados = estadisticas.Sum(e => e.Retirados);

            return new TotalesGeneralesDto
            {
                TotalEstudiantes = total,
                TotalAprobados = aprobados,
                PorcentajeAprobados = total > 0 ? Math.Round(aprobados * 100m / total, 2) : 0,
                TotalReprobados = reprobados,
                PorcentajeReprobados = total > 0 ? Math.Round(reprobados * 100m / total, 2) : 0,
                TotalReprobadosHastaLaFecha = reprobadosHastaLaFecha,
                PorcentajeReprobadosHastaLaFecha = total > 0 ? Math.Round(reprobadosHastaLaFecha * 100m / total, 2) : 0,
                TotalSinCalificaciones = sinCalificaciones,
                PorcentajeSinCalificaciones = total > 0 ? Math.Round(sinCalificaciones * 100m / total, 2) : 0,
                TotalRetirados = retirados,
                PorcentajeRetirados = total > 0 ? Math.Round(retirados * 100m / total, 2) : 0
            };
        }

        private async Task<List<string>> ObtenerGradosDesdeGruposNocturnosAsync(Guid schoolId)
        {
            var nightIds = await GetNightShiftIdsAsync();
            if (nightIds.Count == 0)
                return new List<string>();

            var fromGroups = await _context.Groups.AsNoTracking()
                .Where(g => g.SchoolId == schoolId && g.ShiftId != null && nightIds.Contains(g.ShiftId.Value)
                    && g.Grade != null && g.Grade != "")
                .Select(g => g.Grade!)
                .Distinct()
                .ToListAsync();

            if (fromGroups.Any())
                return fromGroups.OrderBy(x => x).ToList();

            return await _context.SubjectAssignments.AsNoTracking()
                .Where(sa => sa.Group.SchoolId == schoolId && sa.Group.ShiftId != null && nightIds.Contains(sa.Group.ShiftId.Value))
                .Select(sa => sa.GradeLevel.Name)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }

        /// <summary>Grados reales de la escuela según oferta (subject_assignments) y respaldo por groups.grade.</summary>
        private async Task<List<string>> ObtenerGradosPorNivelEscuelaAsync(Guid schoolId, string nivelEducativo)
        {
            var nivel = nivelEducativo.Trim().ToLowerInvariant();
            var desdeOferta = await _context.SubjectAssignments
                .AsNoTracking()
                .Where(sa => sa.Group.SchoolId == schoolId)
                .Select(sa => sa.GradeLevel.Name)
                .Distinct()
                .ToListAsync();

            if (!desdeOferta.Any())
            {
                desdeOferta = await _context.Groups
                    .AsNoTracking()
                    .Where(g => g.SchoolId == schoolId && g.Grade != null && g.Grade != "")
                    .Select(g => g.Grade!)
                    .Distinct()
                    .ToListAsync();
            }

            static bool EsPremediaNombre(string n)
            {
                var t = n.Trim().ToLowerInvariant();
                return t.StartsWith("7", StringComparison.Ordinal) ||
                       t.StartsWith("8", StringComparison.Ordinal) ||
                       t.StartsWith("9", StringComparison.Ordinal);
            }

            static bool EsMediaNombre(string n)
            {
                var t = n.Trim().ToLowerInvariant();
                return t.StartsWith("10", StringComparison.Ordinal) ||
                       t.StartsWith("11", StringComparison.Ordinal) ||
                       t.StartsWith("12", StringComparison.Ordinal);
            }

            if (nivel is "nocturna" or "nocturno")
                return await ObtenerGradosDesdeGruposNocturnosAsync(schoolId);

            return nivel switch
            {
                "premedia" => desdeOferta.Where(EsPremediaNombre).OrderBy(x => x).ToList(),
                "media" => desdeOferta.Where(EsMediaNombre).OrderBy(x => x).ToList(),
                "todos" or "todo" or "todas" => desdeOferta.OrderBy(x => x).ToList(),
                _ => desdeOferta.OrderBy(x => x).ToList()
            };
        }

        private async Task<List<(Guid GroupId, string GroupName, string GradoEtiqueta)>> ListarGruposPorFiltroAsync(
            Guid schoolId,
            string nivelEducativo,
            string? gradoEspecifico,
            string? grupoEspecifico)
        {
            var nivel = nivelEducativo.Trim().ToLowerInvariant();
            IQueryable<Group> q = _context.Groups.AsNoTracking().Where(g => g.SchoolId == schoolId);

            if (nivel is "nocturna" or "nocturno")
            {
                var nightIds = await GetNightShiftIdsAsync();
                q = q.Where(g => g.ShiftId != null && nightIds.Contains(g.ShiftId.Value));
            }

            if (!string.IsNullOrWhiteSpace(grupoEspecifico))
                q = q.Where(g => g.Name == grupoEspecifico);

            var grupos = await q.OrderBy(g => g.Name).ToListAsync();

            var resultado = new List<(Guid GroupId, string GroupName, string GradoEtiqueta)>();
            foreach (var g in grupos)
            {
                var etiqueta = g.Grade;
                if (string.IsNullOrWhiteSpace(etiqueta))
                {
                    etiqueta = await _context.SubjectAssignments
                        .AsNoTracking()
                        .Where(sa => sa.GroupId == g.Id)
                        .Select(sa => sa.GradeLevel.Name)
                        .FirstOrDefaultAsync() ?? "—";
                }

                if (!string.IsNullOrWhiteSpace(gradoEspecifico) &&
                    !string.Equals(etiqueta, gradoEspecifico, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (nivel is "premedia" or "media")
                {
                    var gradosNivel = await ObtenerGradosPorNivelEscuelaAsync(schoolId, nivel);
                    if (gradosNivel.Any() && !gradosNivel.Contains(etiqueta, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

                resultado.Add((g.Id, g.Name, etiqueta));
            }

            return resultado;
        }

        private async Task<List<GradoEstadisticaDto>> ConstruirEstadisticasPorNivelAsync(
            Guid schoolId,
            string trimestre,
            Guid? trimesterId,
            string nivelEducativo,
            string? gradoEspecifico,
            string? grupoEspecifico,
            Guid? especialidadId,
            Guid? areaId,
            Guid? materiaId)
        {
            var estadisticas = new List<GradoEstadisticaDto>();
            var nivel = nivelEducativo.Trim().ToLowerInvariant();

            if (nivel is "todos" or "todo" or "todas" or "nocturna" or "nocturno")
            {
                var filas = await ListarGruposPorFiltroAsync(schoolId, nivelEducativo, gradoEspecifico, grupoEspecifico);
                foreach (var (groupId, groupName, gradoEtiqueta) in filas)
                {
                    var stats = await CalcularEstadisticasGrupoAsync(groupId, trimestre, trimesterId, materiaId, areaId, especialidadId);
                    estadisticas.Add(new GradoEstadisticaDto
                    {
                        Grado = gradoEtiqueta,
                        Grupo = groupName,
                        TotalEstudiantes = stats.Total,
                        Aprobados = stats.Aprobados,
                        PorcentajeAprobados = stats.PorcentajeAprobados,
                        Reprobados = stats.Reprobados,
                        PorcentajeReprobados = stats.PorcentajeReprobados,
                        ReprobadosHastaLaFecha = stats.ReprobadosHastaLaFecha,
                        PorcentajeReprobadosHastaLaFecha = stats.PorcentajeReprobadosHastaLaFecha,
                        SinCalificaciones = stats.SinCalificaciones,
                        PorcentajeSinCalificaciones = stats.PorcentajeSinCalificaciones,
                        Retirados = stats.Retirados,
                        PorcentajeRetirados = stats.PorcentajeRetirados
                    });
                }

                return estadisticas;
            }

            var grados = await ObtenerGradosPorNivelEscuelaAsync(schoolId, nivelEducativo);
            if (!grados.Any())
            {
                _logger.LogWarning("No hay grados resueltos en BD para nivel {Nivel} escuela {SchoolId}", nivelEducativo, schoolId);
                return estadisticas;
            }

            foreach (var grado in grados)
            {
                if (!string.IsNullOrEmpty(gradoEspecifico) && grado != gradoEspecifico)
                    continue;

                var idsPorOferta = await _context.SubjectAssignments
                    .AsNoTracking()
                    .Where(sa => sa.Group.SchoolId == schoolId && sa.GradeLevel.Name == grado)
                    .Select(sa => sa.GroupId)
                    .Distinct()
                    .ToListAsync();

                var idsPorTexto = await _context.Groups
                    .AsNoTracking()
                    .Where(g => g.SchoolId == schoolId && g.Grade == grado)
                    .Select(g => g.Id)
                    .ToListAsync();

                var idsGrupo = idsPorOferta.Union(idsPorTexto).Distinct().ToList();

                if (!string.IsNullOrEmpty(grupoEspecifico))
                {
                    idsGrupo = await _context.Groups
                        .AsNoTracking()
                        .Where(g => idsGrupo.Contains(g.Id) && g.Name == grupoEspecifico)
                        .Select(g => g.Id)
                        .ToListAsync();
                }

                foreach (var gid in idsGrupo)
                {
                    var grupo = await _context.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gid);
                    if (grupo == null)
                        continue;

                    var stats = await CalcularEstadisticasGrupoAsync(grupo.Id, trimestre, trimesterId, materiaId, areaId, especialidadId);
                    estadisticas.Add(new GradoEstadisticaDto
                    {
                        Grado = grado,
                        Grupo = grupo.Name,
                        TotalEstudiantes = stats.Total,
                        Aprobados = stats.Aprobados,
                        PorcentajeAprobados = stats.PorcentajeAprobados,
                        Reprobados = stats.Reprobados,
                        PorcentajeReprobados = stats.PorcentajeReprobados,
                        ReprobadosHastaLaFecha = stats.ReprobadosHastaLaFecha,
                        PorcentajeReprobadosHastaLaFecha = stats.PorcentajeReprobadosHastaLaFecha,
                        SinCalificaciones = stats.SinCalificaciones,
                        PorcentajeSinCalificaciones = stats.PorcentajeSinCalificaciones,
                        Retirados = stats.Retirados,
                        PorcentajeRetirados = stats.PorcentajeRetirados
                    });
                }
            }

            return estadisticas;
        }

        public async Task<List<string>> ObtenerTrimestresDisponiblesAsync(Guid schoolId)
        {
            try
            {
                // Usar la tabla trimester de la escuela (1T, 2T, 3T) para que el reporte funcione
                // aunque solo esté activo un trimestre o las actividades usen TrimesterId
                var trimestres = await _context.Trimesters
                    .Where(t => t.SchoolId == schoolId)
                    .OrderBy(t => t.Order)
                    .Select(t => t.Name)
                    .ToListAsync();

                _logger.LogInformation("Trimestres disponibles para escuela {SchoolId}: {Trimestres}",
                    schoolId, string.Join(", ", trimestres));

                return trimestres;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo trimestres para escuela {SchoolId}", schoolId);
                return new List<string>();
            }
        }

        public async Task<List<string>> ObtenerNivelesEducativosAsync(Guid schoolId)
        {
            var niveles = new List<string> { "Todos", "Premedia", "Media" };
            var nightShiftIds = await GetNightShiftIdsAsync();
            var hayNocturna = nightShiftIds.Count > 0 && await _context.Groups.AsNoTracking()
                .AnyAsync(g => g.SchoolId == schoolId && g.ShiftId != null && nightShiftIds.Contains(g.ShiftId.Value));
            if (hayNocturna)
                niveles.Insert(1, "Nocturna");
            return niveles;
        }

        /// <summary>Grados para el filtro opcional del reporte, alineados con la lógica de <see cref="ConstruirEstadisticasPorNivelAsync"/>.</summary>
        public Task<List<string>> ObtenerGradosParaFiltroReporteAsync(Guid schoolId, string nivelEducativo) =>
            ObtenerGradosPorNivelEscuelaAsync(schoolId, nivelEducativo);

        public async Task<List<(Guid Id, string Nombre)>> ObtenerEspecialidadesAsync(Guid schoolId)
        {
            try
            {
                var especialidades = await _context.Specialties
                    .Where(s => s.SchoolId == schoolId || s.SchoolId == null)
                    .OrderBy(s => s.Name)
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync();

                return especialidades.Select(e => (e.Id, e.Name)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo especialidades");
                return new List<(Guid, string)>();
            }
        }

        public async Task<List<(Guid Id, string Nombre)>> ObtenerAreasAsync()
        {
            try
            {
                var areas = await _context.Areas
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.DisplayOrder)
                    .ThenBy(a => a.Name)
                    .Select(a => new { a.Id, a.Name })
                    .ToListAsync();

                return areas.Select(a => (a.Id, a.Name)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo áreas");
                return new List<(Guid, string)>();
            }
        }

        public async Task<List<(Guid Id, string Nombre)>> ObtenerMateriasAsync(Guid schoolId, Guid? areaId = null, Guid? especialidadId = null)
        {
            try
            {
                var query = _context.Subjects
                    .Where(s => s.SchoolId == schoolId && s.Status == true);

                // Filtrar por área si se especifica
                if (areaId.HasValue)
                {
                    query = query.Where(s => s.AreaId == areaId.Value);
                }

                // Filtrar por especialidad si se especifica
                if (especialidadId.HasValue)
                {
                    var materiasDeEspecialidad = _context.SubjectAssignments
                        .Where(sa => sa.SpecialtyId == especialidadId.Value)
                        .Select(sa => sa.SubjectId)
                        .Distinct();

                    query = query.Where(s => materiasDeEspecialidad.Contains(s.Id));
                }

                var materias = await query
                    .OrderBy(s => s.Name)
                    .Select(s => new { s.Id, s.Name })
                    .ToListAsync();

                return materias.Select(m => (m.Id, m.Name)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo materias");
                return new List<(Guid, string)>();
            }
        }

        #region PDF institucional – solo capa visual (read-only, sin lógica de negocio)

        private const string PdfColorInstitutional = "#1f4e79";
        private const string PdfColorTotalRow = "#1a3a52";
        private const string PdfColorAprobacionFuerte = "#153a5e";
        private const string PdfColorAprobados = "#2e7d32";
        private const string PdfColorReprobados = "#c62828";
        private const float PdfMarginCm = 1.5f;
        private const float PdfLogoHeightPt = 60f;
        private const float PdfHeaderLinePt = 2f;
        private const float PdfFontSizeTablePt = 11.5f;

        /// <summary>Genera el PDF del reporte. Solo representación visual; no altera estado ni datos. Si logoBytes se pasa, se usa en lugar de descargar por URL.</summary>
        public async Task<byte[]> ExportarAPdfAsync(AprobadosReprobadosReportViewModel reporte, byte[]? logoBytes = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            if (logoBytes == null)
                logoBytes = await TryDownloadLogoAsync(reporte.LogoUrl);
            if (logoBytes != null && !IsValidImageBytes(logoBytes))
                logoBytes = null;
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(PdfMarginCm, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));
                    page.Header().Element(c => BuildHeader(c, reporte, logoBytes));
                    page.Content().Layers(layers =>
                    {
                        layers.Layer().AlignCenter().AlignMiddle().Text(reporte.InstitutoNombre).FontSize(72).FontColor(Colors.Grey.Lighten2);
                        layers.PrimaryLayer().Element(c => BuildContent(c, reporte));
                    });
                    page.Footer().Element(c => BuildFooter(c, reporte));
                });
            });
            return doc.GeneratePdf();
        }

        private async Task<byte[]?> TryDownloadLogoAsync(string? logoUrl)
        {
            if (string.IsNullOrWhiteSpace(logoUrl) || (!logoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !logoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return null;
            try { return await _httpClientFactory.CreateClient().GetByteArrayAsync(logoUrl); }
            catch { return null; }
        }

        private static bool IsValidImageBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4) return false;
            // PNG
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8) return true;
            // GIF
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true;
            return false;
        }

        /// <summary>Encabezado institucional: logo arriba izquierda (max 60px), título 22pt, subtítulo 13pt gris, línea 2px, más espacio vertical.</summary>
        private static void BuildHeader(IContainer container, AprobadosReprobadosReportViewModel reporte, byte[]? logoBytes)
        {
            container.Column(col =>
            {
                col.Item().PaddingTop(4).PaddingBottom(14).Row(r =>
                {
                    if (logoBytes != null)
                        r.ConstantItem(PdfLogoHeightPt).Height(PdfLogoHeightPt).Image(logoBytes);
                    r.RelativeItem().PaddingLeft(logoBytes != null ? 12 : 0).AlignLeft().AlignMiddle().Column(c =>
                    {
                        c.Item().PaddingBottom(6).Text(reporte.InstitutoNombre).FontSize(22).Bold().FontColor(PdfColorInstitutional);
                        c.Item().Text("Cuadro de Aprobados y Reprobados por Grado").FontSize(13).FontColor(Colors.Grey.Darken2);
                    });
                });
                col.Item().LineHorizontal(PdfHeaderLinePt).LineColor(PdfColorInstitutional);
                col.Item().PaddingBottom(12);
            });
        }

        /// <summary>Bloque informativo: una fila con coordinador, trimestre, año, nivel, fecha de generación.</summary>
        private static void BuildInfoBlock(IContainer container, AprobadosReprobadosReportViewModel reporte)
        {
            container.PaddingBottom(12).Background(Colors.Grey.Lighten4).Padding(10).Row(r =>
            {
                r.RelativeItem().Column(c => { c.Item().Text("Coordinador").FontSize(7).FontColor(Colors.Grey.Darken1); c.Item().Text(reporte.ProfesorCoordinador).FontSize(9).Bold(); });
                r.RelativeItem().Column(c => { c.Item().Text("Trimestre").FontSize(7).FontColor(Colors.Grey.Darken1); c.Item().Text(reporte.Trimestre).FontSize(9); });
                r.RelativeItem().Column(c => { c.Item().Text("Año lectivo").FontSize(7).FontColor(Colors.Grey.Darken1); c.Item().Text(reporte.AnoLectivo).FontSize(9); });
                r.RelativeItem().Column(c => { c.Item().Text("Nivel").FontSize(7).FontColor(Colors.Grey.Darken1); c.Item().Text(reporte.NivelEducativo).FontSize(9); });
                r.RelativeItem().Column(c => { c.Item().Text("Fecha de generación").FontSize(7).FontColor(Colors.Grey.Darken1); c.Item().Text(reporte.FechaGeneracion.ToString("dd/MM/yyyy HH:mm")).FontSize(9); });
            });
        }

        /// <summary>Resumen ejecutivo: Total estudiantes bloque dominante, % aprobación azul fuerte, números 28-32pt.</summary>
        private static void BuildSummary(IContainer container, TotalesGeneralesDto totales)
        {
            var pctGeneral = totales.TotalEstudiantes > 0 ? Math.Round(totales.TotalAprobados * 100m / totales.TotalEstudiantes, 2) : 0m;
            container.PaddingBottom(16).Row(r =>
            {
                r.RelativeItem(2.2f).Background(Colors.White).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(14).AlignCenter().Column(c =>
                {
                    c.Item().Text("Total estudiantes").FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().PaddingTop(6).Text(totales.TotalEstudiantes.ToString()).FontSize(30).Bold().FontColor(PdfColorInstitutional);
                });
                r.ConstantItem(10);
                r.RelativeItem().Background(Colors.White).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(12).AlignCenter().Column(c =>
                {
                    c.Item().Text("Aprobados").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().PaddingTop(4).Text(totales.TotalAprobados.ToString()).FontSize(28).Bold().FontColor(PdfColorAprobados);
                });
                r.ConstantItem(10);
                r.RelativeItem().Background(Colors.White).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(12).AlignCenter().Column(c =>
                {
                    c.Item().Text("Reprobados").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().PaddingTop(4).Text(totales.TotalReprobados.ToString()).FontSize(28).Bold().FontColor(PdfColorReprobados);
                });
                r.ConstantItem(10);
                r.RelativeItem(1.4f).Background(PdfColorAprobacionFuerte).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(14).AlignCenter().Column(c =>
                {
                    c.Item().Text("% Aprobación general").FontSize(9).FontColor(Colors.White);
                    c.Item().PaddingTop(6).Text($"{pctGeneral:F1}%").FontSize(30).Bold().FontColor(Colors.White);
                });
            });
        }

        /// <summary>Tabla principal: encabezados cortos en una línea, solo números, Grado/Grupo a la izquierda, zebra, fila TOTAL con fondo institucional.</summary>
        private static void BuildMainTable(IContainer container, List<GradoEstadisticaDto> stats, TotalesGeneralesDto totales)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    def.RelativeColumn(0.8f);
                    def.RelativeColumn(0.8f);
                    def.RelativeColumn(1f);
                    def.RelativeColumn(1f);
                    def.RelativeColumn(1f);
                    def.RelativeColumn(1.2f);
                    def.RelativeColumn(1f);
                    def.RelativeColumn(0.9f);
                });
                table.Header(header =>
                {
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(6).AlignLeft().Text("Grado").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(6).AlignLeft().Text("Grupo").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text("Total").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text("Aprob.").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text("Reprob.").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text("Rep.hasta").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text("Sin Cal.").FontSize(9).Bold().FontColor(Colors.White);
                    header.Cell().Background(PdfColorInstitutional).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text("Retir.").FontSize(9).Bold().FontColor(Colors.White);
                });
                int rowIndex = 0;
                string? lastGrado = null;
                foreach (var e in stats)
                {
                    if (lastGrado != null && lastGrado != e.Grado)
                        _ = table.Cell().ColumnSpan(8).Height(2).Background(Colors.White);
                    lastGrado = e.Grado;
                    var rowBg = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(6).AlignLeft().Text(e.Grado).FontSize(9);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(6).AlignLeft().Text(e.Grupo).FontSize(9);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().Text(e.TotalEstudiantes.ToString()).FontSize(9);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().Text(e.Aprobados.ToString()).FontSize(9).FontColor(PdfColorAprobados);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().Text(e.Reprobados.ToString()).FontSize(9).FontColor(PdfColorReprobados);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().Text(e.ReprobadosHastaLaFecha.ToString()).FontSize(9);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().Text(e.SinCalificaciones.ToString()).FontSize(9).FontColor(Colors.Grey.Darken1);
                    table.Cell().Background(rowBg).PaddingVertical(6).PaddingHorizontal(4).AlignCenter().Text(e.Retirados.ToString()).FontSize(9);
                    rowIndex++;
                }
                table.Cell().ColumnSpan(2).Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(6).AlignLeft().Text("TOTALES").FontSize(10).Bold().FontColor(Colors.White);
                table.Cell().Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text(totales.TotalEstudiantes.ToString()).FontSize(10).Bold().FontColor(Colors.White);
                table.Cell().Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text(totales.TotalAprobados.ToString()).FontSize(10).Bold().FontColor("#c8e6c9");
                table.Cell().Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text(totales.TotalReprobados.ToString()).FontSize(10).Bold().FontColor("#ffcdd2");
                table.Cell().Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text(totales.TotalReprobadosHastaLaFecha.ToString()).FontSize(10).Bold().FontColor(Colors.White);
                table.Cell().Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text(totales.TotalSinCalificaciones.ToString()).FontSize(10).Bold().FontColor(Colors.White);
                table.Cell().Background(PdfColorTotalRow).PaddingVertical(8).PaddingHorizontal(4).AlignCenter().Text(totales.TotalRetirados.ToString()).FontSize(10).Bold().FontColor(Colors.White);
            });
        }

        /// <summary>Pie de página auditable: línea separadora sutil, Sistema, fecha completa, Página X de Y, código de validación.</summary>
        private static void BuildFooter(IContainer container, AprobadosReprobadosReportViewModel reporte)
        {
            var codigoValidacion = $"REP-APR-{reporte.FechaGeneracion:yyyy}-{reporte.Trimestre}-{reporte.FechaGeneracion:HHmm}";
            var fechaCompleta = reporte.FechaGeneracion.ToString("dd/MM/yyyy HH:mm");
            container.PaddingTop(8).BorderTop(1f).BorderColor(Colors.Grey.Lighten2).Row(r =>
            {
                r.RelativeItem().AlignLeft().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1))
                    .Text($"Sistema: SchoolManager · {fechaCompleta} · Cód. validación: {codigoValidacion}");
                r.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1))
                    .Text(t => { t.CurrentPageNumber().Format(n => $"Página {n} de "); t.TotalPages().Format(n => $"{n}"); });
            });
        }

        private static void BuildContent(IContainer container, AprobadosReprobadosReportViewModel reporte)
        {
            var stats = reporte.Estadisticas ?? new List<GradoEstadisticaDto>();
            var totales = reporte.TotalesGenerales ?? new TotalesGeneralesDto();

            container.Column(col =>
            {
                col.Item().Element(c => BuildInfoBlock(c, reporte));
                if (stats.Count == 0)
                {
                    col.Item().PaddingTop(12).AlignCenter().Text("No hay datos para los filtros seleccionados.").FontSize(10).FontColor(Colors.Grey.Darken1);
                    return;
                }
                col.Item().Element(c => BuildSummary(c, totales));
                col.Item().Element(c => BuildMainTable(c, stats, totales));
            });
        }

        #endregion

        public async Task<byte[]> ExportarAExcelAsync(AprobadosReprobadosReportViewModel reporte)
        {
            // TODO: Implementar exportación a Excel usando ClosedXML o EPPlus
            await Task.CompletedTask;
            throw new NotImplementedException("Exportación a Excel pendiente de implementación");
        }

        public async Task<(bool Success, string Message)> PrepararDatosParaReporteAsync(Guid schoolId)
        {
            try
            {
                // Solo sincroniza groups.grade desde la oferta académica real (sin mapas fijos ni masivo 3T en actividades).
                var groups = await _context.Groups
                    .Where(g => g.SchoolId == schoolId && (g.Grade == null || g.Grade == ""))
                    .ToListAsync();

                var groupsUpdated = 0;
                foreach (var g in groups)
                {
                    var gradeName = await _context.SubjectAssignments
                        .Where(sa => sa.GroupId == g.Id)
                        .Select(sa => sa.GradeLevel.Name)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrWhiteSpace(gradeName))
                    {
                        g.Grade = gradeName;
                        groupsUpdated++;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("PrepararDatosParaReporte: school {SchoolId}, grupos con grado sincronizado desde oferta: {G}", schoolId, groupsUpdated);
                return (true, $"Se actualizó el grado (campo Grade) en {groupsUpdated} grupos según subject_assignments. No se modificaron actividades ni trimestres.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparando datos para reporte aprobados/reprobados");
                return (false, $"Error: {ex.Message}");
            }
        }
    }
}

