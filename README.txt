===============================================================================
【タイトル】 NicoNamaRokuga
【ファイル】 NicoNamaRokuga.exe
【作成月日】 2020/07/06
【著 作 者】 nnn-revo2012
【開発環境】 Microsoft Windows 10
             Microsoft Visual Studio 2015
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
・タイムシフトはプレアカ2倍速・一般アカ1.25倍速で録画します。
・リアルタイム時の追っかけ録画には対応していません。
・録画されたファイルはlivedlの録画ファイルと互換性があります。
・ffmpegなどの外部プログラムを使って録画することもできます。
・2020/4/4現在 公式生放送のみrtmpで録画可能です(ＴＳは全て不可)。

■インストール方法
適当なフォルダにzipファイルの中のファイルを全て解凍してください。解凍したらその中のNicoNamaRokuga.exe を実行してください。
※ダウンロード時や実行時にウイルスやマルウェアの警告が出る可能性があります。当方でウイルスチェックは行っておりますがあらかじめご了承ください。
外部プログラムを使って録画する場合は別途ffmpeg.exeを入手して本ソフトウェアと同じフォルダーにコピーしてください。

■アンインストール方法
アンインストールの際は NicoNamaRokuga.exe の入っているフォルダごと削除してください。

■使用方法
1.NicoNamaRokuga.exeを起動する。
2.ツールバーにあるツール(T)→オプション(O)をクリックし、アカウント設定のタブの「ブラウザーのクッキーを共有する」を選び
　ニコニコにログイン中のブラウザーを選択する。または「このツールでログインする」を選び、メールアドレスとパスワードを入力する。
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
Copyright (c) 2014 namoshika.
Released under the GNU Lesser GPL
本ソフトウェアでは上記にGoogleChrome80対応の修正を行ったものを使用しております。
https://github.com/guest-nico/SnkLib.App.CookieGetter
Copyright (c) 2019 guest-nico

・WebSocket4Net
https://github.com/kerryjiang/WebSocket4Net
Copyright (c) 2012 kerryjiang
Released under the Apache License 2.0

・Json.NET
https://www.newtonsoft.com/json
Copyright (c) 2007 James Newton-King
Released under the MIT License

・BouncyCastle
http://www.bouncycastle.org/csharp/
Copyright (c) 2000-2020 Legion of the Bouncy Castle Inc.
Released under the MIT License

・SQLite
https://www.sqlite.org/index.html
Released into the Public Domain

・livedl
https://himananiito.hatenablog.jp/entry/livedl
Copyright (c) 2018 himananiito
Released under the MIT License
※ファイル形式やフォーマットを使用しております。

■更新履歴
2020/07/06　Version 0.1.1.02
リリース