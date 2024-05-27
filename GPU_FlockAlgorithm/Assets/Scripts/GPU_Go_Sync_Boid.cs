using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Boids의 시물레이션을 실행하는 ComputeShader를 제어
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

    [Header("게임 오브젝트")]
    // 스폰 포인트
    [SerializeField] private BoidsGameObjectGenerator boidSpawner;
    //boid 게임 오브젝트
    List<GameObject> boidList = new List<GameObject>();
    #endregion

    #region Private Resources
    // Boid 기본 데이터 (속도, 위치 등)을 포함하는 버퍼
    private ComputeBuffer _boidDataBuffer;
    // Boid 조향력(Force)을 포함하는 버퍼
    private ComputeBuffer _boidForceBuffer;
    // Boid 데이터, Force 버퍼 업데이트 용 배열
    BoidData[] boidDataArr;
    Vector3[] forceArr;
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

    // 메쉬 렌더 거리 반환
    public Vector3 GetRenderDistance()
    {
        return renderDistance;
    }
    #endregion

    #region MonoBehaviour Functions
    private void Start()
    {
        //임시로 boids GameObject 가져오기
        boidList = boidSpawner.GetBoidsList();
        Debug.Log(boidList.Count);
        // 버퍼 초기화
        InitBuffer();
    }

    private void Update()
    {
        SyncGameObjects();
    }

    private void OnDestroy()
    {
        // 버퍼 해제
        ReleaseBuffer();
    }

    private void OnDrawGizmos()
    {
        // 메쉬 렌더링 범위
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, renderDistance.x);
    }
    #endregion

    #region Private Functions
    private void InitBuffer()
    {
        // 버퍼 초기화
        _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));
        _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));

        // Boid 데이터, Force 버퍼 업데이트 용 배열
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
