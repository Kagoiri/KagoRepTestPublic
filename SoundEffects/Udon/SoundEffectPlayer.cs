
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Kago171.SoundEffects.Udon
{
    public class SoundEffectPlayer : UdonSharpBehaviour
    {
        [SerializeField][Tooltip("ポインタフォーカス音")] private AudioSource focusSound;
        [SerializeField][Tooltip("ボタン音")] private AudioSource clickSound;
        [SerializeField][Tooltip("ページめくり音")] private AudioSource flipSound;
        [SerializeField][Tooltip("テレポート音(Short)")] private AudioSource teleportSound;
        [SerializeField][Tooltip("テレポート音(Long)")] private AudioSource teleportHeavySound;

        public void PlayFocusSound()
        {
            if(focusSound != null) {
                focusSound.Play();
            }
        }

        public void PlayClickSound()
        {
            if(clickSound != null) {
                clickSound.Play();
            }
        }

        public void PlayFlipSound()
        {
            if(flipSound != null) {
                flipSound.Play();
            }
        }

        public void PlayTeleportSound()
        {
            if(teleportSound != null) {
                teleportSound.Play();
            }
        }

        public void PlayTeleportHeavySound()
        {
            if(teleportHeavySound != null) {
                teleportHeavySound.Play();
            }
        }
    }
}
