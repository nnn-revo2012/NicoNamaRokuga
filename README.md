# NicoNamaRokuga

ニコニコ生放送の生放送・タイムシフトを録画するツールです。

# 特徴

- GUI(Windows Forms)使用。  
- 生放送・タイムシフト対応。  
- タイムシフトはプレアカ2倍速・一般アカ1.25倍速で録画します。  
- リアルタイム時の追っかけ録画には対応していません。  
- livedlの録画ファイルと互換性があります。  
- ffmpegなどの外部プログラムを使って録画することもできます。  
- 2020/4/4現在 公式生放送のみrtmpで録画可能です(ＴＳは全て不可)。  

# 開発環境

- Windows 7 以降  
- Microsoft Visual Studio 2015 以降  
- .NET 4.6.1  

# パッケージ

以下のパッケージをインストールしてください。  

- SnkLib.App.CookieGetter 2.4.0  
https://github.com/nnn-revo2012/SnkLib.App.CookieGetter/releases/tag/v2.4.0  
namoshikaさんのSnkLib.App.CookieGetter(https://github.com/namoshika/SnkLib.App.CookieGetter)を元にguest-nicoさんがGoogleChrome80対応されたもの(https://github.com/guest-nico/SnkLib.App.CookieGetter)です。  
※同時にインストールする Json.net は11.0.2以上にしてください  

- WebSocket4Net 0.15.2  
https://www.nuget.org/packages/WebSocket4Net  


# 実行方法

実行ファイル・ライブラリーを同じフォルダーに入れて実行してください。  
また、外部プログラムも同じフォルダーに入れてください。  

# ライセンス
- NicoNamaRokuga
https://github.com/nnn-revo2012/NicoNamaRokuga  
Copyright (c) 2019 nnn-revo2012  
Released under the GNU General Public License v3.0  


- SnkLib.App.CookieGetter  
https://github.com/namoshika/SnkLib.App.CookieGetter   
Copyright (c) 2014 namoshika.  
Released under the GNU Lesser GPL  
本ソフトウェアでは上記にGoogleChrome80対応の修正を行ったものを使用しております。  
https://github.com/guest-nico/SnkLib.App.CookieGetter  
Copyright (c) 2019 guest-nico  

- WebSocket4Net  
https://github.com/kerryjiang/WebSocket4Net  
Copyright (c) 2012 kerryjiang  
Released under the Apache License 2.0  

- Json.NET  
https://www.newtonsoft.com/json  
Copyright (c) 2007 James Newton-King  
Released under the MIT License  

- BouncyCastle  
http://www.bouncycastle.org/csharp/  
Copyright (c) 2000-2020 Legion of the Bouncy Castle Inc.  
Released under the MIT License  

- SQLite  
https://www.sqlite.org/index.html  
Released into the Public Domain  

- livedl  
https://himananiito.hatenablog.jp/entry/livedl  
Copyright (c) 2018 himananiito  
Released under the MIT License  
※ファイル形式やフォーマットを使用しております。  

