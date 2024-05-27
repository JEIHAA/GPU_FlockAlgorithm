using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [SerializeField] private int ownerID = -1;
    public int OwnerID { get { return ownerID; } set { ownerID = value; } }
    [SerializeField] private int boidID = 0;
    public int BoidID { get { return boidID; } set { boidID = value; } }

    public Vector3 GetBoidPos() { return this.transform.position; }

    [SerializeField] private PlayerController[] player;

    private GameObject owner;
    private Vector3 ownerPos;

    private void OnTriggerEnter(Collider _other)
    {
        if (OwnerID != -1)
            return;
        Debug.Log("OnCollisionEnter");
        if (_other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            OwnerID = _other.gameObject.GetComponent<PlayerController>().OwnerID;
            owner = _other.gameObject;
        }
    }

    private void Update()
    {
        if (OwnerID == -1) { return; }
        else
        {
            GetOwnerPos();
            MoveToOnwer();
        }
    }

    private void GetOwnerPos() {
        ownerPos = owner.transform.position;
    }

    private void MoveToOnwer() {
        transform.position += owner.transform.position * owner.GetComponent<PlayerController>().MoveSpeed * Time.deltaTime;
    }
}
