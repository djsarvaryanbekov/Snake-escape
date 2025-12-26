using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Snake
{
    public event Action OnMoved;
    public event Action OnGrew;
    public event Action OnRemoved;
    public event Action OnSliced;
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
        if (data.headPosition != data.tailPosition) snakeBody.Add(data.tailPosition);
    }

    public void SetSelected(bool selected, SnakeEnd part)
    {
        visualizer?.ToggleSelectionVisuals(selected, part);
    }

    public void SliceAt(Vector2Int hazardPos)
    {
        int index = snakeBody.IndexOf(hazardPos);
        if (index == -1) return;

        if (index == 0) snakeBody.RemoveAt(0);
        else snakeBody.RemoveRange(index, snakeBody.Count - index);

        if (snakeBody.Count == 0) GameManager.Instance.KillSnake(this);
        else OnSliced?.Invoke();
    }

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
            _preWrapTarget = targetPosition;
            HandleWrappingIndices(ref targetPosition);
        }

        var grid = GameManager.Instance.grid;
        var targetObjects = grid.GetObjects(targetPosition);

        foreach (var obj in targetObjects)
        {
            if (obj is Fruit fruit && !fruit.CanSnakeInteract(this, endToMove))
            {
                OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
                return;
            }
            if (obj is Exit exit && !exit.CanSnakeInteract(this, endToMove))
            {
                OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
                return;
            }
        }

        if (IsValidMove(targetPosition, endToMove)) PerformMove(targetPosition, endToMove);
        else OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
    }

    private void HandleWrappingIndices(ref Vector2Int pos)
    {
        var grid = GameManager.Instance.grid;
        if (pos.x < 0) pos.x = grid.GetWidth() - 1;
        else if (pos.x >= grid.GetWidth()) pos.x = 0;
        if (pos.y < 0) pos.y = grid.GetHeight() - 1;
        else if (pos.y >= grid.GetHeight()) pos.y = 0;
    }

    private bool IsValidMove(Vector2Int targetPosition, SnakeEnd endToMove)
    {
        Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

        if (snakeColor != ColorType.Green)
        {
            int dist = Mathf.Abs(targetPosition.x - startPosition.x) + Mathf.Abs(targetPosition.y - startPosition.y);
            if (dist != 1) return false;
        }

        foreach (var snake in GameManager.Instance.snakesOnLevel)
        {
            if (snake == this)
            {
                if (endToMove == SnakeEnd.Tail && targetPosition == GetHeadPosition()) continue;
                if (Body.Contains(targetPosition)) return false;
            }
            else if (snake.Body.Contains(targetPosition)) return false;
        }

        var grid = GameManager.Instance.grid;
        var targetObjects = grid.GetObjects(targetPosition);

        if (targetObjects.OfType<Wall>().Any()) return false;
        
        var liftGate = targetObjects.OfType<LiftGate>().FirstOrDefault();
        if (liftGate != null && !liftGate.IsOpen) return false;

        if (targetObjects.OfType<Box>().Any())
        {
            if (endToMove == SnakeEnd.Tail) return false;
            return CanPushBox(startPosition, targetPosition);
        }

        if (targetObjects.OfType<IceCube>().Any())
        {
            if (endToMove == SnakeEnd.Tail) return false;
            return CanSlideIceCube(startPosition, targetPosition);
        }

        if (targetObjects.OfType<Hole>().Any()) return false;

        return true;
    }

    private bool CanPushBox(Vector2Int fromSnake, Vector2Int toBox)
    {
        Box box = GameManager.Instance.grid.GetObjectOfType<Box>(toBox);
        if (box == null) return false;

        Vector2Int dir = GetPushDirection(fromSnake, toBox);
        bool usesPortal = false;
        Vector2Int portalDelta = Vector2Int.zero;

        foreach (var pos in box.OccupiedCells)
        {
            Vector2Int target = pos + dir;
            Portal p = GameManager.Instance.grid.GetObjectOfType<Portal>(target);
            if (p != null && p.IsActive())
            {
                usesPortal = true;
                portalDelta = p.GetLinkedPortal().GetData().position - target;
                break;
            }
        }

        foreach (var currentPos in box.OccupiedCells)
        {
            Vector2Int targetPos = currentPos + dir;
            if (usesPortal) targetPos += portalDelta;
            if (box.OccupiedCells.Contains(targetPos)) continue;
            if (!IsLocationFreeForObject(targetPos)) return false;
        }
        return true;
    }

    private bool CanSlideIceCube(Vector2Int from, Vector2Int toCube)
    {
        IceCube ice = GameManager.Instance.grid.GetObjectOfType<IceCube>(toCube);
        if (ice == null) return false;

        Vector2Int dir = GetPushDirection(from, toCube);
        int steps = 0;
        List<Vector2Int> virtualShape = new List<Vector2Int>(ice.OccupiedCells);

        while (steps < 50)
        {
            List<Vector2Int> nextShapeStep = new List<Vector2Int>();
            bool blocked = false;
            
            // Calculate next step for whole shape
            foreach (var pos in virtualShape)
            {
                Vector2Int target = pos + dir;
                
                // Check Portal
                Portal p = GameManager.Instance.grid.GetObjectOfType<Portal>(target);
                if (p != null && p.IsActive())
                {
                    Vector2Int dest = p.GetLinkedPortal().GetData().position;
                    // Teleport jump
                    target = dest;
                }

                if (!virtualShape.Contains(target))
                {
                    if (!IsLocationFreeForObject(target))
                    {
                        blocked = true;
                        break;
                    }
                }
                nextShapeStep.Add(target);
            }

            if (blocked) break;
            
            // Check Holes (Simulation)
            int holesCount = 0;
            foreach (var pos in nextShapeStep)
            {
                if (GameManager.Instance.grid.HasObjectOfType<Hole>(pos)) holesCount++;
            }

            if (holesCount == nextShapeStep.Count)
            {
                _lastValidIceCubePos = toCube + (dir * (steps + 1)); 
                return true;
            }

            virtualShape = nextShapeStep;
            steps++;
        }

        if (steps > 0)
        {
            _lastValidIceCubePos = toCube + (dir * steps);
            return true;
        }
        return false;
    }

    private bool IsLocationFreeForObject(Vector2Int pos)
    {
        var grid = GameManager.Instance.grid;
        if (pos.x < 0 || pos.x >= grid.GetWidth() || pos.y < 0 || pos.y >= grid.GetHeight()) return false;

        foreach (var s in GameManager.Instance.snakesOnLevel)
            if (s.Body.Contains(pos)) return false;

        var objects = grid.GetObjects(pos);
        if (objects.OfType<Wall>().Any()) return false;
        if (objects.OfType<Box>().Any()) return false;
        if (objects.OfType<IceCube>().Any()) return false;
        if (objects.OfType<Fruit>().Any()) return false;
        if (objects.OfType<Exit>().Any()) return false;

        var liftGate = objects.OfType<LiftGate>().FirstOrDefault();
        if (liftGate != null && !liftGate.IsOpen) return false;

        return true;
    }

    private Vector2Int GetPushDirection(Vector2Int from, Vector2Int to)
    {
        if (snakeColor == ColorType.Green)
        {
            int dx = to.x - from.x;
            if (Mathf.Abs(dx) > 1) dx = (dx > 0) ? -1 : 1;
            int dy = to.y - from.y;
            if (Mathf.Abs(dy) > 1) dy = (dy > 0) ? -1 : 1;
            return new Vector2Int(dx, dy);
        }
        return new Vector2Int(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
    }

    private void PerformMove(Vector2Int targetPosition, SnakeEnd endToMove)
    {
        var grid = GameManager.Instance.grid;
        Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

        if (grid.HasObjectOfType<Box>(targetPosition))
        {
            Vector2Int dir = GetPushDirection(startPosition, targetPosition);
            GameManager.Instance.MoveBox(targetPosition, targetPosition + dir);
        }
        else if (grid.HasObjectOfType<IceCube>(targetPosition))
        {
            GameManager.Instance.MoveIceCube(targetPosition, _lastValidIceCubePos);
        }

        bool eaten = false;
        if (endToMove == SnakeEnd.Head)
        {
            var fruit = grid.GetObjects(targetPosition).OfType<Fruit>().FirstOrDefault();
            if (fruit != null && fruit.CanSnakeInteract(this, SnakeEnd.Head)) eaten = true;

            snakeBody.Insert(0, targetPosition);
            if (!eaten) snakeBody.RemoveAt(snakeBody.Count - 1);
        }
        else
        {
            snakeBody.Add(targetPosition);
            snakeBody.RemoveAt(0);
        }

        Vector2Int checkPos = (endToMove == SnakeEnd.Head) ? snakeBody[0] : snakeBody[snakeBody.Count - 1];
        var portal = grid.GetObjectOfType<Portal>(checkPos);
        if (portal != null && portal.IsActive())
        {
            Vector2Int dest = portal.GetLinkedPortal().GetData().position;
            if (IsLocationFreeForObject(dest))
            {
                if (endToMove == SnakeEnd.Head) snakeBody[0] = dest;
                else snakeBody[snakeBody.Count - 1] = dest;
                checkPos = dest;
            }
        }

        if (eaten) OnGrew?.Invoke(); else OnMoved?.Invoke();

        foreach (var obj in grid.GetObjects(checkPos).ToList())
            obj.OnSnakeEntered(this, endToMove);

        GameManager.Instance.ReportSnakeMoved();
    }

    public Vector2Int GetHeadPosition() => snakeBody[0];
    public Vector2Int GetTailPosition() => snakeBody[snakeBody.Count - 1];
    public void RemoveFromGame() { snakeBody.Clear(); OnRemoved?.Invoke(); }
}