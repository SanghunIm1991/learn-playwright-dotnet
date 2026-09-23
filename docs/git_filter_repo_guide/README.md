# git filter-repo の使い方（このリポジトリでの実施例つき）

このフォルダは、`git filter-repo` による**過去のコミット履歴の書き換え**を理解するための解説です。このリポジトリで実際に行った作業（2026-09-23）を題材にしています。

> **注意**: 履歴の書き換えは、全コミットのIDが変わる破壊的な操作です。必ずフルバックアップを取ってから、作業用のクローンで行ってください。公開済みのリポジトリでは、force push・共同作業者への連絡・GitHub側のキャッシュ削除依頼なども必要になります。

## フォルダ構成

| パス | 内容 | Git管理 |
|---|---|---|
| `README.md` | この解説 | 対象（公開される） |
| `private/replace-text.txt` | 実際に使った「文字列の置き換え」の指示ファイル | **対象外**（`.gitignore`で除外） |
| `private/commit-callback.py` | 実際に使った「コミットごとの加工」の処理 | **対象外**（`.gitignore`で除外） |

`private/`の2つのファイルには、消したかった記述や旧メールアドレスがそのまま入っています。そのため`.gitignore`で除外し、このPCの中だけに置いています。`git add -f`（除外を無視した強制追加）はしないでください。不要になったら`private/`ごと削除してください。

## このリポジトリで何をしたか

| 目的 | 使った機能 | 結果 |
|---|---|---|
| 旧規約で作ったコミットの名義（author=ClaudeCode、committer=個人メール）を本人のGitHub noreplyアドレスに直し、GitHubの貢献に数えられるようにする | `--commit-callback` | 95コミットの名義を変更 |
| Claudeが関わったことを記録として残す | `--commit-callback` | `Co-Authored-By: Claude <noreply@anthropic.com>`が無かった84コミットに追加 |
| 3ファイルの**過去の版**にだけ残っていた、PCの利用内容に関する記述を消す | `--replace-text` | 4通りの言い回しを、全履歴で中立な表現に置換 |

結果: 最新のファイル内容は書き換え前と1バイトも変わらず（ツリーのハッシュ値が一致）、コミット数は99→98になりました（後述）。全テストも成功しました。

## git filter-repo とは

Gitの履歴を「一度すべて書き出し、加工して、書き戻す」ツールです（内部では`git fast-export`と`git fast-import`を使っています）。Git本体には含まれず、Pythonで動く追加ツールとして導入します。以前からある`git filter-branch`より高速で安全なため、Git公式ドキュメントでも推奨されています。

主な特徴:

- **既定では、新しくcloneしたリポジトリでしか動きません。** 大切な元のリポジトリを誤って書き換えないための安全装置です。
- 書き換え後、リモートの設定（`origin`）を自動で外します。書き換えた履歴を、うっかり元のリモートへpushしないためです。
- 書き換え後、古いオブジェクトを掃除して圧縮し直します（`git gc`相当）。
- 変更前後が同じになったコミット（空のコミット）は自動で取り除きます。

## 使ったオプションの解説

### `--replace-text <ファイル>`: ファイル内容の文字列を全履歴で置き換える

指示ファイルは1行に1件で、次の書式です。

```text
消したい文字列==>置き換え後の文字列
regex:正規表現==>置き換え後の文字列
glob:ワイルドカード*==>置き換え後の文字列
消したい文字列
```

- `==>`の右側を省略すると、`***REMOVED***`に置き換わります。
- **履歴上のすべてのコミットの、すべてのファイルの内容**が対象です。最新の版だけでなく、過去の版にも適用されます。
- 今回の`private/replace-text.txt`は、上の1行目の形式（文字列をそのまま指定）で4行あります。書くときに工夫した点は次の3つです。
  - **表記の揺れをすべて並べた**: 同じ内容でも、ファイルや時期によって言い回しが違っていました。事前に全履歴を検索して、4通りの言い回しをすべて洗い出しました。
  - **文脈を含めて長めに指定した**: 短い単語だけを指定すると、関係のない箇所まで置き換わる危険があります。
  - **置き換え後を、現在のファイルの表現と完全に同じにした**: 最新の版はすでに中立な表現に直してあったので、同じ表現にそろえました。こうすると最新の版は書き換え前と同一になり、検証が簡単になります。
- 保存形式はUTF-8（BOMなし）です。

### `--commit-callback <Pythonコード>`: コミットごとに任意の加工をする

コミット1件ごとに呼ばれるPythonコードを渡します。コードの中では`commit`という変数で、そのコミットの情報を読み書きできます。値はすべて`bytes`型（`b"..."`）です。

| 属性 | 内容 |
|---|---|
| `commit.author_name` / `commit.author_email` | 作者（GitHubの貢献はこのメールアドレスで数えられる） |
| `commit.committer_name` / `commit.committer_email` | コミットした人 |
| `commit.message` | コミットメッセージ |

今回の`private/commit-callback.py`の構造は次のとおりです（実際の値は伏せています）。

```python
NEW_NAME = b"<GitHubのユーザー名>"
NEW_EMAIL = b"<ID>+<ユーザー名>@users.noreply.github.com"
OLD_EMAILS = {b"noreply@anthropic.com", b"<旧メールアドレス>"}
# 旧authorがClaudeCodeで、Co-Authored-By行が無ければ追加する（名義を変える前に判定する）
if commit.author_email == b"noreply@anthropic.com" and b"Co-Authored-By:" not in commit.message:
    commit.message = commit.message.rstrip(b"\n") + b"\n\nCo-Authored-By: Claude <noreply@anthropic.com>\n"
# 旧メールアドレスのauthor・committerを新しい名義に置き換える
if commit.author_email in OLD_EMAILS:
    commit.author_name, commit.author_email = NEW_NAME, NEW_EMAIL
if commit.committer_email in OLD_EMAILS:
    commit.committer_name, commit.committer_email = NEW_NAME, NEW_EMAIL
```

- 名義の変更には`--mailmap`という専用オプションもあります。ただ今回は「旧名義のコミットにだけトレーラーを足す」処理と組み合わせる必要がありました。`--mailmap`とコールバックのどちらが先に適用されるかに依存しないよう、1つのコールバックでまとめて処理しました。
- コマンドラインで渡すときは、**コメント行（`#`で始まる行）を取り除いて**渡しました（`grep -v '^#'`）。日本語のコメントがコマンドライン経由で文字化けし、Pythonのエラーになるのを防ぐためです。

### 今回は使わなかったが、よく使うオプション

| オプション | 用途 |
|---|---|
| `--mailmap <ファイル>` | 名義の変換表（`新しい名前 <新メール> 古い名前 <古いメール>`）で、author・committerを一括変更する |
| `--replace-message <ファイル>` | コミットメッセージの文字列を置き換える（書式は`--replace-text`と同じ） |
| `--path <パス> --invert-paths` | 指定したファイル・フォルダを全履歴から削除する（`--invert-paths`を付けないと、逆に「それ以外」を削除する） |
| `--analyze` | 書き換えはせず、履歴の中の大きなファイルや削除済みファイルなどのレポートを`.git/filter-repo/analysis/`に作る（事前調査に便利） |
| `--dry-run` | 書き換えずに、書き出した履歴をファイルとして残す（何が変わるかの確認用） |

## 実際に実行したコマンド

Windows（Git Bash）で実行しました。パスは例です。

```bash
# 1. 作業用のクローンを作る（元のフォルダには触れない）
git clone --no-local /d/LearnPlaywright /d/LearnPlaywright-rewrite

# 2. クローンの中で書き換える（git-filter-repo は専用のPython仮想環境に導入したものをフルパスで呼ぶ）
cd /d/LearnPlaywright-rewrite && "$LOCALAPPDATA/Programs/git-filter-repo/Scripts/git-filter-repo.exe" \
  --replace-text /d/git-backup/<バックアップ>/replace-text.txt \
  --commit-callback "$(grep -v '^#' /d/git-backup/<バックアップ>/commit-callback.py)"
```

出力の読み方:

```text
NOTICE: Removing 'origin' remote; ...   ← 仕様どおりリモート設定を外した
Parsed 99 commits                         ← 99コミットを読み込んだ
HEAD is now at fef99c2 ...                ← 書き換え後の最新コミット
New history written in 0.91 seconds; now repacking/cleaning...
Completely finished after 1.47 seconds.  ← 完了
```

## 書き換え後の検証

| 確認したこと | 方法 | 結果 |
|---|---|---|
| 消したい記述が残っていない | 全コミットのファイルを`git grep`で検索 | 0件 |
| 名義が直った | `git log --all --format='%an <%ae> \| %cn <%ce>'`を集計 | 全コミットがnoreplyアドレス |
| トレーラーが付いた | コミット1件ずつメッセージを確認 | 付いていないコミット0件 |
| 最新のファイル内容が変わっていない | 新旧の`git rev-parse HEAD^{tree}`を比較 | 完全一致 |
| リポジトリが壊れていない | `git fsck --full` | エラーなし |
| コミット数 | `git rev-list --all --count` | 99→98 |

**コミットが1件減った理由**: 「現在のファイルの旧記述を中立化した」コミットがありました。過去の版も同じ表現に置き換えたことで、このコミットは変更前と変更後が同じ内容になりました。そのため空のコミットとして自動で取り除かれました。どのコミットが消えたかは、新旧のコミットの件名一覧を比較して特定しました。

## 反映のしかた（`.git`の入れ替え）

作業中のエディタやツールが元のフォルダを開いていると、フォルダごとの入れ替えはできません。今回は、最新のファイル内容が完全に同じだったので、**`.git`フォルダだけを入れ替え**ました。

```bash
mv /d/LearnPlaywright/.git /d/git-backup/<バックアップ>/original.git   # 旧履歴を退避（失敗したら戻せばよい）
cp -r /d/LearnPlaywright-rewrite/.git /d/LearnPlaywright/.git            # 新しい履歴を入れる
```

入れ替え直後は、`git status`で多くのファイルが「変更あり」と表示されました。原因は、インデックスに記録されたファイルのサイズ・日時が作業フォルダと合わないことです（この環境は`core.autocrlf=true`）。`git diff`で内容の差分が無いことを確かめてから`git add -A`すると、ステージされる変更は0件で、表示は消えました。

## つまずきやすい点のまとめ

- 書き換え前に、**フルバックアップ**（`git bundle create ... --all`、フォルダ一式のzip、ハッシュ値）を**リポジトリの外**に作り、bundleからの復元テストまで済ませる。
- `git filter-repo`は**新しくcloneしたリポジトリ**で実行する。
- 消したい記述は、**表記の揺れ**まで全履歴から洗い出す。現在のファイルに「引用」として残っていないかも確認する。
- 日本語を含む検索は`LC_ALL=C.UTF-8`で行う（既定のロケールでは並べ替え等が失敗する）。
- コールバックに日本語のコメントを入れる場合は、渡すときにコメント行を除く。
- 秘密情報（APIキー等）の場合は、履歴から消すより先に、キーの無効化・再発行を行う。
- 公開済みの場合は、force pushだけでは消えきらない（フォーク・他人のクローン・GitHubのキャッシュ）。できれば初回push前に行う。

## 関連

- 手順の詳細（ユーザーとClaudeの役割分担、ツールの導入・再導入手順を含む）: Claude Codeのユーザースキル`git-history-rewrite`
- 判断の記録: `docs/qa_log.md`（2026-09-23「公開（履歴修正）」の各行）
- 公式ドキュメント: https://github.com/newren/git-filter-repo
