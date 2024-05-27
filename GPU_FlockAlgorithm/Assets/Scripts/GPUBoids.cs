using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

// Boids의 시물레이션을 실행하는 ComputeShader를 제어
public class GPUBoids : MonoBehaviour
{
    enum Owners { none, player1, player2 }
    [System.Serializable]
    public struct BoidData
    {
        //private int ownerID;
        private Vector3 direction;
        private Vector3 position;
        private Vector3 targetLocation;

        //public int OwnerID { get { return ownerID; } set { ownerID = value; } }
        public Vector3 Direction { get { return direction; } set { direction = value; } }
        public Vector3 Position { get { return position; } set { position = value; } }
        public Vector3 TargetLocation { get { return targetLocation; } set { targetLocation = value; } }
    }

    // 스레드 그룹의 크기
    const int SIMULATION_BLOCK_SIZE = 256;

    #region Built-in Resources
    // Boids 시뮬레이션을 실행하는 ComputeShader의 참조
    [SerializeField] private ComputeShader BoidsCS;
    #endregion

    #region Boids Parameters
    [Header("최대 개체 수")]
    // 최대 개체 수
    [SerializeField, Range(10, 32768)]
    private int maxObjectNum = 256;
    public int MaxObjectNum { get { return maxObjectNum; } }

    [Header("렌더 거리")]
    [SerializeField] private Vector3 renderDistance = new Vector3(20f, 20f, 20f);

    [Header("최대 속도와 힘")]
    // 최대 속도
    [SerializeField] private float maxSpeed = 5.0f;
    // 조향력의 최대치
    [SerializeField] private float maxSteerForce = 0.5f;

    [Header("행동 범위")]
    // 응집 행동 범위
    [SerializeField] private float cohesionNeighborRadius = 2.0f;
    // 정렬 행동 범위
    [SerializeField] private float alignmentNeighborRadius = 2.0f;
    // 분리 행동 범위
    [SerializeField] private float separateNeighborRadius = 2.0f;

    [Header("행동 가중치")]
    // 응집 행동 가중치
    [SerializeField] private float cohesionWeight = 1.0f;
    // 정렬 행동 가중치
    [SerializeField] private float alignmentWeight = 1.0f;
    // 분리 행동 가중치
    [SerializeField] private float separateWeight = 1.0f;

    [Header("스폰 포인트")]
    // 스폰 포인트
    [SerializeField] private BoidsGameObjectGenerator boidSpawner;
    List<GameObject> boidList = new List<GameObject>();
    private BoidManager boid;

    /*[Header("주인 플레이어")]
    // 플레이어 리스트
    [SerializeField] private Transform[] players;
    // 플레이어 위치
    List<Transform> playerPos = new List<Transform>();
    // 주인 플레이어
    [SerializeField] private Transform owner;
    // 주인 플레이어 주변에서 벗어나지 않는 힘의 가중치
    [SerializeField] private float boundOwnerWeight = 10f; // 주인 플레이어 위치
    private Vector3 ownerPos;
    // 주인 플레이어 근처에 머물 범위
    [SerializeField] private Vector3 stayOwnerRadius = new Vector3(5f, 1f, 5f);*/
    #endregion

    #region Private Resources
    // Boid 기본 데이터 (속도, 위치 등)을 포함하는 버퍼
    private ComputeBuffer _boidDataBuffer;
    // Boid 조향력(Force)을 포함하는 버퍼
    private ComputeBuffer _boidForceBuffer;
    #endregion

    #region Accessors
    // Boid의 기본 데이터를 저장하는 버퍼를 반환
    public ComputeBuffer GetBoidDataBuffers()
    {
        return this._boidDataBuffer != null ? this._boidDataBuffer : null;
    }

    // 개체 수 반환
    public int GetMaxObjectNum()
    {
        return this.MaxObjectNum;
    }

/*    // 주인 플레이어 위치를 반환
    public Vector3 GetOwnerPos()
    {
        return ownerPos;
    }

    // 주인 플레이어 근처에서 머물 범위를 반환
    public Vector3 GetStayOwnerRadius()
    {
        return stayOwnerRadius;
    }
*/
    // 메쉬 렌더 거리 반환
    public Vector3 GetRenderDistance()
    {
        return renderDistance;
    }
/*
    private void SetOwner()
    {
        owner = null;
    }

    public List<Transform> GetPlayerPos()
    {
        for (int i = 0; i < playerPos.Count; i++)
            playerPos[i].position = players[i].position;
        return playerPos;
    }*/
    #endregion

    #region MonoBehaviour Functions
    private void Awake()
    {
        //임시로 boids GameObject 가져오기
        boidList = boidSpawner.GetBoidsList();
/*        for (int i = 0; i < players.Length; i++)
        {
            playerPos.Add(players[i].transform);
        }*/
    }

    private void Start()
    {
        // 버퍼 초기화
        InitBuffer();
        //SetOwner();
    }

    private void Update()
    {
/*        if (owner != null)
        {
            ownerPos = owner.transform.position;
        }*/
        //GetPlayerPos();
        //MeshsyncGameObject();
        //FlockingBehavior();
    }

    private void OnDestroy()
    {
        // 버퍼 해제
        ReleaseBuffer();
    }
    #endregion

    #region Private Functions
    // 버퍼 초기화
    private void InitBuffer()
    {
        // 버퍼 초기화
        _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));
        _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));

        // Boid 데이터, Force 버퍼를 초기화
        BoidData[] boidDataArr = new BoidData[MaxObjectNum];
        Vector3[] forceArr = new Vector3[MaxObjectNum];
        int j = 0;
        for (int i = 0; i < MaxObjectNum; ++i)
        {
            //if (j >= boidSpawner.boidSpawners.Length)
            if (j >= boidList.Count)
                j = 0;
            forceArr[i] = Vector3.zero;
            boidDataArr[i].Position = boidList[j].transform.position;
            Debug.Log(boidList[j].transform.position);
            //boidDataArr[i].Position = boidSpawner.boidSpawners[j].transform.position;
            //boidDataArr[i].Position = Random.insideUnitSphere * 1f;
            boidDataArr[i].Direction = Random.insideUnitSphere * 0.1f;
            j++;
            //boidDataArr[i].TargetLocation = boidDataArr[i].Position;
            //boidDataArr[i].OwnerID = (int)Owners.player1;
        }
        _boidDataBuffer.SetData(boidDataArr);
        _boidForceBuffer.SetData(forceArr);
        boidDataArr = null;
        forceArr = null;
    }

    // GameObject의 위치를 ComputeShader와 동기화
    private void MeshsyncGameObject() {
        BoidData[] boidDataArr = new BoidData[MaxObjectNum];
        for (int i = 0; i < boidList.Count; ++i)
        {
            boidDataArr[i].Position = boidList[i].transform.position;
            Debug.Log("MeshsyncGameObject"+boidDataArr[i].Position);
        }
        _boidDataBuffer.SetData(boidDataArr);
        boidDataArr = null;
    }


    // 군집 행동
    private void FlockingBehavior()
    {
        ComputeShader cs = BoidsCS;
        int id = -1;

        // 스레드 그룹 수 구하기
        int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE); // CeilToInt: 반올림

        // 행동 계산
        id = cs.FindKernel("ForceCS"); // 커널 ID를 가져옴
        // ComputeShader에서 버퍼나 텍스쳐를 설정할 때 커널 ID가 필요함
        cs.SetInt("_MaxBoidObjectNum", MaxObjectNum);
        // 속도
        cs.SetFloat("_MaxSpeed", maxSpeed);
        cs.SetFloat("_MaxSteerForce", maxSteerForce);
        // 행동 범위
        cs.SetFloat("_CohesionNeighborRadius", cohesionNeighborRadius);
        cs.SetFloat("_AlignmentNeighborRadius", alignmentNeighborRadius);
        cs.SetFloat("_SeparateNeighborRadius", separateNeighborRadius);
        // 행동 가중치
        cs.SetFloat("_CohesionWeight", cohesionWeight);
        cs.SetFloat("_AlignmentWeight", alignmentWeight);
        cs.SetFloat("_SeparateWeight", separateWeight);
        // 플레이어 위치
/*        cs.SetVector("_OwnerPos", ownerPos);
        cs.SetVector("_StayOwnerRadius", stayOwnerRadius);
        cs.SetFloat("_BoundOwnerWeight", boundOwnerWeight);*/
        // 버퍼
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader 실행
        // Dispatch: ComputeShader에 정의한 커널을 GPU에서 연산을 수행하도록 명령
        // Dispatch(커널 ID, 스레드 그룹 수)


        // 계산된 조향력으로 속도와 위치를 업데이트
        id = cs.FindKernel("IntegrateCS"); // 커널 ID를 가져옴
        cs.SetFloat("_DeltaTime", Time.deltaTime);
        //cs.SetVector("_StayOwnerRadius", stayOwnerRadius);
        cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
        cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
        cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
        cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShader 실행 
    }

    // 버퍼 해제
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