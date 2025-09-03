/* EmbarkArrow.cs */
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class EmbarkArrow : MonoBehaviour
{
    private Animator animator;

    private void Awake()
    {
        // Get the Animator component on this GameObject.
        animator = GetComponent<Animator>();

        // Find the Mesh Renderer on the child object.
        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            // Set the starting alpha to 0. The animator will take over from here.
            Color startColor = meshRenderer.material.color;
            startColor.a = 0f;
            meshRenderer.material.color = startColor;
        }
        
    }

    // This public method will be called by the BoatController.
    // It starts a coroutine to play the fade-out animation and then destroy the object.
    public void TriggerFadeOutAndDestroy()
    {
        // Check if the GameObject is still active to prevent errors.
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeOutAndDestroyCoroutine());
        }
    }

    private IEnumerator FadeOutAndDestroyCoroutine()
    {
        // Trigger the "FadeOut" animation state in the Animator.
        animator.SetTrigger("FadeOut");

        // Wait for the length of the fade-out animation before destroying.
        // It's good practice to get the clip length dynamically.
        // Let's assume the fade-out is the first clip (index 0) for simplicity,
        // but you can make this more robust if you have many clips.
        float animationLength = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animationLength);

        // Destroy the arrow GameObject after the animation is complete.
        Destroy(gameObject);
    }
}