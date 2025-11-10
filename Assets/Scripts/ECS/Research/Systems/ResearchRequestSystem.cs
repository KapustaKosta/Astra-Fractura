using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Research
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ResearchPointGenerationSystem))]
    public partial struct ResearchRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResearchRegistry>();
            state.RequireForUpdate<ResearchState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var requestQuery = state.GetEntityQuery(ComponentType.ReadOnly<StartResearchRequest>());
            if (requestQuery.IsEmpty)
            {
                return;
            }

            var registryEntity = SystemAPI.GetSingletonEntity<ResearchRegistry>();
            var lookup = SystemAPI.GetBuffer<ResearchLookupElement>(registryEntity);
            var playerEntity = SystemAPI.GetSingletonEntity<ResearchState>();
            var researchState = SystemAPI.GetComponentRW<ResearchState>(playerEntity);
            var unlockedTech = SystemAPI.GetBuffer<UnlockedResearchTechnology>(playerEntity);
            var unlockedItems = SystemAPI.GetBuffer<UnlockedResearchItem>(playerEntity);
            var modifiers = SystemAPI.GetBuffer<ActiveResearchModifier>(playerEntity);

            using var requests = requestQuery.ToEntityArray(Allocator.Temp);

            foreach (var requestEntity in requests)
            {
                var request = SystemAPI.GetComponent<StartResearchRequest>(requestEntity);
                Entity techEntity = FindTechnologyEntity(request.TechnologyId, lookup);
                if (techEntity == Entity.Null)
                {
                    CreateNotification(ref state, "Technology data missing");
                    state.EntityManager.DestroyEntity(requestEntity);
                    continue;
                }

                if (IsTechnologyUnlocked(request.TechnologyId, unlockedTech))
                {
                    state.EntityManager.DestroyEntity(requestEntity);
                    continue;
                }

                if (!PrerequisitesSatisfied(techEntity, unlockedTech, ref state))
                {
                    CreateNotification(ref state, "Prerequisites not met");
                    state.EntityManager.DestroyEntity(requestEntity);
                    continue;
                }

                var techData = SystemAPI.GetComponent<ResearchTechnologyData>(techEntity);
                if (researchState.ValueRO.ResearchPoints < techData.Cost)
                {
                    CreateNotification(ref state, "Not enough research points");
                    state.EntityManager.DestroyEntity(requestEntity);
                    continue;
                }

                researchState.ValueRW.ResearchPoints -= techData.Cost;
                unlockedTech.Add(new UnlockedResearchTechnology { TechnologyId = techData.TechnologyId });

                ApplyEffects(techEntity, unlockedItems, modifiers, ref state);

                if (!SystemAPI.HasComponent<ResearchStateDirty>(playerEntity))
                {
                    state.EntityManager.AddComponent<ResearchStateDirty>(playerEntity);
                }

                state.EntityManager.DestroyEntity(requestEntity);
            }
        }

        private static Entity FindTechnologyEntity(int techId, DynamicBuffer<ResearchLookupElement> lookup)
        {
            foreach (var entry in lookup)
            {
                if (entry.TechnologyId == techId)
                {
                    return entry.TechnologyEntity;
                }
            }

            return Entity.Null;
        }

        private static bool IsTechnologyUnlocked(int techId, DynamicBuffer<UnlockedResearchTechnology> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].TechnologyId == techId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PrerequisitesSatisfied(Entity techEntity, DynamicBuffer<UnlockedResearchTechnology> unlocked, ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<ResearchPrerequisiteElement>(techEntity))
            {
                return true;
            }

            var prereqs = state.EntityManager.GetBuffer<ResearchPrerequisiteElement>(techEntity);
            for (int i = 0; i < prereqs.Length; i++)
            {
                if (!IsTechnologyUnlocked(prereqs[i].TechnologyId, unlocked))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ApplyEffects(Entity techEntity, DynamicBuffer<UnlockedResearchItem> unlockedItems, DynamicBuffer<ActiveResearchModifier> modifiers, ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<ResearchEffectElement>(techEntity))
            {
                return;
            }

            var effects = state.EntityManager.GetBuffer<ResearchEffectElement>(techEntity);
            foreach (var effect in effects)
            {
                switch (effect.Kind)
                {
                    case ResearchEffectKind.UnlockItem:
                        if (effect.IntValue >= 0 && !ContainsItem(unlockedItems, effect.IntValue))
                        {
                            unlockedItems.Add(new UnlockedResearchItem { ItemId = effect.IntValue });
                        }
                        break;
                    case ResearchEffectKind.ApplyModifier:
                        ApplyModifier(modifiers, effect.Identifier, effect.FloatValue);
                        break;
                }
            }
        }

        private static bool ContainsItem(DynamicBuffer<UnlockedResearchItem> buffer, int itemId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyModifier(DynamicBuffer<ActiveResearchModifier> modifiers, FixedString64Bytes identifier, float value)
        {
            if (identifier.Length == 0)
            {
                return;
            }

            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Identifier.Equals(identifier))
                {
                    modifiers[i] = new ActiveResearchModifier
                    {
                        Identifier = identifier,
                        Value = value
                    };
                    return;
                }
            }

            modifiers.Add(new ActiveResearchModifier
            {
                Identifier = identifier,
                Value = value
            });
        }

        private static void CreateNotification(ref SystemState state, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new UINotificationRequest { Message = new FixedString128Bytes(message) });
        }
    }
}
