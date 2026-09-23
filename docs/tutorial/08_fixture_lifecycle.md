# 8章 テストフィクスチャのライフサイクル管理

これまでは、各テストの中で Playwright とブラウザを起動し、サーバーは別ターミナルで手動起動していました。この章では、準備（セットアップ）と後片付け（ティアダウン）を NUnit の仕組みで管理し、**サーバーの起動もテストコードが自動で行う**、完成版テストと同じ構成に切り替えます。

## この章の前提知識

- 7章: `[TestCaseSource]` と `FormValuesTestCases.GetCases` で1つのテストメソッドを複数ケース実行できること
- 6章: ヘルパー関数（`SetFormValuesAsync`、`ClickSubmitButtonAsync`、`GetMessageAsync` など）
- 5章: 同じブラウザ内では Cookie が残り、新しく起動したブラウザには Cookie がないこと
- 2章: `Playwright.CreateAsync()` → `LaunchAsync` → `NewPageAsync` → `GotoAsync` の流れ
- 1章: テストの独立性の考え方、サーバー起動待ちはテスト実行側の待機で Playwright の自動待機とは別物であること

## 振り返り: 2章の簡略構成の問題点

2章では流れを追いやすくするため、1つのテストの中で「起動→操作→破棄」を完結させました。しかし7章でケースが12件になると、

- 12回、Playwright とブラウザを起動し直している（遅い）
- テストの前に毎回、人がサーバーを手動起動しなければならない

という問題が目立ってきます。

## NUnit の4つのライフサイクル属性

| 属性 | 実行されるタイミング | この章で行うこと |
|---|---|---|
| `[OneTimeSetUp]` | クラス内の全テストの前に**1回だけ** | サーバー起動、Playwright・ブラウザ起動 |
| `[OneTimeTearDown]` | クラス内の全テストの後に**1回だけ** | ブラウザ・サーバーの停止、一時フォルダ削除 |
| `[SetUp]` | **各テストの前に毎回** | 新しいブラウザコンテキストとページを作る |
| `[TearDown]` | **各テストの後に毎回** | そのコンテキストを閉じる |

### ブラウザは共有、コンテキストはテストごと

`IBrowserContext`（ブラウザコンテキスト）は、ブラウザの中に作る「独立した利用者の部屋」のようなものです。Cookie はコンテキストごとに別々に管理されます。

- 重いブラウザの起動は `[OneTimeSetUp]` で1回だけにして速くする
- 軽いコンテキストは `[SetUp]` で毎回新しく作り、前のテストの Cookie を持ち越さない（1章「テストの独立性」）

これまでの章は「毎回ブラウザを起動し直す」ことで独立性を保っていましたが、ここからは「ブラウザは共有し、コンテキストだけ作り直す」ことで、速さと独立性を両立させます。

## サーバー起動の切り替え（重要）

**この章からは、テストがサーバーを自動で起動します。2〜7章で手動起動していたターミナルでは `Ctrl+C` を押してサーバーを止めておいてください。** 両方が同じポート 5080 を使おうとして衝突し、自動起動が失敗するためです。

自動起動では、テストコードが次のコマンドに相当する処理を子プロセスとして実行します。

```text
dotnet run --project <サーバープロジェクトの場所> --no-build --no-launch-profile --urls http://localhost:5080
```

- `--no-build`: テストのたびにサーバーをビルドし直さない（速くするため）。そのため、**事前にリポジトリのルートで `dotnet build src/LearnPlaywright.Server` を実行してビルドを済ませておく**必要があります。
- `--no-launch-profile`: 開発用の起動設定ファイル（`launchSettings.json`）を使わず、テスト用の指定だけで起動する。
- `--urls`: 待ち受けるURLを明示する（ホストは必ず `localhost`）。

### 2〜7章のテストクラスの扱い

2〜7章で作ったテストクラス（`FirstTests`、`LocatorTests`、`SubmitTests`、`LoadTests`、`HelperTests`、`DataDrivenTests`）は、手動起動のサーバーがある前提で書かれています。自動起動のサーバーは、この章で作る `FixtureTests` クラスのテストを実行している間だけ動くため、それ以外のクラスのテストは接続できずに失敗します。

そこで、これらのクラスには NUnit の `[Ignore]` 属性を付けて、実行対象から外しておきます（ファイルごと削除しても構いません。内容は `FixtureTests` に引き継がれます）。

```csharp
// [Ignore("理由")] を付けたクラスのテストは実行されず、結果に「スキップ」として数えられる
[TestFixture]
[Ignore("手動起動のサーバーが前提の旧レッスン用テストのため")]
public class FirstTests
{
    // （中身はそのまま）
}
```

### サーバープロジェクトの場所の指定

完成版テスト（`tests/LearnPlaywright.Tests`）はリポジトリの中にあるため、テストの実行場所から親フォルダをたどって `global.json` を見つけ、そこを起点にサーバーの場所を決めています（`FindServerProjectPath`）。

学習用プロジェクトはリポジトリの**外**にあるので、この方法は使えません。そこで、サーバープロジェクトの場所を**定数や環境変数で絶対パス指定**します。パスは自分の環境に合わせて書き換えてください。

## テストコード

学習用プロジェクトに `FixtureTests.cs` を作ります。

```csharp
using System.Diagnostics;
using System.Text;
using Microsoft.Playwright;
using static MyFirstPlaywrightTests.FormPageHelpers;

namespace MyFirstPlaywrightTests;

public static class ServerConfig
{
    public const string BaseUrl = "http://localhost:5080";

    // 例: 環境変数 LEARNPLAYWRIGHT_SERVER_PROJECT があればそれを使い、なければ下の定数を使う。
    // 下のパスは例なので、自分のPCでリポジトリを置いた場所に合わせて書き換える
    public static string ServerProjectPath =>
        Environment.GetEnvironmentVariable("LEARNPLAYWRIGHT_SERVER_PROJECT")
        ?? @"C:\path\to\LearnPlaywright\src\LearnPlaywright.Server";

    // この時間を過ぎてもサーバーが応答しなければ、起動失敗とみなす
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
}

[TestFixture]
public sealed class FixtureTests
{
    // クラス全体で共有するもの（OneTimeSetUp で作る）
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private Process? _serverProcess;
    private string? _dataDirectory;
    private readonly StringBuilder _serverLog = new();

    // テストごとに作り直すもの（SetUp で作る）
    private IBrowserContext? _context;
    private IPage? _page;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        // テストで保存されるデータは一時フォルダに書かせ、手動実行時の保存先（App_Data）を汚さない
        _dataDirectory = Path.Combine(Path.GetTempPath(), "LearnPlaywright-e2e-" + Guid.NewGuid().ToString("N"));

        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,           // 余計なウィンドウを出さない
            RedirectStandardOutput = true,   // サーバーのログを受け取り、失敗時の調査に使う
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "run", "--project", ServerConfig.ServerProjectPath, "--no-build", "--no-launch-profile", "--urls", ServerConfig.BaseUrl })
        {
            startInfo.ArgumentList.Add(arg);
        }
        startInfo.Environment["DataDirectory"] = _dataDirectory; // サーバーに保存先を伝える
        startInfo.Environment.Remove("ASPNETCORE_URLS");         // 外部の設定で待ち受け先が変わらないようにする

        _serverProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the server process.");
        _serverProcess.OutputDataReceived += (_, e) => { lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _serverProcess.ErrorDataReceived += (_, e) => { lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        await WaitForServerAsync(); // サーバーが応答するようになるまで待つ

        // Playwright とブラウザは、クラス全体で1回だけ起動する
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        // 後片付けは「1つが失敗しても残りは必ず実行する」よう、それぞれ try で囲む
        try
        {
            if (_browser is not null) await _browser.CloseAsync();
            _playwright?.Dispose();
        }
        catch (Exception ex) { TestContext.Progress.WriteLine("Browser cleanup failed: " + ex.Message); }

        try
        {
            // サーバーと、そこから起動された子プロセスをまとめて止める
            if (_serverProcess is { HasExited: false }) _serverProcess.Kill(entireProcessTree: true);
            _serverProcess?.Dispose();
        }
        catch (Exception ex) { TestContext.Progress.WriteLine("Server cleanup failed: " + ex.Message); }

        try
        {
            if (_dataDirectory is not null && Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (Exception ex) { TestContext.Progress.WriteLine("Data directory cleanup failed: " + ex.Message); }
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        // 共有ブラウザから、このテスト専用のコンテキスト（Cookie も空）を作る
        _context = await _browser!.NewContextAsync();
        _page = await _context.NewPageAsync();
        await _page.GotoAsync(ServerConfig.BaseUrl);
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        if (_context is null) return;
        await _context.CloseAsync(); // コンテキストを閉じると、その中のページと Cookie も消える
        _context = null;
        _page = null;
    }

    // 7章のテストから「起動・ページ表示」の行が消え、本来の手順だけが残る。
    // _page! の ! は「ここでは null でないと分かっている」とコンパイラに伝える記号（SetUp で必ず作られるため）
    [Test]
    [TestCaseSource(typeof(FormValuesTestCases), nameof(FormValuesTestCases.GetCases))]
    public async Task SubmitAndVerifySavedResult(FormValuesTestCase testCase)
    {
        await SetFormValuesAsync(_page!, testCase.Input);
        await ClickSubmitButtonAsync(_page!);
        MessageState message = await GetMessageAsync(_page!);

        Assert.That(message.Type, Is.EqualTo(testCase.ExpectedSubmitSuccess ? "success" : "error"));
    }

    // サーバーの起動完了待ち: GET /api/form-data が 200 か 404 を返せば「応答できる状態」とみなす。
    // これはテスト実行側が HTTP で問い合わせる待機で、Playwright の要素の自動待機とは別の仕組み
    private async Task WaitForServerAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + ServerConfig.StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_serverProcess!.HasExited)
            {
                throw new InvalidOperationException("Server process exited unexpectedly.\n" + GetServerLog());
            }
            try
            {
                using var response = await client.GetAsync(ServerConfig.BaseUrl + "/api/form-data");
                if ((int)response.StatusCode is 200 or 404) return;
            }
            catch (HttpRequestException) { /* まだ待ち受けていない */ }
            catch (TaskCanceledException) { /* 応答が遅い（起動途中） */ }
            // 問い合わせの間隔。「決め打ちで待つ」のではなく、応答があれば即座に抜ける
            await Task.Delay(250);
        }
        throw new TimeoutException("Server did not start within the timeout.\n" + GetServerLog());
    }

    private string GetServerLog()
    {
        lock (_serverLog) return _serverLog.ToString();
    }
}
```

手動起動のサーバーを止め、サーバーのビルドを済ませたうえで `dotnet test` を実行し、`失敗: 0` を確認してください。サーバーの起動待ちが最初に1回入りますが、ブラウザの起動が1回で済むようになった分、ケース1件あたりの時間は短くなります。

## うまくいかないとき

| 症状 | 確認すること |
|---|---|
| `Server process exited unexpectedly` | `ServerProjectPath` が正しいか、サーバーのビルド（`dotnet build src/LearnPlaywright.Server`）を済ませたか |
| `Server did not start within the timeout`、またはテストが自分の意図しないデータで動いている | 手動起動のサーバーが残っていてポート5080が使用中でないか（手動起動のサーバーが残っていると、テストがそちらに接続してしまうこともあります。完成版テストでは、起動前にポートが使用中かを調べて、使用中なら即座に失敗させています） |
| `FixtureTests` 以外のクラスで `net::ERR_CONNECTION_REFUSED` | 2〜7章のテストクラスに `[Ignore]` を付けたか |

## この章のまとめ

- `[OneTimeSetUp]` / `[OneTimeTearDown]` でサーバー・ブラウザをクラス全体で1回だけ起動・停止する
- `[SetUp]` / `[TearDown]` でテストごとに新しいコンテキストを作り、Cookie を持ち越さない
- 以降、サーバーの手動起動は不要（手動起動のターミナルは止めておく）
