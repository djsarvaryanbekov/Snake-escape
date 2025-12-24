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
        if (data.headPosition != data.tailPosition) snakeBody.Add(data.tailPosition);
    }

    public void TryMoveTo(Vector2Int targetPosition, SnakeEnd endToMove)
    {
        // Animation check
        if (visualizer != null && visualizer.IsAnimating)
        {
            OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CurrentlyAnimating });
            return;
        }

        // Red Snake: Only Red can move via tail
        if (endToMove == SnakeEnd.Tail && this.snakeColor != ColorType.Red)
        {
            OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.CantMoveTail });
            return;
        }

        // Green Snake: Border Wrapping
        if (this.snakeColor == ColorType.Green)
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
                if (!fruit.CanSnakeInteract(this, endToMove))
                {
                    OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
                    return;
                }
            }
            // Check Exit interaction
            else if (obj is Exit exit)
            {
                if (!exit.CanSnakeInteract(this, endToMove))
                {
                    OnMoveFailed?.Invoke(this, new OnMoveFailedEventArgs { reason = MoveFailureReason.InteractionFailed });
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
    /// Main movement validation. Checks all GDD rules.
    /// </summary>
    private bool IsValidMove(Vector2Int targetPosition, SnakeEnd endToMove)
    {
        Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

        // === ADJACENCY CHECK ===
        if (snakeColor != ColorType.Green)
        {
            int dist = Mathf.Abs(targetPosition.x - startPosition.x) + Mathf.Abs(targetPosition.y - startPosition.y);
            // Distance must be 1, OR it must be a self-movement (tail to head for Red Snake)
            if (dist != 1) return false;
        }

        // === SNAKE COLLISION ===
        foreach (var snake in GameManager.Instance.snakesOnLevel)
        {
            if (snake == this)
            {
                if (endToMove == SnakeEnd.Tail && targetPosition == GetHeadPosition())
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
        if (targetObjects.OfType<Wall>().Any())
            return false;

        // === LIFT GATE CHECK (Physical Wall) ===
        var liftGate = targetObjects.OfType<LiftGate>().FirstOrDefault();
        if (liftGate != null && !liftGate.IsOpen)
        {
            return false;
        }

        // === BOX PUSHING ===
        if (targetObjects.OfType<Box>().Any())
        {
            if (endToMove == SnakeEnd.Tail) return false; // Only head can push
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
        // Note: Entering a portal is allowed if the portal is Active OR if it's Inactive (just a floor).
        // If Active, we teleport (handled in PerformMove). If Inactive, we just step on it.
        // We only block if there is a blocking object on top of it (checked by Wall/Box checks above).
        
        return true;
    }

    /// <summary>
    /// Check if a box can be pushed. Includes PORTAL LOGIC.
    /// </summary> private bool
  
    private bool CanPushBox(Vector2Int fromSnake, Vector2Int toBox)
    {
        Box box = GameManager.Instance.grid.GetObjectOfType<Box>(toBox);
        if (box == null) return false;

        Vector2Int dir = GetPushDirection(fromSnake, toBox);

        // Check if ANY part of the box hits a portal
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

        // Validate the move for the whole shape
        foreach (var currentPos in box.OccupiedCells)
        {
            Vector2Int targetPos = currentPos + dir;
            
            // If teleporting, adjust target
            if (usesPortal) targetPos += portalDelta;

            // Ignore self-collision
            if (box.OccupiedCells.Contains(targetPos)) continue;

            if (!IsLocationFreeForObject(targetPos)) return false;
        }
        
        return true;
    }
    // Same Logic for Ice Cubes sliding
    private bool CanSlideIceCube(Vector2Int from, Vector2Int toCube)
    {
        IceCube ice = GameManager.Instance.grid.GetObjectOfType<IceCube>(toCube);
        if (ice == null) return false;

        Vector2Int dir = GetPushDirection(from, toCube);
        
        // We simulate sliding the WHOLE shape step-by-step
        bool canSlide = true;
        
        // Safety Break
        int steps = 0;
        
        // We need to track the "virtual position" of the shape during calculation
        List<Vector2Int> virtualShape = new List<Vector2Int>(ice.OccupiedCells);

        while(canSlide && steps < 50)
        {
            List<Vector2Int> nextShapeStep = new List<Vector2Int>();
            bool blocked = false;
            bool overHoles = true; 

            // 1. Predict Next Step for all parts
            foreach(var pos in virtualShape)
            {
                Vector2Int target = pos + dir;
                if (!virtualShape.Contains(target)) // Ignore self
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

            // 2. Check Gravity (Simulation)
            // If the *entire* new shape is over holes, it stops and falls.
            int holesCount = 0;
            foreach(var pos in nextShapeStep)
            {
                if (!GameManager.Instance.grid.HasObjectOfType<Hole>(pos))
                {
                    overHoles = false;
                }
                else
                {
                    holesCount++;
                }
            }
            
            // Logic: If it moved successfully, we update our "last valid" pos.
            // In the new system, GameManager moves by Delta.
            // So we just need to know if we CAN move at least 1 step.
            
            if (holesCount == nextShapeStep.Count)
            {
                // It fell into holes! Valid move, stops here.
                _lastValidIceCubePos = toCube + (dir * (steps + 1)); // Approximate for single cell ref
                return true; 
            }

            virtualShape = nextShapeStep;
            steps++;
        }
        
        // If steps > 0, we moved at least once.
        if (steps > 0)
        {
             // For the prototype, we pass the "primary" cell's destination to GameManager.
             // GameManager calculates delta from that.
             _lastValidIceCubePos = toCube + (dir * steps);
             return true;
        }

        return false;
    }
    /// <summary>
    /// Check if a location is free for pushing objects (boxes/ice).
    /// </summary>
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
        
        // NOTE: We ALLOW Holes (so objects can fall in)
        // NOTE: We ALLOW LaserGates (so objects can enter and die)
        
        var liftGate = objects.OfType<LiftGate>().FirstOrDefault();
        if (liftGate != null && !liftGate.IsOpen) return false;

        return true;
    }
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

        return new Vector2Int(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
    }

    private void PerformMove(Vector2Int targetPosition, SnakeEnd endToMove)
    {
        var grid = GameManager.Instance.grid;
        Vector2Int startPosition = (endToMove == SnakeEnd.Head) ? GetHeadPosition() : GetTailPosition();

        // === PHASE 1: MOVE PHYSICAL OBJECTS ===
        if (grid.HasObjectOfType<Box>(targetPosition))
        {
            Vector2Int dir = GetPushDirection(startPosition, targetPosition);
            // We move box to targetPosition + dir. 
            // The GameManager will handle the portal teleportation logic if that target is a portal.
            GameManager.Instance.MoveBox(targetPosition, targetPosition + dir);
        }
        else if (grid.HasObjectOfType<IceCube>(targetPosition))
        {
            // IceCube logic pre-calculates the final stop position (_lastValidIceCubePos)
            GameManager.Instance.MoveIceCube(targetPosition, _lastValidIceCubePos);
        }

        // === PHASE 2: MOVE SNAKE BODY ===
        bool eaten = false;

        if (endToMove == SnakeEnd.Head)
        {
            var fruit = grid.GetObjects(targetPosition).OfType<Fruit>().FirstOrDefault();
            if (fruit != null && fruit.CanSnakeInteract(this, SnakeEnd.Head))
            {
                eaten = true;
            }

            // Insert new Head position
            snakeBody.Insert(0, targetPosition);
            
            // Handle Tail Removal
            // Note: If eaten, we simply don't remove the tail (snake grows).
            // BUT: If the Snake eats a fruit AND enters a portal in the same move, logic still holds.
            if (!eaten) 
            {
                snakeBody.RemoveAt(snakeBody.Count - 1);
            }
        }
        else // Tail move (Red Snake reverse)
        {
            // Add new Tail
            snakeBody.Add(targetPosition);
            // Remove Old Head
            snakeBody.RemoveAt(0);
        }

        // === PHASE 3: PORTAL TELEPORTATION (Snake Body) ===
        // If the Snake Head moved onto an Active Portal, the Head segment moves to the Exit.
        // This creates a "gap" in the body list between index 0 (Exit) and index 1 (Entrance),
        // effectively stretching the snake through the portal.
        
        Vector2Int newHeadPos = GetHeadPosition();
        Vector2Int newTailPos = GetTailPosition();
        
        // Check Head Teleport
        if (endToMove == SnakeEnd.Head)
        {
            var portal = grid.GetObjectOfType<Portal>(newHeadPos);
            if (portal != null && portal.IsActive())
            {
                Vector2Int dest = portal.GetLinkedPortal().GetData().position;
                
                // Only teleport if the destination isn't blocked by something 
                // (though ValidateMove usually catches this, dynamic changes might occur).
                if (IsLocationFreeForObject(dest))
                {
                    snakeBody[0] = dest;
                }
            }
        }
        // Check Tail Teleport (Red Snake Reverse)
        // If the tail moves ONTO a portal, it should teleport too.
        else if (endToMove == SnakeEnd.Tail)
        {
             var portal = grid.GetObjectOfType<Portal>(newTailPos);
             if (portal != null && portal.IsActive())
             {
                 Vector2Int dest = portal.GetLinkedPortal().GetData().position;
                 if (IsLocationFreeForObject(dest))
                 {
                     snakeBody[snakeBody.Count - 1] = dest;
                 }
             }
        }

        // === PHASE 4: NOTIFICATIONS ===
        if (eaten)
            OnGrew?.Invoke();
        else
            OnMoved?.Invoke();

        // Notify objects at the new head position (like Exits or Buttons)
        foreach (var obj in grid.GetObjects(snakeBody[0]).ToList())
        {
            obj.OnSnakeEntered(this, endToMove);
        }

        GameManager.Instance.ReportSnakeMoved();
    }

    
    // Add this event at the top
    public event Action OnSliced; 

    public void SliceAt(Vector2Int hazardPos)
    {
        int index = snakeBody.IndexOf(hazardPos);
        
        if (index == -1) return; // Position not part of snake

        // GDD Logic:
        // 1. If Head (Index 0) hits: "Head destroyed, previous segment becomes head."
        if (index == 0)
        {
            snakeBody.RemoveAt(0);
        }
        // 2. If Body/Tail hits: "Segment destroyed, all subsequent segments towards tail destroyed."
        else
        {
            // Example: Body [0, 1, 2, 3, 4]. Hit at 2.
            // Remove range starting at 2, count is (5 - 2) = 3 items (2,3,4).
            // Result: [0, 1].
            snakeBody.RemoveRange(index, snakeBody.Count - index);
        }

        // Check if snake is completely destroyed
        if (snakeBody.Count == 0)
        {
            RemoveFromGame();
        }
        else
        {
            // Notify Visualizer to redraw the shorter snake
            OnSliced?.Invoke();
        }
    }
    
    public Vector2Int GetHeadPosition() => snakeBody[0];
    public Vector2Int GetTailPosition() => snakeBody[snakeBody.Count - 1];
    public void RemoveFromGame() { snakeBody.Clear(); OnRemoved?.Invoke(); }
}