using System.Collections.Generic;
using UnityEngine;

namespace Game.Research
{
    [CreateAssetMenu(fileName = "ResearchTree", menuName = "Research/Tree")]
    public class ResearchTreeDefinition : ScriptableObject
    {
        [SerializeField] private List<ResearchTechnology> technologies = new List<ResearchTechnology>();

        public IReadOnlyList<ResearchTechnology> Technologies => technologies;
    }
}
