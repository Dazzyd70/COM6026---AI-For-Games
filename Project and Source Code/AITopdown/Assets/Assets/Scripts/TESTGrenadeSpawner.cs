using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TESTGrenadeSpawner : MonoBehaviour
{
    public GameObject grenadePrefab;
    public Transform shootPoint;
    // Start is called before the first frame update
    void Start()
    {
        GameObject grenadeObj = Instantiate(grenadePrefab, shootPoint.position, Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
