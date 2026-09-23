# 4章 フォームへの入力と送信

3章で見つけられるようになった要素に、実際に値を入力し、送信ボタンを押して、結果のメッセージを確かめます。この章では Playwright の基本的なメソッドをそのまま使って書きます。

## この章の前提知識

- 3章: `page.GetByTestId("...")` と `page.Locator("[data-testid='radio-option'][value='...']")` で要素を探せること
- 3章: `CountAsync()` / `IsVisibleAsync()` はその瞬間の状態を返し、待たないこと
- 2章: 1つのテストメソッドの中で起動〜ページ表示を行う書き方、サーバーの手動起動
- 1章: 固定時間で待たず、自動待機に任せるという考え方

## 部品ごとの操作メソッド

| 部品 | 使うメソッド | 例 |
|---|---|---|
| テキストボックス | `FillAsync(文字列)` | 中身を消してから入力する |
| プルダウン | `SelectOptionAsync(値)` | `option` の `value`（`banana` など）を指定する |
| ラジオボタン | `CheckAsync()` | 絞り込んだ1つをオンにする |
| ボタン | `ClickAsync()` | クリックする |
| スライダー | `EvaluateAsync(...)` | 下で説明 |

これらの操作系メソッドは、**要素が表示されて操作できる状態になるまで自動で待ってから**操作します（1章で紹介した自動待機）。そのため `Task.Delay` のような待機を書く必要はありません。

### スライダーだけは JavaScript で値を設定する

このプロジェクトでは、スライダー（`<input type="range">`）の値は、ブラウザの中で JavaScript を実行して設定する方式に統一しています。値を書き換えるだけでなく、`input` / `change` イベントも発生させ、人が手で動かしたときと同じ通知がページに届くようにしています。完成版テストのヘルパー関数（6章で扱います）も同じ方式です。

## 送信後、どうやって結果を確かめるか

サンプルアプリは送信ボタンが押されると、次の流れで動きます。

1. 画面の値を集めてサーバーへ送る（`app.js` の `handleSubmitButtonClick`）
2. サーバーが値を検証する。テキストが1000文字以下などのルールを満たせば保存して成功（HTTP 200）、満たさなければ保存を拒否（HTTP 400）
3. 結果に応じて、メッセージ欄（`data-testid="message-area"`）の表示を切り替える

| 結果 | メッセージ欄のCSSクラス | 表示される文言 |
|---|---|---|
| 保存成功 | `message--success` | 保存しました。 |
| 保存拒否 | `message--error` | 保存できませんでした。入力内容を確認してください。（HTTP 400） |

テストでは、サーバーの内部を直接調べるのではなく、**画面に表示された結果（メッセージ欄のクラス）だけ**を確かめます。利用者が目にするものを確かめるのが、ブラウザテストの役割だからです。

ここで問題になるのが「送信の結果が画面に反映されるまで、少し時間がかかる」ことです。そこで `Assertions.Expect(...)` を使います。

```csharp
// メッセージ欄に message--success クラスが付くまで、繰り返し確認しながら待つ。
// 条件を満たした時点ですぐ次へ進み、既定では5秒たっても満たさなければ失敗になる
await Assertions.Expect(page.GetByTestId("message-area"))
    .ToHaveClassAsync(new Regex("message--success"));
```

`Assert.That` は「その瞬間の値」を1回だけ比べますが、`Assertions.Expect` は**条件を満たすまで自動で再確認する**検証です。画面が変化するのを待つ場面では、こちらを使います。

## テストコード

学習用プロジェクトに `SubmitTests.cs` を作ります。正常に保存されるケースと、わざと検証エラーにするケースの2つを書きます。

```csharp
using System.Text.RegularExpressions; // Regex（正規表現）を使うため
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

[TestFixture]
public class SubmitTests
{
    [Test]
    public async Task Submit_ValidValues_ShowsSuccessMessage()
    {
        // 準備: これまでと同じく、このテストの中でブラウザを起動してページを開く
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        // テキストボックスに入力する（ダミー値だけを使う）
        await page.GetByTestId("text-input").FillAsync("ダミー太郎");

        // スライダーを42にする。
        // el はスライダー要素、v は第2引数で渡した "42"。値を入れたあと、
        // 手で動かしたときと同じ input / change イベントを発生させる
        await page.GetByTestId("slider").EvaluateAsync(
            "(el, v) => { el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }",
            "42");

        // プルダウンで「バナナ」を選ぶ（画面の表示文字ではなく value="banana" を指定する）
        await page.GetByTestId("select").SelectOptionAsync("banana");

        // 同じ data-testid が3つあるので、value で「緑」に絞り込んでからオンにする
        await page.Locator("[data-testid='radio-option'][value='green']").CheckAsync();

        // 送信ボタンを押す（押せる状態になるまで自動で待ってからクリックされる）
        await page.GetByTestId("submit-button").ClickAsync();

        // 送信結果が画面に出るまで待ちつつ、「成功」の表示になったことを確かめる
        await Assertions.Expect(page.GetByTestId("message-area"))
            .ToHaveClassAsync(new Regex("message--success"));
    }

    [Test]
    public async Task Submit_TooLongText_ShowsErrorMessage()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        // サーバーのルールは「テキストは1000文字まで」。
        // わざと1文字多い1001文字（'a' を1001個並べた文字列）を入れて、拒否されることを確かめる
        await page.GetByTestId("text-input").FillAsync(new string('a', 1001));

        // 他の部品は初期値のまま（スライダー50、プルダウン apple、ラジオ未選択）で送信する
        await page.GetByTestId("submit-button").ClickAsync();

        // 「エラー」の表示になるまで待って確かめる
        await Assertions.Expect(page.GetByTestId("message-area"))
            .ToHaveClassAsync(new Regex("message--error"));
    }
}
```

サーバーを起動した状態で `dotnet test` を実行し、`失敗: 0` になることを確認してください。

## 確認してみよう

- 1つ目のテストの期待値を `message--error` に変えると、5秒ほど待ったあとに失敗します。`Assertions.Expect` が条件を満たすまで待ち続け、時間切れで失敗したことが分かります（確認後は元に戻してください）。
- 2つ目のテストの文字数を `1000` にすると、今度は保存に成功するため失敗します。1000文字は「ちょうど上限」で許可される値です。

## この章のまとめ

- `FillAsync` / `SelectOptionAsync` / `CheckAsync` / `ClickAsync` は、操作できる状態になるまで自動で待つ
- スライダーは `EvaluateAsync` で値を設定し、イベントを発生させる
- 画面の変化を待って確かめるときは `Assertions.Expect(...).ToHaveClassAsync(...)` を使い、固定時間の待機は書かない

次の5章では、送信したデータを「読み込み」ボタンで取り戻し、画面に正しく復元されるかを確かめます。
