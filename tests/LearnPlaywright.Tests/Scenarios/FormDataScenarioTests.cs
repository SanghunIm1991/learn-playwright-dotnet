using System.Diagnostics;
using System.Text;
using LearnPlaywright.Tests.TestData;
using Microsoft.Playwright;
using static LearnPlaywright.Tests.TestData.FormPageHelpers;

namespace LearnPlaywright.Tests.Scenarios;

/// <summary>テスト実行時のサーバー起動・接続設定。</summary>
public static class ServerConfig
{
    public const string BaseUrl = "http://localhost:5080";
    public const string ServerProjectPathPlaceholder = "src/LearnPlaywright.Server";
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    // 環境変数 LEARNPLAYWRIGHT_HEADED=1 でブラウザ画面を表示して実行する（デバッグ用）
    public static bool Headed => Environment.GetEnvironmentVariable("LEARNPLAYWRIGHT_HEADED") == "1";
}

/// <summary>COMP-05 Playwrightテストシナリオ（FUNC-43〜48）。</summary>
[TestFixture]
public sealed class FormDataScenarioTests
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private Process? _serverProcess;
    private string? _dataDirectory;
    private readonly StringBuilder _serverLog = new();

    private IBrowserContext? _context;
    private IPage? _page;

    // FUNC-43
    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        // 保存データはテスト専用の一時ディレクトリへ書き込み、開発用の保存先を汚さない
        _dataDirectory = Path.Combine(Path.GetTempPath(), "LearnPlaywright-e2e-" + Guid.NewGuid().ToString("N"));

        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // ホストはlocalhost固定のままポートのみ明示指定する（NFR-02。ワイルドカード指定は禁止）
        foreach (var arg in new[] { "run", "--project", FindServerProjectPath(), "--no-build", "--no-launch-profile", "--urls", ServerConfig.BaseUrl })
        {
            startInfo.ArgumentList.Add(arg);
        }
        startInfo.Environment["DataDirectory"] = _dataDirectory;
        startInfo.Environment.Remove("ASPNETCORE_URLS");

        _serverProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the server process.");
        _serverProcess.OutputDataReceived += (_, e) => { lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _serverProcess.ErrorDataReceived += (_, e) => { lock (_serverLog) _serverLog.AppendLine(e.Data); };
        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        await WaitForServerAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = !ServerConfig.Headed });
    }

    // FUNC-44
    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        try
        {
            if (_browser is not null) await _browser.CloseAsync();
            _playwright?.Dispose();
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine("Browser cleanup failed: " + ex.Message);
        }

        try
        {
            if (_serverProcess is { HasExited: false }) _serverProcess.Kill(entireProcessTree: true);
            _serverProcess?.Dispose();
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine("Server cleanup failed: " + ex.Message);
        }

        try
        {
            if (_dataDirectory is not null && Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine("Data directory cleanup failed: " + ex.Message);
        }
    }

    // FUNC-45: テストごとに新しいコンテキスト（Cookieを共有しない）を作る
    [SetUp]
    public async Task SetUpAsync()
    {
        _context = await _browser!.NewContextAsync();
        _page = await _context.NewPageAsync();
        await _page.GotoAsync(ServerConfig.BaseUrl);
    }

    // FUNC-46
    [TearDown]
    public async Task TearDownAsync()
    {
        if (_context is null) return;
        await _context.CloseAsync();
        _context = null;
        _page = null;
    }

    // FUNC-47: 入力→送信→メッセージ種別による受理/拒否の検証
    [Test]
    [TestCaseSource(typeof(FormValuesTestCases), nameof(FormValuesTestCases.GetCases))]
    public async Task SubmitAndVerifySavedResult(FormValuesTestCase testCase)
    {
        await SetFormValuesAsync(_page!, testCase.Input);
        await ClickSubmitButtonAsync(_page!);
        MessageState message = await GetMessageAsync(_page!);

        Assert.That(message.Type, Is.EqualTo(testCase.ExpectedSubmitSuccess ? "success" : "error"));
    }

    // FUNC-48: 送信→再読み込み→読み込み→復元値（または未保存メッセージ）の検証
    [Test]
    [TestCaseSource(typeof(FormValuesTestCases), nameof(FormValuesTestCases.GetCases))]
    public async Task LoadAndVerifyRestoredValues(FormValuesTestCase testCase)
    {
        await SetFormValuesAsync(_page!, testCase.Input);
        await ClickSubmitButtonAsync(_page!);
        await _page!.ReloadAsync();
        await ClickLoadButtonAsync(_page!);

        if (testCase.ExpectedRestored is not null)
        {
            FormValues restored = await GetFormValuesAsync(_page!);
            Assert.Multiple(() =>
            {
                Assert.That(restored.Text, Is.EqualTo(testCase.ExpectedRestored.Text));
                Assert.That(restored.Slider, Is.EqualTo(testCase.ExpectedRestored.Slider).Within(1e-9));
                Assert.That(restored.Select, Is.EqualTo(testCase.ExpectedRestored.Select));
                Assert.That(restored.Radio, Is.EqualTo(testCase.ExpectedRestored.Radio));
            });
        }
        else
        {
            MessageState message = await GetMessageAsync(_page!);
            Assert.That(message.Type, Is.EqualTo("info"));
        }
    }

    // 起動完了待ち: GET /api/form-data が200/404を返すまでポーリングする
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
            catch (HttpRequestException)
            {
                // まだ待ち受けていない
            }
            catch (TaskCanceledException)
            {
                // タイムアウト（起動途中）
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Server did not start within the timeout.\n" + GetServerLog());
    }

    private string GetServerLog()
    {
        lock (_serverLog) return _serverLog.ToString();
    }

    // テストアセンブリの場所から上位へ辿り、global.json のあるリポジトリルートを基準にサーバープロジェクトを特定する
    private static string FindServerProjectPath()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }
        if (dir is null) throw new InvalidOperationException("Repository root (global.json) was not found.");
        return Path.Combine(dir.FullName, ServerConfig.ServerProjectPathPlaceholder);
    }
}
