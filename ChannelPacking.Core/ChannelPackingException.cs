namespace ChannelPacking.Core;

public sealed class ChannelPackingException : Exception
{
    public ChannelPackingException(string message)
        : base(message)
    {
    }

    public ChannelPackingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
