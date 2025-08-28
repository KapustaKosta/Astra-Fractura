using Energy.Core;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

namespace Game.Production
{
    public class ProductionUI : MonoBehaviour
    {
        [Header("Panel & Name")]
        public GameObject panel;
        public TextMeshProUGUI buildingNameText;

        [Header("Status & Progress")]
        public TextMeshProUGUI statusText;
        public Image productionProgressBar;

        [Header("Power")]
        public TextMeshProUGUI powerText;
        public Image powerIndicatorBar;

        [Header("Recipe Details")]
        public GameObject recipeDetailsPanel;
        public TextMeshProUGUI recipeInputsText;
        public TextMeshProUGUI recipeOutputText;

        [Header("Controls")]
        public Button startButton;
        public Button stopButton;
        public TMP_InputField amountInputField;
        public TMP_Dropdown recipeDropdown;
        public Button closeButton;

        [Header("Inventory")]
        [SerializeField] private Button openInputInventoryButton;
        [SerializeField] private Button openOutputInventoryButton;

        private EntityManager _em;
        private Entity _activeBuilding;
        private bool _isInitialized;
        private bool _listenersBound;

        private List<int> _availableRecipeIDs = new List<int>();

        void Awake()
        {
            panel?.SetActive(false);
        }

        void LateUpdate()
        {
            if (!_isInitialized && !TryInitialize()) return;

            Entity uiStateEntity = GetUIStateEntity();
            if (uiStateEntity == Entity.Null) return;

            bool shouldBeOpen = _em.HasComponent<UIState>(uiStateEntity) &&
                                _em.GetComponentData<UIState>(uiStateEntity).ActiveUIType == UIType.Production;

            if (panel.activeSelf != shouldBeOpen)
            {
                if (shouldBeOpen)
                {
                    _activeBuilding = _em.GetComponentData<UIState>(uiStateEntity).ActiveUITarget;
                    Show();
                }
                else
                {
                    Hide();
                }
            }

            if (panel.activeSelf)
            {
                RefreshUI();
            }
        }

        private void Show()
        {
            if (!_em.Exists(_activeBuilding)) { Hide(); return; }
            panel.SetActive(true);
            PopulateRecipeDropdown();
            RefreshUI(); // Первоначальное обновление при открытии
        }

        private void Hide()
        {
            panel.SetActive(false);
            _activeBuilding = Entity.Null;
        }

        private Entity GetUIStateEntity()
        {
            if (!_isInitialized) return Entity.Null;
            var query = _em.CreateEntityQuery(typeof(UIState));
            return query.HasSingleton<UIState>() ? query.GetSingletonEntity() : Entity.Null;
        }

        private void OnRecipeChanged(int index)
        {
            if (index < 0 || index >= _availableRecipeIDs.Count) return;
            var recipeID = _availableRecipeIDs[index];
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new SetProductionRecipeRequest { Building = _activeBuilding, RecipeID = recipeID });
        }

        private void OnStartPressed()
        {
            if (recipeDropdown.value < 0 || recipeDropdown.value >= _availableRecipeIDs.Count) return;

            int recipeID = _availableRecipeIDs[recipeDropdown.value];
            int.TryParse(amountInputField.text, out int amount);
            if (amount <= 0) amount = 1;

            var e = _em.CreateEntity();
            _em.AddComponentData(e, new StartProductionRequest { Building = _activeBuilding, RecipeID = recipeID, Amount = amount });
        }

        private void OnStopPressed()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new StopProductionRequest { Building = _activeBuilding });
        }

        private bool TryInitialize()
        {
            if (_isInitialized) return true;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _em = world.EntityManager;
                _isInitialized = true;
                BindListenersIfNeeded();
                return true;
            }
            return false;
        }

        private void BindListenersIfNeeded()
        {
            if (_listenersBound) return;
            startButton?.onClick.AddListener(OnStartPressed);
            stopButton?.onClick.AddListener(OnStopPressed);
            closeButton?.onClick.AddListener(OnClosePressed);
            recipeDropdown?.onValueChanged.AddListener(OnRecipeChanged);
            openInputInventoryButton?.onClick.AddListener(OnOpenInputInventoryPressed);
            openOutputInventoryButton?.onClick.AddListener(OnOpenOutputInventoryPressed);
            _listenersBound = true;
        }

        private void PopulateRecipeDropdown()
        {
            if (recipeDropdown == null) return;
            recipeDropdown.ClearOptions();
            _availableRecipeIDs.Clear();


            var regQuery = _em.CreateEntityQuery(typeof(ProductionRecipeRegistryData));
            if (!regQuery.HasSingleton<ProductionRecipeRegistryData>()) return;
            var registry = regQuery.GetSingleton<ProductionRecipeRegistryData>();


            if (!registry.Blob.IsCreated) return;

            var buildingConfig = _em.GetComponentData<ProductionConfig>(_activeBuilding);
            int stationTypeID = buildingConfig.StationTypeID;

            var opts = new List<string>();
            ref var allRecipes = ref registry.Blob.Value.Recipes;

            for (int i = 0; i < allRecipes.Length; i++)
            {
                if (allRecipes[i].RequiredStationTypeID == stationTypeID)
                {
                    opts.Add(allRecipes[i].RecipeName.ToString());
                    _availableRecipeIDs.Add(allRecipes[i].RecipeID);
                }
            }

            if (opts.Count == 0)
            {
                opts.Add("Нет доступных рецептов");
                startButton.interactable = false;
            }
            else
            {
                startButton.interactable = true;
            }

            recipeDropdown.AddOptions(opts);

            var st = _em.GetComponentData<ProductionBuildingState>(_activeBuilding);
            int idx = _availableRecipeIDs.IndexOf(st.SelectedRecipeID);
            if (idx != -1)
            {
                recipeDropdown.SetValueWithoutNotify(idx);
            }
            else if (_availableRecipeIDs.Count > 0)
            {
                OnRecipeChanged(0);
            }
        }

        private void RefreshUI()
        {
            if (!_em.Exists(_activeBuilding)) return;

            var st = _em.GetComponentData<ProductionBuildingState>(_activeBuilding);
            var load = _em.GetComponentData<ConsumerLoad>(_activeBuilding);
            var usage = _em.GetComponentData<NetLinkUsage>(_activeBuilding);
            var queue = _em.GetBuffer<ProductionQueueItem>(_activeBuilding);

            buildingNameText.text = _em.GetComponentData<NetworkNode>(_activeBuilding).Name.ToString();
            powerText.text = $"Питание: {usage.InUsedKW:0.#} / {load.CurrentKW:0.#} кВт";
            powerIndicatorBar.fillAmount = (load.CurrentKW > 0.01f) ? math.saturate(usage.InUsedKW / load.CurrentKW) : 0;
            powerIndicatorBar.color = st.Status == ProductionStatus.PausedNoPower ? Color.yellow : Color.cyan;

            if (st.ActiveRecipeIndex != -1)
            {
                var regQuery = _em.CreateEntityQuery(typeof(ProductionRecipeRegistryData));
                if (!regQuery.HasSingleton<ProductionRecipeRegistryData>()) return;
                var reg = regQuery.GetSingleton<ProductionRecipeRegistryData>();

                ref var recipe = ref reg.Blob.Value.Recipes[st.ActiveRecipeIndex];
                productionProgressBar.fillAmount = recipe.BaseTime > 0 ? 1f - (st.RemainingTime / recipe.BaseTime) : 0f;
                RefreshRecipeDetails(ref recipe);
            }
            else
            {
                productionProgressBar.fillAmount = 0f;
                recipeDetailsPanel.SetActive(false);
            }

            statusText.text = GetStatusMessage(st, !queue.IsEmpty);
        }

        private void RefreshRecipeDetails(ref ProductionRecipe recipe)
        {
            recipeDetailsPanel.SetActive(true);
            recipeOutputText.text = $"Выход: {recipe.OutputAmount}x ID:{recipe.OutputItemID}";

            var inputBuffer = _em.GetBuffer<InputInventorySlot>(_activeBuilding);
            var sb = new StringBuilder("Требуется:\n");
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                ref var input = ref recipe.Inputs[i];
                int have = 0;
                foreach (var slot in inputBuffer) if (slot.ItemID == input.ItemID) have += slot.Amount;
                string color = have >= input.Amount ? "green" : "red";
                sb.AppendLine($"<color={color}>- ID:{input.ItemID}: {have}/{input.Amount}</color>");
            }
            recipeInputsText.text = sb.ToString();
        }

        private string GetStatusMessage(ProductionBuildingState st, bool hasQueue)
        {
            if (!st.IsOn) return "Статус: Выключено";
            if (!hasQueue) return "Статус: Ожидание";
            return $"Статус: {st.Status}";
        }

        private void OnClosePressed()
        { 
            GameBridge.Instance?.HandleUICloseAction(); 
        }
        private void OnOpenInputInventoryPressed() 
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new OpenInventoryUIRequest { Target = _activeBuilding, Type = InventoryType.Input }); 
        }
        private void OnOpenOutputInventoryPressed() 
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new OpenInventoryUIRequest { Target = _activeBuilding, Type = InventoryType.Output }); 
        }
    }
}