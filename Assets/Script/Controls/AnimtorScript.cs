using System.Collections;
using UnityEngine;

public class AnimtorScript : StateMachineBehaviour
{
    private Coroutine landingCheck;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var mono = animator.GetComponent<MonoBehaviour>();
        landingCheck = mono.StartCoroutine(CheckLanding(animator));
    }

    private IEnumerator CheckLanding(Animator animator)
    {
        var controller = animator.GetComponent<CharacterController>();
        // Wait until character lands
        while (!controller.isGrounded)
            yield return null;

        animator.SetBool("IsLanding", true);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (landingCheck != null)
        {
            var mono = animator.GetComponent<MonoBehaviour>();
            mono.StopCoroutine(landingCheck);
            landingCheck = null;
        }

        // Reset landing flags once we leave this state
        animator.SetBool("IsLanding", false);
    }
}