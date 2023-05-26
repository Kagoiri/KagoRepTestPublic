
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Kago171.SoundEffects.Udon
{
    public class SoundEffectOnEnteringExiting : UdonSharpBehaviour
    {
        [Space(10)]
        [Header("【enterExitArea に入った時と出た時に音源を再生する】")]

        [SerializeField]
        [Tooltip("入ったときに再生するAudioSource")]
        AudioSource audioSourceEnter;

        [SerializeField]
        [Tooltip("出たときに再生するAudioSource")]
        AudioSource audioSourceExit;

        [SerializeField]
        [Tooltip("ここに入った時と出た時に音源を再生する。複数指定した場合、いずれかの中にいる間は入っているとみなす")]
        SoundEffectCollider[] enterExitArea;

        bool inArea = false;  // 現在エリア内にいるか？

        void Start()
        {
            audioSourceEnter.playOnAwake = false;
            audioSourceEnter.Stop();
            audioSourceExit.playOnAwake = false;
            audioSourceExit.Stop();
        }

        void Update()
        {
            bool inAreaNow = false;
            foreach (SoundEffectCollider area in enterExitArea)
            {
                if (area.inArea) {
                    inAreaNow = true;
                }
            }

            if(!inArea && inAreaNow) {
                // エリアに入った
                //Debug.Log($"[Kago] {name} Enter");
                if (audioSourceEnter) {
                    audioSourceEnter.Play();
                }
            }
            else if(inArea && !inAreaNow) {
                // エリアを出た
                //Debug.Log($"[Kago] {name} Exit");
                if (audioSourceExit) {
                    audioSourceExit.Play();
                }
            }

            inArea = inAreaNow;
        }
    }
}
