namespace SchoolManager.Helpers;

/// <summary>
/// Orden visual de las hojas MEDUCA. No altera horas ni CurriculumLoadSubjectId.
/// </summary>
public static class CurriculumLoadDisplayOrder
{
    public const int UnknownAreaRank = 99;
    public const int UnknownSubjectRank = 10_000;
    public const string PremediaTechDisplayName =
        "TECNOLOGÍAS (FAMILIA Y DESARROLLO COMUNITARIO, INFORMÁTICA, COMERCIO)";

    public static int AreaRank(string? areaName)
    {
        var key = Normalize(areaName);
        if (key.Contains("HUMANIST"))
            return 1;
        if (key.Contains("CIENTIFIC"))
            return 2;
        if (key.Contains("TECNOLOGIC"))
            return 3;
        return UnknownAreaRank;
    }

    public static int SubjectRank(string? programName, string? areaName, string? subjectName)
    {
        var program = ResolveProgram(programName);
        if (program == null)
            return UnknownSubjectRank;

        if (!SheetOrder.TryGetValue((program, AreaRank(areaName)), out var subjects))
            return UnknownSubjectRank;

        var key = Normalize(subjectName);
        var index = subjects.IndexOf(key);
        return index >= 0 ? index : UnknownSubjectRank;
    }

    public static bool IsRecognized(string? programName, string? areaName, string? subjectName) =>
        SubjectRank(programName, areaName, subjectName) < UnknownSubjectRank;

    public static bool IsPremediaTechAlias(string? subjectName)
    {
        var key = Normalize(subjectName);
        return key is "FDC 1" or "MET" or "FDC 2" or "D.L." or "MCA 1" or "MCA 2";
    }

    public static bool IsCombinedLogicPhilosophy(string? subjectName) =>
        Normalize(subjectName) == Normalize("LÓGICA / FILOSOFÍA");

    public static string DisplayAreaName(string? areaName)
    {
        return AreaRank(areaName) switch
        {
            1 => "HUMANÍSTICA",
            2 => "CIENTÍFICA",
            3 => "TECNOLÓGICA",
            _ => areaName ?? string.Empty
        };
    }

    public static string DisplaySubjectName(string? programName, string? subjectName)
    {
        var program = ResolveProgram(programName);
        var key = Normalize(subjectName);
        if (program == "INFORMATICA" && key == Normalize("ESPAÑOL (LENGUAJE Y COMUNICACIÓN)"))
            return "ESPAÑOL";
        return subjectName ?? string.Empty;
    }

    public static bool IsUnofficialEmptyDuplicate(string? programName, string? subjectName)
    {
        var program = ResolveProgram(programName);
        var key = Normalize(subjectName);
        if (program != "INFORMATICA")
            return false;
        return key == Normalize("ESPAÑOL")
               || key == Normalize("ÉTICA MORAL, VALORES Y RELACIONES HUMANAS");
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Trim().ToUpperInvariant().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                'Á' or 'À' or 'Ä' => 'A',
                'É' or 'È' or 'Ë' => 'E',
                'Í' or 'Ì' or 'Ï' => 'I',
                'Ó' or 'Ò' or 'Ö' => 'O',
                'Ú' or 'Ù' or 'Ü' => 'U',
                'Ñ' => 'N',
                _ => chars[i]
            };
        }

        return new string(chars);
    }

    private static string? ResolveProgram(string? programName)
    {
        var key = Normalize(programName);
        if (key.Contains("PRE-MEDIA") || key.Contains("PREMEDIA"))
            return "PREMEDIA";
        if (key.Contains("INFORMATICA"))
            return "INFORMATICA";
        if (key.Contains("TURISMO"))
            return "TURISMO";
        if (key.Contains("AUTOTRONICA"))
            return "AUTOTRONICA";
        if (key.Contains("ELECTRICIDAD"))
            return "ELECTRICIDAD";
        return null;
    }

    private static readonly Dictionary<(string Program, int Area), List<string>> SheetOrder =
        new()
        {
            [("PREMEDIA", 1)] = NormalizeList(
                "ESPAÑOL",
                "HISTORIA",
                "GEOGRAFÍA",
                "CÍVICA",
                "VAL. ÉTICOS / REL. HUMANAS",
                "RELACIONES LABORALES",
                "INGLÉS",
                "ORIENTACIÓN",
                "EXPRESIONES ARTÍSTICA",
                "MÚSICA",
                "BELLAS ARTES"),
            [("PREMEDIA", 2)] = NormalizeList(
                "MATEMÁTICA",
                "CIENCIAS NATURALES",
                "SALUD FÍSICA Y MENTAL"),
            [("PREMEDIA", 3)] = NormalizeList(
                PremediaTechDisplayName),

            [("INFORMATICA", 1)] = NormalizeList(
                "ESPAÑOL",
                "INGLÉS COMERCIAL",
                "GEOGRAFÍA DE PANAMÁ",
                "HISTORIA DE PANAMÁ",
                "ÉTICA Y VALORES",
                "BELLAS ARTES",
                "HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.",
                "CÍVICA III",
                "LÓGICA",
                "FILOSOFÍA"),
            [("INFORMATICA", 2)] = NormalizeList(
                "MATEMÁTICA",
                "CIENCIAS INTEGRADAS",
                "QUÍMICA",
                "FÍSICA",
                "EDUCACIÓN FÍSICA"),
            [("INFORMATICA", 3)] = NormalizeList(
                "TECNOLOGÍA DE LA INFORMACIÓN",
                "CONFIGURACIÓN Y ADMINISTRACIÓN DE SISTEMAS OPERATIVOS",
                "DESARROLLO LÓGICO Y PROGRAMACIÓN",
                "ARQUITECTURA DE LAS COMPUTADORAS",
                "PROGRAMACIÓN",
                "MULTIMEDIA Y DESARROLLO WEB",
                "REDES DE COMPUTADORAS",
                "TALLER DE SISTEMAS ROBÓTICOS",
                "APLICACIONES CON BASE DE DATOS",
                "GESTIÓN EMPRESARIAL",
                "PRÁCTICA PROFESIONAL"),

            [("TURISMO", 1)] = NormalizeList(
                "ESPAÑOL (LENGUAJE Y COMUNICACIÓN)",
                "INGLÉS (LENGUAJE Y COMUNICACIÓN)",
                "FRANCÉS",
                "GEOGRAFÍA DE PANAMÁ",
                "GEOGRAFÍA TURÍSTICA DE PANAMÁ",
                "GEOGRAFÍA TURÍSTICA DEL MUNDO",
                "HISTORIA DE PANAMÁ",
                "HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.",
                "CÍVICA",
                "ÉTICA MORAL, VALORES Y RELACIONES HUMANAS",
                "BELLAS ARTES"),
            [("TURISMO", 2)] = NormalizeList(
                "MATEMÁTICA",
                "EDUCACIÓN FÍSICA Y SALUD INTEGRAL"),
            [("TURISMO", 3)] = NormalizeList(
                "TECNOLOGÍA DE LA INFORMACIÓN",
                "TECNOLOGÍA COMERCIAL",
                "CONTABILIDAD",
                "TURISMO (INTRODUCCIÓN AL TURISMO Y CULTURA TURÍSTICA)",
                "TURISMO SOSTENIBLE",
                "GESTIÓN EMPRESARIAL TURÍSTICA",
                "SERVICIOS TURÍSTICOS I Y II",
                "MERCADOTECNIA Y PUBLICIDAD",
                "OFIMÁTICA",
                "ELABORACIÓN DE PROYECTOS TURÍSTICOS",
                "PRÁCTICA PROFESIONAL"),

            [("AUTOTRONICA", 1)] = NormalizeList(
                "ESPAÑOL (LENGUAJE Y COMUNICACIÓN)",
                "INGLÉS (LENGUAJE Y COMUNICACIÓN)",
                "GEOGRAFÍA DE PANAMÁ",
                "ÉTICA, MORAL, VALORES Y RELACIONES HUMANAS",
                "BELLAS ARTES",
                "HISTORIA DE PANAMÁ",
                "HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.",
                "CÍVICA",
                "LÓGICA",
                "FILOSOFÍA"),
            [("AUTOTRONICA", 2)] = NormalizeList(
                "MATEMÁTICA",
                "EDUCACIÓN FÍSICA Y SALUD INTEGRAL",
                "CIENCIAS NATURALES",
                "QUÍMICA",
                "FÍSICA"),
            [("AUTOTRONICA", 3)] = NormalizeList(
                "TECNOLOGÍA DE LA INFORMACIÓN",
                "DIBUJO I RELACIONADO",
                "TALLER I (FUNDAMENTO DE TECNOLOGÍA INDUSTRIAL)",
                "TALLER II (DIAGNÓSTICO AUTOMOTRIZ AUTOMATIZADO)",
                "TALLER III (TECNOLOGÍA Y TALLER APLICADO)",
                "TALLER IV (ELECTRICIDAD Y ELECTRÓNICA AUTOMOTRIZ)",
                "TALLER V (MANTENIMIENTO AUTOMOTRIZ)",
                "GESTIÓN EMPRESARIAL",
                "PRÁCTICA PROFESIONAL"),

            [("ELECTRICIDAD", 1)] = NormalizeList(
                "ESPAÑOL (LENGUAJE Y COMUNICACIÓN)",
                "INGLÉS (LENGUAJE Y COMUNICACIÓN)",
                "GEOGRAFÍA DE PANAMÁ",
                "ÉTICA, MORAL, VALORES Y RELACIONES HUMANAS",
                "BELLAS ARTES",
                "HISTORIA DE PANAMÁ",
                "HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.",
                "CÍVICA",
                "LÓGICA",
                "FILOSOFÍA"),
            [("ELECTRICIDAD", 2)] = NormalizeList(
                "MATEMÁTICA",
                "EDUCACIÓN FÍSICA Y SALUD INTEGRAL",
                "CIENCIAS NATURALES",
                "QUÍMICA",
                "FÍSICA"),
            [("ELECTRICIDAD", 3)] = NormalizeList(
                "TECNOLOGÍA DE LA INFORMACIÓN",
                "DIBUJO I (LINEAL)",
                "TALLER I (DE EQUIPO Y MEDICIONES)",
                "SEGURIDAD INDUSTRIAL",
                "DIBUJO II (APLICACIÓN Y ASISTENCIA POR COMPUTADORA)",
                "TALLER II (INSTALACIÓN RESIDENCIAL Y COMERCIAL)",
                "TALLER III (ELECTRÓNICA)",
                "TALLER IV (ANÁLISIS DE CIRCUITO)",
                "TALLER V (MÁQUINAS ELÉCTRICAS)",
                "TALLER DE PRODUCCIÓN Y DISTRIBUCIÓN",
                "TALLER DE PROYECTOS Y PRESUPUESTOS",
                "GESTIÓN EMPRESARIAL")
        };

    private static List<string> NormalizeList(params string[] names) =>
        names.Select(Normalize).ToList();
}
