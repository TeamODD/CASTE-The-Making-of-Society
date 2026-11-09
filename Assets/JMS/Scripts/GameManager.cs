using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private EventManager eventManager;
    [SerializeField] private StatManager statManager;

    public void ChooseLeft()
    {
        ProcessChoice(false);
    }

    public void ChooseRight()
    {
        ProcessChoice(true);
    }

    private void ProcessChoice(bool isRight)
    {
        EventData currentEvent = eventManager.GetCurrentEvent();

        if (currentEvent == null)
            return;

        ChoiceData choice = isRight
            ? currentEvent.rightChoice
            : currentEvent.leftChoice;

        statManager.ApplyChoice(choice);

        eventManager.NextEvent();
    }
}