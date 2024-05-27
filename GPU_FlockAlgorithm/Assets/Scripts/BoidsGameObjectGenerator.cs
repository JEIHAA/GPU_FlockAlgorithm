using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BoidsGameObjectGenerator : MonoBehaviour
{
    //[SerializeField] private GPUBoids maxObjectNum;
    [SerializeField] private GPU_Go_Sync_Boid maxObjectNum;
    [SerializeField] private Transform boidSpawnerParent;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform boidsParent;
    private List<GameObject> boids = new List<GameObject>();

    public Transform[] boidSpawners;

    private void Awake()
    {
        boidSpawners = boidSpawnerParent.GetComponentsInChildren<Transform>();
        Vector3 boidPosition;
        int idx;
        for (int i = 0; i < maxObjectNum.MaxObjectNum; ++i)
        {
            //boidPosition = boidSpawners[SetRandomPositionTest()].position;
            //boidPosition = boidSpawners[SetRandomPosition()].position;
            idx = SetRandomPositionTest();
            boidPosition = boidSpawners[idx].position;
            boidPosition.y = 1.2f;
            boids.Add(Instantiate(prefab, boidPosition, Quaternion.identity, boidsParent));
            boids[i].name = "boid" + i;
        }
    }
    

    private int SetRandomPositionTest()
    {
        int len = boidSpawners.Length;
        int idx = Random.Range(1, len);
        return idx;
    }

    private int SetRandomPosition()
    {
        System.Random random = new System.Random((int)System.DateTime.Now.Ticks);
        int len = boidSpawners.Length;
        int randomIdx = random.Next(1, len);
        return randomIdx;
    }

    public List<GameObject> GetBoidsList()
    {
        return boids;
    }
}
