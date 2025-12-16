// --- Grid.cs ---

using System;
using UnityEngine;

/// <summary>
/// A generic, logical representation of the game board.
/// This class knows nothing about visuals or game rules. Its only job is to store
/// IGridObject data in a 2D array and provide helper methods to convert between
/// grid coordinates (like [x, z]) and world space positions (a Vector3).
/// </summary>
public class Grid
{
	// --- Grid Properties ---
	private int width;  // The number of cells horizontally.
	private int height; // The number of cells vertically (along the Z axis in 3D).
	private float cellSize; // The size of each square cell in world units.
	private Vector3 originPosition; // The world space coordinate of the grid's bottom-left corner [0, 0].

	// --- The Core Data Structure ---
	// A 2D array that stores an IGridObject for every cell on the board.
	// This holds the logical state of the entire level.
	private IGridObject[,] gridArray;

	/// <summary>
	/// Constructor to create and initialize a new grid.
	/// </summary>
	/// <param name="width">Grid width in cells.</param>
	/// <param name="height">Grid height in cells.</param>
	/// <param name="cellSize">Size of each cell in world units.</param>
	/// <param name="originPosition">World position of the grid's corner [0,0].</param>
	public Grid(int width, int height, float cellSize, Vector3 originPosition)
	{
		this.width = width;
		this.height = height;
		this.cellSize = cellSize;
		this.originPosition = originPosition;

		// Create the 2D array with the specified dimensions.
		gridArray = new IGridObject[width, height];

		// IMPORTANT: Initialize the entire grid with EmptyCell objects by default.
		// This ensures that no cell is ever null, preventing errors. Every spot on the
		// board is either empty or explicitly replaced with another object (Wall, Fruit, etc.).
		for (int x = 0; x < width; x++)
		{
			for (int z = 0; z < height; z++)
			{
				gridArray[x, z] = new EmptyCell();
			}
		}

		// Draw visual lines in the Scene view to help with debugging and level design.
		DrawDebugLines();
	}


	// --- Grid Data Manipulation ---
	/// <summary>
	/// Places or replaces an IGridObject at a specific cell coordinate.
	/// </summary>
	/// <param name="x">The horizontal grid coordinate.</param>
	/// <param name="z">The vertical grid coordinate.</param>
	/// <param name="gridObject">The logical object to place (e.g., new Wall(), new Fruit()).</param>
	public void SetObject(int x, int z, IGridObject gridObject)
	{
		// Safety check to ensure we don't try to write to an index outside the array's bounds.
		if (x >= 0 && z >= 0 && x < width && z < height)
		{
			gridArray[x, z] = gridObject;
		}
	}

	/// <summary>
	/// Retrieves the IGridObject from a specific cell coordinate.
	/// </summary>
	/// <param name="x">The horizontal grid coordinate.</param>
	/// <param name="z">The vertical grid coordinate.</param>
	/// <returns>The IGridObject at that location.</returns>
	public IGridObject GetObject(int x, int z)
	{
		// Check if the requested coordinates are within the grid's bounds.
		if (x >= 0 && z >= 0 && x < width && z < height)
		{
			return gridArray[x, z];
		}
		else
		{
			// If the coordinates are outside the grid, treat it as a Wall.
			// This is a robust way to prevent snakes from moving off the board.
			return new Wall();
		}
	}

	/// <summary>
	/// An overload for GetObject that accepts a Vector2Int instead of separate x and z values.
	/// </summary>
	public IGridObject GetObject(Vector2Int position)
	{
		return GetObject(position.x, position.y);
	}

	// --- UTILITY METHODS ---

	// Simple "getter" methods to provide read-only access to private grid properties.
	public int GetWidth() => width;
	public int GetHeight() => height;
	public float GetCellSize() => cellSize;

	/// <summary>
	/// Converts grid coordinates (x, z) to a world space position (Vector3).
	/// This gives the position of the bottom-left corner of the cell.
	/// </summary>
	public Vector3 GetWorldPosition(int x, int z)
	{
		return new Vector3(x, 0, z) * cellSize + originPosition;
	}

	/// <summary>
	/// Converts grid coordinates (x, z) to the world space position of the CENTER of the cell.
	/// This is most often used for placing visual objects.
	/// </summary>
	public Vector3 GetWorldPositionOfCellCenter(int x, int z)
	{
		return GetWorldPosition(x, z) + new Vector3(cellSize, 0, cellSize) * 0.5f;
	}

	/// <summary>
	/// Converts a world space position (Vector3) back into grid coordinates (x, z).
	/// </summary>
	public void GetXZ(Vector3 worldPosition, out int x, out int z)
	{
		x = Mathf.FloorToInt((worldPosition - originPosition).x / cellSize);
		z = Mathf.FloorToInt((worldPosition - originPosition).z / cellSize);
	}

	/// <summary>
	/// Draws a wireframe of the grid in the Unity Scene view for debugging.
	/// </summary>
	private void DrawDebugLines()
	{
		// Loop through every cell.
		for (int x = 0; x < gridArray.GetLength(0); x++)
		{
			for (int z = 0; z < gridArray.GetLength(1); z++)
			{
				// Draw the right and top lines for each cell.
				Debug.DrawLine(GetWorldPosition(x, z), GetWorldPosition(x + 1, z), Color.white, 100f);
				Debug.DrawLine(GetWorldPosition(x, z), GetWorldPosition(x, z + 1), Color.white, 100f);
			}
		}
		// Draw the final right and top boundaries of the entire grid.
		Debug.DrawLine(GetWorldPosition(0, height), GetWorldPosition(width, height), Color.white, 100f);
		Debug.DrawLine(GetWorldPosition(width, 0), GetWorldPosition(width, height), Color.white, 100f);
	}
}