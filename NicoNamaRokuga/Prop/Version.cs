using System.Reflection;

namespace NicoNamaRokuga.Prop
{
    public class Ver
    {
        public static readonly string Version = "0.1.1.25";
        public static readonly string VerDate = "2022/06/23";

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
