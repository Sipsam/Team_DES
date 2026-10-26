using System;
using UnityEngine;

public enum GameState
{
    Cooperation,
    Competition,
    Finished
}

public sealed class GameStateManager : MonoBehaviour
{
    public event Action<GameState, GameState> StateChanged;

    public GameState CurrentState { get; private set; } = GameState.Cooperation;

    public bool TryTransitionTo(GameState nextState)
    {
        bool isValidTransition =
            (CurrentState == GameState.Cooperation && nextState == GameState.Competition) ||
            (CurrentState == GameState.Competition && nextState == GameState.Finished);

        if (!isValidTransition)
        {
            return false;
        }

        GameState previousState = CurrentState;
        CurrentState = nextState;
        StateChanged?.Invoke(previousState, CurrentState);
        return true;
    }
}
