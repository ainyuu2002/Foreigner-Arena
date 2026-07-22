// PALETTE SWAP BAKE (đổi bộ màu kiểu MUGEN, feedback test P12): shader này KHÔNG render nhân vật —
// chỉ dùng 1 lần qua Graphics.Blit để "nướng" texture gốc thành bản đổi màu (PaletteSwapper cache
// RenderTexture rồi override _MainTex của SpriteRenderer qua MaterialPropertyBlock). Nhờ vậy nhân vật
// vẫn render bằng shader sprite mặc định (ăn Light2D bình thường) và KHÔNG phải đụng vào animation.
// Sprite MUGEN vốn là indexed-palette (ít màu phẳng) → so-màu-theo-dung-sai thay thế trọn bộ đồ.
Shader "FA/PaletteSwapBake"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            int _SwapCount;          // số cặp màu dùng thật (≤ 8).
            float _Tolerance;        // dung sai RGB distance khi so màu gốc.
            float4 _FromColors[8];   // màu gốc (bộ đồ P1).
            float4 _ToColors[8];     // màu thay (bộ đồ alt cho P2).

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);

                // SO MÀU TRONG KHÔNG GIAN sRGB (gamma): project dùng LINEAR color space → tex2D trả
                // giá trị đã linear-hóa (0x99 → ~0.32), còn _FromColors là số sRGB thô từ eyedropper —
                // so thẳng thì KHÔNG BAO GIỜ match (bug "P2 không đổi màu"). Quy cả hai về sRGB rồi so;
                // màu thay cũng khai báo sRGB → convert về linear khi ghi.
                #if defined(UNITY_COLORSPACE_GAMMA)
                float3 cmp = c.rgb;
                #else
                float3 cmp = LinearToGammaSpace(c.rgb);
                #endif

                for (int k = 0; k < _SwapCount; k++)
                {
                    if (distance(cmp, _FromColors[k].rgb) <= _Tolerance)
                    {
                        #if defined(UNITY_COLORSPACE_GAMMA)
                        c.rgb = _ToColors[k].rgb;
                        #else
                        c.rgb = GammaToLinearSpace(_ToColors[k].rgb);
                        #endif
                        break;
                    }
                }
                return c;
            }
            ENDCG
        }
    }
}
