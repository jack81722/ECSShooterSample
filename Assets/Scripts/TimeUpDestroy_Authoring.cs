using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class TimeUpDestroy_Authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float lifeTime;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new TimeUpDestory
        {
            timer = 0,
            lifeTime = lifeTime
        };
        dstManager.AddComponentData(entity, data);
    }
}
