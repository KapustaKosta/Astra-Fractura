using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Класс-контроллер для UI-элементов, связанных с боем (полоса здоровья, имя цели).
/// Этот скрипт висит на объекте в сцене и работает как мост между миром ECS и миром Unity UI.
/// </summary>
public class CombatUIController : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private GameObject combatUIPanel;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image healthFillImage; 

    [Header("Настройки")]
    [SerializeField] private float healthDrainSpeed = 1.0f; // Скорость для плавного убывания здоровья на UI
    [SerializeField] private Color enemyHealthColor = Color.red;
    [SerializeField] private Color friendlyHealthColor = Color.green;

    private EntityManager entityManager;
    private bool isInitialized = false;
    private Entity currentNpcTarget; // Храним текущую цель, чтобы не обновлять статичные данные (имя) на каждом кадре
    private float displayedHealthValue; // Нормализованное значение здоровья, которое плавно меняется для визуального эффекта
    private EntityQuery activeTargetQuery; // Кэшированный запрос к ECS для производительности

    void Start()
    {
        if (combatUIPanel == null || healthSlider == null || nameText == null || healthFillImage == null)
        {
            enabled = false; // Выключаем скрипт, если не все UI элементы назначены в инспекторе.
            return;
        }
        combatUIPanel.SetActive(false);
        TryInitialize();
    }

    /// <summary>
    /// Пытается получить доступ к миру ECS. Это нужно на случай, если скрипт
    /// инициализируется раньше, чем создается сам ECS мир.
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized) return;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            entityManager = world.EntityManager;
            // Создаем и кэшируем запрос, который будет искать синглтон ActiveCombatTarget.
            activeTargetQuery = entityManager.CreateEntityQuery(typeof(ActiveCombatTarget));
            isInitialized = true;
        }
    }

    // Используем LateUpdate, чтобы все игровые вычисления в ECS (включая урон)
    // за текущий кадр уже были завершены, и мы работали с актуальными данными.
    void LateUpdate()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }
        
        // Проверяем, существует ли синглтон ActiveCombatTarget. Его наличие означает, что UI должен быть виден.
        if (!activeTargetQuery.IsEmpty)
        {
            var activeTarget = activeTargetQuery.GetSingleton<ActiveCombatTarget>();
            var targetEntity = activeTarget.TargetEntity;
            
            // Проверка на валидность цели. Если сущность уничтожена или мертва, скрываем UI.
            if (!entityManager.Exists(targetEntity) || 
                entityManager.HasComponent<IsDeadTag>(targetEntity))
            {
                if (combatUIPanel.activeSelf) combatUIPanel.SetActive(false);
                currentNpcTarget = Entity.Null;
                return;
            }

            // Дополнительная проверка на наличие HealthComponent перед его использованием.
            if (!entityManager.HasComponent<HealthComponent>(targetEntity))
            {
                if (combatUIPanel.activeSelf) combatUIPanel.SetActive(false);
                currentNpcTarget = Entity.Null;
                return;
            }

            var health = entityManager.GetComponentData<HealthComponent>(targetEntity);
            
            if (!combatUIPanel.activeSelf)
            {
                combatUIPanel.SetActive(true);
            }
            
            // Если мы переключились на новую цель, обновляем статичные данные: имя и цвет здоровья.
            if (currentNpcTarget != targetEntity)
            {
                currentNpcTarget = targetEntity;
                var npcData = entityManager.GetComponentData<NPCComponent>(targetEntity);
                nameText.text = npcData.Name.ToString();
                
                // Проверяем, нанят ли NPC, чтобы определить цвет полосы здоровья.
                bool isHired = entityManager.HasComponent<NPCHiredTag>(targetEntity);
                healthFillImage.color = isHired ? friendlyHealthColor : enemyHealthColor;

                // При смене цели сбрасываем отображаемое здоровье на актуальное, без анимации.
                displayedHealthValue = health.MaxHealth > 0 ? health.CurrentHealth / health.MaxHealth : 0;
            }
            
            // Вычисляем целевое значение здоровья (от 0 до 1).
            float targetHealthValue = health.MaxHealth > 0 ? health.CurrentHealth / health.MaxHealth : 0;

            // Если отображаемое здоровье больше реального (цель получила урон), плавно уменьшаем его.
            if (displayedHealthValue > targetHealthValue)
            {
                displayedHealthValue = Mathf.MoveTowards(displayedHealthValue,
                    targetHealthValue, healthDrainSpeed * Time.deltaTime);
            }
            // Если здоровье увеличилось (например, лечение), обновляем его мгновенно.
            else
            {
                displayedHealthValue = targetHealthValue;
            }
            
            // Применяем вычисленное значение к слайдеру.
            healthSlider.value = displayedHealthValue;
        }
        else // Если синглтон ActiveCombatTarget не найден, значит, UI должен быть скрыт.
        {
            if (combatUIPanel.activeSelf)
            {
                combatUIPanel.SetActive(false);
                currentNpcTarget = Entity.Null; // Сбрасываем текущую цель.
            }
        }
    }
}