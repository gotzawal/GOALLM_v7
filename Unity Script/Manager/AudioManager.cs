// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Tooltip("Assign an AudioSource component via the Inspector.")]
    public AudioSource audioSource; // Assign this via the Inspector

    private void Awake()
    {
        // Implement Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Ensure AudioSource is assigned
        if (audioSource == null)
        {
            Debug.LogError("AudioManager: AudioSource is not assigned in the Inspector.");
        }
    }

    /// <summary>
    /// Plays audio from a Base64-encoded WAV string.
    /// </summary>
    /// <param name="base64Audio">Base64-encoded WAV audio data.</param>
    public void PlayAudioFromBase64(string base64Audio)
    {
        if (audioSource != null)
        {
            StartCoroutine(DecodeAndPlayAudio(base64Audio));
        }
        else
        {
            Debug.LogError("AudioManager: Cannot play audio because AudioSource is not assigned.");
        }
    }

    private IEnumerator DecodeAndPlayAudio(string base64Audio)
    {
        try
        {
            byte[] audioBytes = Convert.FromBase64String(base64Audio);

            // Parse WAV data
            WAV wav = new WAV(audioBytes);

            // Create AudioClip
            AudioClip audioClip = AudioClip.Create(
                "ServerAudio",
                wav.SampleCount,
                wav.ChannelCount,
                wav.Frequency,
                false
            );
            audioClip.SetData(wav.LeftChannel, 0);

            // Assign and play the audio
            audioSource.clip = audioClip;
            audioSource.Play();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error in AudioManager.DecodeAndPlayAudio: " + ex.Message);
        }

        yield return null;
    }
}

// WAV file parsing class (reusable)
[Serializable]
public class WAV
{
    public float[] LeftChannel { get; private set; }
    public int ChannelCount { get; private set; }
    public int SampleCount { get; private set; }
    public int Frequency { get; private set; }

    public WAV(byte[] wav)
    {
        // Check if mono or stereo
        ChannelCount = BitConverter.ToInt16(wav, 22);

        // Get frequency
        Frequency = BitConverter.ToInt32(wav, 24);

        // Locate the data chunk
        int pos = 12;
        while (
            !(wav[pos] == 'd' && wav[pos + 1] == 'a' && wav[pos + 2] == 't' && wav[pos + 3] == 'a')
        )
        {
            pos += 4;
            int chunkSize = BitConverter.ToInt32(wav, pos);
            pos += 4 + chunkSize;
        }
        pos += 8;

        // Calculate sample count
        SampleCount = (wav.Length - pos) / 2 / ChannelCount;

        // Initialize the left channel array
        LeftChannel = new float[SampleCount];

        // Convert byte data to float
        int i = 0;
        while (pos < wav.Length)
        {
            LeftChannel[i] = BytesToFloat(wav[pos], wav[pos + 1]);
            pos += 2;
            if (ChannelCount == 2)
            {
                pos += 2; // Skip right channel if stereo
            }
            i++;
        }
    }

    private float BytesToFloat(byte firstByte, byte secondByte)
    {
        short s = (short)((secondByte << 8) | firstByte);
        return s / 32768.0f;
    }
}
