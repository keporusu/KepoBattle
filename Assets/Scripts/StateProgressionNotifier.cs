using UnityEngine;

public class StateProgressionNotifier : StateMachineBehaviour
{
    [SerializeField] private string stateName;

    public string StateName => stateName;
    
    public event System.Action<Animator, AnimatorStateInfo, int> OnStateBegin;
    public event System.Action<Animator, AnimatorStateInfo, int> OnStateProgress;
    public event System.Action<Animator, AnimatorStateInfo, int> OnStateEnd;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        OnStateBegin?.Invoke(animator, stateInfo, layerIndex);
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        OnStateProgress?.Invoke(animator,stateInfo,layerIndex);
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        OnStateEnd?.Invoke(animator, stateInfo, layerIndex);
    }

    // OnStateMove is called right after Animator.OnAnimatorMove()
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that processes and affects root motion
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK()
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that sets up animation IK (inverse kinematics)
    //}
}
