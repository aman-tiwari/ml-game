// from https://github.com/keijiro/Pix2Pix

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UI = UnityEngine.UI;

public class Pix2PixCamera : MonoBehaviour {

    public Pix2PixProcessor processor;
    public UI.Text _textUI;

    RenderTexture _resultTexture;

    void Start() {
        if(processor == null) {
            processor = FindObjectOfType<Pix2PixProcessor>();
        }

        _resultTexture = new RenderTexture(processor.width, processor.height, 0);
        _resultTexture.enableRandomWrite = true;
        _resultTexture.Create();

    }

    void OnRenderImage(RenderTexture source, RenderTexture destination) {
        float t = Time.realtimeSinceStartup;
        processor.Render(source, _resultTexture);
        Pix2Pix.GpuBackend.ExecuteAndClearCommandBuffer();
        Graphics.Blit(_resultTexture, destination);
        float dt = Time.realtimeSinceStartup - t;
        _textUI.text =
            string.Format("pix2Pix run time: {0:F3} ms", dt * 1000);
    }
}
