# 10章（発展） UIごとに分かれた複数の送信を検証する

> **この章は発展的な内容で、読むかどうかは自由です。** 9章までで教材の本編は完結しています。
> 「1回で送るべきデータを、UIの部品ごとに何回にも分けて送っているアプリ」をリファクタリングする前に、**今の送信内容をテストで固定しておきたい**人向けの章です。

## この章の前提知識

- 8章: `[OneTimeSetUp]` / `[SetUp]` / `[TearDown]` によるブラウザ・コンテキストの準備と後片付け
- 7章: `[TestCaseSource]` で入力値の組み合わせをデータとして1か所にまとめる考え方
- 6章: `FormValues` 型、`FormTestIds` 定数、`SetFormValuesAsync`（4つの部品にまとめて値を入れる）
- 5章: `RunAndWaitForResponseAsync` で「操作して、その通信の応答を待つ」書き方
- 4章: 期待する表示になるまで待つアサーション（`Assertions.Expect(...)`）

## この章で扱う場面

次のようなアプリを考えます。送信ボタンを押すと、4つの部品の値を**部品ごとに別々の POST** で送っています。

| 送り先 | 送る本文（JSON） |
|---|---|
| `/api/legacy/text` | `{"text":"ダミー太郎"}` |
| `/api/legacy/slider` | `{"slider":42}` |
| `/api/legacy/select` | `{"select":"banana"}` |
| `/api/legacy/radio` | `{"radio":"green"}` |

本来は1回の POST で `{"text":"ダミー太郎","slider":42,"select":"banana","radio":"green"}` を送れば済むので、1回にまとめるリファクタリングをしたい。けれども、書き換えた後に「送る中身が変わっていない」ことを確かめる手段がないと、安心して手を入れられません。

そこで、書き換える**前**に、今の動きをそのまま記録して固定するテストを作ります。このようなテストを **特性テスト**（characterization test）と呼びます。「正しい仕様」ではなく「今こう動いている」を固定するテストで、リファクタリングの安全網として使います。

### 2種類のテストに分ける

この章のいちばん大事な考え方は、**リファクタリングで変わるもの**と**変わってはいけないもの**を、別々のテストで確かめることです。

| | テスト1: 通信の形 | テスト2: データ全体 |
|---|---|---|
| 確かめること | 何件の通信を、どこへ送ったか。1件ずつ、何を送り何が返ったか | 全部の通信の本文をまとめた「送ったデータ全体」が期待どおりか |
| リファクタリングで | **変わる**（4件 → 1件）。失敗したら「形が変わった」合図。新しい形に合わせて書き直す | **変わってはいけない**。書き直さずにそのまま合格し続けることが、リファクタリング成功の条件（本文の形そのものを変える場合の扱いは「自分のプロジェクトで使うとき」を参照） |

1つのテストで両方を確かめてしまうと、リファクタリング後にテストが失敗したとき、「形が変わっただけ（予定どおり）」なのか「データが壊れた（不具合）」なのかを見分けられません。

### 送信と受信の両方を記録する

Playwright では、ページで起きた通信の応答を `page.Response` イベントで受け取れます。応答（`IResponse`）からは、

- `response.Request.Url` / `response.Request.Method` / `response.Request.PostData`: 送った先・メソッド・本文
- `response.Status` / `await response.TextAsync()`: 返ってきた状態コード・本文

を読み取れます。5章の `RunAndWaitForResponseAsync` は「1件の応答を待つ」道具でしたが、この章では「ある操作の間に起きた通信を**全部**記録する」ために、イベントを使います。

記録できるのは「応答が返ってきた通信」です。下で使う `RouteAsync`（偽の応答を返す機能）で応答した通信にも、本物のサーバーの応答と同じように `page.Response` イベントが起きます。一方、応答が返らずに失敗した通信（接続できなかった等）は `page.Response` には現れず、記録されません（そのような通信は `page.RequestFailed` という別のイベントで知ることができます）。

## 練習用のページについて

このリポジトリのサンプルアプリは、すでに1回の POST で送る作りになっています。そこでこの章では、「部品ごとに送る」練習用ページを**テストコードの中に**用意します。

- 本物のサーバーは使いません。Playwright の `RouteAsync`（ブラウザの通信を横取りして、代わりに応答を返す機能）で、ページの HTML と偽のサーバーの返事をテストコードから返します。
- ページの部品と `data-testid` はサンプルアプリと同じなので、6章のヘルパー関数（`SetFormValuesAsync` など）がそのまま使えます。
- 通信先は `http://legacy-practice.test` です。`.test` は「実在しないテスト用」として予約されたドメイン名で、横取りしているので外部へは一切通信しません。**サンプルアプリのサーバーを起動する必要もありません。**

自分のプロジェクトで使うときは、この練習用ページの部分だけを捨てて、本物のアプリを開くように変えます（後の「自分のプロジェクトで使うとき」で説明します）。

## 手順1: 練習用ページ（偽のサーバー）

学習用プロジェクトに `LegacyPracticeSite.cs` を作ります。**このファイルは練習のための道具**なので、中の HTML・JavaScript を細かく読む必要はありません。送信ボタンの処理（`await Promise.all([...]);` の文）が「部品ごとに別々の POST を同時に送っている」ことだけ確認してください。

```csharp
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

// 練習用の「UIごとに分けて送信する」ページと、その通信相手（偽のサーバー）。
// 本物のサーバーは使わず、Playwright の RouteAsync（通信の横取り）で、ブラウザの中だけで完結させる。
// 自分のプロジェクトでは、このクラスは使わず、本物のアプリの URL を開く
public static class LegacyPracticeSite
{
    // .test は「実在しない、テスト用」として予約されているドメイン名。
    // 下の RouteAsync がすべての通信に応答するので、外部へは一切通信しない
    public const string BaseUrl = "http://legacy-practice.test";

    // UIごとの送信先はすべてこのパスで始まる（/api/legacy/text など）
    public const string ApiPathPrefix = "/api/legacy/";

    // page で BaseUrl 配下へ通信したとき、本物のサーバーの代わりに応答する仕組みを登録する
    public static async Task InstallAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await page.RouteAsync(BaseUrl + "/**", async route =>
        {
            var request = route.Request;
            var path = new Uri(request.Url).AbsolutePath;

            if (request.Method == "GET" && path == "/")
            {
                // ページそのもの（HTML）を返す
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "text/html; charset=utf-8",
                    Body = PageHtml,
                });
            }
            else if (request.Method == "POST" && path.StartsWith(ApiPathPrefix, StringComparison.Ordinal))
            {
                // 偽のサーバーの返事: どの項目を受け取ったか（URL の最後の部分）と、保存できたか
                var field = path[ApiPathPrefix.Length..];
                var body = new JsonObject { ["field"] = field, ["saved"] = true };
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = body.ToJsonString(),
                });
            }
            else
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
            }
        });
    }

    // 練習用ページ。部品と data-testid はサンプルアプリと同じなので、6章のヘルパー関数がそのまま使える。
    // 送信ボタンを押すと、4つの部品の値を「1つずつ別々の POST」で送る（これがリファクタリング前の問題の形）
    private const string PageHtml = """
        <!doctype html>
        <html lang="ja">
        <head><meta charset="utf-8"><title>分割送信の練習ページ</title></head>
        <body>
          <input type="text" data-testid="text-input">
          <input type="range" min="0" max="100" value="50" data-testid="slider">
          <select data-testid="select">
            <option value="apple">りんご</option>
            <option value="banana">バナナ</option>
            <option value="cherry">さくらんぼ</option>
          </select>
          <label><input type="radio" name="color" value="red" data-testid="radio-option"> 赤</label>
          <label><input type="radio" name="color" value="green" data-testid="radio-option"> 緑</label>
          <label><input type="radio" name="color" value="blue" data-testid="radio-option"> 青</label>
          <button type="button" data-testid="submit-button">送信</button>
          <p data-testid="message-area"></p>
          <script>
            const byTestId = (id) => document.querySelector(`[data-testid="${id}"]`);

            async function post(path, body) {
              const response = await fetch(path, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body),
              });
              if (!response.ok) throw new Error(path);
            }

            byTestId('submit-button').addEventListener('click', async () => {
              const message = byTestId('message-area');
              message.textContent = '送信中…';
              const checked = document.querySelector('[data-testid="radio-option"]:checked');
              try {
                // UIごとに別々の POST を、同時に送っている
                await Promise.all([
                  post('/api/legacy/text', { text: byTestId('text-input').value }),
                  post('/api/legacy/slider', { slider: Number(byTestId('slider').value) }),
                  post('/api/legacy/select', { select: byTestId('select').value }),
                  post('/api/legacy/radio', { radio: checked ? checked.value : null }),
                ]);
                message.textContent = '保存しました。';
              } catch {
                message.textContent = '保存に失敗しました。';
              }
            });
          </script>
        </body>
        </html>
        """;
}
```

## 手順2: 通信を記録する道具

`RequestRecorder.cs` を作ります。これは練習用ではなく、**自分のプロジェクトでもそのまま使える道具**です。

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace MyFirstPlaywrightTests;

// 1回の通信で「送ったもの」と「受け取ったもの」をまとめた記録
public sealed record CapturedExchange(
    string Path,            // 送り先 URL のパス部分（例: /api/legacy/text）
    string Method,          // HTTP メソッド（POST など）
    JsonNode? RequestBody,  // 送った本文（JSON）。本文がなければ null
    int Status,             // 返ってきた状態コード（200 など）
    JsonNode? ResponseBody  // 返ってきた本文（JSON）。本文がなければ null
);

// ページで起きた通信のうち、パスが pathPrefix で始まるものを記録する。
// new した時点から記録を始め、Dispose（using の終わり）で記録をやめる。
// 自分のプロジェクトでも、このクラスはそのまま使える（pathPrefix を本物の API に合わせる）
public sealed class RequestRecorder : IDisposable
{
    private readonly IPage _page;
    private readonly string _pathPrefix;
    private readonly List<IResponse> _responses = new();

    public RequestRecorder(IPage page, string pathPrefix)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pathPrefix);
        _page = page;
        _pathPrefix = pathPrefix;
        // 「応答が返ってきたら OnResponse を呼んで」と登録する。
        // 送信ボタンを押す「前」に登録しておかないと、先に返った応答を取りこぼす
        _page.Response += OnResponse;
    }

    public void Dispose() => _page.Response -= OnResponse;

    // 記録した通信を、送った本文・受け取った本文を JSON として読み取った形で返す
    public async Task<IReadOnlyList<CapturedExchange>> GetExchangesAsync()
    {
        IResponse[] responses;
        lock (_responses) responses = _responses.ToArray();

        var exchanges = new List<CapturedExchange>();
        foreach (var response in responses)
        {
            var request = response.Request;
            var responseText = await response.TextAsync();
            exchanges.Add(new CapturedExchange(
                Path: new Uri(request.Url).AbsolutePath,
                Method: request.Method,
                RequestBody: ParseJsonOrNull(request.PostData),
                Status: response.Status,
                ResponseBody: ParseJsonOrNull(responseText)));
        }
        return exchanges;
    }

    // JSON を、失敗メッセージで読みやすい文字列にする（日本語を \uXXXX にしない）。
    // UnsafeRelaxedJsonEscaping は「HTML に埋め込むと危険な文字もそのまま出す」設定。
    // ここではテストの失敗メッセージに表示するだけで、HTML には埋め込まないので問題ない
    public static string ToDisplay(JsonNode? node) =>
        node?.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "null";

    private void OnResponse(object? sender, IResponse response)
    {
        if (!new Uri(response.Url).AbsolutePath.StartsWith(_pathPrefix, StringComparison.Ordinal)) return;
        // 応答は Playwright 側の処理から届くので、lock で「読んでいる最中の追加」を防ぐ
        lock (_responses) _responses.Add(response);
    }

    private static JsonNode? ParseJsonOrNull(string? text) =>
        string.IsNullOrEmpty(text) ? null : JsonNode.Parse(text);
}
```

ポイント:

- **記録は操作の前に始める**: `new RequestRecorder(...)` の時点で記録が始まります。送信ボタンを押してから作ると、先に返ってきた応答を取りこぼします。
- **`using` で記録を止める**: 記録を止め忘れると、次の操作の通信まで記録し続けてしまいます。`using var recorder = ...` と書くと、変数の有効範囲を抜けるときに自動で `Dispose`（= 記録の停止）が呼ばれます。
- **パスで絞り込む**: 画像や CSS など、関係のない通信は記録しません。

## 手順3: テストを書く

`SplitRequestTests.cs` を作ります。

```csharp
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static MyFirstPlaywrightTests.FormPageHelpers;

namespace MyFirstPlaywrightTests;

[TestFixture]
public sealed class SplitRequestTests
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        // 練習用ページは偽のサーバーで動くので、8章のサーバー自動起動は不要
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        _context = await _browser!.NewContextAsync();
        _page = await _context.NewPageAsync();
        // 自分のプロジェクトでは、この2行の代わりに本物のアプリの URL を開く
        await LegacyPracticeSite.InstallAsync(_page);
        await _page.GotoAsync(LegacyPracticeSite.BaseUrl + "/");
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        if (_context is null) return;
        await _context.CloseAsync();
        _context = null;
        _page = null;
    }

    // ---- テスト1: 今の通信の「形」をそのまま記録して固定する ----
    // UIごとに1件ずつ、どこへ・何を送り・何が返ってきたか
    [Test]
    public async Task Submit_SendsOneRequestPerUi()
    {
        await SetFormValuesAsync(_page!, new FormValues("ダミー太郎", 42, "banana", "green"));

        var exchanges = await SubmitAndCaptureAsync();

        // 1. 件数と送り先: 4つの部品について1件ずつ（多すぎも少なすぎもしない）。
        //    同時に送っているので、届く順番は毎回同じとは限らない → 順番は問わない
        Assert.That(exchanges.Select(e => e.Path), Is.EquivalentTo(new[]
        {
            "/api/legacy/text", "/api/legacy/slider", "/api/legacy/select", "/api/legacy/radio",
        }));

        // 2. 1件ずつの中身: 送り先ごとに「送った本文」と「返ってきた本文」を比べる。
        //    期待値は、実際の通信を見て書き写した JSON（"""...""" は中の " をそのまま書ける文字列）
        var expected = new Dictionary<string, (string Sent, string Received)>
        {
            ["/api/legacy/text"] = ("""{"text":"ダミー太郎"}""", """{"field":"text","saved":true}"""),
            ["/api/legacy/slider"] = ("""{"slider":42}""", """{"field":"slider","saved":true}"""),
            ["/api/legacy/select"] = ("""{"select":"banana"}""", """{"field":"select","saved":true}"""),
            ["/api/legacy/radio"] = ("""{"radio":"green"}""", """{"field":"radio","saved":true}"""),
        };

        // Assert.Multiple: 途中で食い違いが見つかっても止まらず、すべての食い違いをまとめて報告する
        Assert.Multiple(() =>
        {
            foreach (var exchange in exchanges)
            {
                var (sent, received) = expected[exchange.Path];
                Assert.That(exchange.Method, Is.EqualTo("POST"), $"{exchange.Path} のメソッド");
                Assert.That(exchange.Status, Is.EqualTo(200), $"{exchange.Path} の状態コード");
                AssertJsonEqual(exchange.RequestBody, sent, $"{exchange.Path} へ送った本文");
                AssertJsonEqual(exchange.ResponseBody, received, $"{exchange.Path} から返った本文");
            }
        });
    }

    // ---- テスト2: 送ったデータを「全体として」確かめる ----
    // 何回に分けて送ったかは問わず、全部の本文を1つにまとめて期待値と比べる。
    // リファクタリングで1回の送信にまとめた後も、このテストはそのまま使える
    [Test]
    [TestCaseSource(nameof(SentDataCases))]
    public async Task Submit_SendsExpectedDataAsAWhole(FormValues input)
    {
        await SetFormValuesAsync(_page!, input);

        var exchanges = await SubmitAndCaptureAsync();

        Assert.That(exchanges.Select(e => e.Status), Is.All.EqualTo(200), "すべての送信が成功したこと");
        AssertJsonEqual(MergeRequestBodies(exchanges), ToExpectedSentData(input), "送ったデータ全体");
    }

    // 7章と同じく、入力値の組み合わせはデータとして1か所にまとめる
    private static readonly FormValues[] SentDataCases =
    {
        new("ダミー太郎", 42, "banana", "green"),
        new("", 0, "cherry", null),            // 空文字・最小値・ラジオ未選択
        new("ダミー花子", 100, "apple", "blue"), // 最大値
    };

    // 入力値から「送られるはずのデータ全体」を作る。ここがリファクタリング前後で変わらない約束
    private static JsonObject ToExpectedSentData(FormValues input) => new()
    {
        ["text"] = input.Text,
        ["slider"] = input.Slider,
        ["select"] = input.Select,
        ["radio"] = input.Radio,
    };

    // 送信ボタンを押し、アプリが「保存しました。」と表示するまで待って、その間の通信を返す
    private async Task<IReadOnlyList<CapturedExchange>> SubmitAndCaptureAsync()
    {
        // using var: このメソッドを抜けるときに自動で Dispose され、記録が止まる
        using var recorder = new RequestRecorder(_page!, LegacyPracticeSite.ApiPathPrefix);

        await _page!.GetByTestId(FormTestIds.SubmitButton).ClickAsync();
        // アプリは4件すべての応答を受け取ってから「保存しました。」を表示する。
        // これを「送信がすべて終わった合図」として待つ（押した直後は「送信中…」になるので、
        // 2回続けて送信しても前回の表示を見て通り過ぎることはない）
        await Assertions.Expect(_page.GetByTestId(FormTestIds.MessageArea)).ToHaveTextAsync("保存しました。");

        return await recorder.GetExchangesAsync();
    }

    // 複数の送信の本文（{"text":...} や {"slider":...}）を、1つの JSON にまとめる
    private static JsonObject MergeRequestBodies(IEnumerable<CapturedExchange> exchanges)
    {
        var merged = new JsonObject();
        foreach (var exchange in exchanges)
        {
            if (exchange.RequestBody is not JsonObject body)
            {
                // Assert.Fail は、その場でテストを失敗させて止める（この後の処理は実行されない）。
                // ただしコンパイラはそれを知らず、下の body を「値が入っていないかもしれない」と扱うので、
                // continue（次の繰り返しへ進む）を書いて、この先へは進まないことをコンパイラに示している
                Assert.Fail($"{exchange.Path} の本文が JSON オブジェクトではありません: {RequestRecorder.ToDisplay(exchange.RequestBody)}");
                continue;
            }
            foreach (var (name, value) in body)
            {
                // 同じ項目が2回送られていたら、それも見逃さない
                if (merged.ContainsKey(name)) Assert.Fail($"項目 {name} が複数回送られています（{exchange.Path}）");
                merged[name] = value?.DeepClone(); // 元の JSON から切り離したコピーを入れる
            }
        }
        return merged;
    }

    // JSON の中身が等しいかを比べる（項目の並び順や空白の違いは無視される）
    private static void AssertJsonEqual(JsonNode? actual, string expectedJson, string what) =>
        AssertJsonEqual(actual, JsonNode.Parse(expectedJson), what);

    private static void AssertJsonEqual(JsonNode? actual, JsonNode? expected, string what)
    {
        Assert.That(JsonNode.DeepEquals(actual, expected), Is.True,
            $"{what} が違います。\n  期待: {RequestRecorder.ToDisplay(expected)}\n  実際: {RequestRecorder.ToDisplay(actual)}");
    }
}
```

`dotnet test` を実行し、`SplitRequestTests` の4件（テスト1が1件、テスト2が3ケース）が合格することを確認してください。サーバーの起動は不要です。

### いつ「全部送り終わった」と判断するか

`SubmitAndCaptureAsync` は、アプリが「保存しました。」と表示するまで待ってから記録を取り出しています。練習用ページは4件すべての応答を受け取ってから「保存しました。」を表示するので、これが「送信がすべて終わった」合図になります。

この待ち方をしないと、たとえばボタンを押した直後に記録を取り出して「2件しか記録されていない」と誤って失敗することがあります。4章から一貫している「決め打ちの時間で待たず、終わった合図を待つ」考え方です。

アプリに完了の表示がない場合は、「来るはずの応答」を1件ずつ待つ方法があります。待ち始めるのはクリックの**前**です。クリックの後で待ち始めると、応答がすでに返ってしまっていた場合に取りこぼし、来ない応答を待ち続けてしまうためです（5章の `RunAndWaitForResponseAsync` が「操作」と「待つ条件」を1つのメソッドにまとめて受け取るのも、待ち始めを操作より前にするためです）。

```csharp
using var recorder = new RequestRecorder(_page!, LegacyPracticeSite.ApiPathPrefix);
string[] expectedPaths = { "/api/legacy/text", "/api/legacy/slider", "/api/legacy/select", "/api/legacy/radio" };

// クリックの前に、4件それぞれの応答を待ち始めておく
var waits = expectedPaths
    .Select(path => _page!.WaitForResponseAsync(r => new Uri(r.Url).AbsolutePath == path))
    .ToArray();
await _page!.GetByTestId(FormTestIds.SubmitButton).ClickAsync();
await Task.WhenAll(waits); // 4件すべての応答が返るまで待つ

var exchanges = await recorder.GetExchangesAsync();
```

ただしこの方法では、4件の応答が返った**後**に余計な5件目が送られた場合、それが記録される前にテストが先へ進むことがあります。「余計な通信がないこと」まで確かめたい場合は、アプリの完了表示を待つ方法の方が確実です。

自分のプロジェクトで、完了表示を待つ方法を使っているのに記録の件数がときどき足りない（最後の1件の記録が完了表示より遅れて届く）場合は、2つの方法を組み合わせてください。クリックの**前**に、上のコードと同じように来るはずの応答を待ち始めておき、クリックの後で完了表示と `Task.WhenAll(waits)` の両方を待ってから、記録を取り出します（完了表示の後で待ち始めると、応答はもう返っているので取りこぼします）。

### 期待値の決め方

特性テストの期待値は、「仕様書に書いてある値」ではなく**今のアプリが実際に送っている値**です。初めて書くときは、ブラウザの開発者ツール（F12 → ネットワーク）で実際の通信を見たり、`GetExchangesAsync` の結果を `TestContext.Progress.WriteLine(RequestRecorder.ToDisplay(...))`（8章で使った、実行中にすぐコンソールへ表示する書き方）で書き出したりして、観察した値を期待値に書き写します。

書き写すときに「これはおかしいのでは」という値を見つけても、**リファクタリングと同時に直さない**でください。まず今の動きのまま固定し、リファクタリングが終わってテストが合格してから、別の変更として直します。「リファクタリングで壊れた」のか「わざと直した」のかが混ざらないようにするためです。

## 手順4: テストが本当に不具合を見つけられるか確かめる

合格したテストが「何を変えても合格してしまう」テストでは、安全網になりません。一度、わざとアプリを壊して、テストが失敗することを確かめます。

`LegacyPracticeSite.cs` の JavaScript で、スライダーの値を数値に変換している `Number(...)` を外してみます。

```js
// 変更前: slider: Number(byTestId('slider').value)   → {"slider":42}
// 変更後: slider: byTestId('slider').value           → {"slider":"42"}（文字列になる）
post('/api/legacy/slider', { slider: byTestId('slider').value }),
```

`dotnet test` を実行すると、4件すべてが失敗し、次のように「どこが・どう違うか」が表示されます（出力の一部）。

```text
  失敗 Submit_SendsOneRequestPerUi
     /api/legacy/slider へ送った本文 が違います。
  期待: {"slider":42}
  実際: {"slider":"42"}
  失敗 Submit_SendsExpectedDataAsAWhole(FormValues { Text = ダミー太郎, Slider = 42, Select = banana, Radio = green })
     送ったデータ全体 が違います。
  期待: {"text":"ダミー太郎","slider":42,"select":"banana","radio":"green"}
  実際: {"text":"ダミー太郎","slider":"42","select":"banana","radio":"green"}
```

`42`（数値）と `"42"`（文字列）のような、画面を見ただけでは気付けない違いも検出できています。サーバー側の受け取り方によっては不具合になる違いなので、通信の中身を確かめる意味がよく分かる例です。確認できたら、`Number(...)` を元に戻してください。

## 手順5: リファクタリングした後

アプリを「1回の POST で送る」形に書き換えた後、テストはどうなるでしょうか。練習用ページの `await Promise.all([...]);` の文全体（`await Promise.all([` から `]);` まで）を、次の1回の `post` 呼び出しに置き換えて試せます。

```js
await post('/api/legacy/form', {
  text: byTestId('text-input').value,
  slider: Number(byTestId('slider').value),
  select: byTestId('select').value,
  radio: checked ? checked.value : null,
});
```

結果は次のとおりです。

| テスト | 結果 | 意味 |
|---|---|---|
| テスト1 `Submit_SendsOneRequestPerUi` | 失敗（`Expected: equivalent to < "/api/legacy/text", ... >  But was: < "/api/legacy/form" >`） | 予定どおり「通信の形」が変わった合図 |
| テスト2 `Submit_SendsExpectedDataAsAWhole`（3ケース） | **書き直さずに合格** | 送るデータの中身は変わっていない = リファクタリング成功 |

テスト2は通信が何件でも、全部の本文をまとめてから比べているので、4件が1件になっても同じ期待値で合格します。これが、2種類のテストに分けた理由です。

テスト1は、新しい形に合わせて書き直します。たとえば「1件だけ、この送り先に送る」ことを確かめる形です。あわせて、後半の期待値の表（`expected`）も、新しい送り先（`/api/legacy/form`）の1件分に書き換えてください（古い送り先のままだと、表に無いキーを引いてエラーになります）。

```csharp
Assert.That(exchanges.Select(e => e.Path), Is.EqualTo(new[] { "/api/legacy/form" }));
```

なお、リファクタリングで送り先のパスの始まりまで変わる場合（例: `/api/legacy/` から `/api/v2/` へ）は、`RequestRecorder` に渡すパスも合わせて変えてください。

## 自分のプロジェクトで使うとき

| やること | 内容 |
|---|---|
| 練習用ページを外す | `LegacyPracticeSite.cs` は使わず、`[SetUp]` の `InstallAsync` と `GotoAsync` の2行を、本物のアプリの URL を開く1行に変える。サーバーの起動は8章の方法か手動で行う |
| 記録するパスを合わせる | `RequestRecorder` に渡すパスを、本物の API の送り先に合わせる |
| 完了の合図を合わせる | 「保存しました。」の待ち方を、本物のアプリの完了表示に合わせる。表示がなければ、上の「来るはずの応答を1件ずつ待つ」方法を使う |
| 同じ送り先へ何度も送っている場合 | 部品ごとに送り先が違うのではなく、同じ送り先へ本文だけ変えて何度も送る作りでは、パスを表のキーにしたテスト1や、パスで待つ方法はそのまま使えない。テスト1は、本文の一覧（`RequestRecorder.ToDisplay` で文字列にしたもの）を `Is.EquivalentTo` で比べる形にする。文字列で比べるので、`JsonNode.DeepEquals` と違って項目の並び順まで一致している必要がある（観察した文字列をそのまま期待値に書き写す）。送る順番に意味がある場合は `Is.EqualTo` で順番まで確かめる。完了を待つには、アプリの完了表示を使う |
| どの送信にも共通の項目がある場合 | 分割された各送信が、ユーザーID・フォームID・CSRFトークンなどの共通の項目を毎回含んでいると、`MergeRequestBodies` は「複数回送られています」で失敗する。まとめる前に共通の項目を取り除き、「全件で同じ値か」は別のアサーションで確かめる |
| リファクタリングで本文の形が変わる場合 | 1回にまとめた本文が `{"form":{...}}` のような入れ子になる、項目名が変わる、などの場合は、テスト2も「書き直さずに合格」にはならない。そのときも期待値（`ToExpectedSentData`）は変えず、比べる前に実際の本文を「分割時の本文をまとめたもの」と同じ形にそろえる変換を1か所に用意する。期待値を変えないことで、「中身が変わっていない」ことを確かめ続けられる |
| 本文が JSON でない場合 | フォーム形式（`a=1&b=2`）などで送っている場合、`JsonNode.Parse` は失敗する。`request.PostData` は送った本文をそのままの文字列で返すので、その形式に合わせて読み取る処理に置き換える。**応答の本文**も同じで、`OK` のような文字列や HTML のエラーページが返ると失敗する（空の本文は null として扱う）。また、送信と同時に別のページへ移動する作りでは、応答の本文を読めないことがある |
| 保存先への影響 | 本物のサーバーに送ると、実際にデータが保存される。テスト用の環境・テスト用のデータベースで実行する |
| 入れる値 | 失敗メッセージには送った本文がそのまま表示され、テストの実行ログに残る。**実在の個人情報・パスワード・カード番号などは入れず、ダミー値だけを使う** |

## この章のまとめ

- リファクタリングの前に、今の動きを固定する「特性テスト」を作る
- 「通信の形」（何件・どこへ）と「データ全体」（何を送ったか）を別々のテストで確かめる。リファクタリング後、前者は書き直し、後者はそのまま合格し続けることを確かめる
- `page.Response` イベントで、操作の間に起きた通信を全部記録できる。記録は操作の**前**に始め、`using` で止める
- 送った本文は `request.PostData`、返った本文は `response.TextAsync()` で読み、`JsonNode.DeepEquals` で中身を比べる（項目の並び順や空白の違いは無視される）
- 「全部送り終わった」は、決め打ちの時間ではなく、アプリの完了表示や来るはずの応答を待って判断する
- テストを書いたら、わざとアプリを壊して失敗することを確かめる

## この章で出てきた C# の書き方

| 書き方 | 意味 |
|---|---|
| `"""..."""` | 生文字列リテラル。中の `"` や改行をそのまま書ける（JSON や HTML を書くのに便利） |
| `path[ApiPathPrefix.Length..]` | 文字列の「指定の位置から最後まで」を取り出す（`..` は範囲の指定） |
| `new RouteFulfillOptions { Status = 200, ... }` | オブジェクトを作りながら、プロパティに値を入れる |
| `public sealed class RequestRecorder : IDisposable` | `: IDisposable` は「`Dispose`（後片付け）の仕組みを持つ」という宣言。`using` と組み合わせて使う |
| `using var recorder = ...;` | 変数の有効範囲（ここではメソッド）を抜けるときに、自動で `Dispose` を呼ぶ |
| `_page.Response -= OnResponse;` | `+=` で登録した「〜が起きたらこの処理」を取り消す |
| `Task<IReadOnlyList<CapturedExchange>>` | 「後で `CapturedExchange` の一覧（読み取り専用）が得られる」非同期処理の戻り値 |
| `new Dictionary<string, (string Sent, string Received)> { [キー] = (値1, 値2) }` | キーから値を引ける表。値の `(string Sent, string Received)` は2つの値の組（タプル） |
| `var (sent, received) = expected[exchange.Path];` | 表からキーで値を取り出し、組を2つの変数に分けて受け取る |
| `foreach (var (name, value) in body)` | JSON オブジェクトの項目を、名前と値に分けて1つずつ取り出す |
| `exchange.RequestBody is not JsonObject body` | 「`JsonObject` でなければ」。`JsonObject` だった場合は `body` という名前で使える |
| `Is.EquivalentTo(...)` | NUnit のアサーション。並び順を問わず、同じ要素がそろっているか |
| `Is.All.EqualTo(200)` | NUnit のアサーション。すべての要素が 200 か |
| `Assert.Multiple(() => { ... })` | 中のアサーションが失敗しても止まらず、すべての失敗をまとめて報告する |
| `Assert.Fail("理由")` | その場でテストを失敗させて止める（`Assert.Multiple` の外では、後の処理は実行されない） |
| `[TestCaseSource(nameof(SentDataCases))]` | 引数が1つの形。同じクラスの中にある `SentDataCases`（ここでは static なフィールド）からケースを読む。7章の `typeof(クラス), nameof(メソッド)` の形は、別のクラスのメソッドから読む書き方 |
| `AssertJsonEqual(..., string expectedJson, ...)` と `AssertJsonEqual(..., JsonNode? expected, ...)` | 同じ名前で引数の型だけが違うメソッドを複数書ける（オーバーロード）。呼び出し時に渡した値の型で、どちらが使われるかが決まる |
| `new CapturedExchange(Path: ..., Method: ...)` | 名前付き引数。どの値がどの引数かを、名前を書いて分かりやすくする |
| `_page.Response += OnResponse;` | 8章の `+= (_, e) => { ... }` と同じ登録を、ラムダ式ではなくメソッドの名前で行う。メソッド名で登録すると、`-=` で同じものを取り消せる |
| `.ToArray()` / `Task.WhenAll(waits)` | 一覧を配列にする / 複数の非同期処理がすべて終わるまで待つ |
| `value?.DeepClone()` | `value` が null でなければ、JSON のコピーを作る（null なら null のまま） |
| `JsonNode.Parse(文字列)` / `JsonNode.DeepEquals(a, b)` | JSON の文字列を読み取る / 2つの JSON の中身が等しいかを比べる |
