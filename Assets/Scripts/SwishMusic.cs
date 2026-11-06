
using UnityEngine;

public static class SwishMusic
{
    static AudioSource source;
    public static bool IsOn { get; private set; }

    public static void Initialize()
    {
        if (source != null) return;

        GameObject musicObject = new GameObject("Swish Street Music");
        Object.DontDestroyOnLoad(musicObject);

        source = musicObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>("Audio/BackgroundMusic");
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0.35f;

        IsOn = PlayerPrefs.GetInt("swish-music", 1) == 1;

        if (source.clip == null)
        {
            Debug.LogError("BackgroundMusic not found in Resources/Audio!");
            return;
        }

        if (IsOn) source.Play();
    }

    public static void Toggle()
    {
        if (source == null) Initialize();

        IsOn = !IsOn;
        PlayerPrefs.SetInt("swish-music", IsOn ? 1 : 0);

        if (source == null || source.clip == null) return;

        if (IsOn) source.Play();
        else source.Stop();
    }
}
