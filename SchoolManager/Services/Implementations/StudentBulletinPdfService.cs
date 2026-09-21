using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManager.Dtos;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class StudentBulletinPdfService : IStudentBulletinPdfService
{
    private const string HeaderBlue = "#1e40af";
    private const string AreaBlue = "#e3f2fd";
    private const string PassGreen = "#15803d";
    private const string FailRed = "#b91c1c";
    private const string Muted = "#64748b";

    public byte[] GenerateByGradePdf(StudentBulletinDto bulletin, string schoolName)
    {
        EnsureLicense();
        var school = Display(schoolName, "CELO San Miguelito");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken2));

                page.Header().Element(h => BuildDocumentHeader(h, school, "BOLETÍN ACADÉMICO", null));
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Element(e => BuildMetaGrid(e, new[]
                    {
                        ("Nombre", Display(bulletin.StudentName)),
                        ("Grado", Display(bulletin.Grade)),
                        ("Grupo", Display(bulletin.Group)),
                        ("Programa / Bachiller", Display(bulletin.Specialty)),
                        ("Año académico", Display(bulletin.AcademicYear))
                    }));
                    col.Item().Element(e => BuildGradeTable(e, bulletin.Areas));
                });
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateProgramHistoryPdf(StudentProgramHistoryDto history, string schoolName)
    {
        EnsureLicense();
        var school = Display(schoolName, "CELO San Miguelito");
        var tracks = history.Tracks ?? new List<ProgramHistoryTrackDto>();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken2));

                page.Header().Element(h => BuildDocumentHeader(h, school, "BOLETÍN ACADÉMICO", "PROGRAMA COMPLETO"));
                page.Content().Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Element(e => BuildMetaGrid(e, new[]
                    {
                        ("Nombre", Display(history.StudentName)),
                        ("Programas", tracks.Count == 0 ? "—" : string.Join(" · ", tracks.Select(t => Display(t.ProgramName)).Distinct())),
                        ("Año académico", ResolveHistoryYear(tracks))
                    }));

                    if (tracks.Count == 0)
                    {
                        col.Item().Text("No hay historial académico para mostrar.").FontColor(Muted).Italic();
                    }
                    else
                    {
                        foreach (var track in tracks)
                        {
                            col.Item().Element(e => BuildTrackSection(e, track));
                        }
                    }
                });
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    public string BuildFileName(string? studentName, string? academicYear, bool isProgramComplete)
    {
        var name = SanitizeFileName(studentName);
        var year = SanitizeFileName(string.IsNullOrWhiteSpace(academicYear) || academicYear == "—" ? null : academicYear);
        if (year == "Estudiante")
            year = "SinAnio";
        var prefix = isProgramComplete ? "Boletin_ProgramaCompleto" : "Boletin";
        return $"{prefix}_{name}_{year}.pdf";
    }

    private static void BuildTrackSection(IContainer container, ProgramHistoryTrackDto track)
    {
        var grades = track.Grades ?? new List<ProgramHistoryGradeColumnDto>();
        container.Column(col =>
        {
            col.Spacing(6);
            col.Item().Text(Display(track.ProgramName, "Programa")).FontSize(11).Bold().FontColor(HeaderBlue);
            col.Item().Element(e => BuildHistoryTable(e, grades, track.Areas ?? new List<ProgramHistoryAreaDto>()));
        });
    }

    private static void BuildGradeTable(IContainer container, List<AreaBulletinDto>? areas)
    {
        var list = areas ?? new List<AreaBulletinDto>();
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                def.RelativeColumn(1.4f);
                def.RelativeColumn(2.4f);
                def.ConstantColumn(52);
                def.ConstantColumn(52);
                def.ConstantColumn(52);
                def.ConstantColumn(78);
            });

            table.Header(h =>
            {
                HeaderCell(h.Cell(), "Área");
                HeaderCell(h.Cell(), "Asignatura", alignLeft: true);
                HeaderCell(h.Cell(), "T1");
                HeaderCell(h.Cell(), "T2");
                HeaderCell(h.Cell(), "T3");
                HeaderCell(h.Cell(), "Promedio Final");
            });

            if (list.Count == 0 || list.All(a => (a.Subjects?.Count ?? 0) == 0))
            {
                table.Cell().ColumnSpan(6).Element(EmptyCell).AlignCenter()
                    .Text("No hay asignaturas para mostrar en el boletín.").FontColor(Muted).Italic();
                return;
            }

            foreach (var area in list)
            {
                var subjects = area.Subjects ?? new List<SubjectBulletinDto>();
                for (var i = 0; i < subjects.Count; i++)
                {
                    var subject = subjects[i];
                    table.Cell().Element(AreaCell).Text(i == 0 ? Display(area.AreaName, "Sin área") : "").SemiBold().FontSize(8);
                    table.Cell().Element(BodyCellLeft).Text(Display(subject.SubjectName));
                    ScoreCell(table, subject.T1);
                    ScoreCell(table, subject.T2);
                    ScoreCell(table, subject.T3);
                    ScoreCell(table, subject.FinalAverage, bold: true);
                }
            }
        });
    }

    private static void BuildHistoryTable(
        IContainer container,
        List<ProgramHistoryGradeColumnDto> grades,
        List<ProgramHistoryAreaDto> areas)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                def.RelativeColumn(1.4f);
                def.RelativeColumn(2.6f);
                foreach (var _ in grades)
                    def.ConstantColumn(56);
            });

            table.Header(h =>
            {
                HeaderCell(h.Cell(), "Área");
                HeaderCell(h.Cell(), "Asignatura", alignLeft: true);
                foreach (var grade in grades)
                    HeaderCell(h.Cell(), Display(grade.GradeName));
            });

            var colCount = 2 + grades.Count;
            if (areas.Count == 0 || areas.All(a => (a.Subjects?.Count ?? 0) == 0))
            {
                table.Cell().ColumnSpan((uint)Math.Max(colCount, 1)).Element(EmptyCell).AlignCenter()
                    .Text("No hay asignaturas para este programa.").FontColor(Muted).Italic();
                return;
            }

            foreach (var area in areas)
            {
                var subjects = area.Subjects ?? new List<ProgramHistorySubjectDto>();
                for (var i = 0; i < subjects.Count; i++)
                {
                    var subject = subjects[i];
                    table.Cell().Element(AreaCell).Text(i == 0 ? Display(area.AreaName, "Sin área") : "").SemiBold().FontSize(8);
                    table.Cell().Element(BodyCellLeft).Text(Display(subject.SubjectName));
                    foreach (var col in grades)
                    {
                        var result = (subject.GradeResults ?? new List<ProgramHistoryGradeResultDto>())
                            .FirstOrDefault(r => r.GradeNumber == col.GradeNumber);
                        ScoreCell(table, result?.FinalAverage, bold: true);
                    }
                }
            }
        });
    }

    private static void BuildDocumentHeader(IContainer container, string schoolName, string title, string? subtitle)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(schoolName).FontSize(11).SemiBold().FontColor(HeaderBlue);
            col.Item().AlignCenter().Text(title).FontSize(16).Bold().FontColor(HeaderBlue);
            if (!string.IsNullOrWhiteSpace(subtitle))
                col.Item().AlignCenter().Text(subtitle).FontSize(11).SemiBold().FontColor(HeaderBlue);
            col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(HeaderBlue);
        });
    }

    private static void BuildMetaGrid(IContainer container, IReadOnlyList<(string Label, string Value)> items)
    {
        container.Row(row =>
        {
            foreach (var item in items)
            {
                row.RelativeItem().PaddingRight(8).Column(c =>
                {
                    c.Item().Text(item.Label).FontSize(8).Bold().FontColor(Muted);
                    c.Item().Text(item.Value).FontSize(10).FontColor(Colors.Grey.Darken3);
                });
            }
        });
    }

    private static void BuildFooter(IContainer container)
    {
        container.AlignCenter().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium)).Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}");
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }

    private static void HeaderCell(IContainer cell, string text, bool alignLeft = false)
    {
        var box = cell.Background(HeaderBlue).Padding(6);
        (alignLeft ? box.AlignLeft() : box.AlignCenter())
            .Text(text).FontColor(Colors.White).SemiBold().FontSize(8);
    }

    private static void ScoreCell(TableDescriptor table, decimal? value, bool bold = false)
    {
        var text = FormatScore(value);
        var color = value.HasValue ? (value.Value >= 3.0m ? PassGreen : FailRed) : Muted;
        var span = table.Cell().Element(BodyCellCenter).Text(text).FontColor(color);
        if (bold)
            span.Bold();
    }

    private static IContainer AreaCell(IContainer container) =>
        container.Background(AreaBlue).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignMiddle();

    private static IContainer BodyCellLeft(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignMiddle();

    private static IContainer BodyCellCenter(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignCenter().AlignMiddle();

    private static IContainer EmptyCell(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8);

    private static string FormatScore(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.0", CultureInfo.InvariantCulture) : "—";

    private static string ResolveHistoryYear(List<ProgramHistoryTrackDto> tracks)
    {
        var years = tracks
            .SelectMany(t => t.Grades ?? new List<ProgramHistoryGradeColumnDto>())
            .Select(g => g.AcademicYear)
            .Where(y => !string.IsNullOrWhiteSpace(y) && y != "—")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return years.Count == 0 ? "—" : string.Join(", ", years);
    }

    private static string Display(string? value, string fallback = "—") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string SanitizeFileName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Estudiante";

        var normalized = raw.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c is ' ' or '_' or '-')
                sb.Append('_');
        }

        var cleaned = sb.ToString().Trim('_');
        while (cleaned.Contains("__", StringComparison.Ordinal))
            cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);

        if (cleaned.Length > 60)
            cleaned = cleaned[..60].Trim('_');
        return string.IsNullOrEmpty(cleaned) ? "Estudiante" : cleaned;
    }

    private static void EnsureLicense()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
