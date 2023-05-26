using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TpLab.HeavensGate.Udon;

namespace Kago171.SpatialMusic.Udon
{
    public class SpatialMusicPlayerManager : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("※シーン内に1つだけ配置すること。")]
        [Header("【SpatialMusicPlayerコンポーネント群を管理する】")]

        [SerializeField]
        [Tooltip("プレイヤーのロケーション管理用コンポーネント")]
        PlayerLocationManager playerLocationManager;

        [SerializeField]
        [Tooltip("全体ミュート")]
        bool masterMute = false;

        [SerializeField]
        [Tooltip("全体の基準音量")]
        float masterVolume = 1f;

        [SerializeField]
        [Tooltip("デバッグログを出力するか")]
        bool enableDebugLog = false;

        [SerializeField]
        [Tooltip("デバッグログ出力先のTextコンポーネント")]
        Text debugLogText;

        [SerializeField]
        [Tooltip("オープニング音源")]
        SpatialMusicPlayerForOpening spatialMusicPlayerForOpening;

        float startTime = 0f;   // 開始時のTime.time

        string debugLog;    // デバッグログ

        void Start()
        {
            if (debugLogText) {
                debugLog = "";
            }
            startTime = Time.time;
        }

        void Update()
        {
            if (enableDebugLog && debugLogText) {
                // playerの位置を取得してログ出力（位置は頭の位置を使用）
                Vector3 playerpos_v = Vector3.zero;
                Vector3 playerpos_r = Vector3.zero;
                if (playerLocationManager) {
                    playerpos_r = playerLocationManager.GetRealHeadPosition();
                    playerpos_v = playerLocationManager.GetVirtualHeadPosition();
                }
                else {
                    // playerLocationManager が設定されていなかった場合、RealPositionを使う
                    if (Networking.LocalPlayer != null) { // ワールドアップロード時のランタイムエラー発生防止
                        playerpos_r = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                        playerpos_v = playerpos_r;
                    }
                }
                debugLogText.text = $"T[({GetElapsedTime()})]\r\n";
                if (playerLocationManager.IsInExhibitionRoom) {
                    debugLogText.text += $"V[({playerpos_v.x:f1}, {playerpos_v.z:f1}, {playerpos_v.y:f1})] R[({playerpos_r.x:f1}, {playerpos_r.z:f1}, {playerpos_r.y:f1})]\r\n";
                }
                else {
                    debugLogText.text += $"R[({playerpos_r.x:f1}, {playerpos_r.z:f1}, {playerpos_r.y:f1})]\r\n";
                }

                // 各playerから追加されたログを出力
                debugLogText.text += debugLog;
                debugLog = "";
            }
        }

        public void SetMasterMute(bool mute)
        {
            masterMute = mute;
        }

        public bool GetMasterMute()
        {
            return masterMute;
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = volume;
        }

        public float GetMasterVolume()
        {
            return masterVolume;
        }

        public Vector3 GetPlayerPositionR()
        {
            if (playerLocationManager) {
                return playerLocationManager.GetRealHeadPosition();
            }
            else {
                // playerLocationManager が設定されていなかった場合、RealPositionを使う
                if (Networking.LocalPlayer != null) { // ワールドアップロード時のランタイムエラー発生防止
                    return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                }
            }
            return Vector3.zero;
        }

        public Vector3 GetPlayerPositionV()
        {
            if (playerLocationManager) {
                return playerLocationManager.GetVirtualHeadPosition();
            }
            else {
                // playerLocationManager が設定されていなかった場合、RealPositionを使う
                if (Networking.LocalPlayer != null) { // ワールドアップロード時のランタイムエラー発生防止
                    return Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                }
            }
            return Vector3.zero;
        }

        public float GetElapsedTime()
        {
            return (startTime > 0f) ? (Time.time - startTime) : 0f;
        }

        public void SetEnableDebugLog(bool enable)
        {
            enableDebugLog = enable;
        }

        public bool GetEnableDebugLog()
        {
            return enableDebugLog;
        }

        public void AddDebugLog(string log)
        {
            if (enableDebugLog && debugLogText) {
                debugLog += log;
                debugLog += "\r\n";
            }
        }

        // 一度流れたオープニング音源が再度流れる状態にリセットする
        public void ResetOpeningSound() {
            //Debug.Log("[Kago] SpatialMusicPlayerManager.ResetOpeningSound()");
            if (spatialMusicPlayerForOpening) {
                spatialMusicPlayerForOpening.ResetOpeningSound();
            }
        }
    }
}
