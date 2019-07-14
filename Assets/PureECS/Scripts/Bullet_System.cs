using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class Bullet_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        float deltaTime = Time.deltaTime;
        Entities.WithAll<Bullet, Movement>().ForEach(
            (ref Movement movement, ref Translation translation) =>
            {
                translation.Value += movement.direction * movement.speed * deltaTime;
            });

        
    }
}
