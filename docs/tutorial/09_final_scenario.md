# 9章 完成形のシナリオテスト

いよいよ最後の章です。8章までに学んだことをすべて組み合わせ、このリポジトリの完成版テスト（`tests/LearnPlaywright.Tests/Scenarios/FormDataScenarioTests.cs`）と同じ2つのシナリオテストを完成させます。

## この章の前提知識

- 8章: `[OneTimeSetUp]` / `[OneTimeTearDown]` / `[SetUp]` / `[TearDown]` によるライフサイクル管理と、サーバーの自動起動
- 7章: `FormValuesTestCase`（`ExpectedSubmitSuccess` / `ExpectedRestored`）と `[TestCaseSource]`
- 6章: ヘルパー関数（`SetFormValuesAsync`、`ClickSubmitButtonAsync`、`ClickLoadButtonAsync`、`ExpectFormValuesAsync`、`ExpectMessageTypeAsync`）と、ボタンの関数が応答を待ってから戻る理由
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
    await ClickSubmitButtonAsync(_page!);               // 6章: クリック＋応答待ち

    // 7章: 期待値はテストケースのデータから決まる
    await ExpectMessageTypeAsync(_page!, testCase.ExpectedSubmitSuccess ? "success" : "error");
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
    // 送信の応答が返るまで待ってから戻るので、直後に開き直しても保存が中断されない（6章）
    await ClickSubmitButtonAsync(_page!);

    // 同じコンテキストなので Cookie は残る → 同じ利用者として読み込める（5章）
    await _page!.ReloadAsync();
    await ClickLoadButtonAsync(_page!);

    if (testCase.ExpectedRestored is not null)
    {
        // 保存に成功するはずのケース: 画面の4つの値が期待どおりになっているかを確かめる
        // （期待値が開き直した直後の画面と同じ項目には注意点がある。下の「ポイント」参照）
        await ExpectFormValuesAsync(_page!, testCase.ExpectedRestored);
    }
    else
    {
        // 保存が拒否されるはずのケース: 何も保存されていないので「保存されたデータがありません。」が出る
        await ExpectMessageTypeAsync(_page!, "info");
    }
}
```

`dotnet test` を実行します。2つのシナリオ × 12ケースで、`FixtureTests` のテストが合計24件実行されます（8章で `[Ignore]` を付けた旧レッスンのクラスの20件は「スキップ」として数えられます。雛形の `Test1` を残している場合は合格が1件増えます）。`失敗: 0` なら完成です。

### ポイント: 独立性の実例

`LoadAndVerifyRestoredValues` は、`SubmitAndVerifySavedResult` が先に実行されたかどうかに関係なく成功します。

- 前提データ（保存済みの値）は自分で送信して用意している
- `[SetUp]` で毎回新しいコンテキストを作るので、他のテストの Cookie・保存データを引き継がない

そのため、この1件だけを実行しても、実行順が変わっても、結果は同じです。これが1章で紹介した「テストの独立性」の実例です。

### ポイント: 拒否ケースの確かめ方

`BND-TEXT-OVERLEN`（1001文字）のケースでは、送信が拒否されるので何も保存されません。新しいコンテキストで最初の送信が拒否された場合、読み込みは「データなし」になり、`message--info` が表示されます。`ExpectedRestored` が `null` かどうかで確かめる内容を切り替えているのは、このためです。

### ポイント: 画面を見るだけでは確かめられないこと

ページを開き直した直後の画面は初期値（テキストは空、スライダー50、プルダウン `apple`、ラジオは未選択）です。期待値の中にこの初期値と同じ項目があると、その項目については「読み込みで戻った」のか「最初からそうだった」のかを、画面からは区別できません。このシナリオでは次の3ケースが当てはまります。

| ケース | 初期値と同じ項目 |
|---|---|
| `BND-TEXT-EMPTY` | テキスト（空文字） |
| `BND-SELECT-FIRST` | プルダウン（`apple`） |
| `BND-RADIO-UNSELECTED` | ラジオボタン（未選択） |

さらに `BND-TEXT-EMPTY` では、確かめる順番も関係します。`ExpectFormValuesAsync` はテキスト → スライダー → プルダウン → ラジオの順に確かめるので、最初のテキストの確認は、読み込み結果が画面に反映される前にすぐ合格します。その次のスライダーの確認（期待値42、初期値50）は反映を待ちます。アプリは4つの値を一度に書き換えるので、スライダー以降の確認は反映後の画面に対して行われます。

つまり、これらのケースでも「保存と読み込みがエラーなく通り、他の項目が正しく戻ること」は確かめられます。一方で、「初期値と同じ項目が本当に復元されたか」までは確かめられていません。このような確認漏れを減らす工夫の1つは、**読み込みボタンを押す前に、画面の値を期待値とわざと違うものにしておく**ことです（例: テキストに `"読み込み前"` と入れてから読み込み、空文字に戻ることを確かめる）。この場面の詳しい説明と、ほかの対策は間章で扱っています。

## 完成版テストと読み比べる

リポジトリの `tests/LearnPlaywright.Tests` を開き、自分の学習用プロジェクトと比べてみましょう。

| 学習用プロジェクト | 完成版テスト | 違い |
|---|---|---|
| `FormPageHelpers.cs`（6章） | `TestData/FormPageHelpers.cs` | 値を入れる関数は同じ。**確かめ方と待ち方が違う**（下記参照）。学習用では `FormValues` / `FormTestIds` もこのファイルに置いた |
| `FormTestData.cs`（7章） | `TestData/FormTestData.cs` | 完成版は、`FormValues` / `FormTestIds` のほか、メッセージの状態を表す型 `MessageState` もこちらのファイルにまとめている |
| `FixtureTests.cs`（8〜9章） | `Scenarios/FormDataScenarioTests.cs` | サーバーの場所を `global.json` から探す（`FindServerProjectPath`）。起動前にポート使用中を検出する。環境変数 `LEARNPLAYWRIGHT_HEADED=1` でブラウザ画面を表示して実行できる。この2つのシナリオのほかに、画面表示・Cookie発行・異常系などを確かめる追加のテストも含まれている |

### 完成版ヘルパーの確かめ方・待ち方

完成版の `FormPageHelpers.cs` は、6章とは違う方針で書かれています。

- **確かめ方**: 6章の「期待どおりになるまで待って確かめる関数」（`ExpectFormValuesAsync` など）の代わりに、「今の値を読む関数」（`GetFormValuesAsync` / `GetMessageAsync`）を持ち、テスト側で `Assert.That` を使って比べます。NUnit の `Assert.Multiple`（中の `Assert.That` が途中で失敗しても残りも確かめ、ずれている項目をまとめて報告する機能）を使えるようにするためです。
- **待ち方**: 今の値を読む関数は待たないので、ボタンの関数（`ClickSubmitButtonAsync` / `ClickLoadButtonAsync`）が、サーバーの応答だけでなく**画面の書き換えまで終わるのを待ってから**戻るようにしています。そのために、アプリの form 要素に付けた `data-request-count` 属性（処理が終わるたびに1ずつ増える数）が変わるのを待っています。

この書き方の理由と代償は「間章」で詳しく扱っています。間章を読んでいなくても、完成版のボタンの関数は「押した後、画面の書き換えまで終わるのを待ってから戻る関数」と読めば十分です。

### 共通する形

どちらも、テストメソッドの中には**画面の細かい操作（DOM操作）も、入力値の直書きもありません**。操作はヘルパー関数に、入力値と期待値はテストケースのデータに任せ、テストメソッドは「手順」だけを書いています。これがこの教材で目指してきた形です。

## 教材全体の振り返り

| 章 | 学んだこと |
|---|---|
| 0 | .NET 10 SDK の確認、学習用プロジェクト作成、ブラウザのインストール、サンプルアプリのビルド・起動確認 |
| 1 | 独立性・待機処理・ロケーター・パラメータ化の考え方 |
| 2 | ページを開いてタイトルを確かめる最小のテスト |
| 3 | `data-testid` を使ったロケーター |
| 4 | 入力・送信と、`Assertions.Expect` による変化の待機 |
| 5 | 読み込みと復元の検証、`RunAndWaitForResponseAsync` による応答待ちと `ToHaveValueAsync` による値の確認 |
| 6 | ヘルパー関数による整理（操作する関数と確かめる関数） |
| 間章 | （任意）5〜6章の書き方で足りない場面と、アプリ側に処理完了の目印を置く補助的な手段 |
| 7 | `[TestCaseSource]` によるデータ駆動テスト |
| 8 | ライフサイクル属性とサーバーの自動起動 |
| 9 | 2つのシナリオテストの完成 |

## この章で出てきた C# の書き方

| 書き方 | 意味 |
|---|---|
| `if (testCase.ExpectedRestored is not null)` | `is not null` は「null でない」。値があるときだけ `{ }` の中を実行する |

## 次のステップ

- リポジトリの `tests/LearnPlaywright.Tests` 配下のコードを、最初から最後まで読んでみましょう。2つのシナリオとヘルパー関数の大筋は、ここまでの内容で読めるはずです。細かいところでは、この教材で扱っていない API や書き方（属性の値を読む `GetAttributeAsync`、要素の文字を読む `TextContentAsync`、小数の誤差を許して比べる `Is.EqualTo(...).Within(...)`、一覧を絞り込む `Where` など）も出てきます。追加のテストには、サーバーの応答を差し替える `RouteAsync` など、この教材で扱わなかった機能も登場するので、Playwright の公式ドキュメントと合わせて読んでみてください。
- `FormValuesTestCases.GetCases` に自分でケースを追加してみましょう（例: スライダーの中間値、テキストに日本語の長い文を入れる など。ダミー値だけを使ってください）。
- Playwright には、この教材で扱わなかった機能（画面のスクリーンショット、操作の記録など）もあります。興味があれば公式ドキュメントで調べてみてください。
