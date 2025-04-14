using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    [System.Serializable]
    public class ResourceDrop
    {
        public Item item; // Какой предмет выпадает
        public int minAmount = 1;
        public int maxAmount = 1;
    }

    public ResourceDrop[] drops; // Что может выпасть
    public float health = 30f;
    public Item requiredTool; // Какой инструмент нужен (null = можно руками)
    public float toolEfficiency = 2f; // Множитель урона от правильного инструмента

    public ResourceType resourceType; // Какие ресурсы содержит узел

    public void TakeDamage(float damage, Item toolUsed)
    {
        // Проверяем, есть ли у инструмента флаги для добычи этого ресурса
        if (requiredTool != null && (toolUsed == null || !toolUsed.canHarvest.HasFlag(resourceType)))
        {
            Debug.Log($"Нужен инструмент для: {resourceType}");
            return;
        }

        // Увеличиваем урон при использовании правильного инструмента
        if (toolUsed == requiredTool)
            damage *= toolEfficiency;

        health -= damage;

        if (health <= 0)
            Harvest();
    }

    private void Harvest()
    {
        // Выдаём предметы из drops
        Inventory inventory = FindObjectOfType<Inventory>();
        foreach (var drop in drops)
        {
            int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
            for (int i = 0; i < amount; i++)
            {
                inventory.Add(drop.item);
            }
        }

        Destroy(gameObject); // Уничтожаем ресурс
    }
}