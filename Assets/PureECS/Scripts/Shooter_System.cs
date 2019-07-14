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
                int centerId = shooter.fireSpread;
                float angleSegment = (shooter.fireRange) / (shooter.fireSpread > 0 ? shooter.fireSpread : 1);
                int posIndex;
                for (int i = 0; i < shooter.fireSpread * 2 + 1; i++)
                {
                    var instance = CommandBuffer.Instantiate(index, shooter.bulletPrefab);
                    posIndex = i - centerId;
                    CommandBuffer.SetComponent(index, instance, new Translation() {
                        Value = new float3(
                            location.Position.x, 
                            location.Position.y, 
                            location.Position.z)});
                    var currentAngle = (angleSegment * posIndex + 90) * Mathf.PI / 180f;
                    //Debug.Log(currentAngle);
                    var direct = new float3(Mathf.Cos(currentAngle), 0, Mathf.Sin(currentAngle));
                    //Debug.Log(direct);
                    CommandBuffer.SetComponent(index, instance, new Movement() { direction = direct, speed = shooter.bulletSpeed });
                    shooter.fireTimer = 0;
                }
            }
        }
    }
}



