using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Entities;
using Conveyor;

public class RouteListItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI routeNameText;
    [SerializeField] private Image currentItemIcon;
    [SerializeField] private Button selectItemButton;
    [SerializeField] private Button toggleActivityButton;
    [SerializeField] private TextMeshProUGUI statusText;

    private Entity routeEntity;
    private EntityManager entityManager;

    public void Initialize(EntityManager em, Entity entity, string name, Item itemData, bool isActive)
    {
        this.entityManager = em;
        this.routeEntity = entity;
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

        UpdateStatus(isActive);
    }

    public void UpdateStatus(bool isActive)
    {
        var buttonText = toggleActivityButton.GetComponentInChildren<TextMeshProUGUI>();
        if (isActive)
        {
            statusText.text = "<color=green>Активен</color>";
            if (buttonText != null) buttonText.text = "Пауза";
        }
        else
        {
            statusText.text = "<color=orange>На паузе</color>";
            if (buttonText != null) buttonText.text = "Старт";
        }
    }

    private void OnSelectButtonPressed()
    {
        ConveyorRoutesUI.Instance.RequestSourceInventoryForRoute(routeEntity);
    }

    private void OnToggleButtonPressed()
    {
        var requestEntity = entityManager.CreateEntity();
        entityManager.AddComponentData(requestEntity, new ToggleRouteRequest { RouteEntity = this.routeEntity });

        bool currentState = entityManager.HasComponent<ActiveRouteTag>(this.routeEntity);
        UpdateStatus(!currentState);
    }
}