using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

// Boids�� �ù����̼��� �����ϴ� ComputeShader�� ����
public class GPUBoids_problem : MonoBehaviour
{
    enum Owners { none, player1, player2 }
    [System.Serializable]
    public struct BoidData
    {
        private int ownerID;
        private Vector3 direction;
        private Vector3 position;
        private Vector3 targetLocation;

        public int OwnerID { get { return ownerID; } set { ownerID = value; } }
        public Vector3 Direction { get { return direction; } set { direction = value; } }
        public Vector3 Position { get { return position; } set { position = value; } }
        public Vector3 TargetLocation { get { return targetLocation; } set { targetLocation = value; } }
    }

    [System.Serializable]
    public struct BoidDatatest
    {
        public Vector3 velocity;
        public Vector3 position;
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

    [Header("���� ����Ʈ")]
    // ���� ����Ʈ
    [SerializeField] private BoidsGameObjectGenerator boidSpawner;
    List<GameObject> boids = new List<GameObject>();

    [Header("���� �÷��̾�")]
    // �÷��̾� ����Ʈ
    [SerializeField] private Transform[] players;
    // �÷��̾� ��ġ
    List<Transform> playerPos = new List<Transform>();
    // ���� �÷��̾�
    [SerializeField] private Transform owner;
    // ���� �÷��̾� �ֺ����� ����� �ʴ� ���� ����ġ
    [SerializeField] private float boundOwnerWeight = 10f;
    // ���� �÷��̾� ��ġ
    private Vector3 ownerPos;
    // ���� �÷��̾� ��ó�� �ӹ� ����
    [SerializeField] private Vector3 stayOwnerRadius = new Vector3(5f, 1f, 5f);
    #endregion

    #region Private Resources
    // Boid �����(Force)�� �����ϴ� ����
    private ComputeBuffer _boidForceBuffer;
    // Boid �⺻ ������ (�ӵ�, ��ġ ��)�� �����ϴ� ����
    private ComputeBuffer _boidDataBuffer;
    private ComputeBuffer _testBuffer;
    #endregion

    #region Accessors
    // Boid�� �⺻ �����͸� �����ϴ� ���۸� ��ȯ
    public ComputeBuffer GetBoidDataBuffer()
    {
        return this._boidDataBuffer != null ? this._boidDataBuffer : null;
    }

    // ��ü �� ��ȯ
    public int GetMaxObjectNum()
    {
        return this.MaxObjectNum;
    }

    // ���� �÷��̾� ��ġ�� ��ȯ
    public Vector3 GetOwnerPos()
    {
        return ownerPos;
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
        boids = boidSpawner.GetBoidsList();
        for (int i = 0; i < players.Length; i++)
        {
            playerPos.Add(players[i].transform);
        }
        // ���� �ʱ�ȭ
        InitBuffer();
        SetOwner();
    }

    private void Update()
    {
        FlockingBehavior();
        GetPlayerPos();
    }

    private void OnDestroy()
    {
        // ���� ����
        ReleaseBuffer();
    }

    private void OnDrawGizmos()
    {
        // ���� �÷��̾� ��ó�� �ӹ��� ���� ������
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ownerPos, stayOwnerRadius.x);
    }
    #endregion

    #region Private Functions

    // ���� �ʱ�ȭ
    private void InitBuffer()
    {
        // ���� �ʱ�ȭ
        _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));
        _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));
        _testBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));

        // Boid ������, Force ���۸� �ʱ�ȭ
        BoidData[] boidDataArr = new BoidData[MaxObjectNum];
        Vector3[] forceArr = new Vector3[MaxObjectNum];
        BoidData[] test = new BoidData[MaxObjectNum];
        for (int i = 0; i < MaxObjectNum; ++i)
        {
            forceArr[i] = Vector3.zero;
            boidDataArr[i].Direction = Random.insideUnitSphere * 0.1f;
            boidDataArr[i].Position = boids[i].transform.position;
            boidDataArr[i].TargetLocation = boidDataArr[i].Position;
            boidDataArr[i].OwnerID = (int)Owners.player1;
            test[i].Direction = Random.insideUnitSphere * 0.1f;
            test[i].Position = boids[i].transform.position;
            test[i].TargetLocation = boidDataArr[i].Position;
            test[i].OwnerID = (int)Owners.player1;
        }
        //_boidDataBuffer.SetData(boidDataArr);
        //_boidForceBuffer.SetData(forceArr);
        _testBuffer.SetData(test);
        forceArr = null;
        boidDataArr = null;
        test = null;
    }

    private void SetOwner()
    {
        owner = players[1];
    }

    private List<Transform> GetPlayerPos()
    {
        for (int i = 0; i < playerPos.Count; i++)
            playerPos[i].position = playerPos[i].position;
        return playerPos;
    }


    // ���� �ൿ
    private void FlockingBehavior()
    {
        ComputeShader cs = BoidsCS;
        int id = -1;

        // ������ �׷� �� ���ϱ�
        int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE); // CeilToInt: �ݿø�


        /*// �ൿ ���
        id = cs.FindKernel("ForceCS"); // Ŀ�� ID�� ������
        // ComputeShader���� ���۳� �ؽ��ĸ� ������ �� Ŀ�� ID�� �ʿ���
        Debug.Log(id);
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
        // �÷��̾� ��ġ
        cs.SetVector("_OwnerPos", ownerPos);
        cs.SetVector("_StayOwnerRadius", stayOwnerRadius);
        cs.SetFloat("_BoundOwnerWeight", boundOwnerWeight);
        // ����
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader ����
        // Dispatch: ComputeShader�� ������ Ŀ���� GPU���� ������ �����ϵ��� ���
        // Dispatch(Ŀ�� ID, ������ �׷� ��)


        // ���� ��������� �ӵ��� ��ġ�� ������Ʈ
        id = cs.FindKernel("IntegrateCS"); // Ŀ�� ID�� ������
        Debug.Log(id);
        cs.SetFloat("_DeltaTime", Time.deltaTime);
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader ���� 
*/

        id = cs.FindKernel("TestCS");
        Debug.Log(id);
        if (id < 0)
        {
            Debug.LogError("Kernel not found");
            return;
        }
        cs.SetBuffer(id, "_TestBufferWrite", _testBuffer);
        cs.SetBuffer(id, "_TestBufferRead", _testBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1);
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