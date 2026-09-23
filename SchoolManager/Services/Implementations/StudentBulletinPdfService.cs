using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManager.Dtos;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Services.Implementations;

public class StudentBulletinPdfService : IStudentBulletinPdfService
{
    private const string HeaderBlue = "#1e40af";
    private const string AreaFill = "#f1f5f9";
    private const string PassGreen = "#15803d";
    private const string FailRed = "#b91c1c";
    private const string Muted = "#64748b";
    private const string NeutralSchoolName = "Institución educativa";
    private const float LogoMaxWidthPt = 160f;
    private const float LogoMaxHeightPt = 72f;
    private const float ProgramLogoMaxWidthPt = 130f;
    private const float ProgramLogoMaxHeightPt = 52f;
    private const int MaxImageDownloadBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan ImageDownloadTimeout = TimeSpan.FromSeconds(10);

    private readonly IHttpBytesDownloadCache _httpBytesDownloadCache;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StudentBulletinPdfService> _logger;

    public StudentBulletinPdfService(
        IHttpBytesDownloadCache httpBytesDownloadCache,
        IWebHostEnvironment environment,
        ILogger<StudentBulletinPdfService> logger)
    {
        _httpBytesDownloadCache = httpBytesDownloadCache;
        _environment = environment;
        _logger = logger;
    }

    public async Task<BulletinPdfIdentity> BuildIdentityAsync(
        string? schoolName,
        string? logoUrl,
        CancellationToken cancellationToken = default)
    {
        var identity = new BulletinPdfIdentity
        {
            SchoolName = string.IsNullOrWhiteSpace(schoolName) ? NeutralSchoolName : schoolName.Trim()
        };

        if (string.IsNullOrWhiteSpace(logoUrl))
            return identity;

        try
        {
            var bytes = await TryLoadLogoBytesAsync(logoUrl.Trim(), cancellationToken);
            if (bytes == null)
            {
                _logger.LogWarning("Boletín PDF: no se pudieron obtener bytes de logo. El documento se genera sin imagen.");
                return identity;
            }

            if (!IsValidPngOrJpeg(bytes))
            {
                _logger.LogWarning("Boletín PDF: los bytes de logo no son PNG/JPEG válidos. El documento se genera sin imagen.");
                return identity;
            }

            identity.LogoBytes = bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boletín PDF: error al cargar el logo institucional. El documento se genera sin imagen.");
        }

        return identity;
    }

    public byte[] GenerateByGradePdf(StudentBulletinDto bulletin, BulletinPdfIdentity identity)
    {
        EnsureLicense();
        var branding = ResolveIdentity(identity);
        var academicYear = Display(bulletin.AcademicYear);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken2));

                page.Header().Element(h => BuildInstitutionalHeader(h, branding, academicYear, isProgramComplete: false));
                page.Content().PaddingBottom(8).Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Element(e => BuildMetaGrid(e, new[]
                    {
                        ("Nombre", Display(bulletin.StudentName)),
                        ("Grado", Display(bulletin.Grade)),
                        ("Grupo", Display(bulletin.Group)),
                        ("Programa / Bachiller", Display(bulletin.Specialty)),
                        ("Año académico", academicYear)
                    }));
                    col.Item().Element(e => BuildGradeTable(e, bulletin.Areas));
                    col.Item().Element(e => BuildDirectorSignature(e));
                });
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    public byte[] GenerateProgramHistoryPdf(StudentProgramHistoryDto history, BulletinPdfIdentity identity)
    {
        EnsureLicense();
        var branding = ResolveIdentity(identity);
        var tracks = history.Tracks ?? new List<ProgramHistoryTrackDto>();
        var academicYear = ResolveHistoryYear(tracks);
        var meta = new[]
        {
            ("Nombre", Display(history.StudentName)),
            ("Programas", tracks.Count == 0 ? "—" : string.Join(" · ", tracks.Select(t => Display(t.ProgramName)).Distinct())),
            ("Año académico", academicYear)
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken2));

                page.Content().PaddingBottom(4).Column(col =>
                {
                    col.Spacing(8);

                    if (tracks.Count == 0)
                    {
                        col.Item().Element(e => BuildProgramSection(e, branding, academicYear, meta, track: null));
                        return;
                    }

                    for (var i = 0; i < tracks.Count; i++)
                    {
                        if (i > 0)
                            col.Item().PageBreak();

                        col.Item().Element(e => BuildProgramSection(
                            e, branding, academicYear, meta, tracks[i]));
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

    internal static void BuildInstitutionalHeader(
        IContainer container,
        BulletinPdfIdentity identity,
        string academicYear,
        bool isProgramComplete)
    {
        var schoolName = string.IsNullOrWhiteSpace(identity.SchoolName)
            ? NeutralSchoolName
            : identity.SchoolName.Trim();
        var logoBytes = IsValidPngOrJpeg(identity.LogoBytes) ? identity.LogoBytes : null;

        var compact = isProgramComplete;
        var logoWidth = compact ? ProgramLogoMaxWidthPt : LogoMaxWidthPt;
        var logoHeight = compact ? ProgramLogoMaxHeightPt : LogoMaxHeightPt;

        container.Column(col =>
        {
            col.Spacing(compact ? 1 : 3);
            if (logoBytes != null)
            {
                col.Item().AlignCenter().Width(logoWidth).Height(logoHeight)
                    .AlignCenter().AlignMiddle()
                    .Image(logoBytes)
                    .FitArea();
            }

            col.Item().PaddingTop(logoBytes != null ? (compact ? 2 : 6) : 0).AlignCenter()
                .Text(schoolName).FontSize(compact ? 13 : 14).Bold().FontColor(HeaderBlue);
            col.Item().AlignCenter()
                .Text("BOLETÍN ACADÉMICO").FontSize(compact ? 11 : 12).SemiBold().FontColor(HeaderBlue);

            if (isProgramComplete)
            {
                col.Item().AlignCenter()
                    .Text("PROGRAMA COMPLETO").FontSize(10).SemiBold().FontColor(HeaderBlue);
            }

            col.Item().AlignCenter()
                .Text($"Año académico: {academicYear}").FontSize(8.5f).FontColor(Muted);
            col.Item().PaddingTop(compact ? 4 : 8).LineHorizontal(1.5f).LineColor(HeaderBlue);
        });
    }

    private static void BuildDirectorSignature(IContainer container, bool compact = false)
    {
        container.PaddingTop(compact ? 10 : 32).ShowEntire().AlignCenter().Column(col =>
        {
            col.Item().Width(compact ? 160 : 180).LineHorizontal(1).LineColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(compact ? 3 : 6).AlignCenter()
                .Text("Firma del Director(a)").FontSize(8.5f).FontColor(Colors.Grey.Darken3);
            col.Item().AlignCenter()
                .Text("Director(a)").FontSize(8).FontColor(Muted);
        });
    }

    private static void BuildProgramSection(
        IContainer container,
        BulletinPdfIdentity branding,
        string academicYear,
        IReadOnlyList<(string Label, string Value)> meta,
        ProgramHistoryTrackDto? track)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Element(h => BuildInstitutionalHeader(h, branding, academicYear, isProgramComplete: true));
            col.Item().Element(e => BuildMetaGrid(e, meta, compact: true));

            if (track == null)
                col.Item().Text("No hay historial académico para mostrar.").FontColor(Muted).Italic();
            else
                col.Item().Element(e => BuildTrackSection(e, track));

            col.Item().Element(e => BuildDirectorSignature(e, compact: true));
        });
    }

    private static void BuildTrackSection(IContainer container, ProgramHistoryTrackDto track)
    {
        var grades = track.Grades ?? new List<ProgramHistoryGradeColumnDto>();
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().Text(Display(track.ProgramName, "Programa")).FontSize(10).Bold().FontColor(HeaderBlue);
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
                def.RelativeColumn(1.5f);
                def.RelativeColumn(3.2f);
                def.ConstantColumn(50);
                def.ConstantColumn(50);
                def.ConstantColumn(50);
            });

            table.Header(h =>
            {
                HeaderCell(h.Cell(), "Área");
                HeaderCell(h.Cell(), "Asignatura", alignLeft: true);
                HeaderCell(h.Cell(), "T1");
                HeaderCell(h.Cell(), "T2");
                HeaderCell(h.Cell(), "T3");
            });

            if (list.Count == 0 || list.All(a => (a.Subjects?.Count ?? 0) == 0))
            {
                table.Cell().ColumnSpan(5).Element(EmptyCell).AlignCenter()
                    .Text("No hay asignaturas para mostrar en el boletín.").FontColor(Muted).Italic();
                return;
            }

            foreach (var area in list)
            {
                var subjects = area.Subjects ?? new List<SubjectBulletinDto>();
                if (subjects.Count == 0)
                    continue;

                for (var i = 0; i < subjects.Count; i++)
                {
                    var subject = subjects[i];
                    if (i == 0)
                        AreaGroupCell(table, Display(area.AreaName, "Sin área"), subjects.Count);

                    table.Cell().Element(BodyCellLeft).Text(Display(subject.SubjectName)).FontSize(8.5f);
                    MarkCell(table, subject.T1Display, subject.T1);
                    MarkCell(table, subject.T2Display, subject.T2);
                    MarkCell(table, subject.T3Display, subject.T3);
                }
            }
        });
    }

    private static void BuildHistoryTable(
        IContainer container,
        List<ProgramHistoryGradeColumnDto> grades,
        List<ProgramHistoryAreaDto> areas)
    {
        var populated = (areas ?? new List<ProgramHistoryAreaDto>())
            .Where(a => (a.Subjects?.Count ?? 0) > 0)
            .ToList();

        container.Decoration(decoration =>
        {
            decoration.Before().Element(e => BuildHistoryTableChrome(e, grades, body: null));
            decoration.Content().Column(col =>
            {
                if (populated.Count == 0)
                {
                    col.Item().Element(e => BuildHistoryTableChrome(e, grades, body: null, emptyMessage: true));
                    return;
                }

                foreach (var area in populated)
                    col.Item().Element(e => BuildHistoryTableChrome(e, grades, area));
            });
        });
    }

    private static void BuildHistoryTableChrome(
        IContainer container,
        List<ProgramHistoryGradeColumnDto> grades,
        ProgramHistoryAreaDto? body,
        bool emptyMessage = false)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                def.RelativeColumn(1.5f);
                def.RelativeColumn(3.0f);
                foreach (var _ in grades)
                    def.ConstantColumn(58);
            });

            if (body == null && !emptyMessage)
            {
                HeaderCell(table.Cell(), "Área", compact: true);
                HeaderCell(table.Cell(), "Asignatura", alignLeft: true, compact: true);
                foreach (var grade in grades)
                    HeaderCell(table.Cell(), Display(grade.GradeName), compact: true);
                return;
            }

            if (emptyMessage)
            {
                table.Cell().ColumnSpan((uint)Math.Max(2 + grades.Count, 1)).Element(EmptyCell).AlignCenter()
                    .Text("No hay asignaturas para este programa.").FontColor(Muted).Italic();
                return;
            }

            var subjects = body!.Subjects ?? new List<ProgramHistorySubjectDto>();
            for (var i = 0; i < subjects.Count; i++)
            {
                var subject = subjects[i];
                if (i == 0)
                    AreaGroupCell(table, Display(body.AreaName, "Sin área"), subjects.Count, compact: true);

                table.Cell().Element(BodyCellLeftCompact).Text(Display(subject.SubjectName)).FontSize(8);
                foreach (var col in grades)
                {
                    var result = (subject.GradeResults ?? new List<ProgramHistoryGradeResultDto>())
                        .FirstOrDefault(r => r.GradeNumber == col.GradeNumber);
                        MarkCell(table, result?.Display, result?.FinalAverage, bold: true, compact: true);
                }
            }
        });
    }

    private static void BuildMetaGrid(IContainer container, IReadOnlyList<(string Label, string Value)> items, bool compact = false)
    {
        container.Background(Colors.Grey.Lighten4)
            .PaddingVertical(compact ? 6 : 10)
            .PaddingHorizontal(compact ? 10 : 12)
            .Row(row =>
        {
            foreach (var item in items)
            {
                row.RelativeItem().PaddingRight(compact ? 8 : 10).Column(c =>
                {
                    c.Spacing(1);
                    c.Item().Text(item.Label).FontSize(7.5f).Bold().FontColor(Muted);
                    c.Item().Text(item.Value).FontSize(compact ? 9 : 10).FontColor(Colors.Grey.Darken3);
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

    private static void HeaderCell(IContainer cell, string text, bool alignLeft = false, bool compact = false)
    {
        var box = cell.Background(HeaderBlue)
            .PaddingVertical(compact ? 4 : 7)
            .PaddingHorizontal(compact ? 5 : 6)
            .AlignMiddle();
        (alignLeft ? box.AlignLeft() : box.AlignCenter())
            .Text(text).FontColor(Colors.White).SemiBold().FontSize(compact ? 8 : 8.5f);
    }

    private static void ScoreCell(TableDescriptor table, decimal? value, bool bold = false)
    {
        var text = FormatScore(value);
        var color = value.HasValue ? (value.Value >= 3.0m ? PassGreen : FailRed) : Muted;
        var span = table.Cell().Element(BodyCellCenter).Text(text).FontColor(color);
        if (bold)
            span.Bold();
    }

    private static void MarkCell(TableDescriptor table, string? display, decimal? score, bool bold = false, bool compact = false)
    {
        var text = string.IsNullOrWhiteSpace(display) ? FormatScore(score) : display;
        var color = score.HasValue
            ? (score.Value >= 3.0m ? PassGreen : FailRed)
            : Muted;
        var span = table.Cell()
            .Element(c => compact ? BodyCellCenterCompact(c) : BodyCellCenter(c))
            .Text(text).FontColor(color).FontSize(compact ? 8 : 9);
        if (bold)
            span.Bold();
    }

    private static void AreaGroupCell(TableDescriptor table, string areaName, int rowCount, bool compact = false)
    {
        table.Cell().RowSpan((uint)Math.Max(rowCount, 1)).Element(c =>
            c.Background(AreaFill)
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingHorizontal(compact ? 6 : 8)
                .PaddingVertical(compact ? 4 : 10)
                .AlignCenter()
                .AlignMiddle()
                .Text(areaName)
                .SemiBold()
                .FontSize(8)
                .FontColor(Colors.Grey.Darken3));
    }

    private static IContainer BodyCellLeft(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignMiddle();

    private static IContainer BodyCellCenter(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().AlignMiddle();

    private static IContainer BodyCellLeftCompact(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(5).AlignMiddle();

    private static IContainer BodyCellCenterCompact(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4).AlignCenter().AlignMiddle();

    private static IContainer EmptyCell(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8);

    private async Task<byte[]?> TryLoadLogoBytesAsync(string logoUrl, CancellationToken cancellationToken)
    {
        if (logoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            logoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await _httpBytesDownloadCache.GetOrDownloadAsync(
                logoUrl, MaxImageDownloadBytes, ImageDownloadTimeout, cancellationToken);
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxImageDownloadBytes)
                return null;
            return bytes;
        }

        return TryReadLocalLogo(logoUrl);
    }

    private byte[]? TryReadLocalLogo(string logoUrl)
    {
        if (IsUnsafeLocalLogoPath(logoUrl))
        {
            _logger.LogWarning("Boletín PDF: ruta de logo local rechazada por no ser relativa y segura.");
            return null;
        }

        string relative;
        if (logoUrl.StartsWith('/'))
            relative = logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        else if (!logoUrl.Contains('/'))
            relative = Path.Combine("uploads", "schools", logoUrl);
        else if (logoUrl.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            relative = logoUrl.Replace('/', Path.DirectorySeparatorChar);
        else
            return null;

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            return null;

        var rootFull = Path.GetFullPath(webRoot);
        var fullPath = Path.GetFullPath(Path.Combine(rootFull, relative));
        var relativeToRoot = Path.GetRelativePath(rootFull, fullPath);
        if (relativeToRoot.StartsWith("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeToRoot))
        {
            _logger.LogWarning("Boletín PDF: la ruta resuelta del logo queda fuera de wwwroot.");
            return null;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Boletín PDF: archivo de logo local no encontrado.");
            return null;
        }

        var bytes = File.ReadAllBytes(fullPath);
        return bytes.Length == 0 || bytes.Length > MaxImageDownloadBytes ? null : bytes;
    }

    private static bool IsUnsafeLocalLogoPath(string logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return true;
        if (logoUrl.Contains("..", StringComparison.Ordinal))
            return true;
        if (logoUrl.Contains('\\', StringComparison.Ordinal))
            return true;
        // Rechazar C:\... y UNC. No usar Path.IsPathRooted: en Windows "/uploads/..." es "rooted".
        if (logoUrl.Length >= 2 && char.IsLetter(logoUrl[0]) && logoUrl[1] == ':')
            return true;
        return logoUrl.StartsWith("//", StringComparison.Ordinal);
    }

    private static BulletinPdfIdentity ResolveIdentity(BulletinPdfIdentity? identity)
    {
        if (identity == null)
            return new BulletinPdfIdentity { SchoolName = NeutralSchoolName };

        return new BulletinPdfIdentity
        {
            SchoolName = string.IsNullOrWhiteSpace(identity.SchoolName)
                ? NeutralSchoolName
                : identity.SchoolName.Trim(),
            LogoBytes = IsValidPngOrJpeg(identity.LogoBytes) ? identity.LogoBytes : null
        };
    }

    internal static bool IsValidPngOrJpeg(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 4)
            return false;
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return true;
        return bytes[0] == 0xFF && bytes[1] == 0xD8;
    }

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
