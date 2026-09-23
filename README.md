# LearnPlaywright

Playwright for .NET（C# / NUnit）によるブラウザテスト自動化を学ぶための学習用プロジェクトです。
題材として、テキストボックス・スライダー・プルダウン・ラジオボタンを持つ入力フォーム（HTML/CSS/JavaScript + ASP.NET Core / .NET 10）と、そのUIに対するパラメータ化されたPlaywrightテストを含みます。

> **注意（学習用サンプル）**
> - 認証・認可機構を持たない学習用サンプルです。本番運用を想定した設計ではありません。
> - サーバーは `localhost` でのみ待ち受けます（ループバック以外へのバインドを検出すると起動を停止します）。
> - 入力した値はサーバー上にJSONファイルとして保存されます。実在の個人情報・機微情報は入力せず、ダミー値のみを使ってください。

## 必要な環境

- Windows 11
- .NET 10 SDK（`global.json` で 10.0.x に固定）
- Playwright用ブラウザ（初回のみ導入。下記参照）

## 使い方

```powershell
# ビルド
dotnet build

# サンプルサイトを起動（http://localhost:5080/ をブラウザで開く）
dotnet run --project src/LearnPlaywright.Server

# 初回のみ: Playwright用ブラウザ（Chromium）の導入
pwsh tests/LearnPlaywright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
#   PowerShell 7 が無い場合:
#   powershell -ExecutionPolicy Bypass -File tests/LearnPlaywright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium

# 全テスト（単体テスト + E2Eテスト）を実行。E2Eはサーバーを自動起動します
dotnet test
```

## 構成

| パス | 内容 |
|---|---|
| `src/LearnPlaywright.Server/wwwroot/` | フロントエンドUI（COMP-01） |
| `src/LearnPlaywright.Server/` | バックエンドAPI（COMP-02、ASP.NET Core） |
| `src/LearnPlaywright.Logic/` | データ永続化ロジック（COMP-03、Webフレームワーク非依存） |
| `tests/LearnPlaywright.Tests/TestData/` | テストデータ構造・Playwright操作ヘルパー（COMP-04） |
| `tests/LearnPlaywright.Tests/Scenarios/` | Playwrightテストシナリオ（COMP-05） |
| `tests/LearnPlaywright.UnitTests/` | COMP-02/03等の単体テスト |
| `docs/tutorial/` | 学習教材（COMP-06、環境構築から完成形テストまで） |
| `docs/` | 要件定義〜テスト仕様書、トレーサビリティマトリックス |

学習を始める場合は [`docs/tutorial/README.md`](docs/tutorial/README.md) から読み進めてください。

> **開発用ファイルについて**: `CLAUDE.md`、`.claude/`、`docs/qa_log.md`・`docs/review_log.md`・`docs/handoff.md`は、作者が Claude Code を使って開発した際の作業ルール・設定・記録です。作者のローカル環境にある Claude Code の設定やスキルを前提にした記述を含むため、参照先がこのリポジトリに無い場合があります。このリポジトリを Claude Code で開くと、`.claude/settings.json` のプロジェクト設定（コマンドの許可など）が適用されます。不要であれば削除または変更してから使ってください。学習やテストの実行にはこれらのファイルは必要ありません。

## ライセンス

[MIT License](LICENSE)

本リポジトリには、自作のソースコード・ドキュメントのみを含みます。以下の依存ソフトウェアはリポジトリに同梱せず、ビルド時（NuGet）または初回セットアップ時（ブラウザ）に各自の環境へ取得されます。それぞれのライセンスに従います。

| 依存ソフトウェア | 用途 | ライセンス |
|---|---|---|
| .NET SDK / ASP.NET Core / System.Text.Json 等 | 実行基盤・Webサーバー | MIT |
| NUnit / NUnit3TestAdapter / NUnit.Analyzers | テストフレームワーク | MIT |
| Microsoft.NET.Test.Sdk / coverlet.collector | テスト実行基盤 | MIT |
| Microsoft.Playwright / Microsoft.Playwright.NUnit | ブラウザ自動操作 | MIT（パッケージに同梱されるPlaywrightドライバはApache-2.0、Node.jsはMIT系） |
| Chromium（Chrome for Testing） | テスト用ブラウザ（`playwright.ps1 install`で取得） | 各配布元のライセンス・利用規約 |
