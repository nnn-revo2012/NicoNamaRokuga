
namespace NicoNamaRokuga.Proc
{
    interface IExecProcess
    {

        void ExecPs(string exefile, string argument);
        void BreakProcess(string breakkey);

    }
}
