# 9章 完成形のシナリオテスト

いよいよ最後の章です。8章までに学んだことをすべて組み合わせ、このリポジトリの完成版テスト（`tests/LearnPlaywright.Tests/Scenarios/FormDataScenarioTests.cs`）と同じ2つのシナリオテストを完成させます。

## この章の前提知識

- 8章: `[OneTimeSetUp]` / `[OneTimeTearDown]` / `[SetUp]` / `[TearDown]` によるライフサイクル管理と、サーバーの自動起動
- 7章: `FormValuesTestCase`（`ExpectedSubmitSuccess` / `ExpectedRestored`）と `[TestCaseSource]`
- 6章: ヘルパー関数（`SetFormValuesAsync`、`ClickSubmitButtonAsync`、`ClickLoadButtonAsync`、`GetFormValuesAsync`、`GetMessageAsync`）
- 5章: 同じコンテキスト内の `ReloadAsync()` では Cookie が残ること、未保存のときは `message--info` が表示されること
- 1章: テストの独立性

## シナリオ1: 送信して、受理・拒否を確かめる

8章の `FixtureTests` に書いた `SubmitAndVerifySavedResult` が、そのまま完成形のシナリオ1です。

```csharp
// 入力 → 送信 → メッセージの種別で「受理されたか・拒否されたか」を確かめる
[Test]
[TestCaseSource(typeof(FormValuesTestCases), nameof(FormValuesTestCases.GetCases))]
public async Task SubmitAndVerifySavedResult(FormValuesTestCase testCase)
{
    await SetFormValuesAsync(_page!, testCase.Input);   // 6章: 4つの部品へまとめて入力
    await ClickSubmitButtonAsync(_page!);               // 6章: クリック＋処理完了待ち
    MessageState message = await GetMessageAsync(_page!);

    // 7章: 期待値はテストケースのデータから決まる
    Assert.That(message.Type, Is.EqualTo(testCase.ExpectedSubmitSuccess ? "success" : "error"));
}
```

## シナリオ2: 読み込んで、復元を確かめる

`FixtureTests` クラスに、次のメソッドを追加します。

```csharp
// 送信 → ページを開き直す → 読み込み → 復元された値（または「未保存」メッセージ）を確かめる
[Test]
[TestCaseSource(typeof(FormValuesTestCases), nameof(FormValuesTestCases.GetCases))]
public async Task LoadAndVerifyRestoredValues(FormValuesTestCase testCase)
{
    // 前提データは、このテスト自身が送信して用意する。
    // シナリオ1の結果には頼らない（そもそも [SetUp] でコンテキストが新しくなるので、
    // シナリオ1で保存したデータの Cookie はこのテストには残っていない）
    await SetFormValuesAsync(_page!, testCase.Input);
    await ClickSubmitButtonAsync(_page!);

    // 同じコンテキストなので Cookie は残る → 同じ利用者として読み込める（5章）
    await _page!.ReloadAsync();
    await ClickLoadButtonAsync(_page!);

    if (testCase.ExpectedRestored is not null)
    {
        // 保存に成功するはずのケース: 画面の4つの値が期待どおりに戻っているか
        FormValues restored = await GetFormValuesAsync(_page!);

        // Assert.Multiple: 1つ目が失敗しても残りも確かめ、ずれている項目をまとめて報告する
        Assert.Multiple(() =>
        {
            Assert.That(restored.Text, Is.EqualTo(testCase.ExpectedRestored.Text));
            // 小数を比べるときは、ごくわずかな誤差を許す（Within）のが安全
            Assert.That(restored.Slider, Is.EqualTo(testCase.ExpectedRestored.Slider).Within(1e-9));
            Assert.That(restored.Select, Is.EqualTo(testCase.ExpectedRestored.Select));
            Assert.That(restored.Radio, Is.EqualTo(testCase.ExpectedRestored.Radio));
        });
    }
    else
    {
        // 保存が拒否されるはずのケース: 何も保存されていないので「保存されたデータがありません。」が出る
        MessageState message = await GetMessageAsync(_page!);
        Assert.That(message.Type, Is.EqualTo("info"));
    }
}
```

`dotnet test` を実行します。2つのシナリオ × 12ケースで、`FixtureTests` のテストが合計24件実行されます（8章で `[Ignore]` を付けた旧レッスンのクラスは「スキップ」として数えられます）。`失敗: 0` なら完成です。

### ポイント: 独立性の実例

`LoadAndVerifyRestoredValues` は、`SubmitAndVerifySavedResult` が先に実行されたかどうかに関係なく成功します。

- 前提データ（保存済みの値）は自分で送信して用意している
- `[SetUp]` で毎回新しいコンテキストを作るので、他のテストの Cookie・保存データを引き継がない

そのため、この1件だけを実行しても、実行順が変わっても、結果は同じです。これが1章で紹介した「テストの独立性」の実例です。

### ポイント: 拒否ケースの確かめ方

`BND-TEXT-OVERLEN`（1001文字）のケースでは、送信が拒否されるので何も保存されません。新しいコンテキストで最初の送信が拒否された場合、読み込みは「データなし」になり、`message--info` が表示されます。`ExpectedRestored` が `null` かどうかで確かめる内容を切り替えているのは、このためです。

## 完成版テストと読み比べる

リポジトリの `tests/LearnPlaywright.Tests` を開き、自分の学習用プロジェクトと比べてみましょう。

| 学習用プロジェクト | 完成版テスト | 違い |
|---|---|---|
| `FormPageHelpers.cs`（6章） | `TestData/FormPageHelpers.cs` | 関数の中身は同じ。学習用では `FormValues` / `MessageState` / `FormTestIds` もこのファイルに置いた |
| `FormTestData.cs`（7章） | `TestData/FormTestData.cs` | 完成版は、6章の `FormValues` / `MessageState` / `FormTestIds` もこちらのファイルにまとめている |
| `FixtureTests.cs`（8〜9章） | `Scenarios/FormDataScenarioTests.cs` | サーバーの場所を `global.json` から探す（`FindServerProjectPath`）。起動前にポート使用中を検出する。環境変数 `LEARNPLAYWRIGHT_HEADED=1` でブラウザ画面を表示して実行できる。この2つのシナリオのほかに、画面表示・Cookie発行・異常系などを確かめる追加のテストも含まれている |

どちらも、テストメソッドの中には**画面の細かい操作（DOM操作）も、入力値の直書きもありません**。操作はヘルパー関数に、入力値と期待値はテストケースのデータに任せ、テストメソッドは「手順」だけを書いています。これがこの教材で目指してきた形です。

## 教材全体の振り返り

| 章 | 学んだこと |
|---|---|
| 0 | .NET 10 SDK の確認、学習用プロジェクト作成、ブラウザのインストール |
| 1 | 独立性・待機処理・ロケーター・パラメータ化の考え方 |
| 2 | ページを開いてタイトルを確かめる最小のテスト |
| 3 | `data-testid` を使ったロケーター |
| 4 | 入力・送信と、`Assertions.Expect` による変化の待機 |
| 5 | 読み込みと復元の検証、`data-request-count` による処理完了待ち |
| 6 | ヘルパー関数による整理 |
| 7 | `[TestCaseSource]` によるデータ駆動テスト |
| 8 | ライフサイクル属性とサーバーの自動起動 |
| 9 | 2つのシナリオテストの完成 |

## 次のステップ

- リポジトリの `tests/LearnPlaywright.Tests` 配下のコードを、最初から最後まで読んでみましょう。2つのシナリオとヘルパー関数は、ここまでの内容だけで読めるはずです。追加のテストには、サーバーの応答を差し替える `RouteAsync` など、この教材で扱わなかった機能も登場するので、Playwright の公式ドキュメントと合わせて読んでみてください。
- `FormValuesTestCases.GetCases` に自分でケースを追加してみましょう（例: スライダーの中間値、テキストに日本語の長い文を入れる など。ダミー値だけを使ってください）。
- Playwright には、この教材で扱わなかった機能（画面のスクリーンショット、操作の記録など）もあります。興味があれば公式ドキュメントで調べてみてください。
