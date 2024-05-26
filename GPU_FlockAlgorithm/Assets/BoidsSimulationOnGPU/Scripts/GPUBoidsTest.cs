using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BoidsSimulationOnGPU
{
    // Boids의 시뮬레이션을 실행하는 ComputeShader를 제어
    public class GPUBoidsTest : MonoBehaviour
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

        // 最大オブジェクト数
        [Range(256, 32768)]
        public int MaxObjectNum = 16384;

        public float test = 10;
        public ComputeShader BoidsCS;

        ComputeBuffer _testBuffer;

        // オブジェクト数を取得
        // 개체 수 반환
        public int GetMaxObjectNum1()
        {
            return this.MaxObjectNum;
        }

        // Boidの基本データを格納したバッファを取得
        // Boid의 기본 데이터를 저장하는 버퍼를 반환
        public ComputeBuffer GetBoidDataBuffer1()
        {
            return this._testBuffer != null ? this._testBuffer : null;
        }

        #region MonoBehaviour Functions
        void Start()
        {
            // バッファを初期化
            InitBuffer1();
        }

        void Update()
        {
            // シミュレーション
            Simulation1();
        }

        void OnDestroy()
        {
            // バッファを破棄
            ReleaseBuffer1();
        }
        #endregion

        #region Private Functions
        // バッファを初期化
        // 버퍼 초기화
        void InitBuffer1()
        {
            _testBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)));

            var boidDataArr = new BoidData[MaxObjectNum];
            for (var i = 0; i < MaxObjectNum; i++)
            {
                boidDataArr[i].Position = Random.insideUnitSphere * 1.0f;
                boidDataArr[i].Velocity = Random.insideUnitSphere * 0.1f;
            }
            _testBuffer.SetData(boidDataArr);
            boidDataArr = null;
        }

        // シミュレーション
        void Simulation1()
        {
            ComputeShader cs = BoidsCS;
            int id = -1;

            // スレッドグループの数を求める
            // 스레드 그룹 수 구하기
            int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / SIMULATION_BLOCK_SIZE);

            // 操舵力から、速度と位置を計算
            id = cs.FindKernel("TestCS"); // カーネルIDを取得
            cs.SetBuffer(id, "_TestBufferWrite", _testBuffer);
            cs.SetBuffer(id, "_TestBufferRead", _testBuffer);
            cs.Dispatch(id, threadGroupSize, 1, 1); // ComputeShaderを実行
        }

        // バッファを解放
        void ReleaseBuffer1()
        {
            if (_testBuffer != null)
            {
                _testBuffer.Release();
                _testBuffer = null;
            }
        }
        #endregion
    } // class
} // namespace