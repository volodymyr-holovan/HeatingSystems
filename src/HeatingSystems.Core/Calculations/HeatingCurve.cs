namespace HeatingSystems.Core.Calculations;

/// <summary>
/// Weather-compensated flow temperature of the heating circuit derived from the emitter characteristic
/// Φ/Φ_design = (Δθ/Δθ_design)^n: θ_flow = θ_int + (θ_flow,d − θ_int)·x^(1/n), x = (θ_int − θ_e,actual)/(θ_int − θ_e,design).
/// </summary>
public static class HeatingCurve
{
    /// <summary>Lowest flow temperature supplied by the controller, °C.</summary>
    public const double MinimumFlowTemperature = 25.0;

    public static double FlowTemperature(
        double outdoorTemperature,
        double indoorTemperature,
        double designOutdoorTemperature,
        double designFlowTemperature,
        double emitterExponent)
    {
        var x = Math.Clamp((indoorTemperature - outdoorTemperature) / (indoorTemperature - designOutdoorTemperature), 0.0, 1.0);
        var flow = indoorTemperature + (designFlowTemperature - indoorTemperature) * Math.Pow(x, 1.0 / emitterExponent);
        return Math.Max(MinimumFlowTemperature, Math.Min(flow, designFlowTemperature));
    }
}
