using Microsoft.AspNetCore.Mvc;
using SchoolManager.Models;
using SchoolManager.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AttendanceController : Controller
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    public async Task<IActionResult> Index()
    {
        var attendances = await _attendanceService.GetAllAsync();
        return View(attendances);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var attendance = await _attendanceService.GetByIdAsync(id);
        if (attendance == null) return NotFound();
        return View(attendance);
    }

    public IActionResult Create() => View();

    [HttpPost]
    public async Task<IActionResult> Create(Attendance attendance)
    {
        if (ModelState.IsValid)
        {
            await _attendanceService.CreateAsync(attendance);
            return RedirectToAction(nameof(Index));
        }
        return View(attendance);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var attendance = await _attendanceService.GetByIdAsync(id);
        if (attendance == null) return NotFound();
        return View(attendance);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Attendance attendance)
    {
        if (ModelState.IsValid)
        {
            await _attendanceService.UpdateAsync(attendance);
            return RedirectToAction(nameof(Index));
        }
        return View(attendance);
    }

    public async Task<IActionResult> Delete(Guid id)
    {
        var attendance = await _attendanceService.GetByIdAsync(id);
        if (attendance == null) return NotFound();
        return View(attendance);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        await _attendanceService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SaveAttendances([FromBody] List<AttendanceSaveDto> attendances)
    {
        try
        {
            await _attendanceService.SaveAttendancesAsync(attendances);
            return Ok(new { success = true, message = "Asistencias guardadas correctamente." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Historial([FromBody] HistorialAsistenciaFiltroDto filtro)
    {
        try
        {
            var resultado = await _attendanceService.GetHistorialAsistenciaAsync(filtro);
            return Json(resultado);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Estadisticas([FromBody] EstadisticasFiltroDto filtro)
    {
        Console.WriteLine($"Trimestre: {filtro.Trimestre}, Inicio: {filtro.FechaInicio}, Fin: {filtro.FechaFin}");
        if (filtro == null || filtro.GroupId == Guid.Empty || filtro.GradeId == Guid.Empty || string.IsNullOrEmpty(filtro.Trimestre))
            return BadRequest("Faltan datos para la consulta.");

        var estadisticas = await _attendanceService.GetEstadisticasAsync(
            filtro.GroupId, filtro.GradeId, filtro.Trimestre, filtro.FechaInicio, filtro.FechaFin, filtro.ShiftId
        );
        return Json(estadisticas);
    }
}
