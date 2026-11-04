using System.Globalization;

namespace SourceModels
{
    class VMFLight : VMFBasePlaceableEntity
    {
        public override string ClassName => "light";
        public double Distance { get; set; } = 0;
        public RGBColor Color { get; set; }
        public override void WriteChunkData(VMFWriter vmfWriter)
        {
            base.WriteChunkData(vmfWriter);

            vmfWriter.AddChunkData("_distance", Distance.ToString(CultureInfo.InvariantCulture));
            vmfWriter.AddChunkData("_fifty_percent_distance", (Distance / 2).ToString(CultureInfo.InvariantCulture));
            vmfWriter.AddChunkData("_zero_percent_distance", Distance.ToString(CultureInfo.InvariantCulture));
            vmfWriter.AddChunkData("_hardfalloff", "1");
            vmfWriter.AddChunkData("_light", Color.ToString());
        }
    }
}
