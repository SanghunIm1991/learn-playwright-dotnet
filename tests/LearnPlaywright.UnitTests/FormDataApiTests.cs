using System.Text.Json;
using LearnPlaywright.Logic;
using LearnPlaywright.Server;

namespace LearnPlaywright.UnitTests;

/// <summary>COMP-02（FormDataApi）とNFR-02判定処理の単体テスト。UT-02-xx はテスト仕様書のテストID。</summary>
[TestFixture]
public class FormDataApiTests
{
    private const string ValidBody = "{\"text\":\"dummy\",\"slider\":10,\"select\":\"apple\",\"radio\":\"red\"}";

    private sealed class CookieRecorder
    {
        public List<(string Name, string Value, CookieWriteOptions Options)> Calls { get; } = [];
        public void Set(string name, string value, CookieWriteOptions options) => Calls.Add((name, value, options));
    }

    // UT-02-01: Cookie未設定/空文字は新規発行、設定済みはそのまま使う
    [Test]
    public void GetOrIssueUserId_IssuesOnlyWhenMissing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FormDataApi.GetOrIssueUserId(null, () => "new-id"), Is.EqualTo(new UserIdResolution("new-id", true)));
            Assert.That(FormDataApi.GetOrIssueUserId("", () => "new-id"), Is.EqualTo(new UserIdResolution("new-id", true)));
            Assert.That(FormDataApi.GetOrIssueUserId("existing", () => "new-id"), Is.EqualTo(new UserIdResolution("existing", false)));
            Assert.That(FormDataApi.GetOrIssueUserId(null).UserId, Is.Not.Empty);
        });
    }

    // UT-02-02: Cookie付与は lp_user_id / HttpOnly / Path=/ / 1年、userId空なら例外
    [Test]
    public void IssueUserIdCookie_SetsExpectedOptions()
    {
        var recorder = new CookieRecorder();
        FormDataApi.IssueUserIdCookie("abc", recorder.Set);
        Assert.Multiple(() =>
        {
            Assert.That(recorder.Calls, Has.Count.EqualTo(1));
            Assert.That(recorder.Calls[0].Name, Is.EqualTo("lp_user_id"));
            Assert.That(recorder.Calls[0].Options, Is.EqualTo(new CookieWriteOptions(true, "/", 31536000)));
            Assert.Throws<ArgumentException>(() => FormDataApi.IssueUserIdCookie("", recorder.Set));
        });
    }

    // UT-02-03: リクエストボディ解析（正常・radio欠落・構文不正・型不一致・必須欠落）
    [Test]
    public void ParseFormDataRequestBody_Cases()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FormDataApi.ParseFormDataRequestBody(ValidBody).Slider, Is.EqualTo(10));
            Assert.That(FormDataApi.ParseFormDataRequestBody("{\"text\":\"a\",\"slider\":1,\"select\":\"b\"}").Radio, Is.Null);
            Assert.Throws(Is.InstanceOf<JsonException>(), () => FormDataApi.ParseFormDataRequestBody("{bad"));
            Assert.Throws(Is.InstanceOf<JsonException>(), () =>
                FormDataApi.ParseFormDataRequestBody("{\"text\":\"a\",\"slider\":\"10\",\"select\":\"b\"}"));
            Assert.Throws(Is.InstanceOf<JsonException>(), () => FormDataApi.ParseFormDataRequestBody("{\"text\":\"a\",\"select\":\"b\"}"));
        });
    }

    // UT-02-04: POSTのステータス決定（200/400/500）とCookie新規発行
    [Test]
    public void HandlePostFormData_StatusCodes()
    {
        var recorder = new CookieRecorder();
        var ok = FormDataApi.HandlePostFormData(null, ValidBody,
            new PostDeps((_, _) => new SaveResult(true), recorder.Set, () => "issued"));
        var invalid = FormDataApi.HandlePostFormData("u", ValidBody, new PostDeps((_, _) => new SaveResult(false)));
        var badJson = FormDataApi.HandlePostFormData("u", "{bad", new PostDeps((_, _) => throw new AssertionException("must not be called")));
        var ioError = FormDataApi.HandlePostFormData("u", ValidBody, new PostDeps((_, _) => throw new IOException()));
        var idError = FormDataApi.HandlePostFormData(null, ValidBody,
            new PostDeps((_, _) => new SaveResult(true), recorder.Set, () => throw new InvalidOperationException()));

        Assert.Multiple(() =>
        {
            Assert.That(ok.StatusCode, Is.EqualTo(200));
            Assert.That(recorder.Calls.Single().Value, Is.EqualTo("issued"));
            Assert.That(invalid.StatusCode, Is.EqualTo(400));
            Assert.That(badJson.StatusCode, Is.EqualTo(400));
            Assert.That(ioError.StatusCode, Is.EqualTo(500));
            Assert.That(idError.StatusCode, Is.EqualTo(500));
        });
    }

    // UT-02-05: GETのステータス・本文決定（200+JSON/404/500）
    [Test]
    public void HandleGetFormData_StatusCodesAndBody()
    {
        var dto = new FormDataDto { Text = "t", Slider = 5, Select = "apple", Radio = null };
        var found = FormDataApi.HandleGetFormData("u", new GetDeps(_ => new LoadResult(true, dto)));
        var notFound = FormDataApi.HandleGetFormData("u", new GetDeps(_ => new LoadResult(false, null)));
        var error = FormDataApi.HandleGetFormData("u", new GetDeps(_ => throw new JsonException()));

        Assert.Multiple(() =>
        {
            Assert.That(found.StatusCode, Is.EqualTo(200));
            Assert.That(found.BodyJson, Does.Contain("\"slider\":5").And.Contain("\"radio\":null"));
            Assert.That(notFound, Is.EqualTo(new GetResult(404, null)));
            Assert.That(error, Is.EqualTo(new GetResult(500, null)));
        });
    }

    // UT-02-09: GETでもCookie未設定なら新規発行、Cookie付与失敗は500、Found:trueでData:nullの契約違反は500
    [Test]
    public void Handlers_CookieIssuanceAndContractViolations()
    {
        var recorder = new CookieRecorder();
        var getNew = FormDataApi.HandleGetFormData(null, new GetDeps(_ => new LoadResult(false, null), recorder.Set, () => "g-id"));
        Action<string, string, CookieWriteOptions> failing = (_, _, _) => throw new InvalidOperationException();
        var postCookieError = FormDataApi.HandlePostFormData(null, ValidBody, new PostDeps((_, _) => new SaveResult(true), failing));
        var getCookieError = FormDataApi.HandleGetFormData(null, new GetDeps(_ => new LoadResult(false, null), failing));
        var contractViolation = FormDataApi.HandleGetFormData("u", new GetDeps(_ => new LoadResult(true, null)));

        Assert.Multiple(() =>
        {
            Assert.That(getNew.StatusCode, Is.EqualTo(404));
            Assert.That(recorder.Calls.Single().Value, Is.EqualTo("g-id"));
            Assert.That(postCookieError.StatusCode, Is.EqualTo(500));
            Assert.That(getCookieError.StatusCode, Is.EqualTo(500));
            Assert.That(contractViolation.StatusCode, Is.EqualTo(500));
        });
    }

    // UT-02-06: 委譲先が例外を投げてもロックは解放される（同一userIdで続けて処理できる）
    [Test]
    public void Handlers_ReleaseLockEvenWhenDelegateThrows()
    {
        string userId = "lock-" + Guid.NewGuid().ToString("N");
        FormDataApi.HandlePostFormData(userId, ValidBody, new PostDeps((_, _) => throw new IOException()));
        FormDataApi.HandleGetFormData(userId, new GetDeps(_ => throw new IOException()));

        var task = Task.Run(() => FormDataApi.HandleGetFormData(userId, new GetDeps(_ => new LoadResult(false, null))));
        Assert.That(task.Wait(TimeSpan.FromSeconds(5)), Is.True, "lock was not released");
    }

    // UT-02-07: 同一userIdの処理は直列化される（同時に2つ以上が委譲区間に入らない）
    [Test]
    public void Handlers_SerializeSameUser()
    {
        string userId = "serial-" + Guid.NewGuid().ToString("N");
        int inside = 0, maxInside = 0;
        SaveResult Save(string _, FormDataDto __)
        {
            int now = Interlocked.Increment(ref inside);
            InterlockedMax(ref maxInside, now);
            Thread.Sleep(20);
            Interlocked.Decrement(ref inside);
            return new SaveResult(true);
        }

        Parallel.For(0, 8, _ => FormDataApi.HandlePostFormData(userId, ValidBody, new PostDeps(Save)));
        Assert.That(maxInside, Is.EqualTo(1));
    }

    // UT-02-08: NFR-02 起動時バインドアドレス判定（ループバックのみ許可、ワイルドカード・外部アドレスは拒否）
    [TestCase("http://localhost:5080", true)]
    [TestCase("http://127.0.0.1:5080", true)]
    [TestCase("http://[::1]:5080", true)]
    [TestCase("http://+:5080", false)]
    [TestCase("http://*:5080", false)]
    [TestCase("http://0.0.0.0:5080", false)]
    [TestCase("http://[::]:5080", false)]
    [TestCase("http://192.168.1.10:5080", false)]
    [TestCase("", false)]
    public void LoopbackBindingCheck_IsLoopbackUrl(string url, bool expected)
    {
        Assert.That(LoopbackBindingCheck.IsLoopbackUrl(url), Is.EqualTo(expected));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
