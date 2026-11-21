// ============================================================
// RelicView.cs
// 아티팩트 그림 + 화면 위쪽 "아티팩트·골드 줄"
//
//   RelicView.MakeIcon : 아티팩트 동그라미 하나 (등급 색 + 이름 첫 글자). 그림이 생기면
//                        Resources/Icons/Relic_아이디.png 로 넣으면 그 그림을 씀
//   RelicBar           : 위쪽 버튼 줄 아래, 플레이어마다 한 줄씩 "P1 (아티팩트들)  골드 35"
//                        아티팩트에 마우스를 올리면 설명 말풍선
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class RelicView
{
    public static Color RarityColor(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Starter: return UIFactory.Hex("#C8B48C");
            case RelicRarity.Special: return UIFactory.Hex("#6EC8A0");
            case RelicRarity.Rare: return UIFactory.Hex("#4A9BFF");
            case RelicRarity.Hero: return UIFactory.Hex("#B46BFF");
            case RelicRarity.Boss: return UIFactory.Hex("#E05A4A");
            case RelicRarity.Party: return UIFactory.Hex("#F0C850");   // 공용(파티): 금색
            default: return UIFactory.Hex("#9A9AA6");
        }
    }

    // 말풍선에 넣을 설명: 이름 [등급] / 효과 / 페널티
    public static string Tip(RelicData r)
    {
        return r.Name + "  [" + r.RarityName() + "]\n" + r.FullText();
    }

    // 아티팩트 동그라미. onHover(설명, 위치) 를 주면 마우스를 올렸을 때 부름
    public static Image MakeIcon(Transform parent, RelicData relic, float size, System.Action<string, RectTransform> onHover)
    {
        Color c = RarityColor(relic.Rarity);
        Image ring = UIFactory.MakeCircle("Relic_" + relic.Id, parent, c);
        ring.rectTransform.sizeDelta = new Vector2(size, size);
        Sprite art = Resources.Load<Sprite>("Icons/Relic_" + relic.Id);
        if (art != null)
        {
            Image img = UIFactory.MakeImage("Art", ring.transform, Color.white, false);
            img.sprite = art;
            img.preserveAspect = true;
            UIFactory.Stretch(img.rectTransform, 3, 3, 3, 3);
            img.raycastTarget = false;
        }
        else
        {
            Image inner = UIFactory.MakeCircle("Inner", ring.transform, Color.Lerp(c, Color.black, 0.6f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            inner.raycastTarget = false;
            Text letter = UIFactory.MakeText("Letter", inner.transform, relic.Name.Substring(0, 1), Mathf.RoundToInt(size * 0.5f), Color.Lerp(c, Color.white, 0.5f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(letter.rectTransform);
            letter.fontStyle = FontStyle.Bold;
            letter.raycastTarget = false;
        }
        if (relic.HasPenalty)
        {
            // 페널티가 있으면 오른쪽 위에 작은 빨간 점
            Image dot = UIFactory.MakeCircle("Penalty", ring.transform, UIFactory.Hex("#FF4040"));
            UIFactory.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-4, -4), new Vector2(size * 0.3f, size * 0.3f));
            dot.raycastTarget = false;
        }
        ring.raycastTarget = true;
        if (onHover != null)
        {
            string tip = Tip(relic);
            RectTransform rt = ring.rectTransform;
            HoverRelay hover = ring.gameObject.AddComponent<HoverRelay>();
            hover.OnChange = (on) => onHover(on ? tip : null, on ? rt : null);
        }
        return ring;
    }
}
