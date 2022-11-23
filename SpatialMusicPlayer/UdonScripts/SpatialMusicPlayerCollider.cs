using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Kago171.Udon
{
    public class SpatialMusicPlayerCollider : UdonSharpBehaviour
    {
        public bool isRectangle = false;   // Colliderの形状 true: 矩形, false: 円形
        public float fallOffDistance = 2f;    // 減衰距離（境界線から何m内側で最大音量になるか）
        public float volume = 1f;    // コライダーごとの最大音量
        public bool useVirtualPosition = true;    // 計算に使用するプレイヤー座標 true: 仮想座標, false: 実座標
    }
}
