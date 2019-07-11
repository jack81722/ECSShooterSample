using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class MoveForward_Authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float speed;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new MoveForward() {
            direction = new Unity.Mathematics.float3(transform.forward),
            speed = speed };
        dstManager.AddComponentData(entity, data);
    }
}
