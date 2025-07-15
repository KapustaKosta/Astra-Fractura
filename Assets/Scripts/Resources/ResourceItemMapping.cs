using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject, который служит центральной базой данных для сопоставления
/// типов собираемых ресурсов (например, ResourceCollectionType.Wood) с конкретными
/// объектами-предметами (Item ScriptableObject). Использует паттерн Singleton для
/// удобного глобального доступа из любой точки кода, включая ECS системы.
/// </summary>
[CreateAssetMenu(fileName = "ResourceItemMapping", menuName = "Mappings/ResourceItemMapping")]
public class ResourceItemMapping : ScriptableObject
{
    // Приватное статическое поле для хранения единственного экземпляра класса.
    private static ResourceItemMapping _instance;
    
    /// <summary>
    /// Статическое свойство для доступа к единственному экземпляру ResourceItemMapping.
    /// При первом обращении автоматически загружает ассет из папки "Assets/Resources".
    /// </summary>
    public static ResourceItemMapping Instance
    {
        get
        {
            // Ленивая инициализация: загрузка происходит только при первом запросе.
            if (_instance == null)
            {
                // Загружаем единственный экземпляр из папки Resources.
                _instance = Resources.Load<ResourceItemMapping>("ResourceItemMapping");
                if (_instance == null)
                {
                    #if UNITY_EDITOR
                    Debug.LogError("[ResourceItemMapping] Ошибка: Не удалось найти ассет 'ResourceItemMapping'" +
                                   " в папке Assets/Resources.");
                    #endif
                }
            }
            return _instance;
        }
    }
    

    /// <summary>
    /// Вложенный класс, представляющий одну запись сопоставления "ресурс -> предмет".
    /// Используется для удобного отображения и редактирования списка в инспекторе Unity.
    /// </summary>
    [System.Serializable]
    public class ResourceItemEntry
    {
        public ResourceCollectionType resourceType;
        public Item item; // Ссылка на ScriptableObject предмета.
    }

    /// <summary>
    /// Основной список сопоставлений. Заполняется вручную в инспекторе Unity,
    /// где каждому типу ресурса назначается соответствующий ScriptableObject предмета.
    /// </summary>
    public List<ResourceItemEntry> resourceItems = new List<ResourceItemEntry>();

    // Метод для получения Item по типу ресурса
    /// <summary>
    /// Находит и возвращает объект Item, соответствующий указанному типу ресурса.
    /// Проходит по списку сопоставлений и ищет нужную запись.
    /// </summary>
    /// <param name="resourceType">Тип ресурса, для которого нужно найти предмет.</param>
    /// <returns>ScriptableObject предмета, если сопоставление найдено; в противном случае — null.</returns>
    public Item GetItemByResourceType(ResourceCollectionType resourceType)
    {
        foreach (var entry in resourceItems)
        {
            if (entry.resourceType == resourceType)
            {
                return entry.item;
            }
        }
        #if UNITY_EDITOR
        Debug.LogWarning($"Item для ресурса типа {resourceType} не найден в ResourceItemMapping.");
        #endif
        return null;
    }
}
