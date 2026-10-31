using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>Large-scale field values at one column, each in -1..1.</summary>
public readonly record struct Climate(float Continentalness, float Ruggedness, float Temperature, float Moisture);

/// <summary>
/// Samples the large-scale fields that drive both terrain shape and biome choice.
/// These know nothing about biomes, so landforms stay continuous across biome borders.
/// </summary>
public sealed class ClimateFields
{
    // Fractal noise rarely gets near ±1; this stretches typical output to fill the -1..1 range
    // so curves and biome climate points can use the whole range.
    private const float FieldGain = 1.8f;

    private readonly FastNoiseLite _continentalness;
    private readonly FastNoiseLite _ruggedness;
    private readonly FastNoiseLite _temperature;
    private readonly FastNoiseLite _moisture;
    private readonly FastNoiseLite _coastWarpX;
    private readonly FastNoiseLite _coastWarpZ;
    private readonly float _coastWarpStrength;

    public ClimateFields(WorldGenSettings settings)
    {
        int seed = settings.Seed;
        _continentalness = CreateField(seed + 1, settings.ContinentalnessFrequency, 4);
        _ruggedness      = CreateField(seed + 2, settings.RuggednessFrequency, 3);
        _temperature     = CreateField(seed + 3, settings.TemperatureFrequency, 2);
        _moisture        = CreateField(seed + 4, settings.MoistureFrequency, 3);
        _coastWarpX      = CreateField(seed + 5, settings.ContinentalnessFrequency * 4f, 2);
        _coastWarpZ      = CreateField(seed + 6, settings.ContinentalnessFrequency * 4f, 2);
        _coastWarpStrength = settings.CoastWarpStrength;
    }

    public Climate Sample(float x, float z)
    {
        float warpX = _coastWarpX.GetNoise2D(x, z) * _coastWarpStrength;
        float warpZ = _coastWarpZ.GetNoise2D(x, z) * _coastWarpStrength;

        return new Climate(
            Field(_continentalness, x + warpX, z + warpZ),
            Field(_ruggedness, x, z),
            Field(_temperature, x, z),
            Field(_moisture, x, z));
    }

    private static float Field(FastNoiseLite noise, float x, float z) =>
        Mathf.Clamp(noise.GetNoise2D(x, z) * FieldGain, -1f, 1f);

    private static FastNoiseLite CreateField(int seed, float frequency, int octaves) => new()
    {
        Seed = seed,
        NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
        Frequency = frequency,
        FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
        FractalOctaves = octaves
    };
}
