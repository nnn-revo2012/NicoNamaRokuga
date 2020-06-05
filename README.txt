===============================================================================
【タイトル】 NicoNamaRokuga
【ファイル】 NicoNamaRokuga.exe
【作成月日】 2020/06/05
【著 作 者】 nnn-revo2012
【開発環境】 Microsoft Windows 10
             Microsoft Visual Express 2015 Express for Windows Desktop
【動作環境】 Microsoft Windows 10 / Windows 8.1 / Windows 7
             .NET Framework 4.6.1
【推奨環境】 Microsoft Windows 10
【配布形態】 フリーウェア
【Web Site】 https://github.com/nnn-revo2012/NicoNamaRokuga
【 連絡先 】 要望やバグ報告等はgithubまで
             その他　nnn_revo2012@yahoo.co.jp　
===============================================================================

■説明
・ニコニコ生放送リアルタイム・タイムシフト放送を録画します。
・GUI(Windows Forms)使用。
・録画されたファイルはlivedlの録画ファイルと互換性があります。
・ffmpegなどの外部プログラムを使って録画することもできます。
・2020/4/4現在 公式生放送のみrtmpで録画可能です(ＴＳは全て不可)。

■インストール方法
適当なフォルダにzipファイルの中のファイルを全て解凍してください。解凍したらその中のNicoNamaRokuga.exe を実行してください。
※ダウンロード時や実行時にウイルスやマルウェアの警告が出る可能性があります。ウイルスチェックは行っておりますがご了承ください。
外部プログラムを使って録画する場合は別途ffmpeg.exeを入手して本ソフトウェアと同じフォルダーにコピーしてください。

■アンインストール方法
アンインストールの際は NicoNamaRokuga.exe の入っているフォルダごと削除してください。

■使用方法
1.NicoNamaRokuga.exeを起動する。
2.ツールバーにあるツール(T)→オプション(O)をクリックし、アカウント設定のタブの「ブラウザーのクッキーを共有する」を選び
　ニコニコにログイン中のブラウザーを選択する（こちらを推奨）。または「このツールでログインする」を選び、メールアドレスと
　パスワードを入力する。
3.放送URLに録画したい放送URL(https://live.nicovideo.jp/watch/lv******またはlv******)を入力する。
4.タイムシフトを録画する場合、「タイムシフト開始時間」に時間を入力するとその時間から録画開始します。
5.「録画開始」を押すと録画が始まる。
6.録画したファイルはツールバーのファイル(F)→録画フォルダーを開くでフォルダーが開きます。
　録画ファイルはlivedlと同じくDB(sqlite3)ファイルになっていて、そのままでは再生することはできません（重要）。
7.録画ファイルをNicoNamaRokuga.exeの画面にドラッグアンドドロップしてください。
8.映像ファイル(*.ts)とコメントファイル(*.xml)が作成されます。

■動作環境
.Net Framework 4.6.1以降が必要です。Windows 10では標準でインストールされています。
https://www.microsoft.com/ja-jp/download/details.aspx?id=49981

■免責事項
本ソフトウェアを利用して発生した如何なる損害について著作者は一切の責任を負いません。
また著作者はバージョンアップ、不具合修正の義務を負いません。

■ライセンス関係
・NicoNamaRokuga
https://github.com/nnn-revo2012/NicoNamaRokuga
Copyright (c) 2019 nnn-revo2012
Released under the GNU General Public License v3.0

・SnkLib.App.CookieGetter
https://github.com/namoshika/SnkLib.App.CookieGetter
Copyright (c) 2014 namoshika.さん
Released under the GNU Lesser GPL
本ソフトウェアでは上記にGoogleChrome80対応の修正を行ったものを使用しております。
https://github.com/guest-nico/SnkLib.App.CookieGetter
Copyright (c) 2019 guest-nicoさん

・WebSocket4Net
https://github.com/kerryjiang/WebSocket4Net
kerryjiangさん
Apache License 2.0

・SQLite
https://www.sqlite.org/index.html
Public Domain

・Json.NET
https://www.newtonsoft.com/json
Copyright (c) 2007 James Newton-Kingさん
MIT License

・livedl
https://himananiito.hatenablog.jp/entry/livedl
Copyright (c) 2018 himananiitoさん
MIT License
※ファイル形式やフォーマットを使用しております。

■更新履歴
2020/06/05　Version 0.1.0.20
リリース