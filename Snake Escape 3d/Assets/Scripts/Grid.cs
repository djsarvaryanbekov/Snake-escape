// --- REPLACE ENTIRE FILE: Grid.cs ---

using System;
using System.Collections.Generic;
using System.Linq; // Needed to search lists easily
using UnityEngine;

/// <summary>
/// A generic, logical representation of the game board.
/// UPDATED: Now supports STACKING objects. Each cell holds a List of objects, not just one.
/// </summary>
public class Grid
{
	// --- Grid Properties ---
	private int width;
	private int height;
	private float cellSize;
	private Vector3 originPosition;

	// --- The Core Data Structure ---
	// A 2D Array where every element is a LIST of objects.
	// gridArray[x,y] = List<IGridObject> containing { Floor, Gate, Snake, etc. }
	private List<IGridObject>[,] gridArray;

	public Grid(int width, int height, float cellSize, Vector3 originPosition)
	{
		this.width = width;
		this.height = height;
		this.cellSize = cellSize;
		this.originPosition = originPosition;

		// Initialize the 2D array
		gridArray = new List<IGridObject>[width, height];

		// IMPORTANT: Initialize the LIST in every single cell
		for (int x = 0; x < width; x++)
		{
			for (int z = 0; z < height; z++)
			{
				gridArray[x, z] = new List<IGridObject>();
				// By default, we can add an EmptyCell, or just leave it as an empty list.
				// For compatibility with your existing logic which expects *something* to be there,
				// let's add an EmptyCell.
				gridArray[x, z].Add(new EmptyCell());
			}
		}

		DrawDebugLines();
	}

	// --- Grid Data Manipulation ---

	/// <summary>
	/// Adds an object to the stack at the specific coordinate.
	/// Does NOT remove existing objects.
	/// </summary>
	public void AddObject(int x, int z, IGridObject gridObject)
	{
		if (IsValid(x, z))
		{
			// Optional: Remove "EmptyCell" if we are adding a solid object, 
			// to keep the list clean, but strictly not necessary if logic handles it.
			// For now, simple add.
			gridArray[x, z].Add(gridObject);
		}
	}

	/// <summary>
	/// Removes a specific object instance from the grid.
	/// </summary>
	public void RemoveObject(int x, int z, IGridObject gridObject)
	{
		if (IsValid(x, z))
		{
			if (gridArray[x, z].Contains(gridObject))
			{
				gridArray[x, z].Remove(gridObject);
			}
		}
	}

	/// <summary>
	/// Returns ALL objects at this location.
	/// </summary>
	public List<IGridObject> GetObjects(int x, int z)
	{
		if (IsValid(x, z))
		{
			return gridArray[x, z];
		}
		else
		{
			// Return a list with a Wall if out of bounds
			return new List<IGridObject> { new Wall() };
		}
	}

	public List<IGridObject> GetObjects(Vector2Int pos) => GetObjects(pos.x, pos.y);

	// --- Helpers to find specific things in the stack ---

	public T GetObjectOfType<T>(Vector2Int pos) where T : class, IGridObject
	{
		var list = GetObjects(pos);
		// Return the first object of type T found in the stack
		return list.OfType<T>().FirstOrDefault();
	}
	
	public bool HasObjectOfType<T>(Vector2Int pos) where T : class, IGridObject
	{
		var list = GetObjects(pos);
		return list.OfType<T>().Any();
	}

	// --- UTILITY METHODS ---

	public int GetWidth() => width;
	public int GetHeight() => height;
	public float GetCellSize() => cellSize;

	private bool IsValid(int x, int z)
	{
		return x >= 0 && z >= 0 && x < width && z < height;
	}

	public Vector3 GetWorldPosition(int x, int z)
	{
		return new Vector3(x, 0, z) * cellSize + originPosition;
	}

	public Vector3 GetWorldPositionOfCellCenter(int x, int z)
	{
		return GetWorldPosition(x, z) + new Vector3(cellSize, 0, cellSize) * 0.5f;
	}

	public void GetXZ(Vector3 worldPosition, out int x, out int z)
	{
		x = Mathf.FloorToInt((worldPosition - originPosition).x / cellSize);
		z = Mathf.FloorToInt((worldPosition - originPosition).z / cellSize);
	}

	private void DrawDebugLines()
	{
		for (int x = 0; x < gridArray.GetLength(0); x++)
		{
			for (int z = 0; z < gridArray.GetLength(1); z++)
			{
				Debug.DrawLine(GetWorldPosition(x, z), GetWorldPosition(x + 1, z), Color.white, 100f);
				Debug.DrawLine(GetWorldPosition(x, z), GetWorldPosition(x, z + 1), Color.white, 100f);
			}
		}
		Debug.DrawLine(GetWorldPosition(0, height), GetWorldPosition(width, height), Color.white, 100f);
		Debug.DrawLine(GetWorldPosition(width, 0), GetWorldPosition(width, height), Color.white, 100f);
	}
}