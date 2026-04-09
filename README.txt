===============================================================================
【タイトル】 NicoNamaRokuga
【ファイル】 NicoNamaRokuga.exe
【作成月日】 2026/04/07
【著 作 者】 nnn-revo2012
【開発環境】 Microsoft Windows 11
             Microsoft Visual Studio 2019
【動作環境】 Microsoft Windows 10/11
             .NET Framework 4.8
【推奨環境】 Microsoft Windows 11
【配布形態】 フリーウェア
【Web Site】 https://github.com/nnn-revo2012/NicoNamaRokuga
【 連絡先 】 要望やバグ報告等はgithubまで
             その他　nnn_revo2012@yahoo.co.jp　
===============================================================================

■説明
・ニコニコ生放送の生放送・タイムシフトの**コメントのみ**を保存するツールです。
・GUI(Windows Forms)使用。
・リアルタイム時の追っかけ録画には対応していません。
・録画されたファイルはlivedlの録画ファイルと互換性があります。

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
8.コメントファイル(*.xml)が作成されます。

■動作環境
.Net Framework 4.8が必要です。Windows 10/11では標準でインストールされているので新たにインストールする必要はありません。
https://dotnet.microsoft.com/ja-jp/download/dotnet-framework/thank-you/net48-web-installer
■免責事項
本ソフトウェアを利用して発生した如何なる損害について著作者は一切の責任を負いません。
また著作者はバージョンアップ、不具合修正の義務を負いません。

■ライセンス関係
・NicoNamaRokuga
https://github.com/nnn-revo2012/NicoNamaRokuga
Copyright (c) 2026 nnn-revo2012
Released under the MIT License

・SnkLib.App.CookieGetter
https://github.com/namoshika/SnkLib.App.CookieGetter
Copyright (c) 2014 namoshika.
Released under the GNU Lesser GPL
本ソフトウェアでは上記にGoogleChrome80対応の修正他を行ったものを使用しております。  
https://github.com/guest-nico/SnkLib.App.CookieGetter  
Copyright (c) 2019 guest-nico  
Released under the GNU Lesser GPL  
本ソフトウェアでは上記に更に追加修正を行ったものを使用しております。  
https://github.com/nnn-revo2012/SnkLib.App.CookieGetter
Copyright (c) 2019 nnn-rev02012
Released under the GNU Lesser GPL  

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
2026/04/07　Ver 0.1.2.03
オプション追加、ログイン機能修正
・ログイン時２段階認証対応
・オプションでuser_sessionを指定した場合の処理追加

2026/03/27　Ver 0.1.2.02
オプション追加、ログイン機能修正
・オプションを開く際に「クッキーファイルを直接指定」のチェックボックスがオンでも
　選択ボタン他がアクティブにならないのを修正
・ログイン機能の「チェック」ボタン処理を作成
・ログイン時にaccount.dbのuser_session_secureを読み書きしてた部分を削除
・ログイン時にaccount.dbのuser_sessionがNULLだった場合エラーになるのを修正

2026/03/20　Ver 0.1.2.01
・LICENSEをMIT LICENSEに変更
・新サーバーのコメント読み込み機能作成

2025/06/09　Ver 0.1.1.30(2025/06/09)
・websocket(comment)を削除
・CommentInfo()、CommentControl()を削除
・NicoStartMessage()を新規作成
・NicoStartMessage.csにコメント関連の一部メソッドを移動
・Form1.csやコメント関連の不要なロジック整理(リファクター)
・NicoLiveNetの各メソッドをSirreneのNicoVideoNetと同じようにリファクター
・websocketのstartWatching/changeStreamコマンドにaccessRightMethodを追加
・cookieContainerにchangeStreamのcookieを追加する処理追加
・websocketのroomレス廃止、messageServerレス追加

2024/03/26　Ver 0.1.1.29
・SnkLib.App.CookieGetter、SnkLib.App.CookieGetter.Forms 修正
・ブラウザ一覧表示時のネットアクセスにUAを追加(ニコニコ側仕様変更)
・ブラウザ一覧表示時にChromium系ブラウザのcookieファイルを開けない場合の表示追加

2024/03/13　Ver 0.1.1.28
・ニコ生のwebsocketがセキュリティ強化された(2024/02/14メンテ後)ので修正

2023/06/24　Ver 0.1.1.27
・CookieGetter、CookieGetter.Forms をアップデート

commit 14987ae10668d8c795446c4e67d9bd5a0e0becd2
Author: nnn-revo2012 <nnn_revo2012@yahoo.co.jp>
Date:   Wed Apr 19 18:53:40 2023 +0900

2023/04/19　Ver 0.1.1.26
・Update README.txt README.md
・BouncyCastle 1.8.9 にアップデート(アップデートしないと実行時エラーになる)
・.NET Framework 4.8 にアップデート
・Newtonsoft.Json 13.0.3 にアップデート

2022/06/24　Ver 0.1.1.25
・CookieGetter、CookieGetter.Forms をアップデート
・Newtonsoft.Json を 13.0.1 にアップデート
・ID/PASSログインのロジック修正、アカウントのpremium/normalが取得できなかったのを修正

2022/05/13　Ver 0.1.0.23
・CookieGetterのバージョンアップ

2022/04/14　Ver 0.1.1.22
・ID/PASSログイン時のURLを変更

2021/12/22　Ver 0.1.1.21
・2020/07/06以降のニコ生のサーバー側仕様変更に対応
・プログラムのリファクタリング
・バグ修正

2020/07/06　Ver 0.1.1.02
リリース