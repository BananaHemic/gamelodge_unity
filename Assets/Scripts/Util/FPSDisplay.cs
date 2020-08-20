using UnityEngine;
using UnityEngine.UI;
using Miniscript;
using TMPro;

public class FPSDisplay : GenericSingleton<FPSDisplay>
{
    public TextMeshProUGUI FPSText;

    private float _deltaTime = 0f;
    private int _lastFPS = -1;

    private readonly char[] _fpsCharArray = new char[32];
    private const string MainTextString = "FPS: ";
    private const string LowFpsColorStr = "<color=#E57373>";
    private const string MediumFpsColorStr = "<color=#FFB74D>";
    private const string GoodFpsColorStr = "<color=#81C784>";

    void Update()
    {
        if (Time.frameCount < 1)
            return;
        if (Time.frameCount == 2)
            _deltaTime = Time.unscaledDeltaTime;
        // Low pass filter of the delta time
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

        if (Time.frameCount % 3 == 0)
        {
            SourceLine fpsLine = new SourceLine(_fpsCharArray);
            int fps = Mathf.RoundToInt(1f / _deltaTime);
            if (fps == _lastFPS)
                return;
            _lastFPS = fps;

            if (fps < 15)
                fpsLine.Append(LowFpsColorStr);
            else if (fps < 29)
                fpsLine.Append(MediumFpsColorStr);
            else
                fpsLine.Append(GoodFpsColorStr);
            fpsLine.Append(MainTextString);
            fpsLine.AppendNumber(fps);
            fpsLine.Append("</color>");
            FPSText.SetCharArray(fpsLine.GetBackingArray(), fpsLine.StartIdx, fpsLine.Length);
        }
    }
}