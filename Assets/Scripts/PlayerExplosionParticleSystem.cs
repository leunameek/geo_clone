using UnityEngine;

public class PlayerExplosionParticleSystem : MonoBehaviour
{
    [Header("Particle Settings")]
    public ParticleSystem explosionPrefab;
    public float explosionScale = 1f;
    public Vector3 explosionOffset = Vector3.zero;

    private ParticleSystem instance;

    public void Explode()
    {
        if (explosionPrefab == null)
        {
            CreateDefaultParticleSystem();
        }

        if (explosionPrefab != null)
        {
            Vector3 spawnPos = transform.position + explosionOffset;
            instance = Instantiate(explosionPrefab, spawnPos, Quaternion.identity);
            if (explosionScale != 1f) instance.transform.localScale *= explosionScale;
        }
    }

    private void CreateDefaultParticleSystem()
    {
        GameObject particleObj = new GameObject("PlayerExplosionParticles");
        instance = particleObj.AddComponent<ParticleSystem>();

        var main = instance.main;
        main.duration = 0.5f; main.loop = false;
        main.startLifetime = 0.5f; main.startSpeed = 5f;
        main.startSize = 0.2f; main.startColor = Color.yellow;
        main.gravityModifier = 0.5f;

        var emission = instance.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 30, 50, 1, 0.01f) });

        var shape = instance.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f;

        var col = instance.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.yellow, 0.0f), new GradientColorKey(Color.red, 0.5f), new GradientColorKey(Color.clear, 1.0f) },
            new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            renderer.material = new Material(sh);
            renderer.material.color = Color.yellow;
        }

        explosionPrefab = instance;
    }

    private void OnDestroy()
    {
        if (instance != null) Destroy(instance.gameObject);
    }
}
