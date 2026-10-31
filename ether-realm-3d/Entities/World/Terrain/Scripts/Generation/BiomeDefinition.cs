using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// One biome. Each column picks the biome whose climate point is nearest to the column's climate,
/// and blends shape modifiers and color with nearby biomes near borders.
/// </summary>
[GlobalClass]
public partial class BiomeDefinition : Resource
{
    [Export] public string BiomeName = "Biome";

    [ExportGroup("Climate Point")]
    // Temperature after cooling with altitude.
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Temperature;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Moisture;
    [Export(PropertyHint.Range, "-1,1,0.01")] public float Continentalness;

    [ExportGroup("Surface")]
    [Export] public Color SurfaceColor = new(0.3f, 0.55f, 0.25f);

    [ExportGroup("Shape")]
    // Meters added to the base height.
    [Export] public float HeightOffset;
    // Meters of extra small-scale noise.
    [Export] public float Roughness;
    // 0 keeps the rolling hills, 1 removes them.
    [Export(PropertyHint.Range, "0,1,0.01")] public float Flatten;
    // Height of one terrace step in meters; 0 disables terraces.
    [Export] public float TerraceHeight;
    [Export(PropertyHint.Range, "0,1,0.01")] public float TerraceStrength;
}
