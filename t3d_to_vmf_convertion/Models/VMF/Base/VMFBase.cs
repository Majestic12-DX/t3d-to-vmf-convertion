namespace SourceModels
{
    public abstract class VMFBase : IVMFSerializable
    {
        protected virtual string ChunkName { get; set; } = "base";
        private static int _nextId = 1;
        public int Id { get; set; } = 0;
        public VMFBase()
        {
            Id = _nextId++;
        }

        public virtual void WriteChunkData(VMFWriter vmfWriter) { }
        public virtual void WriteChildren(VMFWriter vmfWriter) { }
        public void WriteToStream(VMFWriter vmfWriter)
        {
            vmfWriter.PushChunk(ChunkName);
            vmfWriter.AddChunkData("id", Id.ToString());

            WriteChunkData(vmfWriter);

            vmfWriter.FlushChunkData();

            WriteChildren(vmfWriter);

            vmfWriter.PopChunk();
        }
    }
}
