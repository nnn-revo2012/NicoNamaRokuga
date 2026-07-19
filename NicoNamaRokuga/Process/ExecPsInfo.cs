using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using NicoNamaRokuga.Prop;

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
        public int Seq { get; set; }
        public string Protocol { get; set; }
        public string Quality { get; set; }
        public string SaveFile { get; set; }
        public string SaveCommentFile { get; set; }
        public string Sqlite3File { get; set; }
        public string SaveExtFile { get; set; }
        public string Ext { get { return (Protocol == "rtmp") ? ".flv" : ".ts"; } }
        public string Xml { get { return ".xml"; } }
        public string Ext2 { get; set; }
        public long   Comment_Offset { get; set; }

        //保存ファイルにシーケンスNoをつける
        public static string GetSaveFileNum(ExecPsInfo epi, string ext = null)
        {
            int idx = epi.Sqlite3File.IndexOf(".sqlite3");
            if (idx < 0) return null;

            var ext2 = epi.Ext;
            if (ext != null) ext2 = ext;
            var ff = epi.Sqlite3File.Substring(0, idx) + "-";

            //同名ファイル名がないかチェック
            while (IsExistFile(ff, epi.Seq, ext2, epi.Xml)) ++epi.Seq;

            return ff + epi.Seq.ToString();
        }

        //実行ファイル用の引数(argumentを設定)
        public static string SetOption(ExecPsInfo epi, string para, string url, string us, Props props, Net.BroadCastInfo bci, bool isdebug)
        {
            var result = epi.Arg;
            var ff = epi.SaveExtFile + epi.Ext;
            var headers = string.Empty;
            if (bci.IsTimeShift())
            {
                if (bci.StartTs_Time > 0)
                    headers = "--hls-start-offset " + Props.SecondsToHHMMSS(bci.StartTs_Time);
                if (bci.EndTs_Time > 0)
                {
                    var ll = bci.EndTs_Time - bci.StartTs_Time;
                    if (ll > 0)
                        headers += " --stream-segmented-duration " + Props.SecondsToHHMMSS(ll);
                }
            }
            result = result.Replace("%HEADERS%", headers);
            result = result.Replace("%URL%", url);
            result = result.Replace("%FILE%", ff);
            if (props.IsLogin == Prop.IsLogin.always && !string.IsNullOrEmpty(us))
                result = result.Replace("%US%", "--niconico-user-session " + us);
            else
                result = result.Replace("%US%", "");
            result = result.Replace("%QUALITY%", props.ExtQuality);
            if (props.ExtIsRetry)
                result = result.Replace("%RETRY%", "--retry-max " + props.ExtRetry + " --retry-streams " + props.ExtReConnect);
            else
                result = result.Replace("%RETRY%", "");

            result = result.Replace("%M3U8%", para);
            if (isdebug)
                result = result.Replace("%DEBUG%", "--loglevel trace");
            else
                result = result.Replace("%DEBUG%", "--loglevel info");

            return result;
        }

        //実行ファイル用の引数(Convert)
        public static string SetConvOption(ExecPsInfo epi, string infile)
        {
            var result = epi.Arg;
            var ff = epi.SaveFile + epi.Ext2;

            result = result.Replace("%PARA%", infile);
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
            return Path.Combine(epi.Sdir, epi.Sfolder, epi.Sfile);
        }

        //Sqlite3の保存ファイルにシーケンスNoをつける
        public static string GetSaveFileSqlite3Num(ExecPsInfo epi, string ext = null)
        {
            int idx = epi.Sqlite3File.IndexOf(".sqlite3");
            if (idx < 0) return null;

            var ext2 = epi.Ext;
            if (ext != null) ext2 = ext;
            var ff = epi.Sqlite3File.Substring(0, idx) + "-";

            //同名ファイル名がないかチェック
            while (IsExistFile(ff, epi.Seq, ext2, epi.Xml)) ++epi.Seq;

            return ff + epi.Seq.ToString();
        }

        //保存ディレクトリーがなければ作る
        public static bool MakeRecDir(ExecPsInfo epi)
        {
            var result = false;

            var s = Path.Combine(epi.Sdir, epi.Sfolder);
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

}
