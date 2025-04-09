using FastAndFractured;
using FMOD;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Utilities;

public struct SubstitleData
{
    public string Text;
    public float Duration;
    public Color TextColor;
    public int FontSize;
    public string SpeakerName;
}
public class SubtitlesManager : AbstractSingleton<SubtitlesManager>
{
    [SerializeField] private TextMeshProUGUI substitleText;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private CanvasGroup canvasGroup;

    private SubstitleData _currentSubtitle;
    private ITimer _displayTimer;
    private ITimer _fadeTimer;
    private bool _isShowing = false;
    protected override void Awake()
    {
        base.Awake();

    }
}
