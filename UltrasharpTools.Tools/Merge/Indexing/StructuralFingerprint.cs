using System.Security.Cryptography;

using Microsoft.Extensions.Logging.Abstractions;

namespace UltrasharpTools.Tools.Merge.Indexing;

/// <summary>
/// Генерирует структурный fingerprint (AST hash) для кода.
/// Игнорирует whitespace, comments, formatting - фокусируется на структуре.
/// КРИТИЧНО: два семантически идентичных кода должны иметь одинаковый StructuralHash.
/// </summary>
public sealed class StructuralFingerprint
{
    private readonly ILogger<StructuralFingerprint> _logger;

    public StructuralFingerprint(ILogger<StructuralFingerprint>? logger = null)
    {
        _logger = logger ?? NullLogger<StructuralFingerprint>.Instance;
    }

    /// <summary>
    /// Вычислить структурный hash для произвольного контента.
    /// Автоматически определяет тип файла и применяет соответствующую нормализацию.
    /// </summary>
    public string ComputeStructuralHash(string content, string? fileName = null)
    {
        try
        {
            // Определить тип контента по расширению
            var fileExtension = fileName != null
            ? Path.GetExtension(fileName).ToLowerInvariant()
            : null;

            string normalizedContent;

            switch (fileExtension)
            {
                case ".cs":
                    normalizedContent = NormalizeCSharpAst(content);
                    break;

                case ".json":
                    normalizedContent = NormalizeJson(content);
                    break;

                case ".xml":
                case ".csproj":
                case ".config":
                    normalizedContent = NormalizeXml(content);
                    break;

                default:
                    // Для неизвестных типов - базовая нормализация
                    normalizedContent = NormalizeGeneric(content);
                    break;
            }

            // Вычислить SHA256 hash
            return ComputeSha256Hash(normalizedContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
            "Failed to compute structural hash for {FileName}, falling back to content hash",
            fileName ?? "<unknown>");

            // Fallback: простая нормализация
            var fallbackContent = NormalizeGeneric(content);
            return ComputeSha256Hash(fallbackContent);
        }
    }

    /// <summary>
    /// Нормализовать C# AST: убрать trivia, whitespace, комментарии.
    /// </summary>
    private string NormalizeCSharpAst(string code)
    {
        // 1. Парсим код в SyntaxTree
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        // 2. Удаляем все trivia (whitespace, comments, directives)
        var normalizedRoot = RemoveAllTrivia(root);

        // 3. Применяем canonical formatting (минимальный whitespace)
        var formatted = normalizedRoot.NormalizeWhitespace(
        indentation: "",
        eol: "\n",
        elasticTrivia: false
        );

        // 4. Возвращаем normalized текст
        return formatted.ToFullString();
    }

    /// <summary>
    /// Рекурсивно удаляет все trivia из синтаксического дерева.
    /// </summary>
    private SyntaxNode RemoveAllTrivia(SyntaxNode node)
    {
        // Удалить leading/trailing trivia
        node = node.WithLeadingTrivia().WithTrailingTrivia();

        // Рекурсивно обработать дочерние ноды
        var newChildren = node.ChildNodes()
        .Select(RemoveAllTrivia)
        .ToArray();

        // Заменить дочерние ноды
        if (newChildren.Any())
        {
            node = node.ReplaceNodes(
            node.ChildNodes(),
            (original, _) => newChildren[node.ChildNodes().ToList().IndexOf(original)]
            );
        }

        return node;
    }

    /// <summary>
    /// Нормализовать JSON: canonical formatting.
    /// </summary>
    private string NormalizeJson(string json)
    {
        try
        {
            // Парсим JSON
            using var doc = JsonDocument.Parse(json);

            // Serialize обратно с фиксированными опциями
            var options = new JsonSerializerOptions
            {
                WriteIndented = false, // Compact format
                PropertyNamingPolicy = null, // Сохранить original names
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            };

            var normalized = JsonSerializer.Serialize(doc.RootElement, options);

            // Сортировать ключи для стабильности (опционально)
            // Пока оставим как есть - порядок из оригинала

            return normalized;
        }
        catch
        {
            // Если парсинг не удался - базовая нормализация
            return NormalizeGeneric(json);
        }
    }

    /// <summary>
    /// Нормализовать XML: canonical formatting.
    /// </summary>
    private string NormalizeXml(string xml)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);

            // Удалить все whitespace-only text nodes
            var whitespaceNodes = doc.Descendants()
            .Where(e => e.Nodes().All(n => n is System.Xml.Linq.XText) &&
            string.IsNullOrWhiteSpace(e.Value))
            .ToList();

            foreach (var node in whitespaceNodes)
            {
                node.Remove();
            }

            // Compact format без indentation
            var settings = new System.Xml.XmlWriterSettings
            {
                Indent = false,
                NewLineHandling = System.Xml.NewLineHandling.None,
                OmitXmlDeclaration = true
            };

            using var stringWriter = new StringWriter();
            using var xmlWriter = System.Xml.XmlWriter.Create(stringWriter, settings);

            doc.Save(xmlWriter);

            return stringWriter.ToString();
        }
        catch
        {
            // Если парсинг не удался - базовая нормализация
            return NormalizeGeneric(xml);
        }
    }

    /// <summary>
    /// Базовая нормализация для неизвестных типов файлов.
    /// </summary>
    private string NormalizeGeneric(string content)
    {
        // 1. Normalize line endings
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");

        // 2. Trim каждую строку (убрать trailing whitespace)
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }

        // 3. Убрать пустые строки в начале и конце
        content = string.Join("\n", lines).Trim();

        // 4. Normalize multiple consecutive blank lines → single blank line
        while (content.Contains("\n\n\n"))
        {
            content = content.Replace("\n\n\n", "\n\n");
        }

        return content;
    }

    /// <summary>
    /// Вычислить SHA256 hash строки.
    /// </summary>
    private static string ComputeSha256Hash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Проверить, семантически ли идентичны два куска кода.
    /// </summary>
    public bool AreStructurallyEquivalent(string contentA, string contentB, string? fileName = null)
    {
        var hashA = ComputeStructuralHash(contentA, fileName);
        var hashB = ComputeStructuralHash(contentB, fileName);

        return hashA == hashB;
    }
}
