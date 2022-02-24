namespace SharpUtilities.Extensions;

public static class MemoryStreamExtensions
{
    public static ReadOnlyMemory<byte> GetReadOnlyMemory(this MemoryStream memoryStream)
    {
        ReadOnlyMemory<byte> memoryStreamMemory = memoryStream.GetBuffer();
        memoryStreamMemory = memoryStreamMemory.Slice(0, (int)memoryStream.Length);
        return memoryStreamMemory;
    }
}
