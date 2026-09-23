using System.Collections.Concurrent;
using System.Text.Json;
using LearnPlaywright.Logic;

namespace LearnPlaywright.Server;

/// <summary>Set-Cookie付与時の属性（COMP-02関数設計書3節 CookieWriteOptions）。</summary>
public sealed record CookieWriteOptions(bool HttpOnly, string Path, int? MaxAge);

/// <summary>FUNC-10の戻り値。</summary>
public sealed record UserIdResolution(string UserId, bool IsNewlyIssued);

/// <summary>FUNC-16の依存オブジェクト。SaveFormDataはCOMP-03への委譲（必須）。</summary>
public sealed record PostDeps(
    Func<string, FormDataDto, SaveResult> SaveFormData,
    Action<string, string, CookieWriteOptions>? SetCookieFn = null,
    Func<string>? IdGenerator = null);

/// <summary>FUNC-17の依存オブジェクト。LoadFormDataはCOMP-03への委譲（必須）。</summary>
public sealed record GetDeps(
    Func<string, LoadResult> LoadFormData,
    Action<string, string, CookieWriteOptions>? SetCookieFn = null,
    Func<string>? IdGenerator = null);

public sealed record PostResult(int StatusCode);

public sealed record GetResult(int StatusCode, string? BodyJson);

/// <summary>
/// COMP-02 バックエンドAPI（HTTP層、FUNC-10〜18）。
/// 業務ロジックは持たず、Cookie処理・ステータスコード決定・COMP-03への委譲に専念する。
/// </summary>
public static class FormDataApi
{
    public const string UserIdCookieName = "lp_user_id";
    public const string EndpointPath = "/api/form-data";

    // Cookie有効期間: 1年（関数設計書3節の暫定値を実装値として確定）。
    public const int CookieMaxAgeSeconds = 31536000;

    // ユーザー識別文字列ごとの排他ロック（関数設計書4節）。プロセス内でのみ有効。
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserLocks = new();

    // FUNC-10
    public static UserIdResolution GetOrIssueUserId(string? cookieValue, Func<string>? idGenerator = null)
    {
        if (!string.IsNullOrEmpty(cookieValue)) return new UserIdResolution(cookieValue, false);
        var generate = idGenerator ?? (() => Guid.NewGuid().ToString("N"));
        return new UserIdResolution(generate(), true);
    }

    // FUNC-11
    public static void IssueUserIdCookie(string userId, Action<string, string, CookieWriteOptions> setCookieFn)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
        ArgumentNullException.ThrowIfNull(setCookieFn);
        setCookieFn(UserIdCookieName, userId, new CookieWriteOptions(HttpOnly: true, Path: "/", MaxAge: CookieMaxAgeSeconds));
    }

    // FUNC-12
    public static FormDataDto ParseFormDataRequestBody(string requestBodyJson)
    {
        ArgumentNullException.ThrowIfNull(requestBodyJson);
        return JsonSerializer.Deserialize<FormDataDto>(requestBodyJson, ApiJsonOptions)
            ?? throw new JsonException("Request body is null.");
    }

    // FUNC-13
    public static string SerializeFormDataToJson(FormDataDto data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.Serialize(data, ApiJsonOptions);
    }

    // FUNC-14（GetOrAddで同一userIdに対するロックインスタンスの一意性を保証する）
    public static void AcquireUserLock(string userId)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
        UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1)).Wait();
    }

    // FUNC-15
    public static void ReleaseUserLock(string userId)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required", nameof(userId));
        if (UserLocks.TryGetValue(userId, out var semaphore)) semaphore.Release();
    }

    // FUNC-16
    public static PostResult HandlePostFormData(string? cookieValue, string requestBodyJson, PostDeps deps)
    {
        ArgumentNullException.ThrowIfNull(deps);
        try
        {
            var resolved = GetOrIssueUserId(cookieValue, deps.IdGenerator);                // 手順1
            if (resolved.IsNewlyIssued && deps.SetCookieFn is not null)
            {
                IssueUserIdCookie(resolved.UserId, deps.SetCookieFn);                        // 手順2
            }

            FormDataDto dto;
            try
            {
                dto = ParseFormDataRequestBody(requestBodyJson);                             // 手順3
            }
            catch (Exception ex) when (ex is JsonException or ArgumentNullException or NotSupportedException)
            {
                return new PostResult(StatusCodes.Status400BadRequest);
            }

            SaveResult result;
            AcquireUserLock(resolved.UserId);                                                // 手順4
            try
            {
                result = deps.SaveFormData(resolved.UserId, dto);                            // 手順5
            }
            finally
            {
                ReleaseUserLock(resolved.UserId);                                            // 手順6
            }

            return new PostResult(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest); // 手順8・9
        }
        catch (Exception)
        {
            return new PostResult(StatusCodes.Status500InternalServerError);                 // 手順1・2・7
        }
    }

    // FUNC-17
    public static GetResult HandleGetFormData(string? cookieValue, GetDeps deps)
    {
        ArgumentNullException.ThrowIfNull(deps);
        try
        {
            var resolved = GetOrIssueUserId(cookieValue, deps.IdGenerator);                // 手順1
            if (resolved.IsNewlyIssued && deps.SetCookieFn is not null)
            {
                IssueUserIdCookie(resolved.UserId, deps.SetCookieFn);                        // 手順2
            }

            LoadResult result;
            AcquireUserLock(resolved.UserId);                                                // 手順3
            try
            {
                result = deps.LoadFormData(resolved.UserId);                                 // 手順4
            }
            finally
            {
                ReleaseUserLock(resolved.UserId);                                            // 手順5
            }

            if (!result.Found || result.Data is null) return new GetResult(StatusCodes.Status404NotFound, null); // 手順7
            return new GetResult(StatusCodes.Status200OK, SerializeFormDataToJson(result.Data));              // 手順8
        }
        catch (Exception)
        {
            return new GetResult(StatusCodes.Status500InternalServerError, null);            // 手順1・2・6
        }
    }

    // FUNC-18
    public static void MapFormDataEndpoints(IEndpointRouteBuilder app, string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (string.IsNullOrEmpty(dataDirectory)) throw new ArgumentException("dataDirectory is required", nameof(dataDirectory));

        // COMP-03の3引数版をbaseDirectory固定のクロージャで2引数契約へ合わせる（COMP-03関数設計書2.1節）。
        Func<string, FormDataDto, SaveResult> save = (userId, dto) => FormDataStore.SaveFormData(userId, dto, dataDirectory);
        Func<string, LoadResult> load = userId => FormDataStore.LoadFormData(userId, dataDirectory);

        app.MapPost(EndpointPath, async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();
            var result = HandlePostFormData(
                context.Request.Cookies[UserIdCookieName],
                body,
                new PostDeps(save, CreateSetCookieFn(context)));
            context.Response.StatusCode = result.StatusCode;
        });

        app.MapGet(EndpointPath, async (HttpContext context) =>
        {
            var result = HandleGetFormData(
                context.Request.Cookies[UserIdCookieName],
                new GetDeps(load, CreateSetCookieFn(context)));
            context.Response.StatusCode = result.StatusCode;
            if (result.BodyJson is not null)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(result.BodyJson);
            }
        });
    }

    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static Action<string, string, CookieWriteOptions> CreateSetCookieFn(HttpContext context) =>
        (name, value, options) => context.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = options.HttpOnly,
            Path = options.Path,
            MaxAge = options.MaxAge is int seconds ? TimeSpan.FromSeconds(seconds) : null,
            SameSite = SameSiteMode.Lax,
        });
}
