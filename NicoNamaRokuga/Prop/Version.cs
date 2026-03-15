using System.Reflection;

namespace NicoNamaRokuga.Prop
{
    public class Ver
    {
        public static readonly string Version = "0.1.2.01";
        public static readonly string VerDate = "2026/03/18";

        public static string GetFullVersion()
        {
            return GetAssemblyName() + " Ver " + Version + "(" + VerDate + ")";
        }

        public static string GetAssemblyName()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            return assembly.Name;
        }
    }
}
