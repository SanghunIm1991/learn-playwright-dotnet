# 3章 ロケーターの選び方（data-testid属性を使う）

1章で「`data-testid` を目印にすると壊れにくい」という考え方を紹介しました。この章では、それを実際のコードで試し、サンプルフォームの各部品を正しく探し出せることを確かめます。値の入力やボタンのクリックはまだ行いません（4章で扱います）。

## この章の前提知識

- 2章: 1つのテストメソッドの中で Playwright を起動し、ページを開いて `Assert.That` で検証するテストを書けること
- 2章: テストの前に、別ターミナルで `dotnet run --project src/LearnPlaywright.Server` によりサーバーを起動しておくこと
- 1章: 見た目のクラス名や要素の位置ではなく、`data-testid` を目印にする理由

## サンプルフォームに付いている data-testid

サンプルアプリの `src/LearnPlaywright.Server/wwwroot/index.html` では、テストで使う要素に次の `data-testid` が付けられています。

| data-testid | 要素 | 補足 |
|---|---|---|
| `text-input` | テキストボックス（ニックネーム） | |
| `slider` | スライダー（満足度） | 0〜100、初期値50 |
| `select` | プルダウン（好きな果物） | 選択肢の値は `apple` / `banana` / `cherry` |
| `radio-option` | ラジオボタン（好きな色） | **3つとも同じ値**。`value` 属性が `red` / `green` / `blue` |
| `submit-button` | 送信ボタン | |
| `load-button` | 読み込みボタン | |
| `message-area` | メッセージ表示欄 | |

`data-testid` は、アプリを作る側が「テストのために」付けておく必要があります。付いていない要素は、この方法では探せません。

完成版テスト（`tests/LearnPlaywright.Tests/TestData/FormTestData.cs`）では、これらの値を次のような定数クラスにまとめています。本章〜5章では値を文字列のまま直接書き、まとめ方は6章で扱います。

```csharp
// 完成版テストにある定数クラス（抜粋）。目印の値を1か所で管理している
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

## page.Locator と GetByTestId

要素を探すには `page.Locator(セレクター)` を使います。セレクターはCSSと同じ書き方で、`[属性名='値']` と書くと「その属性がその値の要素」を指します。

```csharp
// 「data-testid 属性が text-input の要素」を探すロケーター
var textBox1 = page.Locator("[data-testid='text-input']");

// 上と同じ意味の近道。data-testid で探すための専用メソッド
var textBox2 = page.GetByTestId("text-input");
```

どちらも同じ要素を指します。書き間違いが起きにくいので、以降は基本的に `GetByTestId` を使います。

ポイント: ロケーターを作った時点では、まだ要素を探しに行っていません。ロケーターは「探し方のメモ」で、実際に使ったとき（数を数える、クリックする等）に初めて要素を探します。

## 同じ data-testid が複数あるとき（ラジオボタン）

ラジオボタンは3つとも `data-testid="radio-option"` なので、`GetByTestId("radio-option")` は3つの要素すべてに一致します。特定の1つを指したいときは、`value` 属性も条件に加えます。

```csharp
// data-testid が radio-option で、かつ value が green の要素 → 「緑」のラジオボタンだけを指す
var greenRadio = page.Locator("[data-testid='radio-option'][value='green']");
```

## 練習: 各要素が正しく見つかるか確かめる

学習用プロジェクトに `LocatorTests.cs` を作り、次のテストを書きます。

```csharp
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

[TestFixture]
public class LocatorTests
{
    [Test]
    public async Task EachControl_IsFoundByTestId()
    {
        // 2章と同じく、このテストの中で起動からページ表示までを行う
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // GotoAsync はページの読み込みが終わるまで待ってから次へ進む
        await page.GotoAsync("http://localhost:5080/");

        // CountAsync は「今この瞬間に一致する要素の数」を返す（要素が現れるのを待つ機能はない）。
        // ページの読み込みは上で終わっているので、ここではそのまま数えてよい。
        // 1件だけ見つかる = 目印が正しく、他の要素と取り違えていない、という確認になる
        Assert.That(await page.GetByTestId("text-input").CountAsync(), Is.EqualTo(1));
        Assert.That(await page.GetByTestId("slider").CountAsync(), Is.EqualTo(1));
        Assert.That(await page.GetByTestId("select").CountAsync(), Is.EqualTo(1));
        Assert.That(await page.GetByTestId("submit-button").CountAsync(), Is.EqualTo(1));
        Assert.That(await page.GetByTestId("load-button").CountAsync(), Is.EqualTo(1));

        // ラジオボタンは同じ data-testid が3つあるのが正しい状態
        Assert.That(await page.GetByTestId("radio-option").CountAsync(), Is.EqualTo(3));

        // value で絞り込むと、1件だけになる
        var greenRadio = page.Locator("[data-testid='radio-option'][value='green']");
        Assert.That(await greenRadio.CountAsync(), Is.EqualTo(1));

        // IsVisibleAsync は「今この瞬間に画面に表示されているか」を返す（こちらも待たない）
        Assert.That(await page.GetByTestId("submit-button").IsVisibleAsync(), Is.True);
    }
}
```

サーバーを起動した状態で `dotnet test` を実行し、`失敗: 0` になることを確認してください。

試しに `"text-input"` を `"text-inputX"` のように書き換えて実行すると、数が0になりテストが失敗します。目印を1文字でも間違えると要素が見つからないことを体験しておくと、後で失敗したときの原因調査に役立ちます（確認したら元に戻してください）。

## CountAsync / IsVisibleAsync は「待たない」

1章で触れたとおり、`CountAsync()` と `IsVisibleAsync()` は、その瞬間の状態をすぐ返します。この章のように「ページが表示され終わった後の、変化しない部分」を調べるには問題ありませんが、ボタンを押した後に画面が変わるのを確かめる用途には向きません。変化を待つ書き方は、次の4章で学びます。

## この章のまとめ

- `page.Locator("[data-testid='...']")` と `page.GetByTestId("...")` は同じ要素を指す
- 同じ `data-testid` が複数あるときは `[value='...']` などの条件を足して絞り込む
- `CountAsync()` で「ちょうど1件見つかる」ことを確かめると、目印の正しさを確認できる

次の4章では、見つけた要素に値を入力し、送信ボタンを押してみます。
