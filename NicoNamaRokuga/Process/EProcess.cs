using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using NicoNamaRokuga.Net;

namespace NicoNamaRokuga.Proc
{
    public abstract class EProcess
    {
        public volatile int PsStatus = -1; //実行ファイルの状態

        public abstract void ExecPs(string exefile, string argument);
        public abstract void BreakProcess(string breakkey);
    }
}
