using System.Collections.Generic;
using UnityEngine;

namespace Game.Research
{
    [CreateAssetMenu(fileName = "ResearchTechnology", menuName = "Research/Technology")]
    public class ResearchTechnology : ScriptableObject
    {
        [SerializeField] private int technologyId = -1;
        [SerializeField] private string displayName = "New Technology";
        [SerializeField] private Sprite icon;
        [SerializeField] private int cost = 0;
        [SerializeField] private bool isRoot = false;
        [SerializeField] private Vector2 treePosition;
        [SerializeField] private string description;
        [SerializeField] private List<ResearchTechnology> prerequisites = new List<ResearchTechnology>();
        [SerializeField] private List<ResearchEffectDefinition> effects = new List<ResearchEffectDefinition>();

        public int TechnologyId => technologyId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Cost => cost;
        public bool IsRoot => isRoot;
        public Vector2 TreePosition => treePosition;
        public string Description => description;
        public IReadOnlyList<ResearchTechnology> Prerequisites => prerequisites;
        public IReadOnlyList<ResearchEffectDefinition> Effects => effects;
    }

    public enum ResearchEffectKind
    {
        UnlockItem,
        ApplyModifier
    }

    [System.Serializable]
    public struct ResearchEffectDefinition
    {
        public ResearchEffectKind effectKind;
        public Item itemToUnlock;
        public string modifierId;
        public float modifierValue;
    }
}
