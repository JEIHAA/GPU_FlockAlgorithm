using BoidsSimulationOnGPU;
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

  [SerializeField] private GPUBoids GPUBoidCS;

  [SerializeField] private List<PlayerController> player = new List<PlayerController>();
  [SerializeField] private GameObject[] playerGo;
  [SerializeField] private GameObject[] owner;
  [SerializeField] private Vector3 tagetPos;
  public Vector3 TagetPos { get { return tagetPos; } }

  static private int[] ownerHasBoidNum;
  public static int[] OwnerHasBoidNum { get { return ownerHasBoidNum; } set { ownerHasBoidNum = value; } }

  private void Awake()
  {
    playerGo = GameObject.FindGameObjectsWithTag("Player");
    owner = new GameObject[playerGo.Length];
    tagetPos = this.transform.position;
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
      Debug.Log($"ownerHasBoidNum[{OwnerID}]: {ownerHasBoidNum[OwnerID]}");
      Debug.Log($"total Boid: {ownerHasBoidNum[0] + ownerHasBoidNum[1]}");
    }
  }

  private void Update()
  {
    if (OwnerID != -1) {
      SetOwnerPos();
    }
  }

  public Vector3 GetBoidPos() { return this.transform.position; }

  private void SetOwnerPos() {
    tagetPos = owner[OwnerID].transform.position;
  }

  public int GetOwnerID() { return ownerID; }
  public Vector3 GetTargetPos() { return tagetPos; }
}
