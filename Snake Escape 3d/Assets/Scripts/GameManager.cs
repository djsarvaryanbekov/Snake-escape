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
    private Dictionary<PlateColor, List<LaserGate>> gatesByColor = new Dictionary<PlateColor, List<LaserGate>>();
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

    public void RegisterGate(LaserGate gate, LaserGateData data)
    {
        if (!gatesByColor.ContainsKey(data.color)) gatesByColor[data.color] = new List<LaserGate>();
        gatesByColor[data.color]. Add(gate);
    }

    private void HandlePlateTrigger(PressurePlate plate, bool active)
    {
        PlateColor color = plate.GetData().color;
        OnPlateStateChanged?.Invoke(plate. GetData(), active);
        CheckGateSystem(color);
    }

    /// <summary>
    /// SIMPLIFIED GATE LOGIC: 
    /// - If ALL plates are pressed → OPEN
    /// - Otherwise → CLOSED
    /// - NO blocking checks - open gates act like empty cells regardless! 
    /// </summary>
    private void CheckGateSystem(PlateColor color)
    {
        if (!platesByColor.ContainsKey(color)) return;
        
        // Check if ALL plates of this color are pressed
        bool allPlatesActive = platesByColor[color].All(p => p.IsActive);

        if (gatesByColor.ContainsKey(color))
        {
            foreach (var gate in gatesByColor[color])
            {
                Vector2Int gatePos = gate.GetData().position;

                if (allPlatesActive)
                {
                    // ✅ ALL plates pressed → OPEN (doesn't matter what's on it!)
                    gate.Open();
                    OnGateStateChanged?.Invoke(gate.GetData(), true);
                    Debug.Log($"Gate at {gatePos} OPENED (all plates active)");
                }
                else
                {
                    // ❌ NOT all plates → CLOSE
                    gate.Close();
                    OnGateStateChanged?.Invoke(gate.GetData(), false);
                    Debug.Log($"Gate at {gatePos} CLOSED (plates not all pressed)");
                }
            }
        }
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
        gatesByColor.Clear();
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
}