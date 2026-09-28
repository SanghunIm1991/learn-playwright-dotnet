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

データがあった場合はメッセージ欄が「空」になるので、4章のように「メッセージ欄のクラスが変わるまで待つ」方法は使えません（押す前も押した後も空のことがあるからです）。代わりに、次の2つの道具を組み合わせます。どちらも Playwright でよく使われる、標準的な書き方です。

### 道具1: 「期待する値になるまで」待つアサーション

4章の `ToHaveClassAsync` と同じ仲間に、入力欄の値やラジオボタンの状態を確かめるものがあります。

| 確かめたいこと | 使うアサーション |
|---|---|
| 入力欄・スライダー・プルダウンの値 | `Assertions.Expect(要素).ToHaveValueAsync("期待する値")` |
| ラジオボタンがオンになっているか | `Assertions.Expect(要素).ToBeCheckedAsync()` |

どちらも**期待する値になるまで繰り返し確認する**ので、読み込み結果が画面に反映されるのを自然に待てます。「読み込みが終わるのを待つ」と「値を確かめる」が1行で済むのがポイントです。

Playwright には `InputValueAsync()`（今の値を文字列で返す）のような「今この瞬間の値を読む」メソッドもありますが、こちらは待ちません。反映される前に読んでしまうことがあるので、値を**確かめる**ときは `Assertions.Expect` の方を使います。

### 道具2: サーバーの応答を待つ `RunAndWaitForResponseAsync`

ボタンを押すと、ブラウザはサーバーへ通信します。`RunAndWaitForResponseAsync` は、「操作をしてから、その操作で起きた通信の応答が返ってくるまで」をまとめて待つメソッドです。

```csharp
// 第1引数: 実行したい操作（ここでは読み込みボタンのクリック）
// 第2引数: どの応答を待つかの条件（ここでは「URL が /api/form-data で終わる応答」）
await page.RunAndWaitForResponseAsync(
    async () => await page.GetByTestId("load-button").ClickAsync(),
    response => response.Url.EndsWith("/api/form-data"));
```

`async () => ...` や `response => ...` は、その場で小さな関数を書く書き方（ラムダ式）です。「この操作をしてね」「この条件の応答を待ってね」と、処理そのものを引数として渡しています。

応答を待ってから値を確かめれば、「サーバーから値を受け取った後の画面」を確かめていることがはっきりします。

> この2つの道具で足りない場面と、そのときの補助的な手段は、6章の後の「間章」で扱います。間章は読み飛ばしても7章以降に進めます。

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

        // --- 3. 読み込みボタンを押し、サーバーの応答が返るまで待つ ---
        await page.RunAndWaitForResponseAsync(
            async () => await page.GetByTestId("load-button").ClickAsync(),
            response => response.Url.EndsWith("/api/form-data"));

        // --- 4. 各部品が送信した値に戻ったことを確かめる ---
        // ToHaveValueAsync は「その値になるまで」自動で再確認する。
        // 開き直した直後の画面は初期値（空・50・apple・未選択）なので、
        // 読み込み結果が反映されて初めてこれらの確認が通る
        await Assertions.Expect(page.GetByTestId("text-input")).ToHaveValueAsync("ダミー太郎");
        await Assertions.Expect(page.GetByTestId("slider")).ToHaveValueAsync("42");
        await Assertions.Expect(page.GetByTestId("select")).ToHaveValueAsync("banana");

        // ラジオボタンは「緑がオンになっているか」を ToBeCheckedAsync で確かめる
        await Assertions.Expect(page.Locator("[data-testid='radio-option'][value='green']"))
            .ToBeCheckedAsync();
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
- 「`RunAndWaitForResponseAsync` でクリックして応答を待つ」「4つの部品を1つずつ `Assertions.Expect` で確かめる」という手順も、読み込みのたびに書く必要がある
- `"text-input"` のような目印の文字列を、あちこちに直接書いている（書き間違えても気付きにくい）

テストが増えるほど、この繰り返しは負担になります。次の6章では、これらを関数にまとめて整理します。

## この章のまとめ

- 同じブラウザ内の `ReloadAsync()` では Cookie が残り、同じ利用者として読み込める
- 成功時にメッセージが空になる処理は、`RunAndWaitForResponseAsync` でクリックとサーバーの応答待ちをまとめて行う
- 復元された値は `Assertions.Expect(...).ToHaveValueAsync(...)` / `ToBeCheckedAsync()` で、期待する値になるまで待ちながら確かめる
- `InputValueAsync()` のような「今の値を読む」メソッドは待たないので、確かめる用途には `Assertions.Expect` を使う
