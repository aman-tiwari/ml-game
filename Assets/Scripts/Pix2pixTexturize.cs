using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR

[CustomEditor(typeof(Pix2pixTexturize))]
public class Pix2pixTexturizEditor : Editor {

    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        Pix2pixTexturize texturizer = target as Pix2pixTexturize;

        if (EditorGUILayout.DropdownButton(new GUIContent("Bake", "Run Pix2Pix"), FocusType.Keyboard)) {
            texturizer.Bake();
        }


        Rect rect = EditorGUILayout.GetControlRect(true, 256);
        rect.width = 256;

        EditorGUI.DrawPreviewTexture(rect, texturizer.forwardRTex);

        rect = EditorGUILayout.GetControlRect(true, 256);
        rect.width = 256;
        EditorGUI.DrawPreviewTexture(rect, texturizer.rightRTex);

        rect = EditorGUILayout.GetControlRect(true, 256);
        rect.width = 256;
        EditorGUI.DrawPreviewTexture(rect, texturizer.upRTex);
    }

    public void OnSceneGUI() {
        // get the chosen game object
        Pix2pixTexturize texturizer = target as Pix2pixTexturize;
        var transform = texturizer.transform;
        EditorGUI.BeginChangeCheck();

        Vector3 oldForwardPos = texturizer.transform.TransformPoint(texturizer.tpData.forwardInfo.position);
        Vector3 oldRightPos = texturizer.transform.TransformPoint(texturizer.tpData.rightInfo.position);
        Vector3 oldUpPos = texturizer.transform.TransformPoint(texturizer.tpData.upInfo.position);

        Vector3 newForwardPos = Handles.PositionHandle(oldForwardPos, Quaternion.LookRotation(transform.position - oldForwardPos, transform.up));
        Vector3 newRightPos = Handles.PositionHandle(oldRightPos, Quaternion.LookRotation(transform.position - oldRightPos, transform.up));
        Vector3 newUpPos = Handles.PositionHandle(oldUpPos, Quaternion.LookRotation(transform.position - oldUpPos, transform.forward));
        
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(texturizer, "Move triplanr pix2pix texturizer handles");
            texturizer.tpData.forwardInfo.position = texturizer.transform.InverseTransformPoint(newForwardPos);
            texturizer.tpData.rightInfo.position = texturizer.transform.InverseTransformPoint(newRightPos);
            texturizer.tpData.upInfo.position = texturizer.transform.InverseTransformPoint(newUpPos);
            texturizer.isDirty = true;
            texturizer.Update();
        }

    }

}

#endif

[System.Serializable]
public struct TextureInfo {
    public Vector3 position;
    [Range(10.0f, 120.0f)] public float verticalFov;
    [Range(10.0f, 120.0f)] public float horizontalFov;

    public TextureInfo(Vector3 position, float verticalFov, float horizontalFov) {
        this.position = position;
        this.verticalFov = verticalFov;
        this.horizontalFov = horizontalFov;
    }

    public void Apply(Camera other) {
        other.transform.position = position;
        other.fieldOfView = verticalFov;
        other.aspect = horizontalFov / verticalFov;
    }
}

[ExecuteInEditMode]
public class Pix2pixTexturize : MonoBehaviour {

    public Camera textureCam;
    public string weightFileName;
    public Material editorMaterial;
    public Shader triplanarShader;

    public RenderTexture forwardRTex;
    public RenderTexture rightRTex;
    public RenderTexture upRTex;

    string guid; 

    public Vector2 forwardOff = Vector2.zero;
    public Vector2 forwardScale = Vector2.one;
    public bool autoBake = false;
    
    [HideInInspector] public bool isDirty;
    [HideInInspector] public RenderTexture temp;

    Renderer rend;

    public TriplanarData tpData;

    private void OnEnable() {
        Start();
    }

    private void ForeachChild(System.Action<Transform> fn) {
        var transforms = GetComponentsInChildren<Transform>();
        foreach(var t in transforms) {
            if(t != this.transform) {
                fn(t);
            }
        }
    }

    // Start is called before the first frame update
    void Start() {

        rend = GetComponent<Renderer>();

        SetupTpData(transform);
        ForeachChild(SetupTpData);

        temp = new RenderTexture(256, 256, 0);
        temp.enableRandomWrite = true;
        temp.Create();

        forwardRTex = InitRTexture();
        rightRTex = InitRTexture();
        upRTex = InitRTexture();

        textureCam.transform.position = transform.TransformPoint(tpData.forwardInfo.position);

        tpData.forwardInfo.Apply(textureCam);

        CopyToTpTextures(tpData);

        // SaveAssets();
    }

    void SetupTpData(Transform t) {
        if (t.GetComponent<Renderer>() == null) return;
        var tpData = t.GetComponent<TriplanarData>();
        if(tpData == null) {
            tpData = t.gameObject.AddComponent<TriplanarData>();
            tpData.forwardInfo.position = Vector3.forward;
            tpData.forwardInfo.verticalFov = textureCam.fieldOfView;
            tpData.forwardInfo.horizontalFov = textureCam.fieldOfView * textureCam.aspect;

            tpData.rightInfo.position = Vector3.right;
            tpData.rightInfo.verticalFov = textureCam.fieldOfView;
            tpData.rightInfo.horizontalFov = textureCam.fieldOfView * textureCam.aspect;

            tpData.upInfo.position = Vector3.up;
            tpData.upInfo.verticalFov = textureCam.fieldOfView;
            tpData.upInfo.horizontalFov = textureCam.fieldOfView * textureCam.aspect;

            tpData.triplanarMaterial = new Material(triplanarShader);

            tpData.forward = this.tpData.forward ?? new Texture2D(256, 256);
            tpData.forward.filterMode = FilterMode.Point;

            tpData.right = this.tpData.right ?? new Texture2D(256, 256);
            tpData.right.filterMode = FilterMode.Point;

            tpData.up = this.tpData.up ?? new Texture2D(256, 256);
            tpData.up.filterMode = FilterMode.Point;

            tpData.offset = t.position - this.transform.position;
        }

    }

    RenderTexture InitRTexture() {
        var ret = new RenderTexture(256, 256, 32);
        ret.enableRandomWrite = true;
        ret.useMipMap = true;
        ret.Create();
        return ret;
    }

    void CopyToTpTextures(TriplanarData tpData) {
        Graphics.CopyTexture(forwardRTex, tpData.forward);
        Graphics.CopyTexture(rightRTex, tpData.right);
        Graphics.CopyTexture(upRTex, tpData.up);
    }

    void SaveAssets() {

        return;
        if(!File.Exists("Assets/Materials/Pix2pixTriplanar") || guid == null) { 
            guid = AssetDatabase.CreateFolder("Assets/Materials/Pix2pixTriplanar", name);
            string folderPath = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.CreateAsset(tpData.forward, Path.Combine(folderPath, "forward.png"));
            AssetDatabase.CreateAsset(tpData.right, Path.Combine(folderPath, "right.png"));
            AssetDatabase.CreateAsset(tpData.up, Path.Combine(folderPath, "up.png"));
            AssetDatabase.CreateAsset(tpData.triplanarMaterial, Path.Combine(folderPath, "material.mat"));
            AssetDatabase.SaveAssets();
        } else {
            AssetDatabase.SaveAssets();
        }
    }

    Texture2D RenderTextureToTexture2D(RenderTexture rtex) {
        var result = new Texture2D(rtex.width, rtex.height);
        Graphics.CopyTexture(rtex, result);
        return result;
    }

    void SaveTexture(Texture2D tex, string path) {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
    }

    // Update is called once per frame
    public void Update() {
        if (Application.isPlaying) return;

        if (isDirty) {
            rend = GetComponent<Renderer>();
            rend.sharedMaterial = editorMaterial;

            ForeachChild(t => {
                if (t.GetComponent<Renderer>()) {
                    t.GetComponent<Renderer>().sharedMaterial = editorMaterial;
                }
            });

            var oldTex = textureCam.targetTexture;
            var oldPos = textureCam.transform.position;
            var oldRot = textureCam.transform.rotation;

            //textureComputer.clearFlags = CameraClearFlags.Depth;
            tpData.rightInfo.Apply(textureCam);
            textureCam.transform.position = transform.TransformPoint(tpData.rightInfo.position);
            textureCam.transform.LookAt(this.transform, this.transform.up);
            textureCam.targetTexture = rightRTex;
            textureCam.Render();

            tpData.upInfo.Apply(textureCam);
            textureCam.transform.position = transform.TransformPoint(tpData.upInfo.position);
            textureCam.transform.LookAt(this.transform, this.transform.forward);
            textureCam.targetTexture = upRTex;
            textureCam.Render();

            tpData.forwardInfo.Apply(textureCam);
            textureCam.transform.position = transform.TransformPoint(tpData.forwardInfo.position);
            textureCam.transform.LookAt(this.transform, this.transform.up);
            textureCam.targetTexture = forwardRTex;
            textureCam.Render();

            CopyToTpTextures(tpData);

            rend.sharedMaterial = tpData.triplanarMaterial;
            ForeachChild(t => {
                if (t.GetComponent<Renderer>()) {
                    t.GetComponent<Renderer>().sharedMaterial = t.GetComponent<TriplanarData>().triplanarMaterial;
                }
                if(t.GetComponent<TriplanarData>()) {
                    var tpData = t.GetComponent<TriplanarData>();
                    tpData.forwardInfo = this.tpData.forwardInfo;
                    tpData.forward = this.tpData.forward;
                    tpData.rightInfo = this.tpData.rightInfo;
                    tpData.right = this.tpData.right;
                    tpData.upInfo = this.tpData.upInfo;
                    tpData.up = this.tpData.up;
                    
                }
            });
            //textureComputer.transform.position = oldPos;
            //textureComputer.transform.rotation = oldRot;

            textureCam.targetTexture = oldTex;

        } else {
            rend.sharedMaterial = tpData.triplanarMaterial;
        }
    }

    public void Bake(Pix2PixStandalone Pix2Pix = null) {
        var P2P = Pix2Pix ?? new Pix2PixStandalone(weightFileName);

        P2P.Render(forwardRTex, temp);
        Graphics.Blit(temp, forwardRTex);

        P2P.Render(rightRTex, temp);
        Graphics.Blit(temp, rightRTex);

        P2P.Render(upRTex, temp);
        Graphics.Blit(temp, upRTex);

        isDirty = false;

        if (Pix2Pix == null) P2P.Dispose();

        CopyToTpTextures(tpData);
    }

}

    public class Pix2PixStandalone  : System.IDisposable {

    public int width = 256;
    public int height = 256;
    public string _weightFileName;
    bool isInitialized = false;

    public Pix2PixStandalone(string weightFileName, int width = 256, int height = 256) {
        this.width = width;
        this.height = height;
        this._weightFileName = weightFileName;
        InitializePix2Pix();
    }

    public void Dispose() {
        FinalizePix2Pix();
    }

    #region Internal objects

    //public RenderTexture _sourceTexture;
    //public RenderTexture _resultTexture;

    #endregion

    #region Pix2Pix implementation

    Dictionary<string, Pix2Pix.Tensor> _weightTable;
    Pix2Pix.Generator _generator;

    float _budget = 100;
    float _budgetAdjust = 10;

    readonly string[] _performanceLabels = {
        "N/A", "Poor", "Moderate", "Good", "Great", "Excellent"
    };

    void InitializePix2Pix() {
        var filePath = System.IO.Path.Combine(Application.streamingAssetsPath, _weightFileName);
        if (_weightTable == null) _weightTable = Pix2Pix.WeightReader.ReadFromFile(filePath);
        if (_generator == null) _generator = new Pix2Pix.Generator(_weightTable);
        isInitialized = true;
    }

    void FinalizePix2Pix() {
        if (_generator != null) _generator.Dispose();
        if (_weightTable != null) Pix2Pix.WeightReader.DisposeTable(_weightTable);
        isInitialized = false;
    }

    /*public bool UpdatePix2Pix() {
        bool done = false;
        // Advance the Pix2Pix inference until the current budget runs out.
        for (var cost = 0.0f; cost < _budget;) {
            if (!_generator.Running) _generator.Start(_sourceTexture);

            cost += _generator.Step();

            if (!_generator.Running) {
                _generator.GetResult(_resultTexture);
                done = true;
            }
        }

        Pix2Pix.GpuBackend.ExecuteAndClearCommandBuffer();

        // Review the budget depending on the current frame time.
        _budget -= (Time.deltaTime * 60 - 1.25f) * _budgetAdjust;
        _budget = Mathf.Clamp(_budget, 150, 1200);

        _budgetAdjust = Mathf.Max(_budgetAdjust - 0.05f, 0.5f);

        // Update the text display.
        var rate = 60 * _budget / 1000;

        var perf = (_budgetAdjust < 1) ?
            _performanceLabels[(int)Mathf.Min(5, _budget / 100)] :
            "Measuring GPU performance...";

        // _textUI.text =
        //     string.Format("pix2Pix refresh rate: {0:F1} Hz", rate);
        return done;
    }*/

    #endregion

    public void Render(RenderTexture source, RenderTexture output) {
        // if (_generator == null || !isInitialized) InitializePix2Pix();
        // Debug.Log(string.Format("{0}, {1}", source == null, _generator == null));
        _generator.Start(source);
        _generator.Step();
        while (_generator.Running) {
            _generator.Step();
        }
        _generator.GetResult(output);
        Pix2Pix.GpuBackend.ExecuteAndClearCommandBuffer();
    }

}