# git filter-repo の --commit-callback に渡した処理（実際の名義・メールアドレスは伏せ字にした版）。
# 実行時は grep -v '^#' でコメント行を除いて渡すため、コード部分には日本語を書かない。
# 1) 旧規約で author=ClaudeCode だったコミットに、Co-Authored-By 行がなければ追加する
# 2) 旧 author（ClaudeCode）と旧 committer（個人メール）を、GitHubのnoreplyアドレスの名義に置き換える
NEW_NAME = b"<GitHubのユーザー名>"
NEW_EMAIL = b"<ID>+<ユーザー名>@users.noreply.github.com"
OLD_EMAILS = {b"noreply@anthropic.com", b"<旧committerの個人メールアドレス>"}
if commit.author_email == b"noreply@anthropic.com" and b"Co-Authored-By:" not in commit.message:
    commit.message = commit.message.rstrip(b"\n") + b"\n\nCo-Authored-By: Claude <noreply@anthropic.com>\n"
if commit.author_email in OLD_EMAILS:
    commit.author_name, commit.author_email = NEW_NAME, NEW_EMAIL
if commit.committer_email in OLD_EMAILS:
    commit.committer_name, commit.committer_email = NEW_NAME, NEW_EMAIL
