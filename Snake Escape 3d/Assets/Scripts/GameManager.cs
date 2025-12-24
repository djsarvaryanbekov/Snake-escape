using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SnakeEnd { Head, Tail }
public enum ColorType { Red, Green, Blue, Yellow }
public enum PlateColor { Yellow, Purple, Orange }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // --- Events ---
    public event EventHandler LevelWin;
    public event EventHandler ReloadLevel;
    public event Action<Level_SO> OnLevelLoaded;
    public event Action<FruitData> OnFruitEaten;
    public event Action<FruitData> OnFruitSpawned;
    public event Action<Vector2Int> OnExitRemoved;
    public event Action<Vector2Int, Vector2Int> OnBoxMoved;
    public event Action<Vector2Int, Vector2Int> OnIceCubeMoved;
    public event Action<Vector2Int, Vector2Int> OnHoleFilled;
    public event Action<PressurePlateData, bool> OnPlateStateChanged;
    
    // Gate Events
    public event Action<LiftGateData, bool> OnLiftGateStateChanged;
    public event Action<LaserGateData, bool> OnLaserGateStateChanged;

    [HideInInspector] public Grid grid { get; set; }
    [HideInInspector] public List<Snake> snakesOnLevel { get; private set; } = new List<Snake>();

    // Dictionaries for fast lookups
    private Dictionary<PlateColor, List<PressurePlate>> platesByColor = new Dictionary<PlateColor, List<PressurePlate>>();
    private Dictionary<PlateColor, List<LiftGate>> liftGatesByColor = new Dictionary<PlateColor, List<LiftGate>>();
    private Dictionary<PlateColor, List<LaserGate>> laserGatesByColor = new Dictionary<PlateColor, List<LaserGate>>();
    private List<Portal> allPortals = new List<Portal>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    // --- Level Loading & Registration ---

    public void TriggerLevelLoad(Level_SO data)
    {
        LinkPortals();
        OnLevelLoaded?.Invoke(data);
        // Initial state check
        RefreshGameState();
    }

    public void RegisterPlate(PressurePlate plate, PressurePlateData data)
    {
        if (!platesByColor.ContainsKey(data.color)) platesByColor[data.color] = new List<PressurePlate>();
        platesByColor[data.color].Add(plate);
        // Note: We no longer subscribe to individual plate triggers for gate logic. 
        // We update everything centrally to fix the "Gate Stuck" bugs.
        plate.OnPlateTriggered += (p, active) => OnPlateStateChanged?.Invoke(p.GetData(), active);
        
    }

    public void RegisterLiftGate(LiftGate gate, LiftGateData data)
    {
        if (!liftGatesByColor.ContainsKey(data.color)) liftGatesByColor[data.color] = new List<LiftGate>();
        liftGatesByColor[data.color].Add(gate);
    }

    public void RegisterLaserGate(LaserGate gate, LaserGateData data)
    {
        if (!laserGatesByColor.ContainsKey(data.color)) laserGatesByColor[data.color] = new List<LaserGate>();
        laserGatesByColor[data.color].Add(gate);
    }

    public void RegisterPortal(Portal portal) => allPortals.Add(portal);

    // --- State Management (The Fix) ---

    /// <summary>
    /// Call this whenever ANY object moves on the grid.
    /// It updates Plates first, then checks if Gates should Open/Close based on the new Plate states.
    /// </summary>
    public void RefreshGameState()
    {
        UpdateAllPlateStates();
        UpdateAllGateStates();
    }
    
    // Kept for Snake.cs compatibility if it calls ReportSnakeMoved
    public void ReportSnakeMoved() => RefreshGameState();

    private void UpdateAllPlateStates()
    {
        foreach (var colorList in platesByColor.Values)
        {
            foreach (var plate in colorList)
            {
                Vector2Int pos = plate.GetData().position;
                bool occupied = IsCellOccupied(pos);
                plate.SetState(occupied);
            }
        }
    }

    private void UpdateAllGateStates()
    {
        // 1. HANDLE LIFT GATES (Physical)
        foreach (var color in liftGatesByColor.Keys)
        {
            // Check if ALL plates of this color are active
            bool allPlatesActive = false;
            if (platesByColor.ContainsKey(color) && platesByColor[color].Count > 0)
            {
                allPlatesActive = platesByColor[color].All(p => p.IsActive);
            }

            foreach (var gate in liftGatesByColor[color])
            {
                Vector2Int pos = gate.GetData().position;

                if (allPlatesActive)
                {
                    // Open Condition Met
                    if (!gate.IsOpen)
                    {
                        gate.Open();
                        OnLiftGateStateChanged?.Invoke(gate.GetData(), true);
                    }
                }
                else
                {
                    // Close Condition Met
                    // SAFETY CHECK: Is something standing on the gate?
                    if (IsCellOccupied(pos))
                    {
                        // Something is here! Keep it OPEN (Safety Lock).
                        if (!gate.IsOpen)
                        {
                            gate.Open();
                            OnLiftGateStateChanged?.Invoke(gate.GetData(), true);
                        }
                    }
                    else
                    {
                        // Safe to close
                        if (gate.IsOpen)
                        {
                            gate.Close();
                            OnLiftGateStateChanged?.Invoke(gate.GetData(), false);
                        }
                    }
                }
            }
        }

        // 2. HANDLE LASER GATES (Energy)
        foreach (var color in laserGatesByColor.Keys)
        {
            bool allPlatesActive = false;
            if (platesByColor.ContainsKey(color) && platesByColor[color].Count > 0)
            {
                allPlatesActive = platesByColor[color].All(p => p.IsActive);
            }

            foreach (var laser in laserGatesByColor[color])
            {
                // Plates Active = Laser OFF (Safe)
                // Plates Inactive = Laser ON (Dangerous)
                bool shouldBeActive = !allPlatesActive;

                if (laser.IsActive != shouldBeActive)
                {
                    if (shouldBeActive) laser.Activate(); else laser.Deactivate();
                    OnLaserGateStateChanged?.Invoke(laser.GetData(), laser.IsActive);
                }
            }
        }
    }


    public void FillHole(Vector2Int holePos, Vector2Int fillerPos)
    {
        var filler = grid.GetObjects(fillerPos).FirstOrDefault(o => o is Box || o is IceCube);
        if (filler != null) grid.RemoveObject(fillerPos.x, fillerPos.y, filler);

        Hole hole = grid.GetObjectOfType<Hole>(holePos);
        if (hole != null) grid.RemoveObject(holePos.x, holePos.y, hole);

        OnHoleFilled?.Invoke(holePos, fillerPos);
        RefreshGameState();
    }

    public void SnakeHasExited(Snake snake, ExitData data)
    {
        snake.RemoveFromGame();
        snakesOnLevel.Remove(snake);

        Exit e = grid.GetObjectOfType<Exit>(data.position);
        if (e != null)
        {
            grid.RemoveObject(data.position.x, data.position.y, e);
        }
        OnExitRemoved?.Invoke(data.position);

        SpawnFruitFromRemainingSnakes(data.position);

        if (snakesOnLevel.Count == 0)
        {
            LevelWin?.Invoke(this, EventArgs.Empty);
        }

        RefreshGameState();
    }

    private void SpawnFruitFromRemainingSnakes(Vector2Int exitPosition)
    {
        List<ColorType> remainingColors = snakesOnLevel.Select(s => s.Color).Distinct().ToList();

        if (remainingColors.Count == 0) return;

        FruitData newFruit = new FruitData
        {
            colors = remainingColors,
            position = exitPosition
        };

        Fruit fruitObject = new Fruit(newFruit);
        grid.AddObject(exitPosition.x, exitPosition.y, fruitObject);

        OnFruitSpawned?.Invoke(newFruit);
    }

    public void ReportFruitEaten(FruitData data)
    {
        Fruit f = grid.GetObjectOfType<Fruit>(data.position);
        if (f != null)
        {
            grid.RemoveObject(data.position.x, data.position.y, f);
        }
        OnFruitEaten?.Invoke(data);
    }

    // --- Helpers ---

    private bool IsCellOccupied(Vector2Int pos)
    {
        // Check Snakes
        if (snakesOnLevel.Any(s => s.Body.Contains(pos))) return true;

        // Check Boxes/Ice
        if (grid.HasObjectOfType<Box>(pos)) return true;
        if (grid.HasObjectOfType<IceCube>(pos)) return true;

        return false;
    }

    public Snake GetSnakeAtPosition(Vector2Int pos, out SnakeEnd part)
    {
        foreach (var s in snakesOnLevel)
        {
            if (pos == s.GetHeadPosition()) { part = SnakeEnd.Head; return s; }
            if (pos == s.GetTailPosition()) { part = SnakeEnd.Tail; return s; }
        }
        part = default; return null;
    }

    public void KillSnake(Snake snake)
    {
        snake.RemoveFromGame();
        snakesOnLevel.Remove(snake);
        RefreshGameState();
        Debug.Log("Snake Destroyed.");
    }

    private void LinkPortals()
    {
        var groups = allPortals.GroupBy(p => p.GetData().colorId);
        foreach (var group in groups)
        {
            var portals = group.ToList();
            if (portals.Count == 2)
            {
                portals[0].SetLinkedPortal(portals[1]);
                portals[1].SetLinkedPortal(portals[0]);
            }
        }
    }
    
    
    // --- Update in GameManager.cs ---

    
    private void MoveGridEntity(GridEntity entity, Vector2Int delta)
    {
        var grid = this.grid;
        
        // Remove from old positions
        foreach (var pos in entity.OccupiedCells)
        {
            grid.RemoveObject(pos.x, pos.y, entity);
        }

        // Calculate new positions
        List<Vector2Int> newPositions = new List<Vector2Int>();
        foreach (var pos in entity.OccupiedCells)
        {
            newPositions.Add(pos + delta);
        }

        // Update Entity
        entity.ClearPositions();
        foreach (var pos in newPositions)
        {
            entity.AddPosition(pos);
            grid.AddObject(pos.x, pos.y, entity);
        }

        // Trigger Events (Visuals)
        for (int i = 0; i < newPositions.Count; i++)
        {
            Vector2Int oldPos = newPositions[i] - delta;
            if (entity is Box) OnBoxMoved?.Invoke(oldPos, newPositions[i]);
            else if (entity is IceCube) OnIceCubeMoved?.Invoke(oldPos, newPositions[i]);
        }
        
        RefreshGameState();
    }

    // 2. UPDATED MOVE BOX
    public void MoveBox(Vector2Int from, Vector2Int to)
    {
        Box box = grid.GetObjectOfType<Box>(from);
        if (box == null) return;

        // 1. Calculate Delta (Direction)
        Vector2Int delta = to - from;

        // 2. Portal Check (Simple 1x1 Portal Logic for shapes)
        // If the 'leading' cell hits a portal, we calculate a jump
        Portal portal = grid.GetObjectOfType<Portal>(to);
        if (portal != null && portal.IsActive())
        {
            Vector2Int exitPos = portal.GetLinkedPortal().GetData().position;
            delta = exitPos - from; // Recalculate delta to jump across map
        }

        // 3. Move the Entity
        MoveGridEntity(box, delta);
        
        // 4. Check if it falls into holes
        CheckGravity(box);
    }


    public void MoveIceCube(Vector2Int from, Vector2Int to)
    {
        IceCube ice = grid.GetObjectOfType<IceCube>(from);
        if (ice == null) return;
        
        // Note: Snake.cs IceCube logic pre-calculates the final position 
        // including slide momentum and portals. We just calculate the delta.
        Vector2Int delta = to - from;

        MoveGridEntity(ice, delta);
        CheckGravity(ice);
    }


    private void CheckGravity(GridEntity entity)
    {
        int supportNeeded = entity.OccupiedCells.Count;
        int holesFound = 0;
        List<Hole> holesUnderneath = new List<Hole>();

        foreach (var pos in entity.OccupiedCells)
        {
            Hole h = grid.GetObjectOfType<Hole>(pos);
            if (h != null)
            {
                holesFound++;
                holesUnderneath.Add(h);
            }
        }

        // Falls only if COMPLETELY over holes
        if (holesFound == supportNeeded)
        {
            // Remove Entity
            foreach (var pos in entity.OccupiedCells)
            {
                grid.RemoveObject(pos.x, pos.y, entity);
                
                // Visual cleanup event for object destruction
                // OnObjectDestroyed?.Invoke(pos); // Add this if you implemented the destruction event
            }

            // Remove Holes
            foreach (var pos in entity.OccupiedCells)
            {
                Hole h = grid.GetObjectOfType<Hole>(pos);
                if (h != null)
                {
                    grid.RemoveObject(pos.x, pos.y, h);
                    OnHoleFilled?.Invoke(pos, pos);
                }
            }
            RefreshGameState();
        }
    }

    public void ClearLevelData()
    {
        snakesOnLevel.Clear();
        platesByColor.Clear();
        liftGatesByColor.Clear();
        laserGatesByColor.Clear();
        allPortals.Clear();
    }
}