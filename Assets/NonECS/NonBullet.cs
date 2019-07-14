using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NonBullet : MonoBehaviour
{
    public Vector3 direction;
    public float speed;

    public NonBulletPool pool;
    public float lifeTimer;
    public float lifeTime;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifeTime)
        {
            if (pool == null)
                Destroy(gameObject);
            else
            {
                pool.Recycle(this);
            }
        }
    }

    public void ResetLife(float life)
    {
        lifeTimer = 0;
        lifeTime = life;
    }

    private void LateUpdate()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    

    private void OnTriggerEnter(Collider collision)
    {
        if (pool == null)
            Destroy(gameObject);
        else
            pool.Recycle(this);
    }
}
