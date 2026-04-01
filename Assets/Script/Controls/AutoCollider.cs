using UnityEngine;
using System.Collections.Generic; // Required for List and GetComponentsInChildren with a generic type
    
public class AutoCollider : MonoBehaviour
{
    // Use this method to add colliders when the game starts
    void Start()
    {
        AddCollidersToChildren();
    }

    /// <summary>
    /// Finds all child objects with a MeshFilter and adds a MeshCollider to them.
    /// </summary>
    public void AddCollidersToChildren()
    {
        // Get all MeshFilter components in the children, including grandchildren.
        // We use GetComponentsInChildren to automatically traverse the hierarchy.
        // The boolean parameter 'includeInactive' can be set to true if needed.
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            // Ensure we don't add a MeshCollider to the parent object itself if it has a MeshFilter
            if (meshFilter.gameObject == this.gameObject)
            {
                continue;
            }

            // Check if the child already has a MeshCollider to avoid duplicates
            if (meshFilter.gameObject.GetComponent<MeshCollider>() == null)
            {
                // Add the MeshCollider component to the child GameObject
                MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                
                // Assign the mesh from the MeshFilter to the MeshCollider
                meshCollider.sharedMesh = meshFilter.sharedMesh;

                Debug.Log($"Added MeshCollider to: {meshFilter.gameObject.name}", meshFilter.gameObject);
            }
        }
    }

    // Optional: Add a Context Menu item to run this from the Unity Editor
    // Right-click the script in the Inspector and select "Add Mesh Colliders"
    [ContextMenu("Add Mesh Colliders To Children")]
    private void AddMeshCollidersFromEditor()
    {
        // This method can be called in the Editor for quick setup of imported models.
        AddCollidersToChildren();
    }
}
