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

    private void Awake()
    {
        // Проверяем, что Singleton корректно инициализирован
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameModeManager уже существует! Уничтожаем дублирующий объект.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Сохраняем объект между сценами
    }

    public void SetBuildingMode(Item buildingItem)
    {
        if (buildingItem == null || buildingItem.buildingPrefab == null)
        {
            Debug.LogError("Передан некорректный Item в SetBuildingMode!");
            return;
        }

        // Устанавливаем режим строительства
        currentMode = GameMode.Building;
        Debug.Log($"Режим строительства активирован для: {buildingItem.itemName}");

        // Запускаем процесс строительства через BuildingManager
        BuildingManager.Instance.StartBuildingMode(buildingItem);
    }

    public void ReturnToDefaultMode()
    {
        // Завершаем процесс строительства, если он активен
        if (currentMode == GameMode.Building)
        {
            BuildingManager.Instance.CancelBuilding();
        }

        currentMode = GameMode.Default;
        Debug.Log("Возврат в режим по умолчанию.");
    }

    private void Update()
    {
        // Выход из режима строительства по нажатию клавиши Escape
        if (currentMode == GameMode.Building && Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToDefaultMode();
        }
    }
}
