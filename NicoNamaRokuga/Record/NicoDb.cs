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

        public NicoDb(Form1 fo)
        {
            IsDebug = false;

            this._form = fo;

            //var wc = new WebClientEx();
            //_wc = wc;

        }

        ~NicoDb()
        {
            this.Dispose();
        }

        public void CreateDbMedia(string DbFile)
        {

            using (var conn = new SQLiteConnection("Data Source=" + DbFile))
            {
                conn.Open();
                using (SQLiteCommand command = conn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE media ("
                                        + "seqno     INTEGER PRIMARY KEY NOT NULL UNIQUE,"
                                        + "current   INTEGER,"
                                        + "position  REAL,"
                                        + "notfound  INTEGER,"
                                        + "bandwidth INTEGER,"
                                        + "size      INTEGER,"
                                        + "data      BLOB)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX media0 ON media(seqno);"
                                        + "CREATE INDEX media1 ON media(position);"
                                        + "CREATE INDEX media100 ON media(size);"
                                        + "CREATE INDEX media101 ON media(notfound)";
                    command.ExecuteNonQuery();
                }
                conn.Close();
            }

        }

        public void WriteDbMedia(string DbFile)
        {

            using (var conn = new SQLiteConnection("Data Source=" + DbFile))
            {
                conn.Open();
                using (SQLiteCommand command = conn.CreateCommand())
                {
                    command.CommandText = "INSERT INTO media ("
                                        + "seqno,current,position,notfound,bandwidth,size,data) VALUES ("
//
                                        + ")";
                    command.ExecuteNonQuery();
                }
                conn.Close();
            }

        }

        public void CreateDbComment(string DbFile)
        {

            using (var conn = new SQLiteConnection("Data Source=" + DbFile))
            {
                conn.Open();
                using (SQLiteCommand command = conn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE comment ("
                                        + "vpos      INTEGER NOT NULL,"
                                        + "date      INTEGER NOT NULL,"
                                        + "date_usec INTEGER NOT NULL,"
                                        + "date2     INTEGER NOT NULL,"
                                        + "no        INTEGER,"
                                        + "anonymity INTEGER,"
                                        + "user_id   TEXT NOT NULL,"
                                        + "content   TEXT NOT NULL,"
                                        + "mail      TEXT,"
                                        + "premium   INTEGER,"
                                        + "score     INTEGER,"
                                        + "thread    INTEGER,"
                                        + "origin    TEXT,"
                                        + "locale    TEXT,"
                                        + "hash      TEXT UNIQUE NOT NULL)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX comment0 ON comment(hash);"
                                        + "CREATE INDEX comment100 ON comment(date2);"
                                        + "CREATE INDEX comment101 ON comment(no)";
                    command.ExecuteNonQuery();
                }
                conn.Close();
            }

        }

        public void CreateDbKvs(string DbFile)
        {

            using (var conn = new SQLiteConnection("Data Source=" + DbFile))
            {
                conn.Open();
                using (SQLiteCommand command = conn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE kvs ("
                                        + "k TEXT PRIMARY KEY NOT NULL UNIQUE,"
                                        + "v BLOB)";
                    command.ExecuteNonQuery();
                    command.CommandText = "CREATE UNIQUE INDEX kvs0 ON kvs(k)";
                    command.ExecuteNonQuery();
                }
                conn.Close();
            }

        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
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
