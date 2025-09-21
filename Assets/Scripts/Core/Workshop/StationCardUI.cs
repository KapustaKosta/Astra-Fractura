using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Game.Production;
using System.Text;
using Unity.Mathematics;
using Energy.Core;
using Unity.Collections;
using UnityEngine.EventSystems;

namespace Game.Workshop
{
    public class StationCardUI : MonoBehaviour
    {
        [Header("Groups")]
        public GameObject configuredStateGroup;
        public GameObject emptyStateGroup;

        [Header("Configured State")]
        public TextMeshProUGUI titleText;
        public TMP_Dropdown recipeDropdown;
        public Button removeButton;
        public TextMeshProUGUI statusText;
        public Image statusIndicatorImage;
        public Slider progressSlider;
        public TextMeshProUGUI requirementsText;
        public TextMeshProUGUI productionInfoText;
        public Toggle enabledToggle;

        [Header("Empty State")]
        public TMP_Dropdown stationTypeDropdown;
        public Button confirmInstallButton;
        public TextMeshProUGUI emptySlotTitle;


        [Header("Dependencies")]
        [Tooltip("Ссылка на ScriptableObject реестра для получения полных данных о рецептах.")]
        public ProductionRecipeRegistrySO recipeRegistry;


        [Header("Debug")]
        public bool EnableDebug = true;
        public TextMeshProUGUI debugOverlay;
        public int ecsConfirmTimeoutFrames = 60;
        public int enabledConfirmTimeoutFrames = 60;

        private EntityManager _em;
        private Entity _workshop;
        private int _slotIndex;
        private Entity _station;
        private List<StationType> _availableStationTypes;
        private StationType _currentStationType;

        private bool _isListenerBound = false;
        private int _lastStationTypeId = int.MinValue;
        private int _lastRecipesHash = 0;
        private int? _pendingRecipeId = null;
        private int _pendingFramesLeft = 0;

        private bool _emptyListenerBound = false;
        private int _emptySelectedIndex = 0;
        private int _emptyOptionsHash = 0;

        private readonly Queue<string> _dbg = new Queue<string>(32);
        private int _instanceId;

        private readonly Color COLOR_OK = new Color(0.4f, 1.0f, 0.5f);
        private readonly Color COLOR_IDLE = new Color(1.0f, 0.9f, 0.4f);
        private readonly Color COLOR_WAITING = new Color(0.9f, 0.6f, 0.2f);
        private readonly Color COLOR_ERROR = new Color(1.0f, 0.4f, 0.4f);
        private readonly Color COLOR_DISABLED = Color.grey;

        private bool? _pendingEnabled = null;
        private int _pendingEnabledFramesLeft = 0;
        private WorkshopUI _root;
        private bool _hoverForwardersReady = false;


        private readonly List<ProductionRecipeSO> _availableRecipesForStation = new List<ProductionRecipeSO>();


        public void Init(WorkshopUI root)
        {
            _root = root;
            recipeRegistry = root.recipeRegistry;
        }

        private void Awake()
        {
            _instanceId = gameObject.GetInstanceID();
        }

        private void Update()
        {
            if (_pendingRecipeId.HasValue && _pendingFramesLeft > 0)
            {
                _pendingFramesLeft--;
                if (_pendingFramesLeft == 0)
                    DbgWarn($"ECS не подтвердил выбор recipeId={_pendingRecipeId} за timeout={ecsConfirmTimeoutFrames} кадров.");
            }

            if (_pendingEnabled.HasValue && _pendingEnabledFramesLeft > 0)
            {
                _pendingEnabledFramesLeft--;
                if (_pendingEnabledFramesLeft == 0)
                {
                    DbgWarn("ECS не подтвердил включение/выключение станции за таймаут.");
                    _pendingEnabled = null;
                }
            }

            if (debugOverlay && EnableDebug)
                debugOverlay.text = string.Join("\n", _dbg);
        }

        public void Bind(Entity workshop, int slotIndex, Entity station, EntityManager em, List<StationType> availableTypes, bool isChainActive)
        {
            _workshop = workshop;
            _slotIndex = slotIndex;
            _station = station;
            _em = em;
            _availableStationTypes = availableTypes;

            if (!_em.Exists(_station)) { return; }

            var stationConfig = _em.GetComponentData<StationConfig>(station);
            bool isEmpty = stationConfig.StationTypeID == -1;

            configuredStateGroup.SetActive(!isEmpty);
            emptyStateGroup.SetActive(isEmpty);

            if (isEmpty)
            {
                BindEmptyState();
            }
            else
            {
                BindConfiguredState(isChainActive);
            }

            EnsureHoverForwarders();
        }


        private ProductionRecipeSO GetRecipeById(int recipeId)
        {
            if (recipeRegistry == null) return null;
            return recipeRegistry.Recipes.FirstOrDefault(r => r.RecipeID == recipeId);
        }


        private void BindEmptyState()
        {
            emptySlotTitle.text = $"Слот #{_slotIndex + 1}";
            var names = (_availableStationTypes != null ? _availableStationTypes.Select(t => t.StationName).ToList()
                                                        : new List<string>());

            unchecked
            {
                int h = 17;
                h = h * 31 + names.Count;
                for (int i = 0; i < names.Count; i++) h = h * 31 + (names[i]?.GetHashCode() ?? 0);

                if (h != _emptyOptionsHash)
                {
                    _emptyOptionsHash = h;
                    stationTypeDropdown.ClearOptions();
                    if (names.Count == 0)
                    {
                        stationTypeDropdown.AddOptions(new List<string> { "Нет типов станций" });
                        confirmInstallButton.interactable = false;
                    }
                    else
                    {
                        stationTypeDropdown.AddOptions(names);
                        confirmInstallButton.interactable = true;
                    }

                    _emptySelectedIndex = Mathf.Clamp(_emptySelectedIndex, 0, Mathf.Max(0, stationTypeDropdown.options.Count - 1));
                    stationTypeDropdown.SetValueWithoutNotify(_emptySelectedIndex);
                    stationTypeDropdown.RefreshShownValue();
                }
            }

            if (!_emptyListenerBound)
            {
                _emptyListenerBound = true;
                stationTypeDropdown.onValueChanged.AddListener(OnStationTypeChanged);
            }

            confirmInstallButton.onClick.RemoveAllListeners();
            confirmInstallButton.onClick.AddListener(() =>
            {
                if (_availableStationTypes == null || _availableStationTypes.Count == 0) return;
                int idx = Mathf.Clamp(_emptySelectedIndex, 0, _availableStationTypes.Count - 1);
                var selectedType = _availableStationTypes[idx];

                var e = _em.CreateEntity();
                _em.AddComponentData(e, new InstallStationTypeRequest
                {
                    Workshop = _workshop,
                    SlotIndex = _slotIndex,
                    StationTypeID = selectedType.StationTypeID
                });
            });
        }

        private void OnStationTypeChanged(int idx)
        {
            _emptySelectedIndex = Mathf.Clamp(idx, 0, stationTypeDropdown.options.Count - 1);
            stationTypeDropdown.SetValueWithoutNotify(_emptySelectedIndex);
            stationTypeDropdown.RefreshShownValue();
        }

        private void BindConfiguredState(bool isChainActive)
        {
            if (!_em.Exists(_station)) return;
            var stationConfig = _em.GetComponentData<StationConfig>(_station);
            var stationState = _em.GetComponentData<StationState>(_station);

            _currentStationType = _availableStationTypes.Find(t => t.StationTypeID == stationConfig.StationTypeID);
            if (_currentStationType == null)
            {
                emptyStateGroup.SetActive(true);
                configuredStateGroup.SetActive(false);
                return;
            }

            titleText.text = _currentStationType.StationName;

            if (_lastStationTypeId != _currentStationType.StationTypeID)
            {
                _lastStationTypeId = _currentStationType.StationTypeID;
                _lastRecipesHash = 0;
                _pendingRecipeId = null;
                _pendingFramesLeft = 0;
                _isListenerBound = false;
            }

            if (!_isListenerBound)
            {
                enabledToggle.onValueChanged.RemoveAllListeners();
                removeButton.onClick.RemoveAllListeners();
                recipeDropdown.onValueChanged.RemoveAllListeners();

                enabledToggle.onValueChanged.AddListener(OnToggleStation);
                removeButton.onClick.AddListener(OnRemoveStation);
                recipeDropdown.onValueChanged.AddListener(OnRecipeChanged);

                _isListenerBound = true;
            }

            var factEnabled = (stationState.Enabled == 1);
            if (_pendingEnabled.HasValue)
            {
                if (_pendingEnabled.Value == factEnabled)
                {
                    _pendingEnabled = null;
                    _pendingEnabledFramesLeft = 0;
                }
            }
            else
            {
                enabledToggle.SetIsOnWithoutNotify(factEnabled);
            }
            enabledToggle.interactable = true;

            EnsureRecipeOptionsUpToDate();

            int currentRecipeId = stationState.SelectedRecipeID;
            int recipeIndex = _availableRecipesForStation.FindIndex(r => r.RecipeID == currentRecipeId);

            if (recipeIndex == -1 && _availableRecipesForStation.Count > 0)
            {
                recipeIndex = 0;
                _pendingRecipeId = _availableRecipesForStation[0].RecipeID;
                _pendingFramesLeft = ecsConfirmTimeoutFrames;

                recipeDropdown.SetValueWithoutNotify(recipeIndex);
                recipeDropdown.RefreshShownValue();

                SendSetRecipeRequest(_pendingRecipeId.Value);
            }
            else if (recipeIndex != -1 && recipeDropdown.value != recipeIndex)
            {
                recipeDropdown.SetValueWithoutNotify(recipeIndex);
                recipeDropdown.RefreshShownValue();
            }

            if (_pendingRecipeId.HasValue && stationState.SelectedRecipeID == _pendingRecipeId.Value)
            {
                _pendingRecipeId = null;
                _pendingFramesLeft = 0;
            }

            UpdateStatusAndRequirements(stationState, recipeIndex, isChainActive);
        }


        private void EnsureRecipeOptionsUpToDate()
        {
            if (recipeRegistry == null || _currentStationType == null) return;

            var filteredRecipes = recipeRegistry.Recipes
                .Where(r => r != null && r.RequiredStationType != null && r.RequiredStationType.StationTypeID == _currentStationType.StationTypeID)
                .OrderBy(r => r.RecipeName)
                .ToList();

            unchecked
            {
                int h = 17;
                h = h * 31 + filteredRecipes.Count;
                foreach (var recipe in filteredRecipes)
                {
                    h = h * 31 + recipe.RecipeID;
                    h = h * 31 + (recipe.RecipeName?.GetHashCode() ?? 0);
                }

                if (h != _lastRecipesHash)
                {
                    _lastRecipesHash = h;
                    _availableRecipesForStation.Clear();
                    _availableRecipesForStation.AddRange(filteredRecipes);
                    recipeDropdown.ClearOptions();
                    recipeDropdown.AddOptions(_availableRecipesForStation.Select(r => r.RecipeName).ToList());
                    recipeDropdown.RefreshShownValue();
                }
            }
        }

        private void OnRecipeChanged(int idx)
        {
            if (_availableRecipesForStation.Count == 0 || idx < 0 || idx >= _availableRecipesForStation.Count) return;

            int recipeId = _availableRecipesForStation[idx].RecipeID;
            _pendingRecipeId = recipeId;
            _pendingFramesLeft = ecsConfirmTimeoutFrames;

            recipeDropdown.SetValueWithoutNotify(idx);
            recipeDropdown.RefreshShownValue();

            SendSetRecipeRequest(recipeId);
        }


        private void SendSetRecipeRequest(int recipeId)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new SetStationRecipeRequest
            {
                Workshop = _workshop,
                SlotIndex = _slotIndex,
                RecipeID = recipeId
            });
        }

        private void OnRemoveStation()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new RemoveStationRequest { Workshop = _workshop, SlotIndex = _slotIndex });
        }

        private void OnToggleStation(bool isOn)
        {
            if (!_em.Exists(_workshop) || !_em.Exists(_station)) return;

            enabledToggle.SetIsOnWithoutNotify(isOn);
            _pendingEnabled = isOn;
            _pendingEnabledFramesLeft = enabledConfirmTimeoutFrames;

            var e = _em.CreateEntity();
            _em.AddComponentData(e, new ToggleStationRequest
            {
                Workshop = _workshop,
                SlotIndex = _slotIndex,
                Enable = isOn
            });
        }

        private void UpdateStatusAndRequirements(StationState state, int recipeIndex, bool isChainActive)
        {
            requirementsText.gameObject.SetActive(true);
            var sbReq = new StringBuilder();

            ProductionRecipeSO fullRecipe;
            if (recipeIndex != -1 && recipeIndex < _availableRecipesForStation.Count)
            {
                fullRecipe = _availableRecipesForStation[recipeIndex];
            }
            else
            {
                progressSlider.gameObject.SetActive(false);
                if (productionInfoText != null) productionInfoText.gameObject.SetActive(false);
                statusText.text = "Ошибка: рецепт не выбран";
                statusIndicatorImage.color = COLOR_ERROR;
                requirementsText.text = "Выберите рецепт из списка.";
                return;
            }

            bool isEnabled = state.Enabled == 1;

            progressSlider.gameObject.SetActive(false);
            if (productionInfoText != null) productionInfoText.gameObject.SetActive(false);
            
            if (!isEnabled)
            {
                statusText.text = "Отключен от цепи";
                statusIndicatorImage.color = COLOR_DISABLED;
            }
            else if (!isChainActive)
            {
                statusText.text = "Ожидание запуска цепи";
                statusIndicatorImage.color = COLOR_IDLE;
            }
            else if (state.Status == StationStatus.WaitingForInput)
            {
                statusText.text = "Нет ресурсов";
                statusIndicatorImage.color = GetColorForStatus(state.Status);
            }
            else if (state.Status == StationStatus.AwaitingManualLabor)
            {
                statusText.text = "Ожидает рабочего";
                statusIndicatorImage.color = GetColorForStatus(state.Status);
            }
            else if (state.PowerEfficiency < 0.01f)
            {
                statusText.text = "Нет энергии";
                statusIndicatorImage.color = COLOR_WAITING;
            }
            else if (state.PowerEfficiency < 0.99f)
            {
                statusText.text = $"Просадка сети ({state.PowerEfficiency * 100:F0}%)";
                statusIndicatorImage.color = COLOR_WAITING;
            }
            else
            {
                statusText.text = state.Status.ToString();
                statusIndicatorImage.color = GetColorForStatus(state.Status);
            }

            // 2. ОТОБРАЖАЕМ ТАЙМЕРЫ В СТАРОМ ФОРМАТЕ
            if (productionInfoText != null)
            {
                productionInfoText.gameObject.SetActive(true);
                var sbInfo = new StringBuilder();

                float totalBT = math.max(0.01f, fullRecipe.BaseTime + state.TimePenalty);
                float totalHC = math.max(0.01f, fullRecipe.HammerCost);

                switch (state.Status)
                {
                    case StationStatus.ApplyingManualLabor:
                        {
                            progressSlider.gameObject.SetActive(true);
                            float applied = math.max(0f, state.AppliedHammerCost);
                            float remainingHC = math.max(0f, totalHC - applied);
                            progressSlider.value = math.saturate(applied / totalHC);
                            sbInfo.AppendLine($"HC (ручной труд): осталось {remainingHC:F1} сек.");
                            sbInfo.AppendLine($"BT (автомат): будет ещё {totalBT:F1} сек. после HC");
                            break;
                        }
                    case StationStatus.Working:
                        {
                            progressSlider.gameObject.SetActive(true);
                            progressSlider.value = 1.0f - math.saturate(state.RemainingTime / totalBT);
                            sbInfo.AppendLine($"BT (автомат): осталось {state.RemainingTime:F1} сек.");
                            sbInfo.AppendLine($"HC (ручной труд): 0.0 сек.");
                            break;
                        }
                    default:
                        {
                            progressSlider.gameObject.SetActive(true);
                            progressSlider.value = 0f;
                            sbInfo.AppendLine($"HC (ручной труд): {totalHC:F1} сек.");
                            sbInfo.AppendLine($"BT (автомат): {totalBT:F1} сек.");
                            break;
                        }
                }
                productionInfoText.text = sbInfo.ToString();
            }

            // 3. ФОРМИРУЕМ СПИСОК ТРЕБОВАНИЙ
            sbReq.AppendLine("<b>Требования:</b>");
            sbReq.AppendLine($"<color={(isEnabled ? "green" : "grey")}>- Включен в цепь</color>");

            string powerColor;
            string powerTextStr;
            if (!isEnabled || !isChainActive)
            {
                powerColor = "grey";
                powerTextStr = $"- Энергия ({fullRecipe.RequiredKW} кВт)";
            }
            else
            {
                if (state.PowerEfficiency >= 0.99f) powerColor = "green";
                else if (state.PowerEfficiency > 0.01f) powerColor = "yellow";
                else powerColor = "red";
                float receivedKW = fullRecipe.RequiredKW * state.PowerEfficiency;
                powerTextStr = $"- Энергия: {receivedKW:F1}/{fullRecipe.RequiredKW} кВт (КПД: {state.PowerEfficiency * 100:F0}%)";
            }
            sbReq.AppendLine($"<color={powerColor}>{powerTextStr}</color>");

            var resourceResult = CheckHasResourcesDetailed(fullRecipe);
            sbReq.AppendLine($"<color={(!isEnabled ? "grey" : (resourceResult.HasAllResources ? "green" : "red"))}>- Ресурсы</color>");

            if (!resourceResult.HasAllResources && isEnabled)
            {
                sbReq.Append(resourceResult.MissingItemsText);
            }
            requirementsText.text = sbReq.ToString();
        }

        private bool IsLastActiveStationInChain()
        {
            if (!_em.HasBuffer<StationSlot>(_workshop)) return false;
            var slots = _em.GetBuffer<StationSlot>(_workshop);
            for (int i = _slotIndex + 1; i < slots.Length; i++)
            {
                if (_em.Exists(slots[i].Station) &&
                    _em.HasComponent<StationState>(slots[i].Station) &&
                    _em.GetComponentData<StationState>(slots[i].Station).Enabled == 1)
                {
                    return false;
                }
            }
            return true;
        }

        private float GetCumulativePowerForPreviousStations(float selfPower)
        {
            if (!_em.HasBuffer<StationSlot>(_workshop)) return selfPower;
            var slots = _em.GetBuffer<StationSlot>(_workshop);
            float total = selfPower;
            for (int i = 0; i < _slotIndex; i++)
            {
                var slot = slots[i];
                if (!_em.Exists(slot.Station) || !_em.HasComponent<StationState>(slot.Station) || _em.GetComponentData<StationState>(slot.Station).Enabled == 0) continue;
                var state = _em.GetComponentData<StationState>(slot.Station);
                var recipe = GetRecipeById(state.SelectedRecipeID);
                if (recipe != null)
                {
                    total += recipe.RequiredKW;
                }
            }
            return total;
        }

        private ResourceCheckResult CheckHasResourcesDetailed(ProductionRecipeSO recipe)
        {
            if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
            {
                return new ResourceCheckResult { HasAllResources = true, MissingItemsText = "" };
            }

            var availableInventory = new NativeHashMap<int, int>(64, Allocator.Temp);
            if (_em.HasBuffer<InputInventorySlot>(_workshop))
            {
                foreach (var item in _em.GetBuffer<InputInventorySlot>(_workshop))
                {
                    if (item.ItemID != 0)
                    {
                        availableInventory.TryGetValue(item.ItemID, out int current);
                        availableInventory[item.ItemID] = current + item.Amount;
                    }
                }
            }

            if (_em.HasBuffer<WorkshopWIPBufferElement>(_workshop))
            {
                foreach (var item in _em.GetBuffer<WorkshopWIPBufferElement>(_workshop))
                {
                    if (item.ItemID != 0)
                    {
                        availableInventory.TryGetValue(item.ItemID, out int current);
                        availableInventory[item.ItemID] = current + item.Amount;
                    }
                }
            }

            var missingItemsSb = new StringBuilder();
            bool allResourcesFound = true;
            foreach (var requiredIngredient in recipe.Ingredients)
            {
                if (requiredIngredient.Item == null) continue;
                availableInventory.TryGetValue(requiredIngredient.Item.itemID, out int amountOnHand);
                if (amountOnHand < requiredIngredient.Amount)
                {
                    allResourcesFound = false;
                    int missingAmount = requiredIngredient.Amount - amountOnHand;
                    missingItemsSb.AppendLine($"<color=red>- {requiredIngredient.Item.itemName} (не хватает {missingAmount})</color>");
                }
            }
            availableInventory.Dispose();

            return new ResourceCheckResult
            {
                HasAllResources = allResourcesFound,
                MissingItemsText = missingItemsSb.ToString()
            };
        }

        private Color GetColorForStatus(StationStatus status)
        {
            switch (status)
            {
                case StationStatus.Working:
                case StationStatus.ApplyingManualLabor: return COLOR_OK;
                case StationStatus.Idle: return COLOR_IDLE;
                case StationStatus.WaitingForInput:
                case StationStatus.OutputBlocked:
                case StationStatus.AwaitingManualLabor:
                case StationStatus.AwaitingActivation: return COLOR_WAITING;
                case StationStatus.Offline:
                case StationStatus.NeedsRepair: return COLOR_ERROR;
                default: return Color.gray;
            }
        }

        private struct ResourceCheckResult
        {
            public bool HasAllResources;
            public string MissingItemsText;
        }



        private class PointerForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public StationCardUI owner;
            public void OnPointerEnter(PointerEventData e)
            {
                if (owner != null && owner._root != null && owner._em.Exists(owner._station))
                    owner._root.OnStationHoverEnter(owner._station, owner._slotIndex);
            }
            public void OnPointerExit(PointerEventData e)
            {
                owner?._root?.OnStationHoverExit();
            }
        }

        private void EnsureHoverForwarders()
        {
            if (_hoverForwardersReady) return;

            var gfx = GetComponentsInChildren<Graphic>(true);
            foreach (var g in gfx)
            {
                if (!g.raycastTarget) g.raycastTarget = true;
                if (!g.gameObject.TryGetComponent<PointerForwarder>(out var f))
                    f = g.gameObject.AddComponent<PointerForwarder>();
                f.owner = this;
            }
            _hoverForwardersReady = true;
        }


        private void DbgWarn(string msg)
        {
            if (!EnableDebug) return;
            Debug.LogWarning($"[StationCardUI #{_instanceId} slot={_slotIndex}] <WARN> {msg}", this);
        }
    }
}