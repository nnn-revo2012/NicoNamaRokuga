using System.Reflection;

namespace NicoNamaRokuga.Prop
{
    public class Ver
    {
        public static readonly string Version = "0.1.1.09";
        public static readonly string VerDate = "2021/02/02";

        public static string GetFullVersion()
        {
            return GetAssemblyName() + " Ver " + Version;
        }

        public static string GetAssemblyName()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            return assembly.Name;
        }
    }
}
