# 0章 環境構築

この章では、教材を進めるのに必要な環境を整えます。ゴールは「学習用のテストプロジェクトを作り、`dotnet test` で既定のサンプルテストが成功する」ことです。

## この章の前提知識

- Windows のターミナル（PowerShell または Windows Terminal）でコマンドを入力・実行できること
- フォルダの移動（`cd`）ができること
- Playwright や NUnit の知識は不要です

## 手順1: .NET 10 SDK が入っているか確認する

次のコマンドを実行します。

```powershell
# インストール済みの .NET SDK の一覧を表示する
dotnet --list-sdks
```

期待される出力の例（バージョン番号や行数は環境によって異なります）:

```text
8.0.408 [C:\Program Files\dotnet\sdk]
9.0.314 [C:\Program Files\dotnet\sdk]
10.0.401 [C:\Program Files\dotnet\sdk]
```

- **`10.0.` で始まる行があればOK**です（リポジトリの `global.json` は「.NET 10 SDK（10.0.100以上）のうち導入済みの最新」を使う設定のため、10.0系ならどのバージョンでも動きます）。
- ない場合は、公式サイト https://dotnet.microsoft.com/download/dotnet/10.0 から .NET 10 SDK のインストーラーを入手して導入してください（インストールはご自身で行ってください）。導入後、ターミナルを開き直してもう一度 `dotnet --list-sdks` を実行します。

## 手順2: 学習用テストプロジェクトを作る

学習用プロジェクトは、**このリポジトリの外**に作ることをおすすめします。リポジトリの `tests/LearnPlaywright.Tests` は完成版のお手本であり、それを上書き・複製するわけではありません（README「2つのテストプロジェクトの違い」参照）。

```powershell
# 作業用フォルダを作って移動する（場所は自由。例として C:\work を使う。既にある場合 mkdir は不要）
mkdir C:\work
cd C:\work

# NUnit のテストプロジェクトの雛形を作る
#   -n : プロジェクト名（同名のフォルダが作られる）
dotnet new nunit -n MyFirstPlaywrightTests

# 作ったプロジェクトのフォルダへ入る
cd MyFirstPlaywrightTests

# Playwright を NUnit から使うためのパッケージを追加する
dotnet add package Microsoft.Playwright.NUnit
```

`dotnet new nunit` の出力例（テンプレートの表示名はSDKの版によって少し異なります）:

```text
テンプレート "NUnit ... Test Project" が正常に作成されました。
```

作られた `MyFirstPlaywrightTests.csproj` を開き、`<TargetFramework>net10.0</TargetFramework>` になっていることを確認してください。別のバージョン（`net9.0` など）になっていた場合は、一度フォルダを削除し、`dotnet new nunit -n MyFirstPlaywrightTests -f net10.0` で作り直します。

`dotnet add package` の出力の最後に次のような行があれば成功です（バージョン番号は時期によって変わります）。

```text
info : パッケージ 'Microsoft.Playwright.NUnit' のバージョン '1.xx.x' の PackageReference がファイル '...MyFirstPlaywrightTests.csproj' に追加されました。
```

> 補足: 英語表示の環境では、同じ内容が英語で表示されます（例: `info : PackageReference for package 'Microsoft.Playwright.NUnit' version '1.xx.x' added to file ...`）。

## 手順3: Playwright 用のブラウザ本体をインストールする

Playwright は、テスト専用のブラウザ本体（Chromium など）を別途ダウンロードして使います。そのためのスクリプト `playwright.ps1` は、**一度ビルドすると** `bin` フォルダの中に作られます。

```powershell
# まずビルドする（これで bin/Debug/net10.0/playwright.ps1 が作られる）
dotnet build

# Chromium（Chrome の元になっているブラウザ）だけをインストールする
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

- `dotnet build` の出力の最後に `ビルドに成功しました。`（英語環境では `Build succeeded.`）と出ればOKです。
- `pwsh` は PowerShell 7 のコマンドです。「`pwsh` が見つからない」と表示された場合は、Windows に標準で入っている PowerShell 5.1 で次のように実行しても構いません。

```powershell
# -ExecutionPolicy Bypass は「この1回の powershell プロセスに限って」スクリプト実行を許可する指定。
# PC全体の設定は変更しない（ターミナルを閉じれば効果は残らない）。
powershell -ExecutionPolicy Bypass -File bin/Debug/net10.0/playwright.ps1 install chromium
```

ダウンロードの進行状況（`Downloading Chromium ...` のような表示とパーセント表示）が出て、エラーなく終了すれば成功です。

## 手順4: 動作確認（既定のサンプルテストを実行する）

`dotnet new nunit` は、何もしないで成功するだけのサンプルテスト（`UnitTest1.cs` の `Test1`。テンプレートの版によってファイル名が違うことがあります）を最初から用意しています。これを実行して、テストの仕組みが動くことを確認します。

```powershell
# プロジェクトのフォルダで、テストをすべて実行する
dotnet test
```

期待される出力の例（数字や所要時間は環境によって異なります）:

```text
成功!   -失敗:     0、合格:     1、スキップ:     0、合計:     1、期間: 25 ms - MyFirstPlaywrightTests.dll (net10.0)
```

英語環境では次のように表示されます。

```text
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 25 ms - MyFirstPlaywrightTests.dll (net10.0)
```

**`成功!`（または `Passed!`）と `失敗: 0`（`Failed: 0`）が表示されれば、この章は完了です。**

- この教材では、テストの実行には常にこの `dotnet test` の形だけを使います。オプションを付けたり、別の実行方法に切り替えたりはしません。

## うまくいかないとき

| 症状 | 確認すること |
|---|---|
| `dotnet` が見つからない | .NET SDK が入っていない、またはターミナルを開き直していない |
| `playwright.ps1` が見つからない | 先に `dotnet build` を実行したか、`bin/Debug/net10.0` のフォルダ名が合っているか |
| テストが `失敗` になる | 雛形のファイルを編集していないか（この時点では何も書き換えない） |

## この章のまとめ

- `dotnet --list-sdks` で .NET 10 SDK を確認した
- `dotnet new nunit` と `dotnet add package Microsoft.Playwright.NUnit` で学習用プロジェクトを作った
- `playwright.ps1 install chromium` でブラウザ本体を入れた
- `dotnet test` でテストが成功することを確認した

次の1章では、実際にテストを書き始める前に「良いテストの書き方」の考え方を学びます。
