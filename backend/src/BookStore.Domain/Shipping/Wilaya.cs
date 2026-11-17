using BookStore.Domain.Common;

namespace BookStore.Domain.Shipping;

/// <summary>
/// An Algerian province. Keyed by its official number (1–58) rather than a GUID, since the
/// code itself is meaningful (e.g. 16 = Alger) and appears on addresses.
/// </summary>
public sealed class Wilaya
{
    /// <summary>Uniform delivery fee seeded for every wilaya; the admin edits it per wilaya.</summary>
    public const int DefaultShippingFee = 600;

    private Wilaya()
    {
        NameAr = NameFr = string.Empty;
    }

    public Wilaya(int code, string nameAr, string nameFr, int shippingFee = DefaultShippingFee)
    {
        Code = code;
        NameAr = nameAr;
        NameFr = nameFr;
        SetShippingFee(shippingFee);
    }

    public int Code { get; private set; }
    public string NameAr { get; private set; }
    public string NameFr { get; private set; }

    /// <summary>Delivery fee in DZD for orders shipped to this wilaya.</summary>
    public int ShippingFee { get; private set; }

    /// <summary>Lets the admin stop deliveries to a wilaya without deleting it.</summary>
    public bool IsDeliveryAvailable { get; private set; } = true;

    public void SetShippingFee(int fee)
    {
        if (fee < 0)
        {
            throw new DomainException("Shipping fee cannot be negative.");
        }

        ShippingFee = fee;
    }

    public void SetDeliveryAvailable(bool available) => IsDeliveryAvailable = available;
}
