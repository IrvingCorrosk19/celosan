namespace SchoolManager.Services.Implementations;

public class StudentIdCardPdfPrintOptions
{
    public const string SectionName = "StudentIdCardPdf";

    // Valores soportados: CardPrinter, A4Portrait
    public string Profile { get; set; } = "CardPrinter";

    // Escala de contenido de la página HTML antes de capturar.
    // 1.00 = sin ajuste, 0.95 = reduce 5% para dar aire a textos largos.
    public decimal ContentScale { get; set; } = 0.96m;

    /// <summary>Factor de píxeles del viewport de Chromium al capturar (mínimo; puede subirse solo para CardPrinter según el tamaño CSS del carnet). 3 mejora nitidez de foto y tipografía respecto a 2 sin cambiar el layout CSS.</summary>
    public int DeviceScaleFactor { get; set; } = 3;

    /// <summary>Tope de DPR al ajustar la captura (Chromium suele tolerar 3–4 sin problema).</summary>
    public int MaxDeviceScaleFactor { get; set; } = 4;
}
