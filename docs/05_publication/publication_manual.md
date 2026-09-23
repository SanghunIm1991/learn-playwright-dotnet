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

## 3. 現在の状態: 公開保留（2026-09-23）

push前スキャンで、既存の全コミットのcommitterにユーザー個人のメールアドレスが記録されていることを検出した（authorは規約どおり`ClaudeCode <noreply@anthropic.com>`）。履歴の書き換えは規約で禁止されているため、ユーザー判断により公開を保留した。再開時は次のいずれかをユーザーと決める。

- 案A: 既存の履歴は書き換えずローカルに残し、履歴を持たない公開用ブランチ（orphan）に現在のファイル一式を1コミットで作ってpushする。そのコミットのcommitterは個人情報を含まないID（GitHubのnoreplyアドレス等）とする。公開される履歴から個人情報を除くという目的は禁止操作（履歴書き換え）と同じであるため、実施前にその旨を明示して承認を得る。
- 案B: メールアドレスが公開履歴に残ることを許容して、既存履歴のままpushする。
- いずれの場合も、公開後の新しいコミットで個人メールを使わないよう、リポジトリのローカル設定（`git config user.email`）をGitHubのnoreplyアドレスにすることを検討する（設定変更はユーザー承認のうえで行う）。

## 4. push前スキャン手順（毎回・省略不可）

1. 追跡ファイルの内容: `git ls-files`の全ファイルに対し、メールアドレス・APIキー・パスワード・トークン・秘密鍵・ユーザー名を含むローカルパス等のパターンを検索する。あわせて、このPCの利用内容など個人を推測させる記述がないかを確認する。
2. コミット履歴のメタデータ: `git log --all --format='%an <%ae>'`（author）と`--format='%cn <%ce>'`（committer）の両方を確認する。
3. `.gitignore`対象（`bin/`、`obj/`、`App_Data/`等）が追跡されていないことを`git status --ignored`で確認する。
4. 結果を`docs/qa_log.md`の「Pushスキャン記録」に追記し、ユーザーに提示してからpushの承認を得る。

## 5. 公開手順（保留解除後）

1. GitHub上にリポジトリ`learn-playwright-dotnet`（Public、README・LICENSEの自動生成なし）を作成する。作成はユーザーがWeb画面で行うか、承認のうえ`gh repo create`で行う。
2. 3章で決めた方式で公開用の履歴を用意する。
3. 4章のスキャンを実施し、結果を記録・提示する。
4. ユーザーの承認を得て`git remote add origin <URL>`・`git push`を実行する。force pushは行わない。
5. GitHub上で、README・LICENSE・教材が表示されることと、不要なファイルがないことを確認する。
