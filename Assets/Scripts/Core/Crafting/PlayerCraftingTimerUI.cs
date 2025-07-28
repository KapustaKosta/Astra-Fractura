using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Управляет UI-контейнером, который отображает очередь крафта игрока.
/// Этот компонент является "мостом" к миру ECS: он только читает данные о состоянии очереди
/// и синхронизирует с ними визуальное представление, не содержа собственной игровой логики.
/// </summary>
public class PlayerCraftingTimerUI : MonoBehaviour
{
    [SerializeField] private Transform queueContainer;
    [SerializeField] private GameObject queueItemPrefab;
    [SerializeField] private GameObject scrollViewObject;
    
    private EntityManager entityManager;
    private Entity playerEntity;
    private bool isInitialized = false;

    /// <summary>
    /// Локальный кэш созданных UI-элементов.
    /// Используется для оптимизации: вместо того чтобы каждый кадр уничтожать и создавать все
    /// элементы заново, мы добавляем/удаляем только недостающие/лишние.
    /// </summary>
    private readonly List<CraftingQueueItemUI> activeQueueSlots = new List<CraftingQueueItemUI>();

    /// <summary>
    /// Основной цикл, который постоянно опрашивает состояние очереди крафта в мире ECS
    /// и инициирует обновление UI, если это необходимо.
    /// </summary>
    void Update()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }

        // Выполняем проверку на существование сущности игрока на случай, если он будет удален из мира.
        if (!entityManager.Exists(playerEntity))
        {
            isInitialized = false;
            if (queueContainer != null) queueContainer.gameObject.SetActive(false);
            return;
        }

        // Проверяем, есть ли у игрока в принципе очередь крафта (т.е. присоединен ли к нему буфер).
        bool hasQueue = entityManager.HasBuffer<CraftingQueueElement>(playerEntity);

        if (hasQueue)
        {
            var queueBuffer = entityManager.GetBuffer<CraftingQueueElement>(playerEntity);
            // Управляем видимостью всего ScrollView
            if (scrollViewObject != null)
            {
                scrollViewObject.SetActive(!queueBuffer.IsEmpty);
            }
            UpdateQueueDisplay(queueBuffer);
        }
        else
        {
            // Гарантируем, что ScrollView скрыт
            if (scrollViewObject != null)
            {
                scrollViewObject.SetActive(false);
            }
            UpdateQueueDisplay(default);
        }
    }

    /// <summary>
    /// Выполняет отложенную инициализацию, получая ссылки на EntityManager и сущность игрока.
    /// Этот подход необходим в гибридной архитектуре, так как ECS-мир может быть не готов в момент вызова Start().
    /// </summary>
    private void TryInitialize()
    {
        if (isInitialized) return;
        
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerControllerData));
            if (!playerQuery.IsEmpty)
            {
                playerEntity = playerQuery.GetSingletonEntity();
                isInitialized = true;
            }
        }
    }

    /// <summary>
    /// Синхронизирует количество и содержимое UI-элементов с состоянием очереди крафта в ECS.
    /// </summary>
    private void UpdateQueueDisplay(DynamicBuffer<CraftingQueueElement> queueBuffer)
    {
        int queueCount = queueBuffer.IsCreated ? queueBuffer.Length : 0;

        // Этот цикл добавляет недостающие UI-элементы, если очередь крафта в ECS выросла.
        while (activeQueueSlots.Count < queueCount)
        {
            var slotGO = Instantiate(queueItemPrefab, queueContainer);
            activeQueueSlots.Add(slotGO.GetComponent<CraftingQueueItemUI>());
        }
        
        // Этот цикл удаляет лишние UI-элементы, если очередь крафта в ECS сократилась.
        while (activeQueueSlots.Count > queueCount)
        {
            var slotToRemove = activeQueueSlots[activeQueueSlots.Count - 1];
            activeQueueSlots.RemoveAt(activeQueueSlots.Count - 1);
            Destroy(slotToRemove.gameObject);
        }

        // Обновляем данные в каждом UI-слоте, который теперь гарантированно существует.
        for (int i = 0; i < queueCount; i++)
        {
            // Передаем флаг, является ли этот элемент первым в очереди (i == 0),
            // чтобы дочерний UI-компонент мог отобразить либо таймер, либо статус "В очереди".
            activeQueueSlots[i].UpdateData(queueBuffer[i], i == 0);
        }
    }
}