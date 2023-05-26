using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TpLab.HeavensGate.Udon;

namespace Kago171.SpatialMusic.Udon
{
    public class SpatialMusicPlayerWithIntroOutro : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("（SpatialMusicPlayerにイントロ・アウトロつき音源用の特殊処理を加えたもの）")]
        [Header("【Playerの位置に応じて再生状態や音量を変化させる】")]

        [SerializeField]
        [Tooltip("イントロ音源用ファイル(ogg等)")]
        AudioClip introMusicClip;

        [SerializeField]
        [Tooltip("ループ音源用ファイル(ogg等)（イントロ音源から接続再生、およびこの音源自体もループ再生する）")]
        AudioClip loopMusicClip;

        [SerializeField]
        [Tooltip("アウトロ音源用ファイル（ループ音源から接続再生する音源）")]
        AudioClip outroMusicClip;

        [SerializeField]
        [Tooltip("イントロ音源用AudioSource（指定のない場合ループ音源から再生開始する）")]
        AudioSource introAudioSource;

        [SerializeField]
        [Tooltip("ループ音源用AudioSource")]
        AudioSource loopAudioSource;

        [SerializeField]
        [Tooltip("アウトロ音源用AudioSource（指定のない場合ループ音源から遷移しない）")]
        AudioSource outroAudioSource;

        [SerializeField]
        [Tooltip("再生時の最大音量。これを基準にして距離による減衰を加える")]
        float maxVolume = 0.5f;

        [SerializeField]
        [Tooltip("音量が上がる際の1/60秒あたりの最大変化量（小さくするとふわっと音量変化する）")]
        float smoothVolumeOn = 0.02f;

        [SerializeField]
        [Tooltip("音量が下がる際の1/60秒あたりの最大変化量（小さくするとふわっと音量変化する）")]
        float smoothVolumeOff = 0.01f;

        [SerializeField]
        [Tooltip("alwaysOnPlayback = false の場合、開始のタイミングをテンポに合わせる(0→即時再生、70→BPM70の1拍分で同期する)")]
        float startPlaybackSyncBPM = 0f;

        [SerializeField]
        [Tooltip("この範囲内でのみ音量計算する。Is Trigger を有効化した Box Collider コンポーネントを持たせること")]
        SpatialMusicPlayerCollider safeArea;   // 負荷軽減のため、この範囲外では音量計算をしない(0固定)。 SafeArea として使う場合、 Is Trigger を有効化した Box Collider を追加しておくこと

        [SerializeField]
        [Tooltip("音源を鳴らす領域")]
        SpatialMusicPlayerCollider[] colliders;    // 音源を鳴らす領域。複数指定可。Colliderコンポーネントは持たせなくてもよい（SafeArea として使わない場合）
  
        [SerializeField]
        [Tooltip("colliders と重ねて、音源を止める（音量を下げる）領域")]
        SpatialMusicPlayerCollider[] negativeColliders;    // 音源を止める（音量を下げる）領域。複数指定可。Colliderコンポーネントは持たせなくてもよい（SafeArea として使わない場合）

        [SerializeField]
        [Tooltip("再生終了時、ここに設定した Collider をミュートする(volume=0)。複数選択可。（再生終了時に消したい音源の Collider や、再生終了後から流したい音源の negativeCollider をここに設定する）")]
        SpatialMusicPlayerCollider[] stopCollidersAfterOpening;

        [SerializeField]
        [Tooltip("再生終了時、stopCollidersAfterOpening の Volume を 0 にするまでのフェードアウト時間（秒）")]
        float outroFadeLengthSec = 1f;

        [SerializeField]
        [Tooltip("この領域に入ったとき、もしくは出たときにループ音源からアウトロ音源への遷移を開始する。設定がない場合遷移しない")]
        SpatialMusicPlayerCollider endLoopArea;

        [SerializeField]
        [Tooltip("true: endLoopArea に入った時, false: endLoopArea を出た時 にループ音源からアウトロ音源への遷移を開始する")]
        bool endLoopWhenEnterEndLoopArea = true;

        [SerializeField]
        [Tooltip("この領域に入ったとき、もしくは出たときに音源再生状況をリセットする")]
        SpatialMusicPlayerCollider resetArea;

        [SerializeField]
        [Tooltip("true: resetArea に入った時, false: resetArea を出た時 に自動的に音源再生状況をリセットする")]
        bool resetWhenEnterResetArea = true;

        [SerializeField]
        [Tooltip("使用する SpatialMusicPlayerManager")]
        SpatialMusicPlayerManager manager; // 使用する SpatialMusicPlayerManager（基本的に、シーン内に配置する SpatialMusicPlayerManager は1つだけにする）

        float mainLastVolume = 0f;

        bool started = false;  // イントロ音源もしくはループ音源が再生開始されたフラグ（リセット時のみクリア）
        bool loopStarted = false;  // （イントロ音源から遷移して）ループ音源の再生を開始したフラグ（リセット時のみクリア）
        bool outroScheduled = false;  // ループ音源→アウトロ音源への切替を予約したフラグ（リセットもしくは予約をキャンセルした場合にクリア）
        Vector3 playerpos_v = Vector3.zero;
        Vector3 playerpos_r = Vector3.zero;
        float[] stopCollidersAfterOpeningVolumes;    // アウトロ音源の終了時、stopCollidersAfterOpeningのVolumeをフェードアウトするので、元の値を記録しておく

        // 音源が再度流れる状態にリセットする
        public void ResetSound() {
            //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro.ResetSound()");
            if (introAudioSource != null) {
                introAudioSource.volume = 0f;
                introAudioSource.time = 0f;
                introAudioSource.loop = false;
                introAudioSource.Stop();                
            }
            if (loopAudioSource != null) {
                loopAudioSource.volume = 0f;
                loopAudioSource.time = 0f;
                loopAudioSource.loop = true;
                loopAudioSource.Stop();                
            }
            if (outroAudioSource != null) {
                outroAudioSource.volume = 0f;
                outroAudioSource.time = 0f;
                outroAudioSource.loop = false;
                outroAudioSource.Stop();                
            }

            // 各音源の再生・遷移フラグを戻す
            started = false;
            loopStarted = false;
            outroScheduled = false;

            // stopCollidersAfterOpeningのvolumeを初期値に戻す
            for (int i = 0; i < stopCollidersAfterOpening.Length; i++) {
                stopCollidersAfterOpening[i].volume = this.stopCollidersAfterOpeningVolumes[i];
            }
        }

        void Start()
        {
            // イントロ音源の初期設定（ループなし）
            if (introAudioSource != null) {
                introAudioSource.volume = 0f;
                introAudioSource.clip = introMusicClip;
                introAudioSource.loop = false;
                introAudioSource.playOnAwake = false;
            }

            // ループ音源の初期設定（ループあり）
            if (loopAudioSource != null) {
                loopAudioSource.volume = 0f;
                loopAudioSource.clip = loopMusicClip;
                loopAudioSource.loop = true;
                loopAudioSource.playOnAwake = false;
            }

            // アウトロ音源の初期設定（ループなし）
            if (outroAudioSource != null) {
                outroAudioSource.volume = 0f;
                outroAudioSource.clip = outroMusicClip;
                outroAudioSource.loop = false;
                outroAudioSource.playOnAwake = false;
            }

            // アウトロ音源の終了時、stopCollidersAfterOpeningのVolumeをフェードアウトするので、Volumeの初期値を記録しておく
            this.stopCollidersAfterOpeningVolumes = new float[stopCollidersAfterOpening.Length];
            for (int i = 0; i < stopCollidersAfterOpening.Length; i++) {
                this.stopCollidersAfterOpeningVolumes[i] = stopCollidersAfterOpening[i].volume;
            }
        }

        void Update()
        {
            // マスター音量を取得する。マスター音量は全ての音量に優先し、フェードの影響を受けないため、ミュート時は瞬時に無音になる。
            float masterVolume = 1f;
            if (manager) {
                masterVolume = (manager.GetMasterMute()) ? 0f : manager.GetMasterVolume();
            }
            // マスター音量が0（ミュート状態も含む）の場合、必ず音量は0なので、一切音量計算をせずに終了する。
            if (Mathf.Approximately(masterVolume, 0)) {
                if (introAudioSource != null) { introAudioSource.volume = 0f; }
                if (loopAudioSource != null) { loopAudioSource.volume = 0f; }
                if (outroAudioSource != null) { outroAudioSource.volume = 0f; }
                return;
            }

            // playerの位置を取得（位置は頭の位置を使用）
            playerpos_r = manager.GetPlayerPositionR();
            playerpos_v = manager.GetPlayerPositionV();


            float distanceVolume = GetDistanceVolume(colliders, negativeColliders);
            float distanceVolumeNonFade_ForLog = distanceVolume;
            distanceVolume = GetFadedDistanceVolume(distanceVolume, mainLastVolume, smoothVolumeOn, smoothVolumeOff);

            // ループ音源が再生中の場合、（イントロ音源から遷移して）ループ音源の再生を開始したフラグを立てる
            if (!loopStarted && loopAudioSource.isPlaying) {
                //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: intro->loop");
                loopStarted = true;
            }

            if (introAudioSource != null && !loopStarted) {
                // イントロ音源が設定されている場合、音量最大になったらイントロ音源再生開始かつ続けてループ音源の再生を予約する。音量ゼロになったら再生停止
                if (!introAudioSource.isPlaying && (distanceVolume >= 1f)) {
                        if (startPlaybackSyncBPM == 0f || (manager.GetElapsedTime() % (60 / startPlaybackSyncBPM) < 0.03f)) {    // startPlaybackSyncBPMを設定している場合、タイミングを合わせて開始
                            //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: introStarted");
                            introAudioSource.Play();
                            loopAudioSource.PlayScheduled(AudioSettings.dspTime + (introMusicClip.length - introAudioSource.time));
                            started = true;
                        }
                }
                else if (distanceVolume <= 0f) {
                    if (introAudioSource.isPlaying) {
                        //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: introStopped");
                        introAudioSource.Stop();
                    }
                }
            }
            else {
                // イントロ音源が設定されていない、またはイントロ音源からループ音源に遷移してから停止した場合
                if (!loopAudioSource.isPlaying && (distanceVolume > 0f) && !outroScheduled) {    // ループ音源はアウトロ音源に遷移していないことも条件
                    // 音量ゼロでなくなったら現在時刻にもとづいた位置からループ音源を再生開始
                    //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: loopStarted");
                    loopAudioSource.Play();
                    loopAudioSource.time = manager.GetElapsedTime() % loopAudioSource.clip.length; // 先頭から再生しない場合、現在の時間に応じたタイミングにシークする
                    loopAudioSource.loop = true;
                    started = true;
                }
                else if (loopAudioSource.isPlaying && (distanceVolume <= 0f)) {
                    // ループ音源再生中、音量ゼロになったらループ音源再生停止、アウトロ音源への遷移が予約されていたらそれもキャンセル
                    //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: loopStopped");
                    loopAudioSource.Stop();
                    outroAudioSource.Stop();
                    outroScheduled = false;
                }
                else if (outroAudioSource.isPlaying && (distanceVolume <= 0f)) {
                    // アウトロ音源再生中、音量ゼロになったらアウトロ音源再生停止
                    //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: outroStopped");
                    outroAudioSource.Stop();
                    outroScheduled = false;
                }
            }

            // アウトロ音源が設定されている場合のみ、以下3つの処理を行う
            if (outroAudioSource != null) {

                // ループ音源からアウトロ音源への遷移を予約
                // （endLoopWhenEnterEndLoopArea==true の場合は endLoopArea の中、false の場合は外にいて、かつ ループ音源が再生中の時のみ）
                if (!outroScheduled && loopAudioSource.isPlaying && distanceVolume >= 1f && endLoopArea != null) {
                    if ((endLoopWhenEnterEndLoopArea == IsInSafeArea((endLoopArea.useVirtualPosition ? playerpos_v : playerpos_r), endLoopArea))) {
                        //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: loop->outro");
                        // ループ音源 のループ解除
                        loopAudioSource.loop = false;
                        // ループ音源 再生終了後に アウトロ音源 再生開始を予約
                        outroAudioSource.PlayScheduled(AudioSettings.dspTime + (loopMusicClip.length - loopAudioSource.time));
                        outroScheduled = true;
                    }
                }

                // オープニング音源再生状況をリセット
                // （resetWhenEnterResetArea==true の場合は ResetArea の中、false の場合は外にいて、かつ ループ音源、アウトロ音源とも再生が停止している時のみ）
                if (started && !loopAudioSource.isPlaying && !outroAudioSource.isPlaying && resetArea != null) {
                    if ((resetWhenEnterResetArea == IsInSafeArea((resetArea.useVirtualPosition ? playerpos_v : playerpos_r), resetArea))) {
                        //Debug.Log("[Kago] SpatialMusicPlayerWithIntroOutro: call ResetSound()");
                        ResetSound();
                    }
                }

                // アウトロ音源の再生が終わり際(outroFadeLengthSec 以内)であれば、stopCollidersAfterOpeningのvolumeを減らしていく
                if (outroAudioSource.isPlaying) {
                    float outroLastSec = outroMusicClip.length - outroAudioSource.time;
                    if (outroLastSec < outroFadeLengthSec) {
                        for (int i = 0; i < stopCollidersAfterOpening.Length; i++) {
                            if (stopCollidersAfterOpening[i]) {
                                stopCollidersAfterOpening[i].volume = this.stopCollidersAfterOpeningVolumes[i] * outroLastSec / outroFadeLengthSec;
                            }
                        }
                    }
                }
            }

            mainLastVolume = distanceVolume;

            // SpatialMusicPlayerManager にログを追加（SpatialMusicPlayerManager側で全音源のデータをまとめて出力させる）
            if (manager && manager.GetEnableDebugLog()) {
                if (distanceVolume > 0f) {
                    AddDebugLog(name + "_I", distanceVolume, distanceVolumeNonFade_ForLog, introAudioSource);
                    AddDebugLog(name + "_L", distanceVolume, distanceVolumeNonFade_ForLog, loopAudioSource);
                    AddDebugLog(name + "_O", distanceVolume, distanceVolumeNonFade_ForLog, outroAudioSource);
                }
            }

            // 各AudioSourceから実際に出る音量の設定
            introAudioSource.volume = masterVolume * (distanceVolume) * maxVolume;
            if (introAudioSource.volume > 1f) {
                introAudioSource.volume = 1f;
            }
            loopAudioSource.volume = introAudioSource.volume;
            outroAudioSource.volume = introAudioSource.volume;
        }

        // player位置とcolliderをもとに、distanceVolumeを計算する。
        float GetDistanceVolume(SpatialMusicPlayerCollider[] colliders, SpatialMusicPlayerCollider[] negativeColliders) {
            float distanceVolume = 0f;
            if (!safeArea || IsInSafeArea((safeArea.useVirtualPosition ? playerpos_v : playerpos_r), safeArea)) {
                // SafeAreaの設定がない、もしくはSafeArea内に居る場合は音量計算をする。
                // フェード処理を行うため、SafeAreaを出入りした場合も音量はふわっと変化する。

                // 各 collider に対する音量を計算し、最大のものを使用する。
                foreach (SpatialMusicPlayerCollider c in colliders)
                {
                    if (c && c.volume > 0f) { // Colliders に中身が入っていない、もしくは音量0の場合は無視
                        float distanceVolumeEach = c.volume * (c.useVirtualPosition ? GetVolume(playerpos_v, c) : GetVolume(playerpos_r, c));
                        if (distanceVolume < distanceVolumeEach) {
                            distanceVolume = distanceVolumeEach;
                        }
                    }
                }

                // 各 negativeCollider に対する音量を計算し、最大のものを使用する。
                float negativeDistanceVolume = 0f;
                foreach (SpatialMusicPlayerCollider c in negativeColliders)
                {
                    if (c && c.volume > 0f) { // Colliders に中身が入っていない、もしくは音量0の場合は無視
                        float distanceVolumeEach = c.volume * (c.useVirtualPosition ? GetVolume(playerpos_v, c) : GetVolume(playerpos_r, c));
                        if (negativeDistanceVolume < distanceVolumeEach) {
                            negativeDistanceVolume = distanceVolumeEach;
                        }
                    }
                }

                // collider に基づく音量から、negativeCollider に基づく音量を減算する。
                distanceVolume -= negativeDistanceVolume;
                if (distanceVolume < 0f) {
                    distanceVolume = 0f;
                }
            }
            return distanceVolume;
        }

        float GetFadedDistanceVolume(float distanceVolume, float lastVolume, float smoothVolumeOn, float smoothVolumeOff) {
            // フェード処理（前回からの Volumeの変化量が smoothVolumeOn/Off より大きければ、変化量を smoothVolumeOn/Off に抑える）
            float deltaTimex60 = Time.deltaTime * 60;   // Time.deltaTime: 1つ前のフレームからの経過時間(sec)
            if (distanceVolume - lastVolume > smoothVolumeOn * deltaTimex60) {
                distanceVolume = lastVolume + smoothVolumeOn * deltaTimex60;   // 60FPS を基準として、実際の処理速度に応じた変化量に調整
            }
            else if (distanceVolume - lastVolume < -smoothVolumeOff * deltaTimex60) {
                distanceVolume = lastVolume - smoothVolumeOff * deltaTimex60;   // 60FPS を基準として、実際の処理速度に応じた変化量に調整
            }
            return distanceVolume;
        }

        // SpatialMusicPlayerManager にログを追加（SpatialMusicPlayerManager側で全音源のデータをまとめて出力させる）
        void AddDebugLog(string name, float distanceVolume, float distanceVolumeNonFade_ForLog, AudioSource audioSource) {
            string log = "";
            log += $"[{name}] {distanceVolume:f2}";
            if (!Mathf.Approximately(distanceVolume, distanceVolumeNonFade_ForLog)) {
                log += $"->{distanceVolumeNonFade_ForLog:f2}";
            }
            if (!audioSource.isPlaying) {
                log += $"[STOP]";
            }
            log += $" {audioSource.time:f3}/{audioSource.clip.length:f3}({audioSource.timeSamples}/{audioSource.clip.samples})";
            manager.AddDebugLog(log);
        }

        // 音源のSafeArea内にいるか？（いない場合、音量を計算せず、0として扱う）
        // SafeAreaの判定のみ、高さの座標を使用する
        // ColliderをTriggerとして扱いOnPlayerTriggerStayとか使えばいい気もするが、ここだけ処理を変えるのも変な感じがするので…
        bool IsInSafeArea(Vector3 playerpos, SpatialMusicPlayerCollider area) {
            return area.inTrigger;  // SafeAreaの当たり判定にコライダーのTriggerを使う（音量計算がないので、当たっているかどうかだけ調べられればよい）
        }

        // プレイヤー位置とコライダー位置をもとに音量を取得
        float GetVolume(Vector3 playerpos, SpatialMusicPlayerCollider collider)
        {
            float distanceVolume = 0f;
            if (collider.isRectangle) { // 矩形
                if (collider.transform.rotation.y != 0) { // 傾いた矩形

                    // 矩形の頂点座標を求める
                    // まず、Positionが原点, Rotationが0だった場合の各頂点の座標を求める（scale.x, scale.zの半分を絶対値として、それぞれのプラスマイナスで4頂点。yは0固定）
                    // それぞれに対して、rotationを掛ける（roration分回転させる）。rotationはもともとQuaternionなので変換不要で、そのまま掛けるだけ。
                    // ※rotationはinspector上でy軸のみに値が入っている想定（上から見た回転のみを考える）だが、たぶんx,z軸のrotationも入れれば反映されそうな気がする（→ 平行四辺形の領域とかも作れる？）
                    // 最後にワールド上のPositionを足して、実際のワールド内の座標にする(実際に使うのはx,z軸で、y軸の値は無視する)
                    Vector3[] vertex = new Vector3[4];
                    vertex[0] = collider.transform.rotation * new Vector3((collider.transform.lossyScale.x)/2, 0, (collider.transform.lossyScale.z)/2) + collider.transform.position;
                    vertex[1] = collider.transform.rotation * new Vector3((collider.transform.lossyScale.x)/2, 0, -(collider.transform.lossyScale.z)/2) + collider.transform.position;
                    vertex[2] = collider.transform.rotation * new Vector3(-(collider.transform.lossyScale.x)/2, 0, -(collider.transform.lossyScale.z)/2) + collider.transform.position;
                    vertex[3] = collider.transform.rotation * new Vector3(-(collider.transform.lossyScale.x)/2, 0, (collider.transform.lossyScale.z)/2) + collider.transform.position;

                    // 四角形の中心点とプレイヤー位置を結んだ直線が、四角形の各辺のいずれかと交差するか？
                    // どれかと交差するなら領域外 → 音量0
                    // どれとも交差しないなら領域内 → 中心点と交点の距離、および中心点とプレイヤーの距離から音量を求める
                    bool isOutside = false;
                    for (int i=0; i<4; i++) {
                        isOutside = IsIntersected(playerpos, collider.transform.position, vertex[i], vertex[(i<3 ? i+1 : 0)]);
                        if (isOutside) {
                            break;
                        }
                    } 

                    // collider領域の内側に居る場合は音量を計算する
                    if (!isOutside) {
                        if (collider.fallOffDistance == 0) {
                            // コライダーの減衰距離が0の場合、音量は最大
                            distanceVolume = 1f;
                        }
                        else {
                            float distancePlayerAndColliderSurface = 10000f; // 充分大きな値
                            float distancePlayerAndColliderCore = Vector2.Distance(new Vector2(playerpos.x, playerpos.z), new Vector2(collider.transform.position.x, collider.transform.position.z));
                            Vector3[] intersection = new Vector3[4];
                            for (int i=0; i<4; i++) {
                                intersection[i] = GetIntersection(playerpos, collider.transform.position, vertex[i], vertex[(i<3 ? i+1 : 0)]);
                                float distanceEach = Vector2.Distance(new Vector2(playerpos.x, playerpos.z), new Vector2(intersection[i].x, intersection[i].z));
                                if (distancePlayerAndColliderSurface > distanceEach) {
                                    distancePlayerAndColliderSurface = distanceEach;
                                }
                            } 

                            if (distancePlayerAndColliderSurface >= collider.fallOffDistance) {
                                // コライダー表面からの距離が減衰開始距離以上の場合は最大音量
                                distanceVolume = 1f;
                            }
                            else {
                                // 直線的に減衰する
                                distanceVolume = distancePlayerAndColliderSurface / collider.fallOffDistance;
                            }
                        }
                    }
                    else {
                        // collider領域の外に居る場合は無音
                        distanceVolume = 0f;
                    }
                }
                else {  // 傾いていない矩形(collider.transform.rotation.y == 0)

                    // collider領域の内側にいるか？ x/z軸でのコライダー表面からの距離を計算する（負の値であればコライダーの外側）
                    float distancePlayerAndColliderSurfaceX = (collider.transform.lossyScale.x / 2) - Mathf.Abs(playerpos.x - collider.transform.position.x);
                    float distancePlayerAndColliderSurfaceZ = (collider.transform.lossyScale.z / 2) - Mathf.Abs(playerpos.z - collider.transform.position.z);
                    if (distancePlayerAndColliderSurfaceX > 0 && distancePlayerAndColliderSurfaceZ > 0) {
                        // collider領域の内側にいる場合、x/z軸でのコライダー表面からの距離のうち、小さいほうをコライダー表面からの距離とする
                        float distancePlayerAndColliderSurface = Mathf.Min(distancePlayerAndColliderSurfaceX, distancePlayerAndColliderSurfaceZ);
                        if (distancePlayerAndColliderSurface >= collider.fallOffDistance) {
                            // コライダー表面からの距離が減衰開始距離以上の場合は最大音量
                            distanceVolume = 1f;
                        }
                        else {
                            // 直線的に減衰する
                            distanceVolume = distancePlayerAndColliderSurface / collider.fallOffDistance;
                        }
                    }
                    else {
                        // collider領域の外に居る場合は無音
                        distanceVolume = 0f;
                    }
                }
            }
            else {  // 円形
                // y軸は無視して、x/z軸のみの距離を見る
                float distancePlayerAndColliderCore = Vector2.Distance(new Vector2(playerpos.x, playerpos.z), new Vector2(collider.transform.position.x, collider.transform.position.z));
                float fallOffEndDistance = collider.transform.lossyScale.x / 2;
                if (distancePlayerAndColliderCore <= (fallOffEndDistance - collider.fallOffDistance)) {
                    // 減衰開始距離以下の場合は最大音量
                    distanceVolume = 1f;
                }
                else if (distancePlayerAndColliderCore >= fallOffEndDistance) {
                    // 減衰終了距離以上の場合は無音
                    distanceVolume = 0f;
                }
                else {
                    // 直線的に減衰する
                    distanceVolume = (fallOffEndDistance - distancePlayerAndColliderCore) / collider.fallOffDistance;
                }
            }
            return distanceVolume;
        }

        // ある2地点を結ぶ直線と、別の2地点を結ぶ直線が交わるか
        bool IsIntersected(Vector3 pA1, Vector3 pA2, Vector3 pB1, Vector3 pB2)
        {
            float t1 = (pA2.x - pA1.x) * (pB1.z - pA1.z) - (pA2.z - pA1.z) * (pB1.x - pA1.x);
            float t2 = (pA2.x - pA1.x) * (pB2.z - pA1.z) - (pA2.z - pA1.z) * (pB2.x - pA1.x);
            float t3 = (pB2.x - pB1.x) * (pA1.z - pB1.z) - (pB2.z - pB1.z) * (pA1.x - pB1.x);
            float t4 = (pB2.x - pB1.x) * (pA2.z - pB1.z) - (pB2.z - pB1.z) * (pA2.x - pB1.x);
            return ((t1 * t2 < 0f) && (t3 * t4 < 0f));
        }

        // ある2地点を結ぶ直線と、別の2地点を結ぶ直線の交点を返す
        Vector3 GetIntersection(Vector3 pA1, Vector3 pA2, Vector3 pB1, Vector3 pB2)
        {
            float det = (pA1.x - pA2.x) * (pB2.z - pB1.z) - (pB2.x - pB1.x) * (pA1.z - pA2.z);
            float t = ((pB2.z - pB1.z) * (pB2.x - pA2.x) + (pB1.x - pB2.x) * (pB2.z - pA2.z)) / det;
            float x = t * pA1.x + (1f - t) * pA2.x;
            float z = t * pA1.z + (1f - t) * pA2.z;
            return new Vector3(x, 0, z);
        }
    }
}
