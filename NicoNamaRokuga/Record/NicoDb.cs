using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows.Forms;

using System.Data.SQLite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NicoNamaRokuga.Prop;

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
            CreateDbMedia(dbfile);
            CreateDbComment(dbfile);
            CreateDbKvs(dbfile);
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

        //public void Open()
        //{
        //    _cn?.Open();
        //}

        public void CreateDbMedia(string DbFile)
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

        public void WriteDbMedia(Segment seg, PlayListInfo pli, SegmentInfo sgi, byte[] data, int leng, int notfound)
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
            }
        }

        public void CreateDbComment(string DbFile)
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
                                        + "premium   INTEGER,\n"
                                        + "score     INTEGER,\n"
                                        + "thread    INTEGER,\n"
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

        public void WriteDbComment(string command_text, string mail, string user_id, string content)
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
            }
        }

        public void CreateDbKvs(string DbFile)
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

        public void WriteDbKvsProps(string data_props)
        {
            try
            {
                var datap = JObject.Parse(data_props);
                var ttt = string.Empty;

                foreach (var item in Props.PropLists)
                {
                    ttt = (string)datap.SelectToken(item.Value);
                    if (item.Key == "beginTime" || item.Key == "endTime" ||
                        item.Key == "openTime" || item.Key == "serverTime" ||
                        item.Key == "socLevel")
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

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbKvsProps), Ex);
            }
        }

        public void WriteDbKvs(string key, System.Data.DbType dbtype, object data)
        {
            try
            {
                SQLiteParameter p_data = null;
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "INSERT INTO kvs (k, v) VALUES \n" +
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
            }
        }

        public double GetDbMediaLastPos()
        {
            double ll = 0;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT position FROM media ORDER BY POSITION DESC LIMIT 1";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            double.TryParse(reader["position"].ToString(), out ll);
                    }
                }
                return ll;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetDbMediaLastPos), Ex);
                return ll;
            }
        }

        public int GetDbMediaLastSeqNo()
        {
            int seqno = 0;
            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT seqno FROM media ORDER BY seqno DESC LIMIT 1";
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            int.TryParse(reader["seqno"].ToString(), out seqno);
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
