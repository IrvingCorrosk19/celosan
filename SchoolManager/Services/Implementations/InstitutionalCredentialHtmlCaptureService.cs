using PuppeteerSharp;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Options;
using SchoolManager.Services;
using SchoolManager.Services.Interfaces;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Security.Claims;

namespace SchoolManager.Services.Implementations;

public class InstitutionalCredentialHtmlCaptureService : IInstitutionalCredentialHtmlCaptureService
{
    private readonly ILogger<InstitutionalCredentialHtmlCaptureService> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly StudentIdCardPdfPrintOptions _printOptions;
    private readonly IInstitutionalCredentialPdfService _pdfService;

    public InstitutionalCredentialHtmlCaptureService(
        ILogger<InstitutionalCredentialHtmlCaptureService> logger,
        IHttpContextAccessor http,
        IOptions<StudentIdCardPdfPrintOptions> printOptions,
        IInstitutionalCredentialPdfService pdfService)
    {
        _logger = logger;
        _http = http;
        _printOptions = printOptions.Value ?? new StudentIdCardPdfPrintOptions();
        _pdfService = pdfService;
    }

    public async Task<byte[]> GenerateFromUrl(string url)
    {
        var executablePath = await ResolveChromiumExecutablePath();
        _logger.LogInformation("[InstCredPdf] Chromium path: {Path}", executablePath);
        var launchOpts = BuildLaunchOptions(executablePath);

        byte[] frontImg;
        await using (var browser = await Puppeteer.LaunchAsync(launchOpts))
        {
            try
            {
                frontImg = await CaptureFrontFaceAsync(browser, url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[InstCredPdf] First capture failed, retry once");
                await Task.Delay(500);
                frontImg = await CaptureFrontFaceAsync(browser, url);
            }
        }

        return BuildPdfFromFaceImages(frontImg);
    }

    private static bool TryParseUserIdFromGenerateUrl(string url, out Guid userId)
    {
        userId = default;
        try
        {
            var path = new Uri(url, UriKind.Absolute).AbsolutePath.TrimEnd('/');
            var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
            return last != null && Guid.TryParse(last, out userId);
        }
        catch
        {
            return false;
        }
    }

    private byte[] BuildPdfFromFaceImages(byte[] frontImg)
    {
        var pageSize = ResolvePageSize();
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;

        return Document.Create(container =>
        {
            container.Page(p =>
            {
                p.Size(pageSize.WidthMm, pageSize.HeightMm, Unit.Millimetre);
                p.Margin(0);
                p.Content().Image(frontImg).FitArea();
            });
        }).GeneratePdf();
    }

    private LaunchOptions BuildLaunchOptions(string executablePath) =>
        new()
        {
            Headless = true,
            ExecutablePath = executablePath,
            Timeout = 60000,
            Args = BuildLaunchArgs()
        };

    private async Task<byte[]> CaptureFrontFaceAsync(IBrowser browser, string url)
    {
        await using var page = await browser.NewPageAsync();
        var pageSize = ResolvePageSize();

        page.DefaultNavigationTimeout = 60000;
        page.DefaultTimeout = 60000;

        var maxDpr = Math.Max(1, _printOptions.MaxDeviceScaleFactor);
        var baseDpr = Math.Clamp(_printOptions.DeviceScaleFactor, 1, maxDpr);
        await SetCaptureViewportAsync(page, pageSize, baseDpr);

        var reqCookies = _http.HttpContext?.Request.Cookies;
        if (reqCookies?.Count > 0)
        {
            var host = new Uri(url).Host;
            var cpList = reqCookies
                .Select(c => new CookieParam { Name = c.Key, Value = c.Value, Domain = host })
                .ToArray();
            await page.SetCookieAsync(cpList);
        }

        await page.GoToAsync(url, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Networkidle2],
            Timeout = 60000
        });
        await ApplyContentScale(page);
        await page.WaitForSelectorAsync("#idCardFront", new WaitForSelectorOptions { Timeout = 60000 });
        await Task.Delay(400);

        var front = await page.QuerySelectorAsync("#idCardFront")
            ?? throw new InvalidOperationException("No se encontró #idCardFront.");

        if (string.Equals(_printOptions.Profile, "CardPrinter", StringComparison.OrdinalIgnoreCase))
        {
            var box = await front.BoundingBoxAsync();
            if (box != null)
            {
                var bw = (double)box.Width;
                var bh = (double)box.Height;
                if (bw > 0.5 && bh > 0.5)
                {
                    var needDpr = Math.Max(pageSize.WidthPx / bw, pageSize.HeightPx / bh);
                    var optimal = (int)Math.Ceiling(needDpr - 1e-6);
                    optimal = Math.Clamp(Math.Max(baseDpr, optimal), 1, maxDpr);
                    if (optimal != baseDpr)
                    {
                        await SetCaptureViewportAsync(page, pageSize, optimal);
                        await Task.Delay(250);
                        front = await page.QuerySelectorAsync("#idCardFront")
                                ?? throw new InvalidOperationException("No se encontró #idCardFront.");
                    }
                }
            }
        }

        return await Capture(front, pageSize.WidthPx, pageSize.HeightPx);
    }

    private static Task SetCaptureViewportAsync(IPage page,
        (float WidthMm, float HeightMm, int WidthPx, int HeightPx) pageSize, int dpr) =>
        page.SetViewportAsync(new ViewPortOptions
        {
            Width = pageSize.WidthPx + 120,
            Height = pageSize.HeightPx + 120,
            DeviceScaleFactor = dpr
        });

    private async Task ApplyContentScale(IPage page)
    {
        var scale = (double)Math.Clamp((float)_printOptions.ContentScale, 0.85f, 1.00f);
        await page.EvaluateExpressionAsync(
            $"document.documentElement.style.zoom = '{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}';");
    }

    private (float WidthMm, float HeightMm, int WidthPx, int HeightPx) ResolvePageSize()
    {
        if (string.Equals(_printOptions.Profile, "A4Portrait", StringComparison.OrdinalIgnoreCase))
            return (148.0f, 235.0f, 1750, 2777);

        return (
            IdCardPhysicalDimensions.ShortMm,
            IdCardPhysicalDimensions.LongMm,
            IdCardPhysicalDimensions.PortraitWidthPx,
            IdCardPhysicalDimensions.PortraitHeightPx);
    }

    private static string[] BuildLaunchArgs()
    {
        var args = new List<string>
        {
            "--disable-dev-shm-usage",
            "--disable-gpu",
            "--disable-background-networking",
            "--disable-background-timer-throttling",
            "--disable-renderer-backgrounding"
        };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            args.Add("--no-sandbox");
            args.Add("--disable-setuid-sandbox");
        }

        return args.ToArray();
    }

    private async Task<string> ResolveChromiumExecutablePath()
    {
        var envPath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
            }
            : new[]
            {
                "/usr/bin/chromium",
                "/usr/bin/chromium-browser",
                "/usr/bin/google-chrome",
                "/snap/bin/chromium"
            };

        var localExecutable = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(localExecutable))
            return localExecutable;

        _logger.LogInformation("[InstCredPdf] Downloading managed Chromium...");
        var browserFetcher = new BrowserFetcher();
        var installed = await browserFetcher.DownloadAsync();
        return installed.GetExecutablePath();
    }

    private async Task<byte[]> Capture(IElementHandle el, int targetWidth, int targetHeight)
    {
        var img = await el.ScreenshotDataAsync(new ElementScreenshotOptions { Type = ScreenshotType.Png });

        using var input = SKBitmap.Decode(img);
        if (input.Width == targetWidth && input.Height == targetHeight)
        {
            using var data = input.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        using var resized = input.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
        using var image = SKImage.FromBitmap(resized);
        using var data2 = image.Encode(SKEncodedImageFormat.Png, 100);
        return data2.ToArray();
    }
}
