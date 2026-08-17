using System.Collections.Generic;
using UnityEngine;

public class PlayerReplayManager : MonoBehaviour
{
    [Header("Replay Settings")]
    [SerializeField] private float delaySeconds = 0.3f;
    private List<PlayerAction> actionHistory = new List<PlayerAction>();

    public void RecordAction(PlayerAction action)
    {
        actionHistory.Add(action);

        float oldestAllowed = Time.time - delaySeconds - 3f;
        while (actionHistory.Count > 2 && actionHistory[0].time < oldestAllowed)
        {
            actionHistory.RemoveAt(0);
        }
    }

    public bool GetActionAtTime(float targetTime, out PlayerAction replayAction)
    {
        replayAction = default;
        if (actionHistory.Count == 0) return false;

        for (int i = actionHistory.Count - 1; i >= 0; i--)
        {
            if (actionHistory[i].time <= targetTime)
            {
                replayAction = actionHistory[i];
                return true;
            }
        }

        replayAction = actionHistory[0];
        return true;
    }

    public List<PlayerAction> GetActionsBetween(float fromTime, float toTime)
    {
        List<PlayerAction> result = new List<PlayerAction>();

        foreach (var action in actionHistory)
        {
            if (action.time > fromTime && action.time <= toTime)
            {
                result.Add(action);
            }
        }
        return result;
    }
}