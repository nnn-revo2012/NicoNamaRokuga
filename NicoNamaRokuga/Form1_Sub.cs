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
using NicoNamaRokuga.Message;
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
                if (bci.Provider_Type == "channel" || bci.Provider_Type == "official")
                {
                    label4.Text = bci.Community_Title + "(" + bci.Community_Id + ")";
                }
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
                if (Props.ParseQTypes(s, Props.Quality) > 0)
                    label9.Text = Props.Quality[Props.ParseQTypes(s, Props.Quality)];
                else
                    label9.Text = Props.Quality2[Props.ParseQTypes(s, Props.Quality2)];
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
            long seqnoStart = 0;
            long seqnoEnd = 0;

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

                _ndb = new NicoDb(this, filename);
                var revision = _ndb.GetDbCommentRevision();
                AddLog("CommentRevision: " + revision, 1);
                /*
                if (revision < 2)
                {
                    AddLog("DBfile is old. Can't output comments this program.", 1);
                    if (_ndb != null)
                        _ndb.Dispose();
                    return;
                }
                */
                //Kvsデーター読み込み
                var kvs = _ndb.ReadDbKvs();
                bci = new BroadCastInfo(null, null, null, null);
                bci.Provider_Type = kvs["providerType"];
                bci.OnAirStatus = kvs["status"];
                if (kvs.ContainsKey("serverTime"))
                    bci.Server_Time = Props.GetLongParse(kvs["serverTime"]);

                //Syncデーター読み込み
                long sync_seqno = 0, sync_date = 0;
                if (revision > 1)
                {
                    var sync = _ndb.ReadDbSync();
                    if (sync.Count > 0)
                    {
                        var data = sync[0].Split(',');
                        long.TryParse(data[0], out sync_seqno);
                        long.TryParse(data[1], out sync_date);
                        AddLog("sync: " + sync_seqno + "=" + sync_date, 1);
                    }
                }

                //コメント情報
                bci.Open_Time = Props.GetLongParse(kvs["openTime"]);
                bci.Begin_Time = Props.GetLongParse(kvs["beginTime"]);
                bci.End_Time = Props.GetLongParse(kvs["endTime"]);
                if (kvs.ContainsKey("vposBaseTime"))
                    bci.VposBase_Time = Props.GetLongParse(kvs["vposBaseTime"]);
                bci.Provider_Type = kvs["providerType"].ToString();
                if (revision > 2)
                    bci.StreamType = kvs["streamType"].ToString();

                epi.Comment_Offset = 0L;
                if (bci.OnAirStatus == "ENDED")
                {
                    if (bci.StreamType == "dlive")
                        epi.Comment_Offset = seqnoStart * 600; //timeshift
                    else
                        epi.Comment_Offset = seqnoStart * 500; //timeshift
                }
                else
                {
                    if (bci.StreamType == "dlive")
                        epi.Comment_Offset = (sync_date / 10) - (bci.Open_Time * 100) + (seqnoStart - sync_seqno) * 300; //on_air
                    else
                        epi.Comment_Offset = (sync_date / 10) - (bci.Open_Time * 100) + (seqnoStart - sync_seqno) * 150; //on_air
                }

                AddLog("OnAirStatus: " + bci.OnAirStatus, 1);
                AddLog("AdjustVpos: " + props.AdjustVpos, 1);
                AddLog("ProvideType: " + bci.Provider_Type, 1);
                AddLog("OpenTime: " + bci.Open_Time, 1);
                AddLog("VposBaseTime: " + bci.VposBase_Time, 1);
                AddLog("Comment_Offset: " + epi.Comment_Offset, 1);
                _nms = new NicoMessage(this, bci, _nln, _ndb);

                //映像ファイル出力処理
                if (_ndb.CountDbMedia() > 0)
                {
                    if (_ndb.ReadDbMedia2(epi, bci))
                        AddLog("映像出力終了しました。", 1);
                    else
                        AddLog("映像出力失敗しました。", 1);
                    AddLog("offset = " + epi.Comment_Offset.ToString(), 1);
                }
                else
                {
                    AddLog("映像データーはありません。", 1);
                    epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3Num(epi);
                    epi.SaveCommentFile = epi.SaveFile + epi.Xml;
                }
                if (_ndb.CountDbComment() > 0)
                {
                    if (_ndb.ReadDbComment(epi, bci, _nms, revision, props.IsSeetNo, props.AdjustVpos))
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
                if (_nms != null)
                    _nms.Dispose();
            }
            catch (Exception Ex)
            {
                if (_ndb != null)
                    _ndb.Dispose();
                if (_nms != null)
                    _nms.Dispose();
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
