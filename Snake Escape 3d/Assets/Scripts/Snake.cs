// --- REPLACE ENTIRE FILE: Snake.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents the logical state and behavior of a single snake.
/// </summary>
public class Snake
{
	// --- EVENTS ---
	public event Action OnMoved;
	public event Action OnGrew;
	public event Action OnRemoved;
	public class OnMoveFailedEventArgs : EventArgs { public MoveFailureReason reason; }
	public event EventHandler<OnMoveFailedEventArgs> OnMoveFailed;
	public enum MoveFailureReason { InteractionFailed, InvalidTarget, CurrentlyAnimating, CantMoveTail }

	// --- PRIVATE STATE ---
	private List<Vector2Int> snakeBody = new List<Vector2Int>();
	private ColorType snakeColor;
	private SnakeVisualizer visualizer;

	// State variables for complex moves
	private Vector2Int _preWrapTarget;
	private Vector2Int _lastValidIceCubePos;

	// --- PUBLIC PROPERTIES ---
	public IReadOnlyList<Vector2Int> Body => snakeBody;
	public ColorType Color => snakeColor;

	// --- INITIALIZATION ---
	public void SetVisualizer(SnakeVisualizer visualizer)
	{
		this.visualizer = visualizer;
	}

	public void Initialize(SnakeData data)
	{
		snakeColor = data.color;
		snakeBody.Clear();
		snakeBody.Add(data.headPosition);
		if (data.headPosition != data.tailPosition)
		{
			snakeBody.Add(data.tailPosition);
		}
	}

	// --- CORE MOVEMENT LOGIC ---

	public void TryMoveTo(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		if (visualizer != null && visualizer.IsAnimating)
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CurrentlyAnimating });
			return;
		}

		// Red Snake Ability: Moving by Tail
		if (endToMove == SnakeEnd.Tail && this.snakeColor != ColorType.Red)
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CantMoveTail });
			return;
		}

		// Green Snake Ability: Wrapping
		if (this.snakeColor == ColorType.Green)
		{
			_preWrapTarget = targetPosition;
			HandleWrappingIndices(ref targetPosition);
		}

		if (IsValidMove(targetPosition, endToMove))
		{
			PerformMove(targetPosition, endToMove);
		}
		else
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
		}
	}

	private void HandleWrappingIndices(ref Vector2Int pos)
	{
		var grid = GameManager.Instance.grid;
		int w = grid.GetWidth();
		int h = grid.GetHeight();

		if (pos.x < 0) pos.x = w - 1;
		else if (pos.x >= w) pos.x = 0;

		if (pos.y < 0) pos.y = h - 1;
		else if (pos.y >= h) pos.y = 0;
	}

	private bool IsValidMove(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

		// 1. Adjacency Check
		if (snakeColor != ColorType.Green)
		{
			if (Mathf.Abs(targetPosition.x - startPosition.x) + Mathf.Abs(targetPosition.y - startPosition.y) != 1) return false;
		}

		// 2. Collision with Snakes
		foreach (var snake in GameManager.Instance.snakesOnLevel)
		{
			foreach (var segment in snake.Body)
			{
				if (targetPosition == segment) return false;
			}
		}

		IGridObject targetObject = GameManager.Instance.grid.GetObject(targetPosition);

		// 3. Portal Logic
		if (targetObject is Portal portal)
		{
			// NEW RULE: Tail cannot enter portals. Behave like Wall.
			if (endToMove == SnakeEnd.Tail) return false;

			Portal linked = portal.GetLinkedPortal();
			if (linked != null)
			{
				Vector2Int portalExitPos = linked.GetData().position;
				// Recursive check at the exit point
				return IsLocationValidForEntry(portalExitPos, endToMove);
			}
		}

		// 4. Box Push Logic
		if (targetObject is Box)
		{
			if (endToMove == SnakeEnd.Tail) return false;
			return CanPushBox(startPosition, targetPosition);
		}

		// 5. Ice Cube Slide Logic
		if (targetObject is IceCube)
		{
			if (endToMove == SnakeEnd.Tail) return false;
			return CanSlideIceCube(startPosition, targetPosition);
		}

		return targetObject.CanSnakeInteract(this, endToMove);
	}

	/// <summary>
	/// Checks if the destination of a portal is valid.
	/// </summary>
	private bool IsLocationValidForEntry(Vector2Int pos, SnakeEnd end)
	{
		foreach (var snake in GameManager.Instance.snakesOnLevel)
			if (snake.Body.Contains(pos)) return false;

		IGridObject obj = GameManager.Instance.grid.GetObject(pos);

		if (obj is Wall) return false;
		if (obj is Box || obj is IceCube) return false;
		if (obj is LaserGate gate && !gate.IsOpen) return false;

		return obj.CanSnakeInteract(this, end);
	}

	private bool CanPushBox(Vector2Int from, Vector2Int toBox)
	{
		var grid = GameManager.Instance.grid;
		Vector2Int pushDir = GetPushDirection(from, toBox);
		if (pushDir == Vector2Int.zero) return false;

		Vector2Int landPos = toBox + pushDir;

		// Box entering Portal
		if (grid.GetObject(landPos) is Portal p && p.GetLinkedPortal() != null)
		{
			landPos = p.GetLinkedPortal().GetData().position;
		}

		// Validation behind box/at portal exit
		foreach (var s in GameManager.Instance.snakesOnLevel)
			if (s.Body.Contains(landPos)) return false;

		IGridObject objBehind = grid.GetObject(landPos);
		if (objBehind is Wall || objBehind is Box || objBehind is IceCube) return false;
		if (objBehind is LaserGate gate && !gate.IsOpen) return false;

		if (objBehind is Hole) return true;

		return objBehind.CanSnakeInteract(this, SnakeEnd.Head);
	}

	private bool CanSlideIceCube(Vector2Int from, Vector2Int toCube)
	{
		var grid = GameManager.Instance.grid;
		Vector2Int pushDir = GetPushDirection(from, toCube);
		if (pushDir == Vector2Int.zero) return false;

		Vector2Int currentPos = toCube;
		Vector2Int nextPos = currentPos + pushDir;

		int safety = 0;
		while (safety < 100)
		{
			safety++;

			// Ice Cube entering Portal
			if (grid.GetObject(nextPos) is Portal p && p.GetLinkedPortal() != null)
			{
				Vector2Int exitPos = p.GetLinkedPortal().GetData().position;

				if (IsBlockedForIceCube(exitPos))
				{
					_lastValidIceCubePos = currentPos;
					// FIX: Only return true if we actually moved from the start
					return _lastValidIceCubePos != toCube;
				}

				currentPos = exitPos;
				nextPos = currentPos + pushDir;
				continue;
			}

			if (IsBlockedForIceCube(nextPos))
			{
				_lastValidIceCubePos = currentPos;
				// FIX: Only return true if we actually moved from the start
				return _lastValidIceCubePos != toCube;
			}

			if (grid.GetObject(nextPos) is Hole)
			{
				_lastValidIceCubePos = nextPos;
				return true;
			}

			currentPos = nextPos;
			nextPos = currentPos + pushDir;
		}

		return false;
	}

	private bool IsBlockedForIceCube(Vector2Int pos)
	{
		var grid = GameManager.Instance.grid;
		if (pos.x < 0 || pos.x >= grid.GetWidth() || pos.y < 0 || pos.y >= grid.GetHeight()) return true;

		foreach (var s in GameManager.Instance.snakesOnLevel)
			if (s.Body.Contains(pos)) return true;

		IGridObject obj = grid.GetObject(pos);
		if (obj is Wall || obj is Box || obj is IceCube) return true;
		if (obj is LaserGate gate && !gate.IsOpen) return true;

		return false;
	}

	private void PerformMove(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		var grid = GameManager.Instance.grid;

		// 1. Portal Teleportation Check for Head
		IGridObject targetObj = grid.GetObject(targetPosition);
		if (targetObj is Portal portal && portal.GetLinkedPortal() != null)
		{
			// We already checked in IsValidMove that endToMove is NOT Tail.
			targetPosition = portal.GetLinkedPortal().GetData().position;
			targetObj = grid.GetObject(targetPosition);
		}

		// 2. Interaction Logic (Box/IceCube)
		if (targetObj is Box)
		{
			Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();
			Vector2Int pushDir = GetPushDirection(startPosition, targetPosition);

			Vector2Int boxTarget = targetPosition + pushDir;
			if (grid.GetObject(boxTarget) is Portal p && p.GetLinkedPortal() != null)
			{
				boxTarget = p.GetLinkedPortal().GetData().position;
			}

			if (grid.GetObject(boxTarget) is Hole) GameManager.Instance.FillHole(boxTarget, targetPosition);
			else GameManager.Instance.MoveBox(targetPosition, boxTarget);

			targetObj = grid.GetObject(targetPosition);
		}
		else if (targetObj is IceCube)
		{
			// Double check: if lastValidPos is the same as current, we shouldn't be here, but good to be safe.
			if (_lastValidIceCubePos != targetPosition)
			{
				if (grid.GetObject(_lastValidIceCubePos) is Hole)
					GameManager.Instance.FillHole(_lastValidIceCubePos, targetPosition);
				else
					GameManager.Instance.MoveIceCube(targetPosition, _lastValidIceCubePos);

				targetObj = grid.GetObject(targetPosition);
			}
		}

		// 3. Update Body Position
		bool willGrow = targetObj is Fruit;

		if (endToMove == SnakeEnd.Head)
		{
			snakeBody.Insert(0, targetPosition);
			if (!willGrow) snakeBody.RemoveAt(snakeBody.Count - 1);
		}
		else // Tail Move (Red Snake)
		{
			snakeBody.Add(targetPosition);
			if (!willGrow) snakeBody.RemoveAt(0);
		}

		if (willGrow) OnGrew?.Invoke();
		else OnMoved?.Invoke();

		targetObj.OnSnakeEntered(this, endToMove);
		GameManager.Instance.ReportSnakeMoved();
	}

	// --- UTILITY METHODS ---

	public void RemoveFromGame()
	{
		snakeBody.Clear();
		OnRemoved?.Invoke();
	}

	public Vector2Int GetHeadPosition() => snakeBody.Count > 0 ? snakeBody[0] : Vector2Int.zero;
	public Vector2Int GetTailPosition() => snakeBody.Count > 0 ? snakeBody[snakeBody.Count - 1] : Vector2Int.zero;

	private Vector2Int GetPushDirection(Vector2Int from, Vector2Int to)
	{
		if (snakeColor == ColorType.Green)
		{
			int w = GameManager.Instance.grid.GetWidth();
			int h = GameManager.Instance.grid.GetHeight();
			return GetToroidalUnitDirection(from, _preWrapTarget, w, h);
		}
		return new Vector2Int(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
	}

	private Vector2Int GetToroidalUnitDirection(Vector2Int start, Vector2Int target, int gridWidth, int gridHeight)
	{
		int dx = target.x - start.x;
		if (Mathf.Abs(dx) > gridWidth / 2) dx = dx > 0 ? dx - gridWidth : dx + gridWidth;

		int dy = target.y - start.y;
		if (Mathf.Abs(dy) > gridHeight / 2) dy = dy > 0 ? dy - gridHeight : dy + gridHeight;

		return new Vector2Int(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1));
	}
}