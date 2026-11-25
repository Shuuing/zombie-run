using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// <see cref="ToonFantasyCityLook"/> 가 적어 두는 조명 한 벌(맵 두 벌 전환).
/// 에셋 참조(스카이박스·볼륨 프로필)는 그대로, 씬 오브젝트(해·볼륨·밤 막·바닥 안개·빛 웅덩이)는 GlobalObjectId 문자열로 든다
/// (에셋은 씬 오브젝트를 직접 참조할 수 없다). 11-13 부터 두 벌(Look_Original = 처음 · Look_Final = 마지막)을 지우지 않고 쓴다.
/// </summary>
public class ToonFantasyLookSnapshot : ScriptableObject
{
    public string scenePath;
    public string takenAt;

    [Header("Environment")]
    public int ambientMode;
    public Color ambientSky, ambientEquator, ambientGround;
    public float ambientIntensity;
    public Material skybox;

    [Header("Fog")]
    public bool fog;
    public int fogMode;
    public Color fogColor;
    public float fogStart, fogEnd, fogDensity;

    [Serializable] public class LightState { public string id; public string name; public Color color; public float intensity; }
    [Serializable] public class VolumeState { public string id; public string name; public VolumeProfile profile; }
    [Serializable] public class ToggleState { public string id; public string name; public bool on; }

    [Header("Scene objects")]
    public List<LightState> lights = new List<LightState>();
    public List<VolumeState> volumes = new List<VolumeState>();
    public List<ToggleState> nightOverlays = new List<ToggleState>();   // ProtoNightOverlay.enabled
    public List<ToggleState> objects = new List<ToggleState>();         // GameObject.activeSelf (_GroundFog · _LightPools)
}
