using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public GameObject hitmarker;
    public float hitmarkerStayTime = .1f;

    public Image healthBar;

    NetworkHasHealth player;
    NetworkPlayerShooting shooting;

    public void Initialize(NetworkHasHealth player, NetworkPlayerShooting shooting)
    {
        this.player = player;
        this.shooting = shooting;

        shooting.OnShootHit.AddListener(ShowHitMarker);
    }

    void OnDestroy()
    {
        shooting.OnShootHit.RemoveListener(ShowHitMarker);
    }

    void Update()
    {
        if (!player) return;
        healthBar.fillAmount = (float) player.health.Value / player.maxHealth.Value;
    }

    public void ShowHitMarker()
    {
        StartCoroutine(HitAnimation());
    }

    IEnumerator HitAnimation()
    {
        hitmarker.SetActive(true);

        yield return new WaitForSeconds(hitmarkerStayTime);

        hitmarker.SetActive(false);
    }
}
