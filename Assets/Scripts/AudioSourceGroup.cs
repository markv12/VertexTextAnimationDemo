using UnityEngine;

public class AudioSourceGroup : MonoBehaviour
{
    public AudioSource[] typingSources;
    private int nextTypeSource = 0;

    public void PlayFromNextSource(AudioClip clip) {
        AudioSource nextSource = typingSources[nextTypeSource];
        nextSource.clip = clip;
        nextSource.Play();
        nextTypeSource = (nextTypeSource + 1) % typingSources.Length;
    }
}
