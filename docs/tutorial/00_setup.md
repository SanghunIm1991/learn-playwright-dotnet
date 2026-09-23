# 0章 環境構築

この章では、教材を進めるのに必要な環境を整えます。ゴールは「学習用のテストプロジェクトを作り、`dotnet test` で既定のサンプルテストが成功する」ことと、「テスト対象のサンプルアプリをビルド・起動できる」ことです。環境構築の手順はすべて自分の手で実行できるように書いています。

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

## 手順5: サンプルアプリ（このリポジトリ）を準備する

2章以降でテストの対象にするサンプルアプリを、自分のPCでビルドして起動できるようにします。

### 5-1. リポジトリを手元に用意する

GitHub のリポジトリページから、次のどちらかの方法で取得します（置き場所は学習用プロジェクトとは別のフォルダにします）。

- Git を使う場合: リポジトリページの「Code」ボタンに表示されるURLを使って `git clone <リポジトリのURL>` を実行する
- Git を使わない場合: 「Code」→「Download ZIP」でダウンロードし、任意のフォルダに展開する

以降、**リポジトリのルートフォルダ**（`global.json` や `README.md` があるフォルダ）で作業します。

### 5-2. ビルドする（必要なパッケージが自動でダウンロードされる）

```powershell
# リポジトリのルートフォルダへ移動する（パスは自分の置き場所に合わせる）
cd C:\work\LearnPlaywright

# ソリューション全体をビルドする。
# 初回は NuGet パッケージ（NUnit、Microsoft.Playwright.NUnit など）が
# 公式の配布サイト nuget.org から自動でダウンロードされる（%USERPROFILE%\.nuget\packages に保存）
dotnet build
```

出力の最後に `ビルドに成功しました。`（英語環境では `Build succeeded.`）と出ればOKです。

> **初めて `dotnet` を使ったときに表示されるメッセージについて**
>
> .NET SDK を初めて使うと、`.NET へようこそ` という案内と一緒に、次のような表示が出ることがあります。
>
> - **テレメトリ**: .NET ツールの利用状況データが Microsoft に送信される旨の案内です。送信したくない場合は、環境変数 `DOTNET_CLI_TELEMETRY_OPTOUT` を `1` に設定します（任意）。
> - **ASP.NET Core HTTPS 開発証明書をインストールしました**: 開発用の証明書が自分のユーザーの証明書ストアに追加されたという案内です。本サンプルは HTTP（`http://localhost`）だけで動くため、この証明書は使いません。案内にある `dotnet dev-certs https --trust`（証明書を信頼済みにする操作）は**実行する必要はありません**。

### 5-3. サンプルアプリを起動して画面を確認する

```powershell
# サンプルアプリのサーバーを起動する（リポジトリのルートフォルダで実行）
dotnet run --project src/LearnPlaywright.Server
```

次のような行が表示されたら起動完了です。

```text
Now listening on: http://localhost:5080
Bound address: http://localhost:5080 (loopback: True)
```

- `Bound address ... (loopback: True)` は、サーバーが自分のPCの中（ループバックアドレス）だけで待ち受けていることの確認表示です。
- ブラウザで http://localhost:5080/ を開き、「LearnPlaywright サンプルフォーム」の画面が表示されることを確認します。ダミー値を入れて「送信」→ ページを再読み込み →「読み込み」で値が戻ることも試してみてください。
- 確認できたら、ターミナルで `Ctrl+C` を押してサーバーを止めます（2章で改めて起動します）。

### 5-4.（任意）完成版のテストを実行してみる

リポジトリには完成版のテスト（9章で読み比べるお手本）が入っています。先に動かしておくと、「最終的にこうなる」というゴールが分かります。

```powershell
# 完成版テストプロジェクト用に Chromium をインストールする（リポジトリのルートフォルダで実行）。
# 手順3で既に入れていても、Playwright のバージョンが違う場合は別途ダウンロードが必要になる。
# 同じバージョンなら「既にインストール済み」としてすぐ終わる。
pwsh tests/LearnPlaywright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
#   pwsh が無い場合:
#   powershell -ExecutionPolicy Bypass -File tests/LearnPlaywright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium

# 単体テストとE2Eテストをすべて実行する（E2Eテストはサーバーを自動で起動・終了する）
dotnet test
```

期待される出力の例（件数は教材作成時点のもの）:

```text
成功!   -失敗:     0、合格:    52、スキップ:     0、合計:    52、期間: 661 ms - LearnPlaywright.UnitTests.dll (net10.0)
成功!   -失敗:     0、合格:    29、スキップ:     0、合計:    29、期間: 10 s - LearnPlaywright.Tests.dll (net10.0)
```

- 5-3 で起動したサーバーを止めずに実行すると、ポート 5080 が使用中のため E2E テストが失敗します（`is already in use` と表示されます）。`Ctrl+C` で止めてから実行してください。
- ブラウザのダウンロード先は `%LOCALAPPDATA%\ms-playwright` です（合計で数百MB程度）。学習を終えて不要になったら、このフォルダを削除すれば取り除けます。

## うまくいかないとき

| 症状 | 確認すること |
|---|---|
| `dotnet` が見つからない | .NET SDK が入っていない、またはターミナルを開き直していない |
| `playwright.ps1` が見つからない | 先に `dotnet build` を実行したか、`bin/Debug/net10.0` のフォルダ名が合っているか |
| テストが `失敗` になる | 雛形のファイルを編集していないか（この時点では何も書き換えない） |
| `A compatible .NET SDK was not found` | 手順1で 10.0 系の SDK が入っているか（リポジトリの `global.json` は 10.0.100 以上を要求する） |
| `dotnet build` でパッケージの復元に失敗する | インターネットに接続できるか（nuget.org からのダウンロードが必要） |
| `Executable doesn't exist` を含むエラー | そのテストプロジェクトのフォルダで `playwright.ps1 install chromium` を実行したか |
| E2E テストが `is already in use` で失敗する | 手動で起動したサーバーが残っていないか（`Ctrl+C` で止める） |

## この章のまとめ

- `dotnet --list-sdks` で .NET 10 SDK を確認した
- `dotnet new nunit` と `dotnet add package Microsoft.Playwright.NUnit` で学習用プロジェクトを作った
- `playwright.ps1 install chromium` でブラウザ本体を入れた
- `dotnet test` でテストが成功することを確認した
- サンプルアプリをビルド・起動し、ブラウザで画面を確認した（完成版テストも任意で実行した）

次の1章では、実際にテストを書き始める前に「良いテストの書き方」の考え方を学びます。
