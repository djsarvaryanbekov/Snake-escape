// --- REPLACE ENTIRE FILE: Snake.cs ---

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the logical state and behavior of a single snake.
/// This class handles all game rules related to snake movement, growth, and interaction.
/// It has no knowledge of visuals; it only manages data and triggers events.
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
	private Vector2Int preWrapTarget;
	private Vector2Int lastValidIceCubePos;

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

	// --- CORE LOGIC ---
	public void TryMoveTo(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		if (visualizer != null && visualizer.IsAnimating)
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CurrentlyAnimating });
			return;
		}

		if (endToMove == SnakeEnd.Tail && this.snakeColor != ColorType.Red)
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CantMoveTail });
			return;
		}

		if (this.snakeColor == ColorType.Green)
		{
			var grid = GameManager.Instance.grid;
			int gridWidth = grid.GetWidth();
			int gridHeight = grid.GetHeight();
			preWrapTarget = targetPosition;

			if (targetPosition.x < 0) targetPosition.x = gridWidth - 1;
			if (targetPosition.x >= gridWidth) targetPosition.x = 0;
			if (targetPosition.y < 0) targetPosition.y = gridHeight - 1;
			if (targetPosition.y >= gridHeight) targetPosition.y = 0;
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

	private bool IsValidMove(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

		if (snakeColor != ColorType.Green && Mathf.Abs(targetPosition.x - startPosition.x) + Mathf.Abs(targetPosition.y - startPosition.y) != 1)
		{
			return false;
		}

		foreach (var snake in GameManager.Instance.snakesOnLevel)
		{
			foreach (var segment in snake.Body)
			{
				if (targetPosition == segment)
				{
					return false;
				}
			}
		}

		IGridObject targetObject = GameManager.Instance.grid.GetObject(targetPosition);

		// --- BOX PUSH LOGIC ---
		if (targetObject is Box)
		{
			if (endToMove == SnakeEnd.Tail) return false;

			var grid = GameManager.Instance.grid;
			int gridWidth = grid.GetWidth();
			int gridHeight = grid.GetHeight();
			Vector2Int pushDirection;

			if (snakeColor == ColorType.Green)
				pushDirection = GetToroidalUnitDirection(startPosition, preWrapTarget, gridWidth, gridHeight);
			else
				pushDirection = new Vector2Int(Mathf.Clamp(targetPosition.x - startPosition.x, -1, 1), Mathf.Clamp(targetPosition.y - startPosition.y, -1, 1));

			if (pushDirection == Vector2Int.zero) return false;

			Vector2Int boxTarget = targetPosition + pushDirection;

			IGridObject objectBehindBox = grid.GetObject(boxTarget);

			// --- BUG FIX ---: Gates block boxes, even when open.
			if (objectBehindBox is LaserGate)
			{
				return false;
			}

			foreach (var snake in GameManager.Instance.snakesOnLevel)
			{
				foreach (var segment in snake.Body)
				{
					if (boxTarget == segment) return false;
				}
			}

			if (objectBehindBox is EmptyCell || objectBehindBox is PressurePlate) return true;
			if (objectBehindBox is Hole) return true;

			return false;
		}

		// --- ICE CUBE SLIDE LOGIC ---
		if (targetObject is IceCube)
		{
			if (endToMove == SnakeEnd.Tail) return false;

			var grid = GameManager.Instance.grid;
			int gridWidth = grid.GetWidth();
			int gridHeight = grid.GetHeight();
			Vector2Int pushDirection;

			if (snakeColor == ColorType.Green)
				pushDirection = GetToroidalUnitDirection(startPosition, preWrapTarget, gridWidth, gridHeight);
			else
				pushDirection = new Vector2Int(Mathf.Clamp(targetPosition.x - startPosition.x, -1, 1), Mathf.Clamp(targetPosition.y - startPosition.y, -1, 1));

			if (pushDirection == Vector2Int.zero) return false;

			Vector2Int finalPosition = targetPosition;
			while (true)
			{
				Vector2Int nextPosition = finalPosition + pushDirection;
				if (nextPosition.x < 0 || nextPosition.x >= gridWidth || nextPosition.y < 0 || nextPosition.y >= gridHeight) break;

				bool snakeOnCell = false;
				foreach (var snake in GameManager.Instance.snakesOnLevel)
				{
					foreach (var segment in snake.Body)
					{
						if (nextPosition == segment)
						{
							snakeOnCell = true;
							break;
						}
					}
					if (snakeOnCell) break;
				}
				if (snakeOnCell) break;

				IGridObject objectAtNextPos = grid.GetObject(nextPosition);

				// --- BUG FIX ---: Gates block ice cubes, even when open.
				if (objectAtNextPos is LaserGate)
				{
					break; // Stop the slide
				}

				if (objectAtNextPos is EmptyCell || objectAtNextPos is PressurePlate)
				{
					finalPosition = nextPosition;
				}
				else if (objectAtNextPos is Hole)
				{
					finalPosition = nextPosition;
					break;
				}
				else
				{
					break;
				}
			}

			if (finalPosition == targetPosition) return false;
			lastValidIceCubePos = finalPosition;
			return true;
		}

		return targetObject.CanSnakeInteract(this, endToMove);
	}

	private void PerformMove(Vector2Int newPosition, SnakeEnd endToMove)
	{
		IGridObject initialTargetObject = GameManager.Instance.grid.GetObject(newPosition);
		var grid = GameManager.Instance.grid;

		if (initialTargetObject is Box)
		{
			int gridWidth = grid.GetWidth();
			int gridHeight = grid.GetHeight();
			Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();
			Vector2Int pushDirection;
			if (snakeColor == ColorType.Green)
				pushDirection = GetToroidalUnitDirection(startPosition, preWrapTarget, gridWidth, gridHeight);
			else
				pushDirection = new Vector2Int(Mathf.Clamp(newPosition.x - startPosition.x, -1, 1), Mathf.Clamp(newPosition.y - startPosition.y, -1, 1));
			Vector2Int boxTargetPosition = newPosition + pushDirection;

			if (grid.GetObject(boxTargetPosition) is Hole)
				GameManager.Instance.FillHole(boxTargetPosition, newPosition);
			else
				GameManager.Instance.MoveBox(newPosition, boxTargetPosition);

			initialTargetObject = grid.GetObject(newPosition);
		}

		if (initialTargetObject is IceCube)
		{
			if (grid.GetObject(lastValidIceCubePos) is Hole)
				GameManager.Instance.FillHole(lastValidIceCubePos, newPosition);
			else
				GameManager.Instance.MoveIceCube(newPosition, lastValidIceCubePos);

			initialTargetObject = grid.GetObject(newPosition);
		}

		bool willGrow = initialTargetObject is Fruit;
		if (willGrow)
		{
			if (endToMove == SnakeEnd.Head) snakeBody.Insert(0, newPosition);
			else snakeBody.Add(newPosition);
			OnGrew?.Invoke();
		}
		else
		{
			if (endToMove == SnakeEnd.Head)
			{
				snakeBody.RemoveAt(snakeBody.Count - 1);
				snakeBody.Insert(0, newPosition);
			}
			else
			{
				snakeBody.RemoveAt(0);
				snakeBody.Add(newPosition);
			}
			OnMoved?.Invoke();
		}

		initialTargetObject.OnSnakeEntered(this, endToMove);
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

	private Vector2Int GetToroidalUnitDirection(Vector2Int start, Vector2Int target, int gridWidth, int gridHeight)
	{
		int dx = target.x - start.x;
		if (Mathf.Abs(dx) > gridWidth / 2)
			dx = dx > 0 ? dx - gridWidth : dx + gridWidth;

		int dy = target.y - start.y;
		if (Mathf.Abs(dy) > gridHeight / 2)
			dy = dy > 0 ? dy - gridHeight : dy + gridHeight;

		dx = Mathf.Clamp(dx, -1, 1);
		dy = Mathf.Clamp(dy, -1, 1);

		if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) return Vector2Int.zero;

		return new Vector2Int(dx, dy);
	}
}