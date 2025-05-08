using FastAndFractured;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(ChangeSpeedAction), menuName = "EnemyStateMachine/Actions/ChangeSpeedAction")]
public class ChangeSpeedAction : Action
{
    [Range(0, 200)][SerializeField] int speedMultiplier;
    private const float PERCENTAGE_VALUE = 100f;
    public override void Act(Controller controller)
    {
        EnemyAIBrain enemyAIBrain = controller.GetBehaviour<EnemyAIBrain>();
        enemyAIBrain.ModifySpeedMultiplier(speedMultiplier/PERCENTAGE_VALUE);
    }
}
