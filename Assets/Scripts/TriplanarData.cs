using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TriplanarData : MonoBehaviour
{
    Renderer rend;
    public Material triplanarMaterial;
    public TextureInfo forwardInfo;
    public Texture2D forward;

    public TextureInfo rightInfo;
    public Texture2D right;

    public TextureInfo upInfo;
    public Texture2D up;
    
    // Start is called before the first frame update
    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        triplanarMaterial.SetTexture("_UpTex", forward);
        triplanarMaterial.SetVector("_UpFov", new Vector4(forwardInfo.horizontalFov, forwardInfo.verticalFov, 0, 0));
        triplanarMaterial.SetVector("_UpPos", (forwardInfo.position));

        triplanarMaterial.SetTexture("_RightTex", up);
        triplanarMaterial.SetVector("_RightFov", new Vector4(upInfo.horizontalFov, upInfo.verticalFov, 0, 0));
        triplanarMaterial.SetVector("_RightPos", (upInfo.position));

        triplanarMaterial.SetTexture("_ForwardTex", right);
        triplanarMaterial.SetVector("_ForwardFov", new Vector4(rightInfo.horizontalFov, rightInfo.verticalFov, 0, 0));
        triplanarMaterial.SetVector("_ForwardPos", (rightInfo.position));

    }
}
