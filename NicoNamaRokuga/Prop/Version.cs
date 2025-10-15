using System.Reflection;

namespace NicoNamaRokuga.Prop
{
    public class Ver
    {
        public static readonly string Version = "0.1.1.30";
        public static readonly string VerDate = "2025/10/16";

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
