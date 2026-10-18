using UnityEngine;

public enum TutorialTopic
{
    MoveJump,
    Dodge,
    Door,
    Melee,
    Fire,
    MountainStele,
    LightningStele
}

[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialTopic topic = TutorialTopic.MoveJump;
    [SerializeField] private bool requiresInteract;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool oneShot = true;

    private bool completed;
    private bool playerInside;
    private bool pending;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        playerInside = true;

        if (StorySequenceUI.Instance != null && StorySequenceUI.Instance.IsPlaying)
        {
            pending = true;
            return;
        }

        EnterTrigger();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController2D>() != null)
        {
            playerInside = false;
            pending = false;
        }
    }

    private void Update()
    {
        if (completed || !playerInside)
        {
            return;
        }

        if (StorySequenceUI.Instance != null && StorySequenceUI.Instance.IsPlaying)
        {
            pending = true;
            return;
        }

        if (pending)
        {
            pending = false;
            EnterTrigger();
            return;
        }

        if (requiresInteract && Input.GetKeyDown(interactKey))
        {
            Activate();
        }
    }

    private void EnterTrigger()
    {
        if (requiresInteract)
        {
            if (!completed && TutorialUI.Instance != null)
            {
                TutorialUI.Instance.ShowPrompt(GameCopy.StelePrompt, 4f);
            }

            return;
        }

        Activate();
    }

    public void Activate()
    {
        if (completed && oneShot)
        {
            return;
        }

        completed = true;
        TutorialUI ui = TutorialUI.Instance;

        switch (topic)
        {
            case TutorialTopic.MoveJump:
                ui?.ShowPrompt(GameCopy.MoveJumpPrompt, 6f);
                break;

            case TutorialTopic.Dodge:
                ui?.ShowPrompt(GameCopy.DodgePrompt, 6f);
                break;

            case TutorialTopic.Door:
                ui?.ShowPrompt(GameCopy.DoorPrompt, 4f);
                break;

            case TutorialTopic.Melee:
                ui?.ShowPrompt(GameCopy.MeleePrompt, 7f);
                break;

            case TutorialTopic.Fire:
                ui?.ShowPrompt(GameCopy.FireHint, 9f);
                ui?.ShowDialogue(GameCopy.FireLine, 9f);
                SpellInfoUI.Instance?.SetGlyph("火");
                break;

            case TutorialTopic.MountainStele:
                ui?.ShowPrompt(GameCopy.MountainStele, 8f);
                ui?.ShowDialogue(GameCopy.MountainLine, 8f);
                SpellInfoUI.Instance?.SetGlyph("山");
                break;

            case TutorialTopic.LightningStele:
                ui?.ShowPrompt(GameCopy.LightningStele, 8f);
                ui?.ShowDialogue(GameCopy.LightningLine, 8f);
                SpellInfoUI.Instance?.SetGlyph("电");
                break;
        }
    }
}
