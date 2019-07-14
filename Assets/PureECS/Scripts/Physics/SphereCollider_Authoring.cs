using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace MyECS.Physics
{
    public class SphereCollider_Authoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public short group = (short)BulletSharp.CollisionFilterGroups.DefaultFilter;
        public short mask = (short)BulletSharp.CollisionFilterGroups.AllFilter;
        public float radius = 0.5f;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new SphereCollider()
            {
                group = group,
                mask = mask,
                radius = radius
            });
        }
    }

    [Serializable]
    public struct SphereCollider : IComponentData
    {
        public short group;
        public short mask;
        public float radius;
    }
}