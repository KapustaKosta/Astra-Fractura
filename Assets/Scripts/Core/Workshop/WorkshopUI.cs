using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Energy.Core;
using Game.Production;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

namespace Game.Workshop
{
    public class WorkshopUI : MonoBehaviour
    {
        public static WorkshopUI Instance { get; private set; }


        [Header("Dependencies")]
        [Tooltip("Ссылка на ScriptableObject реестра рецептов.")]
        public ProductionRecipeRegistrySO recipeRegistry;


        [Header("Panel & Header")]
        public GameObject panel;
        public TextMeshProUGUI workshopNameText;
        public Button closeButton;

        [Header("Compact Bar")]
        public TextMeshProUGUI powerText;
        public Image powerIndicatorBar;
        public Button openInputInventoryButton;
        public Button openWIPInventoryButton;
        public Button openOutputInventoryButton;

        [Header("Worker Assignment")]
        public GameObject assignWorkerGroup;
        public TMP_Dropdown npcDropdown;
        public Button confirmAssignButton;
        public TextMeshProUGUI npcInfoText;

        [Header("Worker Info")]
        public TextMeshProUGUI workerInfoText;
        public Button manageWorkersButton;

        [Header("Chain Production")]
        public TMP_Dropdown finalRecipeDropdown;
        public TMP_InputField batchAmountInput;
        public Toggle infiniteProductionToggle;
        public Button startProductionChainButton;
        public Button stopProductionChainButton;
        public TextMeshProUGUI chainInfoText;

        [Header("Stations Row")]
        public Transform stationsRow;
        public StationCardUI stationCardPrefab;

        [Header("Station Hover Panel")]
        public GameObject stationHoverPanel;
        public TextMeshProUGUI stationHoverText;

        private readonly List<StationCardUI> _cards = new();
        private readonly List<Entity> _availableNpcs = new List<Entity>();
        private List<StationType> _availableStationTypes;


        private readonly List<ProductionRecipeSO> _producibleRecipes = new List<ProductionRecipeSO>();
        private Dictionary<int, ProductionRecipeSO> _allRecipesMap;


        private EntityManager _em;
        private Entity _activeWorkshop;
        private bool _isInitialized;
        private bool _listenersBound = false;

        private readonly Dictionary<Entity, bool> _infiniteByWorkshop = new();
        private int _lastRecipeFingerprint = int.MinValue;
        private bool _infiniteToggleBound = false;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            _availableStationTypes = new List<StationType>(Resources.LoadAll<StationType>(""))
                .OrderBy(t => t.StationName)
                .ToList();


            _allRecipesMap = new Dictionary<int, ProductionRecipeSO>();
            if (recipeRegistry != null)
            {
                foreach (var recipe in recipeRegistry.Recipes.Where(r => r != null))
                {
                    _allRecipesMap[recipe.RecipeID] = recipe;
                }
            }


            if (panel != null) panel.SetActive(false);
            if (stationHoverPanel != null) stationHoverPanel.SetActive(false);
        }

        void Start()
        {
            TryInit();
            BindListeners();
        }

        void LateUpdate()
        {
            if (!_isInitialized) { TryInit(); return; }

            var gameStateQuery = _em.CreateEntityQuery(typeof(GameState));
            if (gameStateQuery.IsEmpty) return;
            var gameStateEntity = gameStateQuery.GetSingletonEntity();

            bool shouldBeOpen = _em.HasComponent<InUIMode>(gameStateEntity) &&
                                _em.HasComponent<UIState>(gameStateEntity) &&
                                _em.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Workshop;

            if (panel.activeSelf != shouldBeOpen)
            {
                if (shouldBeOpen) { ShowFor(_em.GetComponentData<UIState>(gameStateEntity).ActiveUITarget); }
                else { Hide(); }
            }

            if (panel.activeSelf)
            {
                if (_activeWorkshop == Entity.Null || !_em.Exists(_activeWorkshop)) { Hide(); return; }
                RefreshUI();
            }
        }

        void BindListeners()
        {
            if (!_isInitialized || _listenersBound) return;

            closeButton?.onClick.AddListener(Hide);
            openInputInventoryButton?.onClick.AddListener(OnOpenInputInventory);
            openWIPInventoryButton?.onClick.AddListener(OnOpenWIPInventory);
            openOutputInventoryButton?.onClick.AddListener(OnOpenOutputInventory);
            manageWorkersButton?.onClick.AddListener(OnManageWorkers);
            confirmAssignButton?.onClick.AddListener(OnConfirmAssign);
            startProductionChainButton?.onClick.AddListener(OnStartProductionChain);
            stopProductionChainButton?.onClick.AddListener(OnStopProductionChain);

            finalRecipeDropdown?.onValueChanged.AddListener(_ => UpdateChainInfoText());
            batchAmountInput?.onValueChanged.AddListener(_ => UpdateChainInfoText());
            npcDropdown?.onValueChanged.AddListener(OnNpcSelectionChanged);

            _listenersBound = true;
        }

        void RefreshUI()
        {
            RefreshCompactBar();
            TryRebuildFinalRecipeDropdownIfChanged();

            var queue = _em.GetBuffer<WorkshopProductionQueueItem>(_activeWorkshop);
            var wsState = _em.GetComponentData<WorkshopState>(_activeWorkshop);
            bool isChainActive = wsState.IsOn && !queue.IsEmpty;

            RefreshStations(isChainActive);

            if (isChainActive)
            {
                startProductionChainButton.gameObject.SetActive(false);
                stopProductionChainButton.gameObject.SetActive(true);
                finalRecipeDropdown.gameObject.SetActive(false);
                if (batchAmountInput != null) batchAmountInput.gameObject.SetActive(false);
                if (infiniteProductionToggle != null) infiniteProductionToggle.gameObject.SetActive(false);
                UpdateChainProgressInfo();
            }
            else
            {
                startProductionChainButton.gameObject.SetActive(true);
                stopProductionChainButton.gameObject.SetActive(false);
                finalRecipeDropdown.gameObject.SetActive(true);
                if (batchAmountInput != null) batchAmountInput.gameObject.SetActive(true);

                if (infiniteProductionToggle != null)
                {
                    infiniteProductionToggle.gameObject.SetActive(true);
                    EnsureInfiniteToggleBound();
                    bool saved = GetInfiniteFlagForCurrentWorkshop();
                    infiniteProductionToggle.SetIsOnWithoutNotify(saved);
                    if (batchAmountInput != null) batchAmountInput.interactable = !saved;
                }
                UpdateChainInfoText();
            }
        }

        private void OnInfiniteToggled(bool isOn)
        {
            if (_activeWorkshop != Entity.Null) _infiniteByWorkshop[_activeWorkshop] = isOn;
            if (batchAmountInput) batchAmountInput.interactable = !isOn;
            UpdateChainInfoText();
        }

        private void EnsureInfiniteToggleBound()
        {
            if (infiniteProductionToggle != null && !_infiniteToggleBound)
            {
                infiniteProductionToggle.onValueChanged.AddListener(OnInfiniteToggled);
                _infiniteToggleBound = true;
            }
        }

        private bool GetInfiniteFlagForCurrentWorkshop()
        {
            return _activeWorkshop != Entity.Null && _infiniteByWorkshop.TryGetValue(_activeWorkshop, out var v) && v;
        }

        private void OnStopProductionChain()
        {
            if (_activeWorkshop == Entity.Null || !_em.Exists(_activeWorkshop)) return;
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new RequestWorkshopStop { Workshop = _activeWorkshop });
        }

        void OnStartProductionChain()
        {
            if (_activeWorkshop == Entity.Null || !_em.Exists(_activeWorkshop)) return;
            if (_producibleRecipes.Count == 0 || finalRecipeDropdown.value >= _producibleRecipes.Count) return;
            var selectedRecipe = _producibleRecipes[finalRecipeDropdown.value];

            int amountToProduce, initialAmount;
            if (GetInfiniteFlagForCurrentWorkshop())
            {
                amountToProduce = -1;
                initialAmount = -1;
            }
            else
            {
                int.TryParse(batchAmountInput.text, out int desiredAmount);
                if (desiredAmount <= 0) desiredAmount = 1;
                amountToProduce = desiredAmount;
                initialAmount = amountToProduce;
            }

            var e = _em.CreateEntity();
            _em.AddComponentData(e, new StartWorkshopProductionRequest
            {
                Workshop = _activeWorkshop,
                FinalRecipeID = selectedRecipe.RecipeID,
                Amount = amountToProduce,
                InitialAmount = initialAmount
            });
        }

        public void ShowFor(Entity workshop)
        {
            TryInit();
            _activeWorkshop = workshop;
            if (panel != null) panel.SetActive(true);
            assignWorkerGroup.SetActive(false);
            if (npcInfoText != null) npcInfoText.gameObject.SetActive(false);
            RebuildAndRefresh();
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            if (stationHoverPanel != null) stationHoverPanel.SetActive(false);
            _activeWorkshop = Entity.Null;
        }

        void TryInit()
        {
            if (_isInitialized) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _em = world.EntityManager;
                _isInitialized = true;
            }
        }

        void RebuildAndRefresh()
        {
            if (!_em.Exists(_activeWorkshop)) return;
            ForcePopulateFinalRecipeDropdown();
            RefreshUI();
        }

        private bool TryRebuildFinalRecipeDropdownIfChanged()
        {
            if (!_em.Exists(_activeWorkshop) || finalRecipeDropdown == null) return false;

            int? prevSelectedRecipeId = null;
            if (_producibleRecipes.Count > 0 && finalRecipeDropdown.value < _producibleRecipes.Count)
            {
                prevSelectedRecipeId = _producibleRecipes[finalRecipeDropdown.value].RecipeID;
            }

            var newList = new List<ProductionRecipeSO>();
            int newFingerprint = BuildProducibleRecipesAndFingerprint(newList);

            if (newFingerprint == _lastRecipeFingerprint) return false;

            _lastRecipeFingerprint = newFingerprint;
            _producibleRecipes.Clear();
            _producibleRecipes.AddRange(newList);

            finalRecipeDropdown.ClearOptions();
            if (_producibleRecipes.Count > 0)
                finalRecipeDropdown.AddOptions(_producibleRecipes.Select(r => r.RecipeName).ToList());

            int newIndex = 0;
            if (prevSelectedRecipeId.HasValue)
            {
                newIndex = _producibleRecipes.FindIndex(r => r.RecipeID == prevSelectedRecipeId.Value);
                if (newIndex < 0) newIndex = 0;
            }

            finalRecipeDropdown.SetValueWithoutNotify(_producibleRecipes.Count > 0 ? newIndex : 0);
            finalRecipeDropdown.RefreshShownValue();
            return true;
        }

        private void ForcePopulateFinalRecipeDropdown()
        {
            var list = new List<ProductionRecipeSO>();
            _lastRecipeFingerprint = BuildProducibleRecipesAndFingerprint(list);
            _producibleRecipes.Clear();
            _producibleRecipes.AddRange(list);

            finalRecipeDropdown.ClearOptions();
            if (_producibleRecipes.Count > 0)
                finalRecipeDropdown.AddOptions(_producibleRecipes.Select(r => r.RecipeName).ToList());

            finalRecipeDropdown.SetValueWithoutNotify(0);
            finalRecipeDropdown.RefreshShownValue();
        }


        private int BuildProducibleRecipesAndFingerprint(List<ProductionRecipeSO> outList)
        {
            outList.Clear();
            if (!_em.Exists(_activeWorkshop) || recipeRegistry == null) return 0;

            var slots = _em.GetBuffer<StationSlot>(_activeWorkshop);
            var installedStationTypeIDs = new HashSet<int>();
            foreach (var slot in slots)
            {
                if (_em.HasComponent<StationConfig>(slot.Station))
                {
                    var config = _em.GetComponentData<StationConfig>(slot.Station);
                    if (config.StationTypeID != -1)
                    {
                        installedStationTypeIDs.Add(config.StationTypeID);
                    }
                }
            }

            if (installedStationTypeIDs.Count == 0) return 0;

            foreach (var recipe in recipeRegistry.Recipes)
            {
                if (recipe != null && recipe.RequiredStationType != null && installedStationTypeIDs.Contains(recipe.RequiredStationType.StationTypeID))
                {
                    outList.Add(recipe);
                }
            }

            outList.Sort((a, b) => string.Compare(a.RecipeName, b.RecipeName, System.StringComparison.Ordinal));

            int fp = 17;
            foreach (var recipe in outList) fp = fp * 31 + recipe.RecipeID;
            return fp;
        }

        private int CalculateMaxProducibleAmountChain(ProductionRecipeSO finalRecipe)
        {
            // Эта логика сильно зависит от симуляции и трудно точно предсказать на UI-слое.
            var virtualInventory = new Dictionary<int, int>();
            if (_em.HasBuffer<InputInventorySlot>(_activeWorkshop))
            {
                foreach (var item in _em.GetBuffer<InputInventorySlot>(_activeWorkshop))
                {
                    if (item.ItemID != 0 && item.Amount > 0)
                    {
                        virtualInventory.TryGetValue(item.ItemID, out int current);
                        virtualInventory[item.ItemID] = current + item.Amount;
                    }
                }
            }

            // Упрощенная проверка: есть ли ресурсы хотя бы на 1 запуск *первой* станции в цепи.
            var slots = _em.GetBuffer<StationSlot>(_activeWorkshop);
            foreach (var slot in slots)
            {
                if (_em.HasComponent<StationState>(slot.Station) && _em.GetComponentData<StationState>(slot.Station).Enabled == 1)
                {
                    var state = _em.GetComponentData<StationState>(slot.Station);
                    if (_allRecipesMap.TryGetValue(state.SelectedRecipeID, out var recipe))
                    {
                        foreach (var input in recipe.Ingredients)
                        {
                            if (!virtualInventory.TryGetValue(input.Item.itemID, out int have) || have < input.Amount)
                            {
                                return 0; // Не хватает ресурсов даже на первый шаг
                            }
                        }
                        return 999; // Ресурсы для первого шага есть, возвращаем "много"
                    }
                }
            }
            return 0;
        }

        private float CalculateTotalChainTimeRecursive(int recipeID, Dictionary<int, float> memo)
        {
            // Используем мемоизацию, чтобы не считать одно и то же несколько раз
            if (memo.TryGetValue(recipeID, out float cachedTime))
            {
                return cachedTime;
            }

            if (!_allRecipesMap.TryGetValue(recipeID, out var recipe))
            {
                return 0f;
            }

            float totalTime = recipe.BaseTime + recipe.HammerCost;

            // Рекурсивно добавляем время, необходимое для производства всех ингредиентов
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.Item != null && _allRecipesMap.ContainsKey(ingredient.Item.itemID))
                    {
                        // Находим рецепт для ингредиента (т.к. ID рецепта = ID выходного предмета)
                        int ingredientRecipeID = ingredient.Item.itemID;
                        // Умножаем время производства одного ингредиента на требуемое количество
                        totalTime += CalculateTotalChainTimeRecursive(ingredientRecipeID, memo) * ingredient.Amount;
                    }
                }
            }

            memo[recipeID] = totalTime; // Сохраняем результат в кэш
            return totalTime;
        }
        private void UpdateChainProgressInfo()
        {
            var queue = _em.GetBuffer<WorkshopProductionQueueItem>(_activeWorkshop);
            if (queue.IsEmpty) { chainInfoText.text = "<b>Производство завершено.</b>"; return; }
            var q = queue[0];
            var slots = _em.GetBuffer<StationSlot>(_activeWorkshop);

            Entity currentActiveStation = Entity.Null;
            int currentActiveStationIndex = -1;
            float timeForSubsequentStations = 0f;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!_em.HasComponent<StationState>(slot.Station)) continue;
                var state = _em.GetComponentData<StationState>(slot.Station);
                if (state.Enabled != 1) continue;

                if (currentActiveStation == Entity.Null && state.Status != 
                    StationStatus.Idle && state.Status != StationStatus.WaitingForInput && state.Status != StationStatus.Offline)
                {
                    currentActiveStation = slot.Station;
                    currentActiveStationIndex = i;
                }
                if (currentActiveStation != Entity.Null && i > currentActiveStationIndex)
                {
                    if (_allRecipesMap.TryGetValue(state.SelectedRecipeID, out var recipe))
                    {
                        timeForSubsequentStations += recipe.BaseTime + recipe.HammerCost;
                    }
                }
            }
            if (currentActiveStation == Entity.Null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (!_em.HasComponent<StationState>(slot.Station) || _em.GetComponentData<StationState>(slot.Station).Enabled != 1) continue;
                    currentActiveStation = slot.Station;
                    currentActiveStationIndex = i;
                    timeForSubsequentStations = 0f;
                    for (int j = i + 1; j < slots.Length; j++)
                    {
                        var nextSlot = slots[j];
                        if (_em.HasComponent<StationState>(nextSlot.Station))
                        {
                            var state = _em.GetComponentData<StationState>(nextSlot.Station);
                            if (state.Enabled == 1 && _allRecipesMap.TryGetValue(state.SelectedRecipeID, out var recipe))
                            {
                                timeForSubsequentStations += recipe.BaseTime + recipe.HammerCost;
                            }
                        }
                    }
                    break;
                }
            }
            float totalChainTime = 0f;
            if (_producibleRecipes.Count > 0)
            {
                // Находим рецепт, который сейчас в производстве
                var currentFinalRecipe = _allRecipesMap.Values.FirstOrDefault(r => r.RecipeID == q.FinalRecipeID);
                if (currentFinalRecipe != null)
                {
                    // Используем новую рекурсивную функцию для точного расчета
                    totalChainTime = CalculateTotalChainTimeRecursive(currentFinalRecipe.RecipeID, new Dictionary<int, float>());
                }
            }
            var sb = new StringBuilder("<b>Производство активно.</b>\n");
            if (q.InitialAmount == -1) { sb.AppendLine("Оставшиеся: ∞"); }
            else
            {
                int left = math.max(0, q.AmountToProduce);
                int currentIx = math.clamp(q.InitialAmount - left + 1, 1, q.InitialAmount);
                sb.AppendLine($"Итерация: {currentIx} / {q.InitialAmount}");
            }

            // Используем точное время для одной итерации, умноженное на количество оставшихся
            if (q.AmountToProduce > 0)
            {
                float totalETA = /* remainingInCurrentIteration + */ (math.max(0, q.AmountToProduce - 1) * totalChainTime);
                sb.AppendLine($"Всего осталось ≈ {totalETA:F1} сек.");
            }

            chainInfoText.text = sb.ToString();
        }

        private void UpdateChainInfoText()
        {
            if (finalRecipeDropdown == null || chainInfoText == null || _producibleRecipes.Count == 0 || finalRecipeDropdown.value >= _producibleRecipes.Count)
            {
                chainInfoText.text = "Нет доступных для производства продуктов."; return;
            }
            if (GetInfiniteFlagForCurrentWorkshop())
            {
                chainInfoText.text = "Готовы производить бесконечно (пока есть ресурсы)."; return;
            }
            var selectedRecipe = _producibleRecipes[finalRecipeDropdown.value];
            int desiredBatch = 1;
            if (batchAmountInput != null && !string.IsNullOrWhiteSpace(batchAmountInput.text))
                int.TryParse(batchAmountInput.text, out desiredBatch);
            if (desiredBatch <= 0) desiredBatch = 1;

            int maxProducible = CalculateMaxProducibleAmountChain(selectedRecipe);
            if (maxProducible <= 0)
            {
                chainInfoText.text = $"<color=yellow>Недостаточно ресурсов для цепи: {selectedRecipe.RecipeName}</color>"; return;
            }
            chainInfoText.text = $"Готовы произвести: {selectedRecipe.RecipeName} x {desiredBatch}";
        }



        public void OnStationHoverEnter(Entity station, int slotIndex)
        {
            if (stationHoverPanel == null || stationHoverText == null || !_em.Exists(station) || !_em.HasComponent<StationState>(station)) return;
            var st = _em.GetComponentData<StationState>(station);
            if (st.Enabled == 0) { stationHoverText.text = "Отключен от цепи"; stationHoverPanel.SetActive(true); return; }
            if (st.AssignedWorker != Entity.Null && _em.Exists(st.AssignedWorker))
            {
                string name = _em.HasComponent<NPCComponent>(st.AssignedWorker) ? _em.GetComponentData<NPCComponent>(st.AssignedWorker).Name.ToString() : "NPC";
                float hc = _em.HasComponent<NPCWorkForce>(st.AssignedWorker) ? _em.GetComponentData<NPCWorkForce>(st.AssignedWorker).CurrentHammerPool : 0f;
                stationHoverText.text = $"Обслуживает: {name}\nHC: {hc:0.0}";
                stationHoverPanel.SetActive(true);
                return;
            }
            if (st.Status == StationStatus.AwaitingManualLabor)
            {
                (Entity worker, float delay) pred = PredictWorkerForStation(slotIndex);
                if (pred.worker != Entity.Null && _em.Exists(pred.worker))
                {
                    string name = _em.HasComponent<NPCComponent>(pred.worker) ? _em.GetComponentData<NPCComponent>(pred.worker).Name.ToString() : "NPC";
                    float hc = _em.HasComponent<NPCWorkForce>(pred.worker) ? _em.GetComponentData<NPCWorkForce>(pred.worker).CurrentHammerPool : 0f;
                    stationHoverText.text = pred.delay > 0.001f ? $"Обслужит: {name}\nHC сейчас:" +
                        $" {hc:0.0}\nОсвободится через ≈ {pred.delay:0.0} сек." : $"Обслужит: {name}\nHC сейчас: {hc:0.0}";
                }
                else { stationHoverText.text = "Нет доступных рабочих"; }
                stationHoverPanel.SetActive(true);
            }
        }
        public void OnStationHoverExit() { if (stationHoverPanel != null) stationHoverPanel.SetActive(false); }
        private (Entity worker, float delay) PredictWorkerForStation(int targetSlotIndex)
        {
            if (!_em.HasBuffer<AssignedWorker>(_activeWorkshop)) return (Entity.Null, 0f);
            var workersBuf = _em.GetBuffer<AssignedWorker>(_activeWorkshop);
            if (workersBuf.IsEmpty) return (Entity.Null, 0f);
            var manualStations = new List<(int slotIdx, float remainingHC)>();
            var slots = _em.GetBuffer<StationSlot>(_activeWorkshop);
            for (int i = 0; i < slots.Length; i++)
            {
                var stEnt = slots[i].Station;
                if (!_em.Exists(stEnt) || !_em.HasComponent<StationState>(stEnt)) continue;
                var st = _em.GetComponentData<StationState>(stEnt);
                if (st.Enabled == 0) continue;
                if (st.Status == StationStatus.ApplyingManualLabor || st.Status == StationStatus.AwaitingManualLabor)
                {
                    if (_allRecipesMap.TryGetValue(st.SelectedRecipeID, out var recipe))
                    {
                        float rem = math.max(0f, recipe.HammerCost - st.AppliedHammerCost);
                        manualStations.Add((i, rem));
                    }
                }
            }
            int W = workersBuf.Length; var times = new float[W]; var wEnts = new Entity[W];
            for (int w = 0; w < W; w++) wEnts[w] = workersBuf[w].NpcEntity;
            Entity chosen = Entity.Null; float delay = 0f;
            foreach (var (slotIdx, remHC) in manualStations)
            {
                int best = 0;
                for (int w = 1; w < W; w++) if (times[w] < times[best]) best = w;
                if (slotIdx == targetSlotIndex) { chosen = wEnts[best]; delay = times[best]; break; }
                times[best] += remHC;
            }
            return (chosen, delay);
        }
        void OnManageWorkers() { bool isActive = !assignWorkerGroup.activeSelf; 
            assignWorkerGroup.SetActive(isActive); if (npcInfoText != null) npcInfoText.gameObject.SetActive(isActive);
            if (isActive) { PopulateNpcDropdown(); } }
        void PopulateNpcDropdown()
        {
            npcDropdown.ClearOptions(); _availableNpcs.Clear(); var options = new List<TMP_Dropdown.OptionData>();
            var npcs = _em.CreateEntityQuery(typeof(NPCComponent), typeof(NPCHiredTag)).ToEntityArray(Allocator.Temp);
            foreach (var npcEntity in npcs)
            {
                if (!_em.Exists(npcEntity)) continue;
                var npcData = _em.GetComponentData<NPCComponent>(npcEntity);
                if (npcData.AssignedWorkshop == Entity.Null) { _availableNpcs.Add(npcEntity);
                    options.Add(new TMP_Dropdown.OptionData(npcData.Name.ToString())); }
            }
            npcs.Dispose();
            if (options.Count == 0) { options.Add(new TMP_Dropdown.OptionData("Нет свободных рабочих")); 
                confirmAssignButton.interactable = false; if (npcInfoText != null) npcInfoText.text = ""; }
            else { confirmAssignButton.interactable = true; }
            npcDropdown.AddOptions(options); npcDropdown.SetValueWithoutNotify(0); OnNpcSelectionChanged(0);
        }
        void OnNpcSelectionChanged(int index)
        {
            if (npcInfoText == null || _availableNpcs.Count == 0 || index >= _availableNpcs.Count) { if (npcInfoText != null) npcInfoText.text = ""; return; }
            var npcEntity = _availableNpcs[index]; if (!_em.Exists(npcEntity)) return;
            var sb = new StringBuilder();
            if (_em.HasComponent<NPCWorkForce>(npcEntity)) sb.AppendLine($"Эффективность: {_em.GetComponentData<NPCWorkForce>(npcEntity).CurrentHammerPool:F0}");
            npcInfoText.text = sb.ToString();
        }
        void OnConfirmAssign()
        {
            if (_availableNpcs.Count == 0 || npcDropdown.value >= _availableNpcs.Count) return;
            var req = _em.CreateEntity();
            _em.AddComponentData(req, new AssignWorkerToWorkshopRequest 
            { NpcEntity = _availableNpcs[npcDropdown.value], WorkshopEntity = _activeWorkshop });
            assignWorkerGroup.SetActive(false); if (npcInfoText != null) npcInfoText.gameObject.SetActive(false);
        }
        void OnOpenInputInventory() 
        {
            if (!_isInitialized || _activeWorkshop == Entity.Null)
                return; var e = _em.CreateEntity();
            _em.AddComponentData(e, new OpenInventoryUIRequest { Target = _activeWorkshop, Type = InventoryType.Input }); 
        }
        void OnOpenWIPInventory() 
        {
            if (!_isInitialized || _activeWorkshop == Entity.Null) 
                return; var e = _em.CreateEntity(); 
            _em.AddComponentData(e, new OpenInventoryUIRequest { Target = _activeWorkshop, Type = InventoryType.WIP }); 
        }
        void OnOpenOutputInventory() 
        {
            if (!_isInitialized || _activeWorkshop == Entity.Null) 
                return; var e = _em.CreateEntity(); 
            _em.AddComponentData(e, new OpenInventoryUIRequest { Target = _activeWorkshop, Type = InventoryType.Output }); 
        }
        void RefreshCompactBar()
        {
            workshopNameText.text = _em.GetComponentData<NetworkNode>(_activeWorkshop).Name.ToString();
            var load = _em.GetComponentData<ConsumerLoad>(_activeWorkshop);
            var usage = _em.GetComponentData<NetLinkUsage>(_activeWorkshop);
            var workers = _em.GetBuffer<AssignedWorker>(_activeWorkshop);
            workerInfoText.text = $"Рабочие: {workers.Length}";
            powerText.text = $"Питание: {usage.InUsedKW:0.##} / {load.CurrentKW:0.##} кВт";
            float requiredKW = load.CurrentKW, suppliedKW = usage.InUsedKW;
            if (requiredKW < 0.01f) { powerIndicatorBar.fillAmount = 0;
                powerIndicatorBar.color = Color.grey; } 
            else { powerIndicatorBar.fillAmount = Mathf.Clamp01(suppliedKW / requiredKW);
                powerIndicatorBar.color = (suppliedKW < requiredKW - 0.01f) ? Color.yellow : Color.cyan; }
        }
        void EnsureCards(int count) { while (_cards.Count < count) 
            { var card = Instantiate(stationCardPrefab, stationsRow);
                card.Init(this); _cards.Add(card); } 
            for (int i = 0; i < _cards.Count; i++) _cards[i].gameObject.SetActive(i < count); }
        void RefreshStations(bool isChainActive)
        {
            if (!_em.HasBuffer<StationSlot>(_activeWorkshop)) { EnsureCards(0); return; }
            var slotsBuffer = _em.GetBuffer<StationSlot>(_activeWorkshop); EnsureCards(slotsBuffer.Length);
            for (int i = 0; i < slotsBuffer.Length; i++) _cards[i].Bind(_activeWorkshop, i, slotsBuffer[i].Station, _em, _availableStationTypes, isChainActive);
        }
    }
}