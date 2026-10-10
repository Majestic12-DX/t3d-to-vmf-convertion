namespace T3DtoVMF.Cli.Utils
{
    static class ArgsHandler
    {
        private static string? GetArg(string[] args, int index)
        {
            return index < args.Length ? args[index] : null;
        }

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "-noobj": options.NoOBJ = true; break;
                    
                    case "-nologfile": options.NoLogFile = true; break;

                    case "-loglevel":
                        string? value = GetArg(args, ++i);
                        if (value is null)
                        {
                            Console.Error.WriteLine("Warning: -loglevel needs a valid log level value");
                            break;
                        }
                        bool success = Enum.TryParse(value, true, out LogLevel consoleLogLevel);
                        if (success && Enum.IsDefined(typeof(LogLevel), consoleLogLevel))
                            options.LoggingLevel = consoleLogLevel;
                        else
                            Console.Error.WriteLine($"Warning: Invalid value for -loglevel");
                        break;
                    
                    case "-outputfolder":
                        string? outputFolder = GetArg(args, ++i);
                        if (outputFolder is null)
                        {
                            Console.Error.WriteLine("Warning: -outputfolder needs a valid directory path");
                            break;
                        }

                        if (File.Exists(outputFolder))
                        {
                            Console.Error.WriteLine($"Warning: -outputfolder {outputFolder} is a file! Skipping");
                            break;
                        }

                        options.OutputFolder = outputFolder;

                        break;

                    default:
                        if (!Path.GetExtension(arg).Equals(".t3d", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.Error.WriteLine($"Warning: Unknown arg {arg}! Skipping");
                            break;
                        }

                        if (!File.Exists(arg))
                        {
                            Console.Error.WriteLine($"Error: {Path.GetFileName(arg)} not found! Skipping");
                            Close.SetExitCodeIfWorse(Close.FileMissing);

                            break;
                        }

                        options.T3ds.Add(arg);
                        break;
                }
            }

            return options;
        }
    }
}
