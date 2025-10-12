using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the player's health bar UI element.
/// This script should be attached to a GameObject in the scene that has access to the UI.
/// </summary>
public class PlayerUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The slider that represents the player's health.")]
    [SerializeField] private Slider playerHealthSlider;

    private EntityManager entityManager;
    private EntityQuery playerQuery;
    private EntityQuery gameStateQuery; 
    private bool isInitialized = false;

    void Start()
    {
        if (playerHealthSlider == null)
        {
            enabled = false;
            return;
        }

        playerHealthSlider.gameObject.SetActive(false);
        TryInitialize();
    }

    private void TryInitialize()
    {
        if (isInitialized) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            entityManager = world.EntityManager;
            playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag), typeof(HealthComponent));
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState)); 
            isInitialized = true;
        }
    }
    
    void LateUpdate()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }
        
        bool isUiModeActive = false;
        if (!gameStateQuery.IsEmpty)
        {
            var gameStateEntity = gameStateQuery.GetSingletonEntity();
            if (entityManager.HasComponent<InUIMode>(gameStateEntity))
            {
                isUiModeActive = true;
            }
        }

        // Если активен любой UI, принудительно скрываем HP бар и выходим
        if (isUiModeActive)
        {
            if (playerHealthSlider.gameObject.activeSelf)
            {
                playerHealthSlider.gameObject.SetActive(false);
            }
            return;
        }


        // Старая логика отображения HP бара (работает, только если мы не в режиме UI)
        if (!playerQuery.IsEmpty)
        {
            Entity playerEntity = playerQuery.GetSingletonEntity();
            
            if (entityManager.Exists(playerEntity))
            {
                if (!playerHealthSlider.gameObject.activeSelf)
                {
                    playerHealthSlider.gameObject.SetActive(true);
                }

                var health = entityManager.GetComponentData<HealthComponent>(playerEntity);
                
                float targetHealthValue = health.MaxHealth > 0 ? health.CurrentHealth / health.MaxHealth : 0;
                playerHealthSlider.value = targetHealthValue;
            }
            else
            {
                if (playerHealthSlider.gameObject.activeSelf)
                {
                    playerHealthSlider.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            if (playerHealthSlider.gameObject.activeSelf)
            {
                playerHealthSlider.gameObject.SetActive(false);
            }
        }
    }
}