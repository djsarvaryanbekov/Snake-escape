using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public interface IGridObject
{
    bool CanSnakeInteract(Snake snake, SnakeEnd end);
    void OnSnakeEntered(Snake snake, SnakeEnd end);
}

// --- NEW BASE CLASS FOR SHAPES (Gravity System) ---
public abstract class GridEntity : IGridObject
{
    public List<Vector2Int> OccupiedCells { get; private set; } = new List<Vector2Int>();
    public int EntityId { get; private set; }

    public GridEntity()
    {
        EntityId = UnityEngine.Random.Range(0, 999999);
    }

    public void AddPosition(Vector2Int pos)
    {
        if (!OccupiedCells.Contains(pos)) OccupiedCells.Add(pos);
    }

    public void ClearPositions() => OccupiedCells.Clear();

    public abstract bool CanSnakeInteract(Snake snake, SnakeEnd end);
    public abstract void OnSnakeEntered(Snake snake, SnakeEnd end);
}

public class Box : GridEntity
{
    public override bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
    public override void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}

public class IceCube : GridEntity
{
    public override bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
    public override void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}
// --------------------------------------------------

public class EmptyCell : IGridObject
{
    public bool CanSnakeInteract(Snake snake, SnakeEnd end) => true;
    public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}

public class Wall : IGridObject
{
    public bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
    public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}

public class Hole : IGridObject
{
    // Updated: Returns false for interaction (Snake can't walk on it)
    // BUT Snake.cs specifically checks for this type to allow Boxes to enter it.
    public bool CanSnakeInteract(Snake snake, SnakeEnd end) => false;
    public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
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
        GameManager.Instance.SnakeHasExited(snake, data);
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

public class LiftGate : IGridObject
{
    private readonly LiftGateData data;
    public bool IsOpen { get; private set; } = false;

    public LiftGate(LiftGateData data) { this.data = data; }
    public LiftGateData GetData() => data;

    public bool CanSnakeInteract(Snake snake, SnakeEnd end) => IsOpen;
    public void OnSnakeEntered(Snake snake, SnakeEnd end) { }

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
}

public class LaserGate : IGridObject
{
    private readonly LaserGateData data;
    public bool IsActive { get; private set; } = true;

    public LaserGate(LaserGateData data) { this.data = data; }
    public LaserGateData GetData() => data;

    // Returns TRUE so objects/snakes can enter (and die)
    public bool CanSnakeInteract(Snake snake, SnakeEnd end) => true;

    public void OnSnakeEntered(Snake snake, SnakeEnd end)
    {
        if (IsActive)
        {
            // Placeholder for Blue snake immunity if needed later
            GameManager.Instance.KillSnake(snake);
        }
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

public class Portal : IGridObject
{
    private readonly PortalData data;
    private Portal linkedPortal;

    public Portal(PortalData data) { this.data = data; }

    public void SetLinkedPortal(Portal other) => linkedPortal = other;
    public Portal GetLinkedPortal() => linkedPortal;
    public PortalData GetData() => data;

    public bool IsActive()
    {
        if (linkedPortal == null) return false;
        var destObjects = GameManager.Instance.grid.GetObjects(linkedPortal.data.position);
        
        // Checks for blocking physical objects
        foreach (var obj in destObjects)
        {
            if (obj is Wall || obj is Box || obj is IceCube || obj is LiftGate gate && !gate.IsOpen) 
                return false;
        }
        
        foreach (var snake in GameManager.Instance.snakesOnLevel)
        {
            if (snake.Body.Contains(linkedPortal.data.position)) return false;
        }

        return true;
    }

    public bool CanSnakeInteract(Snake snake, SnakeEnd end) => IsActive();
    public void OnSnakeEntered(Snake snake, SnakeEnd end) { }
}