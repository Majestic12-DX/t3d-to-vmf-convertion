using UTModels;

namespace SourceModels
{
    public class VMFSolid : VMFBase
    {
        protected override string ChunkName => "solid";
        public string? UnrealBrushName { get; set; }
        public CSGOperation? UnrealBrushOperation { get; set; }
        public List<VMFSide> Sides { get; private set; } = new(6);

        public override void WriteChildren(VMFWriter vmfWriter)
        {
            foreach (var side in Sides)
                side.WriteToStream(vmfWriter);
        }

        public override void WriteChunkData(VMFWriter vmfWriter)
        {
            base.WriteChunkData(vmfWriter);

            if (UnrealBrushName is not null)
                vmfWriter.AddChunkData("unrealBrushName", UnrealBrushName);

            if (UnrealBrushOperation is not null)
                vmfWriter.AddChunkData("unrealBrushOperation", UnrealBrushOperation.ToString()!);
        }
    }
}
