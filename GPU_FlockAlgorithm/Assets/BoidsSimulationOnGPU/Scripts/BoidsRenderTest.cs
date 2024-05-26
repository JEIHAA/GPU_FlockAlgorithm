using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BoidsSimulationOnGPU
{
    // Boids를 렌더링하는 쉐이더를 제어하는 C# 스크립트
    // 同GameObjectに、GPUBoidsコンポーネントがアタッチされていること保証
    [RequireComponent(typeof(GPUBoidsTest))]
    public class BoidsRenderTest : MonoBehaviour
    {
        #region Paremeters
        // 描画するBoidsオブジェクトのスケール
        public Vector3 ObjectScale = new Vector3(0.1f, 0.2f, 0.5f);
        #endregion

        #region Script References
        // GPUBoidsスクリプトの参照
        public GPUBoidsTest GPUBoidsScript;
        #endregion

        #region Built-in Resources
        // 描画するメッシュの参照
        public Mesh InstanceMesh;
        // 描画のためのマテリアルの参照
        public Material InstanceRenderMaterial;
        #endregion

        #region Private Variables
        // GPUインスタンシングのための引数（ComputeBufferへの転送用）
        // インスタンスあたりのインデックス数, インスタンス数, 
        // 開始インデックス位置, ベース頂点位置, インスタンスの開始位置
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // GPUインスタンシングのための引数バッファ
        ComputeBuffer argsBuffer;
        #endregion

        #region MonoBehaviour Functions
        void Start()
        {
            // 引数バッファを初期化
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);
        }

        void Update()
        {
            // メッシュをインスタンシング
            RenderInstancedMesh1();
        }

        void OnDisable()
        {
            // 引数バッファを解放
            if (argsBuffer != null)
                argsBuffer.Release();
            argsBuffer = null;
        }
        #endregion
        Vector3 WallCenter = Vector3.zero;
        Vector3 WallSize = new Vector3(32.0f, 32.0f, 32.0f);
        #region Private Functions
        void RenderInstancedMesh1()
        {
            // 描画用マテリアルがNull, または, GPUBoidsスクリプトがNull,
            // またはGPUインスタンシングがサポートされていなければ, 処理をしない
            if (InstanceRenderMaterial == null || GPUBoidsScript == null ||
                !SystemInfo.supportsInstancing)
                return;

            // 指定したメッシュのインデックス数を取得
            // 지정된 메쉬의 인덱스 가져오기
            uint numIndices = (InstanceMesh != null) ? (uint)InstanceMesh.GetIndexCount(0) : 0;
            args[0] = numIndices; // メッシュのインデックス数をセット 메쉬 인덱스 수 설정(초기화)
            args[1] = (uint)GPUBoidsScript.GetMaxObjectNum1(); // インスタンス数をセット 인스턴스 수 초기화
            argsBuffer.SetData(args); // バッファにセット 버퍼에 설정(초기화)

            // Boidデータを格納したバッファをマテリアルにセット
            // Boid 데이터를 저장하는 버퍼를 머티리얼에 설정(초기화)
            InstanceRenderMaterial.SetBuffer("_BoidDataBuffer",
                GPUBoidsScript.GetBoidDataBuffer1());
            // Boidオブジェクトスケールをセット
            // Boid 객체 스타일을 설정(초기화)
            InstanceRenderMaterial.SetVector("_ObjectScale", ObjectScale);
            // 境界領域を定義
            var bounds = new Bounds
            (
                WallCenter, WallSize
            );
            // メッシュをGPUインスタンシングして描画
            // 메쉬를 GPU 인스턴싱하여 그리기
            // 메쉬의 인덱스 수나 인스턴스 수를 ComputerBuffer로 전달
            Graphics.DrawMeshInstancedIndirect
            (
                InstanceMesh,           // インスタンシングするメッシュ 인스턴싱하는 메쉬
                0,                      // submeshのインデックス submesh 인덱스
                InstanceRenderMaterial, // 描画を行うマテリアル  그리기를 할 머티리얼
                bounds,                 // 境界領域 경계영역
                argsBuffer              // GPUインスタンシングのための引数のバッファ GPU 인스턴싱을 위한 인수의 버퍼
            );
        }
        #endregion
    }
}