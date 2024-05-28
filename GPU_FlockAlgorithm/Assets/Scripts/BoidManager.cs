using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class BoidManager : MonoBehaviour
{
    [SerializeField] private int ownerID = -1;
    public int OwnerID { get { return ownerID; } set { ownerID = value; } }
    [SerializeField] private int boidID = 0;
    public int BoidID { get { return boidID; } set { boidID = value; } }

    static private int[] ownerHasBoidNum;
    public static int[] OwnerHasBoidNum { get { return ownerHasBoidNum; } set { ownerHasBoidNum = value; } }

    [SerializeField] private GameObject[] playerGo;
    [SerializeField] private List<PlayerController> player = new List<PlayerController>();
    [SerializeField] private Vector3 ownerPos;
    [SerializeField] private GameObject[] owner;

    private void Awake()
    {
        playerGo = GameObject.FindGameObjectsWithTag("Player");
        owner = new GameObject[playerGo.Length];
        ownerHasBoidNum = new int[playerGo.Length];

        for (int i = 0; i < playerGo.Length; i++) {
            player.Add(playerGo[i].GetComponent<PlayerController>());
            owner[player[i].OwnerID] = player[i].gameObject;
            ownerHasBoidNum[player[i].OwnerID] = 0;
        }
    }

    private void OnTriggerEnter(Collider _other)
    {
        if (OwnerID != -1)
            return;
        Debug.Log("OnCollisionEnter");
        if (_other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            OwnerID = _other.gameObject.GetComponent<PlayerController>().OwnerID;
            ownerHasBoidNum[OwnerID] += 1;
            Debug.Log($"ownerHasBoidNum[{OwnerID}]: {ownerHasBoidNum[OwnerID]}");
            Debug.Log($"total Boid: {ownerHasBoidNum[0] + ownerHasBoidNum[1]}");
        }
    }

    private void Update()
    {
        if (OwnerID != -1) {
            GetOwnerPos();
            MoveToOnwer();
        }
    }

    public Vector3 GetBoidPos() { return this.transform.position; }

    private void GetOwnerPos() {
        ownerPos = owner[OwnerID].transform.position;
    }

    private void MoveToOnwer() {
        transform.position = ownerPos+new Vector3(1f,1f,1f);
    }
}
