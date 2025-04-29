using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceItemMapping", menuName = "Mappings/ResourceItemMapping")]
public class ResourceItemMapping : ScriptableObject
{
    [System.Serializable]
    public class ResourceItemEntry
    {
        public ResourceCollectionType resourceType; // ��� �������
        public Item item; // ��������������� Item
    }

    public List<ResourceItemEntry> resourceItems = new List<ResourceItemEntry>();

    // ����� ��� ��������� Item �� ���� �������
    public Item GetItemByResourceType(ResourceCollectionType resourceType)
    {
        foreach (var entry in resourceItems)
        {
            if (entry.resourceType == resourceType)
            {
                return entry.item;
            }
        }
        Debug.LogWarning($"Item ��� ������� ���� {resourceType} �� ������.");
        return null;
    }
}
