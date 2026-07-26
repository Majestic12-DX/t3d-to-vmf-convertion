namespace SourceModels
{
    // U Axis and V Axis for textures can wait for now...
    public class VMFSide : VMFBase
    {
        protected override string ChunkName => "side";
        public string Material { get; set; } = "dev/dev_measuregeneric01";
        public (Vector Vertex1, Vector Vertex2, Vector Vertex3) Plane { get; set; }
        public int Rotation { get; set; } = 0;
        public int LightMapScale { get; set; } = 16;
        public int SmoothingGroups = 0;

        private double GetDotOffset() => -Vector.Dot(GetNormal(), Plane.Vertex1);
        public Vector GetNormal() => Vector.GetNormal(Plane.Vertex1, Plane.Vertex2, Plane.Vertex3);
        public bool IsPointBehindPlane(Vector vec) => (Vector.Dot(GetNormal(), vec) + GetDotOffset()) <= Vector.CoordinateEpsilon;

        public override void WriteChunkData(VMFWriter vmfWriter)
        {
            vmfWriter.AddChunkData("plane", $"({Plane.Vertex1.ToVMFString()}) ({Plane.Vertex2.ToVMFString()}) ({Plane.Vertex3.ToVMFString()})");
            vmfWriter.AddChunkData("material", Material);
            vmfWriter.AddChunkData("rotation", Rotation.ToString());
            vmfWriter.AddChunkData("lightmapscale", LightMapScale.ToString());
            vmfWriter.AddChunkData("smoothing_groups", SmoothingGroups.ToString());
        }
    }
}
