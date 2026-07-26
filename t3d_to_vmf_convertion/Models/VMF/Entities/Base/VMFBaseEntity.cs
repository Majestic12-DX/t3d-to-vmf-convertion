namespace SourceModels
{
    public abstract class VMFBaseEntity : VMFBase
    {
        protected override string ChunkName => "entity";
        public virtual string ClassName { get; set; } = "no classname";
        public string? TargetName { get; set; }
        public List<VMFSolid> Solids { get; set; } = new();
        public override void WriteChunkData(VMFWriter vmfWriter) 
        {
            vmfWriter.AddChunkData("classname", ClassName);

            if (TargetName is not null)
                vmfWriter.AddChunkData("targetname", TargetName);
        }

        public override void WriteChildren(VMFWriter vmfWriter) 
        { 
            foreach (var solid in Solids)
                solid.WriteToStream(vmfWriter);
        }
    }
}