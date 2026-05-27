using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorLevel : MonoBehaviour
{
    [Header("Configuración de escena")]
    [SerializeField] private string nombreEscena;

    [Header("Tag del jugador")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            SceneManager.LoadScene(nombreEscena);
        }
    }
}
