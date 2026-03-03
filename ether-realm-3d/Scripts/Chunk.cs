using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GC = Godot.Collections;

namespace EtherRealm3D.Scripts;

public partial class Chunk : StaticBody3D
{
	[Export] private Material _material;
	[Export] private CollisionShape3D _collider;
	[Export] private MeshInstance3D _meshInstance;
	
	private readonly List<Vector3> _vertices = new();  
	private readonly List<Vector3> _normals = new();  
	private readonly List<Color> _colors = new();
	
	private readonly Dictionary<Vector3I, Color> _blocks = new();
	
	private readonly object _lock = new object();
	
	public bool IsDirty { get; set; } = false;

	#region Cube data
	// pozice vertexů krychle o velikosti 1x1x1, centrované v počátku souřadnic
	private Vector3[] _cubeVertices = new Vector3[]
	{
		new Vector3(-0.5f, -0.5f,  0.5f), // 0
		new Vector3( 0.5f, -0.5f,  0.5f), // 1
		new Vector3( 0.5f, -0.5f, -0.5f), // 2
		new Vector3(-0.5f, -0.5f, -0.5f), // 3
		new Vector3(-0.5f,  0.5f,  0.5f), // 4
		new Vector3( 0.5f,  0.5f,  0.5f), // 5
		new Vector3( 0.5f,  0.5f, -0.5f), // 6
		new Vector3(-0.5f,  0.5f, -0.5f)  // 7
	};
	
	private enum Faces
	{
		Front,
		Back,
		Left,
		Right,
		Top,
		Bottom
	}
	
	// Dictionary obsahující pozice trojúhelníků pro každou stranu krychle, souřadnice jsou indexy vertexů v counter-clockwise pořadí
	private Dictionary<Faces, Vector3[]> _faceTriangles = new()
	{
		{ Faces.Front, new[] { new Vector3(0, 4, 5), new Vector3(0, 5, 1) } },
		{ Faces.Back, new[] { new Vector3(2, 6, 7), new Vector3(2, 7, 3) } },
		{ Faces.Left, new[] { new Vector3(3, 7, 4), new Vector3(3, 4, 0) } },
		{ Faces.Right, new[] { new Vector3(1, 5, 6), new Vector3(1, 6, 2) } },
		{ Faces.Top, new[] { new Vector3(4, 7, 6), new Vector3(4, 6, 5) } },
		{ Faces.Bottom, new[] { new Vector3(3, 0, 1), new Vector3(3, 1, 2) } }
	};
	
	// Dictionary obsahující normálové vektory pro každou stranu krychle
	private Dictionary<Faces, Vector3> _faceNormals = new()
	{
		{ Faces.Front, new Vector3(0, 0, 1) },
		{ Faces.Back, new Vector3(0, 0, -1) },
		{ Faces.Left, new Vector3(-1, 0, 0) },
		{ Faces.Right, new Vector3(1, 0, 0) },
		{ Faces.Top, new Vector3(0, 1, 0) },
		{ Faces.Bottom, new Vector3(0, -1, 0) }
	};
	
	private Dictionary<Faces, Color> _faceColors = new()
	{
		{ Faces.Front, Colors.Orange },
		{ Faces.Back, Colors.Purple },
		{ Faces.Left, Colors.Blue },
		{ Faces.Right, Colors.Yellow },
		{ Faces.Top, Colors.Green },
		{ Faces.Bottom, Colors.Red }
	};
	#endregion
	public override void _Ready()
	{
		
	}
	
	public override void _Process(double delta)
	{
	}
	
	public void GenerateMesh()
	{
		if (_blocks.Count == 0)
			return;
		
		foreach (var d in _blocks)
		{
			if (!HasNeighbor(Faces.Front, _blocks, d.Key))
				AddFace(Faces.Front, d.Key, d.Value);
			if (!HasNeighbor(Faces.Back, _blocks, d.Key))
				AddFace(Faces.Back, d.Key, d.Value);
			if (!HasNeighbor(Faces.Left, _blocks, d.Key))
				AddFace(Faces.Left, d.Key, d.Value);
			if (!HasNeighbor(Faces.Right, _blocks, d.Key))
				AddFace(Faces.Right, d.Key, d.Value);
			if (!HasNeighbor(Faces.Top, _blocks, d.Key))
				AddFace(Faces.Top, d.Key, d.Value);
			if (!HasNeighbor(Faces.Bottom, _blocks, d.Key))
				AddFace(Faces.Bottom, d.Key, d.Value);
		}
		
		CommitMesh();
	}

	private void CommitMesh()
	{
		GC.Array nestedArray = [];
		nestedArray.Resize((int)Mesh.ArrayType.Max);
		nestedArray[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();  
		nestedArray[(int)Mesh.ArrayType.Normal] = _normals.ToArray();  
		nestedArray[(int)Mesh.ArrayType.Color] = _colors.ToArray();
		
		var arrayMesh = new ArrayMesh();
		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, nestedArray);
		_meshInstance.Mesh = arrayMesh;
		_meshInstance.Mesh.SurfaceSetMaterial(0, _material);
		_collider.Shape = _meshInstance.Mesh.CreateTrimeshShape();
	}
	
	private void AddFace(Faces face, Vector3 position, Color color)
	{
		var triangles = _faceTriangles[face];
		foreach (var triangle in triangles)
		{
			for (int i = 0; i < 3; i++)
			{
				_vertices.Add(_cubeVertices[(int)triangle[i]] + position);  
				_normals.Add(_faceNormals[face]);  
				_colors.Add(color);
			}
		}
	}
	
	private bool HasNeighbor(Faces face, Dictionary<Vector3I, Color> data, Vector3 position)
	{
		var neighborPos = position + _faceNormals[face];
		return data.ContainsKey((Vector3I)neighborPos);
	}
	
	private float Fbm(Noise noise, float x, float z, int octaves = 6)  
	{  
		float value = 0f;  
		float amplitude = 1f;  
		float frequency = 1f;  
		float maxValue = 0f;  
  
		for (int i = 0; i < octaves; i++)  
		{  
			value += noise.GetNoise2D(x * frequency, z * frequency) * amplitude;  
			maxValue += amplitude;  
			amplitude *= 0.5f;  
			frequency *= 2f;  
		}  
  
		return value / maxValue; // normalizace na [-1, 1]  
	}
	
	private float GetMountainNoise(Noise noise, float x, float z)  
	{  
		// Ridged Multifractal - vytvoří ostré hřebeny  
		float n = noise.GetNoise2D(x * 0.01f, z * 0.01f);  
		float ridged = 1.0f - Mathf.Abs(n);   
		return Mathf.Pow(ridged, 3f); // Umocnění zvýrazní špičky a zploští úpatí  
	}
	
	private Color GetBlockColor(int y, int surfaceY, GC.Array<Color> colors)  
	{  
		int depth = surfaceY - y;  
  
		if (depth == 0) return colors[0]; // grass  
		if (depth <= 3) return colors[1]; // dirt  
		return colors[2];                 // stone  
	}
	
	public void GenerateData(int size, int maxHeight, Noise noise, GC.Array<Color> colors)
	{
		for (int x = 0; x < size; x++)
		{
			for (int z = 0; z < size; z++)
			{
				float gx = x + Position.X;
				float gz = z + Position.Z;

				// 1. Základní terén (mírné vlnění)
				float baseNoise = (noise.GetNoise2D(gx * 0.05f, gz * 0.05f) + 1f) * 0.5f;
				float baseHeight = baseNoise * (maxHeight * 0.3f); // Nížiny jsou max do 30% výšky

				// 2. Horská maska (určuje, KDE budou hory)
				// Použijeme velmi nízkou frekvenci pro velké biomy
				float mountainMask = (noise.GetNoise2D(gx * 0.02f, gz * 0.02f) + 1f) * 0.5f;
				mountainMask = Mathf.SmoothStep(0.4f, 0.7f, mountainMask); // Hory jen tam, kde je šum > 0.4

				// 3. Samotné hory
				float mountainNoise = GetMountainNoise(noise, gx, gz);
				float mountainHeight = mountainNoise * (maxHeight * 0.8f); // Hory mohou jít až do 80% výšky

				// 4. Finální kombinace
				// Výška je základní terén + hory vynásobené maskou
				int surfaceY = (int)(baseHeight + (mountainHeight * mountainMask));

				// Omezení, aby to nepřeteklo maxHeight
				surfaceY = Mathf.Clamp(surfaceY, 1, maxHeight - 1);

				if (surfaceY < Position.Y) continue;

				int localSurface = surfaceY - (int)Position.Y;
				int fillTo = Math.Min(localSurface + 1, size);

				for (int y = 0; y < fillTo; y++)
				{
					int worldY = y + (int)Position.Y;
					_blocks[new Vector3I(x, y, z)] = GetBlockColor(worldY, surfaceY, colors);
				}
			}
		}
	}
	
	public byte[] GetSaveData()  
	{  
		if (_blocks.Count == 0) return Array.Empty<byte>();  
  
		using var stream = new System.IO.MemoryStream();  
		using var writer = new System.IO.BinaryWriter(stream);  
  
		writer.Write(_blocks.Count);  
		foreach (var kvp in _blocks)  
		{  
			// Pozice  
			writer.Write(kvp.Key.X);  
			writer.Write(kvp.Key.Y);  
			writer.Write(kvp.Key.Z);  
			// Barva  
			writer.Write(kvp.Value.R);  
			writer.Write(kvp.Value.G);  
			writer.Write(kvp.Value.B);  
			writer.Write(kvp.Value.A);  
		}  
		return stream.ToArray();  
	}  
  
	public void LoadSaveData(byte[] data)  
	{  
		if (data == null || data.Length == 0) return;  
  
		_blocks.Clear();  
		using var stream = new System.IO.MemoryStream(data);  
		using var reader = new System.IO.BinaryReader(stream);  
  
		try   
		{  
			int count = reader.ReadInt32();  
			for (int i = 0; i < count; i++)  
			{  
				Vector3I pos = new Vector3I(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());  
				Color col = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());  
				_blocks[pos] = col;  
			}  
		}  
		catch (Exception e)  
		{  
			GD.PrintErr($"Chyba při čtení dat chunku: {e.Message}");  
		}  
	}
}
