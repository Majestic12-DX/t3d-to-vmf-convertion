// Been a long time since I worked with C#
// Love both of these engines, to be honest :)

using T3DtoVMF.Core.Public;

static class Time
{
    public static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
}

static class Close
{
    public const int Success = 0;
    public const int BadT3D = 1;
    public const int UnexpectedError = 2;
    public const int BadArguments = 3;
    public static int OnPress(int exitCode = Success)
    {
        Console.WriteLine("Press any button to exit...");
        Console.ReadKey();

        return exitCode;
    }
}

sealed class CLILogger : IDisposable
{
    private const string _logFolder = "t3d_to_vmf_logs";
    private readonly StreamWriter? _file;
    private static readonly Dictionary<LogLevel, ConsoleColor> _colors = new()
    {
        { LogLevel.INFO, ConsoleColor.Cyan },
        { LogLevel.WARNING, ConsoleColor.Yellow },
        { LogLevel.ERROR, ConsoleColor.Red },
        { LogLevel.DEBUG, ConsoleColor.DarkGreen },
    };

    public CLILogger(string t3dFileName)
    {
        try
        {
            Directory.CreateDirectory(_logFolder);
            string logName = $"{Path.GetFileName(t3dFileName)} {Time.Now()}.log";
            _file = new StreamWriter(Path.Combine(_logFolder, logName)) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Couldn't create log file, logging to console only: {ex.Message}");
        }
    }

    public void Write(LogLevel level, string message)
    {
        string line = $"[{Time.Now()}] [{level}] {message}";
        _file?.WriteLine(line);

#if !DEBUG
        if (level == LogLevel.DEBUG) return;
#endif

        Console.ForegroundColor = _colors[level];
        Console.WriteLine(line);

        if (level == LogLevel.ERROR) Console.WriteLine();

        Console.ResetColor();
    }

    public void Dispose() => _file?.Dispose();
}

class Program
{
    // Test Files: Every .t3d in Unreal Tournament Maps folder
    static int Main(string[] args)
    {
        string version = "Version 1.3";
        string title = $"UT99 .t3d to .vmf Converter | {version}";
        Console.Title = title;

        // Basic File Checking
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Please drag'n'drop a .t3d file into this application!");
            return Close.OnPress(Close.BadArguments);
        }

        string filePath = args[0];
        if (Path.GetExtension(filePath).ToLower() != ".t3d")
        {
            Console.WriteLine($"Error: File Extension is not .t3d ({Path.GetFileName(filePath)})");
            return Close.OnPress(Close.BadArguments);
        }

        string fileName = Path.GetFileName(filePath);
        using var logger = new CLILogger(fileName);

        string objFolder = "t3d_to_vmf_objs";

        Directory.CreateDirectory(objFolder); 
        var data = new ConversionData
        {
            LogCallback = logger.Write,
            OBJWriterCreator = name => new StreamWriter(Path.Combine(objFolder, name))
        };

        ConversionResult result;
        try
        {
            using var t3dText = new StreamReader(filePath);

            string folder = "t3d_to_vmf_vmfs";
            Directory.CreateDirectory(folder);

            string vmfName = $"{Path.GetFileNameWithoutExtension(fileName)} {Time.Now()}.vmf";
            using var vmfText = new StreamWriter(Path.Combine(folder, vmfName));

            result = T3DtoVMFConverter.Convert(t3dText, vmfText, data);
        }
        catch (FormatException ex)
        {
            logger.Write(LogLevel.ERROR, $"Format Exception, bad .t3d: {ex.Message}");
            return Close.OnPress(Close.BadT3D);
        }
        catch (Exception ex)
        {
            logger.Write(LogLevel.ERROR, $"Unexpected Exception: {ex}");
            return Close.OnPress(Close.UnexpectedError);
        }

        Console.WriteLine($"Convertation Result: " +
            $"\n\t{result.TotalTime} time taken, " +
            $"\n\t{result.BrushCount} brushes" +
            $"\n\t{result.BrushSidesCount} brush sides" +
            $"\n\t{result.BadBrushCount} bad brushes" +
            $"\n\t{result.BadBrushSidesCount} bad brush sides" +
            $"\n\t{result.UniqueVerticesCount} unique vertices\n");

        return Close.OnPress();
    }
}