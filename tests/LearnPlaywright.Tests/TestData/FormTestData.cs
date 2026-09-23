namespace LearnPlaywright.Tests.TestData;

/// <summary>フォーム入力値（COMP-01 FormValues / COMP-03 FormDataDto と同一構成）。</summary>
public sealed record FormValues(
    string Text,     // null不可。空文字列は許容
    double Slider,
    string Select,   // option要素のvalue属性
    string? Radio    // 未選択はnull
);

/// <summary>メッセージ表示領域の表示状態。Typeは 'success' | 'error' | 'info' | null。</summary>
public sealed record MessageState(string Text, string? Type);

/// <summary>1件のテストケース（入力値と期待値の組）。</summary>
public sealed record FormValuesTestCase(
    string CaseId,
    string Category,
    FormValues Input,
    bool ExpectedSubmitSuccess,
    FormValues? ExpectedRestored)   // 保存拒否を期待する場合はnull（読み込み時は「未保存」表示を期待）
{
    public override string ToString() => CaseId;

    // FUNC-27: 送信成功・入力値どおりの復元を期待するケース
    public static FormValuesTestCase RoundTrip(string caseId, string category, FormValues input)
    {
        if (string.IsNullOrEmpty(caseId)) throw new ArgumentException("caseId is required", nameof(caseId));
        ArgumentNullException.ThrowIfNull(input);
        return new FormValuesTestCase(caseId, category, input, true, input);
    }

    // FUNC-28: サーバー側検証により保存が拒否されることを期待するケース
    public static FormValuesTestCase ExpectedRejection(string caseId, string category, FormValues invalidInput)
    {
        if (string.IsNullOrEmpty(caseId)) throw new ArgumentException("caseId is required", nameof(caseId));
        ArgumentNullException.ThrowIfNull(invalidInput);
        return new FormValuesTestCase(caseId, category, invalidInput, false, null);
    }
}

/// <summary>COMP-03の検証ルールおよびCOMP-01のHTML実装に合わせた境界値定数。</summary>
public static class FormFieldLimits
{
    public const int TextMaxLength = 1000;
    public const int SelectMaxLength = 200;
    public const int RadioMaxLength = 200;

    // COMP-01（wwwroot/index.html）のスライダー min/max 属性の実装値（値は確定済み。名称は関数設計書との対応のため維持）
    public const double SliderMinPlaceholder = 0;
    public const double SliderMaxPlaceholder = 100;
}

/// <summary>COMP-01（wwwroot/index.html）のプルダウン・ラジオボタンの選択肢値（実装工程で確定）。</summary>
public static class FormOptionValues
{
    public static readonly IReadOnlyList<string> SelectOptions = ["apple", "banana", "cherry"];
    public static readonly IReadOnlyList<string> RadioOptions = ["red", "green", "blue"];
}

/// <summary>Playwrightが要素を特定するための data-testid 属性値。</summary>
public static class FormTestIds
{
    public const string TextInput = "text-input";
    public const string Slider = "slider";
    public const string Select = "select";
    public const string RadioOption = "radio-option";
    public const string SubmitButton = "submit-button";
    public const string LoadButton = "load-button";
    public const string MessageArea = "message-area";

    // 実装工程で追加: 送信・読み込み処理の完了回数（data-request-count属性）を持つform要素
    public const string Form = "form";
}

/// <summary>[TestCaseSource]から参照されるテストケース列挙（FUNC-29）。</summary>
public static class FormValuesTestCases
{
    // ダミー値のみを用いる（実在の個人情報は含めない。CON-06/07）
    private static readonly FormValues Base = new("ダミー太郎", 42, "banana", "green");

    public static IEnumerable<TestCaseData> GetCases()
    {
        var cases = new List<FormValuesTestCase>
        {
            FormValuesTestCase.RoundTrip("REP-DEFAULT", "代表値", Base),
            FormValuesTestCase.RoundTrip("BND-TEXT-EMPTY", "境界値-文字列長", Base with { Text = "" }),
            FormValuesTestCase.RoundTrip("BND-TEXT-MAXLEN", "境界値-文字列長", Base with { Text = new string('a', FormFieldLimits.TextMaxLength) }),
            FormValuesTestCase.ExpectedRejection("BND-TEXT-OVERLEN", "検証エラー", Base with { Text = new string('a', FormFieldLimits.TextMaxLength + 1) }),
            FormValuesTestCase.RoundTrip("BND-SLIDER-MIN", "境界値-数値", Base with { Slider = FormFieldLimits.SliderMinPlaceholder }),
            FormValuesTestCase.RoundTrip("BND-SLIDER-MAX", "境界値-数値", Base with { Slider = FormFieldLimits.SliderMaxPlaceholder }),
            FormValuesTestCase.RoundTrip("BND-SELECT-FIRST", "境界値-選択肢", Base with { Select = FormOptionValues.SelectOptions[0] }),
            FormValuesTestCase.RoundTrip("BND-SELECT-LAST", "境界値-選択肢", Base with { Select = FormOptionValues.SelectOptions[^1] }),
            FormValuesTestCase.RoundTrip("BND-RADIO-UNSELECTED", "境界値-未選択", Base with { Radio = null }),
        };
        for (int i = 0; i < FormOptionValues.RadioOptions.Count; i++)
        {
            cases.Add(FormValuesTestCase.RoundTrip(
                $"BND-RADIO-EACHOPTION-{i + 1}", "境界値-選択肢", Base with { Radio = FormOptionValues.RadioOptions[i] }));
        }

        return cases.Select(testCase => new TestCaseData(testCase).SetName(testCase.CaseId));
    }
}
