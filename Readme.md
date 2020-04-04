# NicoNamaRokuga

ニコニコ生放送の生放送・タイムシフトを録画するツールです。

# 特徴

- GUI(Windows Forms)使用。  
- 生放送・タイムシフト対応。  
- livedlの録画ファイルと互換性があります。  
- ffmpegなどの外部プログラムを使って録画することもできます。  
- 2020/4/4現在 公式生放送のみrtmpで録画可能です(ＴＳは全て不可)。  

# 開発環境

- Windows 7以降  
- Visual Studio Express 2015  
- .NET 4.6.1  

# パッケージ

以下のパッケージをインストールしてください。  

- SnkLib.App.CookieGetter 2.3.1  
https://www.nuget.org/packages/SnkLib.App.CookieGetter/  
https://www.nuget.org/packages/SnkLib.App.CookieGetter.Forms/  
※同時にインストールされる Json.net は11.0.2以上  

- WebSocket4Net 0.15.2  
https://www.nuget.org/packages/WebSocket4Net  

- BouncyCastle 1.8.1  
https://www.nuget.org/packages/bouncycastle/  
※BouncyCastle.NetCore は署名されていないので使わない  

# 実行方法

実行ファイル・ライブラリーを同じフォルダーに入れて実行してください。  
また、外部プログラムも同じフォルダーに入れてください。  

# ライセンス

- SnkLib.App.CookieGetter  
https://github.com/namoshika/SnkLib.App.CookieGetter   
Copyright (c) 2014 namoshika.さん  
Released under the GNU Lesser GPL  

- WebSocket4Net  
https://github.com/kerryjiang/WebSocket4Net  
kerryjiangさん  
Apache License 2.0   

- Json.NET  
https://www.newtonsoft.com/json   
Copyright (c) 2007 James Newton-Kingさん  
MIT License  

- FFmpeg  

- livedl  
https://himananiito.hatenablog.jp/entry/livedl  
Copyright (c) 2018 himananiitoさん  
MIT License  

