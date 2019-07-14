using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class LifeTime_Authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float lifeTime;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new LifeTime() { lifeTime = lifeTime });
    }
}
