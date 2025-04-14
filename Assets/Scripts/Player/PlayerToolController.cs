using UnityEngine;

public class PlayerToolController : MonoBehaviour
{
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1f;
    private float lastAttackTime;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Time.time > lastAttackTime + attackCooldown)
        {
            TryHarvest();
            lastAttackTime = Time.time;
            Debug.Log(1);
        }
    }

    private void TryHarvest()
    {
        // Для проверки инструмента используем:
        Item currentTool = Inventory.Instance.GetEquippedTool();
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, attackRange))
        {
            ResourceNode resource = hit.collider.GetComponent<ResourceNode>();
            if (resource != null)
            {
                // Получаем текущий инструмент из инвентаря
                Inventory inventory = Inventory.Instance;
                Item equippedTool = inventory.GetEquippedTool(); // Этот метод нужно реализовать
                
                resource.TakeDamage(attackDamage, equippedTool);
            }
        }
    }
}