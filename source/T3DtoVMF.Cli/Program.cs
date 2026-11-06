// Been a long time since I worked with C#
// Love both of these engines, to be honest :)

using System.Text.Json;
using T3DtoVMF.Cli.Utils;
using T3DtoVMF.Core.Public;

class Program
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    // Test Files: Every .t3d in Unreal Tournament Maps folder
    static int Main(string[] args)
    {
        Title.Update();

        // Basic File Checking
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Launched with no args! For example, try drag'n'dropping a .t3d into this tool");
            return Close.OnPress(Close.BadArguments);
        }

        CliOptions cliOptions;
        ConversionOptions conversionOptions;
        (cliOptions, conversionOptions) = ArgsHandler.Parse(args);

        if (cliOptions.T3ds.Count == 0)
        {
            Console.Error.WriteLine($"Error: No .t3d files to work with");
            return Close.OnPress(Close.BadArguments);
        }

        string vmfFolder = "t3d_to_vmf_vmfs";
        string jsonFolder = "t3d_to_vmf_jsons";
        string logFolder = "t3d_to_vmf_logs";
        string baseObjFolder = "t3d_to_vmf_objs";
        string currentTime = Time.Now();

        if (cliOptions.OutputFolder is not null)
        {
            vmfFolder = Path.Combine(cliOptions.OutputFolder, vmfFolder);
            jsonFolder = Path.Combine(cliOptions.OutputFolder, jsonFolder);
            logFolder = Path.Combine(cliOptions.OutputFolder, logFolder);
            baseObjFolder = Path.Combine(cliOptions.OutputFolder, baseObjFolder);
        }

        for (int i = 0; i < cliOptions.T3ds.Count; i++)
        {
            string t3dPath = cliOptions.T3ds[i];
            string fileName = Path.GetFileName(t3dPath);
            Title.Update($"{fileName} {i + 1}/{cliOptions.T3ds.Count}");

            string fileNameForSubDirectories = $"{Path.GetFileNameWithoutExtension(fileName)} {currentTime}";
            using var logger = new CLILogger(fileNameForSubDirectories, cliOptions.LoggingLevel, cliOptions.NoLogFile ? null : logFolder);

            string objFolder = Path.Combine(baseObjFolder, fileNameForSubDirectories);

            var data = new ConversionData
            {
                LogCallback = cliOptions.LoggingLevel == LogLevel.NONE ? null : logger.Write,
                OBJWriterCreator = cliOptions.NoOBJ ? null : name => new StreamWriter(Path.Combine(objFolder, name)),
                Options = conversionOptions
            };

            try
            {
                if (!cliOptions.NoOBJ)
                    Directory.CreateDirectory(objFolder);

                if (!cliOptions.VMFToOutputStream)
                    Directory.CreateDirectory(vmfFolder);

                string vmfName = fileNameForSubDirectories + ".vmf";
                string vmfPath = Path.Combine(vmfFolder, vmfName);

                using var t3dText = new StreamReader(t3dPath);
                using var vmfText = cliOptions.VMFToOutputStream ? new StreamWriter(Console.OpenStandardOutput()) : new StreamWriter(vmfPath);

                ConversionResult result = T3DtoVMFConverter.Convert(t3dText, vmfText, data);

                if (cliOptions.WriteResultJSONFile)
                {
                    Directory.CreateDirectory(jsonFolder);

                    string jsonName = fileNameForSubDirectories + ".json";
                    string jsonPath = Path.Combine(jsonFolder, jsonName);

                    using var jsonStream = new StreamWriter(jsonPath);

                    var output = new { Version = CoreInfo.Version, Map = fileName, Result = result };
                    jsonStream.WriteLine(JsonSerializer.Serialize(output, _jsonOptions));
                }
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