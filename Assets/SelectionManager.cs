using UnityEngine;
using TransformHandles;
using System.Collections.Generic;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    private TransformHandleManager _handleManager;
    private Handle _activeHandle;
    private InteractableItem _currentSelected;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _handleManager = TransformHandleManager.Instance;
    }

    public void SelectItem(InteractableItem item)
    {
        // 1. Toggle Deselect
        if (_currentSelected == item)
        {
            ClearSelection();
            return;
        }

        // 2. Swap Target (Efficient)
        // [Fix] Add NEW target first, then Remove OLD, then Update.
        // This prevents the handle from being destroyed if count goes to 0 temporarily.
        if (_activeHandle != null && _currentSelected != null)
        {
            bool added = _handleManager.AddTarget(item.transform, _activeHandle);
            if (added)
            {
                _handleManager.RemoveTarget(_currentSelected.transform, _activeHandle);
                _currentSelected = item;
                _activeHandle.ChangeHandleType(HandleType.Position);
                return;
            }
            else
            {
                ClearSelection();
            }
        }
        else
        {
             ClearSelection();
        }

        // 3. New Creation
        _currentSelected = item;
        if (_handleManager != null)
        {
            _activeHandle = _handleManager.CreateHandle(item.transform);
            
            if (_activeHandle != null)
            {
                 _activeHandle.gameObject.SetActive(true);
                _activeHandle.ChangeHandleType(HandleType.Position);
            }
        }
    }

    public void ClearSelection()
    {
        if (_currentSelected != null)
        {
            if (_activeHandle != null)
            {
                // [CRITICAL FIX] 
                // RemoveHandle logic in the package DOES NOT clear the _transformHashSet!
                // We MUST call RemoveTarget explicitly to clean up the Manager's internal state.
                _handleManager.RemoveTarget(_currentSelected.transform, _activeHandle);
                
                // If RemoveTarget didn't destroy it (depends on logic), we ensure it's removed.
                // Note: RemoveTarget inside Manager automatically calls DestroyHandle if group is empty.
                // So checking validity or just calling RemoveTarget is usually enough.
                // But for safety, if it still exists (unexpected), we force remove.
                if (_activeHandle != null) 
                {
                    // Check if object still exists (Unity Object check)
                    bool handleExists = _activeHandle != null && !_activeHandle.Equals(null); 
                    if (handleExists) _handleManager.RemoveHandle(_activeHandle);
                }
            }
        }
        _currentSelected = null;
        _activeHandle = null;
    }

    // Update Loop
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) ClearSelection();

        // [New] Spacebar Toggle specific to Position <-> Rotation
        if (_activeHandle != null && Input.GetKeyDown(KeyCode.Space))
        {
            if (_activeHandle.type == HandleType.Position)
            {
                _activeHandle.ChangeHandleType(HandleType.Rotation);
                Debug.Log("Switched to Rotation Mode");
            }
            else
            {
                _activeHandle.ChangeHandleType(HandleType.Position);
                Debug.Log("Switched to Position Mode");
            }
        }
    }
}