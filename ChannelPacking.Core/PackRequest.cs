namespace ChannelPacking.Core;

public sealed record PackRequest(
    PackSlot? Red,
    PackSlot? Green,
    PackSlot? Blue,
    PackSlot? Alpha);
