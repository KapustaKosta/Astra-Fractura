using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Research
{
    public class ResearchTreeAuthoring : MonoBehaviour
    {
        [SerializeField] private ResearchTreeDefinition treeDefinition;

        public ResearchTreeDefinition TreeDefinition => treeDefinition;

        private class Baker : Baker<ResearchTreeAuthoring>
        {
            public override void Bake(ResearchTreeAuthoring authoring)
            {
                if (authoring.TreeDefinition == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[ResearchTreeAuthoring] Tree definition is missing.", authoring);
#endif
                    return;
                }

                var technologies = authoring.TreeDefinition.Technologies;
                if (technologies == null || technologies.Count == 0)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[ResearchTreeAuthoring] Tree definition does not contain technologies.", authoring);
#endif
                    return;
                }

                var registryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent<ResearchRegistry>(registryEntity);

                var lookupBuffer = AddBuffer<ResearchLookupElement>(registryEntity);

                int rootId = -1;
                var seenIds = new HashSet<int>();

                foreach (var tech in technologies)
                {
                    if (tech == null) continue;

                    if (!seenIds.Add(tech.TechnologyId))
                    {
#if UNITY_EDITOR
                        Debug.LogError($"[ResearchTreeAuthoring] Duplicate technology id {tech.TechnologyId} detected.", tech);
#endif
                        continue;
                    }

                    var techEntity = CreateAdditionalEntity(TransformUsageFlags.None);

                    AddComponent(techEntity, new ResearchTechnologyData
                    {
                        TechnologyId = tech.TechnologyId,
                        Cost = math.max(0, tech.Cost)
                    });

                    AddComponent(techEntity, new ResearchTechnologyName
                    {
                        Value = new FixedString64Bytes(string.IsNullOrEmpty(tech.DisplayName) ? $"Tech {tech.TechnologyId}" : tech.DisplayName)
                    });

                    AddComponent(techEntity, new ResearchTechnologyLayout
                    {
                        Position = new float2(tech.TreePosition.x, tech.TreePosition.y)
                    });

                    AddComponent(techEntity, new ResearchTechnologyDescription
                    {
                        Value = new FixedString512Bytes(string.IsNullOrEmpty(tech.Description) ? string.Empty : tech.Description)
                    });

                    var prereqBuffer = AddBuffer<ResearchPrerequisiteElement>(techEntity);
                    if (tech.Prerequisites != null)
                    {
                        foreach (var prereq in tech.Prerequisites)
                        {
                            if (prereq == null) continue;
                            prereqBuffer.Add(new ResearchPrerequisiteElement
                            {
                                TechnologyId = prereq.TechnologyId
                            });
                        }
                    }

                    var effectBuffer = AddBuffer<ResearchEffectElement>(techEntity);
                    if (tech.Effects != null)
                    {
                        foreach (var effect in tech.Effects)
                        {
                            switch (effect.effectKind)
                            {
                                case ResearchEffectKind.UnlockItem:
                                    effectBuffer.Add(new ResearchEffectElement
                                    {
                                        Kind = ResearchEffectKind.UnlockItem,
                                        IntValue = effect.itemToUnlock != null ? effect.itemToUnlock.itemID : -1,
                                        FloatValue = 0f,
                                        Identifier = new FixedString64Bytes(string.Empty)
                                    });
                                    break;
                                case ResearchEffectKind.ApplyModifier:
                                    effectBuffer.Add(new ResearchEffectElement
                                    {
                                        Kind = ResearchEffectKind.ApplyModifier,
                                        IntValue = 0,
                                        FloatValue = effect.modifierValue,
                                        Identifier = new FixedString64Bytes(string.IsNullOrEmpty(effect.modifierId) ? string.Empty : effect.modifierId)
                                    });
                                    break;
                            }
                        }
                    }

                    if (tech.IsRoot)
                    {
                        rootId = tech.TechnologyId;
                    }

                    lookupBuffer.Add(new ResearchLookupElement
                    {
                        TechnologyId = tech.TechnologyId,
                        TechnologyEntity = techEntity
                    });
                }

                AddComponent(registryEntity, new ResearchTreeRoot
                {
                    TechnologyId = rootId
                });
            }
        }
    }
}
