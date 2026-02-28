using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly SCG.Queue<Node> _addTreeNodes = new();

    private FastNoiseLite _noise = new();
    private Vector3 _chunkAmount;
    
    private readonly SCG.List<Task> _threads = new(4);
    
    private PackedScene _chunkScene;
    
    public override void _Ready()
    {
        _chunkScene = GD.Load<PackedScene>("res://Scenes/chunk.tscn");
        _chunkAmount = _dimensions / _chunkSize;    // pro konečný svět spočítáme počet chunků
        
        _threads.Add(Task.Run(() => GenerateChunks(new Vector3(0,0,0))));
        _threads.Add(Task.Run(() => GenerateChunks(new Vector3(_dimensions.X / 2,0,0))));
        _threads.Add(Task.Run(() => GenerateChunks(new Vector3(0, 0, _dimensions.Z / 2))));
        _threads.Add(Task.Run(() => GenerateChunks(new Vector3(_dimensions.X / 2, 0, _dimensions.Z / 2))));
    }

    public Node GetNextReadyChunk()
    {
        if (_addTreeNodes.Count > 0)
        {
            return _addTreeNodes.Dequeue();
        }
        return null;
    }
    
    private void GenerateChunks(Vector3 pos)
    {
        var chunks = _chunkAmount / 2;
        for (int x = 0; x < chunks.X; x++)
        {
            for (int z = 0; z < chunks.Z; z++)
            {
                for (int y = 0; y < _chunkAmount.Y; y++)
                {
                    var chunk = _chunkScene.Instantiate<Chunk>();
                    chunk.Position = new Vector3(x, y, z) * _chunkSize + pos;
                    chunk.GenerateData(_chunkSize, _dimensions.Y, _noise, _colors);
                    chunk.GenerateMesh();
                    _addTreeNodes.Enqueue(chunk);
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        var ch = GetNextReadyChunk();
        if (ch != null) AddChild(ch);
    }

    public override void _ExitTree()
    {
        
    }
}