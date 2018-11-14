using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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
        EditorGUI.DrawPreviewTexture(rect, texturizer.forwardTex);

        rect = EditorGUILayout.GetControlRect(true, 256);
        rect.width = 256;
        EditorGUI.DrawPreviewTexture(rect, texturizer.rightTex);

        rect = EditorGUILayout.GetControlRect(true, 256);
        rect.width = 256;
        EditorGUI.DrawPreviewTexture(rect, texturizer.upTex);
    }

    public void OnSceneGUI() {
        // get the chosen game object
        Pix2pixTexturize texturizer = target as Pix2pixTexturize;

        EditorGUI.BeginChangeCheck();

        Vector3 oldForwardPos = texturizer.transform.TransformPoint(texturizer.forwardCameraPosition);
        Vector3 oldRightPos = texturizer.transform.TransformPoint(texturizer.rightCameraPosition);
        Vector3 oldUpPos = texturizer.transform.TransformPoint(texturizer.upCameraPosition);

        Vector3 newForwardPos = Handles.PositionHandle(oldForwardPos, Quaternion.identity);
        Vector3 newRightPos = Handles.PositionHandle(oldRightPos, Quaternion.identity);
        Vector3 newUpPos = Handles.PositionHandle(oldUpPos, Quaternion.identity);
        
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(texturizer, "Move camera handle");
            texturizer.forwardCameraPosition = texturizer.transform.InverseTransformPoint(newForwardPos);
            texturizer.rightCameraPosition = texturizer.transform.InverseTransformPoint(newRightPos);
            texturizer.upCameraPosition = texturizer.transform.InverseTransformPoint(newUpPos);

            texturizer.isDirty = true;
            texturizer.Update();

            // texturizer.Bake();
        }

    }

}

#endif

[ExecuteInEditMode]
public class Pix2pixTexturize : MonoBehaviour {

    public Camera textureComputer;
    public string weightFileName;
    public Material editorMaterial;
    public Shader triplanarShader;
    public Vector3 forwardCameraPosition = Vector3.forward;
    public Vector3 rightCameraPosition = Vector3.right;
    public Vector3 upCameraPosition = Vector3.up;

    public RenderTexture forwardTex;
    public RenderTexture rightTex;
    public RenderTexture upTex;

    public Vector2 forwardOff = Vector2.zero;
    public Vector2 forwardScale = Vector2.one;
    public bool autoBake = false;
    
    [HideInInspector] public bool isDirty;
    [HideInInspector] public RenderTexture temp;

    Bounds bounds;
    Material triplanarMaterial;
    Renderer rend;

    private void OnEnable() {
        Start();
    }

    // Start is called before the first frame update
    void Start() {
        bounds = new Bounds(Vector3.zero, new Vector3(Mathf.Abs(rightCameraPosition.x) * 2.0f,
                                                            Mathf.Abs(upCameraPosition.y) * 2.0f,
                                                            Mathf.Abs(forwardCameraPosition.z) * 2.0f));

        rend = GetComponent<Renderer>();

        forwardTex = new RenderTexture(256, 256, 16);
        forwardTex.enableRandomWrite = true;
        forwardTex.filterMode = FilterMode.Point;
        forwardTex.Create();

        rightTex = new RenderTexture(256, 256, 16);
        rightTex.enableRandomWrite = true;
        rightTex.filterMode = FilterMode.Point;
        rightTex.Create();

        upTex = new RenderTexture(256, 256, 16);
        upTex.enableRandomWrite = true;
        upTex.filterMode = FilterMode.Point;
        upTex.Create();

        triplanarMaterial = new Material(triplanarShader);

        InitTextures();
    }

    void InitTextures() {
        temp = new RenderTexture(256, 256, 0);
        temp.enableRandomWrite = true;
        temp.Create();
    }

    void OnDrawGizmosSelected() {
        CalcPositons();
        DrawBox();
        // Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    // Update is called once per frame
    public void Update() {
        if (Application.isPlaying) return;

        // TODO: this should actually be the bounds of the camera
        bounds = new Bounds(Vector3.zero, new Vector3(Mathf.Abs(rightCameraPosition.x) * 2.0f,
                                                      Mathf.Abs(upCameraPosition.y) * 2.0f,
                                                      Mathf.Abs(forwardCameraPosition.z) * 2.0f));

        if (isDirty) {
            rend = GetComponent<Renderer>();
            rend.sharedMaterial = editorMaterial;

            var oldTex = textureComputer.targetTexture;
            var oldPos = textureComputer.transform.position;
            var oldRot = textureComputer.transform.rotation;

            //textureComputer.clearFlags = CameraClearFlags.Depth;
            textureComputer.transform.position = transform.TransformPoint(rightCameraPosition);
            textureComputer.transform.LookAt(this.transform);
            textureComputer.targetTexture = rightTex;
            textureComputer.Render();

            textureComputer.transform.position = transform.TransformPoint(upCameraPosition);
            textureComputer.transform.LookAt(this.transform);
            textureComputer.targetTexture = upTex;
            textureComputer.Render();

            textureComputer.transform.position = transform.TransformPoint(forwardCameraPosition);
            textureComputer.transform.LookAt(this.transform);
            textureComputer.targetTexture = forwardTex;
            textureComputer.Render();

            //textureComputer.transform.position = oldPos;
            //textureComputer.transform.rotation = oldRot;

            textureComputer.targetTexture = oldTex;

            SetupTriplanar();

        }

    }

    void SetupTriplanar() {
        if (triplanarMaterial == null) {
            triplanarMaterial = new Material(triplanarShader);
        }

        triplanarMaterial.SetTexture("_UpTex", forwardTex);
        triplanarMaterial.SetTexture("_RightTex", upTex);
        triplanarMaterial.SetTexture("_ForwardTex", rightTex);
        triplanarMaterial.SetVector("_Extents", bounds.extents);

        rend.sharedMaterial = triplanarMaterial;

        if (GetComponent<Material>()) {
            GetComponent<Material>().CopyPropertiesFromMaterial(rend.sharedMaterial);
        }
    }

    public void Bake(Pix2PixStandalone Pix2Pix = null) {
        var P2P = Pix2Pix ?? new Pix2PixStandalone(weightFileName);

        P2P.Render(forwardTex, temp);
        Graphics.Blit(temp, forwardTex);

        P2P.Render(rightTex, temp);
        Graphics.Blit(temp, rightTex);

        P2P.Render(upTex, temp);
        Graphics.Blit(temp, upTex);

        isDirty = false;

        if (Pix2Pix == null) P2P.Dispose();
    }

    Color color = Color.green;

    Vector3 v3FrontTopLeft;
    Vector3 v3FrontTopRight;
    Vector3 v3FrontBottomLeft;
    Vector3 v3FrontBottomRight;
    Vector3 v3BackTopLeft;
    Vector3 v3BackTopRight;
    Vector3 v3BackBottomLeft;
    Vector3 v3BackBottomRight;

    void CalcPositons() {

        //Bounds bounds;
        //BoxCollider bc = GetComponent<BoxCollider>();
        //if (bc != null)
        //    bounds = bc.bounds;
        //else
        //return;

        Vector3 v3Center = bounds.center;
        Vector3 v3Extents = bounds.extents;

        v3FrontTopLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y + v3Extents.y, v3Center.z - v3Extents.z);  // Front top left corner
        v3FrontTopRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y + v3Extents.y, v3Center.z - v3Extents.z);  // Front top right corner
        v3FrontBottomLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y - v3Extents.y, v3Center.z - v3Extents.z);  // Front bottom left corner
        v3FrontBottomRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y - v3Extents.y, v3Center.z - v3Extents.z);  // Front bottom right corner
        v3BackTopLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y + v3Extents.y, v3Center.z + v3Extents.z);  // Back top left corner
        v3BackTopRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y + v3Extents.y, v3Center.z + v3Extents.z);  // Back top right corner
        v3BackBottomLeft = new Vector3(v3Center.x - v3Extents.x, v3Center.y - v3Extents.y, v3Center.z + v3Extents.z);  // Back bottom left corner
        v3BackBottomRight = new Vector3(v3Center.x + v3Extents.x, v3Center.y - v3Extents.y, v3Center.z + v3Extents.z);  // Back bottom right corner

        v3FrontTopLeft = transform.TransformPoint(v3FrontTopLeft);
        v3FrontTopRight = transform.TransformPoint(v3FrontTopRight);
        v3FrontBottomLeft = transform.TransformPoint(v3FrontBottomLeft);
        v3FrontBottomRight = transform.TransformPoint(v3FrontBottomRight);
        v3BackTopLeft = transform.TransformPoint(v3BackTopLeft);
        v3BackTopRight = transform.TransformPoint(v3BackTopRight);
        v3BackBottomLeft = transform.TransformPoint(v3BackBottomLeft);
        v3BackBottomRight = transform.TransformPoint(v3BackBottomRight);
    }

    void DrawBox() {
        //if (Input.GetKey (KeyCode.S)) {
        Debug.DrawLine(v3FrontTopLeft, v3FrontTopRight, color);
        Debug.DrawLine(v3FrontTopRight, v3FrontBottomRight, color);
        Debug.DrawLine(v3FrontBottomRight, v3FrontBottomLeft, color);
        Debug.DrawLine(v3FrontBottomLeft, v3FrontTopLeft, color);

        Debug.DrawLine(v3BackTopLeft, v3BackTopRight, color);
        Debug.DrawLine(v3BackTopRight, v3BackBottomRight, color);
        Debug.DrawLine(v3BackBottomRight, v3BackBottomLeft, color);
        Debug.DrawLine(v3BackBottomLeft, v3BackTopLeft, color);

        Debug.DrawLine(v3FrontTopLeft, v3BackTopLeft, color);
        Debug.DrawLine(v3FrontTopRight, v3BackTopRight, color);
        Debug.DrawLine(v3FrontBottomRight, v3BackBottomRight, color);
        Debug.DrawLine(v3FrontBottomLeft, v3BackBottomLeft, color);
        //}
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