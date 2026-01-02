using System.Collections.Generic;
using TransformHandles;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    private TransformHandleManager _handleManager;
    private Handle _activeHandle;
    private InteractableItem _currentSelected;

    private InteractableItem _currentHovered;

    void Awake() => Instance = this;

    void Start() => _handleManager = TransformHandleManager.Instance;

    public void SelectItem(InteractableItem newItem)
    {
        // 1. 동일 아이템 클릭 시 해제 (Toggle)
        if (_currentSelected == newItem)
        {
            ClearSelection();
            return;
        }

        // 2. 기존 선택 항목이 있다면 깔끔하게 정리
        ClearSelection();

        // 3. 신규 선택 항목 설정 및 시각화
        _currentSelected = newItem;
        if (_currentSelected != null) _currentSelected.SetSelected(true);

        // 4. 핸들 생성 및 설정
        if (_handleManager != null)
        {
            _activeHandle = _handleManager.CreateHandle(_currentSelected.transform);
            ConfigureHandle(_activeHandle);
        }
    }

    public void ClearSelection()
    {
        if (_currentSelected == null) return;

        _currentSelected.SetSelected(false);
        CleanupHandle();

        _currentSelected = null;
        _activeHandle = null;
    }

    private bool _isDragging;

    private void ConfigureHandle(Handle handle)
    {
        if (handle == null) return;
        handle.gameObject.SetActive(true);
        handle.ChangeHandleType(HandleType.Position);

        handle.OnInteractionStartEvent += (h) => _isDragging = true;
        handle.OnInteractionEndEvent += (h) => _isDragging = false;
    }

    private void CleanupHandle()
    {
        if (_activeHandle == null) return;

        _activeHandle.OnInteractionStartEvent -= (h) => _isDragging = true;
        _activeHandle.OnInteractionEndEvent -= (h) => _isDragging = false;
        _isDragging = false;

        if (_currentSelected != null)
        {
            _handleManager.RemoveTarget(_currentSelected.transform, _activeHandle);
        }

        if (!_activeHandle.Equals(null))
        {
            _handleManager.RemoveHandle(_activeHandle);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) ClearSelection();

        UpdateInteraction();

        // 조작 모드 전환 (Space)
        if (_activeHandle != null && Input.GetKeyDown(KeyCode.Space))
        {
            HandleType nextType = (_activeHandle.type == HandleType.Position) ? HandleType.Rotation : HandleType.Position;
            _activeHandle.ChangeHandleType(nextType);
        }

        // 외부 입력 동기화
        if (_activeHandle != null && _currentSelected != null && !_isDragging)
        {
            _activeHandle.target.position = _currentSelected.transform.position;
            _activeHandle.target.rotation = _currentSelected.transform.rotation;
        }
    }

    private void UpdateInteraction()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        bool isOverUI = false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            var eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            if (results.Count > 0 && results[0].gameObject.layer == LayerMask.NameToLayer("UI"))
            {
                isOverUI = true;
            }
        }

        InteractableItem bestInteractable = null;
        HandleBase handleHit = null;
        RaycastHit? bestOtherHit = null;
        float minOtherDistance = float.MaxValue;

        if (!isOverUI)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray);

            foreach (var hit in hits)
            {
                var interactable = hit.collider.GetComponentInParent<InteractableItem>();
                var handle = hit.collider.GetComponentInParent<HandleBase>();

                if (interactable != null)
                {
                    if (bestInteractable == null || hit.distance < minOtherDistance)
                    {
                        bestInteractable = interactable;
                    }
                }
                else if (handle != null)
                {
                    if (handleHit == null) handleHit = handle;
                }
                else
                {
                    if (hit.distance < minOtherDistance)
                    {
                        minOtherDistance = hit.distance;
                        bestOtherHit = hit;
                    }
                }
            }
        }

        // --- Hover 처리 ---
        if (_currentHovered != bestInteractable)
        {
            if (_currentHovered != null) _currentHovered.SetHovered(false);
            _currentHovered = bestInteractable;
            if (_currentHovered != null) _currentHovered.SetHovered(true);
        }

        // --- Click 처리 ---
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            if (handleHit != null)
            {
                // 핸들 조작은 매니저가 처리하므로 무시
            }
            else if (bestInteractable != null)
            {
                SelectItem(bestInteractable);
            }
            else
            {
                // 아무것도 안 맞았거나 일반 물체 클릭 시 해제
                ClearSelection();
            }
        }
    }
}