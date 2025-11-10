using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Research
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ResearchBootstrapSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResearchRegistry>();
            state.RequireForUpdate<ResearchState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                return;
            }

            var registryEntity = SystemAPI.GetSingletonEntity<ResearchRegistry>();
            var root = SystemAPI.GetComponent<ResearchTreeRoot>(registryEntity);
            if (root.TechnologyId < 0)
            {
                _initialized = true;
                return;
            }

            var lookup = SystemAPI.GetBuffer<ResearchLookupElement>(registryEntity);
            Entity rootEntity = Entity.Null;
            foreach (var entry in lookup)
            {
                if (entry.TechnologyId == root.TechnologyId)
                {
                    rootEntity = entry.TechnologyEntity;
                    break;
                }
            }

            if (rootEntity == Entity.Null)
            {
                _initialized = true;
                return;
            }

            var playerEntity = SystemAPI.GetSingletonEntity<ResearchState>();
            var unlockedTech = SystemAPI.GetBuffer<UnlockedResearchTechnology>(playerEntity);

            bool alreadyUnlocked = false;
            foreach (var tech in unlockedTech)
            {
                if (tech.TechnologyId == root.TechnologyId)
                {
                    alreadyUnlocked = true;
                    break;
                }
            }

            if (!alreadyUnlocked)
            {
                unlockedTech.Add(new UnlockedResearchTechnology { TechnologyId = root.TechnologyId });
                ApplyTechnologyEffects(rootEntity, playerEntity, ref state);
                if (!SystemAPI.HasComponent<ResearchStateDirty>(playerEntity))
                {
                    state.EntityManager.AddComponent<ResearchStateDirty>(playerEntity);
                }
            }

            _initialized = true;
        }

        private static void ApplyTechnologyEffects(Entity techEntity, Entity playerEntity, ref SystemState state)
        {
            if (!state.EntityManager.HasBuffer<ResearchEffectElement>(techEntity))
            {
                return;
            }

            var effectBuffer = state.EntityManager.GetBuffer<ResearchEffectElement>(techEntity);
            var unlockedItems = state.EntityManager.GetBuffer<UnlockedResearchItem>(playerEntity);
            var modifiers = state.EntityManager.GetBuffer<ActiveResearchModifier>(playerEntity);

            foreach (var effect in effectBuffer)
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

        private static void ApplyModifier(DynamicBuffer<ActiveResearchModifier> buffer, FixedString64Bytes identifier, float value)
        {
            if (identifier.Length == 0)
            {
                return;
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Identifier.Equals(identifier))
                {
                    buffer[i] = new ActiveResearchModifier
                    {
                        Identifier = identifier,
                        Value = value
                    };
                    return;
                }
            }

            buffer.Add(new ActiveResearchModifier
            {
                Identifier = identifier,
                Value = value
            });
        }
    }
}
