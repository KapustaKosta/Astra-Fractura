using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, служащий реестром для всех игровых предметов.
/// Реализует паттерн Singleton для легкого доступа к данным предметов из любой точки кода.
/// </summary>
[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Inventory/Item Registry")]
public class ItemRegistry : ScriptableObject
{
    private static ItemRegistry _instance;
    
    /// <summary>
    /// Статический экземпляр реестра.
    /// При первом обращении автоматически загружает себя из папки Resources.
    /// </summary>
    public static ItemRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ItemRegistry>("ItemRegistry");
                if (_instance == null)
                {
                    #if UNITY_EDITOR
                    Debug.LogError("[ItemRegistry] Ошибка: Не удалось найти ассет 'ItemRegistry' " +
                                   "в папке Assets/Resources.");
                    return null;
                    #endif
                }
            }
            // Вызываем Initialize() при каждом доступе, чтобы быть устойчивым к Domain Reload в редакторе.
            _instance.Initialize();
            return _instance;
        }
    }

    [Tooltip("Список всех ScriptableObject'ов предметов, используемых в игре.")]
    public List<Item> allItems;
    
    // Внутренний словарь для быстрого поиска предмета по ID.
    private Dictionary<int, Item> itemDict;
    private bool isInitialized = false;

    /// <summary>
    /// Инициализирует реестр, создавая словарь для быстрого доступа к предметам.
    /// Защищен от повторного выполнения.
    /// </summary>
    private void Initialize()
    {
        // Проверяем сам словарь
        if (itemDict != null)
        {
            return;
        }

        itemDict = new Dictionary<int, Item>();
        foreach(var item in allItems)
        {
            if(item == null) continue;

            if (item.itemID == 0)
            {
                #if UNITY_EDITOR
                Debug.LogError($"[ItemRegistry] Ошибка: У предмета '{item.name}' ItemID равный 0.");
                #endif
                continue;
            }
            if(!itemDict.ContainsKey(item.itemID))
            {
                itemDict.Add(item.itemID, item);
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogWarning($"[ItemRegistry]: Обнаружен дубликат ItemID: {item.itemID} у предмета '{item.name}'.");
                #endif
            }
        }
        
        isInitialized = true;
    }

    /// <summary>
    /// Возвращает ScriptableObject предмета по его уникальному ID.
    /// </summary>
    /// <param name="itemID">ID искомого предмета.</param>
    /// <returns>Экземпляр Item или null, если предмет не найден.</returns>
    public Item GetItemData(int itemID)
    {
        if (itemDict == null)
        {
            #if UNITY_EDITOR
            Debug.LogError($"[GetItemData] Попытка получить данные о предмете," +
                           $" но словарь (itemDict) не был инициализирован. ItemID: {itemID}");
            #endif
            return null;
        }

        itemDict.TryGetValue(itemID, out Item item);
        return item;
    }
}