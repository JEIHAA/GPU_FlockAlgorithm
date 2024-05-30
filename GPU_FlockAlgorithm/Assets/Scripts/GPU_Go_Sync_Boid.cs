using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Boids�� �ù����̼��� �����ϴ� ComputeShader�� ����
public class GPU_Go_Sync_Boid : MonoBehaviour
{
    [System.Serializable]
    public struct BoidData
    {
        private Vector3 direction;
        private Vector3 position;

        public Vector3 Direction { get { return direction; } set { direction = value; } }
        public Vector3 Position { get { return position; } set { position = value; } }
    }
    [System.Serializable]
    public struct BoidOwner
    {
        private int ownerID;
        private Vector3 ownerPos;

        public int OwnerID { get { return ownerID; } set { ownerID = value; } }
        public Vector3 OwnerPos { get { return ownerPos; } set { ownerPos = value; } }
    }

    // ������ �׷��� ũ��
    const int SIMULATION_BLOCK_SIZE = 256;

    #region Built-in Resources
    // Boids �ùķ��̼��� �����ϴ� ComputeShader�� ����
    [SerializeField] private ComputeShader BoidsCS;
    #endregion

    #region Boids Parameters
    [Header("�ִ� ��ü ��")]
    // �ִ� ��ü ��
    [SerializeField, Range(10, 32768)]
    private int maxObjectNum = 256;
    public int MaxObjectNum { get { return maxObjectNum; } }

    [Header("���� �Ÿ�")]
    [SerializeField] private Vector3 renderDistance = new Vector3(20f, 2f, 20f);

    [Header("�ִ� �ӵ��� ��")]
    // �ִ� �ӵ�
    [SerializeField] private float maxSpeed = 5.0f;
    // ������� �ִ�ġ
    [SerializeField] private float maxSteerForce = 0.5f;

    [Header("�ൿ ����")]
    // ���� �ൿ ����
    [SerializeField] private float cohesionNeighborRadius = 2.0f;
    // ���� �ൿ ����
    [SerializeField] private float alignmentNeighborRadius = 2.0f;
    // �и� �ൿ ����
    [SerializeField] private float separateNeighborRadius = 2.0f;

    [Header("�ൿ ����ġ")]
    // ���� �ൿ ����ġ
    [SerializeField] private float cohesionWeight = 1.0f;
    // ���� �ൿ ����ġ
    [SerializeField] private float alignmentWeight = 1.0f;
    // �и� �ൿ ����ġ
    [SerializeField] private float separateWeight = 1.0f;

    [Header("���� ������Ʈ")]
    // ���� ����Ʈ
    [SerializeField] private BoidsGameObjectGenerator boidSpawner;
    //boid ���� ������Ʈ
    List<GameObject> boidList = new List<GameObject>();

    [Header("���� �÷��̾�")]
    // ���� �÷��̾� �ֺ����� ����� �ʴ� ���� ����ġ
    [SerializeField] private float boundOwnerWeight = 10f;
    // ���� �÷��̾� ��ó�� �ӹ� ����
    [SerializeField] private Vector3 stayOwnerRadius = new Vector3(5f, 1f, 5f);
    #endregion

    #region Private Resources
    // Boid �⺻ ������ (�ӵ�, ��ġ ��)�� �����ϴ� ����
    private ComputeBuffer _boidDataBuffer;
    // Boid �����(Force)�� �����ϴ� ����
    private ComputeBuffer _boidForceBuffer;
    // Boid�� �÷��̾��� ���踦 �����ϴ� ����
    private ComputeBuffer _boidOwnerBuffer;

    // Boid ������, Force ���� ������Ʈ �� �迭
    BoidData[] boidDataArr;
    Vector3[] forceArr;
    BoidOwner[] boidOwnerArr;
    #endregion

    #region Accessors
    // Boid�� �⺻ �����͸� �����ϴ� ���۸� ��ȯ
    public ComputeBuffer GetBoidDataBuffers()
    {
        return this._boidDataBuffer != null ? this._boidDataBuffer : null;
    }

    // ��ü �� ��ȯ
    public int GetMaxObjectNum()
    {
        return this.MaxObjectNum;
    }

    // �޽� ���� �Ÿ� ��ȯ
    public Vector3 GetRenderDistance()
    {
        return renderDistance;
    }

    // ���� �÷��̾� ��ó���� �ӹ� ������ ��ȯ
    public Vector3 GetStayOwnerRadius()
    {
        return stayOwnerRadius;
    }
    #endregion

    #region MonoBehaviour Functions
    private void Start()
    {
        //�ӽ÷� boids GameObject ��������
        boidList = boidSpawner.GetBoidsList();
        // ���� �ʱ�ȭ
        InitBuffer();
    }

    private void Update()
    {
        SyncGameObjects();
        FlockingBehavior();
    }

    private void OnDestroy()
    {
        // ���� ����
        ReleaseBuffer();
    }

    private void OnDrawGizmos()
    {
        // �޽� ������ ����
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, renderDistance);
    }
    #endregion

    #region Private Functions
    private void InitBuffer()
    {
        // ���� �ʱ�ȭ
        _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));
        _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));
        _boidOwnerBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidOwner)));
        
        // Boid ������, Force ���� ������Ʈ �� �迭
        forceArr = new Vector3[maxObjectNum];
        boidDataArr = new BoidData[maxObjectNum];
        boidOwnerArr = new BoidOwner[maxObjectNum];

        for (int i = 0; i < MaxObjectNum; ++i)
        {
            forceArr[i] = Vector3.zero;
            boidOwnerArr[i].OwnerID = -1;
            boidOwnerArr[i].OwnerPos = Vector3.zero;
            boidDataArr[i].Direction = Random.insideUnitSphere * 0.1f;
            UpdateGameObjectPos(i);
        }
        UpdateBoidDataBuffer();
        UpdateBoidForceBuffer();
        UpdateBoidOwnerBuffer();
    }

    private void UpdateBoidDataBuffer()
    {
        _boidDataBuffer.SetData(boidDataArr);
    }
    private void UpdateBoidForceBuffer()
    {
        _boidForceBuffer.SetData(forceArr);
    }
    private void UpdateBoidOwnerBuffer()
    {
        _boidOwnerBuffer.SetData(boidOwnerArr);
    }

    private void SyncGameObjects()
    {
        for (int i = 0; i < boidList.Count; i++)
        {
            UpdateGameObjectPos(i);
        }
        UpdateBoidDataBuffer();
    }

    public void UpdateGameObjectPos(int index)
    {
        Vector3 boidPos;
        boidPos = boidList[index].transform.position;
        boidPos.y = 0.8f;
        boidDataArr[index].Position = boidPos;
    }


    // ���� �ൿ
    private void FlockingBehavior()
    {
        ComputeShader cs = BoidsCS;
        int id = -1;

        // ������ �׷� �� ���ϱ�
        int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE); // CeilToInt: �ݿø�

        // �ൿ ���
        id = cs.FindKernel("ForceCS"); // Ŀ�� ID�� ������
        // ComputeShader���� ���۳� �ؽ��ĸ� ������ �� Ŀ�� ID�� �ʿ���
        cs.SetInt("_MaxBoidObjectNum", MaxObjectNum);
        // �ӵ�
        cs.SetFloat("_MaxSpeed", maxSpeed);
        cs.SetFloat("_MaxSteerForce", maxSteerForce);
        // �ൿ ����
        cs.SetFloat("_CohesionNeighborRadius", cohesionNeighborRadius);
        cs.SetFloat("_AlignmentNeighborRadius", alignmentNeighborRadius);
        cs.SetFloat("_SeparateNeighborRadius", separateNeighborRadius);
        cs.SetVector("_StayOwnerRadius", stayOwnerRadius);
        // �ൿ ����ġ
        cs.SetFloat("_CohesionWeight", cohesionWeight);
        cs.SetFloat("_AlignmentWeight", alignmentWeight);
        cs.SetFloat("_SeparateWeight", separateWeight);
        cs.SetFloat("_BoundOwnerWeight", boundOwnerWeight);
        // ����
        cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidOwnerBufferRead", _boidOwnerBuffer);
        cs.SetBuffer(id, "_BoidOwnerBufferWrite", _boidOwnerBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader ����
        // Dispatch: ComputeShader�� ������ Ŀ���� GPU���� ������ �����ϵ��� ���
        // Dispatch(Ŀ�� ID, ������ �׷� ��)


        // ���� ��������� �ӵ��� ��ġ�� ������Ʈ
        id = cs.FindKernel("IntegrateCS"); // Ŀ�� ID�� ������
        cs.SetFloat("_DeltaTime", Time.deltaTime);
        //cs.SetVector("_StayOwnerRadius", stayOwnerRadius);
        cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidOwnerBufferRead", _boidOwnerBuffer);
        cs.SetBuffer(id, "_BoidOwnerBufferWrite", _boidOwnerBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader ���� 
    }

    // ���� ����
    private void ReleaseBuffer()
    {
        if (_boidDataBuffer != null)
        {
            _boidDataBuffer.Release();
            _boidDataBuffer = null;
        }
        if (_boidForceBuffer != null)
        {
            _boidForceBuffer.Release();
            _boidForceBuffer = null;
        }
    }
    #endregion
}
