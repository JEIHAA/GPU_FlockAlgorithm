using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GPUBoids;

// Boids를 렌더링하는 셰이더를 제어
[RequireComponent(typeof(GPUBoids))]
public class BoidsRender : MonoBehaviour
{
    // #region, #endregion: 코드 블럭으로 묶기
    #region Parameters
    // 화면에 그릴 Boid 객체의 스케일
    [SerializeField] private Vector3 ObjectScale = new Vector3(1f, 1f, 1f);
    #endregion

    #region Script References
    // 스크립트 참조
    [SerializeField] GPUBoids GPUBoidsScript;
    [SerializeField] PlayerController player;
    #endregion

    #region Built-in Resources
    // 화면에 그릴 메쉬 참조
    [SerializeField] private Mesh InstanceMesh;
    // 화면에 그릴 머티리얼 참조
    [SerializeField] Material InstanceRenderMaterial;
    #endregion

    #region Private Variables
    // GPU 인스턴싱을 위한 인수 (ComputeBuffer 전송용)
    // 인스턴스 당 인덱스 수, 인스턴스 수,
    // 시작 인덱스 위치, 베이스 정점 위치, 인스턴스의 시작 위치
    uint[] args = new uint[5] { 0, 0, 0, 0, 0 }; // Unsigned Integer
    ComputeBuffer argsBuffer;
    #endregion

    #region MonoBehaviour Functions
    private void Start()
    {
        // 인수버퍼 초기화
        // ComputeShader가 메모리 버퍼에 읽고 쓰기 위해 필요한 데이터
        // ComputeBuffer(길이, 이름, 요소 하나의 크기)
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void Update()
    {
        // 메쉬 인스턴싱
        RenderInstanceMesh();
    }

    private void OnDisable()
    {
        // 인수버퍼 해제
        if (argsBuffer != null)
        {
            argsBuffer.Release(); // 버퍼해제. 삭제
        }
        argsBuffer = null;
    }
    #endregion

    #region Private Functions
    void RenderInstanceMesh()
    {
        // 렌더링용 머티리얼이 Null 또는 GPUBoids 스크립트가 Null
        // 또는 GPU 인스턴싱이 지원되지 않으면 처리하지 않음
        if (InstanceMesh == null || GPUBoidsScript == null || !SystemInfo.supportsInstancing) return;

        // 지정한 메쉬의 인덱스 가져오기
        uint numIndices = (InstanceMesh != null) ? (uint)InstanceMesh.GetIndexCount(0) : 0;
        // 메쉬 인덱스 수 설정(초기화)
        args[0] = numIndices;
        args[1] = (uint)GPUBoidsScript.GetMaxObjectNum();
        argsBuffer.SetData(args);

        // Boid 데이터를 저장하는 버퍼를 머티리얼에 설정(초기화)
        InstanceRenderMaterial.SetBuffer("_BoidDataBuffer", GPUBoidsScript.GetBoidDataBuffers());
        
/*        BoidData[] debugData = new BoidData[GPUBoidsScript.GetMaxObjectNum()];
        GPUBoidsScript.GetBoidDataBuffers().GetData(debugData);*/

        // Boid 객체 스케일 설정(초기화)
        InstanceRenderMaterial.SetVector("_ObjectScale", ObjectScale); 
        
        // 경계 영역 정의
        Bounds bounds = new Bounds(player.gameObject.transform.position, GPUBoidsScript.GetRenderDistance());

        // 메쉬를 GPU 인스턴싱하여 그리기
        Graphics.DrawMeshInstancedIndirect(InstanceMesh, 0, InstanceRenderMaterial, bounds, argsBuffer);
        // (인스턴싱하는 메쉬, submesh 인덱스, 머티리얼, 렌더 영역, GPU 인스턴싱을 위한 인수의 버퍼) 지금은 사용하지 않음
        // Graphics.RenderMeshIndirect 사용
    }
    #endregion
}

