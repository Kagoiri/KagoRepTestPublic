using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using TpLab.HeavensGate.Udon;

namespace Kago171.SoundEffects.Udon
{
    public class SoundEffectOnPassing : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("【prepareArea を通った後、 playArea を通ると音源を再生する】")]

        // 基本的な使い方としては、
        // [prepareArea]   [playArea]   [resetArea] と通路上に並べておくと、
        // [prepareArea] → [playArea] → [resetArea] と進んだ場合は [playArea] のところで音源再生される。
        // [prepareArea] ← [playArea] ← [resetArea] と進んだ場合は再生されない。
        // 通路を迂回して、
        //          →→→→→→→→→→→→→→→→→→→→→↓
        //          ↑                    ↓
        // [prepareArea] ← [playArea] ← [resetArea] と進んだ場合も再生されない。

        [SerializeField]
        [Tooltip("再生に使うAudioSource")]
        AudioSource audioSource;

        [SerializeField]
        [Tooltip("prepareArea に入ると、再生フラグを立てる")]
        SoundEffectCollider[] prepareArea;

        [SerializeField]
        [Tooltip("再生フラグが立った状態で playArea に入ると音源を再生し、再生フラグを消す")]
        SoundEffectCollider[] playArea;

        [SerializeField]
        [Tooltip("resetArea に入ると、再生フラグを消す")]
        SoundEffectCollider[] resetArea;

        bool prepared = false;  // 再生フラグ(prepareArea を通ったが、colliderPlay は通っていない状態なら true)

        void Start()
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        void Update()
        {
            foreach (SoundEffectCollider area in prepareArea)
            {
                if (!prepared && area.inArea) {
                    //Debug.Log($"[Kago] {name}.prepared = true");
                    prepared = true;
                }
            }

            foreach (SoundEffectCollider area in resetArea)
            {
                if (prepared && area.inArea) {
                    //Debug.Log($"[Kago] {name}.prepared = false");
                    prepared = false;
                }
            }

            foreach (SoundEffectCollider area in playArea)
            {
                if (prepared && area.inArea) {
                    //Debug.Log($"[Kago] {name}.prepared = false, play audioSource");
                    prepared = false;
                    audioSource.Play();
                }
            }
        }
    }
}
