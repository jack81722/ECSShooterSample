using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MyECS.Physics
{
    public class BoxCollider_Authoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public short group = (short)BulletSharp.CollisionFilterGroups.DefaultFilter;
        public short mask = (short)BulletSharp.CollisionFilterGroups.AllFilter;
        public Vector3 halfWidth = new Vector3(0.5f, 0.5f, 0.5f);

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new BoxCollider()
            {
                group = group,
                mask = mask,
                halfWidth = new float3(halfWidth.x, halfWidth.y, halfWidth.z)
            });
        }
    }

    [Serializable]
    public struct BoxCollider : IComponentData
    {
        public short group;
        public short mask;
        public float3 halfWidth;
    }
}