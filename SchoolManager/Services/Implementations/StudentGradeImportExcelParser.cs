using System.Globalization;
using System.Text;
using OfficeOpenXml;
using SchoolManager.Helpers;

namespace SchoolManager.Services.Implementations;

internal sealed class GradeImportParseException : Exception
{
    public GradeImportParseException(string message) : base(message) { }
}

internal sealed class GradeImportParsedRow
{
    public int RowNumber { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string GradeRaw { get; set; } = string.Empty;
    public string GroupRaw { get; set; } = string.Empty;
    public string ShiftRaw { get; set; } = string.Empty;
    public List<GradeImportParsedScore> Scores { get; set; } = new();
}

internal sealed class GradeImportParsedScore
{
    public string TrimesterCode { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public bool HasFormulaWithoutValue { get; set; }
    public decimal? Score { get; set; }
    public string? ScoreError { get; set; }
}

internal sealed class GradeImportParseResult
{
    public string SheetName { get; set; } = string.Empty;
    public int DataRowCount { get; set; }
    public List<GradeImportParsedRow> Rows { get; set; } = new();
}

public static class StudentGradeImportExcelParser
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxDataRows = 5000;

    private static bool _licenseSet;

    internal static GradeImportParseResult Parse(Stream stream, string fileName)
    {
        EnsureLicense();
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new GradeImportParseException("Solo se aceptan archivos .xlsx.");

        ExcelPackage package;
        try
        {
            package = new ExcelPackage(stream);
        }
        catch (Exception)
        {
            throw new GradeImportParseException("El archivo está corrupto o no es un .xlsx válido.");
        }

        using (package)
        {
            if (package.Workbook.Worksheets.Count == 0)
                throw new GradeImportParseException("El archivo no contiene hojas.");

            var sheet = SelectSheet(package);
            if (sheet.Dimension == null)
                throw new GradeImportParseException("La hoja seleccionada está vacía.");

            var startRow = sheet.Dimension.Start.Row;
            var endRow = sheet.Dimension.End.Row;
            var startCol = sheet.Dimension.Start.Column;
            var endCol = sheet.Dimension.End.Column;
            if (endRow < startRow + 1)
                throw new GradeImportParseException("El archivo no contiene filas de datos.");

            var headers = new List<string>();
            for (var col = startCol; col <= endCol; col++)
                headers.Add(ReadHeader(sheet.Cells[startRow, col]));

            var map = ResolveColumns(headers, sheet, startRow, endRow, startCol);
            var rows = new List<GradeImportParsedRow>();
            var dataRows = 0;

            for (var r = startRow + 1; r <= endRow; r++)
            {
                if (IsRowEmpty(sheet, r, startCol, endCol))
                    continue;

                dataRows++;
                if (dataRows > MaxDataRows)
                    throw new GradeImportParseException($"El archivo supera el máximo de {MaxDataRows} filas de datos.");

                var parsed = new GradeImportParsedRow
                {
                    RowNumber = r,
                    DocumentId = ReadText(sheet.Cells[r, map.DocumentCol]),
                    Email = ReadText(sheet.Cells[r, map.EmailCol]),
                    FirstName = map.NameCol.HasValue ? ReadText(sheet.Cells[r, map.NameCol.Value]) : string.Empty,
                    LastName = map.LastNameCol.HasValue ? ReadText(sheet.Cells[r, map.LastNameCol.Value]) : string.Empty,
                    SubjectName = ReadText(sheet.Cells[r, map.SubjectCol]),
                    GradeRaw = ReadText(sheet.Cells[r, map.GradeCol]),
                    GroupRaw = ReadText(sheet.Cells[r, map.GroupCol]),
                    ShiftRaw = ReadText(sheet.Cells[r, map.ShiftCol])
                };

                foreach (var (code, col) in map.ScoreColumns)
                {
                    var cell = sheet.Cells[r, col];
                    parsed.Scores.Add(ReadScore(code, cell));
                }

                rows.Add(parsed);
            }

            if (dataRows == 0)
                throw new GradeImportParseException("El archivo no contiene filas de datos.");

            return new GradeImportParseResult
            {
                SheetName = sheet.Name,
                DataRowCount = dataRows,
                Rows = rows
            };
        }
    }

    private static ExcelWorksheet SelectSheet(ExcelPackage package)
    {
        var named = package.Workbook.Worksheets
            .FirstOrDefault(s => string.Equals(s.Name?.Trim(), "Notas", StringComparison.OrdinalIgnoreCase));
        if (named != null && named.Dimension != null)
            return named;

        var first = package.Workbook.Worksheets.FirstOrDefault(s => s.Dimension != null);
        if (first == null)
            throw new GradeImportParseException("No se encontró una hoja con datos.");
        return first;
    }

    private sealed class ColumnMap
    {
        public int DocumentCol { get; set; }
        public int EmailCol { get; set; }
        public int? NameCol { get; set; }
        public int? LastNameCol { get; set; }
        public int SubjectCol { get; set; }
        public int GradeCol { get; set; }
        public int GroupCol { get; set; }
        public int ShiftCol { get; set; }
        public List<(string Code, int Col)> ScoreColumns { get; set; } = new();
    }

    private static ColumnMap ResolveColumns(
        List<string> headers,
        ExcelWorksheet sheet,
        int headerRow,
        int endRow,
        int startCol)
    {
        int? FindExact(params string[] keys)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                var h = NormalizeHeader(headers[i]);
                if (keys.Any(k => h == k))
                    return startCol + i;
            }
            return null;
        }

        var document = FindExact("DOCUMENTO ID", "DOCUMENTOID", "DOCUMENTO");
        var email = FindExact("ESTUDIANTE EMAIL", "ESTUDIANTE (EMAIL)", "EMAIL", "CORREO", "ESTUDIANTE");
        var subject = FindExact("ASIGNATURA");
        var grade = FindExact("NIVEL", "GRADO");
        var group = FindExact("GRUPO ACADEMICO", "GRUPO");
        var shift = FindExact("JORNADA");
        var name = FindExact("NOMBRE");
        var lastName = FindExact("APELLIDO");

        var missing = new List<string>();
        if (!document.HasValue) missing.Add("Documento ID");
        if (!email.HasValue) missing.Add("Email");
        if (!subject.HasValue) missing.Add("Asignatura");
        if (!grade.HasValue) missing.Add("Nivel");
        if (!group.HasValue) missing.Add("Grupo");
        if (!shift.HasValue) missing.Add("Jornada");
        if (missing.Count > 0)
            throw new GradeImportParseException("ERROR DE ARCHIVO. Faltan columnas obligatorias: " + string.Join(", ", missing) + ".");

        var scores = ResolveScoreColumns(headers, sheet, headerRow, endRow, startCol);
        if (scores.Count == 0)
            throw new GradeImportParseException("ERROR DE ARCHIVO. No se pudieron identificar de forma inequívoca las columnas de T1/T2/T3.");

        return new ColumnMap
        {
            DocumentCol = document.Value,
            EmailCol = email.Value,
            NameCol = name,
            LastNameCol = lastName,
            SubjectCol = subject.Value,
            GradeCol = grade.Value,
            GroupCol = group.Value,
            ShiftCol = shift.Value,
            ScoreColumns = scores
        };
    }

    private static List<(string Code, int Col)> ResolveScoreColumns(
        List<string> headers,
        ExcelWorksheet sheet,
        int headerRow,
        int endRow,
        int startCol)
    {
        var named = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            var h = NormalizeHeader(headers[i]);
            var code = HeaderToTrimester(h);
            if (code == null)
                continue;

            var col = startCol + i;
            if (h.StartsWith("NOTA", StringComparison.Ordinal))
                named[code] = col;
            else if (!named.ContainsKey(code))
                named[code] = col;
        }

        if (named.Count > 0)
            return named.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();

        var pairs = new List<(string Code, int Col)>();
        for (var i = 0; i < headers.Count; i++)
        {
            var h = NormalizeHeader(headers[i]);
            if (h != "TRIMESTRE")
                continue;

            var triCol = startCol + i;
            var notaCol = (i + 1 < headers.Count && NormalizeHeader(headers[i + 1]) == "NOTA")
                ? startCol + i + 1
                : (int?)null;
            if (!notaCol.HasValue)
                throw new GradeImportParseException("ERROR DE ARCHIVO. Cada columna Trimestre debe ir seguida de una columna Nota.");

            var code = InferTrimesterFromColumn(sheet, triCol, headerRow, endRow);
            if (code == null)
                throw new GradeImportParseException("ERROR DE ARCHIVO. No se pudo determinar de forma inequívoca el trimestre de una columna Trimestre/Nota.");
            if (pairs.Any(p => p.Code == code))
                throw new GradeImportParseException("ERROR DE ARCHIVO. Hay columnas Trimestre/Nota duplicadas para " + code + ".");

            pairs.Add((code, notaCol.Value));
        }

        return pairs;
    }

    private static string? InferTrimesterFromColumn(ExcelWorksheet sheet, int col, int headerRow, int endRow)
    {
        var headerCode = HeaderToTrimester(NormalizeHeader(ReadHeader(sheet.Cells[headerRow, col])));
        if (headerCode != null)
            return headerCode;

        string? found = null;
        for (var r = headerRow + 1; r <= endRow; r++)
        {
            var raw = ReadText(sheet.Cells[r, col]);
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var code = NormalizeTrimester(raw);
            if (code is not ("1T" or "2T" or "3T"))
                return null;
            if (found == null)
                found = code;
            else if (found != code)
                return null;
        }

        return found;
    }

    private static GradeImportParsedScore ReadScore(string code, ExcelRange cell)
    {
        var result = new GradeImportParsedScore { TrimesterCode = code };
        var hasFormula = !string.IsNullOrWhiteSpace(cell.Formula);
        var value = cell.Value;
        if (hasFormula && (value == null || string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture))))
        {
            result.HasFormulaWithoutValue = true;
            result.ScoreError = "La celda contiene una fórmula sin valor calculado almacenado.";
            result.RawValue = cell.Formula;
            return result;
        }

        if (value == null)
            return result;

        result.RawValue = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(result.RawValue))
            return result;

        if (!TryParseScore(value, out var score, out var error))
        {
            result.ScoreError = error;
            return result;
        }

        result.Score = score;
        return result;
    }

    internal static bool TryParseScore(object value, out decimal score, out string? error)
    {
        score = 0;
        error = null;
        switch (value)
        {
            case double d:
                return ValidateScore((decimal)d, out score, out error);
            case decimal dec:
                return ValidateScore(dec, out score, out error);
            case float f:
                return ValidateScore((decimal)f, out score, out error);
            case int i:
                return ValidateScore(i, out score, out error);
            case long l:
                return ValidateScore(l, out score, out error);
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "La nota está vacía.";
            return false;
        }

        text = text.Replace(',', '.');
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            error = "La nota no es un número válido.";
            return false;
        }

        return ValidateScore(parsed, out score, out error);
    }

    private static bool ValidateScore(decimal raw, out decimal score, out string? error)
    {
        score = 0;
        error = null;
        var rounded = Math.Round(raw, 1, MidpointRounding.AwayFromZero);
        if (Math.Abs(raw - rounded) > 0.0000001m)
        {
            error = "La nota debe tener como máximo una cifra decimal.";
            return false;
        }

        if (rounded < 1.0m || rounded > 5.0m)
        {
            error = "La nota debe estar entre 1.0 y 5.0.";
            return false;
        }

        score = rounded;
        return true;
    }

    private static string? HeaderToTrimester(string header)
    {
        return header switch
        {
            "T1" or "1T" or "NOTA T1" or "NOTA 1T" or "NOTAT1" or "NOTA1T" => "1T",
            "T2" or "2T" or "NOTA T2" or "NOTA 2T" or "NOTAT2" or "NOTA2T" => "2T",
            "T3" or "3T" or "NOTA T3" or "NOTA 3T" or "NOTAT3" or "NOTA3T" => "3T",
            _ => null
        };
    }

    internal static string NormalizeHeader(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        text = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is '(' or ')' or '.' or ':' or '_' or '-')
                sb.Append(' ');
            else
                sb.Append(char.ToUpperInvariant(ch));
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal static string NormalizeKey(string? value) => NormalizeHeader(value);

    internal static string NormalizeTrimester(string? value)
    {
        var t = NormalizeHeader(value).Replace(" ", string.Empty);
        if (t is "T1" or "1T") return "1T";
        if (t is "T2" or "2T") return "2T";
        if (t is "T3" or "3T") return "3T";
        return t;
    }

    internal static int? NormalizeGradeNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) && value != "0")
        {
            if (value is null) return null;
        }

        if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var numeric))
            return (int)Math.Truncate(numeric);

        return EnrollmentTypeConstants.ParseGradeNumber(value);
    }

    private static string ReadHeader(ExcelRange cell) =>
        Convert.ToString(cell.Value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static string ReadText(ExcelRange cell)
    {
        if (cell.Value == null)
            return string.Empty;
        if (cell.Value is double d && Math.Abs(d - Math.Truncate(d)) < 0.0000001)
            return ((long)d).ToString(CultureInfo.InvariantCulture);
        return Convert.ToString(cell.Value, CultureInfo.CurrentCulture)?.Trim() ?? string.Empty;
    }

    private static bool IsRowEmpty(ExcelWorksheet sheet, int row, int startCol, int endCol)
    {
        for (var c = startCol; c <= endCol; c++)
        {
            if (!string.IsNullOrWhiteSpace(ReadText(sheet.Cells[row, c])))
                return false;
        }
        return true;
    }

    private static void EnsureLicense()
    {
        if (_licenseSet)
            return;
        ExcelPackage.License.SetNonCommercialOrganization("Celosan");
        _licenseSet = true;
    }
}
