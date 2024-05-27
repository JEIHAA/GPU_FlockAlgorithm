using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [SerializeField] private int ownerID;
    public int OwnerID { get { return ownerID; } set { ownerID = value; } }

    public Vector3 GetBoidPos() { return this.transform.position; }
}
