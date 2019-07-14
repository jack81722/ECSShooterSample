using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class Character_Authoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public int Id;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var data = new Character() { id = Id };
        dstManager.AddComponentData(entity, data);
    }
}
