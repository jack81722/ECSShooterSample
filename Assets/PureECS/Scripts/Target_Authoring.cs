using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class Target_Authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Target());
    }
}

[System.Serializable]
public struct Target : IComponentData
{
    int i;
}

