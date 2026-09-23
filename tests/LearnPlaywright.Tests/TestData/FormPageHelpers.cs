using System.Globalization;
using Microsoft.Playwright;

namespace LearnPlaywright.Tests.TestData;

/// <summary>
/// COMP-04 Playwright操作ヘルパー（FUNC-30〜42）。
/// 要素はすべて data-testid 属性（page.GetByTestId）で特定する。
/// </summary>
public static class FormPageHelpers
{
    private const string RequestCountAttribute = "data-request-count";
    private static readonly string[] MessageTypes = ["success", "error", "info"];

    // FUNC-30
    public static async Task SetTextBoxValueAsync(IPage page, string value)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(value);
        await page.GetByTestId(FormTestIds.TextInput).FillAsync(value);
    }

    // FUNC-31
    public static async Task<string> GetTextBoxValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return await page.GetByTestId(FormTestIds.TextInput).InputValueAsync();
    }

    // FUNC-32: range要素はEvaluateAsyncで値を設定し、input/changeイベントを発火させる方式に確定
    public static async Task SetSliderValueAsync(IPage page, double value)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!double.IsFinite(value)) throw new ArgumentException("value must be a finite number", nameof(value));
        await page.GetByTestId(FormTestIds.Slider).EvaluateAsync(
            "(el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }",
            value.ToString(CultureInfo.InvariantCulture));
    }

    // FUNC-33
    public static async Task<double> GetSliderValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        string raw = await page.GetByTestId(FormTestIds.Slider).InputValueAsync();
        return double.Parse(raw, CultureInfo.InvariantCulture);
    }

    // FUNC-34
    public static async Task SetSelectValueAsync(IPage page, string value)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(value);
        await page.GetByTestId(FormTestIds.Select).SelectOptionAsync(value);
    }

    // FUNC-35
    public static async Task<string> GetSelectValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return await page.GetByTestId(FormTestIds.Select).InputValueAsync();
    }

    // FUNC-36: nullの場合は何もしない（既存の選択状態を保持）
    public static async Task SetRadioValueAsync(IPage page, string? value)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (value is null) return;
        // 値に ' や \ が含まれてもセレクタが壊れないようエスケープする
        string escaped = value.Replace("\\", "\\\\").Replace("'", "\\'");
        await page.Locator($"[data-testid='{FormTestIds.RadioOption}'][value='{escaped}']").CheckAsync();
    }

    // FUNC-37
    public static async Task<string?> GetRadioValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var all = page.GetByTestId(FormTestIds.RadioOption);
        if (await all.CountAsync() == 0) throw new InvalidOperationException("No radio buttons found.");

        var checkedRadios = page.Locator($"[data-testid='{FormTestIds.RadioOption}']:checked");
        int checkedCount = await checkedRadios.CountAsync();
        if (checkedCount == 0) return null;
        if (checkedCount > 1) throw new InvalidOperationException("Multiple radio buttons are checked.");
        return await checkedRadios.GetAttributeAsync("value");
    }

    // FUNC-38: クリック後、COMP-01の処理完了（data-request-countの増加）まで待機する
    public static async Task ClickSubmitButtonAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await ClickAndWaitForCompletionAsync(page, FormTestIds.SubmitButton);
    }

    // FUNC-39: 同上（読み込みボタン）
    public static async Task ClickLoadButtonAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await ClickAndWaitForCompletionAsync(page, FormTestIds.LoadButton);
    }

    // FUNC-40
    public static async Task SetFormValuesAsync(IPage page, FormValues values)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(values);
        await SetTextBoxValueAsync(page, values.Text);
        await SetSliderValueAsync(page, values.Slider);
        await SetSelectValueAsync(page, values.Select);
        await SetRadioValueAsync(page, values.Radio);
    }

    // FUNC-41
    public static async Task<FormValues> GetFormValuesAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new FormValues(
            await GetTextBoxValueAsync(page),
            await GetSliderValueAsync(page),
            await GetSelectValueAsync(page),
            await GetRadioValueAsync(page));
    }

    // FUNC-42
    public static async Task<MessageState> GetMessageAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var area = page.GetByTestId(FormTestIds.MessageArea);
        string text = await area.TextContentAsync() ?? string.Empty;
        string[] classes = (await area.GetAttributeAsync("class") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var types = MessageTypes.Where(t => classes.Contains("message--" + t)).ToList();
        if (types.Count > 1) throw new InvalidOperationException("Multiple message type classes are set.");
        return new MessageState(text, types.Count == 1 ? types[0] : null);
    }

    // FUNC-38/39の共通処理: クリック前の完了回数を記録し、回数が増えるまで待つ（固定時間のsleepは使わない）
    private static async Task ClickAndWaitForCompletionAsync(IPage page, string buttonTestId)
    {
        var form = page.GetByTestId(FormTestIds.Form);
        string before = await form.GetAttributeAsync(RequestCountAttribute) ?? "0";
        await page.GetByTestId(buttonTestId).ClickAsync();
        await Assertions.Expect(form).Not.ToHaveAttributeAsync(RequestCountAttribute, before);
    }
}
