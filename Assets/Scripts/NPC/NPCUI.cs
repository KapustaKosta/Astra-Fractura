using UnityEngine;
using TMPro;
using Unity.Entities;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Unity.Transforms;

public class NPCUI : MonoBehaviour
{
    public static NPCUI Instance { get; private set; } // Реализация Singleton  

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI npcText; // Текст для отображения информации о NPC  
    [SerializeField] private GameObject npcMenu; // Панель меню NPC
    [SerializeField] private Button closeButton; // Кнопка закрытия меню
    [SerializeField] private Button hireButton; // Кнопка "Нанять" в меню
    [SerializeField] private Transform resourceNodeListContainer; // Контейнер для списка ResourceNode
    [SerializeField] private GameObject resourceNodeButtonPrefab; // Префаб кнопки для ResourceNode

    private Entity currentNPCEntity;
    private NPCComponent currentNPC;

    private EntityManager entityManager; // Для работы с EntityManager  
    private SettlementComponent settlement; // Для хранения ссылки на поселение  

    private void Awake()
    {
        // Убедимся, что существует только один экземпляр  
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        npcMenu.SetActive(false); // Скрываем меню по умолчанию

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Подписываемся на события кнопок
        closeButton.onClick.AddListener(HideMenu);
        hireButton.onClick.AddListener(HireNPC);
    }

    public void Show(NPCComponent npc, Entity npcEntity)
    {
        currentNPC = npc;
        currentNPCEntity = npcEntity;

        // Обновляем текст с информацией о NPC  
        npcText.text = $"Имя: {npc.Name}\nВозраст: {npc.Age}\nНавыки: {npc.Skills}\n" +
                       $"Организованность: {npc.Organizedness}\nПреданность: {npc.Loyalty}\nТрудолюбие: {npc.Diligence}";
        npcText.gameObject.SetActive(true); // Показываем текст  
        npcMenu.SetActive(true); // Показываем меню

        // Проверяем, нанят ли NPC
        if (IsHired(npcEntity))
        {
            Debug.Log($"NPC {npc.Name} уже нанят.");
            hireButton.gameObject.SetActive(false); // Скрываем кнопку "Нанять"
            ShowResourceNodeOptions(); // Показываем список ResourceNode
        }
        else
        {
            Debug.Log($"NPC {npc.Name} не нанят.");
            hireButton.gameObject.SetActive(true); // Показываем кнопку "Нанять"
            ClearResourceNodeOptions(); // Очищаем список ResourceNode
        }
    }

    public void Hide()
    {
        // Скрываем текст, кнопку и меню  
        npcText.text = "";
        npcText.gameObject.SetActive(false);
        npcMenu.SetActive(false);
        ClearResourceNodeOptions(); // Очищаем список ResourceNode
    }

    private void HideMenu()
    {
        // Скрываем только меню, оставляя текст и кнопку
        npcMenu.SetActive(false);
    }

    private void ShowResourceNodeOptions()
    {
        // Очищаем предыдущие кнопки
        ClearResourceNodeOptions();

        // Показываем контейнер
        ShowResourceNodeListContainer();

        // Создаём EntityQuery для поиска всех ResourceNode
        var query = entityManager.CreateEntityQuery(typeof(ResourceNode));
        var resourceNodes = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var resourceNode in resourceNodes)
        {
            // Получаем данные ResourceNode
            var resourceNodeData = entityManager.GetComponentData<ResourceNode>(resourceNode);

            // Создаём кнопку для ResourceNode
            var buttonObject = Instantiate(resourceNodeButtonPrefab, resourceNodeListContainer);
            var button = buttonObject.GetComponent<Button>();
            var buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();

            // Устанавливаем текст кнопки
            buttonText.text = $"Добывать: {resourceNodeData.resourceType}";

            // Добавляем обработчик нажатия
            button.onClick.AddListener(() => AssignNPCToResourceNode(resourceNode));
        }

        resourceNodes.Dispose();
    }

    private void ClearResourceNodeOptions()
    {
        // Скрываем контейнер
        HideResourceNodeListContainer();

        // Удаляем все дочерние объекты из контейнера
        foreach (Transform child in resourceNodeListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void AssignNPCToResourceNode(Entity resourceNode)
    {
        if (entityManager.HasComponent<NPCComponent>(currentNPCEntity))
        {
            var npcData = entityManager.GetComponentData<NPCComponent>(currentNPCEntity);
            npcData.Target = resourceNode; // Устанавливаем цель
            entityManager.SetComponentData(currentNPCEntity, npcData);

            // Получаем позицию цели
            var targetPosition = entityManager.GetComponentData<LocalTransform>(resourceNode).Position;

            // Добавляем или обновляем компонент движения
            if (entityManager.HasComponent<NPCMovementComponent>(currentNPCEntity))
            {
                var movement = entityManager.GetComponentData<NPCMovementComponent>(currentNPCEntity);
                movement.TargetPosition = targetPosition;
                movement.HasTarget = true;
                entityManager.SetComponentData(currentNPCEntity, movement);
            }
            else
            {
                entityManager.AddComponentData(currentNPCEntity, new NPCMovementComponent
                {
                    Speed = 2.0f,
                    TargetPosition = targetPosition,
                    HasTarget = true
                });
            }

            Debug.Log($"NPC {npcData.Name} отправлен работать на {resourceNode}.");
        }

        // Скрываем меню после назначения
        Hide();
        
        // Разблокируем управление игроком  
        NPCClickHandler.Instance.LockPlayerControls(false);
    }


    public void HireNPC()
    {
        if (currentNPCEntity != Entity.Null)
        {
            // Получаем текущий компонент поселения  
            var query = entityManager.CreateEntityQuery(typeof(SettlementComponent));

            if (!query.IsEmpty)
            {
                // Получаем сущность синглтона
                var singletonEntity = query.GetSingletonEntity();

                // Получаем данные компонента PlayerSettlementComponent
                settlement = entityManager.GetComponentData<SettlementComponent>(singletonEntity);

                // Добавляем NPC в список поселения  
                if (settlement.NPCs.Length < settlement.NPCs.Capacity)
                {
                    settlement.NPCs.Add(currentNPCEntity);
                    settlement.Population += 1; // Увеличиваем население поселения
                    entityManager.SetComponentData(singletonEntity, settlement);

                    Debug.Log($"NPC {currentNPC.Name} нанят!");
                }
                else
                {
                    Debug.LogWarning("Поселение не может принять больше NPC.");
                }
            }

            // Скрываем UI после найма  
            Hide();

            // Разблокируем управление игроком  
            NPCClickHandler.Instance.LockPlayerControls(false);
        }
    }

    private bool IsHired(Entity npcEntity)
    {
        // Проверяем, находится ли NPC в поселении
        var query = entityManager.CreateEntityQuery(typeof(SettlementComponent));
        if (!query.IsEmpty)
        {
            var singletonEntity = query.GetSingletonEntity();
            var settlement = entityManager.GetComponentData<SettlementComponent>(singletonEntity);

            // Проверяем вручную, содержится ли NPC в списке
            for (int i = 0; i < settlement.NPCs.Length; i++)
            {
                if (settlement.NPCs[i] == npcEntity)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void ShowResourceNodeListContainer()
    {
        if (resourceNodeListContainer != null)
        {
            resourceNodeListContainer.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("resourceNodeListContainer не назначен!");
        }
    }

    private void HideResourceNodeListContainer()
    {
        if (resourceNodeListContainer != null)
        {
            resourceNodeListContainer.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("resourceNodeListContainer не назначен!");
        }
    }
}
