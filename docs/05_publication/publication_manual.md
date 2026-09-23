# 公開手順書（GitHubへのソースコード公開）

## 1. 文書情報

| 項目 | 内容 |
|---|---|
| 文書名 | LearnPlaywright 公開手順書 |
| 版数 | v1.0 |
| 作成日 | 2026-09-23 |
| 作成者 | ClaudeCode |

本プロジェクトの公開工程は、ソースコード一式をGitHubのPublicリポジトリに置くことだけを指す。サーバーへの配置など、アプリを実行可能な状態で公開することは行わない。

## 2. 合意済み事項（`docs/qa_log.md`参照）

| 項目 | 内容 |
|---|---|
| リポジトリ名 | `learn-playwright-dotnet` |
| 公開範囲 | Public |
| ライセンス | MIT（`LICENSE`、著作権者表記は個人名を避け「learn-playwright-dotnet contributors」） |
| push承認 | 都度確認 |

## 3. 経緯と現在の状態

- 2026-09-23: 最初のpush前スキャンで、旧Git規約によりコミットのcommitterに個人のメールアドレスが記録されていたことが分かり、公開をいったん保留した。
- 同日: Git規約を改訂し（author・committerはユーザー本人のGitHub noreplyアドレス、Claudeの関与は`[claude]`接頭辞と`Co-Authored-By`で示す）、`git-history-rewrite`スキルの手順（フルバックアップ→ユーザーが`git filter-repo`で書き換え→Claudeが検証）で初回push前に過去の履歴を修正した。現在の履歴には個人のメールアドレスは含まれない（詳細は`docs/qa_log.md`、手法の解説は`docs/git_filter_repo_guide/README.md`）。
- 同日: GitHubに空のPublicリポジトリ`learn-playwright-dotnet`を作成（`gh repo create`、README・LICENSEの自動生成なし）し、ブランチ名を`main`に変更。公開前の最終レビュー（サブエージェント、静的確認）で重大な指摘なし。
- 同日: ユーザーの承認を得て`git push -u origin main`で公開した（https://github.com/SanghunIm1991/learn-playwright-dotnet ）。以後のpushも毎回4章のスキャンを行い、都度承認を得る。

## 4. push前スキャン手順（毎回・省略不可）

1. 追跡ファイルの内容: `git ls-files`の全ファイルに対し、メールアドレス・APIキー・パスワード・トークン・秘密鍵・ユーザー名を含むローカルパス等のパターンを検索する。あわせて、このPCの利用内容など個人を推測させる記述がないかを確認する。
2. コミット履歴のメタデータ: `git log --all --format='%an <%ae>'`（author）と`--format='%cn <%ce>'`（committer）の両方を確認する。
3. `.gitignore`対象（`bin/`、`obj/`、`App_Data/`等）が追跡されていないことを`git status --ignored`で確認する。
4. 結果を`docs/qa_log.md`の「Pushスキャン記録」に追記し、ユーザーに提示してからpushの承認を得る。

## 5. 公開手順

1. GitHub上にリポジトリ`learn-playwright-dotnet`（Public、README・LICENSEの自動生成なし）を作成する。作成はユーザーがWeb画面で行うか、承認のうえ`gh repo create`で行う。
2. 3章で決めた方式で公開用の履歴を用意する。
3. 4章のスキャンを実施し、結果を記録・提示する。
4. ユーザーの承認を得て`git remote add origin <URL>`・`git push`を実行する。force pushは行わない。
5. GitHub上で、README・LICENSE・教材が表示されることと、不要なファイルがないことを確認する。
