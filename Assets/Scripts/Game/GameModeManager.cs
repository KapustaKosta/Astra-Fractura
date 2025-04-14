using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }
    
    public enum GameMode
    {
        Default,
        Building,
        Combat
    }

    private GameMode currentMode = GameMode.Default;

    public void SetBuildingMode(Item buildingItem)
    {
        currentMode = GameMode.Building;
        Debug.Log($"Режим строительства: {buildingItem.itemName}");
        // Здесь можно добавить логику активации строительной сетки
    }

    public void ReturnToDefaultMode()
    {
        currentMode = GameMode.Default;
    }
}