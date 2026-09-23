using OfficeOpenXml;
using SchoolManager.Services.Implementations;
using Xunit;

namespace SchoolManager.Tests;

public class StudentGradeImportTemplateBuilderTests
{
    [Fact]
    public void Builds_official_workbook_structure()
    {
        Assert.Equal("Plantilla_Importacion_Notas_Celosan.xlsx", StudentGradeImportTemplateBuilder.FileName);

        var bytes = StudentGradeImportTemplateBuilder.Build();
        Assert.True(bytes.Length > 100);

        ExcelPackage.License.SetNonCommercialOrganization("Celosan");
        using var package = new ExcelPackage(new MemoryStream(bytes));
        Assert.Equal("Notas", package.Workbook.Worksheets[0].Name);
        Assert.Equal("Instrucciones", package.Workbook.Worksheets[1].Name);

        var notes = package.Workbook.Worksheets["Notas"];
        for (var i = 0; i < StudentGradeImportTemplateBuilder.NoteHeaders.Length; i++)
            Assert.Equal(StudentGradeImportTemplateBuilder.NoteHeaders[i], notes.Cells[1, i + 1].Text);

        var instructions = package.Workbook.Worksheets["Instrucciones"];
        Assert.Equal("Plantilla de importación de notas — Celosan", instructions.Cells[1, 1].Text);
        Assert.Equal("Campo", instructions.Cells[3, 1].Text);
        Assert.Equal("Obligatorio", instructions.Cells[3, 2].Text);
        Assert.Equal("Ejemplo", instructions.Cells[3, 3].Text);
        Assert.Equal("Uso", instructions.Cells[3, 4].Text);
        Assert.Equal("Observación", instructions.Cells[3, 5].Text);
        Assert.Equal("Regla", instructions.Cells[3, 6].Text);
        Assert.Equal("Documento ID", instructions.Cells[4, 1].Text);
        Assert.Equal("Nota T3", instructions.Cells[14, 1].Text);
        Assert.Equal("Ejemplo de una fila válida", instructions.Cells[16, 1].Text);
        Assert.Equal("Importante", instructions.Cells[21, 1].Text);

        var text = string.Join(" ", Enumerable.Range(1, 30).SelectMany(r => new[]
        {
            instructions.Cells[r, 1].Text,
            instructions.Cells[r, 6].Text
        }));
        Assert.Contains("1.0", text);
        Assert.Contains("5.0", text);
        Assert.Contains("N/A", text);
        Assert.Contains("SN", text);
        Assert.Contains("no se importará", text);
        Assert.Contains("10 MB", text);
        Assert.Contains(".xlsx", text);
        Assert.Contains("año académico", text);
    }
}
