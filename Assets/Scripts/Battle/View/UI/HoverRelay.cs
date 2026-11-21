// ============================================================
// HoverRelay.cs
// 마우스가 올라왔는지/나갔는지 알려 주는 작은 부품 (자바의 MouseListener 비슷)
// 대장간 모루처럼 "마우스를 올리면 빛나게" 할 때 씀
// ============================================================

using UnityEngine;

public class HoverRelay : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public System.Action<bool> OnChange;
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (OnChange != null) OnChange(true); }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (OnChange != null) OnChange(false); }
}
