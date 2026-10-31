using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>Everything the generator decided for one world column.</summary>
public readonly record struct ColumnSample(float Height, Climate Climate, float Temperature, Biome Biome, Color SurfaceColor);

/// <summary>
/// Per-column results for one vertical stack of chunks, indexed x * Size + z.
/// Derived from the seed only, so it is cached in memory and never saved.
/// </summary>
public sealed class ColumnData
{
    public int Size { get; }
    public float[] Heights { get; }
    public Color[] SurfaceColors { get; }
    public float MinHeight { get; }
    public float MaxHeight { get; }

    public ColumnData(int size, float[] heights, Color[] surfaceColors, float minHeight, float maxHeight)
    {
        Size = size;
        Heights = heights;
        SurfaceColors = surfaceColors;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    public int Index(int x, int z) => x * Size + z;
}

/// <summary>Global surface coloring rules applied by the mesher on top of biome colors.</summary>
public sealed record SurfaceColoring(Color RockColor, float RockSlopeStart, float RockSlopeFull);
