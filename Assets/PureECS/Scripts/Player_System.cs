using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(Movement_System))]
public class Player_System : ComponentSystem
{
    protected override void OnUpdate()
    {
        float deltaTime = Time.deltaTime;
        Entities.WithAll<Character, Movement, Controllable>().ForEach(
            (ref Movement movement) =>
            {
                movement.direction = new float3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            });
    }
    
}
