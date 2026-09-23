using System.Drawing;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace SchoolManager.Services.Implementations;

public static class StudentGradeImportTemplateBuilder
{
    public const string FileName = "Plantilla_Importacion_Notas_Celosan.xlsx";
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static readonly string[] NoteHeaders =
    {
        "Documento ID",
        "Estudiante (Email)",
        "Nombre",
        "Apellido",
        "Asignatura",
        "Nivel",
        "Grupo Académico",
        "Jornada",
        "Nota T1",
        "Nota T2",
        "Nota T3"
    };

    private static readonly Color HeaderBlue = Color.FromArgb(31, 78, 120);
    private static readonly Color GuideBlue = Color.FromArgb(217, 234, 247);
    private static readonly Color ImportantYellow = Color.FromArgb(255, 242, 204);

    public static byte[] Build()
    {
        ExcelPackage.License.SetNonCommercialOrganization("Celosan");
        using var package = new ExcelPackage();
        BuildNotesSheet(package.Workbook.Worksheets.Add("Notas"));
        BuildInstructionsSheet(package.Workbook.Worksheets.Add("Instrucciones"));
        return package.GetAsByteArray();
    }

    private static void BuildNotesSheet(ExcelWorksheet notes)
    {
        for (var i = 0; i < NoteHeaders.Length; i++)
        {
            var cell = notes.Cells[1, i + 1];
            cell.Value = NoteHeaders[i];
            StyleHeader(cell);
        }

        notes.Cells[1, 1, 1, NoteHeaders.Length].AutoFilter = true;
        notes.View.FreezePanes(2, 1);
        notes.Column(1).Width = 18;
        notes.Column(2).Width = 26;
        notes.Column(3).Width = 16;
        notes.Column(4).Width = 16;
        notes.Column(5).Width = 22;
        notes.Column(6).Width = 12;
        notes.Column(7).Width = 18;
        notes.Column(8).Width = 14;
        notes.Column(9).Width = 12;
        notes.Column(10).Width = 12;
        notes.Column(11).Width = 12;
        notes.Row(1).Height = 22;
    }

    private static void BuildInstructionsSheet(ExcelWorksheet sheet)
    {
        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 24;
        sheet.Column(4).Width = 28;
        sheet.Column(5).Width = 34;
        sheet.Column(6).Width = 28;
        sheet.Column(7).Width = 18;
        sheet.Column(8).Width = 16;
        sheet.Column(9).Width = 12;
        sheet.Column(10).Width = 12;
        sheet.Column(11).Width = 12;

        MergeTitle(sheet, 1, "Plantilla de importación de notas — Celosan", HeaderBlue, Color.White, 14);
        sheet.Row(1).Height = 28;

        var guideHeaders = new[] { "Campo", "Obligatorio", "Ejemplo", "Uso", "Observación", "Regla" };
        for (var i = 0; i < guideHeaders.Length; i++)
        {
            var cell = sheet.Cells[3, i + 1];
            cell.Value = guideHeaders[i];
            StyleGuideHeader(cell);
        }

        WriteGuideRow(sheet, 4, "Documento ID", "Sí", "8-1076-487", "Identifica al estudiante", "Debe coincidir con Celosan", "No dejar vacío");
        WriteGuideRow(sheet, 5, "Estudiante (Email)", "Sí", "alumno@celosam.com", "Verifica la identidad", "Debe pertenecer al mismo estudiante", "No dejar vacío");
        WriteGuideRow(sheet, 6, "Nombre", "No", "María", "Referencia visual", "No se usa para emparejar", string.Empty);
        WriteGuideRow(sheet, 7, "Apellido", "No", "López", "Referencia visual", "No se usa para emparejar", string.Empty);
        WriteGuideRow(sheet, 8, "Asignatura", "Sí", "MATEMÁTICA", "Identifica la materia", "Debe coincidir con la inscripción", "Coincidencia exacta normalizada");
        WriteGuideRow(sheet, 9, "Nivel", "Sí", "10", "Valida el grado", "También puede ser 10°", "Debe corresponder a la matrícula");
        WriteGuideRow(sheet, 10, "Grupo Académico", "Sí", "10-A", "Valida el grupo", "Debe coincidir con Celosan", "Coincidencia exacta");
        WriteGuideRow(sheet, 11, "Jornada", "Sí", "Noche", "Valida la jornada", "Debe existir en el catálogo", "Debe corresponder a la matrícula");
        WriteGuideRow(sheet, 12, "Nota T1", "Según corresponda", "4.2", "Nota final de 1T", "Vacío = no importar T1", "1.0–5.0, N/A o SN");
        WriteGuideRow(sheet, 13, "Nota T2", "Según corresponda", "4.0", "Nota final de 2T", "Vacío = no importar T2", "1.0–5.0, N/A o SN");
        WriteGuideRow(sheet, 14, "Nota T3", "Según corresponda", "4.5", "Nota final de 3T", "Vacío = no importar T3", "1.0–5.0, N/A o SN");

        MergeTitle(sheet, 16, "Ejemplo de una fila válida", GuideBlue, Color.Black, 11);

        for (var i = 0; i < NoteHeaders.Length; i++)
        {
            var cell = sheet.Cells[18, i + 1];
            cell.Value = NoteHeaders[i];
            StyleHeader(cell);
        }

        var example = new object[]
        {
            "8-1076-487", "alumno@celosam.com", "María", "López", "MATEMÁTICA",
            "10", "10-A", "Noche", 4.2m, 4.0m, 4.5m
        };
        for (var i = 0; i < example.Length; i++)
        {
            var cell = sheet.Cells[19, i + 1];
            cell.Value = example[i];
            if (example[i] is decimal)
                cell.Style.Numberformat.Format = "0.0";
        }

        MergeTitle(sheet, 21, "Importante", ImportantYellow, Color.Black, 11);
        var notes = new[]
        {
            "• El año académico se selecciona en Celosan antes de analizar el archivo; no hace falta incluirlo.",
            "• Puede incluir varios estudiantes y varias asignaturas en el mismo archivo.",
            "• Una celda de nota vacía NO significa 0; significa que ese trimestre no se importará.",
            "• N/A = No asistió. SN = Sin nota. Ninguno se convierte en 0.",
            "• Si existe un error, el preview lo mostrará antes de permitir la confirmación.",
            "• No cambie los nombres de las columnas de la hoja “Notas”.",
            "• Solo se aceptan archivos .xlsx. Tamaño máximo: 10 MB."
        };
        for (var i = 0; i < notes.Length; i++)
        {
            var row = 22 + i;
            sheet.Cells[row, 1, row, 6].Merge = true;
            sheet.Cells[row, 1].Value = notes[i];
            sheet.Cells[row, 1].Style.WrapText = true;
            sheet.Cells[row, 1].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            sheet.Row(row).Height = 18;
        }
    }

    private static void WriteGuideRow(
        ExcelWorksheet sheet,
        int row,
        string field,
        string required,
        string example,
        string use,
        string observation,
        string rule)
    {
        var values = new[] { field, required, example, use, observation, rule };
        for (var i = 0; i < values.Length; i++)
        {
            var cell = sheet.Cells[row, i + 1];
            cell.Value = values[i];
            cell.Style.WrapText = true;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
        }

        sheet.Row(row).Height = 28.5;
    }

    private static void MergeTitle(ExcelWorksheet sheet, int row, string text, Color background, Color foreground, float fontSize)
    {
        var range = sheet.Cells[row, 1, row, 6];
        range.Merge = true;
        range.Value = text;
        range.Style.Font.Bold = true;
        range.Style.Font.Size = fontSize;
        range.Style.Font.Color.SetColor(foreground);
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(background);
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        if (background.ToArgb() == HeaderBlue.ToArgb())
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    private static void StyleHeader(ExcelRange cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.Color.SetColor(Color.White);
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(HeaderBlue);
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        cell.Style.WrapText = true;
    }

    private static void StyleGuideHeader(ExcelRange cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(GuideBlue);
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        cell.Style.WrapText = true;
    }
}
