# 6章 ヘルパー関数によるコードの整理

5章の最後で、同じようなコードが何度も出てくることに気付きました。この章では、それらを**ヘルパー関数**（よく使う処理をまとめた関数）に切り出し、テスト本体を短く読みやすくします。ここで作る関数は、完成版テストの `tests/LearnPlaywright.Tests/TestData/FormPageHelpers.cs` と同じものです。

## この章の前提知識

- 4章: 各部品の操作（`FillAsync` / スライダーの `EvaluateAsync` / `SelectOptionAsync` / `CheckAsync` / `ClickAsync`）と、`Assertions.Expect` による待機
- 5章: `data-request-count` の変化を待つ書き方、`InputValueAsync()` による値の読み取り、`ReloadAsync()` と Cookie の関係
- 3章: `GetByTestId` と、ラジオボタンの `value` による絞り込み

## 整理の方針

| 5章までの書き方 | この章での整理 |
|---|---|
| 目印の文字列 `"text-input"` を直接書く | 定数クラス `FormTestIds` にまとめる |
| 4つの値をばらばらに扱う | 4つの値をまとめた型 `FormValues` を作る |
| 部品ごとに操作コードを毎回書く | 部品ごとの「入れる／読む」関数を作る |
| 4つの部品を1つずつ操作する | まとめて入れる／読む関数を作る |
| メッセージ欄のクラスを毎回調べる | 表示状態を返す関数と型 `MessageState` を作る |

## 手順1: 型と定数を用意する

学習用プロジェクトに `FormPageHelpers.cs` を作り、まず次を書きます。

```csharp
using System.Globalization;
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

// フォームの4つの値をひとまとめにした型。record は「値が全部同じなら等しい」と比べられる型
public sealed record FormValues(
    string Text,     // テキストボックスの値（空文字も可）
    double Slider,   // スライダーの値（0〜100）
    string Select,   // プルダウンで選んだ option の value（apple / banana / cherry）
    string? Radio    // 選んだラジオボタンの value（red / green / blue）。未選択は null
);

// メッセージ欄の表示状態。Type は "success" / "error" / "info"、何も表示していなければ null
public sealed record MessageState(string Text, string? Type);

// 目印（data-testid）の値を1か所で管理する。書き間違いはコンパイル時に気付ける
public static class FormTestIds
{
    public const string TextInput = "text-input";
    public const string Slider = "slider";
    public const string Select = "select";
    public const string RadioOption = "radio-option";
    public const string SubmitButton = "submit-button";
    public const string LoadButton = "load-button";
    public const string MessageArea = "message-area";
    public const string Form = "form"; // data-request-count を持つ form 要素
}
```

## 手順2: 部品ごとの「入れる／読む」関数

同じファイルに、続けて `FormPageHelpers` クラスを書きます。中身は4〜5章で書いたコードそのものです。

```csharp
public static class FormPageHelpers
{
    private const string RequestCountAttribute = "data-request-count";
    private static readonly string[] MessageTypes = ["success", "error", "info"];

    // テキストボックスに入力する（4章の FillAsync と同じ）
    public static async Task SetTextBoxValueAsync(IPage page, string value)
    {
        ArgumentNullException.ThrowIfNull(page);   // 引数の渡し忘れを早めに見つけるための確認
        ArgumentNullException.ThrowIfNull(value);
        await page.GetByTestId(FormTestIds.TextInput).FillAsync(value);
    }

    // テキストボックスの今の値を読む（5章の InputValueAsync と同じ）
    public static async Task<string> GetTextBoxValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return await page.GetByTestId(FormTestIds.TextInput).InputValueAsync();
    }

    // スライダーに値を設定する（4章の EvaluateAsync 方式）
    public static async Task SetSliderValueAsync(IPage page, double value)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!double.IsFinite(value)) throw new ArgumentException("value must be a finite number", nameof(value));
        await page.GetByTestId(FormTestIds.Slider).EvaluateAsync(
            "(el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }",
            // InvariantCulture: PCの地域設定に関係なく「42.5」のように小数点を . で書くため
            value.ToString(CultureInfo.InvariantCulture));
    }

    // スライダーの今の値を読み、数値に変換して返す
    public static async Task<double> GetSliderValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        string raw = await page.GetByTestId(FormTestIds.Slider).InputValueAsync();
        return double.Parse(raw, CultureInfo.InvariantCulture);
    }

    public static async Task SetSelectValueAsync(IPage page, string value)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(value);
        await page.GetByTestId(FormTestIds.Select).SelectOptionAsync(value);
    }

    public static async Task<string> GetSelectValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return await page.GetByTestId(FormTestIds.Select).InputValueAsync();
    }

    // ラジオボタンを選ぶ。null のときは何もしないで終わる（既に選択済みでも解除はしない。何も選ばれていない開いたばかりのページで使う前提）
    // ※アプリ側（app.js）は、radio が null の保存データを「読み込む」と全ラジオの選択を解除する
    public static async Task SetRadioValueAsync(IPage page, string? value)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (value is null) return;
        // 値に ' や \ が含まれてもセレクタが壊れないようエスケープする
        // （セレクタの中では ' で値を囲んでいるので、値の中の ' はそのままだと「値の終わり」と誤解される）
        string escaped = value.Replace("\\", "\\\\").Replace("'", "\\'");
        await page.Locator($"[data-testid='{FormTestIds.RadioOption}'][value='{escaped}']").CheckAsync();
    }

    // 選ばれているラジオボタンの value を返す。1つも選ばれていなければ null
    public static async Task<string?> GetRadioValueAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var all = page.GetByTestId(FormTestIds.RadioOption);
        if (await all.CountAsync() == 0) throw new InvalidOperationException("No radio buttons found.");

        // :checked は「オンになっているもの」だけに絞り込むCSSの書き方
        var checkedRadios = page.Locator($"[data-testid='{FormTestIds.RadioOption}']:checked");
        int checkedCount = await checkedRadios.CountAsync();
        if (checkedCount == 0) return null;
        if (checkedCount > 1) throw new InvalidOperationException("Multiple radio buttons are checked.");
        return await checkedRadios.GetAttributeAsync("value");
    }
```

## 手順3: ボタン・まとめ操作・メッセージの関数

`FormPageHelpers` クラスの続きです。

```csharp
    // 送信ボタンを押し、処理が終わるまで待つ
    public static async Task ClickSubmitButtonAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await ClickAndWaitForCompletionAsync(page, FormTestIds.SubmitButton);
    }

    // 読み込みボタンを押し、処理が終わるまで待つ
    public static async Task ClickLoadButtonAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await ClickAndWaitForCompletionAsync(page, FormTestIds.LoadButton);
    }

    // 4つの部品にまとめて値を入れる
    public static async Task SetFormValuesAsync(IPage page, FormValues values)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(values);
        await SetTextBoxValueAsync(page, values.Text);
        await SetSliderValueAsync(page, values.Slider);
        await SetSelectValueAsync(page, values.Select);
        await SetRadioValueAsync(page, values.Radio);
    }

    // 4つの部品の今の値をまとめて読み、FormValues にして返す
    public static async Task<FormValues> GetFormValuesAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new FormValues(
            await GetTextBoxValueAsync(page),
            await GetSliderValueAsync(page),
            await GetSelectValueAsync(page),
            await GetRadioValueAsync(page));
    }

    // メッセージ欄の文言と種別（クラス名 message--xxx の xxx 部分）を読み取る
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

    // 5章の「押す前の回数を覚える → クリック → 回数が変わるまで待つ」を1か所にまとめたもの。
    // 送信・読み込みのどちらもこれを使うので、クリック後は必ず処理完了まで待ってから戻る
    private static async Task ClickAndWaitForCompletionAsync(IPage page, string buttonTestId)
    {
        var form = page.GetByTestId(FormTestIds.Form);
        string before = await form.GetAttributeAsync(RequestCountAttribute) ?? "0";
        await page.GetByTestId(buttonTestId).ClickAsync();
        await Assertions.Expect(form).Not.ToHaveAttributeAsync(RequestCountAttribute, before);
    }
}
```

`GetMessageAsync` は待たずに今の状態を読みますが、呼ぶ前に `ClickSubmitButtonAsync` が処理完了まで待っているので、読んだ時点で結果は画面に出ています。

## 手順4: 4〜5章のテストを書き直す

新しく `HelperTests.cs` を作ります。4章・5章の正常系テストが、次のように短くなります。

```csharp
using Microsoft.Playwright;
// using static を使うと、FormPageHelpers.SetFormValuesAsync(...) を SetFormValuesAsync(...) と短く書ける
using static MyFirstPlaywrightTests.FormPageHelpers;

namespace MyFirstPlaywrightTests;

[TestFixture]
public class HelperTests
{
    [Test]
    public async Task Submit_ValidValues_ShowsSuccessMessage()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        // 4つの部品への入力が1行で済む（値はダミー）
        await SetFormValuesAsync(page, new FormValues("ダミー太郎", 42, "banana", "green"));
        await ClickSubmitButtonAsync(page);        // クリック＋処理完了待ち

        MessageState message = await GetMessageAsync(page);
        Assert.That(message.Type, Is.EqualTo("success"));
    }

    [Test]
    public async Task Load_AfterSubmit_RestoresValues()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        var input = new FormValues("ダミー太郎", 42, "banana", "green");
        await SetFormValuesAsync(page, input);
        await ClickSubmitButtonAsync(page);
        await page.ReloadAsync();                  // 同じブラウザなので Cookie は残る
        await ClickLoadButtonAsync(page);          // クリック＋処理完了待ち

        FormValues restored = await GetFormValuesAsync(page);
        // record 同士は「4つの値がすべて等しいか」で比べられる
        Assert.That(restored, Is.EqualTo(input));
    }
}
```

サーバーを起動した状態で `dotnet test` を実行し、`失敗: 0` を確認してください。

## まだ残っている課題

テスト本体は短くなりましたが、`"ダミー太郎", 42, "banana", "green"` のような**入力値がテストメソッドの中に直接書かれて**います。「空文字の場合」「1001文字の場合」…と試したいパターンが増えるたびに、ほぼ同じテストメソッドを複製することになります。次の7章では、1章で紹介した「パラメータ化」でこれを解決します。

## この章のまとめ

- 目印は `FormTestIds`、4つの値は `FormValues`、メッセージは `MessageState` にまとめた
- `SetFormValuesAsync` / `ClickSubmitButtonAsync` / `ClickLoadButtonAsync` / `GetFormValuesAsync` / `GetMessageAsync` で、テスト本体が短くなった
- クリック後の処理完了待ちはヘルパーの中に1か所だけ書けばよくなった
