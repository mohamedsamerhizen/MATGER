namespace MATGER.Api.DTOs.Addresses;

public sealed class AddressResponse
{
    public Guid Id { get; init; }

    public string Label { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? Area { get; init; }

    public string Street { get; init; } = string.Empty;

    public string? Building { get; init; }

    public string? Floor { get; init; }

    public string? Apartment { get; init; }

    public string? PostalCode { get; init; }

    public string? Notes { get; init; }

    public bool IsDefault { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
