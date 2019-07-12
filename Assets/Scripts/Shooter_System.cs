using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public class Shooter_System : JobComponentSystem
{
    BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;
    

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        bool shoot = Input.GetButton("Jump");
        var job = new SpawnBulletJob()
        {
            DeltaTime = Time.deltaTime,
            Shoot = shoot,
            CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, inputDeps);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(job);
        return job;
    }

    struct SpawnBulletJob : IJobForEachWithEntity<Shooter, LocalToWorld>
    {
        public float DeltaTime;
        public bool Shoot;
        public EntityCommandBuffer.Concurrent CommandBuffer;

        [BurstCompile]
        public void Execute(Entity entity, int index, ref Shooter shooter, ref LocalToWorld location)
        {
            shooter.fireTimer += DeltaTime;
            if (Shoot && shooter.fireTimer >= shooter.fireRate)
            {
                var instance = CommandBuffer.Instantiate(index, shooter.bulletPrefab);
                CommandBuffer.SetComponent(index, instance, new Translation() { Value = location.Position });
                CommandBuffer.SetComponent(index, instance, new Movement() { direction = new float3(0, 0, 1), speed = shooter.bulletSpeed });
                shooter.fireTimer = 0;
            }
        }
    }
}



