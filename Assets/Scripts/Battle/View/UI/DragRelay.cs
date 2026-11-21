// ============================================================
// DragRelay.cs  (3-163)
// 끌어다 놓기 (드래그 앤 드롭) 를 알려 주는 작은 부품
//   붙인 그림을 마우스로 누른 채 끌면: 시작 → 움직임 → 놓음 순서로 함수를 불러 줌
//   자바 Swing 으로 치면 MouseListener + MouseMotionListener (mousePressed / mouseDragged / mouseReleased) 를 한 번에 묶은 것
//   지금은 상점에서 아티팩트를 판매대로 끌어다 팔 때 씀
// ============================================================
using UnityEngine;
using UnityEngine.EventSystems;

public class DragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public System.Action<PointerEventData> OnBegin;   // 끌기 시작
    public System.Action<PointerEventData> OnMove;    // 끄는 중 (마우스가 움직일 때마다)
    public System.Action<PointerEventData> OnDrop;    // 놓음

    public void OnBeginDrag(PointerEventData e) { if (OnBegin != null) OnBegin(e); }
    public void OnDrag(PointerEventData e) { if (OnMove != null) OnMove(e); }
    public void OnEndDrag(PointerEventData e) { if (OnDrop != null) OnDrop(e); }
}
