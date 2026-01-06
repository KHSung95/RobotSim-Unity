using UnityEngine;

/// <summary>
/// Passive component that manages visual states (Hover/Selection) for an object.
/// Logic is controlled externally by SelectionManager.
/// </summary>
public class InteractableItem : MonoBehaviour
{
    private Outline _outline;
    private bool _isSelected;
    private bool _isHovered;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;

        _outline = gameObject.GetComponent<Outline>();
        if (_outline == null)
        {
            _outline = gameObject.AddComponent<Outline>();
        }
        
        _outline.OutlineMode = Outline.Mode.OutlineAll;
        _outline.enabled = false;
    }

    public void ResetToInitial()
    {
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
    }

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        UpdateOutlineState();
    }

    public void SetHovered(bool isHovered)
    {
        _isHovered = isHovered;
        UpdateOutlineState();
    }

    private void UpdateOutlineState()
    {
        if (_outline == null) return;

        // Priority 1: Selected (Cyan, Thick)
        if (_isSelected)
        {
            _outline.enabled = true;
            _outline.OutlineColor = Color.cyan;
            _outline.OutlineWidth = 6f;
        }
        // Priority 2: Hovered (White, Thin)
        else if (_isHovered)
        {
            _outline.enabled = true;
            _outline.OutlineColor = Color.white;
            _outline.OutlineWidth = 4f;
        }
        else
        {
            _outline.enabled = false;
        }
    }
}