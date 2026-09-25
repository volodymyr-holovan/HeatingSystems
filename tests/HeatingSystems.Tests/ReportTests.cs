using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Reports;
using Xunit;

namespace HeatingSystems.Tests;

[Collection(LocalizerCollection.Name)]
public class ReportTests(CatalogFixture db) : IClassFixture<CatalogFixture>
{
    [Fact]
    public void CalculationService_ProducesConsistentOutcome()
    {
        var climate = db.Catalog.GetClimateLocations().Single(c => c.CityEn == "Kyiv");
        var outcome = new CalculationService(db.Catalog).Calculate(TestData.House(climate));

        Assert.True(outcome.HeatLoss.DesignHeatLoad > 0);
        Assert.NotEmpty(outcome.Recommendations);
        Assert.Equal(db.Catalog.GetHeatingTechnologies().Count + outcome.Recommendations.Select(r => r.Result.HeatPump.Source).Distinct().Count(),
            outcome.Options.Count);
        Assert.True(outcome.Options.Zip(outcome.Options.Skip(1)).All(p => p.First.AnnualCost <= p.Second.AnnualCost));
    }

    [Fact]
    public void HtmlReport_ContainsResultsAndEscapesText()
    {
        var climate = db.Catalog.GetClimateLocations().Single(c => c.CityEn == "Kyiv");
        var outcome = new CalculationService(db.Catalog).Calculate(TestData.House(climate));

        var html = HtmlReportBuilder.Build(outcome, "House <script>", "KEYMARK");

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("House &lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("Heat loss (EN 12831)", html);
        Assert.Contains(outcome.Recommendations[0].Result.HeatPump.Model.Replace("&", "&amp;"), html);
    }
}
