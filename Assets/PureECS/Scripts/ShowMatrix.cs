using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowMatrix : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(transform.localToWorldMatrix);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
