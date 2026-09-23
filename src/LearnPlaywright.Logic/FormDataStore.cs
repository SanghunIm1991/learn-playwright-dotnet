using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LearnPlaywright.Logic;

/// <summary>
/// COMP-03 データ永続化ロジック（FUNC-19〜26）。
/// .NET標準ライブラリのみに依存し、HTTP型（HttpContext等）は一切扱わない（NFR-06）。
/// 排他制御は呼び出し元（COMP-02）が保証する前提とする（関数設計書2.2節）。
/// </summary>
public static class FormDataStore
{
    public const int TextMaxLength = 1000;
    public const int SelectMaxLength = 200;
    public const int RadioMaxLength = 200;

    // 保存・復元に共通のJSON設定。camelCaseのプロパティ名で入出力し、
    // 数値を文字列から読み取る緩和（JsonSerializerDefaults.Web の既定）は行わない。
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // FUNC-19
    public static string MapUserIdToFilePath(string userId, string baseDirectory)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
        if (string.IsNullOrEmpty(baseDirectory)) throw new ArgumentException("baseDirectory is required", nameof(baseDirectory));

        // userIdをそのままパスに使わず、SHA-256の16進数64文字をファイル名とする（パストラバーサル対策）。
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        string fileName = Convert.ToHexStringLower(hash) + ".json";

        string fullBase = Path.GetFullPath(baseDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(fullBase, fileName));

        // 防御的チェック: 結果がbaseDirectory配下であること（ハッシュ命名では原理的に到達しない）。
        string baseWithSeparator = Path.EndsInDirectorySeparator(fullBase) ? fullBase : fullBase + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved path is outside of the base directory.");
        }
        return fullPath;
    }

    // FUNC-20
    public static ValidationResult ValidateFormData(FormDataDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var errors = new List<string>();

        if (dto.Text is null) errors.Add("text is required");
        else if (dto.Text.Length > TextMaxLength) errors.Add($"text exceeds maximum length of {TextMaxLength} characters");

        if (!double.IsFinite(dto.Slider)) errors.Add("slider must be a finite number");

        if (string.IsNullOrEmpty(dto.Select)) errors.Add("select is required");
        if (dto.Select is not null && dto.Select.Length > SelectMaxLength) errors.Add($"select exceeds maximum length of {SelectMaxLength} characters");

        if (dto.Radio is not null && dto.Radio.Length > RadioMaxLength) errors.Add($"radio exceeds maximum length of {RadioMaxLength} characters");

        return new ValidationResult(errors.Count == 0, errors);
    }

    // FUNC-21
    public static string SerializeFormDataForStorage(FormDataDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    // FUNC-22
    public static FormDataDto DeserializeFormDataFromStorage(string jsonContent)
    {
        ArgumentNullException.ThrowIfNull(jsonContent);
        // 構文異常・型不一致・必須プロパティ欠落はJsonExceptionとして伝播する。未知のプロパティは無視される。
        return JsonSerializer.Deserialize<FormDataDto>(jsonContent, JsonOptions)
            ?? throw new JsonException("JSON content is null.");
    }

    // FUNC-23
    public static void WriteFormDataFile(string filePath, string jsonContent)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(jsonContent);
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, jsonContent, new UTF8Encoding(false));
    }

    // FUNC-24
    public static FileReadResult ReadFormDataFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        try
        {
            return new FileReadResult(true, File.ReadAllText(filePath, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // 未存在は例外ではなく戻り値で表現する（REQ-04の「未存在」判定の基礎）。
            return new FileReadResult(false, null);
        }
    }

    // FUNC-25
    public static SaveResult SaveFormData(string userId, FormDataDto dto, string baseDirectory)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrEmpty(baseDirectory)) throw new ArgumentException("baseDirectory is required", nameof(baseDirectory));

        if (!ValidateFormData(dto).Valid) return new SaveResult(false);

        string filePath = MapUserIdToFilePath(userId, baseDirectory);
        string json = SerializeFormDataForStorage(dto);
        WriteFormDataFile(filePath, json); // I/O異常はcatchせず伝播させる（COMP-02契約）
        return new SaveResult(true);
    }

    // FUNC-26
    public static LoadResult LoadFormData(string userId, string baseDirectory)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
        if (string.IsNullOrEmpty(baseDirectory)) throw new ArgumentException("baseDirectory is required", nameof(baseDirectory));

        string filePath = MapUserIdToFilePath(userId, baseDirectory);
        FileReadResult read = ReadFormDataFile(filePath);
        if (!read.Exists) return new LoadResult(false, null);

        return new LoadResult(true, DeserializeFormDataFromStorage(read.Content!));
    }
}
