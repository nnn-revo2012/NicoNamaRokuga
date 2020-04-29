using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using NicoNamaRokuga.Net;
using NicoNamaRokuga.Rec;

namespace NicoNamaRokuga.Proc
{
    public class ExecPsInfo
    {
        public string Exec { get; set; }
        public string Arg { get; set; }
        public string Sdir { get; set; }
        public string Sfile { get; set; }
        public string Sfolder { get; set; }
        public string BreakKey { get; set; }
        public int    Seq { get; set; }
        public string Protocol { get; set; }
        public string Quality { get; set; }
        public string SaveFile { get; set; }
        public string Sqlite3File { get; set; }
        public string Ext { get { return (Protocol == "rtmp") ? ".flv" : ".ts"; } }
        public string Xml { get { return ".xml"; } }

        //保存ファイルにシーケンスNoをつける
        public static string GetSaveFileNum(ExecPsInfo epi)
        {
            var ff = epi.Sdir.TrimEnd('\\') + "\\" + epi.Sfolder.Trim('\\') + "\\" + epi.Sfile;

            //同名ファイル名がないかチェック
            while (IsExistFile(ff, epi.Seq, epi.Ext, epi.Xml)) ++epi.Seq;

            return ff + epi.Seq.ToString();
        }

        //実行ファイル用の引数(argumentを設定)
        public static string SetOption(ExecPsInfo epi, string para)
        {
            var result = epi.Arg;
            var ff = epi.SaveFile + epi.Ext;
            var headers = string.Empty;

            result = result.Replace("%HEADERS%", headers);
            result = result.Replace("%PARA%", para);
            result = result.Replace("%FILE%", ff);

            return result;
        }

        //同名ファイル名がないかチェック
        public static bool IsExistFile(string file, int seq, string ext1, string ext2)
        {
            var fn1 = file + seq.ToString() + ext1;
            var fn2 = file + seq.ToString() + ext2;

            return (!File.Exists(fn1) && !File.Exists(fn2)) ? false : true;
        }

        //Sqlite3用の保存ファイル名
        public static string GetSaveFileSqlite3(ExecPsInfo epi)
        {
            return epi.Sdir.TrimEnd('\\') + "\\" + epi.Sfolder.Trim('\\') + "\\" + epi.Sfile;
        }

        //Sqlite3の保存ファイルにシーケンスNoをつける
        public static string GetSaveFileSqlite3Num(ExecPsInfo epi)
        {
            int idx = epi.Sqlite3File.IndexOf(".sqlite3");
            if (idx < 0) return null;

            var ff = epi.Sqlite3File.Substring(0, idx) + "-";

            //同名ファイル名がないかチェック
            while (IsExistFile(ff, epi.Seq, epi.Ext, epi.Xml)) ++epi.Seq;

            return ff + epi.Seq.ToString();
        }

        //保存ディレクトリーがなければ作る
        public static bool MakeRecDir(ExecPsInfo epi)
        {
            var result = false;

            var s = epi.Sdir.TrimEnd('\\') + "\\" + epi.Sfolder.Trim('\\');
            if (!Directory.Exists(s))
            {
                //フォルダー作成
                Directory.CreateDirectory(s);
                result = true;
            }
            else
            {
                result = true;
            }
            return result;
        }

    }

    public class ExecProcess : EProcess, IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        //public volatile int PsStatus = -1; //実行ファイルの状態
        private Process _ps = null;

        private NicoNetComment _nNetComment = null;   //WebSocket(Comment)
        private BroadCastInfo _bci = null;
        private RetryInfo _ri = null;
        private Form1 _form = null;

        public ExecProcess(Form1 fo, BroadCastInfo bci, NicoNetComment nNetComment, RetryInfo ri)
        {
            IsDebug = false;

            PsStatus = -1;
            this._nNetComment = nNetComment;
            this._bci = bci;
            this._ri = ri;
            this._form = fo;
        }

        ~ExecProcess()
        {
            this.Dispose();
        }

        public override void ExecPs(string exefile, string argument)
        {
            try
            {
                if (Form1.props.IsVideo)
                {
                    //ファイルの実行(ps)
                    Process _process1 = new Process();
                    _ps = _process1;
                    _ps.StartInfo.FileName = exefile;
                    _ps.StartInfo.Arguments = argument;

                    _ps.StartInfo.UseShellExecute = false;
                    _ps.StartInfo.CreateNoWindow = true;

                    // 標準出力を受信する
                    _ps.StartInfo.RedirectStandardOutput = true;
                    _ps.StartInfo.RedirectStandardError = true;
                    _ps.OutputDataReceived += receivedPs;
                    _ps.ErrorDataReceived += receivedErrorPs;

                    // 標準入力
                    _ps.StartInfo.RedirectStandardInput = true;

                    // プロセスが終了したときに Exited イベントを発生させる
                    _ps.EnableRaisingEvents = true;
                    // Windows フォームのコンポーネントを設定して、コンポーネントが作成されているスレッドと
                    // 同じスレッドで Exited イベントを処理するメソッドが呼び出されるようにする
                    _ps.SynchronizingObject = _form;
                    // プロセス終了時に呼び出される Exited イベントハンドラの設定
                    _ps.Exited += exitedPs;

                    _ps.Start();
                    _ps.Refresh();
                    _ps.PriorityClass = ProcessPriorityClass.BelowNormal;

                    //中断ボタンに変更
                    _form.AddLog(string.Format("実行ファイル: {0}", _ps.StartInfo.FileName), 9);
                    _form.AddLog(string.Format("パラメーター: {0}", _ps.StartInfo.Arguments), 9);
                    _form.AddLog("プロセス実行中です。", 1);

                }
                PsStatus = 0; //実行中
                //EnableButton(false);

                if (Form1.props.IsComment)
                {
                    if (!_bci.IsTimeShift())
                    {
                        if (_nNetComment.WsStatus < 1)
                        {
                            while (_nNetComment.WsStatus != 0) ;
                            _nNetComment.StartGetComment();
                        }
                    }
                    else if (_bci.IsTimeShift() && !_ri.IsRetry)
                    {
                        if (_nNetComment.WsStatus < 1)
                        {
                            while (_nNetComment.WsStatus != 0) ;
                            _nNetComment.StartGetTSComment();
                        }
                    }
                }
                if (Form1.props.IsVideo)
                {
                    _ps.BeginOutputReadLine();
                    _ps.BeginErrorReadLine();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }


        private void receivedPs(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string text = e.Data + "\r\n";
                    _form.AddExecLog(text);
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(receivedPs), Ex);
            }
        }

        private void receivedErrorPs(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string text = e.Data + "\r\n";
                    _form.AddExecLog(text);
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(receivedErrorPs), Ex);
            }
        }

        // プロセスの終了を捕捉する Exited イベントハンドラ
        private void exitedPs(object sender, EventArgs e)
        {
            try
            {
                var proc = (Process)sender;

                _form.AddLog(string.Format("プロセス終了しました。コード: {0} ", proc.ExitCode), 1);
                PsStatus = (proc.ExitCode == 0) ? 1 : 2; //1:正常終了 2:異常終了
                //EnableButton(true);
                _ps.CancelOutputRead(); // 使い終わったら止める
                _ps.CancelErrorRead();
                if (_ps.HasExited)
                {
                    _ps.Dispose();
                    _ps = null;
                }

                //生放送の場合プロセスが終了したらコメントサーバーを切断する。
                if (Form1.props.IsComment)
                {
                    if (!_bci.IsTimeShift() && _nNetComment.WsStatus == 0)
                    {
                        _nNetComment?.Close();
                        _form.AddLog("コメントファイル出力終了", 1);
                        _nNetComment.EndXmlDoc();
                        _nNetComment?.Dispose();
                        _nNetComment.WsStatus = 1;  //再接続なし
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(exitedPs), Ex);
            }
        }

        public override void BreakProcess(string breakkey) 
        {
            if (_ps != null && !_ps.HasExited)
            {
                if (string.IsNullOrEmpty(breakkey))
                {
                    _ps.Kill();
                }
                else
                {
                    _ps.StandardInput.WriteLine(breakkey);
                }
                _ps.StandardInput.Close();
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _ps?.Dispose();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            //GC.SuppressFinalize(this);
        }
    }
}
