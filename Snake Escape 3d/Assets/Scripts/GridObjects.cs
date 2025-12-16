// --- GridObjects.cs ---

using UnityEngine;

/// <summary>
/// This is the core "contract" for any object that can exist on the game grid.
/// An interface is a promise. Any class that implements IGridObject PROMISES
/// to provide its own versions of the methods defined here. This allows the Snake class
/// to interact with any object without needing to know its specific type (Fruit, Wall, etc.).
/// It just knows it can call these two methods on whatever is on a given tile.
/// </summary>
public interface IGridObject
{
	/// <summary>
	/// The "Rule Check" method. Called by a snake BEFORE it moves.
	/// The object on the target tile gets to decide if the snake is allowed to move there.
	/// </summary>
	/// <param name="snake">The snake that is attempting to interact.</param>
	/// <param name="end">Which end of the snake (Head or Tail) is moving.</param>
	/// <returns>True if the snake can enter this tile, false otherwise.</returns>
	bool CanSnakeInteract(Snake snake, SnakeEnd end);

	/// <summary>
	/// The "Action" method. Called AFTER a snake has successfully moved onto this tile.
	/// This is where the object's effect happens (e.g., getting eaten, triggering a win).
	/// </summary>
	/// <param name="snake">The snake that has entered the tile.</param>
	/// <param name="end">The end of the snake that entered the tile.</param>
	void OnSnakeEntered(Snake snake, SnakeEnd end);
}

// --------------------------------------------------------------------
// --- Concrete Implementations of the IGridObject contract ---------
// --------------------------------------------------------------------

/// <summary>
/// Represents an empty, passable tile on the grid.
/// </summary>
public class EmptyCell : IGridObject
{
	// An empty cell always allows a snake to enter.
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) { return true; }

	// When a snake enters, nothing special happens.
	public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}

/// <summary>
/// Represents an impassable wall.
/// </summary>
public class Wall : IGridObject
{
	// A wall NEVER allows a snake to enter.
	public bool CanSnakeInteract(Snake snake, SnakeEnd end) { return false; }

	// This method should logically never be called. If it is, it indicates a bug in the movement code.
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered was called on a Wall object! This should not happen.");
	}
}

/// <summary>
/// Represents a fruit that a snake can eat to grow.
/// </summary>
public class Fruit : IGridObject
{
	// Fruits store their data (position, required colors) in a data-only class.
	private readonly FruitData data;
	public Fruit(FruitData data) { this.data = data; }

	// The rule for fruits:
	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{
		// 1. Only the head can eat fruit. The tail cannot.
		if (end == SnakeEnd.Tail) return false;

		// 2. The snake's color must be one of the colors the fruit accepts.
		return data.colors.Contains(snake.Color);
	}

	// The action for fruits:
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		// When a snake successfully enters the fruit's tile, the fruit reports back
		// to the GameManager that it has been eaten. The GameManager then handles the
		// logic for removing the fruit from the grid and making the snake grow.
		GameManager.Instance.ReportFruitEaten(data);
	}
}

/// <summary>
/// Represents an exit portal for a snake to complete the level.
/// </summary>
public class Exit : IGridObject
{
	// Exits store their data (position, color, required length).
	private readonly ExitData data;
	public Exit(ExitData data) { this.data = data; }

	// The rule for exits:
	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{
		// 1. Only the head can enter an exit.
		if (end == SnakeEnd.Tail) return false;

		// 2. The snake's color must match the exit's color AND
		//    the snake's body length must meet the minimum requirement.
		return (snake.Color == data.color && snake.Body.Count >= data.requiredLength);
	}

	// The action for exits:
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		// When a snake enters, the exit tells the GameManager that this snake has finished.
		// The GameManager handles removing the snake, spawning a new fruit, etc.
		GameManager.Instance.SnakeHasExited(snake, data);
	}
}

/// <summary>
/// Represents a pushable Box. Its logic is unique.
/// </summary>
public class Box : IGridObject
{
	// The rule for boxes is very strict:
	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{
		// A snake can NEVER directly enter a box's cell.
		// This always returns false.
		// This forces the special case in `Snake.cs -> IsValidMove` to handle the "push" logic.
		// The snake checks "if target is box, can I push it?". If yes, the box is moved,
		// leaving an EmptyCell behind, and the snake moves into *that* EmptyCell.
		return false;
	}

	// This method should never be called for the same reason as above.
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered was called on a Box object! The snake should have moved into the empty space created after the push.");
	}
}

public class IceCube : IGridObject
{

	// The rule for boxes is very strict:
	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{
		// A snake can NEVER directly enter a box's cell.
		// This always returns false.
		// This forces the special case in `Snake.cs -> IsValidMove` to handle the "push" logic.
		// The snake checks "if target is box, can I push it?". If yes, the box is moved,
		// leaving an EmptyCell behind, and the snake moves into *that* EmptyCell.
		return false;
	}

	// This method should never be called for the same reason as above.
	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered was called on a IceCube object! The snake should have moved into the empty space created after the push.");
	}



}


public class Hole : IGridObject
{
	public bool CanSnakeInteract(Snake snake, SnakeEnd end)
	{

		return false;
	}

	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		Debug.LogError("OnSnakeEntered was called on a Hole object! This should not happen.");

	}
}

public class PressurePlate : IGridObject
{
	private readonly PressurePlateData data;
	public bool IsActive { get; private set; } = false;

	public PressurePlate(PressurePlateData data) { this.data = data; }

	/// <summary>
	/// Provides public access to the plate's data (e.g., its position).
	/// </summary>
	public PressurePlateData GetData() => data;

	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => true;

	public void OnSnakeEntered(Snake snake, SnakeEnd end)
	{
		// This logic is now handled centrally by GameManager.UpdateAllPlateStates()
		// to correctly account for all objects (snakes, boxes) and leaving the plate.
	}

	public void Activate()
	{
		if (!IsActive)
		{
			IsActive = true;
			GameManager.Instance.ReportPlateStateChange(data, true);
		}
	}

	public void Deactivate()
	{
		if (IsActive)
		{
			IsActive = false;
			GameManager.Instance.ReportPlateStateChange(data, false);
		}
	}
}

public class LaserGate : IGridObject
{
	private readonly LaserGateData data;
	public bool IsOpen { get; private set; } = false;

	public LaserGate(LaserGateData data) { this.data = data; }

	/// <summary>
	/// Provides public access to the gate's data (e.g., its position).
	/// </summary>
	public LaserGateData GetData() => data;

	public bool CanSnakeInteract(Snake snake, SnakeEnd end) => IsOpen;

	public void OnSnakeEntered(Snake snake, SnakeEnd end) { }

	public void Open() => IsOpen = true;
	public void Close() => IsOpen = false;
}