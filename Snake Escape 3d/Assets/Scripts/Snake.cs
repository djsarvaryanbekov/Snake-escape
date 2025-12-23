// --- REPLACE ENTIRE FILE: Snake.cs ---

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Snake
{
	// Events
	public event Action OnMoved;
	public event Action OnGrew;
	public event Action OnRemoved;
	public class OnMoveFailedEventArgs : EventArgs { public MoveFailureReason reason; }
	public event EventHandler<OnMoveFailedEventArgs> OnMoveFailed;
	public enum MoveFailureReason { InteractionFailed, InvalidTarget, CurrentlyAnimating, CantMoveTail }

	private List<Vector2Int> snakeBody = new List<Vector2Int>();
	private ColorType snakeColor;
	private SnakeVisualizer visualizer;
	private Vector2Int _preWrapTarget;
	private Vector2Int _lastValidIceCubePos;

	public IReadOnlyList<Vector2Int> Body => snakeBody;
	public ColorType Color => snakeColor;

	public void SetVisualizer(SnakeVisualizer visualizer) => this.visualizer = visualizer;

	public void Initialize(SnakeData data)
	{
		snakeColor = data.color;
		snakeBody.Clear();
		snakeBody.Add(data.headPosition);
		if (data.headPosition != data.tailPosition) snakeBody.Add(data. tailPosition);
	}

	public void TryMoveTo(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		// Animation check
		if (visualizer != null && visualizer.IsAnimating)
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CurrentlyAnimating });
			return;
		}

		// Red Snake:  Only Red can move via tail
		if (endToMove == SnakeEnd.Tail && this.snakeColor != ColorType.Red)
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CantMoveTail });
			return;
		}

		// Green Snake: Border Wrapping
		if (this.snakeColor == ColorType. Green)
		{
			_preWrapTarget = targetPosition;
			HandleWrappingIndices(ref targetPosition);
		}

		// ===== CHECK FRUIT & EXIT INTERACTION BEFORE MOVING =====
		var grid = GameManager.Instance.grid;
		var targetObjects = grid.GetObjects(targetPosition);

		foreach (var obj in targetObjects)
		{
			// Check Fruit interaction
			if (obj is Fruit fruit)
			{
				if (! fruit. CanSnakeInteract(this, endToMove))
				{
					Debug.Log($"Snake cannot eat this fruit.  Color mismatch or invalid end (tail).");
					OnMoveFailed?. Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
					return;
				}
			}
			// Check Exit interaction
			else if (obj is Exit exit)
			{
				if (!exit. CanSnakeInteract(this, endToMove))
				{
					Debug.Log($"Snake cannot use this exit. Color mismatch, too short, or invalid end (tail).");
					OnMoveFailed?. Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
					return;
				}
			}
		}

		// Now validate the move is physically possible
		if (IsValidMove(targetPosition, endToMove))
		{
			PerformMove(targetPosition, endToMove);
		}
		else
		{
			OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
		}
	}

	/// <summary>
	/// GREEN SNAKE: Wraps around field edges (periodic boundary conditions).
	/// </summary>
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

	/// <summary>
	/// Main movement validation.  Checks all GDD rules.
	/// </summary>
	private bool IsValidMove(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

		// === ADJACENCY CHECK ===
		if (snakeColor != ColorType.Green)
		{
			int dist = Mathf.Abs(targetPosition.x - startPosition.x) + Mathf.Abs(targetPosition.y - startPosition. y);
			if (dist != 1) return false;
		}

		// === SNAKE COLLISION ===
		foreach (var snake in GameManager.Instance.snakesOnLevel)
		{
			if (snake == this)
			{
				if (endToMove == SnakeEnd. Tail && targetPosition == GetHeadPosition())
					continue; // Red snake tail toward head is allowed
				
				if (Body.Contains(targetPosition))
					return false;
			}
			else
			{
				if (snake.Body.Contains(targetPosition))
					return false;
			}
		}

		var grid = GameManager.Instance.grid;
		var targetObjects = grid.GetObjects(targetPosition);

		// === WALL CHECK ===
		if (targetObjects. OfType<Wall>().Any())
			return false;

		// === LIFT GATE CHECK (Physical Wall) ===
		// If gate exists AND is NOT Open, it blocks movement.
		var liftGate = targetObjects.OfType<LiftGate>().FirstOrDefault();
		if (liftGate != null && !liftGate.IsOpen)
		{
			Debug.Log($"Cannot move to {targetPosition} - Lift Gate is CLOSED!");
			return false;
		}

		// === LASER GATE CHECK (Energy) ===
		// Laser Gates DO NOT block movement. You can walk into them (and die).
		// So we do explicitly NOTHING here.

		// === BOX PUSHING ===
		if (targetObjects.OfType<Box>().Any())
		{
			if (endToMove == SnakeEnd. Tail) return false; // Only head can push
			return CanPushBox(startPosition, targetPosition);
		}

		// === ICE CUBE SLIDING ===
		if (targetObjects.OfType<IceCube>().Any())
		{
			if (endToMove == SnakeEnd.Tail) return false; // Only head can push
			return CanSlideIceCube(startPosition, targetPosition);
		}

		// === HOLE CHECK ===
		if (targetObjects.OfType<Hole>().Any())
			return false;

		// === PORTAL CHECK ===
		var portal = targetObjects.OfType<Portal>().FirstOrDefault();
		if (portal != null && ! portal.IsActive())
			return false;

		return true;
	}

	/// <summary>
	/// Check if a box can be pushed to the next cell. 
	/// </summary>
	private bool CanPushBox(Vector2Int from, Vector2Int toBox)
	{
		Vector2Int dir = GetPushDirection(from, toBox);
		Vector2Int landPos = toBox + dir;
		
		// GDD: Check if we are pushing the box INTO a Portal
		// Boxes do NOT teleport, they just occupy the portal tile.
		// So we just check if landPos is free.
		
		return IsLocationFreeForObject(landPos);
	}

	/// <summary>
	/// ICE CUBE SLIDING: Ice cubes slide until they hit something solid or a hole.
	/// They can pass through ACTIVE portals.
	/// </summary>
	private bool CanSlideIceCube(Vector2Int from, Vector2Int toCube)
	{
		Vector2Int dir = GetPushDirection(from, toCube);
		Vector2Int current = toCube;
		_lastValidIceCubePos = toCube;

		while (true)
		{
			Vector2Int next = current + dir;

			if (! IsLocationFreeForObject(next))
				break;

			var nextObjects = GameManager.Instance.grid.GetObjects(next);

			// Check for hole (stop and fill it)
			if (nextObjects. OfType<Hole>().Any())
			{
				_lastValidIceCubePos = next;
				return true;
			}

			// Check for active portal (teleport through it)
			var portal = nextObjects.OfType<Portal>().FirstOrDefault();
			if (portal != null && portal.IsActive())
			{
				Vector2Int dest = portal.GetLinkedPortal().GetData().position;
				if (IsLocationFreeForObject(dest))
				{
					_lastValidIceCubePos = dest;
					current = dest;
					continue; // Keep sliding from exit portal
				}
				else
				{
					_lastValidIceCubePos = current;
					return _lastValidIceCubePos != toCube;
				}
			}

			_lastValidIceCubePos = next;
			current = next;
		}

		return _lastValidIceCubePos != toCube;
	}

	/// <summary>
	/// Check if a location is free for pushing objects (boxes/ice).
	/// </summary>
	private bool IsLocationFreeForObject(Vector2Int pos)
	{
		var grid = GameManager.Instance.grid;

		// Out of bounds
		if (pos. x < 0 || pos. x >= grid.GetWidth() || pos.y < 0 || pos.y >= grid.GetHeight())
			return false;

		// Other snakes in the way
		foreach (var s in GameManager.Instance.snakesOnLevel)
		{
			if (s. Body.Contains(pos)) return false;
		}

		var objects = grid.GetObjects(pos);

		// Static obstacles
		if (objects.OfType<Wall>().Any()) return false;
		if (objects.OfType<Box>().Any()) return false;
		if (objects.OfType<IceCube>().Any()) return false;
		if (objects. OfType<Hole>().Any()) return false;

		// === LIFT GATE (Physical) ===
		// If closed, it acts as a wall.
		var liftGate = objects.OfType<LiftGate>().FirstOrDefault();
		if (liftGate != null && !liftGate.IsOpen)
		{
			// Closed Gate blocks objects
			return false;
		}

		// === LASER GATE (Energy) ===
		// Does NOT block objects. If object lands here, it gets destroyed (handled in GameManager).
		// So we return true here.

		return true;
	}

	/// <summary>
	/// Calculate push direction.  GREEN snake uses special wrapping logic.
	/// </summary>
	private Vector2Int GetPushDirection(Vector2Int from, Vector2Int to)
	{
		if (snakeColor == ColorType.Green)
		{
			var grid = GameManager.Instance.grid;
			int w = grid.GetWidth();
			int h = grid.GetHeight();

			int dx = to.x - from.x;
			if (Mathf.Abs(dx) > 1) dx = (dx > 0) ? -1 : 1;

			int dy = to.y - from.y;
			if (Mathf.Abs(dy) > 1) dy = (dy > 0) ? -1 : 1;

			return new Vector2Int(dx, dy);
		}

		return new Vector2Int(Mathf. Clamp(to.x - from. x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
	}

	/// <summary>
	/// Execute the move. Updates body, handles eating, portals, and objects.
	/// </summary>
	private void PerformMove(Vector2Int targetPosition, SnakeEnd endToMove)
	{
		var grid = GameManager.Instance.grid;
		Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

		// === PHASE 1: MOVE PHYSICAL OBJECTS ===
		if (grid.HasObjectOfType<Box>(targetPosition))
		{
			Vector2Int dir = GetPushDirection(startPosition, targetPosition);
			GameManager.Instance.MoveBox(targetPosition, targetPosition + dir);
		}
		else if (grid.HasObjectOfType<IceCube>(targetPosition))
		{
			GameManager.Instance.MoveIceCube(targetPosition, _lastValidIceCubePos);
		}

		// === PHASE 2: MOVE SNAKE BODY ===
		bool eaten = false;

		if (endToMove == SnakeEnd.Head)
		{
			var fruit = grid.GetObjects(targetPosition).OfType<Fruit>().FirstOrDefault();
			if (fruit != null && fruit. CanSnakeInteract(this, SnakeEnd.Head))
			{
				eaten = true;
			}

			snakeBody. Insert(0, targetPosition);
			if (! eaten) snakeBody.RemoveAt(snakeBody.Count - 1);
		}
		else // Tail move
		{
			snakeBody.Add(targetPosition);
			snakeBody.RemoveAt(0);
		}

		// === PHASE 3: PORTAL TELEPORTATION ===
		Vector2Int currentEndPos = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();
		var portal = grid.GetObjectOfType<Portal>(currentEndPos);

		if (portal != null && portal.IsActive())
		{
			Vector2Int dest = portal.GetLinkedPortal().GetData().position;
			if (IsLocationFreeForObject(dest))
			{
				if (endToMove == SnakeEnd.Head)
					snakeBody[0] = dest;
				else
					snakeBody[snakeBody.Count - 1] = dest;
			}
		}

		// === PHASE 4: NOTIFICATIONS ===
		if (eaten)
			OnGrew?. Invoke();
		else
			OnMoved?.Invoke();

		foreach (var obj in grid.GetObjects(snakeBody[0]).ToList())
		{
			obj.OnSnakeEntered(this, endToMove);
		}

		GameManager.Instance.ReportSnakeMoved();
	}

	public Vector2Int GetHeadPosition() => snakeBody[0];
	public Vector2Int GetTailPosition() => snakeBody[snakeBody.Count - 1];
	public void RemoveFromGame() { snakeBody.Clear(); OnRemoved?.Invoke(); }
}