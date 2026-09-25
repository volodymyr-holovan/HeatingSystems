using HeatingSystems.Core.Calculations;
using HeatingSystems.Core.Models;
using Xunit;

namespace HeatingSystems.Tests;

public class ThermalTransmittanceTests
{
    [Fact]
    public void WallUValue_MatchesHandCalculation()
    {
        // R = 0.13 + 0.38/0.81 + 0.10/0.040 + 0.02/0.93 + 0.04 = 3.1606 m²K/W
        var u = ThermalTransmittance.UValue(TestData.House().Wall, HeatFlowDirection.Horizontal);
        Assert.Equal(0.31639, u, 4);
    }

    [Fact]
    public void UnheatedSpace_UsesInternalResistanceOnBothSides()
    {
        var c = new Construction(new[] { new Layer(TestData.Concrete, 0.2) });
        var expected = 1 / (0.10 + 0.2 / 2.04 + 0.10);
        Assert.Equal(expected, ThermalTransmittance.UValue(c, HeatFlowDirection.Upwards, adjoinsUnheatedSpace: true), 10);
    }

    [Fact]
    public void SlabOnGround_UninsulatedBranch_MatchesIso13370()
    {
        // B' = 100/(0.5·40) = 5 m; d_t = 0.4 + 2·(0.17 + 0 + 0.04) = 0.82 m < B'
        var u = GroundHeatTransfer.SlabUValue(100, 40, 0.4, Construction.Empty);
        Assert.Equal(0.72689, u, 4);
    }

    [Fact]
    public void SlabOnGround_WellInsulatedBranch_MatchesIso13370()
    {
        // d_t = 0.4 + 2·(0.17 + 0.3/0.04 + 0.04) = 15.82 m ≥ B' = 5 m → U = 2/(0.457·5 + 15.82)
        var floor = new Construction(new[] { new Layer(TestData.Eps, 0.3) });
        Assert.Equal(2 / (0.457 * 5 + 15.82), GroundHeatTransfer.SlabUValue(100, 40, 0.4, floor), 10);
    }

    [Fact]
    public void PeriodicGroundCoefficient_MatchesIso13370()
    {
        Assert.Equal(47.0561, GroundHeatTransfer.PeriodicCoefficient(40, 0.4, Construction.Empty), 3);
    }
}

public class HeatLossTests
{
    [Fact]
    public void NaturalVentilation_UsesMaximumOfInfiltrationAndHygienicFlow()
    {
        // V_inf = 250·4·0.03 = 30 m³/h; V_min = 0.5·250 = 125 m³/h
        Assert.Equal(125, HeatLossCalculator.EffectiveAirFlow(TestData.House()), 9);
    }

    [Fact]
    public void MechanicalVentilation_AppliesHeatRecovery()
    {
        var b = TestData.House();
        var mech = new BuildingInput
        {
            Climate = b.Climate, HeatedFloorArea = 100, HeatedVolume = 250, AirTightnessN50 = 4, ShieldingCoefficient = 0.03,
            MinimumAirChangeRate = 0.5, MechanicalVentilation = true, HeatRecoveryEfficiency = 0.8,
        };
        Assert.Equal(30 + 125 * 0.2, HeatLossCalculator.EffectiveAirFlow(mech), 9);
    }

    [Fact]
    public void DesignLoad_IsSumOfComponents()
    {
        var b = TestData.House();
        var r = HeatLossCalculator.Calculate(b);
        const double dT = 42;
        var wall = (120 - 15 - 2) * (0.316391 + 0.05) * dT;
        var window = 15 * (1.3 + 0.05) * dT;
        var door = 2 * (1.8 + 0.05) * dT;
        var roofU = 1 / (0.10 + 0.2 / 2.04 + 0.2 / 0.037 + 0.10);
        var roof = 100 * (roofU + 0.05) * 0.9 * dT;
        var vent = 0.34 * 125 * dT;
        var ground = r.GroundLoss;

        Assert.Equal(wall + window + door + roof + vent + ground, r.DesignHeatLoad, 0);
        Assert.Equal(vent, r.VentilationLoss, 6);
        // EN 12831: Φ_g = 1.45·(θi − θm,e)/(θi − θe)·A·U·(θi − θe) = 1.45·A·U·(20 − 7.7)
        var groundU = GroundHeatTransfer.SlabUValue(100, 40, 0.5, b.Floor);
        Assert.Equal(1.45 * 100 * groundU * 12.3, ground, 6);
        Assert.Equal(r.DesignHeatLoad / 100, r.SpecificHeatLoad, 9);
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        var invalid = new BuildingInput { Climate = TestData.Kyiv, HeatedFloorArea = 0, HeatedVolume = 0 };
        Assert.Throws<ArgumentException>(() => HeatLossCalculator.Calculate(invalid));
    }

    [Fact]
    public void BetterInsulation_ReducesLoad()
    {
        var baseLoad = HeatLossCalculator.Calculate(TestData.House()).DesignHeatLoad;
        var b = TestData.House();
        var better = new BuildingInput
        {
            Climate = b.Climate, HeatedFloorArea = 100, HeatedVolume = 250, ExternalWallAreaGross = 120,
            Wall = new Construction(new[] { new Layer(TestData.Brick, 0.38), new Layer(TestData.Eps, 0.20) }),
            WindowArea = 15, Window = TestData.Pvc, DoorArea = 2, RoofArea = 100, Roof = b.Roof,
            FloorArea = 100, Floor = b.Floor, FloorPerimeter = 40,
        };
        Assert.True(HeatLossCalculator.Calculate(better).DesignHeatLoad < baseLoad);
    }
}

public class TemperatureBinTests
{
    [Fact]
    public void Bins_PreserveHoursAndMeanTemperature()
    {
        var bins = TemperatureBins.Create(TestData.Kyiv);
        var hours = bins.Sum(b => b.Hours);
        Assert.Equal(176 * 24, hours, 6);
        Assert.Equal(-0.1, bins.Sum(b => b.Temperature * b.Hours) / hours, 9);
        Assert.Contains(bins, b => b.Temperature < TestData.Kyiv.DesignTemperature);
    }

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(1.0, 0.841344746)]
    [InlineData(-1.96, 0.024997895)]
    [InlineData(3.3, 0.999516576)]
    public void NormalCdf_IsAccurate(double x, double expected) =>
        Assert.Equal(expected, TemperatureBins.NormalCdf(x), 6);
}

public class EnergyDemandTests
{
    [Fact]
    public void WithoutGains_EqualsDegreeDayMethod()
    {
        var b = TestData.House(internalGains: 0, hotWater: false);
        var loss = HeatLossCalculator.Calculate(b);
        var demand = EnergyDemandCalculator.Calculate(b, loss);

        var dd = b.Climate.DegreeDays(20); // 176 · 20.1 = 3537.6 K·d
        var air = (loss.TransmissionCoefficient + loss.VentilationCoefficient) * dd * 24 / 1000;
        var ground = (loss.GroundCoefficient * (20 - 7.7) + loss.GroundPeriodicCoefficient * (7.7 + 0.1)) * 176 * 24 / 1000;
        Assert.Equal(air + ground, demand.SpaceHeating, 0);
        Assert.Equal(0, demand.HotWater);
    }

    [Fact]
    public void InternalGains_ReduceDemand_ButNotBelowZero()
    {
        var b0 = TestData.House(internalGains: 0);
        var b4 = TestData.House(internalGains: 4);
        var loss = HeatLossCalculator.Calculate(b0);
        var d0 = EnergyDemandCalculator.Calculate(b0, loss);
        var d4 = EnergyDemandCalculator.Calculate(b4, loss);
        Assert.True(d4.SpaceHeating < d0.SpaceHeating);
        Assert.True(d4.UtilisedGains <= d4.InternalGains);
        Assert.All(d4.Bins, bin => Assert.True(bin.Load >= 0));
    }

    [Fact]
    public void GainUtilisation_FollowsIso13790()
    {
        Assert.Equal(3.0 / 4.0, EnergyDemandCalculator.GainUtilisation(100, 100, 3), 12);
        Assert.Equal((1 - Math.Pow(0.5, 3)) / (1 - Math.Pow(0.5, 4)), EnergyDemandCalculator.GainUtilisation(50, 100, 3), 12);
        Assert.Equal(1.0, EnergyDemandCalculator.GainUtilisation(0, 100, 3));
    }

    [Fact]
    public void HotWaterDemand_MatchesFormula()
    {
        // 3 persons · 45 l · 365 d · 1.163 Wh/(l·K) · 45 K · 1.15
        var expected = 3 * 45 * 365 * 1.163e-3 * 45 * 1.15;
        Assert.Equal(expected, EnergyDemandCalculator.HotWaterDemand(TestData.House()), 9);
    }

    [Fact]
    public void HeatingCurve_ReachesDesignFlowAtDesignTemperature()
    {
        Assert.Equal(55, HeatingCurve.FlowTemperature(-22, 20, -22, 55, 1.3), 9);
        Assert.Equal(HeatingCurve.MinimumFlowTemperature, HeatingCurve.FlowTemperature(20, 20, -22, 55, 1.3), 9);
        var mid = HeatingCurve.FlowTemperature(-1, 20, -22, 55, 1.3);
        Assert.Equal(20 + 35 * Math.Pow(0.5, 1 / 1.3), mid, 9);
    }
}

public class HeatPumpTests
{
    [Fact]
    public void OperatingPoint_ImplementsHplibModel()
    {
        var hp = TestData.AirHeatPump();
        var op = HeatPumpSimulator.OperatingPoint(hp, 7, 35);
        var cop = hp.CopP1 * 7 + hp.CopP2 * 35 + hp.CopP3 + hp.CopP4 * 7;
        // Regulated unit: P_el,max = P_el,ref; P_el,min = 0.25·P_el(T_in = T_amb = −7 °C).
        var pelMin = 0.25 * hp.ElectricPowerRef * (hp.PelP1 * -7 + hp.PelP2 * 35 + hp.PelP3 + hp.PelP4 * -7);
        Assert.Equal(cop, op.Cop, 10);
        Assert.Equal(hp.ElectricPowerRef * cop, op.MaxThermalPower, 6);
        Assert.Equal(pelMin * cop, op.MinThermalPower, 6);
        Assert.InRange(op.Cop, 3.5, 7.0);
    }

    [Fact]
    public void Operate_AppliesCyclingDegradationBelowMinimumOutput()
    {
        var op = new HeatPumpOperatingPoint(4.0, 8000, 2000);
        Assert.Equal((4000.0, 1000.0), HeatPumpSimulator.Operate(op, 4000));
        Assert.Equal((8000.0, 2000.0), HeatPumpSimulator.Operate(op, 10000));
        var (heat, el) = HeatPumpSimulator.Operate(op, 1000); // CR = 0.5 → COP = 4·0.5/(0.45 + 0.1)
        Assert.Equal(1000, heat);
        Assert.Equal(1000 / (4.0 * 0.5 / 0.55), el, 9);
    }

    [Fact]
    public void ModelScop_ReproducesCertifiedScopOfReferenceProduct()
    {
        var hp = TestData.AirHeatPump();
        Assert.InRange(En14825.ModelScop(hp), hp.Scop * 0.9, hp.Scop * 1.1);
    }

    [Fact]
    public void OperatingPoint_BelowOperationLimit_IsUnavailable()
    {
        Assert.False(HeatPumpSimulator.OperatingPoint(TestData.AirHeatPump(), -15, 45).IsAvailable);
        Assert.False(HeatPumpSimulator.OperatingPoint(TestData.AirHeatPump(), 0, 80).IsAvailable);
    }

    [Fact]
    public void Cop_DecreasesWithFlowTemperature()
    {
        var hp = TestData.AirHeatPump();
        Assert.True(HeatPumpSimulator.OperatingPoint(hp, 2, 35).Cop > HeatPumpSimulator.OperatingPoint(hp, 2, 55).Cop);
    }

    [Fact]
    public void Simulation_EnergyBalanceIsConsistent()
    {
        var b = TestData.House(emitter: EmitterType.Underfloor);
        var loss = HeatLossCalculator.Calculate(b);
        var demand = EnergyDemandCalculator.Calculate(b, loss);
        var r = HeatPumpSimulator.Simulate(TestData.AirHeatPump(), b, loss, demand);

        Assert.Equal(demand.SpaceHeating, r.SpaceHeating, 6);
        Assert.Equal(demand.HotWater, r.HotWaterHeat, 6);
        Assert.InRange(r.SpaceHeatingSpf, 2.0, 6.5);
        Assert.True(r.HeatFromBackup > 0, "TOL = −10 °C → back-up heater needed in the coldest bins");
        Assert.NotNull(r.BivalentTemperature);
    }

    [Fact]
    public void LowerFlowTemperature_GivesHigherSpf()
    {
        double Spf(EmitterType e)
        {
            var b = TestData.House(emitter: e);
            var loss = HeatLossCalculator.Calculate(b);
            return HeatPumpSimulator.Simulate(TestData.AirHeatPump(2), b, loss, EnergyDemandCalculator.Calculate(b, loss)).SpaceHeatingSpf;
        }
        Assert.True(Spf(EmitterType.Underfloor) > Spf(EmitterType.Radiators));
    }
}

public class SystemComparisonTests
{
    [Fact]
    public void CondensingBoiler_EfficiencyDependsOnFlowTemperature()
    {
        var t = new HeatingTechnology("gas_condensing", "Газ", "natural_gas", 0.90, 0.08, "", "");
        Assert.Equal(0.98, t.EfficiencyAt(35), 9);
        Assert.Equal(0.90, t.EfficiencyAt(70), 9);
        Assert.Equal(0.90 + 0.08 * 15 / 35, t.EfficiencyAt(55), 9);
    }

    [Fact]
    public void Evaluate_ComputesCostAndEmissions()
    {
        var b = TestData.House();
        var loss = HeatLossCalculator.Calculate(b);
        var demand = EnergyDemandCalculator.Calculate(b, loss);
        var gas = new EnergyCarrier("natural_gas", "Газ", "м³", 9.3, 7.96, 0.202, "");
        var tech = new HeatingTechnology("gas_standard", "Котел", "natural_gas", 0.86, 0, "", "");

        var option = SystemComparison.Evaluate(tech, gas, demand, 55);
        var final = demand.Total / 0.86;
        Assert.Equal(final, option.FinalEnergy, 6);
        Assert.Equal(final / 9.3, option.FuelQuantity, 6);
        Assert.Equal(final / 9.3 * 7.96, option.AnnualCost, 6);
        Assert.Equal(final * 0.202, option.AnnualCo2, 6);
    }

    [Fact]
    public void ClimateZone_FollowsDegreeDays()
    {
        Assert.Equal(1, TestData.Kyiv.ClimateZone); // 3537.6 K·d
        Assert.Equal(2, (TestData.Kyiv with { HeatingSeasonDays = 158, HeatingSeasonMeanTemperature = 1.8 }).ClimateZone);
    }
}
