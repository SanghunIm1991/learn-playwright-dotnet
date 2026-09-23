using System.Text.Json.Serialization;

namespace LearnPlaywright.Logic;

/// <summary>
/// フォーム入力値（COMP-03関数設計書2.3節 FormDataDto）。
/// COMP-01のFormValues（JS）・COMP-04のFormValues（C#テスト側）と同一のフィールド構成を持つ。
/// </summary>
public sealed class FormDataDto
{
    // text/slider/selectはJSON上の必須プロパティとする（欠落時はデシリアライズ例外 → HTTP 400）。
    // 値がnullであること自体は許容し、意味的な検証はFUNC-20（ValidateFormData）に委ねる。
    [JsonRequired]
    public string? Text { get; init; }

    [JsonRequired]
    public double Slider { get; init; }

    [JsonRequired]
    public string? Select { get; init; }

    // 未選択状態はnull。プロパティ欠落時もnullとして扱う。
    public string? Radio { get; init; }
}

/// <summary>保存処理の結果。success:falseは入力値検証エラー専用（I/O異常は例外で表現する）。</summary>
public sealed record SaveResult(bool Success);

/// <summary>読込処理の結果。Found:falseは対応ファイル未存在を表す。</summary>
public sealed record LoadResult(bool Found, FormDataDto? Data);

/// <summary>FUNC-20の検証結果。違反したルールごとに1件のメッセージを持つ。</summary>
public sealed record ValidationResult(bool Valid, IReadOnlyList<string> Errors);

/// <summary>FUNC-24の読み取り結果。</summary>
public sealed record FileReadResult(bool Exists, string? Content);
