using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class GPUBoidsTest : MonoBehaviour
{
    [System.Serializable]
    struct BoidData
    {
        public Vector3 Velocity;
        public Vector3 Position;
    }

    const int SIMULATION_BLOCK_SIZE = 256;

    [Range(256, 32768)]
    public int MaxObjectNum = 16384;
    public ComputeShader BoidsCS;

    ComputeBuffer _testBuffer;

    public int GetMaxObjectNum()
    {
        return this.MaxObjectNum;
    }

    public ComputeBuffer GetBoidDataBuffer()
    {
        return this._testBuffer != null ? this._testBuffer : null;
    }

    #region MonoBehaviour Functions
    void Start()
    {
        InitBuffer();
    }

    void Update()
    {
        Simulation();
    }

    void OnDestroy()
    {
        ReleaseBuffer();
    }
    #endregion

    #region Private Functions
    // ���� �ʱ�ȭ
    void InitBuffer()
    {
        // ���� �ʱ�ȭ �� RWStructuredBuffer�� ����� ���̹Ƿ� ComputeBufferType.Default ��� ComputeBufferType.Raw�� ����
        _testBuffer = new ComputeBuffer(MaxObjectNum, Marshal.SizeOf(typeof(BoidData)), ComputeBufferType.Default);

        var boidDataArr = new BoidData[MaxObjectNum];
        for (var i = 0; i < MaxObjectNum; i++)
        {
            boidDataArr[i].Position = Random.insideUnitSphere * 1.0f;
            boidDataArr[i].Velocity = Random.insideUnitSphere * 0.1f;
        }
        _testBuffer.SetData(boidDataArr);
    }

    void Simulation()
    {
        ComputeShader cs = BoidsCS;
        int id = cs.FindKernel("TestCS");

        if (id < 0)
        {
            Debug.LogError("Kernel not found");
            return;
        }

        // ������ �׷� �� ���ϱ�
        int threadGroupSize = Mathf.CeilToInt(MaxObjectNum / (float)SIMULATION_BLOCK_SIZE);

        // ComputeShader���� ����� ���۸� ����
        cs.SetBuffer(id, "_TestBufferWrite", _testBuffer);
        cs.SetBuffer(id, "_TestBufferRead", _testBuffer);

        // ����� �޽����� �߰��Ͽ� Ŀ���� ����� ȣ��Ǵ��� Ȯ��
        Debug.Log("Dispatching Compute Shader with kernel ID: " + id);

        // Ŀ�� ����
        cs.Dispatch(id, threadGroupSize, 1, 1);
    }

    void ReleaseBuffer()
    {
        if (_testBuffer != null)
        {
            _testBuffer.Release();
            _testBuffer = null;
        }
    }
    #endregion
}
