using Godot;
using System;
using Godot.Collections;
using SCG = System.Collections.Generic;

namespace EtherRealm3D.Scripts;

public partial class ChunkManager : Node
{
    [Export] private int _chunkSize = 16;
    [Export] private int _noiseSeed;
    [Export] private Array<Color> _colors = new();
    [Export] private Vector3I _dimensions = new Vector3I(128, 64, 128);
    private readonly SCG.Dictionary<Vector3, Color> _data = new();
    
    private FastNoiseLite _noise = new();
    private Vector3 _chunkAmount;
    
    private PackedScene _chunkScene;
    
    public override void _Ready()
    {
        _chunkScene = GD.Load<PackedScene>("res://Scenes/chunk.tscn");
        _chunkAmount = _dimensions / _chunkSize;    // pro konečný svět spočítáme počet chunků
    }

    public void GenerateChunks()
    {
        for (int x = 0; x < _chunkAmount.X; x++)
        {
            for (int z = 0; z < _chunkAmount.Z; z++)
            {
                for (int y = 0; y < _chunkAmount.Y; y++)
                {
                    var chunk = _chunkScene.Instantiate<Chunk>();
                    chunk.Position = new Vector3(x, y, z) * _chunkSize;
                    AddChild(chunk);
                    chunk.GenerateData(_chunkSize, _dimensions.Y, _noise, _colors);
                    chunk.GenerateMesh();
                }
            }
        }
    }
}