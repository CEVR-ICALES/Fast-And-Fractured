// Shader created with Shader Forge v1.38 
// Shader Forge (c) Freya Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.38;sub:START;pass:START;ps:flbk:Legacy Shaders/Bumped Diffuse,iptp:0,cusa:False,bamd:0,cgin:,lico:0,lgpr:1,limd:1,spmd:1,trmd:0,grmd:0,uamb:True,mssp:True,bkdf:True,hqlp:False,rprd:False,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:0,bdst:1,dpts:2,wrdp:True,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:False,qofs:0,qpre:1,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.4705882,fgcg:0.4509804,fgcb:0.3490196,fgca:1,fgde:0.005,fgrn:0,fgrf:0.01,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:4013,x:33720,y:32841,varname:node_4013,prsc:2|diff-9285-OUT,normal-7351-OUT;n:type:ShaderForge.SFN_Tex2d,id:8555,x:32419,y:32886,ptovrint:False,ptlb:DetailAlbedo,ptin:_DetailAlbedo,varname:_DetailAlbedo,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:c9f56c646bacaf94fa7cbb00c69f0c2a,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:24,x:32417,y:33068,ptovrint:False,ptlb:DetailNormal,ptin:_DetailNormal,varname:_DetailNormal,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:2c3560939cb013f479c187b25ab669f0,ntxv:3,isnm:True;n:type:ShaderForge.SFN_Tex2d,id:4206,x:32417,y:33253,ptovrint:False,ptlb:DetailMask,ptin:_DetailMask,varname:_DetailMask,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:3490e62b39635344fa005d902a24d679,ntxv:2,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:3249,x:32416,y:32523,ptovrint:False,ptlb:MainTex,ptin:_MainTex,varname:_MainTex,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:b7e106a25ba8c7c4198888b4ed65fdc7,ntxv:0,isnm:False;n:type:ShaderForge.SFN_OneMinus,id:6715,x:32608,y:32886,varname:node_6715,prsc:2|IN-8555-RGB;n:type:ShaderForge.SFN_Vector1,id:6250,x:32417,y:33411,varname:node_6250,prsc:2,v1:1;n:type:ShaderForge.SFN_Vector1,id:9515,x:32417,y:33467,varname:node_9515,prsc:2,v1:0.5;n:type:ShaderForge.SFN_OneMinus,id:4699,x:32596,y:33253,varname:node_4699,prsc:2|IN-4206-RGB;n:type:ShaderForge.SFN_Vector1,id:9328,x:32417,y:33519,varname:node_9328,prsc:2,v1:0;n:type:ShaderForge.SFN_Append,id:6034,x:33171,y:33270,varname:node_6034,prsc:2|A-3519-OUT,B-6250-OUT;n:type:ShaderForge.SFN_ComponentMask,id:3519,x:32982,y:33270,varname:node_3519,prsc:2,cc1:0,cc2:1,cc3:-1,cc4:-1|IN-4569-OUT;n:type:ShaderForge.SFN_Lerp,id:4569,x:32803,y:33270,varname:node_4569,prsc:2|A-24-RGB,B-9328-OUT,T-4699-OUT;n:type:ShaderForge.SFN_Tex2d,id:7352,x:32416,y:32704,ptovrint:False,ptlb:BumpMap,ptin:_BumpMap,varname:_BumpMap,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:d01c515efeed30b4cb7b6a631164cbb2,ntxv:3,isnm:True;n:type:ShaderForge.SFN_NormalBlend,id:7351,x:33373,y:33250,varname:node_7351,prsc:2|BSE-7352-RGB,DTL-6034-OUT;n:type:ShaderForge.SFN_RemapRange,id:5482,x:32778,y:32886,varname:node_5482,prsc:2,frmn:0.5,frmx:1,tomn:-1,tomx:1|IN-6715-OUT;n:type:ShaderForge.SFN_OneMinus,id:5086,x:32955,y:32886,varname:node_5086,prsc:2|IN-5482-OUT;n:type:ShaderForge.SFN_Blend,id:8019,x:33156,y:32804,varname:node_8019,prsc:2,blmd:0,clmp:True|SRC-5086-OUT,DST-3249-RGB;n:type:ShaderForge.SFN_Lerp,id:9285,x:33396,y:32867,varname:node_9285,prsc:2|A-8019-OUT,B-3249-RGB,T-4699-OUT;proporder:3249-7352-8555-24-4206;pass:END;sub:END;*/

Shader "Shader Forge/DestroyIt Mobile" {
    Properties {
        _MainTex ("MainTex", 2D) = "white" {}
        _BumpMap ("BumpMap", 2D) = "bump" {}
        _DetailAlbedo ("DetailAlbedo", 2D) = "white" {}
        _DetailNormal ("DetailNormal", 2D) = "bump" {}
        _DetailMask ("DetailMask", 2D) = "black" {}
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles ps4 
            #pragma target 3.0
            uniform sampler2D _DetailAlbedo; uniform float4 _DetailAlbedo_ST;
            uniform sampler2D _DetailNormal; uniform float4 _DetailNormal_ST;
            uniform sampler2D _DetailMask; uniform float4 _DetailMask_ST;
            uniform sampler2D _MainTex; uniform float4 _MainTex_ST;
            uniform sampler2D _BumpMap; uniform float4 _BumpMap_ST;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                LIGHTING_COORDS(7,8)
                UNITY_FOG_COORDS(9)
                #if defined(LIGHTMAP_ON) || defined(UNITY_SHOULD_SAMPLE_SH)
                    float4 ambientOrLightmapUV : TEXCOORD10;
                #endif
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                #ifdef LIGHTMAP_ON
                    o.ambientOrLightmapUV.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.ambientOrLightmapUV.zw = 0;
                #elif UNITY_SHOULD_SAMPLE_SH
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                    o.ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _BumpMap_var = UnpackNormal(tex2D(_BumpMap,TRANSFORM_TEX(i.uv0, _BumpMap)));
                float3 _DetailNormal_var = UnpackNormal(tex2D(_DetailNormal,TRANSFORM_TEX(i.uv0, _DetailNormal)));
                float node_9328 = 0.0;
                float4 _DetailMask_var = tex2D(_DetailMask,TRANSFORM_TEX(i.uv0, _DetailMask));
                float3 node_4699 = (1.0 - _DetailMask_var.rgb);
                float node_6250 = 1.0;
                float3 node_7351_nrm_base = _BumpMap_var.rgb + float3(0,0,1);
                float3 node_7351_nrm_detail = float3(lerp(_DetailNormal_var.rgb,float3(node_9328,node_9328,node_9328),node_4699).rg,node_6250) * float3(-1,-1,1);
                float3 node_7351_nrm_combined = node_7351_nrm_base*dot(node_7351_nrm_base, node_7351_nrm_detail)/node_7351_nrm_base.z - node_7351_nrm_detail;
                float3 node_7351 = node_7351_nrm_combined;
                float3 normalLocal = node_7351;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                UNITY_LIGHT_ATTENUATION(attenuation,i, i.posWorld.xyz);
                float3 attenColor = attenuation * _LightColor0.xyz;
/////// GI Data:
                UnityLight light;
                #ifdef LIGHTMAP_OFF
                    light.color = lightColor;
                    light.dir = lightDirection;
                    light.ndotl = LambertTerm (normalDirection, light.dir);
                #else
                    light.color = half3(0.f, 0.f, 0.f);
                    light.ndotl = 0.0f;
                    light.dir = half3(0.f, 0.f, 0.f);
                #endif
                UnityGIInput d;
                d.light = light;
                d.worldPos = i.posWorld.xyz;
                d.worldViewDir = viewDirection;
                d.atten = attenuation;
                #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                    d.ambient = 0;
                    d.lightmapUV = i.ambientOrLightmapUV;
                #else
                    d.ambient = i.ambientOrLightmapUV;
                #endif
                Unity_GlossyEnvironmentData ugls_en_data;
                ugls_en_data.roughness = 1.0 - 0;
                ugls_en_data.reflUVW = viewReflectDirection;
                UnityGI gi = UnityGlobalIllumination(d, 1, normalDirection, ugls_en_data );
                lightDirection = gi.light.dir;
                lightColor = gi.light.color;
/////// Diffuse:
                float NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float3 directDiffuse = max( 0.0, NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += gi.indirect.diffuse;
                float4 _DetailAlbedo_var = tex2D(_DetailAlbedo,TRANSFORM_TEX(i.uv0, _DetailAlbedo));
                float3 node_6715 = (1.0 - _DetailAlbedo_var.rgb);
                float3 node_5086 = (1.0 - (node_6715*4.0+-3.0));
                float4 _MainTex_var = tex2D(_MainTex,TRANSFORM_TEX(i.uv0, _MainTex));
                float3 diffuseColor = lerp(saturate(min(node_5086,_MainTex_var.rgb)),_MainTex_var.rgb,node_4699);
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse;
                fixed4 finalRGBA = fixed4(finalColor,1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "Meta"
            Tags {
                "LightMode"="Meta"
            }
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityMetaPass.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles ps4 
            #pragma target 3.0
            uniform sampler2D _DetailAlbedo; uniform float4 _DetailAlbedo_ST;
            uniform sampler2D _DetailMask; uniform float4 _DetailMask_ST;
            uniform sampler2D _MainTex; uniform float4 _MainTex_ST;
            struct VertexInput {
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST );
                return o;
            }
            float4 frag(VertexOutput i) : SV_Target {
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                UnityMetaInput o;
                UNITY_INITIALIZE_OUTPUT( UnityMetaInput, o );
                
                o.Emission = 0;
                
                float4 _DetailAlbedo_var = tex2D(_DetailAlbedo,TRANSFORM_TEX(i.uv0, _DetailAlbedo));
                float3 node_6715 = (1.0 - _DetailAlbedo_var.rgb);
                float3 node_5086 = (1.0 - (node_6715*4.0+-3.0));
                float4 _MainTex_var = tex2D(_MainTex,TRANSFORM_TEX(i.uv0, _MainTex));
                float4 _DetailMask_var = tex2D(_DetailMask,TRANSFORM_TEX(i.uv0, _DetailMask));
                float3 node_4699 = (1.0 - _DetailMask_var.rgb);
                float3 diffColor = lerp(saturate(min(node_5086,_MainTex_var.rgb)),_MainTex_var.rgb,node_4699);
                o.Albedo = diffColor;
                
                return UnityMetaFragment( o );
            }
            ENDCG
        }
    }
    FallBack "Legacy Shaders/Bumped Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
}
