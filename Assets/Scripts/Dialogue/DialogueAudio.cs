using UnityEngine;
using System.Collections;

public class DialogueAudio : MonoBehaviour
{
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource voiceSource;
    public float bgmFadeDuration = 0.8f;
    public float globalFadeOutDuration = 1.0f;
    private string currentBgmName;

    public static DialogueAudio Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure the three AudioSources exist so scenes without bindings still play audio.
        EnsureAudioSources();
    }

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
        }
    }

    public void PlayBGM(string bgmName)
    {
        if (bgmSource != null && !string.IsNullOrEmpty(bgmName))
        {
            if (!string.IsNullOrEmpty(currentBgmName) && currentBgmName == bgmName && bgmSource.isPlaying)
                return;

            AudioClip clip = Resources.Load<AudioClip>("Audio/BGM/" + bgmName);
            if (clip != null)
            {
                bgmSource.clip = clip;
                bgmSource.loop = true;
                bgmSource.Play();
                currentBgmName = bgmName;
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
        if (voiceSource == null)
        {
            Debug.LogWarning("[DialogueAudio] voiceSource missing; cannot play voice.");
            return;
        }

        if (!string.IsNullOrEmpty(voiceName))
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Voice/" + voiceName);
            if (clip != null)
            {
                voiceSource.clip = clip;
                voiceSource.Play();
                Debug.Log($"[DialogueAudio] PlayVoice loaded and playing: {voiceName}");
            }
            else
            {
                Debug.LogWarning($"[DialogueAudio] voice clip not found at Resources/Audio/Voice/{voiceName}");
            }
        }
    }

    public bool IsVoicePlaying()
    {
        return voiceSource != null && voiceSource.isPlaying;
    }

    public Coroutine FadeOutAll(float duration = -1f)
    {
        return StartCoroutine(FadeOutAllCoroutine(duration));
    }

    public IEnumerator FadeOutAllCoroutine(float duration = -1f)
    {
        float fadeDuration = duration > 0f ? duration : (globalFadeOutDuration > 0f ? globalFadeOutDuration : bgmFadeDuration);
        if (fadeDuration <= 0f)
        {
            StopAndResetVolumes();
            yield break;
        }

        AudioSource[] sources = { bgmSource, sfxSource, voiceSource };
        float[] originalVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            originalVolumes[i] = sources[i] != null ? sources[i].volume : 0f;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float factor = 1f - Mathf.Clamp01(t / fadeDuration);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                    sources[i].volume = originalVolumes[i] * factor;
            }
            yield return null;
        }

        StopAndResetVolumes(originalVolumes);
    }

    private void StopAndResetVolumes(float[] originalVolumes = null)
    {
        AudioSource[] sources = { bgmSource, sfxSource, voiceSource };
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;
            sources[i].Stop();
            if (originalVolumes != null && i < originalVolumes.Length)
                sources[i].volume = originalVolumes[i];
        }
    }
}
