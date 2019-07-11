using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class TimeUpDestroy_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        float deltaTime = Time.deltaTime;
        Entities.WithAllReadOnly<TimeUpDestory>().ForEach(
        (Entity id, ref TimeUpDestory timeup) =>
        {
            timeup.timer += deltaTime;
            
            if(timeup.timer >= timeup.lifeTime)
            {
                PostUpdateCommands.RemoveComponent<TimeUpDestory>(id);
                PostUpdateCommands.AddComponent(id, new TimeUpUsable());
            }
        });


    }
}
