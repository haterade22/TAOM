namespace TAOM.Features.SupplyLines.Domain;

/// <summary>Price breakdown for one prospective order, in denars. Pure data.</summary>
public readonly struct SupplyQuote
{
    public SupplyQuote(int goods, int troops, int transport, int guard)
    {
        Goods = goods;
        Troops = troops;
        Transport = transport;
        Guard = guard;
    }

    public int Goods { get; }
    public int Troops { get; }
    public int Transport { get; }
    public int Guard { get; }
    public int Total => Goods + Troops + Transport + Guard;
}
