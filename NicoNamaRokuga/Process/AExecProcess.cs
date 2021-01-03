using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using NicoNamaRokuga.Net;
using NicoNamaRokuga.Rec;

namespace NicoNamaRokuga.Proc
{
    public abstract class AEexecProcess
    {
        public volatile int PsStatus = -1; //実行ファイルの状態

        protected NicoNetComment _nNetComment = null;   //WebSocket(Comment)
        protected BroadCastInfo _bci = null;
        protected NicoDb _ndb = null;
        protected RetryInfo _ri = null;
        protected Form1 _form = null;

        public abstract void ExecPs(string exefile, string argument);
        public abstract void BreakProcess(string breakkey);
    }
}
