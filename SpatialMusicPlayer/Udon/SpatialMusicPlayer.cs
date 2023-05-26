using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TpLab.HeavensGate.Udon;

namespace Kago171.SpatialMusic.Udon
{
    public class SpatialMusicPlayer : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("【Playerの位置に応じて再生状態や音量を変化させる】")]

        [SerializeField]
        [Tooltip("再生する音源ファイル(ogg等)")]
        AudioClip musicClip;

        [SerializeField]
        [Tooltip("再生に使うAudioSource")]
        AudioSource audioSource;

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
        [Tooltip("この範囲内でのみ音量計算する（Y軸を考慮する）。Is Trigger を有効化した Box Collider コンポーネントを持たせること")]
        SpatialMusicPlayerCollider safeArea;   // 負荷軽減のため、この範囲外では音量計算をしない(0固定)。高さを考慮するので上下で別のPlayerを配置できる。SpatialMusicPlayerCollider を SafeArea として使う場合、 Is Trigger を有効化した Box Collider を追加しておくこと

        [SerializeField]
        [Tooltip("音源を鳴らす領域（Y軸は考慮しない）")]
        SpatialMusicPlayerCollider[] colliders;    // 音源を鳴らす領域。複数指定可。高さを考慮しないので、領域は上から見て平面的に配置する。上下で音源を変えたい場合SafeAreaを使う。Colliderコンポーネントは持たせなくてもよい（SafeArea として使わない場合）

        [SerializeField]
        [Tooltip("colliders と重ねて、音源を止める（音量を下げる）領域")]
        SpatialMusicPlayerCollider[] negativeColliders;    // 音源を止める（音量を下げる）領域。使い方は colliders と同じ

        [SerializeField]
        [Tooltip("true:常時再生, false:近づいたら再生開始")]
        bool alwaysOnPlayback = true;

        [SerializeField]
        [Tooltip("alwaysOnPlayback = true の場合、開始のタイミングをテンポに合わせる(0→即時再生、70→BPM70の1拍分で同期する)")]
        float startPlaybackSyncBPM = 0f;

        [SerializeField]
        [Tooltip("音源のループ再生 true:する, false:しない")]
        bool loopPlay = true;

        [SerializeField]
        [Tooltip("使用する SpatialMusicPlayerManager")]
        SpatialMusicPlayerManager manager; // 使用する SpatialMusicPlayerManager（基本的に、シーン内に配置する SpatialMusicPlayerManager は1つだけにする）

        [SerializeField]
        [Tooltip("デバッグ用：音が出ていなくてもログ出力する")]
        bool forceLog = false;

        float lastVolume = 0f;
        bool started = false;   // 近づいたら再生開始する音源で、再生を開始したかどうか
        Vector3 playerpos_v = Vector3.zero;
        Vector3 playerpos_r = Vector3.zero;

        void Start()
        {
            audioSource.volume = 0f;
            audioSource.clip = musicClip;
            audioSource.loop = loopPlay;
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        void Update()
        {
            // SpatialMusicPlayerManager からマスター音量を取得する。マスター音量は全ての音量に優先し、フェードの影響を受けないため、ミュート時は即座に無音になる。
            float masterVolume = 1f;
            if (manager) {
                masterVolume = (manager.GetMasterMute()) ? 0f : manager.GetMasterVolume();
            }
            // マスター音量が0（ミュート状態も含む）の場合、出力音量を0として、一切音量計算をせずに終了する。
            if (Mathf.Approximately(masterVolume, 0)) {
                audioSource.volume = 0;
                return;
            }

            // playerの位置を取得（位置は頭の位置を使用）
            playerpos_r = manager.GetPlayerPositionR();
            playerpos_v = manager.GetPlayerPositionV();

            float distanceVolume = GetDistanceVolume(colliders, negativeColliders);
            float distanceVolumeNonFade_ForLog = distanceVolume;
            if (distanceVolume > 0f || lastVolume > 0f) {
                distanceVolume = GetFadedDistanceVolume(distanceVolume, lastVolume, smoothVolumeOn, smoothVolumeOff);
            }

            if (alwaysOnPlayback) {
                // alwaysOnPlayback にチェックを入れている場合、音量ゼロでなくなったら現在時刻にもとづいた位置から再生開始、音量ゼロになったら再生停止
                if (!audioSource.isPlaying && (distanceVolume > 0f)) {
                    audioSource.Play();
                    audioSource.time = manager.GetElapsedTime() % audioSource.clip.length; // 先頭から再生しない場合、現在の時間に応じたタイミングにシークする
                }
                else if (audioSource.isPlaying && (distanceVolume <= 0f)) {
                    audioSource.Stop();
                }
            }
            else {
                // alwaysOnPlayback にチェックを入れていない場合、音量最大になったら再生開始、音量ゼロになったら再生停止
                if (!audioSource.isPlaying && (distanceVolume >= 1f)) {
                    if (!started) {    // 再生開始後、遠ざかって音量0になるまでの間は再生再開しない（ループしない音源で、自動停止した後すぐ再生開始するのを防ぐため）
                        if (startPlaybackSyncBPM == 0f || (manager.GetElapsedTime() % (60 / startPlaybackSyncBPM) < 0.03f)) {    // startPlaybackSyncBPMを設定している場合、タイミングを合わせて開始
                            audioSource.Play();
                            started = true;
                        }
                    }
                }
                else if (distanceVolume <= 0f) {
                    started = false;
                    if (audioSource.isPlaying) {
                        audioSource.Stop();
                    }
                }
            }

            lastVolume = distanceVolume;

            // SpatialMusicPlayerManager にログを追加（SpatialMusicPlayerManager側で全音源のデータをまとめて出力させる）
            if (manager && manager.GetEnableDebugLog() && ((distanceVolume > 0f) || forceLog)) {
                AddDebugLog(name, distanceVolume, distanceVolumeNonFade_ForLog, audioSource);
            }

            // AudioSourceから実際に出る音量の設定
            audioSource.volume = masterVolume * (distanceVolume) * maxVolume;
            if (audioSource.volume > 1f) {
                audioSource.volume = 1f;
            }
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
                            // コライダーの減衰距離が0でない場合、コライダー表面からの距離をもとに音量を計算する
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
