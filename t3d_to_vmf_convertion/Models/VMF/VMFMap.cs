namespace SourceModels
{
    public class VMFMap : IVMFSerializable
    {
        public VMFWorld World { get; set; } = new();
        public List<VMFBaseEntity> Entities { get; set; } = new();
        public void WriteToStream(VMFWriter vmfWriter)
        {
            // Writing typical VMF header
            // I guess. I'm not a mapper
            vmfWriter.PushChunk("versioninfo");
            vmfWriter.AddChunkData("editorversion", "400");
            vmfWriter.AddChunkData("editorbuild", "8870");
            vmfWriter.AddChunkData("mapversion", "1");
            vmfWriter.AddChunkData("formatversion", "100");
            vmfWriter.AddChunkData("prefab", "0");
            vmfWriter.FlushChunkDataAndPop();

            vmfWriter.PushChunk("visgroups");
            vmfWriter.PopChunk();

            vmfWriter.PushChunk("viewsettings");
            vmfWriter.AddChunkData("bSnapToGrid", "1");
            vmfWriter.AddChunkData("bShowGrid", "1");
            vmfWriter.AddChunkData("bShowLogicalGrid", "1");
            vmfWriter.AddChunkData("nGridSpacing", "8");
            vmfWriter.FlushChunkDataAndPop();

            vmfWriter.PushChunk("cameras");
            vmfWriter.AddChunkData("activecamera", "-1");
            vmfWriter.FlushChunkDataAndPop();

            World.WriteToStream(vmfWriter);

            foreach (var entity in Entities)
                entity.WriteToStream(vmfWriter);
        }

        public int GetBrushCount()
        {
            int count = World.Solids.Count;
            foreach (var entity in Entities)
                count += entity.Solids.Count;

            return count;
        }

        public int GetBrushSidesCount()
        {
            int count = 0;
            foreach (var solid in World.Solids)
                count += solid.Sides.Count;

            foreach (var entity in Entities)
                foreach (var solid in entity.Solids)
                    count += solid.Sides.Count;

            return count;
        }
    }
}
