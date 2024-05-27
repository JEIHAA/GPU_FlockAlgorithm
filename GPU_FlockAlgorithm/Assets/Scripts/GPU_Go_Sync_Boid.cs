using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Boids�� �ù����̼��� �����ϴ� ComputeShader�� ����
public class GPU_Go_Sync_Boid : MonoBehaviour
{
    enum Owners { none, player1, player2 }
    [System.Serializable]
    public struct BoidData
    {
        private Vector3 direction;
        private Vector3 position;
        private int ownerID;

        public Vector3 Direction { get { return direction; } set { direction = value; } }
        public Vector3 Position { get { return position; } set { position = value; } }
        public int OwnerID { get { return ownerID; } set { ownerID = value; } }
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
    [SerializeField] private Vector3 renderDistance = new Vector3(20f, 20f, 20f);

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
    #endregion

    #region Private Resources
    // Boid �⺻ ������ (�ӵ�, ��ġ ��)�� �����ϴ� ����
    private ComputeBuffer _boidDataBuffer;
    // Boid �����(Force)�� �����ϴ� ����
    private ComputeBuffer _boidForceBuffer;
    // Boid ������, Force ���� ������Ʈ �� �迭
    BoidData[] boidDataArr;
    Vector3[] forceArr;
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
    #endregion

    #region MonoBehaviour Functions
    private void Start()
    {
        //�ӽ÷� boids GameObject ��������
        boidList = boidSpawner.GetBoidsList();
        Debug.Log(boidList.Count);
        // ���� �ʱ�ȭ
        InitBuffer();
    }

    private void Update()
    {
        SyncGameObjects();
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
        Gizmos.DrawWireSphere(transform.position, renderDistance.x);
    }
    #endregion

    #region Private Functions
    private void InitBuffer()
    {
        // ���� �ʱ�ȭ
        _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));
        _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));

        // Boid ������, Force ���� ������Ʈ �� �迭
        boidDataArr = new BoidData[maxObjectNum];
        forceArr = new Vector3[maxObjectNum];
        //Vector3 boidPos;
        for (int i = 0; i < MaxObjectNum; ++i)
        {
            forceArr[i] = Vector3.zero;
            boidDataArr[i].Direction = Random.insideUnitSphere * 0.1f;
            UpdateGameObjectState(i);
            //boidDataArr[i].Position = new Vector3(i, 1, 0);
            /*boidPos = boidList[i].transform.position;
            boidPos.y = 1f;*/
            //boidDataArr[i].Position = boidPos;
        }
        UpdateBoidDataBuffer();
        UpdateBoidForceBuffer();
    }

    private void UpdateBoidDataBuffer()
    {
        _boidDataBuffer.SetData(boidDataArr);
    }
    private void UpdateBoidForceBuffer()
    {
        _boidForceBuffer.SetData(forceArr);
    }

    private void SyncGameObjects()
    {
        for (int i = 0; i < boidList.Count; i++)
        {
            UpdateGameObjectState(i);
        }
        UpdateBoidDataBuffer();

    }

    /*
    public void UpdateGameObjectState(Vector3 position)
    {
        
        for (int i = 0; i < MaxObjectNum; ++i)
        {
            boidDataArr[i].Position = position;
        }
        UpdateBoidDataBuffer();
    }*/

    public void UpdateGameObjectState(int index)
    {
        Vector3 boidPos;
        boidPos = boidList[index].transform.position;
        boidPos.y = 1f;
        boidDataArr[index].Position = boidList[index].transform.position;   
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
        // �ൿ ����ġ
        cs.SetFloat("_CohesionWeight", cohesionWeight);
        cs.SetFloat("_AlignmentWeight", alignmentWeight);
        cs.SetFloat("_SeparateWeight", separateWeight);
        // ����
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader ����
        // Dispatch: ComputeShader�� ������ Ŀ���� GPU���� ������ �����ϵ��� ���
        // Dispatch(Ŀ�� ID, ������ �׷� ��)


        // ���� ��������� �ӵ��� ��ġ�� ������Ʈ
        id = cs.FindKernel("IntegrateCS"); // Ŀ�� ID�� ������
        cs.SetFloat("_DeltaTime", Time.deltaTime);
        //cs.SetVector("_StayOwnerRadius", stayOwnerRadius);
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
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
