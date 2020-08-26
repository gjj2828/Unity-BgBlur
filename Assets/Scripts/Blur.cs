//#define USE_CB_BLUR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(RectTransform))]
public class Blur : MonoBehaviour
{
    private enum EBlurState
    {
          NONE
    #if USE_CB_BLUR
        , GRAB
        , CB
    #else // USE_CB_BLUR
        , BLUR
    #endif // USE_CB_BLUR
    }

    public Shader m_GrabShader;
    public Shader m_BlurShader;
    [Tooltip("模糊程度")]
    public float m_BufferSize = 0.5f;

    private RawImage m_RI;
    private RectTransform m_Trans;
    private RenderTexture m_RT;
    private EBlurState m_BlurState = EBlurState.NONE;
    private Material m_OriMat;
    private Material m_GrabMat;
    private Material m_BlurMat;
    private Vector4 m_LastOffsetSize;
    private int m_LastWidth;
    private int m_LastHeight;

#if USE_CB_BLUR
    private CommandBuffer m_CB;
#endif // USE_CB_BLUR
    private YieldInstruction m_EndOfFrame = new WaitForEndOfFrame();

    private static int BG_TEXTURE_ID = Shader.PropertyToID("_BgTexture");
    private static int OFFSET_SIZE_ID = Shader.PropertyToID("_OffsetSize");
#if USE_CB_BLUR
    private static int TEMP_1_ID = Shader.PropertyToID("_Temp1");
    private static int TEMP_2_ID = Shader.PropertyToID("_Temp2");
    private static int BLUR_TEX_ID = Shader.PropertyToID("_BlurTex");

    private static CameraEvent CAM_EVT = CameraEvent.AfterEverything;
#endif // USE_CB_BLUR

    private Material blurMat
    {
        get
        {
            if(!m_BlurMat)
            {
                m_BlurMat = new Material(m_BlurShader);
                m_BlurMat.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_BlurMat;
        }
    }

    private Material grabMat
    {
        get
        {
            if(!m_GrabMat)
            {
                m_GrabMat = new Material(m_GrabShader);
                m_GrabMat.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_GrabMat;
        }
    }

    void Start()
    {
        m_RI = GetComponent<RawImage>();
        m_RI.enabled = false;
        m_Trans = GetComponent<RectTransform>();
        UpdateOffsetSize(m_Trans.rect, true);

#if USE_CB_BLUR
        m_CB = new CommandBuffer();
        m_CB.name = "Blur";
#endif // USE_CB_BLUR
    }

#if USE_CB_BLUR
    private void AddCommandBuffer()
    {
        Camera cam = Camera.current;

        int width = cam.pixelWidth;
        int height = cam.pixelHeight;

        m_CB.GetTemporaryRT(TEMP_1_ID, -2, -2, 0, FilterMode.Bilinear);
        m_CB.GetTemporaryRT(TEMP_2_ID, -2, -2, 0, FilterMode.Bilinear);
        m_CB.Blit(m_RT, TEMP_1_ID);
        // horizontal blur
        m_CB.SetGlobalVector("offsets", new Vector4(2.0f * m_BufferSize / width, 0, 0, 0));
        m_CB.Blit(TEMP_1_ID, TEMP_2_ID, blurMat);
        // vertical blur
        m_CB.SetGlobalVector("offsets", new Vector4(0, 2.0f * m_BufferSize / height, 0, 0));
        m_CB.Blit(TEMP_2_ID, TEMP_1_ID, blurMat);
        // horizontal blur
        m_CB.SetGlobalVector("offsets", new Vector4(4.0f * m_BufferSize / width, 0, 0, 0));
        m_CB.Blit(TEMP_1_ID, TEMP_2_ID, blurMat);
        // vertical blur
        m_CB.SetGlobalVector("offsets", new Vector4(0, 4.0f * m_BufferSize / height, 0, 0));
        m_CB.Blit(TEMP_2_ID, m_RT, blurMat);

        m_CB.ReleaseTemporaryRT(TEMP_1_ID);
        m_CB.ReleaseTemporaryRT(TEMP_2_ID);

        cam.AddCommandBuffer(CAM_EVT, m_CB);
    }

    private void RemoveCommandBuffer()
    {
        Camera cam = Camera.current;

        cam.RemoveCommandBuffer(CAM_EVT, m_CB);
        m_CB.Clear();
    }
#endif // USE_CB_BLUR

    void Update()
    {
        UpdateOffsetSize(m_Trans.rect);
    }

    private void UpdateOffsetSize(Rect rect, bool force = false)
    {
        Material mat = m_OriMat ? m_OriMat : m_RI.material;
        if(mat == null) return;

        Camera cam = m_RI.canvas.worldCamera;

        Vector3 wpMin = m_Trans.transform.TransformPoint(rect.xMin, rect.yMin, 0.0f);
        Vector3 wpMax = m_Trans.transform.TransformPoint(rect.xMax, rect.yMax, 0.0f);
        Vector2 spMin = RectTransformUtility.WorldToScreenPoint(cam, wpMin);
        Vector2 spMax = RectTransformUtility.WorldToScreenPoint(cam, wpMax);

        int width = cam.pixelWidth;
        int height = cam.pixelHeight;

        Vector4 offsetSize = new Vector4(spMin.x / width, spMin.y / height, (spMax.x - spMin.x) / width, (spMax.y - spMin.y) / height);
        if(!force && offsetSize == m_LastOffsetSize) return;

        mat.SetVector(OFFSET_SIZE_ID, offsetSize);

        m_LastOffsetSize = offsetSize;
    }

    public void DoBlur()
    {
        StartBlur();
    }

    public void ClearBlur()
    {
        m_RI.enabled = false;
        m_RI.texture = null;
        if(m_RT != null)
        {
            RenderTexture.ReleaseTemporary(m_RT);
            m_RT = null;
        }
    }

    private void StartBlur()
    {
        if (m_BlurState != EBlurState.NONE) return;

        m_OriMat = m_RI.material;
        m_RI.material = grabMat;
        m_RI.enabled = true;

    #if USE_CB_BLUR
        m_BlurState = EBlurState.GRAB;
    #else // USE_CB_BLUR
        StartCoroutine(StopBlur());
        m_BlurState = EBlurState.BLUR;
    #endif // USE_CB_BLUR
    }

#if !USE_CB_BLUR
    private IEnumerator StopBlur()
    {
        yield return m_EndOfFrame;
        BlurOver();
    }
#endif // !USE_CB_BLUR

    private void BlurOver()
    {
        m_RI.material = m_OriMat;
        m_OriMat = null;
        m_BlurState = EBlurState.NONE;
    }

#if USE_CB_BLUR
    private void OnRenderObject()
    {
        if(m_BlurState == EBlurState.NONE) return;
        if(Camera.current != m_RI.canvas.worldCamera) return;

        if(m_BlurState == EBlurState.GRAB)
        {
            Texture tex = Shader.GetGlobalTexture(BG_TEXTURE_ID);
            if(tex != null)
            {
                Camera cam = Camera.current;
                int width = cam.pixelWidth;
                int height = cam.pixelHeight;

                if(m_RT != null && (width != m_LastWidth || height != m_LastHeight))
                {
                    m_RI.texture = null;
                    RenderTexture.ReleaseTemporary(m_RT);
                    m_RT = null;
                }
                if(m_RT == null)
                {
                    m_RT = RenderTexture.GetTemporary(width, height);
                    m_RI.texture = m_RT;
                    m_LastWidth = width;
                    m_LastHeight = height;
                }

                Graphics.Blit(tex, m_RT);

                AddCommandBuffer();
                m_BlurState = EBlurState.CB;
            }
            else
            {
                m_BlurState = EBlurState.NONE;
            }
        }
        else if(m_BlurState == EBlurState.CB)
        {
            RemoveCommandBuffer();
            BlurOver();
        }


    }
#else // USE_CB_BLUR
    private void OnRenderObject()
    {
        if (m_BlurState != EBlurState.BLUR) return;
        if (Camera.current != m_RI.canvas.worldCamera) return;

        Texture tex = Shader.GetGlobalTexture(BG_TEXTURE_ID);
        if (tex != null)
        {
            //Graphics.Blit(tex, m_RT);

            Camera cam = Camera.current;
            int width = cam.pixelWidth;
            int height = cam.pixelHeight;
            int widthHalf = width / 2;
            int heightHalf = height / 2;

            if(m_RT != null && (width != m_LastWidth || height != m_LastHeight))
            {
                m_RI.texture = null;
                RenderTexture.ReleaseTemporary(m_RT);
                m_RT = null;
            }
            if(m_RT == null)
            {
                m_RT = RenderTexture.GetTemporary(width, height);
                m_RI.texture = m_RT;
                m_LastWidth = width;
                m_LastHeight = height;
            }

            // get two smaller RTs
            RenderTexture temp1 = RenderTexture.GetTemporary(widthHalf, heightHalf);
            RenderTexture temp2 = RenderTexture.GetTemporary(widthHalf, heightHalf);

            // downsample screen copy into smaller RT, release screen RT
            Graphics.Blit(tex, temp1);

            // horizontal blur
            Shader.SetGlobalVector("offsets", new Vector4(2.0f * m_BufferSize / width, 0, 0, 0));
            Graphics.Blit(temp1, temp2, blurMat);
            // vertical blur
            Shader.SetGlobalVector("offsets", new Vector4(0, 2.0f * m_BufferSize / height, 0, 0));
            Graphics.Blit(temp2, temp1, blurMat);
            // horizontal blur
            Shader.SetGlobalVector("offsets", new Vector4(4.0f * m_BufferSize / width, 0, 0, 0));
            Graphics.Blit(temp1, temp2, blurMat);
            // vertical blur
            Shader.SetGlobalVector("offsets", new Vector4(0, 4.0f * m_BufferSize / height, 0, 0));
            Graphics.Blit(temp2, m_RT, blurMat);

            RenderTexture.ReleaseTemporary(temp1);
            RenderTexture.ReleaseTemporary(temp2);
        }
    }
#endif // USE_CB_BLUR
}
