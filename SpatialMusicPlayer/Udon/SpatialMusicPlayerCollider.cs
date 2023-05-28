using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Kago171.SpatialMusic.Udon
{
    public class SpatialMusicPlayerCollider : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("ただし、SafeArea, EndLoopArea, ResetArea として使う場合、同じObject に Collider を追加し Is Trigger を有効にすること")]
        [Header("※Colliderコンポーネントは不要")]
        [Header("【SpatialMusicPlayerの音量計算に用いるCollider】")]

        [Tooltip("Colliderの形状 true: 矩形, false: 円形")]
        public bool isRectangle = false;

        [Tooltip("減衰距離（境界線から何m内側で最大音量になるか）")]
        public float fallOffDistance = 2f;

        [Tooltip("Colliderごとの最大音量")]
        public float volume = 1f;

        [Tooltip("計算に使用するプレイヤー座標 true: 仮想座標, false: 実座標")]
        public bool useVirtualPosition = true;

        // 
        [System.NonSerialized]
        public bool inTrigger = false;   // 現在コライダー内にいるかどうか。SafeAreaの内外判定に使う

        void Start()
        {
            // inTrigger の初期状態を設定
            // 開始時にCollider内部に居るかどうかを判定する方法がない？　ので初回のみ計算で判定する。ここだけ BoxCollider かつ Rotation.*==0 前提の実装になっているので注意
            if (Networking.LocalPlayer != null) { // ワールドアップロード時のランタイムエラー発生防止
                Vector3 playerpos = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                inTrigger = ((playerpos.x >= this.transform.position.x - (this.transform.lossyScale.x / 2))
                    && (playerpos.x <= this.transform.position.x + (this.transform.lossyScale.x / 2))
                    && (playerpos.y >= this.transform.position.y - (this.transform.lossyScale.y / 2))
                    && (playerpos.y <= this.transform.position.y + (this.transform.lossyScale.y / 2))
                    && (playerpos.z >= this.transform.position.z - (this.transform.lossyScale.z / 2))
                    && (playerpos.z <= this.transform.position.z + (this.transform.lossyScale.z / 2)));
            }
            //Debug.Log($"[Kago] {name}.inTrigger = {inTrigger}");
        }

        // SafeAreaの当たり判定にコライダーのTriggerを使う（音量計算がないので、当たっているかどうかだけ調べられればよい）
        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                inTrigger = true;
                //Debug.Log($"[Kago] {name}.OnPlayerTriggerEnter()");
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                inTrigger = false;
                //Debug.Log($"[Kago] {name}.OnPlayerTriggerExit()");
            }
        }
    }
}
