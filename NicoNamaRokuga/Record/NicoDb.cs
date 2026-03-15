using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using System.Data.SQLite;
using Newtonsoft.Json.Linq;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;
using NicoNamaRokuga.Message;
using NicoNamaRokuga.Proc;

namespace NicoNamaRokuga.Rec
{
    public class NicoDb : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private SQLiteConnection _cn = null;

        //Debug
        public bool IsDebug { get; set; }

        private Form1 _form = null;

        public NicoDb(Form1 fo, string dbfile)
        {
            IsDebug = false;

            this._form = fo;

            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = dbfile,
                SyncMode = SynchronizationModes.Off,
                JournalMode = SQLiteJournalModeEnum.Wal,
            };
            var conn = new SQLiteConnection(builder.ToString());
            _cn = conn;

            Open();
        }

        ~NicoDb()
        {
            this.Dispose();
        }

        public void Open()
        {
            _cn?.Open();
        }

        public void Close()
        {
            _cn?.Close();
        }

        public void CreateDbAll()
        {
            CreateDbMedia();
            CreateDbComment();
            CreateDbKvs();
            CreateDbSync();
        }

        public void CreateDbMedia()
        {
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS media (\n"
                                        + "seqno     INTEGER PRIMARY KEY NOT NULL UNIQUE,\n"
                                        + "current   INTEGER,\n"
                                        + "position  REAL,\n"
                                        + "notfound  INTEGER,\n"
                                        + "bandwidth INTEGER,\n"
                                        + "size      INTEGER,\n"
                                        + "data      BLOB)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS media0 ON media(seqno);"
                                        + "CREATE INDEX IF NOT EXISTS media1 ON media(position);"
                                        + "CREATE INDEX IF NOT EXISTS media100 ON media(size);"
                                        + "CREATE INDEX IF NOT EXISTS media101 ON media(notfound)";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CreateDbMedia), Ex);
            }
        }

        public bool WriteDbMedia(Segment seg, PlayListInfo pli, SegmentInfo sgi, byte[] data, int leng, int notfound)
        {
            try 
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "INSERT INTO media \n";
                    if (notfound > 0)
                        command.CommandText += "(seqno,current,position,bandwidth,size,data,notfound) VALUES (\n";
                    else
                        command.CommandText += "(seqno,current,position,bandwidth,size,data) VALUES (\n";
                    command.CommandText += sgi.SeqNo.ToString() + ",\n"
                                         + pli.SeqNo.ToString() + ",\n"
                                         + sgi.Position.ToString() + ",\n"
                                         + pli.Player.FirstOrDefault().Bandwidth.ToString() + ",\n"
                                         + leng.ToString() + ",\n";
                    if (notfound > 0)
                        command.CommandText += "@data," + notfound.ToString() + ");";
                    else
                        command.CommandText += "@data);";

                    var param = new SQLiteParameter("@data", System.Data.DbType.Binary);
                    param.Value = data;
                    command.Parameters.Add(param);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbMedia), Ex);
                return false;
            }

            return true;
        }

        public bool ReadDbMedia(ExecPsInfo epi, BroadCastInfo bci)
        {
            FileStream fs = null;
            var result = false;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    long seqno = -1L;
                    long prevseqno = -1L;
                    int bw = -1;
                    int prevbw = -1;
                    int size;
                    byte[] data;
                    bool off_flg = true;

                    command.CommandText = "SELECT seqno, bandwidth, size, data FROM media\n"
                                        + "WHERE IFNULL(notfound, 0) == 0 AND data IS NOT NULL\n"
                                        + "ORDER BY seqno";
                    //ファイルオープン
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3Num(epi);
                        epi.SaveCommentFile = epi.SaveFile + epi.Xml;
                        fs = new FileStream(epi.SaveFile + epi.Ext, FileMode.Create);

                        while (reader.Read())
                        {
                            seqno = (long )reader["seqno"];
                            if (off_flg)
                            {
                                epi.Comment_Offset = bci.IsTimeShift() ? seqno * 500L : bci.Server_Time / 10L - bci.Open_Time * 100L;
                                off_flg = false;
                            }
                            bw = (int )(long )reader["bandwidth"];
                            // チャンクが飛んでいる場合はファイルを分ける
                            // BANDWIDTHが変わる場合はファイルを分ける
                            if ((prevseqno > -1L && seqno - prevseqno > 1L) || (prevbw > -1 && bw != prevbw))
                            {
                                if (bw != prevbw)
                                    Debug.WriteLine("Bandwitdh changed: {0} --> {1}\n", prevbw, bw);
                                else
                                    Debug.WriteLine("SeqNo. skipped: {0} --> {1}\n", prevseqno, seqno);
                                fs.Dispose();
                                epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3Num(epi);
                                fs = new FileStream(epi.SaveFile + epi.Ext, FileMode.Create);
                            }
                            size = (int )(long )reader["size"];
                            data = (byte[] )reader["data"];
                            fs.Write(data, 0, size);
                            prevseqno = seqno;
                            prevbw = bw;
                        }
                        result = true;
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbMedia), Ex);
            }
            finally
            {
                if (fs != null) fs.Dispose();
            }
            return result;
        }

        public bool ReadDbMedia2(ExecPsInfo epi, BroadCastInfo bci)
        {
            //FileStream fs = null;
            var ecv = new List<ExecConvert>();
            var result = false;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    long seqno = -1L;
                    long prevseqno = -1L;
                    int bw = -1;
                    int prevbw = -1;
                    int size;
                    byte[] data;
                    bool off_flg = true;
                    string arg;

                    command.CommandText = "SELECT seqno, bandwidth, size, data FROM media\n"
                                        + "WHERE IFNULL(notfound, 0) == 0 AND data IS NOT NULL\n"
                                        + "ORDER BY seqno";
                    //ファイルオープン
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3Num(epi, epi.Ext2);
                        epi.SaveCommentFile = epi.SaveFile + epi.Xml;
                        //fs = new FileStream(epi.SaveFile + epi.Ext, FileMode.Create);
                        ecv.Add(new ExecConvert(_form));
                        arg = ExecPsInfo.SetConvOption(epi, null);
                        ecv[ecv.Count()-1].ExecPs(epi.Exec, arg);

                        while (reader.Read())
                        {
                            seqno = (long)reader["seqno"];
                            if (off_flg)
                            {
                                epi.Comment_Offset = bci.IsTimeShift() ? seqno * 500L : bci.Server_Time / 10L - bci.Open_Time * 100L;
                                off_flg = false;
                            }
                            bw = (int)(long)reader["bandwidth"];
                            // チャンクが飛んでいる場合はファイルを分ける
                            // BANDWIDTHが変わる場合はファイルを分ける
                            if ((prevseqno > -1L && seqno - prevseqno > 1L) || (prevbw > -1 && bw != prevbw))
                            {
                                if (bw != prevbw)
                                    Debug.WriteLine("Bandwitdh changed: {0} --> {1}\n", prevbw, bw);
                                else
                                    Debug.WriteLine("SeqNo. skipped: {0} --> {1}\n", prevseqno, seqno);
                                //fs.Dispose();
                                ecv[ecv.Count() - 1].StopInput();
                                epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3Num(epi, epi.Ext2);
                                //fs = new FileStream(epi.SaveFile + epi.Ext, FileMode.Create);
                                ecv.Add(new ExecConvert(_form));
                                arg = ExecPsInfo.SetConvOption(epi, null);
                                ecv[ecv.Count() - 1].ExecPs(epi.Exec, arg);
                            }
                            size = (int)(long)reader["size"];
                            data = (byte[])reader["data"];
                            //fs.Write(data, 0, size);
                            ecv[ecv.Count()-1].InputProcess(data);
                            prevseqno = seqno;
                            prevbw = bw;
                        }
                        result = true;
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbMedia2), Ex);
            }
            finally
            {
                //if (fs != null) fs.Dispose();
                ecv[ecv.Count()-1].StopInput();
                //全プロセスが終了したのを確認
                //_form.AddLog("プロセス数=" + ecv.Count(), 1);
                var endflg = false;
                while (!endflg)
                {
                    for (var i = 0; i < ecv.Count; i++)
                    {
                        if (ecv[i] != null)
                        {
                            if (ecv[i].PsStatus >= 1)
                            {
                                ecv[i].Dispose();
                                ecv[i] = null;
                            }
                            else
                            {
                                endflg = false;
                                Task.Delay(1000).Wait();
                            }
                        }
                        else
                        {
                            endflg = true;
                        }
                    }
                }
                //if (ecv != null) ecv.Dispose();
                //_form.AddLog("全プロセス終了しました", 1);
            }
            return result;
        }

        public void CreateDbComment()
        {
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS comment (\n"
                                        + "vpos      INTEGER NOT NULL,\n"
                                        + "date      INTEGER NOT NULL,\n"
                                        + "date_usec INTEGER NOT NULL,\n"
                                        + "date2     INTEGER NOT NULL,\n"
                                        + "no        INTEGER,\n"
                                        + "anonymity INTEGER,\n"
                                        + "user_id   TEXT NOT NULL,\n"
                                        + "content   TEXT NOT NULL,\n"
                                        + "mail      TEXT,\n"
                                        + "name      TEXT,\n"
                                        + "premium   INTEGER,\n"
                                        + "score     INTEGER,\n"
                                        + "thread    TEXT,\n"
                                        + "origin    TEXT,\n"
                                        + "locale    TEXT,\n"
                                        + "hash      TEXT UNIQUE NOT NULL)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS comment0 ON comment(hash);"
                                        + "CREATE INDEX IF NOT EXISTS comment100 ON comment(date2);"
                                        + "CREATE INDEX IF NOT EXISTS comment101 ON comment(no)";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CreateDbComment), Ex);
            }
        }

        public bool WriteDbComment(string command_text, string mail, string user_id, string content)
        {
            try 
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "INSERT INTO comment \n" +
                                          command_text;

                    var p_mail = new SQLiteParameter("@mail", System.Data.DbType.String);
                    p_mail.Value = mail;
                    command.Parameters.Add(p_mail);

                    var p_user_id = new SQLiteParameter("@user_id", System.Data.DbType.String);
                    p_user_id.Value = user_id;
                    command.Parameters.Add(p_user_id);

                    var p_content = new SQLiteParameter("@content", System.Data.DbType.String);
                    p_content.Value = content;
                    command.Parameters.Add(p_content);

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbComment), Ex);
                return false;
            }

            return true;
        }

        public bool ReadDbComment(ExecPsInfo epi, BroadCastInfo bci, NicoMessage nsm)
        {
            var enc = new System.Text.UTF8Encoding(false);
            StreamWriter sw = null;
            var result = false;
            var data = new Dictionary<string, string>();
            int rev = 0;

            try
            {
                rev = GetDbCommentRevision();
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT thread,\n"
                                        + "IFNULL(no, -1) AS no,\n"
                                        + "vpos, date, date_usec,\n"
                                        + "IFNULL(mail, \"\") AS mail,\n";
                    if (rev > 0)
                        command.CommandText += "IFNULL(name, \"\") AS name,\n";
                    command.CommandText += "user_id,\n"
                                        + "IFNULL(premium, 0) AS premium,\n"
                                        + "IFNULL(anonymity, 0) AS anonymity,\n"
                                        + "IFNULL(locale, \"\") AS locale\n,"
                                        + "IFNULL(origin, \"\") AS origin,\n"
                                        + "IFNULL(score, 0) AS score,\n"
                                        + "content\n"
                                        + "FROM comment ORDER BY date2";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        sw = new StreamWriter(epi.SaveCommentFile, true, enc);
                        nsm.SetStreamWriter(sw);
                        nsm.BeginXmlDoc();
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                data[reader.GetName(i)] = reader.GetValue(i).ToString();
                            }
                            data["vpos"] = nsm.CalcVpos(bci.Open_Time,epi.Comment_Offset, data["date"], data["vpos"], bci.Provider_Type);
                            sw.Write(nsm.Table2Xml(data));
                        }
                        nsm.EndXmlDoc();
                        nsm.DisposeStreamWriter();
                        if (sw != null)
                            sw.Dispose();
                        result = true;
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbComment), Ex);
            }
            finally
            {
                if (sw != null) sw.Dispose();
            }
            return result;
        }

        public void CreateDbKvs()
        {
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS kvs (\n"
                                        + "k TEXT PRIMARY KEY NOT NULL UNIQUE,\n"
                                        + "v BLOB)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS kvs0 ON kvs(k)";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CreateDbKvs), Ex);
            }
        }

        public bool WriteDbKvsProps(BroadCastInfo bci)
        {
            try
            {
                if (bci == null || bci.Data_Props == null)
                    return false;

                var datap = JObject.Parse(bci.Data_Props);
                var ttt = string.Empty;

                foreach (var item in Props.PropLists)
                {
                    ttt = (string)datap.SelectToken(item.Value);
                    if (item.Key == "beginTime" || item.Key == "endTime" ||
                        item.Key == "openTime" || item.Key == "serverTime" ||
                        item.Key == "socLevel" || item.Key == "vposBaseTime")
                    {
                        double ddd;
                        if (double.TryParse(ttt, out ddd))
                            WriteDbKvs(item.Key, System.Data.DbType.Double, ddd);
                    }
                    else if (item.Key == "isFollowerOnly" || item.Key == "isPrivate" || item.Key == "isLoggedIn")
                    {
                        if (ttt != null)
                            WriteDbKvs(item.Key, System.Data.DbType.Int32, (int )datap.SelectToken(item.Value));
                    }
                    else
                    {
                        if (ttt != null)
                            WriteDbKvs(item.Key, System.Data.DbType.String, ttt);
                    }

                }
                ttt = (string)datap.SelectToken(Props.PropLists["userPageUrl"]);
                if (ttt != null)
                    WriteDbKvs("userId", System.Data.DbType.String, Props.GetChNo(ttt));

                WriteDbKvs("streamType", System.Data.DbType.String, bci.StreamType);
                WriteDbKvs("socId", System.Data.DbType.String, bci.Community_Id);
                WriteDbKvs("socName", System.Data.DbType.String, bci.Community_Title);
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbKvsProps), Ex);
                return false;
            }
            return true;
        }

        public bool WriteDbKvs(string key, System.Data.DbType dbtype, object data)
        {
            try
            {
                SQLiteParameter p_data = null;
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "INSERT OR IGNORE INTO kvs (k, v) VALUES \n" +
                                          "(\"" + key + "\" ,@data);";
                    p_data = new SQLiteParameter("@data", dbtype);
                    if (dbtype == System.Data.DbType.Double)
                        p_data.Value = (double )data;
                    else if (dbtype == System.Data.DbType.Int32)
                        p_data.Value = (int )data;
                    else
                        p_data.Value = (string )data;
                    command.Parameters.Add(p_data);

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbKvs), Ex);
                return false;
            }

            return true;
        }

        public IDictionary<string, string> ReadDbKvs()
        {
            var kvs = new Dictionary<string, string>();

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT k,v FROM kvs";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            kvs[reader["k"].ToString()] = 
                                Encoding.UTF8.GetString((byte[])reader["v"]); //blob
                   }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbKvs), Ex);
                return kvs;
            }
            return kvs;
        }

        public void CreateDbSync()
        {
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS sync (\n"
                                        + "seqno INTEGER PRIMARY KEY NOT NULL UNIQUE,\n"
                                        + "date  INTEGER NOT NULL)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS sync0 ON sync(seqno)";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CreateDbSync), Ex);
            }
        }

        public IList<string> ReadDbSync()
        {
            var sync = new List<string>();

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT seqno, date FROM sync ORDER by seqno";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        //fmt.Printf("data: %d,%d\n", seqno, date)
                        //data = append(data, fmt.Sprintf("%d,%d", seqno, date))
                        string data;
                        while (reader.Read())
                        {
                            data = reader["seqno"].ToString() + "," +
                                   reader["date"].ToString();
                            sync.Add(data);
                        }
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbSync), Ex);
                return sync;
            }
            return sync;
        }

        public bool WriteDbSync(string data)
        {
            try
            {
                long seqno = 0, date = 0;
                var m = Regex.Match(data, @"""beginning_timestamp"":(\d+),""sequence"":(\d+)");
                if (m.Success)
                {
                    date = long.Parse(m.Groups[1].Value);
                    seqno = long.Parse(m.Groups[2].Value);
                }
                else
                {
                    return false;
                }
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "INSERT OR IGNORE INTO sync(seqno, date) VALUES \n" +
                                          "(" + seqno + ", " + date + ");";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbSync), Ex);
                return false;
            }

            return true;
        }
        public double GetDbMediaLastPos()
        {
            double dbl = 0.0D;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT position FROM media ORDER BY POSITION DESC LIMIT 1";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            double.TryParse(reader["position"].ToString(), out dbl);
                    }
                }
                return dbl;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetDbMediaLastPos), Ex);
                return dbl;
            }
        }

        public long GetDbMediaLastSeqNo()
        {
            long seqno = 0;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT seqno FROM media ORDER BY seqno DESC LIMIT 1";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            long.TryParse(reader["seqno"].ToString(), out seqno);
                    }
                }
                return seqno;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetDbMediaLastSeqNo), Ex);
                return seqno;
            }
        }

        public long CountDbMedia()
        {
            long result = -1;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM media";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            result = reader.GetInt64(0);
                    }
                }
                return result;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CountDbMedia), Ex);
                return result;
            }
        }

        public long CountDbComment()
        {
            long result = -1;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM comment";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            result = reader.GetInt64(0);
                    }
                }
                return result;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CountDbComment), Ex);
                return result;
            }
        }
        public long GetDbFromWhen()
        {
            long when = 0L;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    long date2 = 0L;
                    command.CommandText = "SELECT date2 FROM comment ORDER BY date2 ASC LIMIT 1";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            date2 = reader.GetInt64(0);
                    }
                    if (date2 == 0L)
                    {
                        double endTime = 0.0;
                        command.CommandText = "SELECT v FROM kvs WHERE k = \"endTime\"";
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                                endTime = reader.GetDouble(0);
                        }
                        when = (long)endTime + 360L;
                        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        if (unix < when)
                            when = unix;
                    }
                    else
                    {
                        when = date2 / (1000 * 1000) + 1;
                    }
                }
                return when;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetDbFromWhen), Ex);
                return when;
            }
        }

        public int GetDbCommentRevision()
        {
            int result = -1;
            int rev = 0;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(name) FROM pragma_table_info('comment') WHERE name = 'name'";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            rev = (int)reader.GetInt64(0);
                    }
                    if (rev > 0) result = 1;
                    //2024/8/5以降（メッセージサーバーに変更後）
                    command.CommandText = "SELECT COUNT(k) FROM kvs WHERE k = 'vposBaseTime'";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            rev = (int)reader.GetInt64(0);
                    }
                    if (rev > 0) result = 2;
                    //2025/2以降（新動画サーバーに変更後）
                    command.CommandText = "SELECT COUNT(k) FROM 'kvs' WHERE k = 'streamType'";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            rev = (int)reader.GetInt64(0);
                    }
                    if (rev > 0) result = 3;
                }
                return result;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetDbCommentRevision), Ex);
                return result;
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _cn?.Close();
                    _cn?.Dispose();
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
