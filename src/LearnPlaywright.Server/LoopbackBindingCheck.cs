using System.Net;

namespace LearnPlaywright.Server;

/// <summary>
/// NFR-02: 起動時に実際のバインドアドレスがループバックのみであることを確認するための判定処理。
/// </summary>
public static class LoopbackBindingCheck
{
    /// <summary>
    /// "http://localhost:5080" 形式のURLのホスト部がループバック（localhost / 127.0.0.1 / ::1）であればtrue。
    /// ワイルドカード（+, *）・0.0.0.0・[::]・その他のホストはfalse。
    /// </summary>
    public static bool IsLoopbackUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        // "+" や "*" はUriとして解釈できない場合があるため先に除外する。
        if (url.Contains("://+") || url.Contains("://*")) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        string host = uri.Host.Trim('[', ']');
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
