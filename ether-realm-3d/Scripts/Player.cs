using Godot;
using System;

namespace EtherRealm3D.Scripts;

public partial class Player : CharacterBody3D
{
	// Children
	private Camera3D _camera;
	private RayCast3D _rayCast;
	
	public const float Speed = 15.0f;
	public const float JumpVelocity = 10f;
	
	private const float MouseSensitivity = 0.002f;

	public override void _Ready()
	{
		Input.SetMouseMode(Input.MouseModeEnum.Captured);
		
		_camera = GetNode<Camera3D>("Camera3D");
		_rayCast = GetNode<RayCast3D>("RayCast3D");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseEvent)
		{
			RotateY(-mouseEvent.Relative.X * MouseSensitivity);
			
			float newXRotation = _camera.RotationDegrees.X - mouseEvent.Relative.Y * MouseSensitivity * 100;
			newXRotation = Mathf.Clamp(newXRotation, -90, 90);
			_camera.RotationDegrees = new Vector3(newXRotation, 0, 0);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;
		var speed = Input.IsActionPressed("sprint") ? Speed * 2f : Speed;
		
		// Add the gravity.
		/*if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}
		else if (_rayCast.IsColliding())
		{
			// Auto jump to climb small ledges.
			velocity.Y = JumpVelocity;
		}*/

		// Handle Jump.
		/*if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}*/
		
		if (Input.IsActionPressed("free_cursor"))
		{
			Input.SetMouseMode(Input.MouseModeEnum.Visible);
		}
		else
		{
			Input.SetMouseMode(Input.MouseModeEnum.Captured);
		}
		
		Vector2 inputDir = Input.GetVector("left", "right", "up", "down");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * speed;
			velocity.Z = direction.Z * speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
		}

		if (Input.IsActionPressed("crouch"))
		{
			velocity.Y = -speed;
		}
		else if (Input.IsActionPressed("jump"))
		{
			velocity.Y = speed;
		}
		else
		{
			velocity.Y = Mathf.MoveToward(Velocity.Y, 0, speed);;
		}
		
		Velocity = velocity;
		MoveAndSlide();
	}
}
