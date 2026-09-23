# 2章 最初のPlaywrightテストを書く

この章では、サンプルアプリのページをブラウザで開き、ページのタイトルが正しいかを確かめるだけの、いちばん小さなテストを書いて実行します。この章の最後で `dotnet test` が成功すれば、「環境構築から自分のテストの実行まで」を一通り体験したことになります。

## この章の前提知識

- 0章: 学習用プロジェクト `MyFirstPlaywrightTests` を作り、`dotnet test` が成功したこと
- 0章 手順5: サンプルアプリ（このリポジトリ）を手元に用意し、`dotnet build` でビルドできたこと
- 1章: テストは他のテストに頼らず独立させる、固定時間で待たない、という考え方

## 注意: 入力するのはダミー値だけ

サンプルアプリは学習用で、認証の仕組みがなく、自分のPC（`localhost`）の中だけで動かす前提です。この章では入力は行いませんが、以降の章でもフォームには**実在の個人情報を入れず、ダミー値だけ**を使ってください。

## 手順1: サンプルアプリのサーバーを起動する

テストがページを開くには、サーバーが動いている必要があります。**新しいターミナルをもう1つ開き**、このリポジトリのルートフォルダ（`global.json` があるフォルダ）で次を実行します。

```powershell
# リポジトリのルートフォルダに移動する
cd C:\work\learn-playwright-dotnet

# サンプルアプリ（ASP.NET Core）のサーバーを起動する
dotnet run --project src/LearnPlaywright.Server
```

しばらくして次のような表示が出れば起動完了です。

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5080
info: LearnPlaywright.Server[0]
      Bound address: http://localhost:5080 (loopback: True)
```

（0章 手順5-3 と同じ表示です。前後に `Application started.` などの行も出ます。）

- このターミナルは**開いたまま**にしておきます（閉じるとサーバーが止まります）。止めたいときは、そのターミナルで `Ctrl+C` を押します。
- ブラウザで http://localhost:5080/ を開くと、「LearnPlaywright サンプルフォーム」という見出しのページが表示されます。

## 手順2: NUnit の最小構成を知る

NUnit では、クラスとメソッドに**属性**（`[ ]` で囲んだ目印）を付けてテストを表します。

| 属性 | 付ける場所 | 意味 |
|---|---|---|
| `[TestFixture]` | クラス | 「このクラスにはテストが入っている」という目印 |
| `[Test]` | メソッド | 「このメソッドは1つのテストである」という目印 |

## 手順3: Playwright の3つの登場人物

| 型 | 役割 |
|---|---|
| `IPlaywright` | Playwright そのもの。ここからブラウザの種類（Chromium など）を選ぶ |
| `IBrowser` | 起動したブラウザ本体 |
| `IPage` | ブラウザの中の1つのタブ（ページ）。ここに対して「開く」「クリック」などを行う |

## 手順4: テストコードを書く

学習用プロジェクトに `FirstTests.cs` というファイルを新しく作り、次の内容を書きます（雛形の `UnitTest1.cs` は削除しても、残したままでも構いません）。

```csharp
// Playwright の型（IPlaywright, IBrowser, IPage など）を使えるようにする
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

// このクラスにテストが入っていることを NUnit に知らせる
[TestFixture]
public class FirstTests
{
    // [Test] を付けたメソッドが1つのテストになる。
    // Playwright の操作は非同期（完了を待つ必要がある）なので、async Task にする
    [Test]
    public async Task TopPage_HasExpectedTitle()
    {
        // 1. Playwright を起動する。
        //    using を付けると、テストの終わりに自動で後片付け（破棄）される
        using var playwright = await Playwright.CreateAsync();

        // 2. Chromium ブラウザを起動する。
        //    Headless = true は「画面を表示せずに裏で動かす」指定（テストが速く、邪魔にならない）
        //    await using で、テストの終わりにブラウザが自動で閉じられる
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        // 3. 新しいページ（タブ）を開く
        var page = await browser.NewPageAsync();

        // 4. 手動で起動しておいたサーバーのトップページを開く
        await page.GotoAsync("http://localhost:5080/");

        // 5. ページのタイトル（<title> タグの中身）を取り出す
        string title = await page.TitleAsync();

        // 6. 期待どおりのタイトルかを確かめる。違っていればテストは失敗になる
        Assert.That(title, Is.EqualTo("LearnPlaywright サンプルフォーム"));
    }
}
```

### なぜ1つのメソッドの中で「起動から破棄まで」全部やるのか

本来は、ブラウザの起動などの準備処理はテストごとに書かず、まとめて管理します。ただし、その書き方（NUnit の準備・後片付け用の仕組み）は8章で学びます。今は「1つのテストの中に、必要なことが全部書いてある」ほうが流れを追いやすいので、あえてこの形にしています。

また、テストごとにブラウザを新しく起動しているので、1章で学んだ「テストの独立性」も自然に守られています。

## 手順5: テストを実行する

サーバーを起動したターミナルとは**別のターミナル**で、学習用プロジェクトのフォルダに移動して実行します。

```powershell
# 学習用プロジェクトのフォルダに移動する
cd C:\work\MyFirstPlaywrightTests

# テストを実行する
dotnet test
```

期待される出力の例:

```text
成功!   -失敗:     0、合格:     2、スキップ:     0、合計:     2、期間: 1 s - MyFirstPlaywrightTests.dll (net10.0)
```

雛形の `Test1` を残している場合は合計2件、削除した場合は1件になります。**`失敗: 0` なら成功です。** 英語環境では `Passed!` と表示されます。

## うまくいかないとき

| 症状 | 確認すること |
|---|---|
| `net::ERR_CONNECTION_REFUSED` を含むエラー | サーバーが起動しているか（手順1のターミナルが開いたままか） |
| `Executable doesn't exist` を含むエラー | 0章の手順3（ブラウザのインストール）が済んでいるか |
| `A compatible .NET SDK was not found` | 0章の手順1で .NET 10 SDK（10.0.x）が導入済みか。リポジトリの `global.json` は 10.0.100 以上の10.0系SDKを要求する |
| タイトルが違うと言われる | 期待値の文字列（全角・半角、スペース）が完全に一致しているか |

## この章のまとめ

- `[TestFixture]` / `[Test]` でテストを表す
- `Playwright.CreateAsync()` → `LaunchAsync` → `NewPageAsync` → `GotoAsync` の順でページを開く
- `Assert.That(実際の値, Is.EqualTo(期待値))` で結果を確かめる
- 2〜7章では、テストの前にサーバーを別ターミナルで手動起動しておく

次の3章では、ページの中の入力欄やボタンを探す方法（ロケーター）を学びます。
