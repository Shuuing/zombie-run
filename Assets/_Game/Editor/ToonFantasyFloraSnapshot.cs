using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="ToonFantasyCityFlora"/> 가 나무·덤불·꽃 교체 직전에 적어 두는 기록(<c>Flora_Original.asset</c>). 씬 오브젝트는 GlobalObjectId 문자열로 든다.
/// 풀은 11-13 정리 뒤로 기록 없이 두 벌 전환(<see cref="CityGrassVersions"/>)을 쓴다 — 예전 풀 기록(Grass_Original · Weeds_Original)은 ToonFantasyCity/Backup~ 에 있다.
/// </summary>
public class ToonFantasyFloraSnapshot : ScriptableObject
{
    public string takenAt;

    [Serializable] public class Swap { public string name; public string original; public bool wasActive; public string replacement; }

    [Header("나무·덤불·꽃")]
    public List<Swap> swaps = new List<Swap>();
}
