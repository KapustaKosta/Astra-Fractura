using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Research
{
    public class ResearchUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ResearchTreeDefinition treeDefinition;

        [Header("UI References")]
        [SerializeField] private GameObject researchPanel;
        [SerializeField] private RectTransform nodesContainer;
        [SerializeField] private RectTransform connectionsContainer;
        [SerializeField] private ResearchNodeWidget nodePrefab;
        [SerializeField] private Image connectionPrefab;
        [SerializeField] private TextMeshProUGUI researchPointsText;
        [SerializeField] private TextMeshProUGUI selectedTitleText;
        [SerializeField] private TextMeshProUGUI selectedDescriptionText;
        [SerializeField] private TextMeshProUGUI selectedCostText;
        [SerializeField] private TextMeshProUGUI selectedEffectsText;
        [SerializeField] private Button closeButton;

        private readonly List<ResearchNodeWidget> nodeInstances = new List<ResearchNodeWidget>();
        private readonly Dictionary<int, ResearchNodeWidget> nodesById = new Dictionary<int, ResearchNodeWidget>();
        private readonly List<RectTransform> connectionInstances = new List<RectTransform>();

        private EntityManager entityManager;
        private Entity playerEntity;
        private bool isInitialized;
        private ResearchTechnology selectedTechnology;

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => GameBridge.Instance?.HandleUICloseAction());
            }

            TryInitialize();
            if (researchPanel != null)
            {
                researchPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                TryInitialize();
                return;
            }

            var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
            if (gameStateQuery.IsEmpty)
            {
                return;
            }

            var gameStateEntity = gameStateQuery.GetSingletonEntity();
            bool isUIMode = entityManager.HasComponent<InUIMode>(gameStateEntity);
            UIType activeType = UIType.None;
            if (isUIMode && entityManager.HasComponent<UIState>(gameStateEntity))
            {
                activeType = entityManager.GetComponentData<UIState>(gameStateEntity).ActiveUIType;
            }

            bool shouldBeActive = isUIMode && activeType == UIType.Research;
            if (researchPanel != null && researchPanel.activeSelf != shouldBeActive)
            {
                researchPanel.SetActive(shouldBeActive);
                if (shouldBeActive)
                {
                    RebuildTree();
                    RefreshNodeStates(forceRefresh: true);
                }
            }

            if (researchPanel != null && researchPanel.activeSelf)
            {
                RefreshNodeStates(forceRefresh: false);
            }
        }

        private void LateUpdate()
        {
            if (!isInitialized)
            {
                return;
            }

            if (researchPanel != null && researchPanel.activeSelf)
            {
                RefreshNodeStates(forceRefresh: false);
            }
        }

        private void TryInitialize()
        {
            if (isInitialized)
            {
                return;
            }

            if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                return;
            }

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            if (playerQuery.IsEmpty)
            {
                return;
            }

            playerEntity = playerQuery.GetSingletonEntity();
            isInitialized = true;
        }

        private void RebuildTree()
        {
            foreach (var node in nodeInstances)
            {
                if (node != null)
                {
                    Destroy(node.gameObject);
                }
            }
            nodeInstances.Clear();
            nodesById.Clear();
            selectedTechnology = null;

            foreach (var connection in connectionInstances)
            {
                if (connection != null)
                {
                    Destroy(connection.gameObject);
                }
            }
            connectionInstances.Clear();
            
            UpdateSelectionPanel(0);

            if (treeDefinition == null || nodesContainer == null || nodePrefab == null)
            {
                return;
            }

            var technologies = treeDefinition.Technologies;
            if (technologies == null)
            {
                return;
            }

            foreach (var tech in technologies)
            {
                if (tech == null)
                {
                    continue;
                }

                var instance = Instantiate(nodePrefab, nodesContainer);
                var rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = tech.TreePosition;
                }

                instance.Initialize(tech, SelectTechnology, RequestResearch);
                nodeInstances.Add(instance);
                nodesById[tech.TechnologyId] = instance;
            }

            if (connectionPrefab != null && connectionsContainer != null)
            {
                foreach (var tech in technologies)
                {
                    if (tech == null || tech.Prerequisites == null)
                    {
                        continue;
                    }

                    foreach (var prereq in tech.Prerequisites)
                    {
                        if (prereq == null || !nodesById.TryGetValue(tech.TechnologyId, out var toNode) || !nodesById.TryGetValue(prereq.TechnologyId, out var fromNode))
                        {
                            continue;
                        }

                        CreateConnection(fromNode.GetComponent<RectTransform>(), toNode.GetComponent<RectTransform>());
                    }
                }
            }
        }

        private void RefreshNodeStates(bool forceRefresh)
        {
            if (!isInitialized)
            {
                return;
            }

            if (!entityManager.Exists(playerEntity))
            {
                TryInitialize();
                return;
            }

            if (!entityManager.HasComponent<ResearchState>(playerEntity))
            {
                return;
            }

            bool shouldRefresh = forceRefresh || entityManager.HasComponent<ResearchStateDirty>(playerEntity);
            if (!shouldRefresh)
            {
                return;
            }

            var researchState = entityManager.GetComponentData<ResearchState>(playerEntity);
            if (researchPointsText != null)
            {
                researchPointsText.text = researchState.ResearchPoints.ToString();
            }

            var unlockedTechIds = new HashSet<int>();
            if (entityManager.HasBuffer<UnlockedResearchTechnology>(playerEntity))
            {
                var techBuffer = entityManager.GetBuffer<UnlockedResearchTechnology>(playerEntity);
                for (int i = 0; i < techBuffer.Length; i++)
                {
                    unlockedTechIds.Add(techBuffer[i].TechnologyId);
                }
            }

            foreach (var node in nodeInstances)
            {
                if (node == null || node.Technology == null)
                {
                    continue;
                }

                var tech = node.Technology;
                ResearchNodeState state = ResearchNodeState.Available;

                if (unlockedTechIds.Contains(tech.TechnologyId))
                {
                    state = ResearchNodeState.Completed;
                }
                else if (!PrerequisitesMet(tech, unlockedTechIds))
                {
                    state = ResearchNodeState.Locked;
                }

                bool canAfford = researchState.ResearchPoints >= tech.Cost;
                node.SetState(state, canAfford);
                node.SetSelected(selectedTechnology == tech);
            }

            UpdateSelectionPanel(researchState.ResearchPoints);

            if (entityManager.HasComponent<ResearchStateDirty>(playerEntity))
            {
                entityManager.RemoveComponent<ResearchStateDirty>(playerEntity);
            }
        }

        private void SelectTechnology(ResearchTechnology technology)
        {
            selectedTechnology = technology;
            foreach (var node in nodeInstances)
            {
                if (node != null)
                {
                    node.SetSelected(node.Technology == selectedTechnology);
                }
            }

            if (entityManager.HasComponent<ResearchState>(playerEntity))
            {
                var state = entityManager.GetComponentData<ResearchState>(playerEntity);
                UpdateSelectionPanel(state.ResearchPoints);
            }
            else
            {
                UpdateSelectionPanel(0);
            }
        }

        private void RequestResearch(ResearchTechnology technology)
        {
            if (technology == null || !isInitialized)
            {
                return;
            }

            if (!entityManager.HasComponent<ResearchState>(playerEntity))
            {
                return;
            }

            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new StartResearchRequest { TechnologyId = technology.TechnologyId });
        }

        private void UpdateSelectionPanel(int currentPoints)
        {
            if (selectedTitleText != null)
            {
                selectedTitleText.text = selectedTechnology != null ? selectedTechnology.DisplayName : string.Empty;
            }

            if (selectedDescriptionText != null)
            {
                selectedDescriptionText.text = selectedTechnology != null ? selectedTechnology.Description : string.Empty;
            }

            if (selectedCostText != null)
            {
                selectedCostText.text = selectedTechnology != null ? $"Cost: {selectedTechnology.Cost}" : string.Empty;
            }

            if (selectedEffectsText != null)
            {
                selectedEffectsText.text = BuildEffectsDescription(selectedTechnology, currentPoints);
            }
        }

        private string BuildEffectsDescription(ResearchTechnology technology, int currentPoints)
        {
            if (technology == null)
            {
                return string.Empty;
            }

            var effects = technology.Effects;
            if (effects == null || effects.Count == 0)
            {
                return "No effects";
            }

            var builder = new StringBuilder();
            foreach (var effect in effects)
            {
                switch (effect.effectKind)
                {
                    case ResearchEffectKind.UnlockItem:
                        builder.Append("Unlocks ");
                        builder.Append(effect.itemToUnlock != null ? effect.itemToUnlock.itemName : "unknown item");
                        builder.AppendLine();
                        break;
                    case ResearchEffectKind.ApplyModifier:
                        builder.Append("Modifier ");
                        builder.Append(string.IsNullOrEmpty(effect.modifierId) ? "(unnamed)" : effect.modifierId);
                        builder.Append(" -> ");
                        builder.Append(effect.modifierValue.ToString("0.##"));
                        builder.AppendLine();
                        break;
                }
            }

            builder.Append("Current points: ");
            builder.Append(currentPoints);
            return builder.ToString();
        }

        private static bool PrerequisitesMet(ResearchTechnology technology, HashSet<int> unlockedIds)
        {
            var prerequisites = technology.Prerequisites;
            if (prerequisites == null || prerequisites.Count == 0)
            {
                return true;
            }

            foreach (var prereq in prerequisites)
            {
                if (prereq == null)
                {
                    continue;
                }

                if (!unlockedIds.Contains(prereq.TechnologyId))
                {
                    return false;
                }
            }

            return true;
        }

        private void CreateConnection(RectTransform from, RectTransform to)
        {
            if (from == null || to == null || connectionPrefab == null || connectionsContainer == null)
            {
                return;
            }

            Vector2 startCenter = from.anchoredPosition;
            Vector2 endCenter = to.anchoredPosition;
            Vector2 direction = endCenter - startCenter;
            if (direction.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Vector2 directionNormalized = direction.normalized;
            Vector2 startOffset = CalculateEdgeOffset(from, directionNormalized);
            Vector2 endOffset = CalculateEdgeOffset(to, -directionNormalized);

            Vector2 startPoint = startCenter + startOffset;
            Vector2 endPoint = endCenter + endOffset;
            Vector2 finalDirection = endPoint - startPoint;
            float distance = finalDirection.magnitude;
            if (distance < 1e-3f)
            {
                return;
            }

            var connection = Instantiate(connectionPrefab, connectionsContainer);
            var lineRect = connection.rectTransform;
            lineRect.anchoredPosition = (startPoint + endPoint) * 0.5f;
            lineRect.sizeDelta = new Vector2(distance, lineRect.sizeDelta.y);
            float angle = Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            connectionInstances.Add(lineRect);
        }

        private static Vector2 CalculateEdgeOffset(RectTransform rect, Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Vector2.zero;
            }

            Vector2 extents = rect.rect.size * 0.5f;
            float absX = Mathf.Abs(direction.x);
            float absY = Mathf.Abs(direction.y);

            float tx = absX > 1e-5f ? extents.x / absX : float.PositiveInfinity;
            float ty = absY > 1e-5f ? extents.y / absY : float.PositiveInfinity;
            float t = Mathf.Min(tx, ty);

            if (float.IsInfinity(t))
            {
                t = 0f;
            }

            return direction.normalized * t;
        }
    }
}
