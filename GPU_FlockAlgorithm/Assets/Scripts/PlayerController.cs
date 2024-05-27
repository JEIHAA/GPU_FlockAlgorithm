using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public int OwnerID;
    public float MoveSpeed = 5.0f;
    public GPUBoids boids;
    private Vector3 stayOnwerRadius;

    private void Awake()
    {
        //stayOnwerRadius = boids.GetStayOwnerRadius();
    }

    private void OnDrawGizmos()
    {
        // ���� �÷��̾� ��ó�� �ӹ��� ���� ������
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, stayOnwerRadius.x);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W)) {
            this.transform.position += transform.forward * MoveSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.A))
        {
            this.transform.position += -transform.right * MoveSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            this.transform.position += -transform.forward * MoveSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            this.transform.position += transform.right * MoveSpeed * Time.deltaTime;
        }
    }
}
