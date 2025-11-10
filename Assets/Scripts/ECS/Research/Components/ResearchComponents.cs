using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Research
{
    public struct ResearchRegistry : IComponentData { }

    public struct ResearchTreeRoot : IComponentData
    {
        public int TechnologyId;
    }

    public struct ResearchTechnologyData : IComponentData
    {
        public int TechnologyId;
        public int Cost;
    }

    public struct ResearchTechnologyName : IComponentData
    {
        public FixedString64Bytes Value;
    }

    public struct ResearchTechnologyDescription : IComponentData
    {
        public FixedString512Bytes Value;
    }

    public struct ResearchTechnologyLayout : IComponentData
    {
        public float2 Position;
    }

    public struct ResearchLookupElement : IBufferElementData
    {
        public int TechnologyId;
        public Entity TechnologyEntity;
    }

    public struct ResearchPrerequisiteElement : IBufferElementData
    {
        public int TechnologyId;
    }

    public struct ResearchEffectElement : IBufferElementData
    {
        public ResearchEffectKind Kind;
        public int IntValue;
        public float FloatValue;
        public FixedString64Bytes Identifier;
    }

    public struct ResearchState : IComponentData
    {
        public int ResearchPoints;
    }

    public struct ResearchPointAccumulator : IComponentData
    {
        public double LastTickTime;
        public float FractionalRemainder;
    }

    public struct ResearchPointSource : IComponentData
    {
        public float PointsPerSecond;
    }

    public struct UnlockedResearchTechnology : IBufferElementData
    {
        public int TechnologyId;
    }

    public struct UnlockedResearchItem : IBufferElementData
    {
        public int ItemId;
    }

    public struct ActiveResearchModifier : IBufferElementData
    {
        public FixedString64Bytes Identifier;
        public float Value;
    }

    public struct ResearchStateDirty : IComponentData { }
}
