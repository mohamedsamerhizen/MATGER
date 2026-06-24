namespace MATGER.Api.DTOs.Risk;

public sealed class RiskSummaryResponse
{
    public int OpenSignals { get; init; }

    public int CriticalOpenSignals { get; init; }

    public int HighOpenSignals { get; init; }

    public int MediumOpenSignals { get; init; }

    public int LowOpenSignals { get; init; }

    public int ResolvedSignals { get; init; }

    public int DismissedSignals { get; init; }

    public int SignalsCreatedLast24Hours { get; init; }
}
