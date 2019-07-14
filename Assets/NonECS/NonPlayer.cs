using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NonPlayer : MonoBehaviour
{
    private Vector3 position;
    public Vector3 direction;
    public float speed = 5;

    public NonBullet bulletPrefab;
    public int InitialSuppleCount = 100;
    public float bulletSpeed = 10;
    public float bulletLife = 1f;
    private float fireTimer = 0;
    public float fireRate = 0.1f;
    public int fireSpread = 1;
    public float fireRange = 30f;

    NonBulletPool bulletPool;
    
    // Start is called before the first frame update
    void Start()
    {
        bulletPool = new NonBulletPool(bulletPrefab);
        bulletPool.Supple(InitialSuppleCount);
        fireTimer = 0;
    }

    // Update is called once per frame
    void Update()
    {
        // movement
        float deltaTime = Time.deltaTime;
        direction = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        position += direction * speed * deltaTime;

        // fire
        fireTimer += deltaTime;
        if (Input.GetButton("Jump"))
        {
            if (fireTimer >= fireRate)
            {
                Fire();
                fireTimer = 0;
            }
        }
    }

    private void LateUpdate()
    {
        transform.position = position;
    }

    public void Fire()
    {
        float angleSegment = fireRange / (fireSpread > 0 ? fireSpread: 1);
        //Debug.Log(angleSegment);
        for (int i = 0; i < fireSpread * 2 + 1; i++)
        {
            var bullet = bulletPool.Get(position, bulletLife);
            int posIndex = i - fireSpread;
            float currentAngle = (angleSegment * posIndex + 90) * Mathf.PI / 180f;
            Vector3 direct = new Vector3(Mathf.Cos(currentAngle), 0, Mathf.Sin(currentAngle));
            bullet.direction = direct;
            bullet.speed = bulletSpeed;
        }
        
    }
}

public class NonBulletPool
{
    NonBullet prefab;
    Queue<NonBullet> usable;
    List<NonBullet> used;

    public int UsableCount { get { return usable.Count; } }
    public int TotalCount { get { return usable.Count + used.Count; } }

    public NonBulletPool(NonBullet prefab)
    {
        this.prefab = prefab;
        usable = new Queue<NonBullet>();
        used = new List<NonBullet>();
    }

    public void Supple(int count)
    {
        for(int i = 0; i < count; i++)
        {
            NonBullet stuff = GameObject.Instantiate<NonBullet>(prefab);
            stuff.pool = this;
            stuff.gameObject.SetActive(false);
            usable.Enqueue(stuff);
        }
    }

    public NonBullet Get(Vector3 position, float life)
    {
        if(usable.Count <= 0)
        {
            for (int i = 0; i < TotalCount; i++)
            {
                NonBullet stuff = GameObject.Instantiate<NonBullet>(prefab);
                stuff.pool = this;
                stuff.gameObject.SetActive(false);
                usable.Enqueue(stuff);
            }
        }
        NonBullet bullet = usable.Dequeue();
        bullet.transform.position = position;
        bullet.gameObject.SetActive(true);
        bullet.ResetLife(life);
        used.Add(bullet);
        return bullet;
    }

    public void Recycle(NonBullet bullet)
    {
        used.Remove(bullet);
        usable.Enqueue(bullet);
        bullet.gameObject.SetActive(false);
    }
}
