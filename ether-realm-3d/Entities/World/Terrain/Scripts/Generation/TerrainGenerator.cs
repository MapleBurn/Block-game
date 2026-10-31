using System;
using System.Collections.Concurrent;
using EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Generation;

/// <summary>
/// Deterministic terrain source. Per column it runs:
/// climate fields -> base shape -> biome weights -> biome shape modifiers -> surface color.
/// Holds no scene state, so it is safe to call from background threads.
/// </summary>
public sealed class TerrainGenerator
{
    private readonly ClimateFields _climate;
    private readonly TerrainShaper _shaper;
    private readonly BiomeSelector _biomes;
    private readonly FastNoiseLite _roughness;
    private readonly float _seaLevel;
    private readonly float _lapseRate;
    private readonly int _cellsPerAxis;
    private readonly float _voxelSize;

    // Column results shared by every chunk in the same vertical stack.
    private readonly ConcurrentDictionary<Vector2I, Lazy<ColumnData>> _columnCache = new();

    public SurfaceColoring Surface { get; }

    /// <summary>Main thread only: reads Godot resources once and copies what it needs.</summary>
    public TerrainGenerator(WorldGenSettings settings, int cellsPerAxis, float voxelSize)
    {
        _climate = new ClimateFields(settings);
        _shaper  = new TerrainShaper(settings);
        _biomes  = new BiomeSelector(settings);
        _roughness = new FastNoiseLite
        {
            Seed = settings.Seed + 20,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = settings.BiomeRoughnessFrequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 3
        };

        _seaLevel = settings.SeaLevel;
        _lapseRate = settings.TemperatureLapseRate;
        _cellsPerAxis = cellsPerAxis;
        _voxelSize = voxelSize;
        Surface = new SurfaceColoring(settings.RockColor, settings.RockSlopeStart, settings.RockSlopeFull);
    }

    public ColumnSample SampleColumn(float x, float z)
    {
        Climate climate = _climate.Sample(x, z);
        ShapeSample shape = _shaper.Sample(x, z, climate);

        // Cool the climate with altitude so peaks pick cold biomes.
        float temperature = climate.Temperature - Mathf.Max(0f, shape.Height - _seaLevel) * _lapseRate;

        Biome[] biomes = _biomes.Biomes;
        Span<float> weights = stackalloc float[biomes.Length];
        int nearest = _biomes.Weigh(temperature, climate.Moisture, climate.Continentalness, weights);

        float offset = 0f, roughness = 0f, flatten = 0f;
        var color = new Color(0, 0, 0);
        for (int i = 0; i < biomes.Length; i++)
        {
            float w = weights[i];
            if (w == 0f) continue;
            var biome = biomes[i];
            offset    += biome.HeightOffset * w;
            roughness += biome.Roughness * w;
            flatten   += biome.Flatten * w;
            color     += biome.SurfaceColor * w;
        }

        float height = shape.Base + shape.Hills * (1f - flatten) + offset;
        if (roughness > 0f)
            height += _roughness.GetNoise2D(x, z) * roughness;

        // Terraces go last so their steps stay flat.
        float terraceShift = 0f;
        for (int i = 0; i < biomes.Length; i++)
        {
            var biome = biomes[i];
            if (weights[i] == 0f || biome.TerraceHeight <= 0f || biome.TerraceStrength <= 0f) continue;
            terraceShift += (Terrace(height, biome.TerraceHeight) - height) * biome.TerraceStrength * weights[i];
        }
        height += terraceShift;

        return new ColumnSample(height, climate, temperature, biomes[nearest], new Color(color.R, color.G, color.B));
    }

    /// <summary>Column results for a vertical stack of chunks, computed once and cached.</summary>
    public ColumnData GetColumns(Vector2I chunkColumn) =>
        _columnCache.GetOrAdd(chunkColumn, c => new Lazy<ColumnData>(() => SampleColumns(c))).Value;

    /// <summary>Drops cached columns farther than <paramref name="radius"/> chunks from the center.</summary>
    public void ForgetColumnsOutside(Vector2I center, int radius)
    {
        foreach (var key in _columnCache.Keys)
            if (Math.Abs(key.X - center.X) > radius || Math.Abs(key.Y - center.Y) > radius)
                _columnCache.TryRemove(key, out _);
    }

    public ChunkData GenerateChunk(Vector3I coord, ColumnData columns)
    {
        int samples = _cellsPerAxis + 1;
        float bottom = coord.Y * _cellsPerAxis * _voxelSize;
        float top = bottom + _cellsPerAxis * _voxelSize;

        // Skip chunks the surface never crosses; the mesher would emit nothing for them anyway.
        if (bottom >= columns.MaxHeight) return ChunkData.Uniform(coord, samples, ChunkContent.Air);
        if (top < columns.MinHeight) return ChunkData.Uniform(coord, samples, ChunkContent.Solid);

        float[] heights = columns.Heights;
        var densities = new float[samples * samples * samples];
        int i = 0;
        for (int x = 0; x < samples; x++)
        for (int y = 0; y < samples; y++)
        {
            float worldY = bottom + y * _voxelSize;
            int column = columns.Index(x, 0);
            for (int z = 0; z < samples; z++)
                densities[i++] = worldY - heights[column + z];
        }

        return new ChunkData(coord, samples, densities);
    }

    private ColumnData SampleColumns(Vector2I chunkColumn)
    {
        int samples = _cellsPerAxis + 1;
        float chunkWorldSize = _cellsPerAxis * _voxelSize;
        float originX = chunkColumn.X * chunkWorldSize;
        float originZ = chunkColumn.Y * chunkWorldSize;

        var heights = new float[samples * samples];
        var colors = new Color[samples * samples];
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int x = 0; x < samples; x++)
        for (int z = 0; z < samples; z++)
        {
            var column = SampleColumn(originX + x * _voxelSize, originZ + z * _voxelSize);
            int index = x * samples + z;
            heights[index] = column.Height;
            colors[index] = column.SurfaceColor;
            minHeight = Mathf.Min(minHeight, column.Height);
            maxHeight = Mathf.Max(maxHeight, column.Height);
        }

        return new ColumnData(samples, heights, colors, minHeight, maxHeight);
    }

    /// <summary>Snaps height to flat steps joined by short, steep risers.</summary>
    private static float Terrace(float height, float step)
    {
        float level = height / step;
        float floor = Mathf.Floor(level);
        return (floor + Mathf.SmoothStep(0.7f, 1f, level - floor)) * step;
    }
}
