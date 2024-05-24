Shader "Custom/BoidsRender"
{
    // GPU�ν��Ͻ�. ������Ʈ ������ ���̴�
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma instancing_options procedural:setup // Unity�� �ν��Ͻ��� ����ϴ� �ɼ� ����
        // Graphics.RenderMeshIndirect�� �Բ� ����� �߰� �踮��Ʈ�� ����
        // ���ؽ� ���̴� ���� �ܰ迡�� procedural �ڿ� ������ �Լ� ȣ��

        struct Input
        {
            float2 uv_MainTex;
        };
        // Boid�� ����ü
        struct BoidData{
            float3 velocity;
            float position;
        }

        // #ifdef : UNITY_PROCEDURAL_INSTANCING_ENABLED�� true���
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED 
        // Boid �������� ����ü ����
        StructureBuffer<BoidData> _BoidDataBuffer;
        #endif

        //sampler2D _MainTex; // �ؽ���

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        float3 _ObjectScale; // Boid ��ü�� ũ��

        float4x4 eulerAnglesToRotationMatrix(float3 angles){
            // ���Ϸ���(����)�� ȸ�� ��ķ� ��ȯ    
        }

        // ���� ���̴�
        void vert(inout appdata_full v){
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            
            // �ν��Ͻ� ID�� Boid�� ������ ��������
            BoidData boidData = _BoidDataBuffer[unity_InstanceID]; // unity_InstanceID: �ν��Ͻ����� ������ ID

            float3 pos = boidData.position.xyz; // Boid�� ��ġ
            float3 scl = _ObjectScale; // Boid�� ������

            // ��ü�� ��ǥ���� ���� ��ǥ�� ��ȯ�ϴ� ����� ����
            float4x4 object2world = (float4x4)0;
            // ������ �� ����
            object2world._11_22_33_44 = float4(scl.xyz, 1.0); // ����� �밢�� ��ҿ� �� x,y,z,1 �Ҵ�
            // �ӵ����� Y�࿡ ���� ȸ���� ���
            float rotY = atan2(boidData.velocity.x, boidData.velocity.z);
            // �ӵ����� X�࿡ ���� ȸ�� ����
            float rotX = -atan2(boidData.velocity.y / (length(boidData.velocity.xyz) + 1e-8)); // +1e-8: 0 ������ ����
            // ���Ϸ���(����)���� ȸ�� ��� ���ϱ�
            float4x4 rotMatrix = eulerAnglesToRotationMatrix(float3(rotX, rotY, 0));
            // ��Ŀ� ȸ�� ����
            object2world = mul(rotMatrixm object2world);
            // ��Ŀ� ��ġ(�����̵�)�� ����
            object2world.14_24_34 += pos.xyz;
            
            // ������ ��ǥ ��ȯ
            v.vertex = mul(object2worldm v.vertex);
            // ������ ��ǥ ��ȯ
            v.normal = normalize(mul(object2world, v.normal));
            #endif
        }

        void setup(){    
        }

        // �����̽� ���̴�
        void surf(Input IN, inout SurfaceOutputStandard o){
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
