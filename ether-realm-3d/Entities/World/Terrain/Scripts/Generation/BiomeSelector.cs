using System;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// Plain copy of a <see cref="BiomeDefinition"/>, safe to read from background threads.
/// </summary>
public sealed class Biome
{
    public string Name { get; init; }
    public float Temperature { get; init; }
    public float Moisture { get; init; }
    public float Continentalness { get; init; }
    public Color SurfaceColor { get; init; }
    public float HeightOffset { get; init; }
    public float Roughness { get; init; }
    public float Flatten { get; init; }
    public float TerraceHeight { get; init; }
    public float TerraceStrength { get; init; }

    public static Biome From(BiomeDefinition definition) => new()
    {
        Name            = definition.BiomeName,
        Temperature     = definition.Temperature,
        Moisture        = definition.Moisture,
        Continentalness = definition.Continentalness,
        SurfaceColor    = definition.SurfaceColor,
        HeightOffset    = definition.HeightOffset,
        Roughness       = definition.Roughness,
        Flatten         = definition.Flatten,
        TerraceHeight   = definition.TerraceHeight,
        TerraceStrength = definition.TerraceStrength
    };

    /// <summary>Used when the settings define no biomes.</summary>
    public static readonly Biome Fallback = new()
    {
        Name = "Default",
        SurfaceColor = new Color(0.3f, 0.55f, 0.25f)
    };
}

/// <summary>
/// Picks biomes by nearest climate point and weighs nearby biomes for smooth borders.
/// </summary>
public sealed class BiomeSelector
{
    // Weights below this are treated as zero so far-away biomes cost nothing.
    public const float MinWeight = 0.001f;

    private readonly float _blendSharpness;

    public Biome[] Biomes { get; }

    public BiomeSelector(WorldGenSettings settings)
    {
        Biomes = new Biome[Math.Max(settings.Biomes.Count, 1)];
        if (settings.Biomes.Count == 0)
            Biomes[0] = Biome.Fallback;
        for (int i = 0; i < settings.Biomes.Count; i++)
            Biomes[i] = Biome.From(settings.Biomes[i]);

        _blendSharpness = settings.BiomeBlendSharpness;
    }

    /// <summary>
    /// Fills <paramref name="weights"/> (length = Biomes.Length) with blend weights summing to 1
    /// and returns the index of the nearest biome.
    /// </summary>
    public int Weigh(float temperature, float moisture, float continentalness, Span<float> weights)
    {
        int nearest = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < Biomes.Length; i++)
        {
            var biome = Biomes[i];
            float dt = temperature - biome.Temperature;
            float dm = moisture - biome.Moisture;
            float dc = continentalness - biome.Continentalness;
            float distance = dt * dt + dm * dm + dc * dc;

            weights[i] = distance;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        // Measure relative to the nearest biome so the nearest always gets the largest weight.
        float total = 0f;
        for (int i = 0; i < Biomes.Length; i++)
        {
            float w = Mathf.Exp(-(weights[i] - nearestDistance) * _blendSharpness);
            if (w < MinWeight) w = 0f;
            weights[i] = w;
            total += w;
        }

        for (int i = 0; i < Biomes.Length; i++)
            weights[i] /= total;

        return nearest;
    }
}
