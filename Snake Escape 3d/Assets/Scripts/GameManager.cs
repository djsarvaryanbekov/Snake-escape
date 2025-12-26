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
    
    // Object Events
    public event Action<FruitData> OnFruitEaten;
    public event Action<FruitData> OnFruitSpawned;
    public event Action<Vector2Int> OnExitRemoved;
    public event Action<Vector2Int, Vector2Int> OnBoxMoved;
    public event Action<Vector2Int, Vector2Int> OnIceCubeMoved;
    public event Action<Vector2Int, Vector2Int> OnHoleFilled;
    
    // Logic Events
    public event Action<PressurePlateData, bool> OnPlateStateChanged;
    public event Action<LiftGateData, bool> OnLiftGateStateChanged;
    public event Action<LaserGateData, bool> OnLaserGateStateChanged;
    public event Action<PortalData, bool> OnPortalStateChanged;
    public event Action<Snake> OnSnakeDied;

    [HideInInspector] public Grid grid { get; set; }
    [HideInInspector] public List<Snake> snakesOnLevel { get; private set; } = new List<Snake>();

    // Dictionaries for fast lookups
    private Dictionary<PlateColor, List<PressurePlate>> platesByColor = new Dictionary<PlateColor, List<PressurePlate>>();
    private Dictionary<PlateColor, List<LiftGate>> liftGatesByColor = new Dictionary<PlateColor, List<LiftGate>>();
    private Dictionary<PlateColor, List<LaserGate>> laserGatesByColor = new Dictionary<PlateColor, List<LaserGate>>();
    private List<Portal> allPortals = new List<Portal>();
    
    // State tracking to prevent event spam
    private Dictionary<Portal, bool> portalStates = new Dictionary<Portal, bool>();

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
        RefreshGameState();
    }

    public void RegisterPlate(PressurePlate plate, PressurePlateData data)
    {
        if (!platesByColor.ContainsKey(data.color)) platesByColor[data.color] = new List<PressurePlate>();
        platesByColor[data.color].Add(plate);
        
        // Connect visual event so plates animate
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

    // --- State Management ---

    public void RefreshGameState()
    {
        UpdateAllPlateStates();
        UpdateAllGateStates();
        UpdatePortalStates();
    }
    
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
                    if (!gate.IsOpen)
                    {
                        gate.Open();
                        OnLiftGateStateChanged?.Invoke(gate.GetData(), true);
                    }
                }
                else
                {
                    // Safety Lock: Don't close if something is standing on it
                    if (IsCellOccupied(pos))
                    {
                        // Something is here! Keep it OPEN.
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
                bool shouldBeActive = !allPlatesActive;

                if (laser.IsActive != shouldBeActive)
                {
                    if (shouldBeActive) 
                    {
                        laser.Activate();
                        // TRAP LOGIC: Check if something is inside when it turns ON
                        CheckLaserKillZone(laser.GetData().position);
                    }
                    else 
                    {
                        laser.Deactivate();
                    }
                    OnLaserGateStateChanged?.Invoke(laser.GetData(), laser.IsActive);
                }
            }
        }
    }

    private void UpdatePortalStates()
    {
        foreach (var portal in allPortals)
        {
            bool isActive = portal.IsActive();

            if (!portalStates.ContainsKey(portal) || portalStates[portal] != isActive)
            {
                portalStates[portal] = isActive;
                OnPortalStateChanged?.Invoke(portal.GetData(), isActive);
            }
        }
    }

    // --- Object Interaction & Movement ---

    public void MoveBox(Vector2Int from, Vector2Int to)
    {
        Box box = grid.GetObjectOfType<Box>(from);
        if (box == null) return;

        Vector2Int delta = to - from;

        // Portal Logic
        Portal portal = grid.GetObjectOfType<Portal>(to);
        if (portal != null && portal.IsActive())
        {
            Vector2Int exitPos = portal.GetLinkedPortal().GetData().position;
            delta = exitPos - from;
        }

        MoveGridEntity(box, delta);

        CheckHazards(box); 
        if (box.OccupiedCells.Count > 0) CheckGravity(box); 
    }

    public void MoveIceCube(Vector2Int from, Vector2Int to)
    {
        IceCube ice = grid.GetObjectOfType<IceCube>(from);
        if (ice == null) return;
        
        Vector2Int delta = to - from;

        MoveGridEntity(ice, delta);
        
        CheckHazards(ice);
        if (ice.OccupiedCells.Count > 0) CheckGravity(ice);
    }

    private void MoveGridEntity(GridEntity entity, Vector2Int delta)
    {
        var grid = this.grid;
        
        foreach (var pos in entity.OccupiedCells) grid.RemoveObject(pos.x, pos.y, entity);

        List<Vector2Int> newPositions = new List<Vector2Int>();
        foreach (var pos in entity.OccupiedCells) newPositions.Add(pos + delta);

        entity.ClearPositions();
        foreach (var pos in newPositions)
        {
            entity.AddPosition(pos);
            grid.AddObject(pos.x, pos.y, entity);
        }

        for (int i = 0; i < newPositions.Count; i++)
        {
            Vector2Int oldPos = newPositions[i] - delta;
            if (entity is Box) OnBoxMoved?.Invoke(oldPos, newPositions[i]);
            else if (entity is IceCube) OnIceCubeMoved?.Invoke(oldPos, newPositions[i]);
        }
        
        RefreshGameState();
    }

    // --- Hazards & Gravity ---

    private void CheckHazards(GridEntity entity)
    {
        foreach (var pos in entity.OccupiedCells)
        {
            LaserGate laser = grid.GetObjectOfType<LaserGate>(pos);
            if (laser != null && laser.IsActive)
            {
                DestroyGridEntity(entity);
                return;
            }
        }
    }

    private void CheckGravity(GridEntity entity)
    {
        int supportNeeded = entity.OccupiedCells.Count;
        int holesFound = 0;

        foreach (var pos in entity.OccupiedCells)
        {
            if (grid.HasObjectOfType<Hole>(pos)) holesFound++;
        }

        if (holesFound == supportNeeded)
        {
            foreach (var pos in entity.OccupiedCells)
            {
                Hole h = grid.GetObjectOfType<Hole>(pos);
                if (h != null)
                {
                    grid.RemoveObject(pos.x, pos.y, h);
                    OnHoleFilled?.Invoke(pos, pos);
                }
            }
            DestroyGridEntity(entity);
        }
    }
    
    private void DestroyGridEntity(GridEntity entity)
    {
        foreach (var pos in entity.OccupiedCells)
        {
            grid.RemoveObject(pos.x, pos.y, entity);
        }
        RefreshGameState();
        Debug.Log("Grid Entity Destroyed.");
    }

    private void CheckLaserKillZone(Vector2Int pos)
    {
        // Slice Snakes
        foreach (var snake in snakesOnLevel.ToList())
        {
            if (snake.Body.Contains(pos))
            {
                snake.SliceAt(pos);
            }
        }

        // Destroy Objects
        var objects = grid.GetObjects(pos);
        var entity = objects.OfType<GridEntity>().FirstOrDefault();
        if (entity != null)
        {
            DestroyGridEntity(entity);
        }
    }

    public void KillSnake(Snake snake)
    {
        snake.RemoveFromGame();
        if (snakesOnLevel.Contains(snake)) snakesOnLevel.Remove(snake);
        
        OnSnakeDied?.Invoke(snake);
        RefreshGameState();
        Debug.Log("Snake Killed.");
    }
    
    // --- Other Logic ---

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
        if (e != null) grid.RemoveObject(data.position.x, data.position.y, e);
        
        OnExitRemoved?.Invoke(data.position);
        SpawnFruitFromRemainingSnakes(data.position);

        if (snakesOnLevel.Count == 0) LevelWin?.Invoke(this, EventArgs.Empty);

        RefreshGameState();
    }

    private void SpawnFruitFromRemainingSnakes(Vector2Int exitPosition)
    {
        List<ColorType> remainingColors = snakesOnLevel.Select(s => s.Color).Distinct().ToList();
        if (remainingColors.Count == 0) return;

        FruitData newFruit = new FruitData { colors = remainingColors, position = exitPosition };
        Fruit fruitObject = new Fruit(newFruit);
        grid.AddObject(exitPosition.x, exitPosition.y, fruitObject);

        OnFruitSpawned?.Invoke(newFruit);
    }

    public void ReportFruitEaten(FruitData data)
    {
        Fruit f = grid.GetObjectOfType<Fruit>(data.position);
        if (f != null) grid.RemoveObject(data.position.x, data.position.y, f);
        OnFruitEaten?.Invoke(data);
    }

    public bool IsCellOccupied(Vector2Int pos)
    {
        if (snakesOnLevel.Any(s => s.Body.Contains(pos))) return true;
        if (grid.HasObjectOfType<Box>(pos)) return true;
        if (grid.HasObjectOfType<IceCube>(pos)) return true;
        return false;
    }

    public Snake GetSnakeAtPosition(Vector2Int pos, out SnakeEnd part)
    {
        foreach (var s in snakesOnLevel)
        {
            if (s.Body.Count == 0) continue; // Skip dead snakes
            if (pos == s.GetHeadPosition()) { part = SnakeEnd.Head; return s; }
            if (pos == s.GetTailPosition()) { part = SnakeEnd.Tail; return s; }
        }
        part = default; return null;
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

    public void ClearLevelData()
    {
        snakesOnLevel.Clear();
        platesByColor.Clear();
        liftGatesByColor.Clear();
        laserGatesByColor.Clear();
        allPortals.Clear();
        portalStates.Clear();
    }
}