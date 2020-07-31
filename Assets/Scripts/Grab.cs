using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class Grab : MonoBehaviour
{
    public RawImage m_RawImage;

    private RenderTexture m_RT;

    // Start is called before the first frame update
    void Start()
    {
        Camera cam = GetComponent<Camera>();
        m_RT = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGB32);
        m_RawImage.texture = m_RT;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Texture tex = Shader.GetGlobalTexture("_BgTexture");
        if(tex != null)
        {
            Graphics.Blit(tex, m_RT);
        }
        Graphics.Blit(source, destination);
    }
}
