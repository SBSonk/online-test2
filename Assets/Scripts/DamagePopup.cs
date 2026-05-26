using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    public TextMeshPro textMesh;
    public float floatSpeed = 2f;
    public float fadeSpeed = 3f;
    public float lifeTime = 0.5f;

    private Color textColor;
    private float timer;
    private Transform mainCamera;

    public void Initialize(int damageAmount)
    {
        textMesh.text = damageAmount.ToString();
        textColor = textMesh.color;
        timer = lifeTime;
        
        if (Camera.main != null) 
            mainCamera = Camera.main.transform;

        transform.position += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
    }

    void Update()
    {
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        if (mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.position);
        }

        timer -= Time.deltaTime;
        if (timer < 0)
        {
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;
            
            if (textColor.a <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}