using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Entities;
using Conveyor;
using Energy.Core;

/// <summary>
/// Управляет одним элементом (одной строкой) в списке маршрутов.
/// Отвечает за отображение всей актуальной информации о конкретном маршруте
/// (статус, энергопотребление) и обработку пользовательских действий (старт/пауза, выбор предмета).
/// </summary>
public class RouteListItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI routeNameText;
    [SerializeField] private Image currentItemIcon;
    [SerializeField] private Button selectItemButton;
    [SerializeField] private Button toggleActivityButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI energyUsageText;

    private Entity routeEntity;
    private EntityManager entityManager;
    private bool isInitialized = false;

    /// <summary>
    /// Инициализирует элемент UI данными о конкретном маршруте.
    /// Вызывается из `ConveyorRoutesUI` при создании этого объекта.
    /// </summary>
    /// <param name="em">Ссылка на EntityManager.</param>
    /// <param name="entity">Сущность, представляющая данный маршрут.</param>
    /// <param name="name">Отображаемое имя маршрута.</param>
    /// <param name="itemData">Данные о предмете, назначенном на маршрут (может быть null).</param>
    public void Initialize(EntityManager em, Entity entity, string name, Item itemData)
    {
        this.entityManager = em;
        this.routeEntity = entity;
        this.isInitialized = true;
        
        routeNameText.text = name;

        if (itemData != null && itemData.icon != null)
        {
            currentItemIcon.sprite = itemData.icon;
            currentItemIcon.enabled = true;
        }
        else
        {
            currentItemIcon.sprite = null;
            currentItemIcon.enabled = false;
        }

        selectItemButton.onClick.RemoveAllListeners();
        selectItemButton.onClick.AddListener(OnSelectButtonPressed);

        toggleActivityButton.onClick.RemoveAllListeners();
        toggleActivityButton.onClick.AddListener(OnToggleButtonPressed);
    }
    
    /// <summary>
    /// Вызывается каждый кадр. Обеспечивает постоянную синхронизацию UI с состоянием
    /// сущности-маршрута в мире ECS.
    /// </summary>
    void Update()
    {
        if (!isInitialized || !entityManager.Exists(routeEntity))
        {
            // Если сущность маршрута была удалена (например, при сносе конвейера),
            // деактивируем этот элемент UI.
            gameObject.SetActive(false);
            return;
        }
        
        // Обновляем динамические части UI каждый кадр.
        UpdateStatusAndEnergy();
    }

    /// <summary>
    /// Считывает актуальные данные из компонентов сущности-маршрута и обновляет
    /// текстовые поля статуса, энергопотребления и кнопки управления.
    /// </summary>
    public void UpdateStatusAndEnergy()
    {
        var buttonText = toggleActivityButton.GetComponentInChildren<TextMeshProUGUI>();
        
        // Определяем, чего хочет пользователь (включил ли он маршрут).
        bool userWantsActive = entityManager.HasComponent<UserEnabledRouteTag>(this.routeEntity);
        // Определяем, работает ли маршрут по факту (прошел ли все проверки).
        bool isActuallyActive = entityManager.HasComponent<ActiveRouteTag>(this.routeEntity);

        if (buttonText != null)
        {
            buttonText.text = userWantsActive ? "Пауза" : "Старт";
        }

        // Отображаем энергопотребление.
        if (entityManager.HasComponent<ConveyorEnergyDemand>(this.routeEntity))
        {
            var demand = entityManager.GetComponentData<ConveyorEnergyDemand>(this.routeEntity);
            energyUsageText.text = $"{demand.RequiredKW:F2} кВт";
            energyUsageText.gameObject.SetActive(true);
        }
        else
        {
            energyUsageText.gameObject.SetActive(false);
        }
        
        // Если пользователь не включал маршрут, он всегда на паузе.
        if (!userWantsActive)
        {
            statusText.text = "<color=orange>На паузе</color>";
            return;
        }

        // Если пользователь включил, но маршрут не работает по факту (из-за нехватки энергии/сети).
        if (!isActuallyActive)
        {
            if (!entityManager.HasComponent<NetworkNode>(this.routeEntity) || 
                entityManager.GetComponentData<NetworkNode>(this.routeEntity).SubnetId == 0)
            {
                statusText.text = "<color=red>Нет сети</color>";
                return;
            }
            statusText.text = "<color=red>Нет энергии</color>";
            return;
        }

        // Если маршрут активен, показываем детальный статус энергии.
        if (entityManager.HasComponent<RoutePowerStatus>(this.routeEntity))
        {
            var powerStatus = entityManager.GetComponentData<RoutePowerStatus>(this.routeEntity);
            if (powerStatus.PowerRatio < 0.99f)
            {
                statusText.text = $"<color=yellow>Нехватка энергии ({(powerStatus.PowerRatio * 100):F0}%)</color>";
            }
            else
            {
                statusText.text = "<color=green>Активен</color>";
            }
        }
        else
        {
            statusText.text = "<color=orange>Статус неизвестен</color>";
        }
    }

    /// <summary>
    /// Обработчик нажатия на кнопку выбора предмета. Передает запрос в главный UI.
    /// </summary>
    private void OnSelectButtonPressed()
    {
        ConveyorRoutesUI.Instance.RequestSourceInventoryForRoute(routeEntity);
    }

    /// <summary>
    /// Обработчик нажатия на кнопку "Старт/Пауза". Создает ECS-сущность-запрос
    /// для изменения пользовательского состояния маршрута.
    /// </summary>
    private void OnToggleButtonPressed()
    {
        if (!entityManager.Exists(routeEntity)) return;
        
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new ToggleRouteRequest { RouteEntity = this.routeEntity });
    }
}