using Godot;

namespace EtherRealm3D.Scripts.UI;

public partial class WorldSelection : Control
{
    public void OnFilePressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/world.tscn");
    }
}