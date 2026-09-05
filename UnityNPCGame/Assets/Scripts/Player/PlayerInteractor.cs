using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 1.2f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private InteractionPrompt prompt;

    private IInteractable currentTarget;

    private void Update()
    {
        FindNearestInteractable();

        if (currentTarget != null)
        {
            if (prompt != null)
            {
                prompt.Show(currentTarget.InteractionPrompt, ((MonoBehaviour)currentTarget).transform.position);
            }

            if (Input.GetKeyDown(interactKey))
            {
                currentTarget.Interact(gameObject);
            }
        }
        else if (prompt != null)
        {
            prompt.Hide();
        }
    }

    private void FindNearestInteractable()
    {
        currentTarget = null;
        float nearestDist = float.MaxValue;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactableLayer);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IInteractable>(out var interactable))
            {
                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    currentTarget = interactable;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
