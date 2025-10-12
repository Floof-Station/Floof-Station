using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Content.Server._Vulp.Weather;
using Content.Server._Vulp.Weather.Functions;
using Content.Shared._Vulp.Weather;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Serilog;


namespace Content.IntegrationTests.Tests._Vulp;

#nullable enable

[TestFixture]
public sealed class WeatherCycleTest
{
    private const float NominalPressure = 101; // In kpa
    private const float MaxDeviation = 0.05f * NominalPressure; // ±5%

    private static List<ProtoId<WeatherCyclePrototype>> IgnoredCycles =
    [
        // Think twice and thrice before adding anything here.
    ];

    private ISawmill Log = default!;

    [Test]
    public async Task EnsureAllWeathersHaveTheSamePressure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        Log = server.Log;

        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var weatherCycles = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WeatherCycleSystem>();
        var errorLog = new List<string>(30);
        foreach (var proto in prototypes.EnumeratePrototypes<WeatherCyclePrototype>())
        {
            if (IgnoredCycles.Contains(proto.ID))
                continue;

            // Needed to set up state IDs for logging
            weatherCycles.ValidatePrototype(proto);

            // Ensure weather don't set atmos in their update loops
            Assert.That(
                !proto.Weathers.Values.Any(it => it.OnUpdate.Any(func => func is WeatherSetAtmos)),
                $"Weathers should not be changing the atmosphere on every tick! {proto}");

            var avgPressure = proto.Weathers.Values
                .Select(it => GetAtmos(proto, it, out var atmos) ? atmos : null)
                .Where(it => it != null)
                .Select(it => it!.Pressure)
                .DefaultIfEmpty(-1)
                .Average();

            Assert.That(
                avgPressure > 0,
                $"No state in {proto.ID} sets the map atmosphere. This can become an issue, consider setting it at least in the default state.");

            // Ensure the average pressure set by weathers is roughly the same
            foreach (var state in proto.Weathers.Values)
                ValidateState(proto, state, avgPressure, errorLog);
        }

        if (errorLog.Count > 0)
        {
            // This has to be done separately so we can log them all at once
            var builder = new StringBuilder(errorLog.Count * 100);
            builder.AppendLine("The following weather cycles have states with pressures that are not equal to the average pressure.");
            foreach (var error in errorLog)
            {
                builder.Append("- ");
                builder.AppendLine(error);
            }
            Log.Error(builder.ToString());
        }

        await pair.CleanReturnAsync();
    }

    private void ValidateState(WeatherCyclePrototype proto, WeatherCycleData data, float calculatedAvgPressure, List<string> errorOutput)
    {
        if (!GetAtmos(proto, data, out var atmos))
            return;

        var pressure = atmos.Pressure;

        // I thought Log.Error would allow tests to continue after an error, but it seems not. Oh well.
        if (MathF.Abs(pressure - calculatedAvgPressure) > MaxDeviation)
        {
            // Suggest a mole count that would yield the average pressure without this node
            var numAtmosStates = proto.Weathers.Count(it => GetAtmos(proto, it.Value, out _));
            if (numAtmosStates <= 1)
                numAtmosStates = 2; // Hack to avoid division by 0. It shouldn't happen in practice since avg==pressures[0] if there's just 1 state.

            var avgPWithoutThis = (calculatedAvgPressure * numAtmosStates - pressure) / (numAtmosStates - 1);
            var k = avgPWithoutThis / pressure;
            var suggestedMoles = new GasMixture(atmos);
            suggestedMoles.Multiply(k);

            errorOutput.Add($"{proto.ID}:{data.StateId} " +
                $"must have the same pressure (±{MaxDeviation}) as the average {calculatedAvgPressure}. " +
                $"Current: {pressure}, nominal: {NominalPressure}. " +
                $"Average pressure without this (faulty) state: {avgPWithoutThis}. " +
                $"Suggested mole count: [{string.Join(", ", suggestedMoles.ToPrettyString().MolesPerGas.Values)}]");
        }
    }

    private static bool GetAtmos(WeatherCyclePrototype proto, WeatherCycleData data, [NotNullWhen(true)] out GasMixture? mixture)
    {
        mixture = null;
        foreach (var func in data.OnTransition)
        {
            if (func is not WeatherSetAtmos atmos)
                continue;

            Assert.That(mixture == null, $"State {data.StateId} of {proto.ID} contains more than 1 WeatherSetAtmos function");
            mixture = atmos.Mixture;
        }

        return mixture != null;
    }
}
