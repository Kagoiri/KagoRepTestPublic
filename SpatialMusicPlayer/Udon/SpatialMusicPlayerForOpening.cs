using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
//using TpLab.HeavensGate.Udon;

namespace Kago171.SpatialMusic.Udon
{
    public class SpatialMusicPlayerForOpening : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("（SpatialMusicPlayerにオープニング音源用の特殊処理を加えたもの）")]
        [Header("【Playerの位置に応じて再生状態や音量を変化させる】")]

        [SerializeField]
        [Tooltip("オープニング楽曲のループ音源用ファイル(ogg等)")]
        AudioClip loopMusicClip;

        [SerializeField]
        [Tooltip("オープニング楽曲のコーダ音源用ファイル（ループ音源から接続再生する音源）")]
        AudioClip codaMusicClip;

        [SerializeField]
        [Tooltip("ループ音源用AudioSource)")]
        AudioSource loopAudioSource;

        [SerializeField]
        [Tooltip("コーダ音源用AudioSource)")]
        AudioSource codaAudioSource;

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
        [Tooltip("この範囲内でのみ音量計算する。Is Trigger を有効化した Box Collider コンポーネントを持たせること")]
        SpatialMusicPlayerCollider safeArea;   // 負荷軽減のため、この範囲外では音量計算をしない(0固定)。 SafeArea として使う場合、 Is Trigger を有効化した Box Collider を追加しておくこと

        [SerializeField]
        [Tooltip("音源を鳴らす領域")]
        SpatialMusicPlayerCollider[] colliders;    // 音源を鳴らす領域。複数指定可。Colliderコンポーネントは持たせなくてもよい（SafeArea として使わない場合）
  
        [SerializeField]
        [Tooltip("colliders と重ねて、音源を止める（音量を下げる）領域")]
        SpatialMusicPlayerCollider[] negativeColliders;    // 音源を止める（音量を下げる）領域。複数指定可。Colliderコンポーネントは持たせなくてもよい（SafeArea として使わない場合）

        [SerializeField]
        [Tooltip("オープニング終了時、ここに設定した Collider をミュートする(volume=0)。複数選択可。（オープニング終了時に消したい音源の Collider や、オープニング終了後から流したい音源の negativeCollider をここに設定する）")]
        SpatialMusicPlayerCollider[] stopCollidersAfterOpening;

        [SerializeField]
        [Tooltip("オープニング終了時、stopCollidersAfterOpening の Volume を 0 にするまでのフェードアウト時間（秒）")]
        float codaFadeLengthSec = 1f;

        [SerializeField]
        [Tooltip("この領域に入ったとき、もしくは出たときにオープニング音源を再生開始する(colliders のミュートを解除する)。設定がない場合、初期状態で再生開始する")]
        SpatialMusicPlayerCollider startArea;

        [SerializeField]
        [Tooltip("true: StartAreaに入った時, false: StartAreaを出た時 にオープニング音源を再生開始する")]
        bool startWhenEnterStartArea = true;

        [SerializeField]
        [Tooltip("この領域に入ったとき、もしくは出たときにオープニング音源の再生状況をリセットする")]
        SpatialMusicPlayerCollider resetArea;

        [SerializeField]
        [Tooltip("true: ResetAreaに入った時, false: ResetAreaを出た時 に自動的にオープニング音源再生状況をリセットする")]
        bool resetWhenEnterResetArea = true;

        [SerializeField]
        [Tooltip("使用する SpatialMusicPlayerManager")]
        SpatialMusicPlayerManager manager; // 使用する SpatialMusicPlayerManager（基本的に、シーン内に配置する SpatialMusicPlayerManager は1つだけにする）

        float mainLastVolume = 0f;

        bool started = false;  // startAreaに入ったor出たことにより、オープニング音源再生が開始された
        bool mainCodaScheduled = false;  // mainLoop→mainCodaへの切替を行った
        Vector3 playerpos_v = Vector3.zero;
        Vector3 playerpos_r = Vector3.zero;
        float[] stopCollidersVolumes;    // 開始時、collidersのVolumeを0にするので、元の値を記録しておく
        float[] stopCollidersAfterOpeningVolumes;    // mainCodaの終了時、stopCollidersAfterOpeningのVolumeをフェードアウトするので、元の値を記録しておく

        // オープニング音源(mainLoop/mainCoda)が再度流れる状態にリセットする
        public void ResetOpeningSound() {
            //Debug.Log("[Kago] SpatialMusicPlayerForOpening.ResetOpeningSound()");
            loopAudioSource.volume = 0f;
            codaAudioSource.volume = 0f;

            loopAudioSource.time = 0f;
            codaAudioSource.time = 0f;

            loopAudioSource.loop = true;
            codaAudioSource.loop = false;

            loopAudioSource.Stop();
            codaAudioSource.Stop();

            // mainCodaを再生スケジュールしていない状態に戻す
            mainCodaScheduled = false;

            // stopCollidersAfterOpeningのvolumeを初期値に戻す
            for (int i = 0; i < stopCollidersAfterOpening.Length; i++) {
                stopCollidersAfterOpening[i].volume = this.stopCollidersAfterOpeningVolumes[i];
            }

            // 再生開始前状態に戻して、colliders を再度ミュート(volume=0)
            if (startArea != null && colliders != null) {
                started = false;
                for (int i = 0; i < colliders.Length; i++) {
                    colliders[i].volume = 0f;
                }
            }
        }

        void Start()
        {
            // mainLoop音源の初期設定（ループあり）
            if (loopAudioSource != null) {
                loopAudioSource.volume = 0f;
                loopAudioSource.clip = loopMusicClip;
                loopAudioSource.loop = true;
                loopAudioSource.playOnAwake = false;
            }

            // mainCoda音源の初期設定（ループなし）
            if (codaAudioSource != null) {
                codaAudioSource.volume = 0f;
                codaAudioSource.clip = codaMusicClip;
                codaAudioSource.loop = false;
                codaAudioSource.playOnAwake = false;
            }

            // mainCodaの終了時、stopCollidersAfterOpeningのVolumeをフェードアウトするので、Volumeの初期値を記録しておく
            this.stopCollidersAfterOpeningVolumes = new float[stopCollidersAfterOpening.Length];
            for (int i = 0; i < stopCollidersAfterOpening.Length; i++) {
                this.stopCollidersAfterOpeningVolumes[i] = stopCollidersAfterOpening[i].volume;
            }

            // startArea の設定がある場合は、オープニング再生開始を待つため colliders をミュート(volume=0)、元に戻す際のためにVolumeの初期値を記録しておく
            // startArea の設定がない場合は、何もしない（初期状態でオープニング再生が開始しているものとする）
            if (startArea == null) {
                started = true;
            }
            else if (colliders != null) {
                this.stopCollidersVolumes = new float[colliders.Length];
                for (int i = 0; i < stopCollidersVolumes.Length; i++) {
                    this.stopCollidersVolumes[i] = colliders[i].volume;
                    colliders[i].volume = 0f;
                }
            }
        }

        void Update()
        {
            // SpatialMusicPlayerManager と AudioSource は（現状では）必須
            if (manager == null || loopAudioSource == null || codaAudioSource == null) {
                Debug.LogError($"[{name}] <SpatialMusicPlayerForOpening> : Manager or any Audio Source is not found!");
                return;
            }

            // マスター音量を取得する。マスター音量は全ての音量に優先し、フェードの影響を受けないため、ミュート時は瞬時に無音になる。
            // ミュート解除時に瞬時に音量を戻すため、ミュート中でも音量計算を行う。
            float masterVolume = 1f;
            if (manager) {
                masterVolume = (manager.GetMasterMute()) ? 0f : manager.GetMasterVolume();
            }
            // マスター音量が0（ミュート状態も含む）の場合、必ず音量は0なので、一切音量計算をせずに終了する。
            if (Mathf.Approximately(masterVolume, 0)) {
                loopAudioSource.volume = 0f;
                codaAudioSource.volume = 0f;
                return;
            }

            // playerの位置を取得（位置は頭の位置を使用）
            playerpos_r = manager.GetPlayerPositionR();
            playerpos_v = manager.GetPlayerPositionV();

            // startWhenEnterStartArea==true の場合は startArea の中、false の場合は外にいる時、 colliders のミュートを解除する
            if (!started && startArea && !mainCodaScheduled) {
                if ((startWhenEnterStartArea == IsInSafeArea((startArea.useVirtualPosition ? playerpos_v : playerpos_r), startArea))) {
                    //Debug.Log("[Kago] SpatialMusicPlayerForOpening: started = true");
                    for (int i = 0; i < colliders.Length; i++) {
                        colliders[i].volume = this.stopCollidersVolumes[i];
                    }
                    started = true;
                }
            }

            // resetWhenEnterResetArea==true の場合は ResetArea の中、false の場合は外にいて、かつ mainLoop、mainCodaとも再生が停止している時、オープニング音源再生状況をリセットする
            if (mainCodaScheduled && !loopAudioSource.isPlaying && !codaAudioSource.isPlaying && resetArea) {
                if ((resetWhenEnterResetArea == IsInSafeArea((resetArea.useVirtualPosition ? playerpos_v : playerpos_r), resetArea))) {
                    //Debug.Log("[Kago] SpatialMusicPlayerForOpening: call ResetOpeningSound()");
                    ResetOpeningSound();
                }
            }

            // mainCodaの再生が終わり際(codaFadeLengthSec 以内)であれば、stopCollidersAfterOpeningのvolumeを減らしていく
            if (codaAudioSource.isPlaying) {
                float mainCodaLastSec = codaMusicClip.length - codaAudioSource.time;
                if (mainCodaLastSec < codaFadeLengthSec) {
                    for (int i = 0; i < stopCollidersAfterOpening.Length; i++) {
                        if (stopCollidersAfterOpening[i]) {
                            stopCollidersAfterOpening[i].volume = this.stopCollidersAfterOpeningVolumes[i] * mainCodaLastSec / codaFadeLengthSec;
                        }
                    }
                }
            }

            float mainDistanceVolume = GetDistanceVolume(colliders, negativeColliders);
            float mainDistanceVolumeNonFade_ForLog = mainDistanceVolume;
            mainDistanceVolume = GetFadedDistanceVolume(mainDistanceVolume, mainLastVolume, smoothVolumeOn, smoothVolumeOff);


            // mainLoopは音量ゼロでなくなったら現在時刻にもとづいた位置から再生開始、音量ゼロになったら再生停止
            if (!loopAudioSource.isPlaying && (mainDistanceVolume > 0f) && !mainCodaScheduled) {    // mainLoopはmainCodaに遷移していないことも条件
                loopAudioSource.Play();
                loopAudioSource.time = manager.GetElapsedTime() % loopAudioSource.clip.length; // 先頭から再生しない場合、現在の時間に応じたタイミングにシークする
            }
            else if (loopAudioSource.isPlaying && (mainDistanceVolume <= 0f)) {
                loopAudioSource.Stop();
            }

            // main音源の音量が最大になったらmainLoop→mainCodaへの切替処理
            if (!mainCodaScheduled && !codaAudioSource.isPlaying && (mainDistanceVolume >= 1f)) {
                // mainLoop のループ解除
                loopAudioSource.loop = false;
                // mainLoop 再生終了後に mainCoda 再生開始をスケジュール
                codaAudioSource.PlayScheduled(AudioSettings.dspTime + (loopMusicClip.length - loopAudioSource.time));
                mainCodaScheduled = true;
            }

            mainLastVolume = mainDistanceVolume;

            // SpatialMusicPlayerManager にログを追加（SpatialMusicPlayerManager側で全音源のデータをまとめて出力させる）
            if (manager && manager.GetEnableDebugLog()) {
                if (mainDistanceVolume > 0f) {
                    AddDebugLog(name + "_ML", mainDistanceVolume, mainDistanceVolumeNonFade_ForLog, loopAudioSource);
                    AddDebugLog(name + "_MC", mainDistanceVolume, mainDistanceVolumeNonFade_ForLog, codaAudioSource);
                }
            }

            // 各AudioSourceから実際に出る音量の設定
            loopAudioSource.volume = masterVolume * (mainDistanceVolume) * maxVolume;
            if (loopAudioSource.volume > 1f) {
                loopAudioSource.volume = 1f;
            }
            codaAudioSource.volume = loopAudioSource.volume;
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
