using UnityEngine;

public class FeetIK : MonoBehaviour
{
    public Animator animator;



    
        private void Start()
        {
             animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if(!animator) return;
            

        }
    
}
