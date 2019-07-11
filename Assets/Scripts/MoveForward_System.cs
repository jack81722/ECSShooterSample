using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class MoveForward_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities.WithAllReadOnly<MoveForward>().ForEach(
            (ref MoveForward movement, ref Translation translation) => 
            {
                float deltaTime = Time.deltaTime;
                translation = new Translation()
                {
                    Value = translation.Value + movement.direction * movement.speed * deltaTime
                };
            });
    }
}
