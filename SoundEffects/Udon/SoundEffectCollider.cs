using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace Kago171.SoundEffects.Udon
{
    public class SoundEffectCollider : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("同じObject に Collider を追加し Is Trigger を有効にすること")]
        [Header("【SoundEffectOnPassingの再生の判定に用いるCollider】")]

        [System.NonSerialized]
        public bool inArea = false;   // 現在コライダー内にいるかどうか。SafeAreaの内外判定に使う

        void Start()
        {
            ;
        }

        // コライダーのTrigger判定を元に、範囲内にいるかどうかの情報を更新する
        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                inArea = true;
                //Debug.Log($"[Kago] {name}.OnPlayerTriggerEnter()");
            }
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                inArea = false;
                //Debug.Log($"[Kago] {name}.OnPlayerTriggerExit()");
            }
        }
    }
}
