namespace MATGER.Api.Identity;

public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public bool Enabled { get; set; }

    public int CustomerCount { get; set; } = 24;

    public int ProductsPerCategory { get; set; } = 12;

    public int OrderCount { get; set; } = 240;

    public int RandomSeed { get; set; } = 20260623;

    public string DemoPassword { get; set; } = "Demo12345";
}
