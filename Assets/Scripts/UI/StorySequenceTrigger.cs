using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StorySequenceTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = true;

    private bool fired;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fired || StorySequenceUI.Instance == null)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        fired = true;
        StorySequenceUI.Instance.PlayEnding();

        if (!oneShot)
        {
            fired = false;
        }
    }
}
