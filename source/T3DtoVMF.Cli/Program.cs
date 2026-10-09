// Been a long time since I worked with C#
// Love both of these engines, to be honest :)

using UTModels;

class Program
{
    // Test Files: Every .t3d in Unreal Tournament Maps folder
    static void Main(string[] args)
    {
        string version = "Version 1.3";
        string title = $"UT99 .t3d to .vmf Converter | {version}";
        Console.Title = title;

        // Basic File Checking
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Please drag'n'drop a .t3d file into this application!");
            Console.ReadKey();
            return;
        }

        string filePath = args[0];
        if (Path.GetExtension(filePath).ToLower() != ".t3d")
        {
            Console.WriteLine($"Error: File Extension is not .t3d ({Path.GetFileName(filePath)})");
            Console.ReadKey();
            return;
        }

        string fileName = Path.GetFileName(filePath);
        if (!Logger.SetLogFileName(fileName))
            Console.WriteLine("Couldn't setup log file for logger. No logs will be present");

        // Reading .t3d
        Logger.Write(LogLevel.INFO, $"Started Conversion. {version}");

        List<BaseActor> actors = new();
        T3DReader t3dReader = new T3DReader();

        try
        {
            using var t3dText = new StreamReader(filePath);
            actors = t3dReader.ReadT3DActors(t3dText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fully read .t3d: {ex.Message}");
            return;
        }

        // Writing VMF
        VMFWriter vmfWriter = new VMFWriter();

        try
        {
            string folder = "t3d_to_vmf_vmfs";
            Directory.CreateDirectory(folder);

            string vmfName = $"{Path.GetFileNameWithoutExtension(fileName)} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.vmf";
            using var vmfText = new StreamWriter(Path.Combine(folder, vmfName));
            vmfWriter.WriteVMFFromUnrealActors(actors, vmfText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fully convert to .vmf: {ex.Message}");
            return;
        }
        
        // Finishing
        Logger.Write(LogLevel.INFO, "Finished Convertion");
        Logger.Close();

        Console.WriteLine("Press any button to exit...");
        Console.ReadKey();
    }
}