using System.ComponentModel.DataAnnotations;

namespace MATGER.Api.DTOs.Addresses;

public sealed class UpdateAddressRequest
{
    [MaxLength(80)]
    public string? Label { get; init; }

    [MaxLength(150)]
    public string? FullName { get; init; }

    [MaxLength(40)]
    public string? PhoneNumber { get; init; }

    [MaxLength(100)]
    public string? Country { get; init; }

    [MaxLength(100)]
    public string? City { get; init; }

    [MaxLength(120)]
    public string? Area { get; init; }

    [MaxLength(200)]
    public string? Street { get; init; }

    [MaxLength(80)]
    public string? Building { get; init; }

    [MaxLength(80)]
    public string? Floor { get; init; }

    [MaxLength(80)]
    public string? Apartment { get; init; }

    [MaxLength(40)]
    public string? PostalCode { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }

    public bool? IsDefault { get; init; }
}
