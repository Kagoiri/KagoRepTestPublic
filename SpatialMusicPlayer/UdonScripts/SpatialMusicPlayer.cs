using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TpLab.HeavensDoor.Udon;

namespace Kago171.Udon
{
    public class SpatialMusicPlayer : UdonSharpBehaviour
    {
        [SerializeField] AudioClip musicClip;
        [SerializeField] AudioSource audioSource;
        [SerializeField] PlayerLocationManager playerLocationManager;
        [SerializeField] Text debugText;

        [SerializeField] float maxVolume = 0.5f; // 最大音量
        [SerializeField] float smoothVolumeOn = 0.02f; // 音量が上がる際の1/60秒あたりの最大変化量（小さくするとふわっと音量変化する）
        [SerializeField] float smoothVolumeOff = 0.01f; // 音量が下がる際の1/60秒あたりの最大変化量（小さくするとふわっと音量変化する）
        [SerializeField] bool alwaysOnPlayback = true; // true:常時再生, false:近づいたら再生開始

        [SerializeField] SpatialMusicPlayerCollider[] colliders;    // 音源を鳴らす領域、複数指定可
        [SerializeField] SpatialMusicPlayerCollider[] negativeColliders;    // 音源を止める（音量を下げる）領域、複数指定可
        [SerializeField] SpatialMusicPlayerCollider safeArea;   // 負荷軽減のため、この範囲外では音量計算をしない(0固定)、矩形＆Rotation=0のColliderのみ使用可能

        float lastVolume = 0f;
        bool inSafeArea = true;
        Vector3 playerpos_v;
        Vector3 playerpos_r;

        void Start()
        {
            audioSource.volume = 0f;
            audioSource.clip = musicClip;
            if (alwaysOnPlayback) {
                audioSource.Play();
            }
        }

        void Update()
        {
            if (debugText) {
                debugText.text = "";
                debugText.text += $"[{name}]\r\n";
                debugText.text += $"Playing: {audioSource.isPlaying}\r\n";
            }

            // playerの位置を取得（位置は頭の位置を使用）
            if (playerLocationManager) {
                playerpos_r = playerLocationManager.GetRealHeadPosition();
                playerpos_v = playerLocationManager.GetVirtualHeadPosition();
            }
            else {
                // playerLocationManager が設定されていなかった場合、RealPositionを使
                if(Networking.LocalPlayer != null) { // ワールドアップロード時のランタイムエラー発生防止
                    playerpos_r = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                    playerpos_v = playerpos_r;
                }
            }
            //debugText.text += $"Player: V[({playerpos_v.x:f1}, {playerpos_v.z:f1})] R[({playerpos_r.x:f1}, {playerpos_r.z:f1})]\r\n";

            float distanceVolume = 0f;
            if (!safeArea || isInSafeArea((safeArea.useVirtualPosition ? playerpos_v : playerpos_r), safeArea)) {
                // SafeAreaの設定がない、もしくはSafeArea内に居る場合は音量計算をする

                // 各 collider に対する音量を計算し、最大のものを使用する。
                foreach (SpatialMusicPlayerCollider c in colliders)
                {
                    if (c) { // Colliders に中身が入っていなかった場合は無視
                        //debugText.text += $"{c.name}: ";
                        float distanceVolumeEach = c.volume * (c.useVirtualPosition ? GetVolume(playerpos_v, c) : GetVolume(playerpos_r, c));
                        if (distanceVolume < distanceVolumeEach) {
                            distanceVolume = distanceVolumeEach;
                        }
                        //debugText.text += $"VolEach:{distanceVolumeEach:f2}, Vol:{distanceVolume:f2}\r\n";
                    }
                }

                // 各 negativeCollider に対する音量を計算し、最大のものを使用する。
                float negativeDistanceVolume = 0f;
                foreach (SpatialMusicPlayerCollider c in negativeColliders)
                {
                    if (c) { // Colliders に中身が入っていなかった場合は無視
                        //debugText.text += $"{c.name}: ";
                        float distanceVolumeEach = c.volume * (c.useVirtualPosition ? GetVolume(playerpos_v, c) : GetVolume(playerpos_r, c));
                        if (negativeDistanceVolume < distanceVolumeEach) {
                            negativeDistanceVolume = distanceVolumeEach;
                        }
                        //debugText.text += $"VolEach:{distanceVolumeEach:f2}, NegVol:{negativeDistanceVolume:f3}\r\n";
                    }
                }

                // collider に基づく音量から、negativeCollider に基づく音量を減算する。
                distanceVolume -= negativeDistanceVolume;
                if (distanceVolume < 0f) {
                    distanceVolume = 0f;
                }
            }
            if (debugText) { debugText.text += $"Volume: {distanceVolume:f2}"; }

            // 前回からの Volumeの変化量が smoothVolumeOn/Off より大きければ、変化量を smoothVolumeOn/Off に抑える。
            if (distanceVolume - lastVolume > smoothVolumeOn) {
                distanceVolume = lastVolume + smoothVolumeOn;
                if (debugText) { debugText.text += $" -> {distanceVolume:f2}"; }
            }
            else if (distanceVolume - lastVolume < -smoothVolumeOff) {
                distanceVolume = lastVolume - smoothVolumeOff;
                if (debugText) { debugText.text += $" -> {distanceVolume:f2}"; }
            }

            // alwaysOnPlayback にチェックを入れていない場合、音量最大になったら再生開始、音量ゼロになったら再生停止
            if(!alwaysOnPlayback) {
                if (!audioSource.isPlaying && (distanceVolume >= 1f)) {
                    audioSource.Play();
                }
                else if (audioSource.isPlaying && (distanceVolume <= 0f)) {
                    audioSource.Stop();
                    //audioSource.Pause();  // Pauseだと、再び再生開始した際に途中から急に始まって変な感じになるので
                }
            }


            lastVolume = distanceVolume;
            //audioSource.volume = distanceVolume * maxVolume;
            audioSource.volume = distanceVolume * distanceVolume * maxVolume;   // 聴感上、2乗にした方が自然な変化になった
        }

        bool isInSafeArea(Vector3 pos, SpatialMusicPlayerCollider area) {
            return ((pos.x >= area.transform.position.x - (area.transform.lossyScale.x / 2))
            && (pos.x <= area.transform.position.x + (area.transform.lossyScale.x / 2))
            && (pos.z >= area.transform.position.z - (area.transform.lossyScale.z / 2))
            && (pos.z <= area.transform.position.z + (area.transform.lossyScale.z / 2)));
        }

        // プレイヤー位置とコライダー位置をもとに音量を取得
        float GetVolume(Vector3 playerpos, SpatialMusicPlayerCollider collider)
        {
            float distanceVolume = 0f;
            if (collider.isRectangle) { // 矩形

                /* 
                 * 矩形の頂点座標を求める
                 * まず、Positionが原点, Rotationが0だった場合の各頂点の座標を求める（scale.x, scale.zの半分を絶対値として、それぞれのプラスマイナスで4頂点。yは0固定）
                 * それぞれに対して、rotationを掛ける（roration分回転させる）。rotationはもともとQuaternionなので変換不要で、そのまま掛けるだけ。
                 * ※rotationはinspector上でy軸のみに値が入っている想定（上から見た回転のみを考える）だが、たぶんx,z軸のrotationも入れれば反映されそうな気がする（→ 平行四辺形の領域とかも作れる？）
                 * 最後にワールド上のPositionを足して、実際のワールド内の座標にする(実際に使うのはx,z軸で、y軸の値は無視する)
                 */
                Vector3[] vertex = new Vector3[4];
                vertex[0] = collider.transform.rotation * new Vector3((collider.transform.lossyScale.x)/2, 0, (collider.transform.lossyScale.z)/2) + collider.transform.position;
                vertex[1] = collider.transform.rotation * new Vector3((collider.transform.lossyScale.x)/2, 0, -(collider.transform.lossyScale.z)/2) + collider.transform.position;
                vertex[2] = collider.transform.rotation * new Vector3(-(collider.transform.lossyScale.x)/2, 0, -(collider.transform.lossyScale.z)/2) + collider.transform.position;
                vertex[3] = collider.transform.rotation * new Vector3(-(collider.transform.lossyScale.x)/2, 0, (collider.transform.lossyScale.z)/2) + collider.transform.position;
                //debugText.text += $"[({vertex[0].x:f1},{vertex[0].z:f1}), ({vertex[1].x:f1},{vertex[1].z:f1}), ({vertex[2].x:f1},{vertex[2].z:f1}), ({vertex[3].x:f1},{vertex[3].z:f1})]";

                /*
                 * 四角形の中心点とプレイヤー位置を結んだ直線が、四角形の各辺のいずれかと交差するか？
                 * どれかと交差するなら領域外 → 音量0
                 * どれとも交差しないなら領域内 → 中心点と交点の距離、および中心点とプレイヤーの距離から音量を求める
                 */
                //debugText.text += $": X[";
                bool isOutside = false;
                for (int i=0; i<4; i++) {
                    isOutside = isIntersected(playerpos, collider.transform.position, vertex[i], vertex[(i<3 ? i+1 : 0)]);
                    //debugText.text += $"{isOutside}, ";
                    if (isOutside) {
                        break;
                    }
                } 
                //debugText.text += $"]\r\n";

                // collider領域の内側に居る場合は音量を計算する
                if (!isOutside) {
                    //debugText.text += $"i[";
                    float distancePlayerAndColliderSurface = 10000f; // 充分大きな値
                    float distancePlayerAndColliderCore = Vector2.Distance(new Vector2(playerpos.x, playerpos.z), new Vector2(collider.transform.position.x, collider.transform.position.z));
                    Vector3[] intersection = new Vector3[4];
                    for (int i=0; i<4; i++) {
                        intersection[i] = getIntersection(playerpos, collider.transform.position, vertex[i], vertex[(i<3 ? i+1 : 0)]);
                        float distanceEach = Vector2.Distance(new Vector2(playerpos.x, playerpos.z), new Vector2(intersection[i].x, intersection[i].z));
                        //debugText.text += $"({intersection[i].x:f1},{intersection[i].z:f1}:{distanceEach:f1}), ";
                        if (distancePlayerAndColliderSurface > distanceEach) {
                            distancePlayerAndColliderSurface = distanceEach;
                        }
                    } 
                    //debugText.text += $"], Min:{distancePlayerAndColliderSurface:f1}\r\n";

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
                    // collider領域の外に居る場合は音量ゼロ
                    distanceVolume = 0f;
                }
            }
            else {  // 円形

                // y軸は無視して、x/z軸のみの距離を見る
                float distancePlayerAndColliderCore = Vector2.Distance(new Vector2(playerpos.x, playerpos.z), new Vector2(collider.transform.position.x, collider.transform.position.z));
                float fallOffEndDistance = (collider.transform.lossyScale.x)/2;
                if (distancePlayerAndColliderCore <= (fallOffEndDistance - collider.fallOffDistance)) {
                    // 減衰開始距離以下の場合は最大音量
                    distanceVolume = 1f;
                }
                else if (distancePlayerAndColliderCore >= fallOffEndDistance) {
                    // 減衰終了距離以上の場合は最大ゼロ
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
        bool isIntersected(Vector3 pA1, Vector3 pA2, Vector3 pB1, Vector3 pB2)
        {
            float t1 = (pA2.x - pA1.x) * (pB1.z - pA1.z) - (pA2.z - pA1.z) * (pB1.x - pA1.x);
            float t2 = (pA2.x - pA1.x) * (pB2.z - pA1.z) - (pA2.z - pA1.z) * (pB2.x - pA1.x);
            float t3 = (pB2.x - pB1.x) * (pA1.z - pB1.z) - (pB2.z - pB1.z) * (pA1.x - pB1.x);
            float t4 = (pB2.x - pB1.x) * (pA2.z - pB1.z) - (pB2.z - pB1.z) * (pA2.x - pB1.x);
            return ((t1 * t2 < 0f) && (t3 * t4 < 0f));
        }

        // ある2地点を結ぶ直線と、別の2地点を結ぶ直線の交点を返す
        Vector3 getIntersection(Vector3 pA1, Vector3 pA2, Vector3 pB1, Vector3 pB2)
        {
            float det = (pA1.x - pA2.x) * (pB2.z - pB1.z) - (pB2.x - pB1.x) * (pA1.z - pA2.z);
            float t = ((pB2.z - pB1.z) * (pB2.x - pA2.x) + (pB1.x - pB2.x) * (pB2.z - pA2.z)) / det;
            float x = t * pA1.x + (1f - t) * pA2.x;
            float z = t * pA1.z + (1f - t) * pA2.z;
            return new Vector3(x, 0, z);
        }
    }
}
