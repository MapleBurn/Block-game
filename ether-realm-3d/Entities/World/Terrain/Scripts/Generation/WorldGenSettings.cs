using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// Every tunable value of the world generator. Field values (continentalness, ruggedness,
/// temperature, moisture) are all in -1..1; curves map that range left to right.
/// </summary>
[GlobalClass]
public partial class WorldGenSettings : Resource
{
    [Export] public int Seed;
    [Export] public float SeaLevel;

    [ExportGroup("Continentalness")]
    [Export] public float ContinentalnessFrequency = 0.0008f;
    // How far coastlines get pushed around, in meters. Breaks up blobby continent shapes.
    [Export] public float CoastWarpStrength = 80.0f;
    // Continentalness (deep ocean -> far inland) to base height between the two limits below.
    [Export] public Curve ContinentalnessHeight;
    [Export] public float MinBaseHeight = -40.0f;
    [Export] public float MaxBaseHeight = 40.0f;

    [ExportGroup("Ruggedness")]
    [Export] public float RuggednessFrequency = 0.003f;
    // Ruggedness (smooth -> rugged) to how much of MountainHeight is applied.
    [Export] public Curve RuggednessMountains;
    [Export] public float MountainHeight = 80.0f;
    [Export] public float MountainFrequency = 0.006f;
    // Mountains fade out between these continentalness values so they don't rise from the sea.
    [Export(PropertyHint.Range, "-1,1,0.01")] public float MountainInlandStart;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float MountainInlandFull = 0.25f;
    [Export] public float HillHeight = 6.0f;
    [Export] public float HillFrequency = 0.02f;

    [ExportGroup("Climate")]
    [Export] public float TemperatureFrequency = 0.0008f;
    [Export] public float MoistureFrequency = 0.001f;
    // Temperature lost per meter above sea level, so peaks get cold biomes.
    [Export] public float TemperatureLapseRate = 0.008f;

    [ExportGroup("Biomes")]
    [Export] public Godot.Collections.Array<BiomeDefinition> Biomes = new();
    // Higher = narrower transitions between neighbouring biomes.
    [Export] public float BiomeBlendSharpness = 150.0f;
    // Frequency of the noise behind each biome's Roughness.
    [Export] public float BiomeRoughnessFrequency = 0.05f;

    [ExportGroup("Surface")]
    [Export] public Color RockColor = new(0.45f, 0.43f, 0.4f);
    // Surface normal Y where rock starts showing, and where it fully covers the biome color.
    [Export(PropertyHint.Range, "0,1,0.01")] public float RockSlopeStart = 0.8f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RockSlopeFull = 0.6f;
}
