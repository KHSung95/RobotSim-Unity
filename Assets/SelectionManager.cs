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

    private Transform _currentSelectedTransform;

    public bool IsRobotSelected => _currentSelected != null && _currentSelected.GetComponent<RobotSim.Robot.RobotStateProvider>() != null;
    public bool IsSelected => _currentSelected != null;

    private RobotSim.ROS.MoveRobotToPoseClient _robotClient;

    private void Awake() => Instance = this;
    private void Start() => _handleManager = TransformHandleManager.Instance;

    public void SelectItem(InteractableItem newItem)
    {
        if (newItem == null) return;

        // 1. 동일 아이템 클릭 시 해제 (Toggle)
        if (_currentSelected == newItem)
        {
            ClearSelection();
            return;
        }

        // 2. 기존 선택 항목이 있다면 정리
        ClearSelection();

        // 3. 신규 선택 항목 설정
        _currentSelected = newItem;
        _currentSelected.SetSelected(true);

        // 4. 핸들 생성
        if (_handleManager != null)
        {
            // Robot인 경우 Target Gizmo(Ghost)를 핸들 타겟으로 설정
            if (IsRobotSelected)
            {
                _robotClient = _currentSelected.GetComponent<RobotSim.ROS.MoveRobotToPoseClient>();
                _currentSelectedTransform = _robotClient.targetTransform;
            }
            else
            {
                _robotClient = null;
                _currentSelectedTransform = _currentSelected.transform;
            }

            _activeHandle = _handleManager.CreateHandle(_currentSelectedTransform);
            ConfigureHandle(_activeHandle);

            // [New] 로봇이 선택된 경우에만 Target Gizmo 메쉬 보이기
            if (IsRobotSelected) SetTargetGizmoVisibility(true);
        }
    }

    private void SetTargetGizmoVisibility(bool visible)
    {
        if (_robotClient != null && _robotClient.targetTransform != null)
        {
            // Root 뿐만 아니라 자식에 MeshRenderer가 있을 수 있으므로 GetComponentsInChildren 사용
            var renderers = _robotClient.targetTransform.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.enabled = visible;
            }
        }
    }

    public void ClearSelection()
    {
        if (_currentSelected == null) return;

        // [New] 해제 전 기즈모 숨기기
        SetTargetGizmoVisibility(false);

        _currentSelected.SetSelected(false);
        CleanupHandle();

        _currentSelected = null;
        _activeHandle = null;
        _robotClient = null;
    }

    private bool _isDragging;

    private void ConfigureHandle(Handle handle)
    {
        if (handle == null) return;
        handle.gameObject.SetActive(true);
        handle.ChangeHandleType(HandleType.Position);

        handle.OnInteractionStartEvent += OnHandleInteractionStart;
        handle.OnInteractionEndEvent += OnHandleInteractionEnd;
    }

    private void OnHandleInteractionStart(Handle h) => _isDragging = true;
    private void OnHandleInteractionEnd(Handle h) => _isDragging = false;

    private void CleanupHandle()
    {
        if (_activeHandle == null) return;

        _activeHandle.OnInteractionStartEvent -= OnHandleInteractionStart;
        _activeHandle.OnInteractionEndEvent -= OnHandleInteractionEnd;
        _isDragging = false;

        if (_currentSelected != null)
        {
            _handleManager.RemoveTarget(_currentSelectedTransform, _activeHandle);
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

        // Gizmo Control Logic
        if (_activeHandle != null && !_isDragging)
        {
             // ROS Service Call
             if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
             {
                 if (_robotClient != null)
                 {
                     Debug.Log("[SelectionManager] Enter pressed. Sending Robot Move Request...");
                     _robotClient.SendMoveRequest();
                 }
             }
             
             // Reset Logic (R)
             if (Input.GetKeyDown(KeyCode.R))
             {
                // Case 1: Nothing selected -> Viewpoint Reset
                if (_currentSelected == null)
                {
                    var freeCam = FindFirstObjectByType<Freecam>();
                    if (freeCam != null) freeCam.ResetView();
                }
                // Case 2: Robot Selected -> Robot Joint Reset
                else if (IsRobotSelected)
                {
                    var resetHandler = FindFirstObjectByType<RobotSim.Control.RobotResetHandler>();
                    if (resetHandler != null) resetHandler.TriggerReset();
                }
                // Case 3: Selected item has "Target" tag -> Initial Pose Reset
                else if (_currentSelected.CompareTag("Target"))
                {
                    _currentSelected.ResetToInitial();
                }
                // Case 4: Selected item is Camera -> Mount-based Reset
                else
                {
                    // Check if it's a Freecam
                    var freeCam = _currentSelected.GetComponent<Freecam>();
                    if (freeCam != null)
                    {
                        freeCam.ResetView();
                    }

                    // Check if it's a VirtualCameraMount
                    var camMount = _currentSelected.GetComponentInParent<RobotSim.Sensors.VirtualCameraMount>();
                    if (camMount != null)
                    {
                        if (camMount.MountType == RobotSim.Sensors.CameraMountType.HandEye)
                        {
                            camMount.transform.localPosition = Vector3.zero;
                            camMount.transform.localRotation = Quaternion.identity;
                        }
                        else if (camMount.MountType == RobotSim.Sensors.CameraMountType.BirdEye)
                        {
                            camMount.transform.position = Vector3.zero;
                            camMount.transform.rotation = Quaternion.identity;
                        }
                    }
                }
             }
         }

        // 외부 입력 동기화 (Robot이 아닐 때만 Sync)
        if (_activeHandle != null && _currentSelected != null && !_isDragging && !IsRobotSelected)
        {
            _activeHandle.target.position = _currentSelectedTransform.position;
            _activeHandle.target.rotation = _currentSelectedTransform.rotation;
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