# 5章 読み込みと復元の検証

4章では「送信すると保存される」ことを確かめました。この章では、保存した内容を「読み込み」ボタンで取り戻し、画面の各部品に正しく戻ってくる（復元される）ことを確かめます。

## この章の前提知識

- 4章: `FillAsync` / `EvaluateAsync`（スライダー） / `SelectOptionAsync` / `CheckAsync` / `ClickAsync` でフォームを操作できること
- 4章: `Assertions.Expect(...).ToHaveClassAsync(...)` で画面の変化を待って確かめられること
- 3章: `GetByTestId` と、ラジオボタンを `value` で絞り込む書き方
- 2章: 1つのテストメソッドの中で起動〜ページ表示を行う書き方、サーバーの手動起動

## 保存データは「誰のもの」として扱われるか

サンプルアプリは、利用者を **Cookie** で見分けています。Cookie を持たないリクエスト（送信でも読み込みでも）を初めて受けたとき、サーバーがブラウザに識別用の Cookie（`lp_user_id`）を渡し、以後その Cookie を持っているブラウザからの読み込みに対して、同じデータを返します。

- 同じブラウザの中でページを開き直しても（`page.ReloadAsync()`）、Cookie はそのまま残ります。
- 新しく起動したブラウザには Cookie がないので、「保存されたデータはない」と扱われます。

これまでの章のテストは毎回ブラウザを新しく起動しているので、各テストは他のテストが保存したデータの影響を受けません。

## 読み込み完了をどう待つか

読み込みボタンを押したときのアプリの動きは次のとおりです（`app.js` の `handleLoadButtonClick`）。

| サーバーの応答 | 画面の変化 |
|---|---|
| データあり（HTTP 200） | 各部品に値を反映し、**メッセージ欄を空にする** |
| データなし（HTTP 404） | メッセージ欄に「保存されたデータがありません。」（クラス `message--info`）を表示 |

データがあった場合はメッセージ欄が「空」になるので、4章のように「メッセージ欄のクラスが変わるまで待つ」方法が使えません（押す前も押した後も空のことがあるからです）。

そこでサンプルアプリには、テストのための目印がもう1つ用意されています。フォーム要素（`data-testid="form"`）の **`data-request-count` 属性**で、送信・読み込みの処理が終わるたびに 1 ずつ増えます。

```html
<form id="user-form" data-testid="form" data-request-count="0" novalidate>
```

「押す前の値を覚えておき、値が変わるまで待つ」と書けば、処理が終わったことを確実に待てます。

```csharp
var form = page.GetByTestId("form");

// 押す前の処理完了回数を読んでおく（属性がなければ "0" とみなす）
string before = await form.GetAttributeAsync("data-request-count") ?? "0";

await page.GetByTestId("load-button").ClickAsync();

// 属性の値が「押す前の値ではなくなる」まで待つ（Not を付けると「〜でなくなるまで」になる）
await Assertions.Expect(form).Not.ToHaveAttributeAsync("data-request-count", before);
```

## テストコード

学習用プロジェクトに `LoadTests.cs` を作ります。

```csharp
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

[TestFixture]
public class LoadTests
{
    [Test]
    public async Task Load_AfterSubmit_RestoresValues()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        // --- 1. 前提データを自分で用意する（4章と同じ手順で送信する） ---
        // 他のテストが保存したデータを当てにしない（テストの独立性）
        await page.GetByTestId("text-input").FillAsync("ダミー太郎");
        await page.GetByTestId("slider").EvaluateAsync(
            "(el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }",
            "42");
        await page.GetByTestId("select").SelectOptionAsync("banana");
        await page.Locator("[data-testid='radio-option'][value='green']").CheckAsync();
        await page.GetByTestId("submit-button").ClickAsync();
        // 保存が終わったことを確かめてから次へ進む
        await Assertions.Expect(page.GetByTestId("message-area"))
            .ToHaveClassAsync(new Regex("message--success"));

        // --- 2. ページを開き直す ---
        // 画面に残っている入力値ではなく、サーバーから取り戻した値を確かめるため。
        // 同じブラウザなので Cookie は残り、「同じ利用者」として扱われる
        await page.ReloadAsync();

        // --- 3. 読み込みボタンを押し、処理が終わるまで待つ ---
        var form = page.GetByTestId("form");
        string before = await form.GetAttributeAsync("data-request-count") ?? "0";
        await page.GetByTestId("load-button").ClickAsync();
        await Assertions.Expect(form).Not.ToHaveAttributeAsync("data-request-count", before);

        // --- 4. 各部品の現在の値を読み取り、送信した値と一致するか確かめる ---
        // InputValueAsync は入力欄・スライダー・プルダウンの「今の値」を文字列で返す
        Assert.That(await page.GetByTestId("text-input").InputValueAsync(), Is.EqualTo("ダミー太郎"));
        Assert.That(await page.GetByTestId("slider").InputValueAsync(), Is.EqualTo("42"));
        Assert.That(await page.GetByTestId("select").InputValueAsync(), Is.EqualTo("banana"));

        // ラジオボタンは「緑がオンになっているか」を IsCheckedAsync で確かめる
        Assert.That(
            await page.Locator("[data-testid='radio-option'][value='green']").IsCheckedAsync(),
            Is.True);
    }

    [Test]
    public async Task Load_WithoutSubmit_ShowsNoDataMessage()
    {
        // 新しく起動したブラウザには Cookie がない = 何も保存していない利用者
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        // 送信せずにいきなり読み込みボタンを押す
        await page.GetByTestId("load-button").ClickAsync();

        // 「データなし」のときはメッセージが表示されるので、4章と同じ方法で待てる
        var message = page.GetByTestId("message-area");
        await Assertions.Expect(message).ToHaveClassAsync(new Regex("message--info"));
        await Assertions.Expect(message).ToHaveTextAsync("保存されたデータがありません。");
    }
}
```

サーバーを起動した状態で `dotnet test` を実行し、`失敗: 0` になることを確認してください。

## 補足: 保存されたデータの置き場所

手動起動したサーバーは、送信された内容をリポジトリの `src/LearnPlaywright.Server/App_Data/` にJSONファイルとして保存します。このフォルダはGitの管理対象外に設定されています。ここにもダミー値しか入らないよう、フォームには実在の個人情報を入れないでください。

## ここまでのコードを振り返る

4章と5章のテストを並べると、次のことに気付くはずです。

- `"(el, v) => { el.value = v; ... }"` のようなスライダー設定のコードや、`page.GetByTestId("...")` の呼び出しが、何度も繰り返し出てくる
- 「押す前の回数を読む → クリック → 回数が変わるまで待つ」という手順も、読み込みのたびに書く必要がある
- `"text-input"` のような目印の文字列を、あちこちに直接書いている（書き間違えても気付きにくい）

テストが増えるほど、この繰り返しは負担になります。次の6章では、これらを関数にまとめて整理します。

## この章のまとめ

- 同じブラウザ内の `ReloadAsync()` では Cookie が残り、同じ利用者として読み込める
- 成功時にメッセージが空になる処理は、`data-request-count` の変化を `Expect(...).Not.ToHaveAttributeAsync` で待つ
- `InputValueAsync()` と `IsCheckedAsync()` で、復元された値を読み取って確かめる
