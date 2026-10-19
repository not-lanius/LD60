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
                ui?.PlaySequence(GameCopy.MoveJumpTutorial);
                break;

            case TutorialTopic.Dodge:
                ui?.PlaySequence(GameCopy.DodgeTutorial);
                break;

            case TutorialTopic.Door:
                ui?.PlaySequence(GameCopy.DoorTutorial);
                break;

            case TutorialTopic.Melee:
                ui?.PlaySequence(GameCopy.MeleeTutorial);
                break;

            case TutorialTopic.Fire:
                ui?.PlaySequence(GameCopy.FireTutorial);
                SpellInfoUI.Instance?.UnlockGlyph("火");
                SpellInfoUI.Instance?.SetGlyph("火");
                break;

            case TutorialTopic.MountainStele:
                ui?.PlaySequence(GameCopy.MountainTutorial);
                SpellInfoUI.Instance?.UnlockGlyph("山");
                SpellInfoUI.Instance?.SetGlyph("山");
                break;

            case TutorialTopic.LightningStele:
                ui?.PlaySequence(GameCopy.LightningTutorial);
                SpellInfoUI.Instance?.UnlockGlyph("电");
                SpellInfoUI.Instance?.SetGlyph("电");
                break;
        }
    }
}
