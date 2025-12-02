using UnityEngine;

public class DialogueAudio : MonoBehaviour
{
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource voiceSource;
    public float bgmFadeDuration = 0.8f;

    public static DialogueAudio Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlayBGM(string bgmName)
    {
        if (bgmSource != null && !string.IsNullOrEmpty(bgmName))
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/BGM/" + bgmName);
            if (clip != null)
            {
                bgmSource.clip = clip;
                bgmSource.Play();
            }
        }
    }

    public void PlaySFX(string sfxName)
    {
        if (sfxSource != null && !string.IsNullOrEmpty(sfxName))
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/SFX/" + sfxName);
            if (clip != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }
    }

    public void PlayVoice(string voiceName)
    {
        if (voiceSource != null && !string.IsNullOrEmpty(voiceName))
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Voice/" + voiceName);
            if (clip != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
            }
        }
    }
}


