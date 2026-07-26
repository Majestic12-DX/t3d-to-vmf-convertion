namespace SourceModels
{
    public interface IVMFSerializable
    {
        void WriteToStream(VMFWriter vmfWriter);
    }
}
