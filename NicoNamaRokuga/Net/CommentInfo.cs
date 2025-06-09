using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Web;

namespace NicoNamaRokuga.Net
{
    public class CommentInfo
    {
        public string Url { set; get; }
        public string SaveFile { get; set; }
        public long OpenTime { get; set; }
        public long BeginTime { get; set; }
        public long EndTime { get; set; }
        public long   VposBaseTime { get; set; }
        public long Offset { get; set; }

        public CommentInfo(string userid)
        {
        }
    }

    public class CommentControl
    {
        public int status { set; get; }
        public string _waybackkey { set; get; }
        public long _when { set; get; }                    //when
        public long _last_res { set; get; }                //last_res
        public List<List<string>> _come_list { set; get; }
        public List<string> _come_text { set; get; }

        public CommentControl()
        {
            status = 0;
            _waybackkey = null;
            _last_res = 0L;
            _come_list = new List<List<string>>();
            _come_text = new List<string>();
        }
    }

}
