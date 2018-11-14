// based on https://github.com/keijiro/Pix2Pix

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UI = UnityEngine.UI;
using UnityEditor;

public class Pix2PixProcessor : MonoBehaviour {

    #region Editable attributes 
    [SerializeField] public int width = 256;
    [SerializeField] public int height = 256;
    [SerializeField] public string _weightFileName;

    bool isInitialized = false;
    #endregion

    #region Internal objects

    [HideInInspector] public RenderTexture _sourceTexture;
    [HideInInspector] public RenderTexture _resultTexture;

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
        Debug.Log("init");
        var filePath = System.IO.Path.Combine(Application.streamingAssetsPath, _weightFileName);
        if(_weightTable == null) _weightTable = Pix2Pix.WeightReader.ReadFromFile(filePath);
        if(_generator == null) _generator = new Pix2Pix.Generator(_weightTable);
        isInitialized = true;
    }

    void FinalizePix2Pix() {
        Debug.Log("fin");
        if(_generator != null) _generator.Dispose();
        if(_weightTable != null) Pix2Pix.WeightReader.DisposeTable(_weightTable);
        isInitialized = false;
    }

    public bool UpdatePix2Pix() {
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
    }

    #endregion

    #region MonoBehaviour implementation


    void OnEnable() {
#if UNITY_EDITOR
        // EditorApplication.playModeStateChanged += StateChange;
        // InitializePix2Pix();
#endif
    }

    void OnDisable() {
#if UNITY_EDITOR
        // EditorApplication.playModeStateChanged -= StateChange;
        // FinalizePix2Pix();
#endif
    }


#if UNITY_EDITOR
    void StateChange(PlayModeStateChange state) {
        if(state == PlayModeStateChange.ExitingEditMode) {
            FinalizePix2Pix();
        } else if(state == PlayModeStateChange.EnteredEditMode) {
            InitializePix2Pix();
        } else if(state == PlayModeStateChange.ExitingPlayMode) {
            FinalizePix2Pix();
        } else if(state == PlayModeStateChange.EnteredPlayMode) {
            InitializePix2Pix();
        }
    }
#endif

    void Start() {
        InitializePix2Pix();
    }

    void OnDestroy() {
        FinalizePix2Pix();
    }
    

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


    #endregion
}
