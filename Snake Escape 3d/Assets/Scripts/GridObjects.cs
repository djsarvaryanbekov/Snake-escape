// --- GridObjects.cs ---

using UnityEngine;
using System;
using System.Linq;

public interface IGridObject
{
	bool CanSnakeInteract(Snake snake, SnakeEnd end);
	void OnSnakeEntered(Snake snake, SnakeEnd end);
}

// === PORTAL - WITH ACTIVE/INACTIVE LOGIC ===

public class Portal : IGridObject
{
	private readonly PortalData data;
	private Portal linkedPortal;

	public Portal(PortalData data) { this.data = data; }

	public void SetLinkedPortal(Portal other) => linkedPortal = other;
	public Portal GetLinkedPortal() => linkedPortal;
	public PortalData GetData() => data;

	/// <summary>
	/// Portal is ACTIVE if its linked destination is FREE (no blocking objects).
	/// </summary>
	public bool IsActive()
	{
		if (linkedPortal == null) return false;
		
		var destObjects = GameManager.Instance.grid.GetObjects(linkedPortal. data.position);
		
		// Check for blocking objects at destination
		// Boxes and IceCubes block portals
		foreach (var obj in destObjects)
		{
			if (obj is Box || obj is IceCube) return false;
		}
		
		// Snakes also block portals (except we'll handle "stretching" logic in Snake. cs)
		foreach (var snake in GameManager.Instance.snakesOnLevel)
		{
			if (snake.Body.Contains(linkedPortal.data.position)) return false;
		}

		return true;
	}

	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => IsActive();
	public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}

// === OTHER GRID OBJECTS (simplified, no changes needed) ===

public class EmptyCell : IGridObject
{
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => true;
	public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}

public class Wall : IGridObject
{
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered called on Wall!  This should not happen.");
	}
}

public class Fruit : IGridObject
{
	private readonly FruitData data;
	public Fruit(FruitData data) { this.data = data; }

	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{
		if (end == SnakeEnd.Tail) return false;
		return data.colors.Contains(snake.Color);
	}

	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		GameManager.Instance.ReportFruitEaten(data);
	}
}

public class Exit : IGridObject
{
	private readonly ExitData data;
	public Exit(ExitData data) { this.data = data; }

	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{
		if (end == SnakeEnd.Tail) return false;
		return (snake.Color == data.color && snake.Body.Count >= data.requiredLength);
	}

	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		GameManager.Instance. SnakeHasExited(snake, data);
	}
}

public class Box : IGridObject
{
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered called on Box! The snake should have moved into empty space after push.");
	}
}

public class IceCube : IGridObject
{
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered called on IceCube! The snake should have moved into empty space after push.");
	}
}

public class Hole : IGridObject
{
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered called on Hole! This should not happen.");
	}
}

public class PressurePlate : IGridObject
{
	private readonly PressurePlateData data;
	public bool IsActive { get; private set; } = false;
	public event Action<PressurePlate, bool> OnPlateTriggered;

	public PressurePlate(PressurePlateData data) { this.data = data; }
	public PressurePlateData GetData() => data;

	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => true;
	public void OnSnakeEntered(Snake snake, SnakeEnd end) { }

	public void SetState(bool active)
	{
		if (IsActive == active) return;
		IsActive = active;
		OnPlateTriggered?.Invoke(this, IsActive);
	}
}

// 1. PHYSICAL LIFT GATE (Renamed)
public class LiftGate : IGridObject
{
	private readonly LiftGateData data;
	public bool IsOpen { get; private set; } = false;

	public LiftGate(LiftGateData data) { this.data = data; }
	public LiftGateData GetData() => data;

	// If Closed -> Acts as Wall (False). If Open -> Acts as Floor (True).
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => IsOpen;
	public void OnSnakeEntered(Snake snake, SnakeEnd end) { }

	public void Open() => IsOpen = true;
	public void Close() => IsOpen = false;
}
// --- REPLACE IN GridObjects.cs (LaserGate Class) ---

public class LaserGate : IGridObject
{
	private readonly LaserGateData data;
	public bool IsActive { get; private set; } = true;

	public LaserGate(LaserGateData data) { this.data = data; }
	public LaserGateData GetData() => data;

	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => true; 
	
	public void OnSnakeEntered(Snake snake, SnakeEnd end) 
	{
		// KILL LOGIC
		if (IsActive)
		{
			Debug.Log("Snake hit a laser and died!");
			GameManager.Instance.KillSnake(snake);
		}
	}

	public void Deactivate() => IsActive = false;
	public void Activate() => IsActive = true;
}