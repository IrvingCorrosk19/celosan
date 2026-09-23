using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManager.Dtos;
using SchoolManager.Services.Implementations;
using SchoolManager.Services.Interfaces;
using Xunit;

namespace SchoolManager.Tests;

public class StudentBulletinPdfServiceTests
{
    [Fact]
    public void Grade_pdf_without_logo_is_valid()
    {
        var pdf = CreateService().GenerateByGradePdf(SampleBulletin(), new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba"
        });

        AssertPdf(pdf);
    }

    [Fact]
    public async Task Cloudinary_https_logo_generates_pdf_when_reachable()
    {
        const string url = "https://res.cloudinary.com/dvtygdy0o/image/upload/v1777598074/schools/logos/ChatGPT_Image_30_abr_2026_08_07_59_p.m._eoifgr.png";
        byte[]? bytes;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            bytes = await http.GetByteArrayAsync(url);
        }
        catch
        {
            return;
        }

        Assert.True(bytes.Length > 4);
        Assert.True(
            (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) ||
            (bytes[0] == 0xFF && bytes[1] == 0xD8),
            "Cloudinary no devolvió PNG/JPEG");

        var pdf = CreateService().GenerateByGradePdf(SampleBulletin(), new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba",
            LogoBytes = bytes
        });
        AssertPdf(pdf);
    }

    [Fact]
    public void Grade_pdf_with_valid_png_logo_is_valid()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var pdf = CreateService().GenerateByGradePdf(SampleBulletin(), new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba",
            LogoBytes = png
        });

        AssertPdf(pdf);
    }

    [Fact]
    public void Grade_pdf_with_invalid_logo_bytes_is_valid_without_image()
    {
        var pdf = CreateService().GenerateByGradePdf(SampleBulletin(), new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba",
            LogoBytes = new byte[] { 1, 2, 3, 4 }
        });

        AssertPdf(pdf);
    }

    [Fact]
    public void Grade_pdf_with_pending_premedia_is_valid()
    {
        var bulletin = SampleBulletin();
        bulletin.PendingPremedia =
        [
            new PendingPremediaSubjectDto
            {
                SubjectId = Guid.NewGuid(),
                SubjectName = "Matemática",
                Grade = "9°",
                GradeNumber = 9,
                T1 = 2.5m,
                T2 = 3.0m,
                T3 = null,
                FinalAverage = 2.8m
            }
        ];

        var pdf = CreateService().GenerateByGradePdf(bulletin, new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba"
        });

        AssertPdf(pdf);
        AssertDoesNotContainPending(pdf);
    }

    [Fact]
    public void Program_pdf_with_two_large_tracks_and_logo_is_valid()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var history = SamplePremediaHistory();
        history.Tracks.Add(new ProgramHistoryTrackDto
        {
            SpecialtyId = Guid.NewGuid(),
            ProgramType = "Media",
            ProgramName = "BACHILLER EN INFORMÁTICA",
            Grades =
            [
                new() { GradeNumber = 10, GradeName = "10°", AcademicYear = "2024" },
                new() { GradeNumber = 11, GradeName = "11°", AcademicYear = "2025" },
                new() { GradeNumber = 12, GradeName = "12°", AcademicYear = "2026" }
            ],
            Areas = history.Tracks[0].Areas
        });

        var pdf = CreateService().GenerateProgramHistoryPdf(history, new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba",
            LogoBytes = png
        });

        AssertPdf(pdf);
        AssertDoesNotContainPending(pdf);
    }

    [Fact]
    public void Program_pdf_with_oversized_area_paginates()
    {
        var history = SamplePremediaHistory();
        var grades = history.Tracks[0].Grades;
        history.Tracks[0].Areas[0].Subjects = Enumerable.Range(1, 40)
            .Select(i => new ProgramHistorySubjectDto
            {
                SubjectId = Guid.NewGuid(),
                SubjectName = $"Asignatura extra {i}",
                GradeResults = grades.Select(g => new ProgramHistoryGradeResultDto
                {
                    GradeNumber = g.GradeNumber,
                    FinalAverage = 3.8m
                }).ToList()
            })
            .ToList();

        var pdf = CreateService().GenerateProgramHistoryPdf(history, new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba"
        });

        AssertPdf(pdf);
        var pages = System.Text.RegularExpressions.Regex.Matches(
            System.Text.Encoding.ASCII.GetString(pdf),
            @"/Type\s*/Page(?!s)").Count;
        Assert.True(pages >= 2, $"Expected multiple pages, got {pages}");
    }

    [Fact]
    public void Program_pdf_without_logo_is_valid()
    {
        var pdf = CreateService().GenerateProgramHistoryPdf(SampleHistory(), new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba"
        });

        AssertPdf(pdf);
    }

    [Fact]
    public void Program_pdf_premedia_section_fits_single_page()
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var pdf = CreateService().GenerateProgramHistoryPdf(SamplePremediaHistory(), new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba",
            LogoBytes = png
        });

        AssertPdf(pdf);
        AssertDoesNotContainPending(pdf);
        var pages = System.Text.RegularExpressions.Regex.Matches(
            System.Text.Encoding.ASCII.GetString(pdf),
            @"/Type\s*/Page(?!s)").Count;
        Assert.Equal(1, pages);
    }

    [Fact]
    public void Grade_pdf_with_grouped_areas_is_valid()
    {
        var bulletin = SampleBulletin();
        bulletin.Areas =
        [
            AreaWithSubjects("CIENTÍFICA", "Biología", "Física", "Química"),
            AreaWithSubjects("HUMANÍSTICA", "Español", "Historia", "Filosofía"),
            AreaWithSubjects("TECNOLÓGICA", "Informática", "Tecnología")
        ];

        var pdf = CreateService().GenerateByGradePdf(bulletin, new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba"
        });

        AssertPdf(pdf);
    }

    [Fact]
    public void Long_table_still_generates_pdf()
    {
        var bulletin = SampleBulletin();
        bulletin.Areas[0].Subjects = Enumerable.Range(1, 40)
            .Select(i => new SubjectBulletinDto
            {
                SubjectId = Guid.NewGuid(),
                SubjectName = $"Asignatura con nombre largo número {i} para verificar wrap",
                T1 = 4.5m,
                T2 = 3.2m,
                T3 = 2.8m,
                FinalAverage = 3.5m
            })
            .ToList();

        var pdf = CreateService().GenerateByGradePdf(bulletin, new BulletinPdfIdentity
        {
            SchoolName = "Colegio de Prueba"
        });

        AssertPdf(pdf);
    }

    [Fact]
    public async Task BuildIdentity_without_logo_url_uses_neutral_name_and_no_bytes()
    {
        var identity = await CreateService().BuildIdentityAsync("  ", null);

        Assert.Equal("Institución educativa", identity.SchoolName);
        Assert.Null(identity.LogoBytes);
    }

    [Fact]
    public async Task BuildIdentity_keeps_school_name_when_download_fails()
    {
        var identity = await CreateService().BuildIdentityAsync(
            "Escuela Real",
            "https://example.test/missing-logo.png");

        Assert.Equal("Escuela Real", identity.SchoolName);
        Assert.Null(identity.LogoBytes);
    }

    [Fact]
    public async Task BuildIdentity_loads_local_png_from_wwwroot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bulletin-pdf-logo-" + Guid.NewGuid().ToString("N"));
        var schoolsDir = Path.Combine(root, "uploads", "schools");
        Directory.CreateDirectory(schoolsDir);
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await File.WriteAllBytesAsync(Path.Combine(schoolsDir, "logo.png"), png);

        try
        {
            var env = new StubWebHostEnvironment { WebRootPath = root };
            var service = new StudentBulletinPdfService(
                new FakeHttpBytesDownloadCache(), env, NullLogger<StudentBulletinPdfService>.Instance);

            Assert.True(File.Exists(Path.Combine(schoolsDir, "logo.png")));

            var byFileName = await service.BuildIdentityAsync("Escuela Local", "logo.png");
            var byRelative = await service.BuildIdentityAsync("Escuela Local", "/uploads/schools/logo.png");
            var missing = await service.BuildIdentityAsync("Escuela Local", "no-existe.png");

            Assert.True(byFileName.LogoBytes != null, $"filename logo null. webRoot={env.WebRootPath}");
            Assert.True(byRelative.LogoBytes != null, "relative logo null");
            Assert.Null(missing.LogoBytes);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static StudentBulletinPdfService CreateService() =>
        new(new FakeHttpBytesDownloadCache(), new StubWebHostEnvironment(), NullLogger<StudentBulletinPdfService>.Instance);

    private static AreaBulletinDto AreaWithSubjects(string areaName, params string[] subjects) =>
        new()
        {
            AreaId = Guid.NewGuid(),
            AreaName = areaName,
            Subjects = subjects.Select(name => new SubjectBulletinDto
            {
                SubjectId = Guid.NewGuid(),
                SubjectName = name,
                T1 = 4.0m,
                T2 = 3.5m,
                T3 = 4.2m,
                FinalAverage = 3.9m
            }).ToList()
        };

    private static StudentBulletinDto SampleBulletin() => new()
    {
        StudentId = Guid.NewGuid(),
        StudentName = "Estudiante Demo",
        Grade = "11°",
        Group = "A",
        Specialty = "Ciencias",
        AcademicYear = "2026",
        Areas =
        [
            new AreaBulletinDto
            {
                AreaId = Guid.NewGuid(),
                AreaName = "Ciencias",
                Subjects =
                [
                    new SubjectBulletinDto
                    {
                        SubjectId = Guid.NewGuid(),
                        SubjectName = "Física",
                        T1 = 4.0m,
                        T2 = 3.5m,
                        T3 = 4.2m,
                        FinalAverage = 3.9m
                    }
                ]
            }
        ]
    };

    private static StudentProgramHistoryDto SamplePremediaHistory()
    {
        var grades = new List<ProgramHistoryGradeColumnDto>
        {
            new() { GradeNumber = 7, GradeName = "7°", AcademicYear = "2024" },
            new() { GradeNumber = 8, GradeName = "8°", AcademicYear = "2025" },
            new() { GradeNumber = 9, GradeName = "9°", AcademicYear = "2026" }
        };

        ProgramHistorySubjectDto Subject(string name) => new()
        {
            SubjectId = Guid.NewGuid(),
            SubjectName = name,
            GradeResults = grades.Select(g => new ProgramHistoryGradeResultDto
            {
                GradeNumber = g.GradeNumber,
                FinalAverage = 3.8m
            }).ToList()
        };

        return new StudentProgramHistoryDto
        {
            StudentId = Guid.NewGuid(),
            StudentName = "Estudiante Premedia",
            Tracks =
            [
                new ProgramHistoryTrackDto
                {
                    SpecialtyId = Guid.NewGuid(),
                    ProgramType = "Premedia",
                    ProgramName = "PRE-MEDIA",
                    Grades = grades,
                    Areas =
                    [
                        new ProgramHistoryAreaDto
                        {
                            AreaId = Guid.NewGuid(),
                            AreaName = "HUMANÍSTICA",
                            Subjects =
                            [
                                Subject("ESPAÑOL"),
                                Subject("HISTORIA"),
                                Subject("GEOGRAFÍA"),
                                Subject("CÍVICA"),
                                Subject("VAL. ÉTICOS / REL. HUMANAS"),
                                Subject("RELACIONES LABORALES"),
                                Subject("INGLÉS"),
                                Subject("ORIENTACIÓN")
                            ]
                        },
                        new ProgramHistoryAreaDto
                        {
                            AreaId = Guid.NewGuid(),
                            AreaName = "CIENTÍFICA",
                            Subjects =
                            [
                                Subject("MATEMÁTICA"),
                                Subject("CIENCIAS NATURALES"),
                                Subject("SALUD FÍSICA Y MENTAL")
                            ]
                        },
                        new ProgramHistoryAreaDto
                        {
                            AreaId = Guid.NewGuid(),
                            AreaName = "TECNOLÓGICA",
                            Subjects =
                            [
                                Subject("FDC 1"),
                                Subject("MET")
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static StudentProgramHistoryDto SampleHistory() => new()
    {
        StudentId = Guid.NewGuid(),
        StudentName = "Estudiante Demo",
        Tracks =
        [
            new ProgramHistoryTrackDto
            {
                SpecialtyId = Guid.NewGuid(),
                ProgramName = "Bachiller en Ciencias",
                Grades =
                [
                    new ProgramHistoryGradeColumnDto
                    {
                        GradeNumber = 11,
                        GradeName = "11°",
                        AcademicYear = "2026"
                    }
                ],
                Areas =
                [
                    new ProgramHistoryAreaDto
                    {
                        AreaId = Guid.NewGuid(),
                        AreaName = "Ciencias",
                        Subjects =
                        [
                            new ProgramHistorySubjectDto
                            {
                                SubjectId = Guid.NewGuid(),
                                SubjectName = "Física",
                                GradeResults =
                                [
                                    new ProgramHistoryGradeResultDto
                                    {
                                        GradeNumber = 11,
                                        FinalAverage = 3.9m
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        ]
    };

    private static void AssertPdf(byte[] pdf)
    {
        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 100);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    private static void AssertDoesNotContainPending(byte[] pdf)
    {
        var text = System.Text.Encoding.Latin1.GetString(pdf);
        Assert.DoesNotContain("PREMEDIA", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PENDIENTES", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeHttpBytesDownloadCache : IHttpBytesDownloadCache
    {
        public Task<byte[]?> GetOrDownloadAsync(
            string absoluteUrl,
            int maxBytes,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SchoolManager.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
