using UnityEngine;

public class BukaPintu : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string triggerName = "isBuka";

    public void Buka()
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }
}
