using System.Collections.Generic;
using TransformHandles;
using UnityEngine;
using UnityEngine.EventSystems;

public class InteractableItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Material highlightMat;
    private List<Material> originalMats = new List<Material>();
    private List<Renderer> renderers;
    private TransformHandleManager _manager;

    void Start()
    {
        renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
        foreach (var r in renderers) originalMats.Add(r.material);

        _manager = TransformHandleManager.Instance;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        foreach (var r in renderers) r.material = highlightMat;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        for (int i = 0; i < renderers.Count; i++) renderers[i].material = originalMats[i];
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭되면 나를 선택해달라고 매니저에게 요청함
        SelectionManager.Instance.SelectItem(this);
    }
}