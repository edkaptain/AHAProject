using UnityEngine;

public class GhostItem : MonoBehaviour
{
    public GameObject interactor;
    public GameObject[] others;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == interactor)
        {
            gameObject.SetActive(false);
            if (others.Length > 0)
            {
                for (int i = 0; i < others.Length; i++)
                {
                    others[i].gameObject.SetActive(false);
                }
            }


        }
    }
}
