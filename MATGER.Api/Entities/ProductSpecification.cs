namespace MATGER.Api.Entities;

public sealed class ProductSpecification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? GroupName { get; set; }

    public int SortOrder { get; set; }
}
