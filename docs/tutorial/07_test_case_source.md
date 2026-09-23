# 7章 TestCaseSourceによるデータ駆動テスト

6章の最後に、「入力値がテストメソッドの中に直接書かれているので、パターンが増えるとメソッドを複製するしかない」という課題が残りました。この章では、1章で紹介した**パラメータ化**を NUnit の `[TestCaseSource]` で実現し、1つのテストメソッドで多数の入力パターンを試せるようにします。

## この章の前提知識

- 6章: `FormValues` / `MessageState` 型、`FormTestIds` 定数、ヘルパー関数（`SetFormValuesAsync`、`ClickSubmitButtonAsync`、`GetMessageAsync` など）
- 4章: テキストは1000文字まで保存でき、1001文字だと拒否される（`message--error`）こと
- 1章: 入力値と期待値をデータとして一か所にまとめ、テストの手順と分ける考え方

## 振り返り: 何が困っていたか

「空文字」「1000文字ちょうど」「1001文字」「スライダー0」「スライダー100」…を試すには、4〜6章の書き方だと1パターンにつき1つのテストメソッドが必要でした。手順（入力→送信→確認）はどれも同じで、違うのは**入力値と期待する結果だけ**です。

## 手順1: 1件のテストケースを表す型を作る

学習用プロジェクトに `FormTestData.cs` を作ります。まず「1件のテストケース」を表す型です。

```csharp
namespace MyFirstPlaywrightTests;

// 1件のテストケース = 「名前・分類・入力値・期待する結果」の組
public sealed record FormValuesTestCase(
    string CaseId,                 // ケースを見分ける名前（テスト結果の表示にも使う）
    string Category,               // 代表値・境界値などの分類（人が読むための情報）
    FormValues Input,              // フォームに入れる値
    bool ExpectedSubmitSuccess,    // 保存に成功するはずなら true、拒否されるはずなら false
    FormValues? ExpectedRestored)  // 読み込みで戻るはずの値。拒否を期待するケースでは null
{
    // テスト結果の一覧でケースを見分けやすいよう、文字列にすると CaseId になるようにする
    public override string ToString() => CaseId;

    // 「保存に成功し、入れた値がそのまま戻ってくる」ことを期待するケースを作る
    public static FormValuesTestCase RoundTrip(string caseId, string category, FormValues input)
    {
        if (string.IsNullOrEmpty(caseId)) throw new ArgumentException("caseId is required", nameof(caseId));
        ArgumentNullException.ThrowIfNull(input);
        return new FormValuesTestCase(caseId, category, input, true, input);
    }

    // 「サーバーの検証で保存が拒否される」ことを期待するケースを作る
    public static FormValuesTestCase ExpectedRejection(string caseId, string category, FormValues invalidInput)
    {
        if (string.IsNullOrEmpty(caseId)) throw new ArgumentException("caseId is required", nameof(caseId));
        ArgumentNullException.ThrowIfNull(invalidInput);
        return new FormValuesTestCase(caseId, category, invalidInput, false, null);
    }
}
```

`RoundTrip` と `ExpectedRejection` のような「作り方に名前を付けた関数」（ファクトリメソッド）を使うと、期待値の組み合わせを間違えにくくなります。たとえば「成功を期待するのに復元値が null」といった矛盾したケースを作ってしまうことを防げます。

## 手順2: 境界値と選択肢の定数

同じファイルに、ケースを作るときに使う定数を書きます。数字を直接書かずに名前を付けておくと、「なぜ1000なのか」が読み手に伝わります。

```csharp
// サーバーの検証ルールと、画面（index.html）のスライダー範囲に合わせた境界値
public static class FormFieldLimits
{
    // テキストボックスに入れてよい最大文字数（サーバーの検証ルール）。
    // 1000文字ちょうどは成功、1001文字は検証エラーになることを下のケースで確かめる
    public const int TextMaxLength = 1000;

    // プルダウン・ラジオボタンの値の最大文字数（サーバーの検証ルール）。
    // この教材でも完成版の E2E テストでも使っていない。画面からは決められた選択肢
    // （apple など）しか選べず、201文字の値をブラウザ操作で送れないため。
    // この長さの境界は、サーバーのロジックへ値を直接渡す単体テスト
    // （tests/LearnPlaywright.UnitTests の FormDataStoreTests）で確かめている。
    // ここではサーバーのルールと対応させるために定数として残している
    public const int SelectMaxLength = 200;
    public const int RadioMaxLength = 200;

    // スライダーの最小値・最大値（index.html の min / max 属性の値）。
    // 名前に Placeholder（仮の値）と付いているのは、設計の段階ではまだ値が決まっていなかった名残り。
    // 値は実装で 0 と 100 に確定済みだが、設計書と名前で対応を取れるように名前はそのまま残している
    public const double SliderMinPlaceholder = 0;
    public const double SliderMaxPlaceholder = 100;
}

// 画面のプルダウン・ラジオボタンの選択肢（value 属性の値）
public static class FormOptionValues
{
    public static readonly IReadOnlyList<string> SelectOptions = ["apple", "banana", "cherry"];
    public static readonly IReadOnlyList<string> RadioOptions = ["red", "green", "blue"];
}
```

## 手順3: テストケースの一覧を作る

同じファイルに、テストケースをまとめて返すクラスを書きます。

```csharp
public static class FormValuesTestCases
{
    // 基準になる入力値（ダミー値のみ）。各ケースはここから1か所だけ変えて作る
    private static readonly FormValues Base = new("ダミー太郎", 42, "banana", "green");

    // テストケースの一覧を返す。テスト側の [TestCaseSource] がこのメソッドを名前で呼び出し、
    // 返ってきたケース1件ごとにテストメソッドを1回ずつ実行する
    public static IEnumerable<TestCaseData> GetCases()
    {
        var cases = new List<FormValuesTestCase>
        {
            FormValuesTestCase.RoundTrip("REP-DEFAULT", "代表値", Base),
            // with { ... } は「Base をコピーし、指定した項目だけ変える」書き方
            FormValuesTestCase.RoundTrip("BND-TEXT-EMPTY", "境界値-文字列長", Base with { Text = "" }),
            FormValuesTestCase.RoundTrip("BND-TEXT-MAXLEN", "境界値-文字列長", Base with { Text = new string('a', FormFieldLimits.TextMaxLength) }),
            FormValuesTestCase.ExpectedRejection("BND-TEXT-OVERLEN", "検証エラー", Base with { Text = new string('a', FormFieldLimits.TextMaxLength + 1) }),
            FormValuesTestCase.RoundTrip("BND-SLIDER-MIN", "境界値-数値", Base with { Slider = FormFieldLimits.SliderMinPlaceholder }),
            FormValuesTestCase.RoundTrip("BND-SLIDER-MAX", "境界値-数値", Base with { Slider = FormFieldLimits.SliderMaxPlaceholder }),
            FormValuesTestCase.RoundTrip("BND-SELECT-FIRST", "境界値-選択肢", Base with { Select = FormOptionValues.SelectOptions[0] }),
            FormValuesTestCase.RoundTrip("BND-SELECT-LAST", "境界値-選択肢", Base with { Select = FormOptionValues.SelectOptions[^1] }),
            FormValuesTestCase.RoundTrip("BND-RADIO-UNSELECTED", "境界値-未選択", Base with { Radio = null }),
        };
        // ラジオボタンの選択肢それぞれについてケースを追加する（選択肢が増えても自動で追従する）
        for (int i = 0; i < FormOptionValues.RadioOptions.Count; i++)
        {
            cases.Add(FormValuesTestCase.RoundTrip(
                $"BND-RADIO-EACHOPTION-{i + 1}", "境界値-選択肢", Base with { Radio = FormOptionValues.RadioOptions[i] }));
        }

        // NUnit に渡す形（TestCaseData）に包み、テスト結果に表示される名前を CaseId にする
        return cases.Select(testCase => new TestCaseData(testCase).SetName(testCase.CaseId));
    }
}
```

`TestCaseData` は NUnit の型です。`dotnet new nunit` の雛形では `NUnit.Framework` が自動で `using` されるよう設定されていますが、「`TestCaseData` が見つからない」というエラーが出た場合は、ファイルの先頭に `using NUnit.Framework;` を追加してください。

## 手順4: [TestCaseSource] でテストメソッドにデータを流し込む

`DataDrivenTests.cs` を作ります。テストメソッドが引数 `testCase` を受け取る点が、これまでと違います。

```csharp
using Microsoft.Playwright;
using static MyFirstPlaywrightTests.FormPageHelpers;

namespace MyFirstPlaywrightTests;

[TestFixture]
public class DataDrivenTests
{
    // [TestCaseSource(どのクラスの, どのメソッドから)] で、GetCases が返すケースの数だけ
    // このメソッドが繰り返し実行される。各回の testCase に1件ずつ入ってくる
    [Test]
    [TestCaseSource(typeof(FormValuesTestCases), nameof(FormValuesTestCases.GetCases))]
    public async Task SubmitAndVerifySavedResult(FormValuesTestCase testCase)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync("http://localhost:5080/");

        // 入力値はテストケースから受け取る。メソッドの中に値を直接書かない
        await SetFormValuesAsync(page, testCase.Input);
        await ClickSubmitButtonAsync(page);
        MessageState message = await GetMessageAsync(page);

        // 期待する結果もテストケースから決まる（成功なら success、拒否なら error）
        Assert.That(message.Type, Is.EqualTo(testCase.ExpectedSubmitSuccess ? "success" : "error"));
    }
}
```

サーバーを起動した状態で `dotnet test` を実行します。今回は**テストメソッドは1つなのに、ケースの数（12件）だけテストが実行**され、合計の件数が一気に増えることを確認してください。失敗したときは、`BND-TEXT-OVERLEN` のようにケース名で表示されるので、どのパターンで失敗したかがすぐ分かります。

## 手順5: パターンを1件追加してみる

たとえば「スライダーが中央の50」のケースを足したいときは、`GetCases` の一覧に1行追加するだけです。

```csharp
FormValuesTestCase.RoundTrip("REP-SLIDER-MID", "代表値", Base with { Slider = 50 }),
```

**テストメソッド `SubmitAndVerifySavedResult` は1文字も変更していません。** これがパラメータ化の効果です（確認できたら、追加した行は削除して構いません）。

## ここで気になること

ケースが12件になったことで、テスト全体の実行時間が長くなったはずです。各テストの中で毎回 Playwright とブラウザを起動し直しているためです。また、これまでずっと「別ターミナルでサーバーを手動起動する」必要がありました。次の8章では、これらの準備・後片付けを NUnit の仕組みで正しく管理します。

## この章のまとめ

- 1件のテストケースを `FormValuesTestCase` で表し、`RoundTrip` / `ExpectedRejection` で作る
- `FormValuesTestCases.GetCases` にケースを集め、`[TestCaseSource]` でテストメソッドに流し込む
- パターンの追加はデータを1行足すだけで、テストメソッドの修正は不要
