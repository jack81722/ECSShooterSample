using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[System.Serializable]
public struct Shooter : IComponentData
{
    public float fireTimer;
    public float fireRate;
    public int fireSpread; // 1, 3, 5, 7, 9
    public float fireRange;

    public float bulletSpeed;
    public float bulletLifeTime;

    public Entity bulletPrefab;
}
