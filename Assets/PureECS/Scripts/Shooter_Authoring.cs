using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class Shooter_Authoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public float fireRate;
    public int fireSpread;
    public float fireRange;
    public float bulletSpeed;
    public float bulletLifeTime;
    public GameObject bulletPrefab;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Shooter shooter = new Shooter()
        {
            fireTimer = fireRate,           // first shoot
            fireRate = fireRate,
            fireSpread = fireSpread,
            fireRange = fireRange,
            bulletSpeed = bulletSpeed,
            bulletLifeTime = bulletLifeTime,
            bulletPrefab = conversionSystem.GetPrimaryEntity(bulletPrefab)
        };
        dstManager.AddComponentData(entity, shooter);
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(bulletPrefab);
    }
}
