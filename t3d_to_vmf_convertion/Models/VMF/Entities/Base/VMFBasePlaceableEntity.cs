namespace SourceModels
{
    public abstract class VMFBasePlaceableEntity : VMFBaseEntity
    {
        public Vector Origin { get; set; }
        public Angle Angles { get; set; }

        public List<KeyValuePair<string, string>> KeyValues { get; } = new();

        public override void WriteChunkData(VMFWriter vmfWriter)
        {
            base.WriteChunkData(vmfWriter);

            if (!Origin.IsZero())
                vmfWriter.AddChunkData("origin", Origin.ToVMFString());

            if (!Angles.IsZero())
                vmfWriter.AddChunkData("angles", Angles.ToVMFString());

            // KeyValues can wait for now...
        }
    }
}