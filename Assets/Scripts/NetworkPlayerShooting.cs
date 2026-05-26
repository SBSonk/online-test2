using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class NetworkPlayerShooting : NetworkBehaviour
{
    public Transform cameraTransform;
    public Weapon equippedWeapon;
    public LayerMask shootableLayer;

    // State
    float lastFIreTime = 0;

    public UnityEvent OnShootHit;

    void OnApplicationFocus(bool focus)
    {
        if (!focus) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!IsOwner || !cameraTransform) return;

        if (Input.GetKey(KeyCode.Mouse0) && CanShoot(lastFIreTime))
        {
            lastFIreTime = Time.time;

            Ray r = new Ray(cameraTransform.position, cameraTransform.forward);
            ShootServerRpc(r);
        }
    }

    bool CanShoot(float lastShootTime) => equippedWeapon != null ? Time.time > lastShootTime + equippedWeapon.firerate : false;

    [Rpc(SendTo.Server)]
    public void ShootServerRpc(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hitInfo, equippedWeapon.range, shootableLayer))
        {
            if (hitInfo.transform.root == transform.root) return;

            if (hitInfo.transform.TryGetComponent(out NetworkHasHealth health))
            {
                health.TakeDamage(equippedWeapon.damage);
                NotifyBulletHitRpc();
            }
        }
    }

    [Rpc(SendTo.Owner)]
    void NotifyBulletHitRpc() => OnShootHit?.Invoke();
}