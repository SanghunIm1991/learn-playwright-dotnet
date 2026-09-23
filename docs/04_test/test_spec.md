# テスト仕様書

## 1. 文書情報

| 項目 | 内容 |
|---|---|
| 文書名 | LearnPlaywright テスト仕様書 |
| 版数 | v1.2 |
| 作成日 | 2026-09-23 |
| 作成者 | ClaudeCode |

### 改訂履歴

| 版数 | 日付 | 変更内容 | 変更者 |
|---|---|---|---|
| v1.0 | 2026-09-23 | 初版作成 | ClaudeCode |
| v1.1 | 2026-09-23 | レビュー指摘（B-1〜5）対応: E2E-01/02/04の対応関数を実態に合わせて修正、無理な要件対応付け（CON-05↔UT-03-01、NFR-06↔UT-03-13、CON-03↔UT-XX-01）を削除、テストレベル定義を明確化、413未検証を既知の限界に追記 | ClaudeCode |
| v1.2 | 2026-09-24 | レビュー指摘（軽微4件）対応: E2E-01の対応要件にNFR-07を追加（E2E-02と同一のTestCaseSource/データ構造を用いるため） | ClaudeCode |

## 2. テスト方針

- **テストレベル**: 単体（関数・クラス単位。COMP-02/03のC#関数、成果物構成の機械的確認）と統合（サーバーを実プロセスとして起動しブラウザから全層を通すPlaywright E2E、および完成した教材ドキュメント全体の検査）の2レベルとする。本プロジェクトではCOMP-01〜03の組み合わせ検証をE2Eが兼ねるため、結合レベルのテストは個別に置かない。教材検査（TESTMOD-05）は成果物全体を対象とするため統合に分類する。
- **由来**: 仕様/設計ベース（要件・関数設計書から導出）、コードベース（実装を見て追加）、レビュー指摘ベース。トレーサビリティマトリックス（③④）には仕様/設計ベースのみ記載する。
- **TDD**: 採用しない（設計書ベースの後付けテスト）。
- **リグレッション**: フルリグレッション（`dotnet test`で全件実行、所要1分未満のため代表ケース選定は不要）。
- **COMP-01（JavaScript）の検証**: Node.js未導入のため関数単体テストは行わず、E2E（TESTMOD-03）経由で検証する。`app.js`は将来の単体テスト用に`module.exports`を用意済み。
- **GUIの目視確認**: 画面の見た目（配置・視認性）は自動テストの対象外。テスト工程完了時のユーザー確認で目視する（`dotnet run --project src/LearnPlaywright.Server`→ http://localhost:5080/ ）。

### 2.1 実行方法

```
dotnet test
```

- 初回のみPlaywrightブラウザの導入が必要: `dotnet build` 後に `pwsh tests/LearnPlaywright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`（PowerShell 7が無い場合は`powershell -ExecutionPolicy Bypass -File ...`）。
- E2Eはテストフィクスチャがサーバーを`http://localhost:5080`で自動起動する（ポート5080を他で使用中の場合は失敗する）。保存データは一時ディレクトリに書き込み、終了時に削除する。
- ブラウザ画面を表示して実行する場合は環境変数`LEARNPLAYWRIGHT_HEADED=1`を設定する。

## 3. テストモジュール一覧

| モジュールID | ファイル | レベル | 対象 |
|---|---|---|---|
| TESTMOD-01 | `tests/LearnPlaywright.UnitTests/FormDataStoreTests.cs` | 単体 | COMP-03 |
| TESTMOD-02 | `tests/LearnPlaywright.UnitTests/FormDataApiTests.cs` | 単体 | COMP-02（NFR-02判定処理を含む） |
| TESTMOD-03 | `tests/LearnPlaywright.Tests/Scenarios/FormDataScenarioTests.cs`（`TestData/`のCOMP-04を利用） | 統合 | COMP-01〜05 |
| TESTMOD-04 | `tests/LearnPlaywright.UnitTests/ProjectConstraintTests.cs` | 単体 | 成果物構成（NFR-06/08, CON-02/07） |
| TESTMOD-05 | 教材ドキュメント検査（`docs/tutorial/`、レビューサブエージェントによるチェックリスト検査） | 統合 | COMP-06 |

## 4. テストケース一覧

### TESTMOD-01: COMP-03 単体テスト

| テストID | 内容 | 期待結果 | 対応関数 | 対応要件 | レベル | 由来 |
|---|---|---|---|---|---|---|
| UT-03-01 | パストラバーサル文字列・予約デバイス名・NUL文字を含むuserIdのパス変換 | ファイル名が`[0-9a-f]{64}.json`、基準ディレクトリ直下 | FUNC-19 | REQ-03 | 単体 | 仕様/設計 |
| UT-03-02 | 5000文字のuserId、同一入力の再変換 | 固定長・決定的 | FUNC-19 | REQ-03 | 単体 | 仕様/設計 |
| UT-03-03 | userId/baseDirectoryがnull・空文字 | ArgumentException | FUNC-19 | REQ-03 | 単体 | 仕様/設計 |
| UT-03-04a〜k | 検証ルールの境界値（text 空/null/1000/1001、slider NaN/Infinity/負値、select 空/null/201、radio 201） | 表のとおり有効/無効 | FUNC-20 | REQ-03 | 単体 | 仕様/設計 |
| UT-03-05 | 複数ルール違反 | 違反3件をすべて列挙 | FUNC-20 | REQ-03 | 単体 | 仕様/設計 |
| UT-03-06 | 保存用JSON変換の往復 | camelCase・radio null・値一致 | FUNC-21, FUNC-22 | REQ-03, REQ-04 | 単体 | 仕様/設計 |
| UT-03-07 | 復元時の構文不正・型不一致／未知プロパティ・radio欠落 | 前者JsonException、後者は許容 | FUNC-22 | REQ-04 | 単体 | 仕様/設計 |
| UT-03-08 | 保存→上書き保存→読込 | 最新値が復元される（ディレクトリ自動作成） | FUNC-23, FUNC-24, FUNC-25, FUNC-26 | REQ-03, REQ-04 | 単体 | 仕様/設計 |
| UT-03-09 | 検証エラー時の保存 | success:false、ファイル未作成 | FUNC-25 | REQ-03 | 単体 | 仕様/設計 |
| UT-03-10 | 未保存ユーザーの読込 | Found:false（例外なし） | FUNC-24, FUNC-26 | REQ-04 | 単体 | 仕様/設計 |
| UT-03-11 | 破損ファイルの読込 | JsonException（未存在と区別） | FUNC-26 | REQ-04 | 単体 | 仕様/設計 |
| UT-03-12 | Save/Loadの引数不正 | ArgumentException系 | FUNC-25, FUNC-26 | REQ-03, REQ-04 | 単体 | 仕様/設計 |
| UT-03-13 | 書き込み時のI/O異常 | IOExceptionが伝播（success:falseにしない） | FUNC-23, FUNC-25 | REQ-03 | 単体 | 仕様/設計 |

### TESTMOD-02: COMP-02 単体テスト

| テストID | 内容 | 期待結果 | 対応関数 | 対応要件 | レベル | 由来 |
|---|---|---|---|---|---|---|
| UT-02-01 | Cookie値 null/空/設定済み | 未設定時のみ新規発行 | FUNC-10 | REQ-06 | 単体 | 仕様/設計 |
| UT-02-02 | Cookie付与の属性、userId空 | lp_user_id・HttpOnly・Path=/・1年／ArgumentException | FUNC-11 | REQ-06 | 単体 | 仕様/設計 |
| UT-02-03 | リクエストボディ解析（正常・radio欠落・構文不正・型不一致・必須欠落） | 正常は解析、異常はJsonException | FUNC-12 | REQ-02, REQ-03 | 単体 | 仕様/設計 |
| UT-02-04 | POSTのステータス決定 | 200/400（検証エラー・構文不正）/500（I/O・ID生成失敗）、新規Cookie発行 | FUNC-16 | REQ-02, REQ-03, REQ-06, NFR-06 | 単体 | 仕様/設計 |
| UT-02-05 | GETのステータス・本文決定 | 200+JSON/404/500 | FUNC-13, FUNC-17 | REQ-04, NFR-06 | 単体 | 仕様/設計 |
| UT-02-06 | 委譲先例外後のロック解放 | 同一userIdで後続処理が完了する | FUNC-14, FUNC-15, FUNC-16, FUNC-17 | NFR-06 | 単体 | 仕様/設計 |
| UT-02-07 | 同一userIdの並行POST | 委譲区間の同時実行数が常に1 | FUNC-14, FUNC-15, FUNC-16 | NFR-06 | 単体 | 仕様/設計 |
| UT-02-08 | 起動時バインドアドレス判定 | localhost/127.0.0.1/::1のみtrue、`+`/`*`/0.0.0.0/[::]/外部IPはfalse | （起動処理、FUNC外） | NFR-02, CON-01 | 単体 | 仕様/設計 |
| UT-02-09 | GETでのCookie新規発行、Cookie付与失敗（POST/GET）、Found:trueでData:nullの契約違反 | 発行される／500／500 | FUNC-11, FUNC-16, FUNC-17 | REQ-06 | 単体 | レビュー指摘 |

### TESTMOD-03: E2E（COMP-05シナリオ、COMP-04ヘルパー経由）

E2E-01・E2E-02はCOMP-04 FUNC-29の12ケース（REP-DEFAULT、BND-TEXT-EMPTY/MAXLEN/OVERLEN、BND-SLIDER-MIN/MAX、BND-SELECT-FIRST/LAST、BND-RADIO-UNSELECTED、BND-RADIO-EACHOPTION-1〜3）でパラメータ化される。

| テストID | 内容 | 期待結果 | 対応関数 | 対応要件 | レベル | 由来 |
|---|---|---|---|---|---|---|
| E2E-01 | 入力→送信→メッセージ種別の検証（FUNC-47） | 受理ケースはsuccess、BND-TEXT-OVERLENはerror | FUNC-01, FUNC-03, FUNC-05, FUNC-07, FUNC-09, FUNC-16, FUNC-18, FUNC-25, FUNC-27〜30, FUNC-32, FUNC-34, FUNC-36, FUNC-38, FUNC-40, FUNC-42, FUNC-43〜47 | REQ-01, REQ-02, REQ-03, REQ-08, REQ-09, NFR-07, CON-01, CON-03, CON-04 | 統合 | 仕様/設計 |
| E2E-02 | 送信→再読み込み→読み込み→復元値の検証（FUNC-48） | 受理ケースは入力値どおり復元、拒否ケースはinfoメッセージ | FUNC-02, FUNC-04, FUNC-06, FUNC-08, FUNC-09, FUNC-17, FUNC-18, FUNC-26, FUNC-27〜42, FUNC-43〜46, FUNC-48 | REQ-04, REQ-05, REQ-06, REQ-08, REQ-09, NFR-07, CON-01, CON-04 | 統合 | 仕様/設計 |
| E2E-03 | 6種類のUIコントロールと注意書きの表示 | 全コントロール表示、ラジオ3件、注意書きに「個人情報」 | （静的HTML） | REQ-01, REQ-07, NFR-01 | 統合 | 仕様/設計 |
| E2E-04 | Cookie未設定での初回リクエスト | lp_user_id（HttpOnly）が発行される | FUNC-09, FUNC-10, FUNC-11, FUNC-18, FUNC-39 | REQ-06, CON-05 | 統合 | 仕様/設計 |
| E2E-05 | 読み込み応答を差し替え（select未知値・radio null） | selectは現状維持、radioは選択解除、text/sliderは復元 | FUNC-02, FUNC-08 | REQ-05 | 統合 | レビュー指摘 |
| E2E-06 | 読み込み応答を500に差し替え | errorメッセージ表示 | FUNC-04, FUNC-08 | REQ-04 | 統合 | レビュー指摘 |
| E2E-07 | Content-Type: text/plain でのPOST | 415で拒否 | FUNC-18 | NFR-01, CON-05 | 統合 | レビュー指摘 |

### TESTMOD-04: 成果物構成の単体テスト

| テストID | 内容 | 期待結果 | 対応関数 | 対応要件 | レベル | 由来 |
|---|---|---|---|---|---|---|
| UT-XX-01 | ロジック層アセンブリの参照 | Microsoft.AspNetCore.*を参照しない | FUNC-19〜26 | NFR-06 | 単体 | 仕様/設計 |
| UT-XX-02 | style.cssの文字色/背景色の組 | すべてコントラスト比4.5:1以上 | （静的CSS） | NFR-08 | 単体 | 仕様/設計 |
| UT-XX-03 | index.htmlが読み込むスクリプト | ローカルのapp.jsのみ、フレームワーク不使用 | FUNC-01〜09 | CON-02 | 単体 | 仕様/設計 |
| UT-XX-04 | .gitignoreの保存先除外 | `src/LearnPlaywright.Server/App_Data/`を含む | FUNC-23 | CON-07 | 単体 | 仕様/設計 |

### TESTMOD-05: 教材ドキュメント検査

レビューサブエージェントが`docs/tutorial/`を読み、以下のチェックリストを判定する（1回、`docs/review_log.md`に結果を記録）。

| テストID | 検査内容 | 合格基準 | 対応関数 | 対応要件 | レベル | 由来 |
|---|---|---|---|---|---|---|
| DOC-01 | ベストプラクティス章の内容 | 独立性・待機処理・ロケーター・パラメータ化の4トピックを解説 | FUNC-50 | REQ-10 | 統合 | 仕様/設計 |
| DOC-02 | 章の配置順序 | ベストプラクティス章が段階的レッスンより前 | FUNC-49〜58 | REQ-11 | 統合 | 仕様/設計 |
| DOC-03 | 段階的レッスンの構成 | 2〜9章の各章が解説テキストとサンプルコードを含む | FUNC-51〜58 | REQ-12 | 統合 | 仕様/設計 |
| DOC-04 | 到達性 | 0〜2章の手順だけで環境構築〜`dotnet test`成功まで到達でき、期待出力例がある | FUNC-49〜51 | NFR-03 | 統合 | 仕様/設計 |
| DOC-05 | 前提の線形性 | 各章が後続章の概念を先取りせず、冒頭に前提知識を明記 | FUNC-51〜58 | NFR-04 | 統合 | 仕様/設計 |
| DOC-06 | サンプルコードのコメント | 全サンプルコードに初心者向け説明コメント | FUNC-49〜58 | NFR-05 | 統合 | 仕様/設計 |
| DOC-07 | 実装との一致・機微情報 | 引用コードのシグネチャ・testid・URLが実装と一致、個人情報・秘密情報なし | FUNC-49〜58 | CON-06, CON-07 | 統合 | 仕様/設計 |

## 5. テスト結果（2026-09-23）

| モジュール | 件数 | 結果 |
|---|---|---|
| TESTMOD-01・02・04（LearnPlaywright.UnitTests） | 52 | 全件成功 |
| TESTMOD-03（LearnPlaywright.Tests） | 29（E2E-01×12、E2E-02×12、E2E-03〜07） | 全件成功 |
| TESTMOD-05 | 7項目 | `docs/review_log.md`参照 |

## 6. 未自動化・既知の限界

- CON-06（Public公開）・CON-07のコミット履歴メタデータ確認は、公開工程のpush前スキャン（`docs/qa_log.md`「Pushスキャン記録」）で確認する。
- 画面の見た目（レイアウト・配置）はユーザーの目視確認に委ねる。
- COMP-01の関数単体の異常系のうち、DOM要素欠落時のTypeError（FUNC-01/02/05/06/09）等はE2Eでは再現できないため未検証（Node.js等の導入時に追加を検討）。サーバー応答の異常系はE2E-05・06でRouteAsyncにより検証した。
- POST本文の上限（64K文字超で413）は自動テスト未検証（コード読解で確認済み。415はE2E-07で検証）。
- E2Eは固定ポート5080を使用する。ポートが使用中の場合は起動前に検出してテストを失敗させる（別サーバーに対する誤った成功を防ぐ）。
