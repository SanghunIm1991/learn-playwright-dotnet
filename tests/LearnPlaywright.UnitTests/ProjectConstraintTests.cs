using System.Globalization;
using System.Text.RegularExpressions;
using LearnPlaywright.Logic;

namespace LearnPlaywright.UnitTests;

/// <summary>要件・制約のうち、成果物の構成を機械的に確認できるものの単体テスト。UT-XX-xx はテスト仕様書のテストID。</summary>
[TestFixture]
public class ProjectConstraintTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (global.json) was not found.");
    }

    private static string WwwRoot(string file) => Path.Combine(RepoRoot(), "src", "LearnPlaywright.Server", "wwwroot", file);

    // UT-XX-01: ロジック層（COMP-03）はASP.NET Core等のWebフレームワークに依存しない
    [Test]
    public void LogicAssembly_DoesNotReferenceAspNetCore()
    {
        var references = typeof(FormDataStore).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);
        Assert.That(references, Has.None.StartsWith("Microsoft.AspNetCore"));
    }

    // UT-XX-02: style.css の文字色/背景色の組はすべてコントラスト比4.5:1以上（背景未指定のブロックは白背景とみなす）
    [Test]
    public void StyleSheet_ColorPairsMeetContrastRatio()
    {
        string css = File.ReadAllText(WwwRoot("style.css"));
        var blocks = Regex.Matches(css, @"([^{}]+)\{([^}]*)\}");
        var checkedPairs = 0;
        Assert.Multiple(() =>
        {
            foreach (Match block in blocks)
            {
                string body = block.Groups[2].Value;
                var fg = Regex.Match(body, @"(?<![-\w])color:\s*#([0-9a-fA-F]{6})");
                if (!fg.Success) continue;
                var bg = Regex.Match(body, @"background-color:\s*#([0-9a-fA-F]{6})");
                double ratio = ContrastRatio(fg.Groups[1].Value, bg.Success ? bg.Groups[1].Value : "ffffff");
                checkedPairs++;
                Assert.That(ratio, Is.GreaterThanOrEqualTo(4.5), $"selector '{block.Groups[1].Value.Trim()}' ratio={ratio:F2}");
            }
        });
        Assert.That(checkedPairs, Is.GreaterThan(0));
    }

    // UT-XX-03: フロントエンドはフレームワーク・外部スクリプトを読み込まない（ローカルのapp.jsのみ）
    [Test]
    public void IndexHtml_LoadsOnlyLocalPlainScript()
    {
        string html = File.ReadAllText(WwwRoot("index.html"));
        var scripts = Regex.Matches(html, "<script[^>]*src=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(scripts, Is.EqualTo(new[] { "app.js" }));
            Assert.That(html, Does.Not.Contain("http://").And.Not.Contain("https://"));
            Assert.That(File.ReadAllText(WwwRoot("app.js")), Does.Not.Match("(?i)\\b(react|vue|angular|jquery)\\b"));
        });
    }

    // UT-XX-04: 利用者データの保存先ディレクトリが .gitignore で除外されている
    [Test]
    public void GitIgnore_ExcludesDataDirectory()
    {
        string gitignore = File.ReadAllText(Path.Combine(RepoRoot(), ".gitignore"));
        Assert.That(gitignore, Does.Contain("src/LearnPlaywright.Server/App_Data/"));
    }

    private static double ContrastRatio(string hex1, string hex2)
    {
        double l1 = Luminance(hex1), l2 = Luminance(hex2);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            double c = int.Parse(hex.Substring(offset, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }
}
