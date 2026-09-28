# 6章 ヘルパー関数によるコードの整理

5章の最後で、同じようなコードが何度も出てくることに気付きました。この章では、それらを**ヘルパー関数**（よく使う処理をまとめた関数）に切り出し、テスト本体を短く読みやすくします。

## この章の前提知識

- 4章: 各部品の操作（`FillAsync` / スライダーの `EvaluateAsync` / `SelectOptionAsync` / `CheckAsync` / `ClickAsync`）と、`Assertions.Expect(...).ToHaveClassAsync(...)` による待機
- 5章: `RunAndWaitForResponseAsync` による応答待ち、`ToHaveValueAsync` / `ToBeCheckedAsync` による値の確認、`ReloadAsync()` と Cookie の関係
- 3章: `GetByTestId` と、ラジオボタンの `value` による絞り込み

## 整理の方針

| 5章までの書き方 | この章での整理 |
|---|---|
| 目印の文字列 `"text-input"` を直接書く | 定数クラス `FormTestIds` にまとめる |
| 4つの値をばらばらに扱う | 4つの値をまとめた型 `FormValues` を作る |
| 部品ごとに操作コードを毎回書く | 部品ごとの「値を入れる」関数を作る |
| クリックと応答待ちを毎回書く | 「押して応答を待つ」関数を作る |
| 4つの部品を1つずつ確かめる | まとめて入れる／まとめて確かめる関数を作る |
| メッセージ欄のクラスを毎回調べる | メッセージの種類を確かめる関数を作る |

ヘルパー関数は「操作する関数」と「確かめる関数」に分けます。確かめる関数の中身は 4〜5章と同じ `Assertions.Expect` なので、画面が期待どおりになるまで自動で待ってくれます。

## 手順1: 型と定数を用意する

学習用プロジェクトに `FormPageHelpers.cs` を作り、まず次を書きます。

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

// フォームの4つの値をひとまとめにした型。record は「値が全部同じなら等しい」と比べられる型
public sealed record FormValues(
    string Text,     // テキストボックスの値（空文字も可）
    double Slider,   // スライダーの値（0〜100）
    string Select,   // プルダウンで選んだ option の value（apple / banana / cherry）
    string? Radio    // 選んだラジオボタンの value（red / green / blue）。未選択は null
);

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
}
```

## 手順2: 部品ごとに値を入れる関数

同じファイルに、続けて `FormPageHelpers` クラスを書きます。中身は4章で書いたコードそのものです。

```csharp
public static class FormPageHelpers
{
    // 送信・読み込みのどちらも、この URL のサーバー API と通信する（app.js と同じ値）
    private const string FormDataApiPath = "/api/form-data";

    // テキストボックスに入力する（4章の FillAsync と同じ）
    public static async Task SetTextBoxValueAsync(IPage page, string value)
    {
        ArgumentNullException.ThrowIfNull(page);   // 引数の渡し忘れを早めに見つけるための確認
        ArgumentNullException.ThrowIfNull(value);
        await page.GetByTestId(FormTestIds.TextInput).FillAsync(value);
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

    // プルダウンで選択肢を選ぶ（4章の SelectOptionAsync と同じ。value は option の value 属性の値）
    public static async Task SetSelectValueAsync(IPage page, string value)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(value);
        await page.GetByTestId(FormTestIds.Select).SelectOptionAsync(value);
    }

    // ラジオボタンを選ぶ。null のときは何もしないで終わる（既に選択済みでも解除はしない。何も選ばれていない開いたばかりのページで使う前提）
    public static async Task SetRadioValueAsync(IPage page, string? value)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (value is null) return;
        await RadioOption(page, value).CheckAsync();
    }

    // value でラジオボタンを1つに絞り込んだロケーターを返す（3章の書き方）。
    // 入れる関数と確かめる関数の両方で使うので、1か所にまとめている
    private static ILocator RadioOption(IPage page, string value)
    {
        // 改行などの制御文字はCSSセレクタを壊すため受け付けない
        if (value.Any(char.IsControl))
            throw new ArgumentException("ラジオボタンの値に制御文字は使えません。", nameof(value));
        // 値に ' や \ が含まれてもセレクタが壊れないようエスケープする
        // （セレクタの中では ' で値を囲んでいるので、値の中の ' はそのままだと「値の終わり」と誤解される）
        string escaped = value.Replace("\\", "\\\\").Replace("'", "\\'");
        // $"..." は、{ } の中に書いた値を文字列に埋め込む書き方
        return page.Locator($"[data-testid='{FormTestIds.RadioOption}'][value='{escaped}']");
    }
```

## 手順3: ボタン・まとめ操作・確認の関数

`FormPageHelpers` クラスの続きです。

```csharp
    // 送信ボタンを押し、サーバーの応答が返るまで待つ
    public static async Task ClickSubmitButtonAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await ClickAndWaitForResponseAsync(page, FormTestIds.SubmitButton, "POST");
    }

    // 読み込みボタンを押し、サーバーの応答が返るまで待つ
    public static async Task ClickLoadButtonAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await ClickAndWaitForResponseAsync(page, FormTestIds.LoadButton, "GET");
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

    // 4つの部品が期待どおりの値になっていることを確かめる。
    // 5章と同じく Assertions.Expect を使うので、画面に反映されるまで自動で待つ
    public static async Task ExpectFormValuesAsync(IPage page, FormValues expected)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(expected);
        await Assertions.Expect(page.GetByTestId(FormTestIds.TextInput)).ToHaveValueAsync(expected.Text);
        // 画面のスライダーの値は "42" のような文字列なので、数値を同じ形の文字列にして比べる
        await Assertions.Expect(page.GetByTestId(FormTestIds.Slider))
            .ToHaveValueAsync(expected.Slider.ToString(CultureInfo.InvariantCulture));
        await Assertions.Expect(page.GetByTestId(FormTestIds.Select)).ToHaveValueAsync(expected.Select);

        if (expected.Radio is null)
        {
            // 「未選択」を期待する場合: オンになっているラジオボタン（:checked）が0個であること。
            // ToHaveCountAsync は「要素の数が指定の数になるまで」待って確かめるアサーション
            await Assertions.Expect(page.Locator($"[data-testid='{FormTestIds.RadioOption}']:checked"))
                .ToHaveCountAsync(0);
        }
        else
        {
            await Assertions.Expect(RadioOption(page, expected.Radio)).ToBeCheckedAsync();
        }
    }

    // メッセージ欄の種類（"success" / "error" / "info"）が期待どおりであることを確かめる。
    // 種類は class 名 "message--success" などで表されるので、4章と同じ ToHaveClassAsync で確かめる
    public static async Task ExpectMessageTypeAsync(IPage page, string expectedType)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(expectedType);
        // \b は「単語の区切り」。"message--info" が "message--information" のような別名に一致しないようにする。
        // $@"..." は「{ } で値を埋め込める（$）」＋「\ をそのまま書ける（@）」文字列。
        // Regex.Escape は、値に正規表現で特別な意味を持つ記号が含まれていても文字どおりに扱わせるための変換
        await Assertions.Expect(page.GetByTestId(FormTestIds.MessageArea))
            .ToHaveClassAsync(new Regex($@"\bmessage--{Regex.Escape(expectedType)}\b"));
    }

    // 5章の「クリックして応答を待つ」を1か所にまとめたもの。
    // 送信（POST）と読み込み（GET）は同じ URL なので、HTTP メソッドも条件に入れて区別する
    private static async Task ClickAndWaitForResponseAsync(IPage page, string buttonTestId, string method)
    {
        await page.RunAndWaitForResponseAsync(
            async () => await page.GetByTestId(buttonTestId).ClickAsync(),
            response => response.Url.EndsWith(FormDataApiPath) && response.Request.Method == method);
    }
}
```

### なぜボタンの関数でも応答を待つのか

確かめる関数（`ExpectFormValuesAsync` / `ExpectMessageTypeAsync`）は自分で待つので、「ボタンを押す関数」は押すだけでもよさそうに見えます。それでも応答を待つようにしているのは、**ボタンを押した直後に次の操作をしても、通信の途中で割り込まないようにするため**です。

たとえば「送信ボタンを押す → すぐにページを開き直す（`ReloadAsync`）」と書いた場合、応答を待たないと、保存が終わる前にページが開き直されて送信が中断されるおそれがあります。ボタンの関数が応答まで待ってから戻るようにしておけば、呼ぶ側はこうした心配をせずに次の手順を書けます（9章でこの書き方を使います）。

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
        await ClickSubmitButtonAsync(page);                 // クリック＋応答待ち
        await ExpectMessageTypeAsync(page, "success");      // 「成功」の表示になるまで待って確かめる
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
        await ClickLoadButtonAsync(page);          // クリック＋応答待ち

        // 送信した値がそのまま戻っていることを、4つまとめて確かめる
        await ExpectFormValuesAsync(page, input);
    }
}
```

サーバーを起動した状態で `dotnet test` を実行し、`失敗: 0` を確認してください。

## 確認してみよう

`Load_AfterSubmit_RestoresValues` の最後の行を `await ExpectFormValuesAsync(page, new FormValues("ダミー太郎", 42, "cherry", "green"));` に変えて実行すると、5秒ほど待ったあとに失敗し、プルダウンの値が期待（`cherry`）と違うことが表示されます。確かめる関数が、期待どおりになるまで待ってから失敗を報告していることが分かります。確認後は元に戻してください。

## まだ残っている課題

テスト本体は短くなりましたが、`"ダミー太郎", 42, "banana", "green"` のような**入力値がテストメソッドの中に直接書かれて**います。「空文字の場合」「1001文字の場合」…と試したいパターンが増えるたびに、ほぼ同じテストメソッドを複製することになります。次の7章では、1章で紹介した「パラメータ化」でこれを解決します。

## この章のまとめ

- 目印は `FormTestIds`、4つの値は `FormValues` にまとめた
- 操作する関数（`SetFormValuesAsync` / `ClickSubmitButtonAsync` / `ClickLoadButtonAsync`）と、確かめる関数（`ExpectFormValuesAsync` / `ExpectMessageTypeAsync`）に分けた
- 確かめる関数は `Assertions.Expect` を使うので、画面に反映されるまで自動で待つ
- ボタンを押す関数は応答が返るまで待ってから戻るので、直後に次の操作を書いても通信の途中で割り込まない

## この章で出てきた C# の書き方

| 書き方 | 意味 |
|---|---|
| `public sealed record FormValues(string Text, ...)` | `record` は値をひとまとめにする型で、全部の値が同じなら「等しい」と比べられる。`sealed` は「この型を元に別の型を作らせない」指定 |
| `string?` | `?` を付けると「値がない（`null`）こともある」型になる。付けない `string` は null を入れない前提 |
| `double` | 小数も扱える数値の型 |
| `public static class FormPageHelpers` / `public static async Task ...` | `static` の関数は、クラスを `new` せずに `FormPageHelpers.SetTextBoxValueAsync(...)` のように呼べる |
| `private const string ...` / `private static ...` | `private` は「このクラスの中だけで使う」という意味（外からは呼べない） |
| `private static readonly string[] ...` | `readonly` は最初に入れた後に入れ替えない値。`string[]` は文字列の配列 |
| `ArgumentNullException.ThrowIfNull(page)` | 引数が `null` なら、その場でエラー（例外）にする |
| `throw new ArgumentException("...", nameof(value))` | エラー（例外）を起こして処理を止める。`nameof(value)` は変数名 `"value"` を文字列として得る書き方 |
| `if (!double.IsFinite(value))` | `!` は「〜でない」。「有限の数でなければ」という条件 |
| `if (value is null) return;` | `is null` は「null であるか」。`return;` でその場で関数を終える |
| `value.Any(char.IsControl)` | 文字列の中に、条件（ここでは制御文字か）に当てはまる文字が1つでもあるか |
| `value.Replace("a", "b")` | 文字列の中の `a` を `b` に置き換えた新しい文字列を返す |
| `"\\"` | 文字列の中で `\` を1文字書くには `\\` と書く（`\` は特別な意味を持つため） |
| `$"...{値}..."` | `{ }` の中の値を文字列に埋め込む |
| `$@"..."` | `$`（値の埋め込み）と `@`（`\` を特別扱いしない）を組み合わせた文字列 |
| `ILocator RadioOption(...)` | 戻り値の型を書いた関数。`ILocator`（ロケーター）を返す |
| `if (...) { ... } else { ... }` | 条件が成り立つときと成り立たないときで処理を分ける |
| `using static MyFirstPlaywrightTests.FormPageHelpers;` | クラス名を省いて `SetFormValuesAsync(...)` のように呼べるようにする宣言 |
| `new FormValues("ダミー太郎", 42, "banana", "green")` | `record` の値を作る。引数は宣言した順（Text, Slider, Select, Radio） |
