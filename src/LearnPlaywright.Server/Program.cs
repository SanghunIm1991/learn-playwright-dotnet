using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using LearnPlaywright.Server;

// 学習用サンプル: 認証・認可機構を持たず、ローカルホストでのみ動作させる前提（CON-01, CON-05）。
var builder = WebApplication.CreateBuilder(args);

// NFR-02: URL未指定時は localhost 固定の既定URLで待ち受ける。
// --urls / ASPNETCORE_URLS でポートを変更する場合もホストは必ず localhost / 127.0.0.1 とすること（ワイルドカード禁止）。
if (string.IsNullOrEmpty(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://localhost:5080");
}

var app = builder.Build();

// 保存先ディレクトリ（CON-07: .gitignore で除外済み）。設定キー DataDirectory で上書きできる（E2Eテストで一時ディレクトリを指定）。
string dataDirectory = Path.GetFullPath(
    app.Configuration["DataDirectory"] ?? Path.Combine(app.Environment.ContentRootPath, "App_Data"));

app.UseDefaultFiles();
app.UseStaticFiles();
FormDataApi.MapFormDataEndpoints(app, dataDirectory);

// NFR-02: 起動後に実バインドアドレスをログ出力し、ループバック以外を含む場合は直ちに停止する。
app.Lifetime.ApplicationStarted.Register(() =>
{
    var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
        ?? Array.Empty<string>();
    foreach (var address in addresses)
    {
        app.Logger.LogInformation("Bound address: {Address} (loopback: {IsLoopback})", address, LoopbackBindingCheck.IsLoopbackUrl(address));
    }
    if (addresses.Count == 0 || !addresses.All(LoopbackBindingCheck.IsLoopbackUrl))
    {
        app.Logger.LogCritical("Non-loopback binding detected. Stopping the server (NFR-02).");
        app.Lifetime.StopApplication();
    }
});

app.Run();
