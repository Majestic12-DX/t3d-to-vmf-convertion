namespace SourceModels
{
    interface IVMFSerializable
    {
        void WriteToStream(VMFWriter vmfWriter);
    }
}
