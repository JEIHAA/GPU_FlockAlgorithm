Shader "Custom/BoidsRender"
{
    // GPU인스턴싱. 오브젝트 렌더링 쉐이더
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
        #pragma instancing_options procedural:setup // Unity가 인스턴스에 사용하는 옵션 지정
        // Graphics.RenderMeshIndirect와 함께 사용할 추가 배리언트를 생성
        // 버텍스 셰이더 시작 단계에서 procedural 뒤에 지정한 함수 호출

        struct Input
        {
            float2 uv_MainTex;
        };

        // Boid의 구조체
        struct BoidData
        {
            float3 position;
            float3 direction;
        };

        // #ifdef : UNITY_PROCEDURAL_INSTANCING_ENABLED가 true라면
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED 
        // Boid 데이터의 구조체 버퍼
        StructureBuffer<BoidData> _BoidDataBuffer;
        #endif

        sampler2D _MainTex; // 텍스쳐

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        float3 _ObjectScale; // Boid 객체의 크기
        
        // 오일러각(라디안)을 회전 행렬로 변환    
        float4x4 eulerAnglesToRotationMatrix(float3 angles){
            float ch = cos(angles.y); float sh = sin(angles.y); // heading
            float ca = cos(angles.z); float sa = sin(angles.z); // attitude
            float cb = cos(angles.x); float sb = sin(angles.x); // bank
            // RyRxRz (Heading Bank Attitude)
            return float4x4(
            ch * ca + sh * sb * sa, -ch * sa + sh * sb * ca, sh * cb, 0,
            cb * sa, cb * ca, -sb, 0,
            -sh * ca + ch * sb * sa, sh * sa + ch * sb * ca, ch * cb, 0,
            0, 0, 0, 1
            );
        }

        // 정점 셰이더
        void vert(inout appdata_full v){
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            
            // 인스턴스 ID로 Boid의 데이터 가져오기
            BoidData boidData = _BoidDataBuffer[unity_InstanceID]; // unity_InstanceID: 인스턴스마다 고유한 ID

            float3 pos = boidData.position.xyz; // Boid의 위치
            float3 scl = _ObjectScale; // Boid의 스케일

            // 객체의 좌표에서 월드 좌표를 변환하는 행렬을 정의
            float4x4 object2world = (float4x4)0;
            // 스케일 값 대입
            object2world._11_22_33_44 = float4(scl.xyz, 1.0);

            // 속도에서 Y 축에 대한 회전을 계산
            float rotY = atan2(boidData.direction.x, boidData.direction.z);
            // 속도에서 X 축에 대한 회전을 산출
            float rotX = -asin(boidData.direction.y / (length(boidData.direction.xyz) + 1e-8)); // + 1e-8: 0 나눗셈방지

            // 오일러각(라디안)에서 회전 행렬을 구한다
            float4x4 rotMatrix = eulerAnglesToRotationMatrix(float3(rotX, rotY, 0));
            // 행렬에 회전을 적용
            object2world = mul(rotMatrix, object2world);
            // 행렬에 위치(평행이동)를 적용
            object2world._14_24_34 += pos.xyz;

            // 정점을 좌표 변환
            v.vertex = mul(object2world, v.vertex);
            // 법선을 좌표 변환
            v.normal = normalize(mul(object2world, v.normal));
            #endif
        }

        void setup(){    
        }

        // 서페이스 셰이더
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
