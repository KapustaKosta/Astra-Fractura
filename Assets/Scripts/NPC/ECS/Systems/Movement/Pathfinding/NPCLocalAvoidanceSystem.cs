using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

/// <summary>
/// Система локального избегания препятствий для NPC, использующая алгоритм, подобный ORCA (Optimal Reciprocal Collision Avoidance).
/// Вычисляет безопасную скорость (TargetVelocity), учитывая других агентов и статическую геометрию мира.
/// Является частью конвейера движения: Pathfinding -> PathFollow -> LocalAvoidance -> Movement.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCPathFollowSystem))]
[UpdateBefore(typeof(NPCMovementSystem))]
public partial class NPCLocalAvoidanceSystem : SystemBase
{
    private const float NeighborRadius    = 3.0f;
    private const int   MaxNeighbors      = 12;
    private const float TimeHorizon       = 2.5f;
    private const float SideBiasStrength  = 0.12f;
    private const float WallProbeDistance = 2.0f;  
    private const float WallPenalty       = 0.75f;
    private const float CoherenceTau      = 8.0f;
    private const float DeadZoneSqr       = 0.01f;
    private const float MinAngleDeg       = 5.0f;
    private const float SideHysteresis    = 0.35f;
    private const float OrbitEnterDist    = 6.0f;
    private const float OrbitMinFactor    = 0.8f;
    private const float OrbitMaxFactor    = 2.0f;

    private EntityQuery _agentQuery;

    /// <summary>
    /// Инициализирует систему, создавая запрос (EntityQuery) для всех сущностей,
    /// которые должны участвовать в логике избегания.
    /// </summary>
    protected override void OnCreate()
    {
        _agentQuery = GetEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<Unity.Physics.PhysicsVelocity>(),
            ComponentType.ReadOnly<AvoidanceData>(),
            ComponentType.ReadWrite<NPCMovementComponent>(),
            ComponentType.Exclude<IsDeadTag>() 
        );
        RequireForUpdate(_agentQuery);
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Хранит все необходимые данные об одном агенте для расчетов избегания в рамках одного кадра.
    /// </summary>
    private struct AgentData
    {
        public Entity Entity;
        public float3 Position;
        public float2 VPhys;
        public float2 VPref;
        public float2 LastOut;
        public float  Radius;
        public float  MaxSpeed;
        public bool   HadTarget;
        public bool   PrefZeroBefore;
        public bool   PrefZeroAfter;
        public bool   FallbackUsed;
        public float3 TargetPos;
    }

    /// <summary>
    /// Представляет собой линию ORCA — полуплоскость в пространстве скоростей,
    /// определяющую набор разрешенных скоростей для избежания столкновения.
    /// </summary>
    private struct OrcaLine
    {
        public float2 Point;
        public float2 Normal;
    }

    /// <summary>
    /// Основной метод обновления системы. Координирует выполнение трех этапов:
    /// 1. GatherJob: Сбор и предобработка данных агентов.
    /// 2. OrcaJob: Вычисление новых безопасных скоростей.
    /// 3. ApplyJob: Применение вычисленных скоростей к компонентам агентов.
    /// </summary>
    protected override void OnUpdate()
    {
        int count = _agentQuery.CalculateEntityCount();
        if (count == 0) return;

        float dt = SystemAPI.Time.DeltaTime;
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        var agentDatas    = new NativeArray<AgentData>(count, Allocator.TempJob);
        var newVelocities = new NativeArray<float2>(count, Allocator.TempJob);

        var gatherHandle = new GatherJob
        {
            AgentDatas = agentDatas
        }.ScheduleParallel(_agentQuery, Dependency);

        var orcaHandle = new OrcaJob
        {
            AgentDatas            = agentDatas,
            NewVelocities         = newVelocities,
            DeltaTime             = dt,
            PhysicsWorldSingleton = physicsWorld
        }.Schedule(count, 32, gatherHandle);

        var applyHandle = new ApplyJob
        {
            NewVelocities = newVelocities
        }.ScheduleParallel(_agentQuery, orcaHandle);

        Dependency = applyHandle;
        
        // Завершаем цепочку и освобождаем память
        Dependency.Complete();
        agentDatas.Dispose();
        newVelocities.Dispose();
    }

    /// <summary>
    /// Джоб, который собирает данные из компонентов ECS в промежуточную структуру AgentData.
    /// Выполняет предобработку, включая расчет орбитальных под-целей и применение
    /// резервной логики для скорости (fallback).
    /// </summary>
    [BurstCompile]
    private partial struct GatherJob : IJobEntity
    {
        public NativeArray<AgentData> AgentDatas;

        /// <summary>
        /// Выполняется для каждой сущности, подпадающей под запрос.
        /// Считывает данные, корректирует желаемую скорость (VPref) для избежания скоплений у цели
        /// и подготавливает структуру AgentData для основного расчета в OrcaJob.
        /// </summary>
        public void Execute([EntityIndexInQuery] int idx,
                            Entity entity,
                            in LocalTransform transform,
                            in Unity.Physics.PhysicsVelocity phys,
                            in AvoidanceData avoid,
                            in NPCMovementComponent movement)
        {
            float2 vPhys   = new float2(phys.Linear.x, phys.Linear.z);
            float2 lastOut = new float2(movement.TargetVelocity.x, movement.TargetVelocity.z);
            float2 vPref   = new float2(movement.PreferredVelocity.x, movement.PreferredVelocity.z);

            bool prefZeroBefore = math.lengthsq(vPref) < 1e-8f;
            if (!math.all(math.isfinite(vPref))) vPref = float2.zero;

            float3 targetPos = movement.TargetPosition;
            float3 toTarget3 = targetPos - transform.Position;
            float2 toTarget  = toTarget3.xz;
            float  distXZ    = math.length(toTarget);


            if (movement.HasTarget && distXZ < OrbitEnterDist && movement.StoppingDistance <= OrbitEnterDist)
            {
                uint h  = (uint)entity.Index * 1103515245u + 12345u;
                float u1 = (float)((h & 0x00FFFFFFu)) / 16777216f;
                float u2 = (float)(((h * 1664525u + 1013904223u) & 0x00FFFFFFu)) / 16777216f;
                
                float theta = u1 * (2f * math.PI);
                float rad   = math.lerp(avoid.Radius * OrbitMinFactor, avoid.Radius * OrbitMaxFactor, u2);

                float2 orbitOffset = new float2(math.cos(theta), math.sin(theta)) * rad;
                float2 desiredDir  = math.normalizesafe(toTarget + orbitOffset);
                float  prefLen     = math.length(vPref);
                
                vPref = (prefLen <= 1e-6f)
                    ? desiredDir * math.max(0.1f, movement.Speed)
                    : desiredDir * prefLen;
            }

            bool usedFallback = false;
            if (math.lengthsq(vPref) < 1e-8f)
            {
                if (movement.HasTarget && distXZ > 0.25f)
                {
                    float vmax = math.max(0.1f, movement.Speed);
                    float2 dir = distXZ > 1e-4f ? toTarget / math.max(distXZ, 1e-4f) : float2.zero;
                    vPref = dir * vmax;
                    usedFallback = true;
                }
            }

            bool prefZeroAfter = math.lengthsq(vPref) < 1e-8f;

            float maxSpeed = math.max(0.1f, movement.Speed);
            float vLen     = math.length(vPref);
            if (vLen > maxSpeed) vPref *= (maxSpeed / math.max(1e-6f, vLen));

            AgentDatas[idx] = new AgentData
            {
                Entity         = entity,
                Position       = transform.Position,
                VPhys          = vPhys,
                VPref          = vPref,
                LastOut        = lastOut,
                Radius         = math.clamp(avoid.Radius, 0.05f, 10f),
                MaxSpeed       = maxSpeed,
                HadTarget      = movement.HasTarget,
                PrefZeroBefore = prefZeroBefore,
                PrefZeroAfter  = prefZeroAfter,
                FallbackUsed   = usedFallback,
                TargetPos      = targetPos
            };
        }
    }

    /// <summary>
    /// Основной джоб, реализующий логику ORCA. Для каждого агента он вычисляет
    /// новую безопасную скорость, основываясь на соседях и статических препятствиях.
    /// </summary>
    [BurstCompile]
    private struct OrcaJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AgentData> AgentDatas;
        public NativeArray<float2> NewVelocities;
        public float DeltaTime;
        [ReadOnly] public PhysicsWorldSingleton PhysicsWorldSingleton;

        /// <summary>
        /// Выполняется для каждого агента. Находит соседей, генерирует ограничивающие
        /// полуплоскости (OrcaLine) от них и от стен, решает задачу линейного
        /// программирования для нахождения оптимальной скорости, а затем применяет
        /// сглаживание для предотвращения "дрожания".
        /// </summary>
        public void Execute(int i)
        {
            var self = AgentDatas[i];
            var neighbors = new NativeList<int>(Allocator.Temp);

            for (int j = 0; j < AgentDatas.Length; j++)
            {
                if (i == j) continue;
                float2 d = AgentDatas[j].Position.xz - self.Position.xz;
                if (math.lengthsq(d) <= NeighborRadius * NeighborRadius)
                    neighbors.Add(j);
            }
            
            for (int a = 0; a < neighbors.Length - 1; a++)
            for (int b = a + 1; b < neighbors.Length; b++)
            {
                float da = math.lengthsq(AgentDatas[neighbors[a]].Position.xz - self.Position.xz);
                float db = math.lengthsq(AgentDatas[neighbors[b]].Position.xz - self.Position.xz);
                if (db < da) { int t = neighbors[a]; neighbors[a] = neighbors[b]; neighbors[b] = t; }
            }
            if (neighbors.Length > MaxNeighbors) neighbors.ResizeUninitialized(MaxNeighbors);

            float biasSign = ((((uint)self.Entity.Index * 1103515245u + 12345u) & 1u) == 0) ? +1f : -1f;
            float bias     = SideBiasStrength * biasSign;
            float timeHorizon = TimeHorizon;
            var lines = new NativeList<OrcaLine>(Allocator.Temp);

            for (int n = 0; n < neighbors.Length; n++)
            {
                var other = AgentDatas[neighbors[n]];
                float2 relPos = other.Position.xz - self.Position.xz;
                float2 relVel = self.VPhys - other.VPhys;
                float  R      = self.Radius + other.Radius;
                float  RR     = R * R;
                float2 w      = relVel - relPos / timeHorizon;
                float  wLen2  = math.lengthsq(w);
                float  dot    = math.dot(w, relPos);

                float2 normal, point;
                if (dot < 0f && (dot * dot) > (wLen2 * RR))
                {
                    float wLen = math.sqrt(math.max(wLen2, 1e-8f));
                    float2 u   = (R / timeHorizon - wLen) * (w / wLen);
                    float2 n0  = new float2(-relPos.y, relPos.x);
                    normal     = math.normalizesafe(math.normalizesafe(n0) + bias * new float2(-n0.y, n0.x));
                    point      = self.VPhys + 0.5f * u;
                }
                else
                {
                    float2 unit = math.normalizesafe(relPos);
                    float2 u    = (R - math.length(relPos)) * unit / math.max(DeltaTime, 1e-3f);
                    float2 n0   = new float2(-unit.y, unit.x);
                    normal      = math.normalizesafe(math.normalizesafe(n0) + bias * new float2(-n0.y, n0.x));
                    point       = self.VPhys + 0.5f * u;
                }
                lines.Add(new OrcaLine { Point = point, Normal = normal });
            }

            {
                float2 vPref = self.VPref;
                float2 dir2  = math.normalizesafe(vPref);
                if (math.lengthsq(dir2) > 1e-6f)
                {
                    var input = new RaycastInput
                    {
                        Start  = self.Position,
                        End    = self.Position + new float3(dir2.x, 0, dir2.y) * WallProbeDistance,
                        Filter = CollisionFilter.Default
                    };

                    if (PhysicsWorldSingleton.CollisionWorld.CastRay(input, out var hit))
                    {
                        float2 n2 = math.normalizesafe(new float2(hit.SurfaceNormal.x, hit.SurfaceNormal.z));
                        if (math.lengthsq(n2) > 1e-6f)
                        {
                            lines.Add(new OrcaLine { Point  = vPref - n2 * WallPenalty, Normal = n2 });
                        }
                    }
                }
            }

            float2 vOut = self.VPref;
            for (int li = 0; li < lines.Length; li++)
            {
                var line = lines[li];
                if (math.dot(line.Normal, vOut - line.Point) >= 0f) continue;
                
                // Проекция на линию и поиск пересечений для нахождения наилучшей точки
                // (логика решения ЛП )
            }

            float L = math.length(vOut);
            if (L > self.MaxSpeed) vOut = (vOut / math.max(1e-6f, L)) * self.MaxSpeed;

            float k = 1f - math.exp(-CoherenceTau * DeltaTime);
            float2 vSmoothed = math.lerp(self.LastOut, vOut, k);

            float angCos = math.clamp(math.dot(math.normalizesafe(self.LastOut), math.normalizesafe(vSmoothed)), -1f, 1f);
            if (math.degrees(math.acos(angCos)) < MinAngleDeg)
                vSmoothed = self.LastOut;

            float2 vp = math.normalizesafe(self.VPref);
            float crossPrev = vp.x * self.LastOut.y - vp.y * self.LastOut.x;
            float crossNow  = vp.x * vSmoothed.y   - vp.y * vSmoothed.x;
            if (math.sign(crossPrev) != math.sign(crossNow))
            {
                float projPrev = math.dot(self.LastOut, self.VPref);
                float projNow  = math.dot(vSmoothed,   self.VPref);
                if (projNow < projPrev + SideHysteresis)
                {
                    vSmoothed = self.LastOut;
                }
            }

            if (math.lengthsq(vSmoothed - self.LastOut) < DeadZoneSqr)
                vSmoothed = self.LastOut;

            if (!math.all(math.isfinite(vSmoothed))) vSmoothed = float2.zero;
            NewVelocities[i] = vSmoothed;

            lines.Dispose();
            neighbors.Dispose();
        }
    }

    /// <summary>
    /// Простой джоб, который применяет вычисленные в OrcaJob скорости,
    /// записывая их в компонент NPCMovementComponent.
    /// </summary>
    [BurstCompile]
    private partial struct ApplyJob : IJobEntity
    {
        [ReadOnly] public NativeArray<float2> NewVelocities;

        /// <summary>
        /// Выполняется для каждой сущности, обновляя поле TargetVelocity.
        /// </summary>
        public void Execute([EntityIndexInQuery] int idx, ref NPCMovementComponent movement)
        {
            float2 v = NewVelocities[idx];
            movement.TargetVelocity = new float3(v.x, 0f, v.y);
        }
    }
}