using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class Movement_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        float deltaTime = Time.deltaTime;
        Entities.WithAll<Movement>().ForEach(
            (ref Movement movement, ref Translation translation) =>
            {
                translation = new Translation()
                {
                    Value = translation.Value + movement.direction * movement.speed * deltaTime
                };
            });
    }
}
