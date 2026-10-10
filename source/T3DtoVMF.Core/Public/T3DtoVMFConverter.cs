using System.Diagnostics;

namespace T3DtoVMF.Core.Public
{
    public static class T3DtoVMFConverter
    {
        public static ConversionResult Convert(TextReader t3d, TextWriter vmf, ConversionData data)
        {
            Settings.Data = data;

            Logger.Write(LogLevel.INFO, $"Started Conversion");

            try
            {
                var readTimer = Stopwatch.StartNew();
                var actors = new T3DReader().ReadT3DActors(t3d);
                readTimer.Stop();

                var writeTimer = Stopwatch.StartNew();
                var stats = new VMFWriter().WriteVMFFromUnrealActors(actors, vmf);
                writeTimer.Stop();

                Logger.Write(LogLevel.INFO, "Finished Conversion");

                return new ConversionResult
                {
                    T3DReadTime = readTimer.Elapsed,
                    VMFWriteTime = writeTimer.Elapsed,
                    BrushCount = stats.BrushCount,
                    BrushSidesCount = stats.BrushSidesCount,
                    BadBrushCount = stats.BadBrushCount,
                    BadBrushSidesCount = stats.BadBrushSidesCount,
                    UniqueVerticesCount = stats.UniqueVerticesCount
                };
            }
            finally
            {
                Settings.Data = new();
            }
        }
    }
}