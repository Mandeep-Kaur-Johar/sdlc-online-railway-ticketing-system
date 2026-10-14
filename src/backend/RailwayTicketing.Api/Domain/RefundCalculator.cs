using RailwayTicketing.Api.Options;

namespace RailwayTicketing.Api.Domain;

/// <summary>
/// Calculates refund entitlement for User Story 3007 using configurable policy bands.
/// </summary>
public static class RefundCalculator
{
    public static (int Percentage, decimal Amount) Calculate(
        decimal paidAmount,
        DateTime departureUtc,
        DateTime nowUtc,
        RefundPolicyOptions policy)
    {
        if (paidAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paidAmount));
        }

        var hoursBefore = (departureUtc - nowUtc).TotalHours;
        var percentage = 0;

        foreach (var band in policy.Bands.OrderByDescending(b => b.MinHoursBeforeDeparture))
        {
            if (hoursBefore >= band.MinHoursBeforeDeparture)
            {
                percentage = band.RefundPercentage;
                break;
            }
        }

        var amount = Math.Round(paidAmount * percentage / 100m, 2, MidpointRounding.AwayFromZero);
        return (percentage, amount);
    }
}
