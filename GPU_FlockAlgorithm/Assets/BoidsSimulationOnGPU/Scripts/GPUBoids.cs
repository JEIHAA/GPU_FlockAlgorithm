using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BoidsSimulationOnGPU
{
    // Boids의 시뮬레이션을 실행하는 ComputeShader를 제어
    public class GPUBoids : MonoBehaviour
    {
        // Boidデータの構造体
        [System.Serializable]
        struct BoidData
        {
            public Vector3 Velocity; // 速度
            public Vector3 Position; // 位置
        }
        // スレッドグループのスレッドのサイズ
        const int SIMULATION_BLOCK_SIZE = 256;

        #region Boids Parameters
        // 最大オブジェクト数
        [Range(5, 32768)]
        public int MaxObjectNum = 16384;

        [Header("게임 오브젝트")]
        // 스폰 포인트
        [SerializeField] private BoidsGameObjectGenerator boidSpawner;
        //boid 게임 오브젝트
        List<GameObject> boidList = new List<GameObject>();

        // 結合を適用する他の個体との半径
        public float CohesionNeighborhoodRadius = 2.0f;
        // 整列を適用する他の個体との半径
        public float AlignmentNeighborhoodRadius = 2.0f;
        // 分離を適用する他の個体との半径
        public float SeparateNeighborhoodRadius = 1.0f;

        // 速度の最大値
        public float MaxSpeed = 5.0f;
        // 操舵力の最大値
        public float MaxSteerForce = 0.5f;

        // 結合する力の重み
        public float CohesionWeight = 1.0f;
        // 整列する力の重み
        public float AlignmentWeight = 1.0f;
        // 分離する力の重み
        public float SeparateWeight = 3.0f;

        // 壁を避ける力の重み
        public float AvoidWallWeight = 10.0f;

        // 壁の中心座標   
        public Vector3 WallCenter = Vector3.zero;
        // 壁のサイズ
        public Vector3 WallSize = new Vector3(32.0f, 32.0f, 32.0f);
        
        BoidData[] boidDataArr;

        #endregion

        #region Built-in Resources
        // Boidsシミュレーションを行うComputeShaderの参照
        public ComputeShader BoidsCS;
        #endregion

        #region Private Resources
        // Boidの操舵力（Force）を格納したバッファ
        // Boid 조향력(Force)를 포함하는 버퍼
        ComputeBuffer _boidForceBuffer;
        // Boidの基本データ（速度, 位置, Transformなど）を格納したバッファ
        // Boid의 기본 데이터(속도, 위치)를 포함하는 버퍼
        ComputeBuffer _boidDataBuffer;
        #endregion

        #region Accessors
        // Boidの基本データを格納したバッファを取得
        // Boid의 기본 데이터를 저장하는 버퍼를 반환
        public ComputeBuffer GetBoidDataBuffer()
        {
            return this._boidDataBuffer != null ? this._boidDataBuffer : null;
        }

        // オブジェクト数を取得
        // 개체 수 반환
        public int GetMaxObjectNum()
        {
            return this.MaxObjectNum;
        }

        // シミュレーション領域の中心座標を返す
        // 시뮬레이션 영역의 중심 좌표 반환
        public Vector3 GetSimulationAreaCenter()
        {
            return this.WallCenter; 
        }

        // シミュレーション領域のボックスのサイズを返す
        // 시뮬레이션 영역의 박스 크기를 반환
        public Vector3 GetSimulationAreaSize()
        {
            return this.WallSize;
        }
        #endregion

        #region MonoBehaviour Functions
        void Start()
        {
            //임시로 boids GameObject 가져오기
            boidList = boidSpawner.GetBoidsList();
            // バッファを初期化
            InitBuffer();
        }

        void Update()
        {
            // シミュレーション
            SyncGameObjects();
            SyncCSMesh();
            Simulation();
        }

        void OnDestroy()
        {
            // バッファを破棄
            ReleaseBuffer();
        }

        void OnDrawGizmos()
        {
            // デバッグとしてシミュレーション領域をワイヤーフレームで描画
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(WallCenter, WallSize);
        }
        #endregion

        #region Private Functions
        // バッファを初期化
        // 버퍼 초기화
        void InitBuffer()
        {
            // バッファを初期化 버퍼 초기화
            // GPU상에서 계산하기 위한 데이터를 저장하는 버퍼로 ComputeBuffer를 사용
            // ComputeBuffer: ComputeShader를 위해 데이터를 저장하는 데이터 타입
            // C# 스크립트에서 GPU상의 메모리 버퍼에 대해 읽기나 쓰기를 할 수 있음
            // new ComputeBuffer(버퍼를 이루는 요소 수, 요소 1개당 크기(바이트단위))
            _boidDataBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData))); //Marshal.SizeOf로 버퍼 요소로 사용할 자료형의 바이트 단위 크기를 얻을 수 있음
            _boidForceBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(Vector3)));

            // Boidデータ, Forceバッファを初期化 Boid 데이터, Force 버퍼 초기화
            var forceArr = new Vector3[MaxObjectNum];
            boidDataArr = new BoidData[MaxObjectNum];
            for (var i = 0; i < MaxObjectNum; i++)
            {
                float idx = i % 2;
                forceArr[i] = Vector3.zero;
                UpdateGameObjectPos(i);
                boidDataArr[i].Velocity = Random.insideUnitSphere * 0.1f;
            }
            _boidForceBuffer.SetData(forceArr); // 버퍼에 들어갈 구조체 배열의 값을 설정
            _boidDataBuffer.SetData(boidDataArr);
        }

        private void UpdateBoidDataBuffer()
        {
            _boidDataBuffer.SetData(boidDataArr);
        }

        private void SyncGameObjects()
        {
            for (int i = 0; i < boidList.Count; i++)
            {
                UpdateGameObjectPos(i);
            }
            UpdateBoidDataBuffer();
        }

        private void SyncCSMesh()
        {
            for (int i = 0; i < MaxObjectNum; i++)
            {
                UpdateCSPos(i);
            }
        }

        public void UpdateCSPos(int index)
        {
            boidList[index].transform.position = boidDataArr[index].Position;
        }

        public void UpdateGameObjectPos(int index)
        {
            Vector3 boidPos;
            boidPos = boidList[index].transform.position;
            boidPos.y = 0.8f;
            boidDataArr[index].Position = boidPos;
        }

        // シミュレーション
        void Simulation()
        {
            ComputeShader cs = BoidsCS;
            int id = -1;

            // スレッドグループの数を求める
            // 스레드 그룹 수 구하기
            int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE);

            // 操舵力を計算
            id = cs.FindKernel("ForceCS"); // カーネルIDを取得
            cs.SetInt("_MaxBoidObjectNum", MaxObjectNum);
            cs.SetFloat("_CohesionNeighborhoodRadius", CohesionNeighborhoodRadius);
            cs.SetFloat("_AlignmentNeighborhoodRadius", AlignmentNeighborhoodRadius);
            cs.SetFloat("_SeparateNeighborhoodRadius", SeparateNeighborhoodRadius);
            cs.SetFloat("_MaxSpeed", MaxSpeed);
            cs.SetFloat("_MaxSteerForce", MaxSteerForce);
            cs.SetFloat("_SeparateWeight", SeparateWeight);
            cs.SetFloat("_CohesionWeight", CohesionWeight);
            cs.SetFloat("_AlignmentWeight", AlignmentWeight);
            cs.SetVector("_WallCenter", WallCenter);
            cs.SetVector("_WallSize", WallSize);
            cs.SetFloat("_AvoidWallWeight", AvoidWallWeight);
            cs.SetBuffer(id, "_BoidDataBufferRead", _boidDataBuffer);
            cs.SetBuffer(id, "_BoidForceBufferWrite", _boidForceBuffer);
            cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShaderを実行

            // 操舵力から、速度と位置を計算
            id = cs.FindKernel("IntegrateCS"); // カーネルIDを取得
            cs.SetFloat("_DeltaTime", Time.deltaTime);
            cs.SetBuffer(id, "_BoidForceBufferRead", _boidForceBuffer);
            cs.SetBuffer(id, "_BoidDataBufferWrite", _boidDataBuffer);
            cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShaderを実行
        }

        // バッファを解放
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