using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GPUBoids;

// Boids�� �������ϴ� ���̴��� ����
[RequireComponent(typeof(GPUBoids))]
public class BoidsRender : MonoBehaviour
{
    // #region, #endregion: �ڵ� ������ ����
    #region Parameters
    // ȭ�鿡 �׸� Boid ��ü�� ������
    [SerializeField] private Vector3 ObjectScale = new Vector3(1f, 1f, 1f);
    #endregion

    #region Script References
    // ��ũ��Ʈ ����
    [SerializeField] GPUBoids GPUBoidsScript;
    [SerializeField] PlayerController player;
    #endregion

    #region Built-in Resources
    // ȭ�鿡 �׸� �޽� ����
    [SerializeField] private Mesh InstanceMesh;
    // ȭ�鿡 �׸� ��Ƽ���� ����
    [SerializeField] Material InstanceRenderMaterial;
    #endregion

    #region Private Variables
    // GPU �ν��Ͻ��� ���� �μ� (ComputeBuffer ���ۿ�)
    // �ν��Ͻ� �� �ε��� ��, �ν��Ͻ� ��,
    // ���� �ε��� ��ġ, ���̽� ���� ��ġ, �ν��Ͻ��� ���� ��ġ
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 }; // Unsigned Integer
    ComputeBuffer argsBuffer;
    #endregion

    #region MonoBehaviour Functions
    private void Start()
    {
        // �μ����� �ʱ�ȭ
        // ComputeShader�� �޸� ���ۿ� �а� ���� ���� �ʿ��� ������
        // ComputeBuffer(����, �̸�, ��� �ϳ��� ũ��)
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void Update()
    {
        // �޽� �ν��Ͻ�
        RenderInstanceMesh();
    }

    private void OnDisable()
    {
        // �μ����� ����
        if (argsBuffer != null)
        {
            argsBuffer.Release(); // ��������. ����
        }
        argsBuffer = null;
    }
    #endregion

    #region Private Functions
    void RenderInstanceMesh()
    {
        // �������� ��Ƽ������ Null �Ǵ� GPUBoids ��ũ��Ʈ�� Null
        // �Ǵ� GPU �ν��Ͻ��� �������� ������ ó������ ����
        if (InstanceMesh == null || GPUBoidsScript == null || !SystemInfo.supportsInstancing) return;

        // ������ �޽��� �ε��� ��������
        uint numIndices = (InstanceMesh != null) ? (uint)InstanceMesh.GetIndexCount(0) : 0;
        // �޽� �ε��� �� ����(�ʱ�ȭ)
        args[0] = numIndices;
        args[1] = (uint)GPUBoidsScript.GetMaxObjectNum();
        argsBuffer.SetData(args);

        // Boid �����͸� �����ϴ� ���۸� ��Ƽ���� ����(�ʱ�ȭ)
        InstanceRenderMaterial.SetBuffer("_BoidDataBuffer", GPUBoidsScript.GetBoidDataBuffers());
        
/*        BoidData[] debugData = new BoidData[GPUBoidsScript.GetMaxObjectNum()];
        GPUBoidsScript.GetBoidDataBuffers().GetData(debugData);*/

        // Boid ��ü ������ ����(�ʱ�ȭ)
        InstanceRenderMaterial.SetVector("_ObjectScale", ObjectScale); 
        
        // ��� ���� ����
        Bounds bounds = new Bounds(player.gameObject.transform.position, GPUBoidsScript.GetRenderDistance());

        // �޽��� GPU �ν��Ͻ��Ͽ� �׸���
        Graphics.DrawMeshInstancedIndirect(InstanceMesh, 0, InstanceRenderMaterial, bounds, argsBuffer);
        // (�ν��Ͻ��ϴ� �޽�, submesh �ε���, ��Ƽ����, ���� ����, GPU �ν��Ͻ��� ���� �μ��� ����) ������ ������� ����
        // Graphics.RenderMeshIndirect ���
    }
    #endregion
}

