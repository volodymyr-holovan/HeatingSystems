using HeatingSystems.Core.Models;

namespace HeatingSystems.Core.Calculations;

/// <summary>One outdoor temperature bin (1 K wide) with its number of hours in the heating season.</summary>
public readonly record struct TemperatureBin(double Temperature, double Hours);

/// <summary>
/// Builds an outdoor temperature bin distribution for the heating season (EN 14825 bin method).
/// Hourly temperatures are modelled as a normal distribution N(θ_m,season, σ),
/// σ = (θ_m,season − θ_e) / z, where the design temperature θ_e corresponds to the quantile z = 3.3
/// (≈ 0.05 % of heating season hours are colder than θ_e). The distribution is discretised into
/// 1 K bins and normalised so that Σ hours = heating season hours and Σ θ·hours / Σ hours = θ_m,season.
/// </summary>
public static class TemperatureBins
{
    public const double DesignQuantile = 3.3;

    public static IReadOnlyList<TemperatureBin> Create(ClimateLocation climate)
    {
        var mean = climate.HeatingSeasonMeanTemperature;
        var sigma = Math.Max(1.0, (mean - climate.DesignTemperature) / DesignQuantile);
        var totalHours = climate.HeatingSeasonHours;

        var lo = Math.Floor(mean - 4 * sigma);
        var hi = Math.Ceiling(mean + 4 * sigma);
        var bins = new List<TemperatureBin>();
        for (var t = lo; t <= hi; t += 1.0)
        {
            var p = NormalCdf((t + 0.5 - mean) / sigma) - NormalCdf((t - 0.5 - mean) / sigma);
            if (p > 1e-9) bins.Add(new TemperatureBin(t, p));
        }

        var sum = bins.Sum(x => x.Hours);
        var result = bins.Select(x => x with { Hours = x.Hours / sum * totalHours }).ToList();

        // Discretisation shifts the mean slightly; correct it so that degree-days are preserved exactly.
        var shift = mean - result.Sum(x => x.Temperature * x.Hours) / totalHours;
        return result.Select(x => x with { Temperature = x.Temperature + shift }).ToList();
    }

    /// <summary>Standard normal cumulative distribution function.</summary>
    public static double NormalCdf(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));

    /// <summary>Error function, W. J. Cody rational approximation via erfc (relative error &lt; 1.2·10⁻⁷).</summary>
    public static double Erf(double x)
    {
        // Numerical Recipes erfc (Chebyshev fit), accurate to 1.2e-7 everywhere.
        var z = Math.Abs(x);
        var t = 1.0 / (1.0 + 0.5 * z);
        var ans = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
                  t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 +
                  t * (-0.82215223 + t * 0.17087277)))))))));
        var erfc = x >= 0 ? ans : 2.0 - ans;
        return 1.0 - erfc;
    }
}
