# 申し送り事項

## 現在の申し送り

- **公開済み（2026-09-23）**: https://github.com/SanghunIm1991/learn-playwright-dotnet （Public、ブランチmain、MIT）。以後のpushも毎回push前スキャン（`docs/05_publication/publication_manual.md` 4章）を行い、都度承認を得る（CLAUDE.md「push運用方針」）。
- **ユーザー側の後片付け（削除時期はユーザーが判断、Claudeは削除しない）**:
  - 作業用クローン2つ（リポジトリと同じドライブの`LearnPlaywright-rewrite`・`LearnPlaywright-rewrite2`）
  - リポジトリ外のバックアップフォルダ（bundle・zip・旧`.git`2つ・指示ファイル。旧履歴と旧メールアドレスを含む。場所は`git-history-rewrite`実施時にユーザーと決めたもの）
  - `docs/git_filter_repo_guide/private/`（伏せ字なしの実物、.gitignore対象）
  - git-filter-repo（ユーザー用フォルダの専用仮想環境）は他リポジトリでも使うため残す想定。削除方法は`git-history-rewrite`スキルの手順0
- **このセッションで変わった全プロジェクト共通のルール**（ユーザーレベルのスキル、次セッションから有効）:
  - `git-conventions`: author・committerはユーザー本人（`--author`を指定せずGit設定を使う。`user.email`はGitHubのnoreplyアドレスに設定済み）。Claudeの関与は`[claude]`接頭辞と`Co-Authored-By`トレーラー。最初のコミット前に`git config user.email`がnoreplyかを確認する
  - `git-history-rewrite`（新規）: 過去の履歴の修正。書き換えとforce pushはユーザーが`!`付きで実行し、Claudeは調査・バックアップ・指示ファイル作成・検証を担当する
- **push状況**: 2026-09-24に再レビュー分をpush済み（`dbf9f00`まで）。以後のpushも毎回push前スキャンを行い、都度承認を得る。
- 要確認の独自判断: 読み込み時に保存データの`radio`が`null`なら全ラジオの選択を解除する仕様（REQ-05解釈、`docs/qa_log.md`の実装レビュー指摘#8）。

## 過去の申し送り（解決済み）

- **後日の再レビュー**: 2026-09-24、保留一覧の5項目すべてを再レビューし収束（`docs/review_log.md`末尾参照）。
- **公開の保留（コミット履歴のcommitterに個人メールアドレス）**: 2026-09-23、Git規約を改訂（author・committerはユーザー本人のnoreplyアドレス）し、`git-history-rewrite`スキルの手順で過去の履歴を修正して解消。
- **テスト工程完了→公開工程移行のゲート承認**: 2026-09-23、ユーザーが画面の見た目・教材を確認し承認（教材0章への環境構築手順の追記指示に対応済み）。工程名は「デプロイ」から「公開」に変更。
- **関数設計COMP-06の論理設計収束後の中断**: 2026-09-23、次セッションで再開し、ユーザー指示によりCOMP-06可読性向上フェーズを保留して実装・テスト工程を完了。
- **.NET 10 SDKの導入待ち**: 2026-09-23、ユーザーが公式インストーラーで.NET 10 SDK（10.0.401）を導入完了。`global.json`で固定済み。
- **関数設計COMP-04の論理レビュー指摘対応**: 2026-09-23、REQ-09の誤付与削除・qa_log記録漏れ・コード例追加・見出し修正の4件を対応し収束（v1.2）。
- **COMP-02/COMP-05間のサーバーポート指定に関する設計矛盾**: 2026-09-23、要件定義書NFR-02をv1.4へ改訂、COMP-02関数設計書v1.6で整合を確保。
