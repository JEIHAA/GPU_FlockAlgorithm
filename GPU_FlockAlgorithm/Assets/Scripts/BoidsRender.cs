using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

namespace BoidsSimulationOnGPU
	{
	// Boids를 렌더링하는 쉐이더를 제어하는 C# 스크립트
	[RequireComponent(typeof(GPUBoids))]
	public class BoidsRender : MonoBehaviour
	{
		#region Paremeters
		public Vector3 ObjectScale = new Vector3(1f, 1f, 1f);
		#endregion

		#region Script References
		public GPUBoids GPUBoidsScript;
		#endregion

		#region Built-in Resources
		public Mesh InstanceMesh;
		public List<Material> InstanceRenderMaterial;
		public Material material;
		#endregion

		#region Private Variables
		uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
		ComputeBuffer argsBuffer;
		#endregion

		#region MonoBehaviour Functions
		void Start()
		{
			argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
			ComputeBufferType.IndirectArguments);
		}

		void Update()
		{
			RenderInstancedMesh();
		}

		void OnDisable()
		{
			if (argsBuffer != null)
			argsBuffer.Release();
			argsBuffer = null;
		}
		#endregion

		#region Private Functions
		void RenderInstancedMesh()
		{
			if (InstanceRenderMaterial == null || GPUBoidsScript == null || !SystemInfo.supportsInstancing) {
				Debug.LogError("InstanceRenderMaterial is Null!");
				return;
			}

			int subMeshCount = InstanceMesh != null ? InstanceMesh.subMeshCount : 0;

			for (int i = 0; i < subMeshCount; ++i)
			{
				
				Debug.Log(i);
				if (i >= InstanceRenderMaterial.Count)
				{
					material = InstanceRenderMaterial[0];
					Debug.LogError("Need more material");
					break;
				}
				else
				{
					material = InstanceRenderMaterial[i];
				}
				uint numIndices = (uint)InstanceMesh.GetIndexCount(0);
				numIndices = (uint)InstanceMesh.GetIndexCount(i);
				argsBuffer.SetData(args); // 버퍼에 설정(초기화)

				// 지정된 메쉬의 인덱스 가져오기
				args[0] = numIndices; // 메쉬 인덱스 수 설정(초기화)
				args[1] = (uint)GPUBoidsScript.GetMaxObjectNum(); // 인스턴스 수 초기

				Debug.Log(InstanceRenderMaterial[i]);

				material.SetBuffer("_BoidDataBuffer", GPUBoidsScript.GetBoidDataBuffer()); // Boid 데이터를 저장하는 버퍼를 머티리얼에 설정(초기화)
				material.SetVector("_ObjectScale", ObjectScale); // Boid 객체 스타일을 설정(초기화)

				var bounds = new Bounds
				(
					GPUBoidsScript.GetRenderAreaCenter(), // 중심
					GPUBoidsScript.GetRenderAreaSize()    // 크기
				);

				Graphics.DrawMeshInstancedIndirect(InstanceMesh, i, InstanceRenderMaterial[i], bounds, argsBuffer);
				// 인스턴싱 할 메쉬, 서브메쉬 인덱스, 머티리얼, 경계영역, 인수버퍼
			}
		}
		#endregion
	}
}