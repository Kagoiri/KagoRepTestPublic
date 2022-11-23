using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TpLab.HeavensDoor.Udon;

public class SpatialMusicPlayerOld : UdonSharpBehaviour
{
    [SerializeField] Text debugText;
    //[SerializeField] Slider volumeSlider;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip musicClip;
    [SerializeField] float fallOffStartDistance = 2.0f;   // 減衰開始距離
    [SerializeField] float fallOffEndDistance = 10.0f;    // 減衰終了距離

    [SerializeField] float maxVolume = 0.5f; // 最大音量

    [SerializeField] PlayerLocationManager manager;
    
    void Start()
    {
        audioSource.volume = 0.0f;
        audioSource.clip = musicClip;
        audioSource.Play();
    }

    public void OnVolumeChanged()
    {
        //audioSource.volume = volumeSlider.value * baseVolume;
    }

    void Update()
    {
        var player = Networking.LocalPlayer;

        if (player != null)//Unityの再生ボタンで実行すると変数がnullになる
        {
            //playerの位置を取得（位置は頭の位置を使用）
//            var headData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
//            Vector3 playerpos = headData.position;
            Vector3 playerpos = manager.GetVirtualHeadPosition();
            Vector3 objectpos = transform.position;

            /*
            // 距離の2乗に対する割合
            float sqrDistance = Vector3.SqrMagnitude(playerpos - objectpos);
            float sqrMaxDistance = (maxDistance * maxDistance);
            float distanceVolume = (sqrMaxDistance - sqrDistance) / sqrMaxDistance;
            if (distanceVolume < 0.0f) {
                distanceVolume = 0.0f;
            }
            debugText.text = string.Format("{0}\r\nsqrDistance: {1:f3}\r\nVolume: {2:f3}", name, sqrDistance, distanceVolume);
            */

            // 直線的に減衰する
            float distanceVolume = 0.0f;
            float distance = Vector3.Distance(playerpos, objectpos);
            if (distance <= fallOffStartDistance) {
                // 減衰開始距離以下の場合は最大音量
                distanceVolume = 1.0f;
            }
            else if (distance >= fallOffEndDistance) {
                // 減衰終了距離以上の場合は最大ゼロ
                distanceVolume = 0.0f;
            }
            else {
                distanceVolume = (fallOffEndDistance - distance) / (fallOffEndDistance - fallOffStartDistance);
            }
            debugText.text = string.Format("{0}[TEST]\r\nDistance: {1:f3}\r\nVolume: {2:f3}", name, distance, distanceVolume);

            audioSource.volume = distanceVolume * maxVolume;

            //debugText.text = string.Format("{0}\r\nplayerpos: {1}\r\nobjectpos: {2}\r\ndistance1: {3}\r\ndistance2: {4}", name, playerpos.ToString(), objectpos.ToString(), distance1.ToString(), distance2.ToString());
        }
    } 
}
