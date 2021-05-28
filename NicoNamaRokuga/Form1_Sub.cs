using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;


using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;
using NicoNamaRokuga.Proc;
using NicoNamaRokuga.Rec;

namespace NicoNamaRokuga
{
    public partial class Form1 : Form
    {
        //ログウインドウ初期化
        private void ClearLog()
        {
            this.Invoke(new Action(() =>
            {
                lock (lockObject)
                {
                    listBox1.Items.Clear();
                    listBox1.TopIndex = 0;
                }
            }));
        }

        //ログウインドウ書き込み
        public void AddLog(string s, int num)

        {
            this.Invoke(new Action(() =>
            {
                lock (lockObject)
                {
                    if (num == 1)
                    {
                        if (listBox1.Items.Count > 50)
                        {
                            listBox1.Items.RemoveAt(0);
                            listBox1.TopIndex = listBox1.Items.Count - 1;
                        }
                        listBox1.Items.Add(s);
                        listBox1.TopIndex = listBox1.Items.Count - 1;
                    }
                    else if (num == 2) //エラー
                    {
                        MessageBox.Show(s + "\r\n",
                            "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (num == 3) //注意
                    {
                        MessageBox.Show(s + "\r\n",
                            "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    if (props.IsLogging && LogFile != null)
                        System.IO.File.AppendAllText(LogFile, System.DateTime.Now.ToString("HH:mm:ss ") + s + "\r\n");
                }
            }));
        }

        //実行プロセスのログ書き込み
        public void AddExecLog(string s)
        {
            this.Invoke(new Action(() =>
            {
                lock (lockObject2)
                {
                    textBox7.Text = s;
                    if (props.IsLogging && LogFile2 != null)
                        System.IO.File.AppendAllText(LogFile2, System.DateTime.Now.ToString("HH:mm:ss ") + s);
                }
            }));
        }

        //data-propsをファイルに書き込み
        public void AddDataProps(string s)
        {
            this.Invoke(new Action(() =>
            {
                if (props.IsLogging && LogFile3 != null)
                {
                    System.IO.File.AppendAllText(LogFile3, JObject.Parse(s).ToString());
                }
            }));
        }

        private void ClearHosoData()
        {
            this.Invoke(new Action(() =>
            {
                label2.Text = "";
                label3.Text = "";
                label4.Text = "";
                label5.Text = "";
                label6.Text = "";
                label7.Text = "";
                label8.Text = "";
                label9.Text = "";
            }));
        }

        //放送情報を表示
        private void DispHosoData(BroadCastInfo bci)
        {
            this.Invoke(new Action(() =>
            {
                label2.Text = bci.Title;
                label3.Text = Props.GetProviderType(bci.Provider_Type);
                label4.Text = bci.Community_Title + "(" + bci.Community_Id + ")";
                label5.Text = bci.Provider_Name + "(" + bci.Provider_Id + ")";
                label6.Text = Props.GetUnixToDateTime(bci.Begin_Time).ToString() + " 開始";
                label8.Text = "生放送";
                if (bci.IsTimeShift())
                {
                    label7.Text = Props.GetUnixToDateTime(bci.End_Time).ToString() + " 終了";
                    label8.Text = "タイムシフト";
                }

            }));
        }

        //画質情報を表示
        public void DispQuality(string s)
        {
            this.Invoke(new Action(() =>
            {
                label9.Text = Props.Quality[Props.ParseQTypes(s)];
            }));
        }

        public void EnableButton(bool flag)
        {
            //true 中断→録画開始
            this.Invoke(new Action(() =>
            {
                if (flag)
                {
                    this.textBox1.Enabled = true;
                    //this.button2.Enabled = true;
                    this.button1.Text = "録画開始";
                    this.button1.Focus();
                }
                else
                {
                    this.textBox1.Enabled = false;
                    //this.button2.Enabled = false;
                    this.button1.Text = "中断";
                    this.button1.Focus();
                }
            }));
        }

        private void StartExtract(string filename)
        {
            if (filename.IndexOf(".sqlite3") < 0) return;

            try
            {
                //保存ファイル名作成
                epi = new ExecPsInfo();
                epi.Sqlite3File = filename;
                epi.Protocol = Protocol.hls.ToString();
                epi.Seq = 0;
                epi.Exec = GetExecFile(props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())]);
                epi.Arg = "-i - -c copy -y \"%FILE%\"";
                epi.Ext2 = ".mp4";

                //Kvsデーター読み込み
                _ndb = new NicoDb(this, filename);
                var kvs = _ndb.ReadDbKvs();

                bci = new BroadCastInfo(null, null, null, null);
                bci.Provider_Type = kvs["providerType"];
                bci.OnAirStatus = kvs["status"];
                bci.Server_Time = Props.GetLongParse(kvs["serverTime"]);

                //コメント情報
                cmi = new CommentInfo("NaN");
                cmi.OpenTime = Props.GetLongParse(kvs["openTime"]);
                cmi.BeginTime = Props.GetLongParse(kvs["beginTime"]);
                cmi.EndTime = Props.GetLongParse(kvs["endTime"]);
                cmi.Offset = 0L;
                cctl = null;
                _nNetComment = new NicoNetComment(this, bci, cmi, _nLiveNet, _ndb, cctl);

                //映像ファイル出力処理
                if (_ndb.CountDbMedia() > 0)
                {
                    if (_ndb.ReadDbMedia2(epi, cmi, bci))
                        AddLog("映像出力終了しました。", 1);
                    else
                        AddLog("映像出力失敗しました。", 1);
                    AddLog("offset = " + cmi.Offset.ToString(), 1);
                }
                else
                {
                    AddLog("映像データーはありません。", 1);
                    epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3Num(epi);
                    cmi.SaveFile = epi.SaveFile + epi.Xml;
                }
                if (_ndb.CountDbComment() > 0)
                {
                    if (_ndb.ReadDbComment(cmi, bci, _nNetComment))
                        AddLog("コメント出力終了しました。", 1);
                    else
                        AddLog("コメント出力失敗しました。", 1);
                }
                else
                {
                    AddLog("コメントデーターはありません。", 1);
                }

                //終了処理
                if (_ndb != null)
                    _ndb.Dispose();
                if (_nNetComment != null)
                    _nNetComment.Dispose();
            }
            catch (Exception Ex)
            {
                if (_ndb != null)
                    _ndb.Dispose();
                if (_nNetComment != null)
                    _nNetComment.Dispose();
                AddLog("出力処理エラー。\r\n" + Ex.Message, 2);
            }
        }

        //実行ファイルと同じフォルダにある指定ファイルのフルパスをGet
        private string GetExecFile(string file)
        {
            var fullAssemblyName = this.GetType().Assembly.Location;
            if (Path.GetFileName(file) == file)
                return Path.Combine(Path.GetDirectoryName(fullAssemblyName), file);
            return file;
        }

    }
}
