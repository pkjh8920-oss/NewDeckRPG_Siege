// ============================================================
// PotionView.cs
// 포션 아이콘 (Resources/Icons/Potion_아이디.png). 빈 칸은 점선 동그라미
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public static class PotionView
{
    public static Color ColorOf(PotionData p)
    {
        return p != null ? UIFactory.Hex(p.ColorHex) : new Color(0.4f, 0.4f, 0.45f);
    }

    // 포션 그림 (p == null 이면 빈 칸)
    public static Image MakeIcon(Transform parent, PotionData p, float size)
    {
        Image back = UIFactory.MakeCircle("Potion", parent, new Color(0f, 0f, 0f, 0.45f));
        back.rectTransform.sizeDelta = new Vector2(size, size);
        Image ring = UIFactory.MakeImage("Ring", back.transform, p != null ? ColorOf(p) : new Color(1f, 1f, 1f, 0.25f), false);
        ring.sprite = UIFactory.RingSprite;
        UIFactory.Stretch(ring.rectTransform);
        ring.raycastTarget = false;
        if (p != null)
        {
            Image art = UIFactory.MakeImage("Art", back.transform, Color.white, false);
            art.sprite = Resources.Load<Sprite>("Icons/Potion_" + p.Id);
            art.preserveAspect = true;
            UIFactory.Stretch(art.rectTransform, 2, 2, 2, 2);
            art.raycastTarget = false;
        }
        return back;
    }
}
