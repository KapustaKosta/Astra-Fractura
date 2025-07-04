using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceItemMapping", menuName = "Mappings/ResourceItemMapping")]
public class ResourceItemMapping : ScriptableObject
{
    [System.Serializable]
    public class ResourceItemEntry
    {
        public ResourceCollectionType resourceType; // Тип ресурса
        public Item item; // Соответствующий Item
    }

    public List<ResourceItemEntry> resourceItems = new List<ResourceItemEntry>();

    // Метод для получения Item по типу ресурса
    public Item GetItemByResourceType(ResourceCollectionType resourceType)
    {
        foreach (var entry in resourceItems)
        {
            if (entry.resourceType == resourceType)
            {
                return entry.item;
            }
        }
        Debug.LogWarning($"Item для ресурса типа {resourceType} не найден.");
        return null;
    }
}
