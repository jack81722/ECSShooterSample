using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class Movement_Authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Vector3 direct;
    public float speed;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new Movement() {
            direction = new Unity.Mathematics.float3(direct),
            speed = speed };
        dstManager.AddComponentData(entity, data);
    }
}
