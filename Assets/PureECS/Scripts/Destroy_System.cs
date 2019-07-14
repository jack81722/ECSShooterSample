using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class Destroy_System : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    protected override void OnCreate()
    {   
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {   
        var job = new LifeEndJob()
        {
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);
        return job;
    }

    struct LifeEndJob : IJobForEachWithEntity<Destruction, LocalToWorld>
    {   
        public EntityCommandBuffer.Concurrent CommandBuffer;

        [BurstCompile]
        public void Execute(Entity entity, int index, [ReadOnly]ref Destruction lifeEnd, ref LocalToWorld location)
        {
            CommandBuffer.DestroyEntity(index, entity);
        }
    }
}
