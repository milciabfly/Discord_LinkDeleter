![GCSICON](https://milhomescd.com/GCS_icon.png)

# Discord LinkDeleterとは
Discord LinkDeleterはDiscordサーバーにて、招待リンク or 設定したURLリンクが送信された際に自動削除するBOTです。

## できる事
- 招待URLの自動削除
- 設定したURLの自動削除
- 複数回検知した場合のUser自動タイムアウト
- 設定したURLの一覧確認

- 自動削除するURLの追加 #管理者のみ
- 自動削除するURLの削除 #管理者のみ

## 起動方法
### １．ソースコード、又は[アプリケーション](https://github.com/milciabfly/Discord_LinkDeletor/releases/tag/v1.0)のダウンロード
### ２．.envファイル、.iniファイルの設定
#### 実行ファイルがあるフォルダのルートに以下二つのファイルを作成
- .env
- config.ini
#### 作成したファイルの編集
```env
.env
DISCORD_TOKEN="{""内にディスコードトークンを設定}"
```
```ini
config.ini
[IDS]
; ギルドのID (設定必須)
GUILDID={イコールの後にサーバーIDを入力}
; 許可ロールのID
; 設定しない場合は無視
ROLEID={URL送信を許可するロールのIDを入力}

[CUSTOM_MESSAGE]
; BOTのステータス
STATUS=👀リンクの送信が禁止されています。
; URL削除時のメッセージ (改行 \n)
DELETE_MESSAGE=> Discord招待リンクを検知したため自動で削除しました。\n> このサーバーに招待を送信することは禁止されています。
; CUSTOM URL削除時のメッセージ (改行 \n)
CUSTOM_DELETE_MESSAGE=> 禁止されているリンクを検知したため自動で削除しました。

[TIMEOUT]
; ユーザをタイムアウトするまでの回数
MAX_TIMEOUT_ATTEMPTS=3
; ユーザのタイムアウト時間
TIMEOUT_DURATION=2
```
### ２ー２．パッケージをインストールする（ソースコードをダウンロードした場合）
```powershell
Install-Package Discord.net
Install-Package DotNetEnv
Install-Package ini-parser
Install-Package Microsoft.Extensions.DependencyInjection
```
### ３．環境に合わせたやり方で起動する
