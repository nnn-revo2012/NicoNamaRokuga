using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Security.Cryptography;

using System.Data.SQLite;

namespace NicoNamaRokuga.Prop
{
    public class Account : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private SQLiteConnection _cn = null;

        //Debug
        public bool IsDebug { get; set; }

        public Account(string dbfile)
        {
            IsDebug = false;

            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = dbfile,
                SyncMode = SynchronizationModes.Off,
                JournalMode = SQLiteJournalModeEnum.Wal,
            };
            if (Directory.Exists(Path.GetDirectoryName(dbfile)))
            {
                var conn = new SQLiteConnection(builder.ToString());
                _cn = conn;
                Open();
            }

        }

        ~Account()
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

        public void CreateDbAccount()
        {
            if (_cn == null) return;

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS niconico (\n"
                                        + "alias   TEXT PRIMARY KEY NOT NULL UNIQUE,\n"
                                        + "user    TEXT, \n"
                                        + "pass    TEXT, \n"
                                        + "session TEXT, \n"
                                        + "secure  TEXT, \n"
                                        + "aesiv   TEXT, \n"
                                        + "aeskey  TEXT);";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CreateDbAccount), Ex);
            }
        }

        public bool WriteDbUser(string alias, string user, string pass)
        {
            string aesiv = "";string aeskey = "";
            string data = pass;
            if (_cn == null) return false;

            try
            {
                if (!string.IsNullOrEmpty(pass))
                {
                    CreateAesKey(out aesiv, out aeskey);
                    data = EncryptAes(pass, aesiv, aeskey);
                }
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "INSERT OR REPLACE INTO niconico \n";
                    command.CommandText += "(alias, user, pass, aesiv, aeskey, session, secure) VALUES \n";
                    command.CommandText += "(\"" + alias + "\",\"" + user + "\", \"" + data + "\", \n";
                    command.CommandText += "\""+ aesiv + "\", \"" + aeskey + "\", NULL, NULL)\n";
                    Debug.WriteLine(command.CommandText);
                    command.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbUser), Ex);
            }

            return true;
        }

        public bool WriteDbSession(string alias, string session, string secure)
        {
            if (_cn == null) return false;

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "UPDATE niconico SET \n";
                    command.CommandText += "session=\"" + session + "\", \n";
                    command.CommandText += "secure=\"" + secure + "\" \n";
                    command.CommandText += "WHERE alias=\"" + alias + "\" \n";
                    Debug.WriteLine(command.CommandText);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteDbSession), Ex);
                return false;
            }

            return true;
        }

        public bool ReadDbUser(string alias, out string user, out string pass)
        {
            user = pass = "";
            string aesiv = ""; string aeskey = "";
            string data = null;
            if (_cn == null) return false;

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT user, pass, aesiv, aeskey FROM niconico \n";
                    command.CommandText += "WHERE alias=\"" + alias + "\" \n";
                    Debug.WriteLine(command.CommandText);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            user   = (string)reader["user"];
                            data   = (string)reader["pass"];
                            aesiv  = (string)reader["aesiv"];
                            aeskey = (string)reader["aeskey"];
                        }
                    }
                    if (!string.IsNullOrEmpty(data))
                        pass = DecryptAes(data, aesiv, aeskey);
                }
                return true;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbUser), Ex);
                return false;
            }
        }

        public bool ReadDbSession(string alias, out string session, out string secure)
        {
            session = secure = "";
            if (_cn == null) return false;

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT session, secure FROM niconico \n";
                    command.CommandText += "WHERE alias=\"" + alias + "\" \n";
                    Debug.WriteLine(command.CommandText);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            session = (string )reader["session"];
                            secure =  (string )reader["secure"];
                        }
                    }
                }
                return true;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReadDbSession), Ex);
                return false;
            }
        }

        public bool GetSession(string alias, CookieContainer cc)
        {
            string session = null;
            string secure = null;
            if (_cn == null) return false;

            try
            {
                if (!ReadDbSession(alias, out session, out secure))
                    return false;
                if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(secure))
                    return false;

                cc.Add(new Cookie("user_session", session, "/", ".nicovideo.jp"));
                cc.Add(new Cookie("user_session_secure", secure, "/", ".nicovideo.jp"));
                return true;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetSession), Ex);
                return false;
            }
        }

        public bool SetSession(string alias, CookieContainer cc)
        {
            string session = null;
            string secure = null;
            if (_cn == null) return false;

            try
            {
                foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                {
                    if (ck.Name == "user_session")
                        session = (string)ck.Value;
                    else if (ck.Name == "user_session_secure")
                        secure = (string)ck.Value;
                }
                if (!string.IsNullOrEmpty(session) && !string.IsNullOrEmpty(secure))
                    if (WriteDbSession(alias, session, secure))
                        return true;
                    else
                        return false;
                else
                    return false;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(SetSession), Ex);
                return false;
            }
        }

        public long CountDbAccount()
        {
            long result = -1;
            if (_cn == null) return result;

            try
            {
                using (SQLiteCommand command = _cn.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM niconico";
                    Debug.WriteLine(command.CommandText);
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
                DebugWrite.Writeln(nameof(CountDbAccount), Ex);
                return result;
            }
        }

        private void CreateAesKey(out string iv, out string key)
        {
            // AES暗号サービスを生成
            var csp = new AesCryptoServiceProvider();
            csp.BlockSize = 128;
            csp.KeySize = 128;
            csp.Mode = CipherMode.CBC;
            csp.Padding = PaddingMode.PKCS7;

            // IV および 鍵 を自動生成
            csp.GenerateIV();
            csp.GenerateKey();

            // 鍵を出力；
            iv = Convert.ToBase64String(csp.IV);
            key = Convert.ToBase64String(csp.Key);
        }

        private string EncryptAes(string plainText, string iv, string key)
        {
            var cipherText = string.Empty;

            var csp = new AesCryptoServiceProvider();
            csp.BlockSize = 128;
            csp.KeySize = 128;
            csp.Mode = CipherMode.CBC;
            csp.Padding = PaddingMode.PKCS7;
            csp.IV = Convert.FromBase64String(iv);
            csp.Key = Convert.FromBase64String(key);

            using (var outms = new MemoryStream())
            using (var encryptor = csp.CreateEncryptor())
            using (var cs = new CryptoStream(outms, encryptor, CryptoStreamMode.Write))
            {
                using (var writer = new StreamWriter(cs))
                {
                    writer.Write(plainText);
                }
                cipherText = Convert.ToBase64String(outms.ToArray());
            }

            return cipherText;
        }

        private string DecryptAes(string cipherText, string iv, string key)
        {
            var plainText = string.Empty;

            var csp = new AesCryptoServiceProvider();
            csp.BlockSize = 128;
            csp.KeySize = 128;
            csp.Mode = CipherMode.CBC;
            csp.Padding = PaddingMode.PKCS7;
            csp.IV = Convert.FromBase64String(iv);
            csp.Key = Convert.FromBase64String(key);

            using (var inms = new MemoryStream(Convert.FromBase64String(cipherText)))
            using (var decryptor = csp.CreateDecryptor())
            using (var cs = new CryptoStream(inms, decryptor, CryptoStreamMode.Read))
            using (var reader = new StreamReader(cs))
            {
                plainText = reader.ReadToEnd();
            }

            return plainText;
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
