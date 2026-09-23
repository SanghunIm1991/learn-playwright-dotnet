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
