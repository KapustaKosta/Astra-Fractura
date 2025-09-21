using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Game.Production;
using System;

namespace Game.Workshop
{
    /// <summary>
    /// Управляет UI-элементом (карточкой) для специфического назначения рабочего на конкретный станок.
    /// </summary>
    public class StationAssignmentCardUI : MonoBehaviour
    {
        [Header("UI References")]
        public Image stationIcon;
        public TextMeshProUGUI stationNameText;
        public TextMeshProUGUI hammerCostText;
        public TextMeshProUGUI assignedNpcText;
        public Button unassignButton;
        public TMP_Dropdown assignDropdown;
        public Button confirmAssignButton;
        public GameObject assignmentGroup;
        public GameObject infoGroup;

        private EntityManager _em;
        private Entity _workshop;
        private int _slotIndex;
        private Entity _station;

        private readonly List<Entity> _availableNpcsForDropdown = new List<Entity>();

        private Entity? _pendingNpcAssignment = null;
        private int _pendingFramesLeft = 0;
        private const int EcsConfirmTimeoutFrames = 60;

        private Entity _lastSelectedNpc = Entity.Null;
        private bool _isBinding = false;

        /// <summary>
        /// Вызывается каждый кадр для обработки таймаута ожидания подтверждения от ECS.
        /// </summary>
        private void Update()
        {
            if (_pendingNpcAssignment.HasValue && _pendingFramesLeft > 0)
            {
                _pendingFramesLeft--;
                if (_pendingFramesLeft == 0)
                {
                    _pendingNpcAssignment = null;
                }
            }
        }
        
        /// <summary>
        /// Связывает карточку с данными о станке, настраивает отображение информации
        /// (иконка, имя, стоимость) и элементов управления (выпадающий список, кнопки).
        /// </summary>
        public void Bind(Entity workshop, int slotIndex, Entity station, EntityManager em, List<Entity> allAvailableNpcs, Dictionary<int, ProductionRecipeSO> recipeMap)
        {
            _workshop = workshop;
            _slotIndex = slotIndex;
            _station = station;
            _em = em;

            if (!_em.Exists(_station))
            {
                gameObject.SetActive(false);
                return;
            }

            var stationConfig = _em.GetComponentData<StationConfig>(_station);

            if (stationConfig.StationTypeID == -1)
            {
                gameObject.SetActive(false);
                return;
            }

            var stationState = _em.GetComponentData<StationState>(_station);
            var stationType = WorkshopUI.Instance.GetStationTypeById(stationConfig.StationTypeID);

            if (stationIcon != null)
            {
                stationIcon.sprite = stationType != null ? stationType.Icon : null;
                stationIcon.gameObject.SetActive(stationIcon.sprite != null);
            }

            stationNameText.text = stationType != null ? stationType.StationName : $"Слот #{_slotIndex + 1}";

            if (recipeMap.TryGetValue(stationState.SelectedRecipeID, out var recipe))
            {
                hammerCostText.text = $"Требуется HC: {recipe.HammerCost:F1}";
            }
            else
            {
                hammerCostText.text = "Требуется HC: -";
            }

            Entity specificWorker = stationState.SpecificWorker;

            if (_pendingNpcAssignment.HasValue && specificWorker == _pendingNpcAssignment.Value)
            {
                _pendingNpcAssignment = null;
                _pendingFramesLeft = 0;
            }

            if (specificWorker != Entity.Null && _em.Exists(specificWorker))
            {
                infoGroup.SetActive(true);
                assignmentGroup.SetActive(false);
                var npcName = _em.GetComponentData<NPCComponent>(specificWorker).Name.ToString();
                assignedNpcText.text = $"Назначен: {npcName}";
            }
            else
            {
                infoGroup.SetActive(false);
                assignmentGroup.SetActive(true);
                PopulateDropdown(allAvailableNpcs);
            }

            unassignButton.onClick.RemoveAllListeners();
            unassignButton.onClick.AddListener(OnUnassign);

            confirmAssignButton.onClick.RemoveAllListeners();
            confirmAssignButton.onClick.AddListener(OnConfirmAssign);

            assignDropdown.onValueChanged.RemoveAllListeners();
            assignDropdown.onValueChanged.AddListener(OnAssignDropdownChanged);

            assignDropdown.interactable = !_pendingNpcAssignment.HasValue;
            confirmAssignButton.interactable = !_pendingNpcAssignment.HasValue && _availableNpcsForDropdown.Count > 0;
        }

        /// <summary>
        /// Заполняет выпадающий список доступными для назначения NPC.
        /// </summary>
        private void PopulateDropdown(List<Entity> allAvailableNpcs)
        {
            _isBinding = true;

            Entity previouslySelected = _lastSelectedNpc;
            if (previouslySelected == Entity.Null && _availableNpcsForDropdown.Count > 0 && assignDropdown.value >= 0 && assignDropdown.value < _availableNpcsForDropdown.Count)
            {
                previouslySelected = _availableNpcsForDropdown[assignDropdown.value];
            }

            _availableNpcsForDropdown.Clear();
            assignDropdown.ClearOptions();

            var pairs = new List<(string name, Entity ent)>();
            foreach (var npcEntity in allAvailableNpcs)
            {
                if (!_em.Exists(npcEntity)) continue;
                var npcData = _em.GetComponentData<NPCComponent>(npcEntity);

                if (npcData.AssignedWorkshop == Entity.Null || npcData.AssignedWorkshop == _workshop)
                {
                    pairs.Add((npcData.Name.ToString(), npcEntity));
                }
            }

            pairs.Sort((a, b) =>
            {
                int c = string.Compare(a.name, b.name, StringComparison.Ordinal);
                if (c != 0) return c;
                return a.ent.Index.CompareTo(b.ent.Index);
            });

            if (pairs.Count == 0)
            {
                assignDropdown.AddOptions(new List<string> { "Нет доступных" });
                confirmAssignButton.interactable = false;
                assignDropdown.SetValueWithoutNotify(0);
                _isBinding = false;
                return;
            }

            var options = new List<string>(pairs.Count);
            foreach (var p in pairs)
            {
                _availableNpcsForDropdown.Add(p.ent);
                options.Add(p.name);
            }
            assignDropdown.AddOptions(options);

            int newIndex = 0;
            if (previouslySelected != Entity.Null)
            {
                int found = _availableNpcsForDropdown.IndexOf(previouslySelected);
                if (found >= 0) newIndex = found;
            }

            assignDropdown.SetValueWithoutNotify(newIndex);
            _lastSelectedNpc = _availableNpcsForDropdown[newIndex];

            _isBinding = false;
        }

        /// <summary>
        /// Обрабатывает изменение выбора в выпадающем списке.
        /// </summary>
        private void OnAssignDropdownChanged(int idx)
        {
            if (_isBinding) return;
            if (idx >= 0 && idx < _availableNpcsForDropdown.Count)
            {
                _lastSelectedNpc = _availableNpcsForDropdown[idx];
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки "назначить". Создает сущность-запрос
        /// AssignWorkerToStationRequest для назначения выбранного NPC на этот станок.
        /// </summary>
        private void OnConfirmAssign()
        {
            if (_availableNpcsForDropdown.Count == 0) return;
            int selectedIndex = assignDropdown.value;
            if (selectedIndex < 0 || selectedIndex >= _availableNpcsForDropdown.Count) return;

            var npcToAssign = _availableNpcsForDropdown[selectedIndex];

            _lastSelectedNpc = npcToAssign;
            _pendingNpcAssignment = npcToAssign;
            _pendingFramesLeft = EcsConfirmTimeoutFrames;

            var e = _em.CreateEntity();
            _em.AddComponentData(e, new AssignWorkerToStationRequest
            {
                Workshop = _workshop,
                SlotIndex = _slotIndex,
                NpcEntity = npcToAssign
            });
        }
        
        /// <summary>
        /// Обработчик нажатия кнопки "снять назначение". Создает сущность-запрос
        /// UnassignWorkerFromStationRequest для снятия NPC с этого станка.
        /// </summary>
        private void OnUnassign()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new UnassignWorkerFromStationRequest
            {
                Workshop = _workshop,
                SlotIndex = _slotIndex
            });
        }
    }
}