using System.Globalization;
using T3DtoVMF.Core.Public;

namespace T3DtoVMF.Cli.Utils
{
    static class ArgsHandler
    {
        private static string[] _args = [];
        private static int _index;

        private static string? GetNextArg()
        {
            _index++;
            return _index < _args.Length ? _args[_index] : null;
        }

        private static int? GetInt()
            => int.TryParse(GetNextArg(), CultureInfo.InvariantCulture, out int value) ? value : null;

        private static double? GetDouble()
            => double.TryParse(GetNextArg(), CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : null;

        public static (CliOptions, ConversionOptions) Parse(string[] args)
        {
            var cliOptions = new CliOptions();
            var conversionOptions = new ConversionOptions();

            _args = args;
            for (_index = 0; _index < _args.Length; _index++)
            {
                string arg = _args[_index];
                switch (arg.ToLowerInvariant())
                {
                    #region CLI
                    case "-noobj": cliOptions.NoOBJ = true; break;
                    
                    case "-nologfile": cliOptions.NoLogFile = true; break;

                    case "-loglevel":
                        string? value = GetNextArg();
                        if (value is null)
                        {
                            Console.Error.WriteLine("Warning: -loglevel needs a valid log level value (for example: error)");
                            break;
                        }

                        bool successfulLogParse = Enum.TryParse(value, true, out LogLevel consoleLogLevel);
                        if (successfulLogParse && Enum.IsDefined(typeof(LogLevel), consoleLogLevel))
                            cliOptions.LoggingLevel = consoleLogLevel;
                        else
                            Console.Error.WriteLine($"Warning: Couldn't parse enum for -loglevel value \"{value}\"");

                        break;
                    
                    case "-outputfolder":
                        string? outputFolder = GetNextArg();
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

                        cliOptions.OutputFolder = outputFolder;

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

                        cliOptions.T3ds.Add(arg);
                        break;
                    #endregion

                    #region Conversion
                    case "-trynonmanifoldplanarholesfixing":
                        conversionOptions.TryNonManifoldPlanarHolesFixing = true;
                        break;

                    case "-skipconcave":
                        conversionOptions.SkipConcave = true;
                        break;

                    case "-skipconvex":
                        conversionOptions.SkipConvex = true;
                        break;

                    case "-skipnonmanifold":
                        conversionOptions.SkipNonManifold = true;
                        break;

                    case "-skipmovers":
                        conversionOptions.SkipMovers = true;
                        break;

                    case "-nocsg":
                        conversionOptions.NoCSG = true;
                        break;

                    case "-nomoverbehavior":
                        conversionOptions.NoMoverBehavior = true;
                        break;

                    case "-noareaportals":
                        conversionOptions.NoAreaPortals = true;
                        break;

                    case "-nooutofbounds":
                        conversionOptions.NoOutOfBounds = true;
                        break;

                    case "-nolights":
                        conversionOptions.NoLights = true;
                        break;

                    case "-noplayerspawns":
                        conversionOptions.NoPlayerSpawns = true;
                        break;

                    case "-includeactive":
                        conversionOptions.IncludeActive = true;
                        break;

                    case "-scale":
                        double? scale = GetDouble();
                        if (scale is null)
                        {
                            Console.Error.WriteLine("Warning: -scale needs a valid scale value (for example: 0.92307692307)"); // 72 / 78, Unreal player is a little taller
                            break;
                        }

                        if (scale == 0)
                        {
                            Console.Error.WriteLine("Warning: -scale 0 would give you garbage, skipping!");
                            break;
                        }

                        conversionOptions.Scale = scale.Value;
                        break;

                    case "-walkablesurfacenormal":
                        double? walkableNormal = GetDouble();
                        if (walkableNormal is null)
                        {
                            Console.Error.WriteLine("Warning: -walkablesurfacenormal needs a valid normal value (for example: 0.7)"); // Unreal seems to use 0.7 as well?
                            break;
                        }

                        conversionOptions.WalkableSurface_Normal = walkableNormal.Value;
                        break;

                    case "-lightmap_walkable":
                        int? lightmapWalkable = GetInt();
                        if (lightmapWalkable is null)
                        {
                            Console.Error.WriteLine("Warning: -lightmap_walkable needs a valid lightmap scale value (for example: 16)");
                            break;
                        }

                        conversionOptions.LightmapScale_WalkableSurface = lightmapWalkable.Value;
                        break;

                    case "-lightmap_wall":
                        int? lightmapWall = GetInt();
                        if (lightmapWall is null)
                        {
                            Console.Error.WriteLine("Warning: -lightmap_wall needs a valid lightmap scale value (for example: 16)");
                            break;
                        }

                        conversionOptions.LightmapScale_Wall = lightmapWall.Value;

                        break;

                    case "-lightmap_ceiling":
                        int? lightmapCeiling = GetInt();
                        if (lightmapCeiling is null)
                        {
                            Console.Error.WriteLine("Warning: -lightmap_ceiling needs a valid lightmap scale value (for example: 16)");
                            break;
                        }

                        conversionOptions.LightmapScale_Ceiling = lightmapCeiling.Value;

                        break;
                    #endregion
                }
            }

            _args = [];
            return (cliOptions, conversionOptions);
        }
    }
}
