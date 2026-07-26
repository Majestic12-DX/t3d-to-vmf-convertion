namespace SourceModels
{
    public class VMFLight : VMFBasePlaceableEntity
    {
        public override string ClassName => "light";
        public double Distance { get; set; } = 0;
        public RGBColor Color { get; set; }
        public override void WriteChunkData(VMFWriter vmfWriter)
        {
            base.WriteChunkData(vmfWriter);

            vmfWriter.AddChunkData("_distance", Distance.ToString());
            vmfWriter.AddChunkData("_light", Color.ToString());
        }
    }
}
