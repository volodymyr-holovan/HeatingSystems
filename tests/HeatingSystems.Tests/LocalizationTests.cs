using System.Text.RegularExpressions;
using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Localization;
using HeatingSystems.Core.Reports;
using Xunit;

namespace HeatingSystems.Tests;

/// <summary>Tests that change the global UI language must not run in parallel with other tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizerCollection
{
    public const string Name = "Localizer";
}

[Collection(LocalizerCollection.Name)]
public class LocalizationTests
{
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.Compiled);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HeatingSystems.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [Fact]
    public void StringTables_HaveTheSameKeys()
    {
        var en = Localizer.Keys(AppLanguage.English).ToHashSet();
        var uk = Localizer.Keys(AppLanguage.Ukrainian).ToHashSet();
        Assert.Empty(en.Except(uk));
        Assert.Empty(uk.Except(en));
        Assert.True(en.Count > 300);
    }

    [Fact]
    public void Translations_UseTheSamePlaceholders()
    {
        try
        {
            foreach (var key in Localizer.Keys(AppLanguage.English))
            {
                Localizer.SetLanguage(AppLanguage.English);
                var en = Localizer.T(key);
                Localizer.SetLanguage(AppLanguage.Ukrainian);
                var uk = Localizer.T(key);
                var p1 = Placeholder.Matches(en).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x);
                var p2 = Placeholder.Matches(uk).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x);
                Assert.True(p1.SequenceEqual(p2), $"Placeholders differ for '{key}'");
                Assert.False(string.IsNullOrWhiteSpace(uk), $"Empty translation for '{key}'");
            }
        }
        finally
        {
            Localizer.SetLanguage(AppLanguage.English);
        }
    }

    [Fact]
    public void EveryKeyUsedInSourceCode_Exists()
    {
        var keys = Localizer.Keys(AppLanguage.English).ToHashSet();
        // Any string literal that looks like a localisation key (known prefixes) plus XAML {l:Tr key}.
        const string prefixes = "app|page|building|results|report|comparison|recommendation|catalog|reference|projects|project|status|" +
                                "error|validation|loss|check|source|control|category|unit|option|toolbar|input|language|sort|efficiency|" +
                                "shielding|mass|emitter|roofType|floorType|common";
        var patterns = new[]
        {
            new Regex(@"Localizer\.[TF]\(""([^""]+)"""),
            new Regex(@"\{l:Tr ([a-zA-Z]+\.[\w.]+)\}"),
            new Regex(@"""((?:" + prefixes + @")\.[A-Za-z][\w.]*)"""),
        };
        var src = Path.Combine(RepositoryRoot(), "src");
        var files = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        var missing = new SortedSet<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var pattern in patterns)
            foreach (Match m in pattern.Matches(text))
            foreach (var g in m.Groups.Values.Skip(1).Where(g => g.Success))
                if (!keys.Contains(g.Value)) missing.Add($"{g.Value} ({Path.GetFileName(file)})");
        }
        Assert.True(missing.Count == 0, "Missing keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void ReferenceNames_FollowTheLanguage()
    {
        try
        {
            Assert.Equal("Kyiv", TestData.Kyiv.City);
            Assert.Equal("External walls", Localizer.T("loss.walls"));
            Localizer.SetLanguage(AppLanguage.Ukrainian);
            Assert.Equal("Київ", TestData.Kyiv.City);
            Assert.Equal("Зовнішні стіни", Localizer.T("loss.walls"));
            Assert.Equal("uk-UA", Localizer.Culture.Name);
        }
        finally
        {
            Localizer.SetLanguage(AppLanguage.English);
        }
        Assert.Equal("en-US", Localizer.Culture.Name);
    }

    [Fact]
    public void Report_IsProducedInBothLanguages_WithoutMissingKeys()
    {
        var b = TestData.House();
        var loss = HeatLossCalculator.Calculate(b);
        var demand = EnergyDemandCalculator.Calculate(b, loss);
        var outcome = new CalculationOutcome
        {
            Building = b, HeatLoss = loss, Demand = demand, EnvelopeChecks = Array.Empty<EnvelopeCheckItem>(),
            Recommendations = Array.Empty<Core.Recommendations.Recommendation>(), Options = Array.Empty<SystemOption>(), CalculatedAt = DateTime.Now,
        };
        try
        {
            var en = HtmlReportBuilder.Build(outcome, "P", "src");
            Localizer.SetLanguage(AppLanguage.Ukrainian);
            var uk = HtmlReportBuilder.Build(outcome, "P", "src");
            Assert.Contains("lang=\"en\"", en);
            Assert.Contains("Heat loss (EN 12831)", en);
            Assert.Contains("lang=\"uk\"", uk);
            Assert.Contains("Тепловтрати (EN 12831)", uk);
            Assert.DoesNotContain("[", uk.Replace("[if", ""), StringComparison.Ordinal);
        }
        finally
        {
            Localizer.SetLanguage(AppLanguage.English);
        }
        Assert.Empty(Localizer.MissingKeys);
    }
}
