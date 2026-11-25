using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// <c>Resources/SFX</c> 의 효과음 임포트 설정을 고정한다.
    ///
    /// <para><b>왜 후처리기인가.</b> 뱅크는 <c>synth.py</c>/<c>hf_post.py</c> 가 다시 만든다.
    /// 설정을 손으로 찍어두면 파일을 덮어쓸 때마다 누가 언제 다시 찍어야 하는지가 문제가 된다.
    /// 여기 두면 <b>파일이 들어오는 순간</b> 맞는 설정으로 들어온다.</para>
    ///
    /// <para><b>왜 PCM 인가 (기본값 Vorbis 가 아니라).</b> 두 가지 이유가 있다.
    /// <list type="number">
    /// <item>Vorbis 인코더는 클립 앞뒤에 패딩을 남긴다. 1.12초짜리 <c>flame_loop</c> 처럼
    /// 이음새를 crossfade 로 지워 만든 루프는 그 패딩 때문에 한 바퀴마다 딸깍거린다 —
    /// 파형에서 지운 이음새가 인코딩에서 되살아난다.</item>
    /// <item>클립이 전부 1.6초 이하로 짧아서 압축 이득이 거의 없다. 43개를 다 합쳐
    /// 44.1kHz 모노 16비트로 약 2MB 다. 대신 디코딩 지연이 없어 탄착음이 판정과 같은
    /// 프레임에 난다 — 초당 수십 번 나는 소리에서는 그쪽이 훨씬 중요하다.</item>
    /// </list></para>
    /// </summary>
    public class SfxImportSettings : AssetPostprocessor
    {
        const string Marker = "/Resources/SFX/";

        void OnPreprocessAudio()
        {
            if (assetPath.Replace('\\', '/').IndexOf(Marker, System.StringComparison.Ordinal) < 0) return;

            var imp = (AudioImporter)assetImporter;

            // 전부 모노로 만들어 뒀지만, 나중에 스테레오 소스가 섞여 들어오면
            // 좌우가 파형에 박혀 ProtoAudio 의 pan 계산과 싸운다. 여기서 못 박는다.
            imp.forceToMono = true;
            imp.loadInBackground = false;   // 첫 재생이 늦으면 안 된다
            imp.ambisonic = false;

            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
        }
    }
}
