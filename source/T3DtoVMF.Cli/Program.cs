// Been a long time since I worked with C#
// Love both of these engines, to be honest :)

using T3DtoVMF.Cli.Utils;
using T3DtoVMF.Core.Public;

class Program
{
    // Test Files: Every .t3d in Unreal Tournament Maps folder
    static int Main(string[] args)
    {
        Title.Update();

        // Basic File Checking
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Launched with no args! For example, try drag'n'dropping a .t3d into this tool");
            return Close.OnPress(Close.BadArguments);
        }

        CliOptions options = ArgsHandler.Parse(args);
        if (options.T3ds.Count == 0)
        {
            Console.WriteLine($"Error: No .t3d files to work with");
            return Close.OnPress(Close.BadArguments);
        }

        string vmfFolder = "t3d_to_vmf_vmfs";
        string logFolder = "t3d_to_vmf_logs";
        string baseObjFolder = "t3d_to_vmf_objs";

        if (options.OutputFolder is not null)
        {
            vmfFolder = Path.Combine(options.OutputFolder, vmfFolder);
            logFolder = Path.Combine(options.OutputFolder, logFolder);
            baseObjFolder = Path.Combine(options.OutputFolder, baseObjFolder);
        }

        for (int i = 0; i < options.T3ds.Count; i++)
        {
            string t3dPath = options.T3ds[i];
            string fileName = Path.GetFileName(t3dPath);
            Title.Update($"{fileName} {i + 1}/{options.T3ds.Count}");

            using var logger = new CLILogger(fileName, options.LoggingLevel, options.NoLogFile ? null : logFolder);

            string objFolderName = $"{Path.GetFileNameWithoutExtension(fileName)} {Time.Now()}";
            string objFolder = Path.Combine(baseObjFolder, objFolderName);

            var data = new ConversionData
            {
                LogCallback = options.LoggingLevel == LogLevel.NONE ? null : logger.Write,
                OBJWriterCreator = options.NoOBJ ? null : name => new StreamWriter(Path.Combine(objFolder, name))
            };

            try
            {
                if (!options.NoOBJ)
                    Directory.CreateDirectory(objFolder);

                Directory.CreateDirectory(vmfFolder);

                string vmfName = $"{Path.GetFileNameWithoutExtension(fileName)} {Time.Now()}.vmf";
                string vmfPath = Path.Combine(vmfFolder, vmfName);

                using var t3dText = new StreamReader(t3dPath);
                using var vmfText = new StreamWriter(vmfPath);

                ConversionResult result = T3DtoVMFConverter.Convert(t3dText, vmfText, data);

                logger.Write(LogLevel.INFO, $"Conversion Result: " +
                    $"\n\t{result.TotalTime} time taken, " +
                    $"\n\t{result.BrushCount} brushes" +
                    $"\n\t{result.BrushSidesCount} brush sides" +
                    $"\n\t{result.BadBrushCount} bad brushes" +
                    $"\n\t{result.BadBrushSidesCount} bad brush sides" +
                    $"\n\t{result.UniqueVerticesCount} unique vertices\n");
            }
            catch (FormatException ex)
            {
                logger.Write(LogLevel.ERROR, $"Format Exception, bad .t3d: {ex.Message}");
                Close.SetExitCodeIfWorse(Close.BadT3D);
            }
            catch (Exception ex)
            {
                logger.Write(LogLevel.ERROR, $"Unexpected Exception: {ex}");
                Close.SetExitCodeIfWorse(Close.UnexpectedError);
            }
        }

        return Close.OnPress();
    }
}