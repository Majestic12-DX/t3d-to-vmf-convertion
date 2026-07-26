namespace SourceModels
{
    public class VMFWorld : VMFBaseEntity
    {
        protected override string ChunkName => "world";
        public override string ClassName => "worldspawn";
        public string? Message { get; set; }
        public string? ChapterTitle { get; set; }

        public override void WriteChunkData(VMFWriter vmfWriter)
        {
            vmfWriter.AddChunkData("mapversion", "1");

            base.WriteChunkData(vmfWriter);

            if (Message is not null)
                vmfWriter.AddChunkData("message", Message);

            if (ChapterTitle is not null)
                vmfWriter.AddChunkData("chaptertitle", ChapterTitle);

            vmfWriter.AddChunkData("detailmaterial", "detail/detailsprites");
            vmfWriter.AddChunkData("detailvbsp", "detail.vbsp");
            vmfWriter.AddChunkData("maxpropscreenwidth", "-1");
            vmfWriter.AddChunkData("skyname", "sky_day01_01");
        }
    }
}
