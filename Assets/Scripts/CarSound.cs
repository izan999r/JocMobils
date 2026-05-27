using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarAudio : MonoBehaviour
{
    [Header("Clip")]
    [SerializeField] private AudioClip engineLoopClip;

    [Header("Pitch")]
    [Tooltip("Pitch en reposo (coche parado)")]
    [SerializeField] private float idlePitch = 0.6f;

    [Tooltip("Pitch a velocidad máxima")]
    [SerializeField] private float maxPitch = 2.0f;

    [Tooltip("Suavizado del cambio de pitch. Más alto = más reactivo")]
    [SerializeField] private float pitchSmooth = 4f;

    [Header("Volumen")]
    [Tooltip("Volumen en ralentí")]
    [SerializeField] private float idleVolume = 0.4f;

    [Tooltip("Volumen a velocidad máxima")]
    [SerializeField] private float maxVolume = 0.9f;

    [Tooltip("Suavizado del cambio de volumen")]
    [SerializeField] private float volumeSmooth = 4f;

    [Header("Referencia")]
    [Tooltip("Velocidad máxima del coche (debe coincidir con el CarController)")]
    [SerializeField] private float maxSpeed = 20f;

    private Rigidbody rb;
    private AudioSource engineSource;
    private float currentPitch;
    private float currentVolume;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        engineSource = gameObject.AddComponent<AudioSource>();
        engineSource.clip = engineLoopClip;
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 0f;

        currentPitch = idlePitch;
        currentVolume = idleVolume;
    }

    private void Start()
    {
        if (engineLoopClip == null)
        {
            Debug.LogWarning("[CarAudio] engineLoopClip no asignado.");
            return;
        }
        engineSource.pitch = currentPitch;
        engineSource.volume = currentVolume;
        engineSource.Play();
    }

    private void Update()
    {
        float speed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);

        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, speedRatio);
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, speedRatio);

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * pitchSmooth);
        currentVolume = Mathf.Lerp(currentVolume, targetVolume, Time.deltaTime * volumeSmooth);

        engineSource.pitch = currentPitch;
        engineSource.volume = currentVolume;
    }
}
