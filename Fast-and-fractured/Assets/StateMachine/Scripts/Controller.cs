using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Managers.PauseSystem;
using FastAndFractured;
namespace StateMachine
{
    public class Controller : MonoBehaviour, IPausable
    {
        [SerializeField] private List<Behaviour> availableBehaviours = new List<Behaviour>();

        [SerializeField] State firstState;

        State currentState;

        private bool _paused = false;
        [SerializeField] private bool _isDebugging;

        // Start is called before the first frame update
        private void Awake()
        {
            LevelController.Instance.charactersCustomStart.AddListener(CustomStart);
        }
        public void CustomStart()
        {
            LoadFirsState();
            PauseManager.Instance.RegisterPausable(this);
        }
        public void LoadFirsState()
        {
            ChangeState(firstState);
        }
        // Update is called once per frame
        void Update()
        {
            if (_paused) return;
            if (currentState != null)
            {
                UpdateStateActions();
                EvaluateStateTransitions();
            }
        }
        void UpdateStateActions()
        {
            if (currentState != null)
            {
                foreach (Action action in currentState.actions)
                {
                    action.Act(this);
                }
            }
        }
        void EvaluateStateTransitions()
        {
            State newState = null;

            for (int i = 0; i < currentState.transitions.Length && newState == null; i++)
            {
                if (!currentState.transitions[i].hasExitTime || currentState.AreAllActionsFinished())
                    if (currentState.transitions[i].decision.Decide(this))
                    {
                        newState = currentState.transitions[i].trueState;
                    }
                    else
                    {

                        newState = currentState.transitions[i].falseState;
                    }
            }
            if (newState != null)
            {
                ChangeState(newState);
            }

        }
        void ChangeState(State newState)
        {
            if(_isDebugging)
                Debug.Log($"Entering state {newState.name}");
            ExitState(currentState);
            var newlyInstantiatedState = InstantiateCopyOfState(newState);
            EnterState(newlyInstantiatedState);
            currentState = newlyInstantiatedState;
        }


        void SafelyDestroyInstantiatedState(State stateToDestroy)
        {
            SafelyDestroyInstantiatedActions(stateToDestroy.enterActions);
            SafelyDestroyInstantiatedActions(stateToDestroy.actions);
            SafelyDestroyInstantiatedActions(stateToDestroy.exitActions);
            Destroy(stateToDestroy);
        }
        void ExitState(State stateToExit)
        {
            if (stateToExit == null) return;

            foreach (Action action in stateToExit.exitActions)
            {
                action.Act(this);
            }
            SafelyDestroyInstantiatedState(stateToExit);
        }
        void EnterState(State stateToEnter)
        {
            foreach (Action action in stateToEnter.enterActions)
            {
                action.Act(this);
            }
        }

        public State GetCurrentState() { return currentState; }


        State InstantiateCopyOfState(State stateToCopy)
        {
            State newState = Instantiate(stateToCopy);

            newState.enterActions = InstantiateActions(newState.enterActions);
            newState.actions = InstantiateActions(newState.actions);
            newState.exitActions = InstantiateActions(newState.exitActions);
            newState.name = newState.name.Replace("(Clone)", "");
            return newState;
        }

        void SafelyDestroyInstantiatedActions(Action[] actions)
        {
            int arrayLength = actions.Length;
            for (int i = 0; i < arrayLength; i++)
            {
                Destroy(actions[i]);
            }
        }


        Action[] InstantiateActions(Action[] actionsToInstantiate)
        {
            int arrayLength = actionsToInstantiate.Length;
            Action[] actions = new Action[arrayLength];
            for (int i = 0; i < arrayLength; i++)
            {
                Action newAction = Instantiate(actionsToInstantiate[i]);
                actions[i] = newAction;
            }
            return actions;
        }


        public T GetBehaviour<T>() where T : Behaviour
        {
            foreach (var behaviour in availableBehaviours)
            {
                if (behaviour is T component)
                {
                    return component;
                }
            }
            return null;
        }

        public void ForceState(State newState)
        {
            ChangeState(newState);
        }

        public void OnPause()
        {
            _paused = true;
        }

        public void OnResume()
        {
            _paused = false;
        }

        private void OnDestroy()
        {
            PauseManager.Instance.UnregisterPausable(this);
        }

    }
}