using Microsoft.AspNetCore.Mvc;
using SchoolManager.ViewModels;
using System;
using System.Threading.Tasks;
using SchoolManager.Services.Interfaces;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

namespace SchoolManager.Controllers
{
    [Authorize(Roles = "director")]
    public class DirectorController : Controller
    {
        private readonly IDirectorService _directorService;
        private readonly ITrimesterService _trimesterService;
        private readonly ISubjectWithdrawalRequestService _withdrawalRequestService;

        public DirectorController(
            IDirectorService directorService,
            ITrimesterService trimesterService,
            ISubjectWithdrawalRequestService withdrawalRequestService)
        {
            _directorService = directorService;
            _trimesterService = trimesterService;
            _withdrawalRequestService = withdrawalRequestService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Obtener solo los totales iniciales para carga rápida
                var model = await _directorService.GetInitialTotalsAsync();
                
                // Si no hay datos, inicializar un modelo vacío
                if (model == null)
                {
                    var trimestres = await _trimesterService.GetAllAsync();
                    model = new DirectorViewModel
                    {
                        TrimestresDisponibles = trimestres,
                        TrimestreSeleccionado = null,
                        TotalEstudiantes = 0,
                        TotalAprobados = 0,
                        TotalReprobados = 0,
                        TotalSinEvaluar = 0,
                        PorcentajeAprobados = 0,
                        PorcentajeReprobados = 0,
                        PorcentajeSinEvaluar = 0,
                        // Inicializamos las listas vacías ya que se cargarán después vía AJAX
                        MateriasDesempeno = new List<MateriaDesempenoViewModel>(),
                        Profesores = new List<ProfesorDesempenoViewModel>(),
                        MateriasAprobacion = new List<MateriaAprobacionViewModel>(),
                        Alertas = new List<AlertaNotificacionViewModel>(),
                        Recomendaciones = new List<string>()
                    };
                }

                return View("Director", model);
            }
            catch (Exception ex)
            {
                // Log the error
                var trimestres = await _trimesterService.GetAllAsync();
                var emptyModel = new DirectorViewModel
                {
                    TrimestresDisponibles = trimestres,
                    TrimestreSeleccionado = null,
                    TotalEstudiantes = 0,
                    TotalAprobados = 0,
                    TotalReprobados = 0,
                    TotalSinEvaluar = 0,
                    PorcentajeAprobados = 0,
                    PorcentajeReprobados = 0,
                    PorcentajeSinEvaluar = 0,
                    MateriasDesempeno = new List<MateriaDesempenoViewModel>(),
                    Profesores = new List<ProfesorDesempenoViewModel>(),
                    MateriasAprobacion = new List<MateriaAprobacionViewModel>(),
                    Alertas = new List<AlertaNotificacionViewModel>(),
                    Recomendaciones = new List<string>()
                };
                return View("Director", emptyModel);
            }
        }

        [HttpGet]
        public async Task<IActionResult> SubjectWithdrawalRequests()
        {
            var requests = await _withdrawalRequestService.GetPendingAsync();
            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewSubjectWithdrawal(Guid requestId, bool approve, string? reviewObservation)
        {
            var result = await _withdrawalRequestService.ReviewAsync(requestId, approve, reviewObservation);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(SubjectWithdrawalRequests));
        }

        [HttpPost]
        public async Task<IActionResult> FiltrarPorTrimestre([FromBody] string trimestre)
        {
            try
            {
                var model = await _directorService.GetDashboardViewModelAsync(trimestre);
                return Json(model);
            }
            catch (Exception)
            {
                return BadRequest(new { error = "Ocurrió un error al filtrar los datos." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMateriasDesempeno(int page = 1, int pageSize = 10, string trimestre = null)
        {
            try
            {
                var result = await _directorService.GetMateriasDesempenoAsync(page, pageSize, trimestre);
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Error al obtener el desempeño de materias.", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProfesoresDesempeno(int page = 1, int pageSize = 10, string trimestre = null)
        {
            try
            {
                var result = await _directorService.GetProfesoresDesempenoAsync(page, pageSize, trimestre);
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Error al obtener el desempeño de profesores.", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMateriasAprobacion(int page = 1, int pageSize = 5, string trimestre = null)
        {
            try
            {
                var result = await _directorService.GetMateriasAprobacionAsync(page, pageSize, trimestre);
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Error al obtener la aprobación de materias.", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAlertas(int page = 1, int pageSize = 5, string trimestre = null)
        {
            try
            {
                var result = await _directorService.GetAlertasAsync(page, pageSize, trimestre);
                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Error al obtener las alertas.", details = ex.Message });
            }
        }
    }
} 