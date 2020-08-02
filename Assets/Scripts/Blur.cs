using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class Blur : MonoBehaviour
{
    public Shader m_GrabShader;
    public Shader m_BlurShader;
    [Tooltip("模糊程度")]
    public float m_BufferSize = 0.5f;

    private RawImage m_RI;
    private RenderTexture m_RT;
    private bool m_IsBluring = false;
    private Material m_OriMat;
    private Material m_GrabMat;
    private Material m_BlurMat;

    private static int BG_TEXTURE_ID = Shader.PropertyToID("_BgTexture");
    private static int TEMP_1_ID = Shader.PropertyToID("_Temp1");
    private static int TEMP_2_ID = Shader.PropertyToID("_Temp2");

    // Start is called before the first frame update
    void Start()
    {
        m_RI = GetComponent<RawImage>();
        Camera cam = m_RI.canvas.worldCamera;
        m_RT = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGB32);
        m_RI.texture = m_RT;
        m_RI.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void DoBlur()
    {
        if (m_IsBluring) return;

        if (!m_GrabMat)
        {
            m_GrabMat = new Material(m_GrabShader);
            m_GrabMat.hideFlags = HideFlags.HideAndDontSave;
        }

        m_OriMat = m_RI.material;
        m_RI.material = m_GrabMat;
        m_RI.enabled = true;

        m_IsBluring = true;
    }

    private void OnRenderObject()
    {
        if (!m_IsBluring) return;

        Texture tex = Shader.GetGlobalTexture(BG_TEXTURE_ID);
        if (tex != null)
        {
            //Graphics.Blit(tex, m_RT);

            Camera cam = m_RI.canvas.worldCamera;
            int width = cam.pixelWidth;
            int height = cam.pixelHeight;
            int widthHalf = width / 2;
            int heightHalf = height / 2;

            // get two smaller RTs
            RenderTexture temp1 = RenderTexture.GetTemporary(widthHalf, heightHalf, 0, RenderTextureFormat.ARGB32);
            RenderTexture temp2 = RenderTexture.GetTemporary(widthHalf, heightHalf, 0, RenderTextureFormat.ARGB32);

            // downsample screen copy into smaller RT, release screen RT
            Graphics.Blit(tex, temp1);

            if (!m_BlurMat)
            {
                m_BlurMat = new Material(m_BlurShader);
                m_BlurMat.hideFlags = HideFlags.HideAndDontSave;
            }

            // horizontal blur
            Shader.SetGlobalVector("offsets", new Vector4(2.0f * m_BufferSize / width, 0, 0, 0));
            Graphics.Blit(temp1, temp2, m_BlurMat);
            // vertical blur
            Shader.SetGlobalVector("offsets", new Vector4(0, 2.0f * m_BufferSize / height, 0, 0));
            Graphics.Blit(temp2, temp1, m_BlurMat);
            // horizontal blur
            Shader.SetGlobalVector("offsets", new Vector4(4.0f * m_BufferSize / width, 0, 0, 0));
            Graphics.Blit(temp1, temp2, m_BlurMat);
            // vertical blur
            Shader.SetGlobalVector("offsets", new Vector4(0, 4.0f * m_BufferSize / height, 0, 0));
            Graphics.Blit(temp2, temp1, m_BlurMat);

            Graphics.Blit(temp1, m_RT);

            m_RI.material = m_OriMat;
            
            m_IsBluring = false;
        }
    }
}
