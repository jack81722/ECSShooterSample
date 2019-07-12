using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class LifeTime_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        float deltaTime = Time.deltaTime;
        Entities.WithAll<LifeTime>().ForEach(
            (Entity id, ref LifeTime life) =>
            {
                life.timer += deltaTime;

                if(life.timer >= life.lifeTime)
                {
                    PostUpdateCommands.RemoveComponent<LifeTime>(id);
                    PostUpdateCommands.AddComponent(id, new LifeEnd());
                }
            });
    }
}
