using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.UIElements;
using JetBrains.Annotations;

namespace BoidsSimulationOnGPU
{
  // Boids의 시뮬레이션을 실행하는 ComputeShader를 제어
  public class GPUBoids : MonoBehaviour
  {
    [System.Serializable]
    struct BoidData
    {
        public Vector3 velocity; 
        public Vector3 position;

        public Vector3 Velocity { get { return velocity; } set { velocity = value; } }
        public Vector3 Position { get { return position; } set { position = value; } }
    }

    [System.Serializable]
    public struct BoidTarget
    {
        private int ownerID;
        private Vector3 targetPos;

        public int OwnerID { get { return ownerID; } set { ownerID = value; } }
        public Vector3 TargetPos { get { return targetPos; } set { targetPos = value; } }
    }

    // 스레드 그룹의 크기
    const int SIMULATION_BLOCK_SIZE = 256;

    #region Built-in Resources
    // Boids 시뮬레이션을 실행하는 ComputeShader의 참조
    [SerializeField] private ComputeShader boidsCS;
    //[SerializeField] private BoidManager boidManager;
    #endregion

    #region Boids Parameters
    [Header("최대 개체 수")]
    // 최대 개체 수
    [Range(5, 32768)]
    public int MaxObjectNum = 256;

    [Header("게임 오브젝트")]
    // 스폰 포인트
    [SerializeField] private BoidsGameObjectGenerator boidSpawner;
    //boid 게임 오브젝트
    List<GameObject> boidList = new List<GameObject>();

    [Header("최대 속도와 힘")]
    // 최대 속도
    [SerializeField] private float maxSpeed = 5.0f;
    // 조향력의 최대치
    [SerializeField] private float maxSteerForce = 0.5f;

    [Header("행동 범위")]
    // 응집 행동 범위
    [SerializeField] private float cohesionNeighborhoodRadius = 2.0f;
    // 정렬 행동 범위
    [SerializeField] private float alignmentNeighborhoodRadius = 2.0f;
    // 분리 행동 범위
    [SerializeField] private float separateNeighborhoodRadius = 2.0f;

    [Header("행동 가중치")]
    // 응집 행동 가중치
    [SerializeField] private float cohesionWeight = 1.0f;
    // 정렬 행동 가중치
    [SerializeField] private float alignmentWeight = 1.0f;
    // 분리 행동 가중치
    [SerializeField] private float separateWeight = 1.0f;

    [Header("주인 플레이어 바운드")]
    // 주인 플레이어 주변에서 벗어나지 않는 힘의 가중치
    [SerializeField] private float boundOwnerWeight = 10f;
    // 주인 플레이어 근처에 머물 범위
    [SerializeField] private Vector3 stayOwnerRadius = new Vector3(5f, 1f, 5f);

    [Header("경계구역 바운드")]
    // 경계 반발력
    public float AvoidWallWeight = 10.0f;

    // 렌더 경계 중심
    public Vector3 renderAreaCenter = Vector3.zero;
    // 렌더 경계 범위
    public Vector3 renderAreaSize = new Vector3(45.0f, 1.0f, 45.0f);

    // 경계 범위
    public Vector3 boundarySize = new Vector3(5.0f, 1.0f, 5.0f);
    #endregion

    #region Private Resources
    // Boid 기본 데이터 (속도, 위치 등)를 관리하는 버퍼
    private ComputeBuffer _boidDataBuffer;
    public ComputeBuffer _BoidDataBuffer
    { get { return _boidDataBuffer; } set { _boidDataBuffer = value; } }
    // Boid 조향력(Force)을 관리하는 버퍼
    private ComputeBuffer _boidForceBuffer;
    // Boid와 플레이어의 관계를 관리하는 버퍼
    private ComputeBuffer _boidTargetBuffer;

    // Boid 데이터, Force 버퍼 업데이트 용 배열
    BoidData[] boidDataArr;
    Vector3[] forceArr;
    BoidTarget[] boidTargetArr;
    #endregion


    #region Accessors
    // Boid의 기본 데이터를 저장하는 버퍼를 반환
    public ComputeBuffer GetBoidDataBuffer()
    {
     return this._boidDataBuffer != null ? this._boidDataBuffer : null;
    }
    // boidTargerArr 반환
    public BoidTarget[] GetBoidTargetArr()
    {
      return this.boidTargetArr != null ? this.boidTargetArr : null;
    }

    // 개체 수 반환
    public int GetMaxObjectNum()
    {
        return this.MaxObjectNum;
    }

    // 시뮬레이션 영역의 중심 좌표 반환
    public Vector3 GetRenderAreaCenter()
    {
        return this.renderAreaCenter; 
    }

    // 시뮬레이션 영역의 박스 크기를 반환
    public Vector3 GetRenderAreaSize()
    {
        return this.renderAreaSize;
    }

    // 주인 플레이어 근처에서 머물 범위를 반환
    public Vector3 GetStayOwnerRadius()
    {
        return stayOwnerRadius;
    }
    #endregion

    #region MonoBehaviour Functions
    void Start()
    {
      // 생성된 boids GameObject 가져오기
      boidList = boidSpawner.GetBoidsList();
      // 버퍼 초기화
      InitBuffer();
    }

    void Update()
    {
      UpdateBoidTargetPos();
      Simulation();
      SyncToCSMesh();
      SyncToGameObjects();
    }

    void OnDestroy()
    {
      // 버퍼 해제
      ReleaseBuffer();
    }

    // 바운드 영역 렌더링
/*    void OnDrawGizmos()
    {
      Gizmos.color = Color.cyan;
      for(int i = 0; i < boidTargetArr.Length; i++)
        Gizmos.DrawWireCube(boidTargetArr[i].TargetPos, boundarySize);
    }*/
    #endregion

    #region Private Functions
    // 버퍼 초기화
    void InitBuffer()
    {
      // 버퍼 초기화
      // GPU상에서 계산하기 위한 데이터를 저장하는 버퍼로 ComputeBuffer를 사용
      // ComputeBuffer: ComputeShader를 위해 데이터를 저장하는 데이터 타입
      // C# 스크립트에서 GPU상의 메모리 버퍼에 대해 읽기나 쓰기를 할 수 있음
      // new ComputeBuffer(버퍼를 이루는 요소 수, 요소 1개당 크기(바이트단위))
      _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData))); //Marshal.SizeOf로 버퍼 요소로 사용할 자료형의 바이트 단위 크기를 얻을 수 있음
      _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));
      _boidTargetBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidTarget)));

      // Boid 데이터, Force 버퍼 초기화
      forceArr = new Vector3[MaxObjectNum];
      boidDataArr = new BoidData[MaxObjectNum];
      boidTargetArr = new BoidTarget[MaxObjectNum];
      BoidManager boidManager;
      for (var i = 0; i < MaxObjectNum; i++)
      {
        forceArr[i] = Vector3.zero;
        boidManager = boidList[i].GetComponent<BoidManager>();
        UpdateBoidTargetArr(i, boidManager.OwnerID, boidManager.TagetPos);
        UpdateBoidDataArr(i);
        boidDataArr[i].Velocity = Random.insideUnitSphere * 0.1f;
      }
      UpdateBoidForceBuffer();// 버퍼에 들어갈 구조체 배열의 값을 설정
      UpdateBoidDataBuffer();
      UpdateBoidTargetBuffer();
    }

    #region Setting Functions
    public void UpdateBoidDataArr(int index)
    {
      Vector3 boidPos;
      boidPos = boidList[index].transform.position;
      boidDataArr[index].Position = boidPos;
    }
    public void UpdateBoidTargetArr( int index, int ownerID, Vector3 targetPos)
    {
      boidTargetArr[index].OwnerID = ownerID;
      boidTargetArr[index].TargetPos = targetPos;
    }

    public void UpdateBoidTargetPos() 
    {
      for (int i = 0; i < boidList.Count; i++) {
        boidTargetArr[i].OwnerID = boidList[i].GetComponent<BoidManager>().GetOwnerID();
        boidTargetArr[i].TargetPos = boidList[i].GetComponent<BoidManager>().GetTargetPos();      
      }
      UpdateBoidTargetBuffer();
    }

    public void UpdateCSMeshPos(int index)
    {  
      _boidDataBuffer.GetData(boidDataArr);
      boidList[index].transform.position = boidDataArr[index].Position;
    }

    private void SyncToGameObjects()
    {
      for (int i = 0; i < boidList.Count; i++)
      {
        UpdateBoidDataArr(i);
      }
      UpdateBoidDataBuffer();
    }

    private void SyncToCSMesh()
    {
      for (int i = 0; i < MaxObjectNum; i++)
      {
        UpdateCSMeshPos(i);
      }
    }

    private void UpdateBoidDataBuffer()
    {
      _boidDataBuffer.SetData(boidDataArr);
    }
    private void UpdateBoidForceBuffer()
    {
      _boidForceBuffer.SetData(forceArr);
    }
    private void UpdateBoidTargetBuffer()
    {
      _boidTargetBuffer.SetData(boidTargetArr);
    }
    #endregion

    // Boids시뮬레이션
    void Simulation()
    {
      ComputeShader cs = boidsCS;
      int id = -1;

      // 스레드 그룹 수 구하기
      int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE);

      id = cs.FindKernel("ForceCS"); 

      cs.SetInt("_MaxBoidObjectNum", MaxObjectNum);

      cs.SetFloat("_MaxSpeed", maxSpeed);
      cs.SetFloat("_MaxSteerForce", maxSteerForce);

      cs.SetFloat("_CohesionNeighborhoodRadius", cohesionNeighborhoodRadius);
      cs.SetFloat("_AlignmentNeighborhoodRadius", alignmentNeighborhoodRadius);
      cs.SetFloat("_SeparateNeighborhoodRadius", separateNeighborhoodRadius);
      
      cs.SetFloat("_SeparateWeight", separateWeight);
      cs.SetFloat("_CohesionWeight", cohesionWeight);
      cs.SetFloat("_AlignmentWeight", alignmentWeight);

      cs.SetFloat("_AvoidWallWeight", AvoidWallWeight);
      cs.SetVector("_BoundarySize", boundarySize);

      cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
      cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
      cs.Dispatch(id, threadGroupSize, 1, 1); 

      id = cs.FindKernel("IntegrateCS"); 
      cs.SetFloat("_DeltaTime", Time.deltaTime);
      cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
      cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
      cs.SetBuffer(id, "_BoidTargetBufferRead", _boidTargetBuffer);
      //cs.SetBuffer(id, "_BoidTargetBufferWrite", _boidTargetBuffer);
      cs.Dispatch(id, threadGroupSize, 1, 1); 
    }

    // 버퍼 해제
    void ReleaseBuffer()
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
  } // class
} // namespace