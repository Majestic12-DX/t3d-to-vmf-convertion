using T3DtoVMF.Core.Public;

namespace T3DtoVMF.Cli.Utils
{
    static class Title
    {
        public static void Update(string extra = "")
        {
            string version = CoreInfo.Version;
            Console.Title = $"UE1 .t3d to .vmf Converter | v{version} | {extra}";
        }
    }
}
