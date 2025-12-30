using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // 필수 네임스페이스
public class InteractableItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Material highlightMat;

    private List<Material> originalMat;
    private List<Renderer> Renderers;

    void Start()
    {
        Renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
        originalMat = new List<Material>();
        foreach(var r in Renderers)
        {
            originalMat.Add(r.material);
        }
    }

    // 마우스가 아이템 위에 올라갔을 때 (하이라이트 효과)
    public void OnPointerEnter(PointerEventData eventData)
    {
        foreach (var r in Renderers)
        {
            r.material = highlightMat;
        }
    }

    // 마우스가 나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        for (int i = 0; i < Renderers.Count; i++)
        {
            Renderers[i].material = originalMat[i];
        }
    }

    // 클릭했을 때 (로봇의 타겟으로 설정하거나 선택)
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"{gameObject.name}이 선택되었습니다!");

        // 예: 전역 매니저에게 "내가 이제 타겟이야"라고 알림
        // SimulationManager.Instance.SetTarget(this.transform);
    }
}