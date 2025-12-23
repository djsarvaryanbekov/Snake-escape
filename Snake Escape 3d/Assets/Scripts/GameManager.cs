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
    
    // Events
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
    public event Action<LaserGateData, bool> OnGateStateChanged;

    [HideInInspector] public Grid grid { get; set; }
    [HideInInspector] public List<Snake> snakesOnLevel { get; private set; } = new List<Snake>();

    private Dictionary<PlateColor, List<PressurePlate>> platesByColor = new Dictionary<PlateColor, List<PressurePlate>>();
    // Updated Dictionaries
    private Dictionary<PlateColor, List<LiftGate>> liftGatesByColor = new Dictionary<PlateColor, List<LiftGate>>();
    private Dictionary<PlateColor, List<LaserGate>> laserGatesByColor = new Dictionary<PlateColor, List<LaserGate>>();

    private List<Portal> allPortals = new List<Portal>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void TriggerLevelLoad(Level_SO data)
    {
        LinkPortals();
        OnLevelLoaded?.Invoke(data);
        UpdateAllPlateStates();
    }

    public void RegisterPlate(PressurePlate plate, PressurePlateData data)
    {
        if (!platesByColor.ContainsKey(data.color)) platesByColor[data.color] = new List<PressurePlate>();
        platesByColor[data.color].Add(plate);
        
        plate.OnPlateTriggered += HandlePlateTrigger; 
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


    public void ReportSnakeMoved() => UpdateAllPlateStates();

    private void UpdateAllPlateStates()
    {
        foreach (var colorList in platesByColor.Values)
        {
            foreach (var plate in colorList)
            {
                Vector2Int pos = plate.GetData().position;
                bool occupied = snakesOnLevel.Any(s => s.Body.Contains(pos)) || 
                               grid.HasObjectOfType<Box>(pos) || 
                               grid.HasObjectOfType<IceCube>(pos);
                
                plate.SetState(occupied);
            }
        }
    }

    public void RegisterPortal(Portal portal) => allPortals.Add(portal);
    
    private void LinkPortals()
    {
        var groups = allPortals.GroupBy(p => p.GetData().colorId);
        foreach (var group in groups)
        {
            var portals = group.ToList();
            if (portals.Count == 2)
            {
                portals[0].SetLinkedPortal(portals[1]);
                portals[1]. SetLinkedPortal(portals[0]);
            }
        }
    }

    public void MoveBox(Vector2Int from, Vector2Int to)
    {
        if (grid.HasObjectOfType<Hole>(to)) { FillHole(to, from); return; }
        
        Box box = grid.GetObjectOfType<Box>(from);
        if (box != null)
        {
            grid.RemoveObject(from. x, from.y, box);
            grid.AddObject(to.x, to.y, box);
            OnBoxMoved?.Invoke(from, to);
            UpdateAllPlateStates();
        }
    }

    public void MoveIceCube(Vector2Int from, Vector2Int to)
    {
        if (grid.HasObjectOfType<Hole>(to)) { FillHole(to, from); return; }

        IceCube ice = grid.GetObjectOfType<IceCube>(from);
        if (ice != null)
        {
            grid.RemoveObject(from.x, from.y, ice);
            grid.AddObject(to.x, to.y, ice);
            OnIceCubeMoved?.Invoke(from, to);
            UpdateAllPlateStates();
        }
    }

    public void FillHole(Vector2Int holePos, Vector2Int fillerPos)
    {
        var filler = grid.GetObjects(fillerPos).FirstOrDefault(o => o is Box || o is IceCube);
        if (filler != null) grid.RemoveObject(fillerPos.x, fillerPos.y, filler);
        
        Hole hole = grid.GetObjectOfType<Hole>(holePos);
        if (hole != null) grid.RemoveObject(holePos.x, holePos. y, hole);

        OnHoleFilled?.Invoke(holePos, fillerPos);
        UpdateAllPlateStates();
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

        UpdateAllPlateStates();
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

    public void ClearLevelData()
    {
        snakesOnLevel.Clear();
        platesByColor.Clear();
        allPortals.Clear();
    }

    public Snake GetSnakeAtPosition(Vector2Int pos, out SnakeEnd part)
    {
        foreach (var s in snakesOnLevel)
        {
            if (pos == s.GetHeadPosition()) { part = SnakeEnd.Head; return s; }
            if (pos == s.GetTailPosition()) { part = SnakeEnd. Tail; return s; }
        }
        part = default; return null;
    }
    
    private void HandlePlateTrigger(PressurePlate plate, bool active)
    {
        PlateColor color = plate.GetData().color;
        OnPlateStateChanged?.Invoke(plate.GetData(), active);
        CheckGateSystem(color);
    }
    
    private void CheckGateSystem(PlateColor color)
    {
        if (!platesByColor.ContainsKey(color)) return;
        
        bool allPlatesActive = platesByColor[color].All(p => p.IsActive);

        // 1. HANDLE LIFT GATES (Physical)
        if (liftGatesByColor.ContainsKey(color))
        {
            foreach (var gate in liftGatesByColor[color])
            {
                Vector2Int pos = gate.GetData().position;

                if (allPlatesActive)
                {
                    // Plates Pressed -> Gate goes UP (Open)
                    if (!gate.IsOpen)
                    {
                        gate.Open();
                        OnLiftGateStateChanged?.Invoke(gate.GetData(), true); // Need to add this Event
                    }
                }
                else
                {
                    // Plates Released -> Gate tries to DOWN (Close)
                    // SAFETY CHECK: Is something standing on the gate?
                    if (IsCellOccupied(pos))
                    {
                        // Something is here! Keep it OPEN.
                        // We do nothing, waiting for the object to leave.
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
        if (laserGatesByColor.ContainsKey(color))
        {
            foreach (var laser in laserGatesByColor[color])
            {
                // Logic: Plates Active = Laser OFF (Safe). Plates Inactive = Laser ON (Kill).
                bool shouldBeActive = !allPlatesActive; 

                if (laser.IsActive != shouldBeActive)
                {
                    if (shouldBeActive) laser.Activate(); else laser.Deactivate();
                    OnLaserGateStateChanged?.Invoke(laser.GetData(), laser.IsActive); // Need to add this Event
                }
            }
        }
    }

    // Helper to check for Safety Lock
    private bool IsCellOccupied(Vector2Int pos)
    {
        // Check Snakes
        if (snakesOnLevel.Any(s => s.Body.Contains(pos))) return true;
        
        // Check Boxes/Ice
        if (grid.HasObjectOfType<Box>(pos)) return true;
        if (grid.HasObjectOfType<IceCube>(pos)) return true;

        return false;
    }
    
    
    public void KillSnake(Snake snake)
    {
        snake.RemoveFromGame();
        snakesOnLevel.Remove(snake);
        UpdateAllPlateStates();
        
        // Optional: Trigger Game Over or Restart logic here
        // For prototype, just removing it is fine.
        Debug.Log("Snake Destroyed.");
    }

    // Define new Events at top of Class
    public event Action<LiftGateData, bool> OnLiftGateStateChanged;
    public event Action<LaserGateData, bool> OnLaserGateStateChanged;
}