using BepInEx;
using UnityEngine;
using System.Collections;
using System.IO;

[BepInPlugin("com.yourname.bloodstorm", "Blood Storm", "1.4.0")]
public class BloodStorm : BaseUnityPlugin
{
    private GameObject snowEmitter;
    private GameObject tornadoObject;
    private GameObject audioHost;
    private AudioSource windAudio;
    private AudioSource ambientAudio;
    private AudioSource whisperAudio;

    private float windStrength = 10f;
    private float tornadoCooldown = 0f;
    private System.Random random = new System.Random();
    private bool weatherActive = false;

    private void Start()
    {
        Logger.LogInfo("[Blood Storm] Loaded!");
        Invoke("ApplyBloodStorm", 5f);
    }

    // ───────────────────────────────
    // MAIN WEATHER
    // ───────────────────────────────
    private void ApplyBloodStorm()
    {
        weatherActive = true;

        // FOG
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.2f, 0.0f, 0.0f);
        RenderSettings.fogDensity = 0.14f;

        // LIGHT
        RenderSettings.ambientLight = new Color(0.6f, 0.1f, 0.1f);
        var mainLight = GameObject.FindObjectOfType<Light>();
        if (mainLight != null)
        {
            mainLight.color = new Color(1.0f, 0.6f, 0.6f);
            mainLight.intensity = 0.3f;
        }

        // SKYBOX
        if (RenderSettings.skybox != null)
            RenderSettings.skybox.SetColor("_Tint", new Color(0.3f, 0.0f, 0.0f));

        // EFFECTS
        CreateBloodSnow();
        StartCoroutine(ApplyWindEffect());
        StartCoroutine(SpawnBloodyCreatures());
        SetupAudio();

        Logger.LogInfo("[Blood Storm] Activated!");
    }

    // ───────────────────────────────
    // AUDIO SETUP
    // ───────────────────────────────
    private void SetupAudio()
    {
        if (audioHost != null) return;

        audioHost = new GameObject("BloodStormAudio");
        DontDestroyOnLoad(audioHost);

        windAudio = audioHost.AddComponent<AudioSource>();
        ambientAudio = audioHost.AddComponent<AudioSource>();
        whisperAudio = audioHost.AddComponent<AudioSource>();

        string modDir = Path.Combine(Paths.PluginPath, "BloodStorm", "Sounds");

        windAudio.clip = LoadClip(Path.Combine(modDir, "blood_wind.ogg"));
        ambientAudio.clip = LoadClip(Path.Combine(modDir, "blood_ambient.ogg"));
        whisperAudio.clip = LoadClip(Path.Combine(modDir, "whispers.ogg"));

        if (windAudio.clip != null)
        {
            windAudio.loop = true;
            windAudio.volume = 0.4f;
            windAudio.Play();
        }

        if (ambientAudio.clip != null)
        {
            ambientAudio.loop = true;
            ambientAudio.volume = 0.25f;
            ambientAudio.Play();
        }

        if (whisperAudio.clip != null)
        {
            StartCoroutine(PlayWhispers());
        }
    }

    private AudioClip LoadClip(string path)
    {
        if (!File.Exists(path))
        {
            Logger.LogWarning("[Blood Storm] Missing sound: " + path);
            return null;
        }

        var www = new WWW("file://" + path);
        return www.GetAudioClip(false, true);
    }

    private IEnumerator PlayWhispers()
    {
        while (weatherActive && whisperAudio.clip != null)
        {
            yield return new WaitForSeconds(Random.Range(15f, 45f));
            whisperAudio.volume = Random.Range(0.1f, 0.25f);
            whisperAudio.pitch = Random.Range(0.9f, 1.1f);
            whisperAudio.PlayOneShot(whisperAudio.clip);
        }
    }

    // ───────────────────────────────
    // BLOOD SNOW + WIND + TORNADO
    // ───────────────────────────────
    private void CreateBloodSnow()
    {
        if (snowEmitter != null) return;

        snowEmitter = new GameObject("BloodSnowEmitter");
        snowEmitter.transform.position = new Vector3(0, 25, 0);

        var ps = snowEmitter.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startSpeed = 2f;
        main.startSize = 0.05f;
        main.startLifetime = 10f;
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;

        var emission = ps.emission;
        emission.rateOverTime = 250f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(100f, 1f, 100f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = new Color(1f, 0.5f, 0.5f);
    }

    private IEnumerator ApplyWindEffect()
    {
        while (weatherActive)
        {
            yield return new WaitForSeconds(1f);

            var bodies = GameObject.FindObjectsOfType<Rigidbody>();
            Vector3 windDir = new Vector3(
                Mathf.Sin(Time.time * 0.5f),
                0,
                Mathf.Cos(Time.time * 0.5f)
            ).normalized;

            foreach (var rb in bodies)
            {
                if (rb.mass < 80f)
                    rb.AddForce(windDir * windStrength, ForceMode.Acceleration);
            }

            if (tornadoCooldown <= 0f && random.NextDouble() < 0.03)
            {
                SpawnTornado();
                tornadoCooldown = 60f;
            }
            else
                tornadoCooldown -= 1f;
        }
    }

    private void SpawnTornado()
    {
        if (tornadoObject != null) return;

        tornadoObject = new GameObject("BloodTornado");
        tornadoObject.transform.position = new Vector3(random.Next(-40, 40), 0, random.Next(-40, 40));

        var ps = tornadoObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startSpeed = 8f;
        main.startSize = 0.2f;
        main.startLifetime = 2f;
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 500f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 3f;
        shape.length = 20f;

        StartCoroutine(TornadoBehavior(tornadoObject));
    }

    private IEnumerator TornadoBehavior(GameObject tornado)
    {
        float lifetime = 15f;
        float speed = 5f;
        Vector3 dir = new Vector3(random.Next(-1, 2), 0, random.Next(-1, 2)).normalized;

        while (lifetime > 0f)
        {
            lifetime -= Time.deltaTime;
            tornado.transform.position += dir * speed * Time.deltaTime;
            yield return null;
        }

        Destroy(tornado);
        tornadoObject = null;
    }

    // ───────────────────────────────
    // MONSTERS (with fade on proximity)
    // ───────────────────────────────
    private IEnumerator SpawnBloodyCreatures()
    {
        yield return new WaitForSeconds(10f);
        while (weatherActive)
        {
            if (random.NextDouble() < 0.2)
                SpawnRedMonster();
            yield return new WaitForSeconds(30f);
        }
    }

    private void SpawnRedMonster()
    {
        var player = GameObject.FindObjectOfType<CharacterController>();
        if (player == null) return;

        Vector3 spawnPos = player.transform.position + new Vector3(random.Next(-20, 20), 0, random.Next(-20, 20));
        GameObject monster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        monster.name = "Crimson Entity";
        monster.transform.position = spawnPos;

        var rb = monster.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        var glow = monster.AddComponent<Light>();
        glow.color = new Color(1f, 0.1f, 0.1f);
        glow.intensity = 2f;
        glow.range = 6f;

        var rend = monster.GetComponent<MeshRenderer>();
        rend.material.color = new Color(0.6f, 0.1f, 0.1f);

        monster.AddComponent<BloodyMonsterAI>();
    }

    private class BloodyMonsterAI : MonoBehaviour
    {
        private float moveTimer;
        private Vector3 target;
        private System.Random rand = new System.Random();
        private float fadeSpeed = 1.5f;
        private bool fading = false;
        private float respawnDelay = 10f;
        private Renderer rend;
        private Light glow;

        private void Start()
        {
            rend = GetComponent<Renderer>();
            glow = GetComponent<Light>();
            PickNewTarget();
        }

        private void Update()
        {
            moveTimer -= Time.deltaTime;
            if (moveTimer <= 0 && !fading)
                PickNewTarget();

            transform.position = Vector3.MoveTowards(transform.position, target, Time.deltaTime * 2f);

            var player = GameObject.FindObjectOfType<CharacterController>();
            if (player != null && !fading)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist < 5f)
                    StartCoroutine(FadeAndDisappear());
            }
        }

        private void PickNewTarget()
        {
            moveTimer = 3f + (float)rand.NextDouble() * 4f;
            Vector3 randomDir = new Vector3((float)rand.NextDouble() * 20f - 10f, 0, (float)rand.NextDouble() * 20f - 10f);
            target = transform.position + randomDir;
        }

        private IEnumerator FadeAndDisappear()
        {
            fading = true;
            float alpha = 1f;
            Color c = rend.material.color;

            while (alpha > 0f)
            {
                alpha -= Time.deltaTime * fadeSpeed;
                c.a = alpha;
                rend.material.color = c;
                glow.intensity = Mathf.Lerp(2f, 0f, 1f - alpha);
                yield return null;
            }

            gameObject.SetActive(false);
            yield return new WaitForSeconds(respawnDelay);

            transform.position += new Vector3(rand.Next(-30, 30), 0, rand.Next(-30, 30));
            c.a = 1f;
            rend.material.color = c;
            glow.intensity = 2f;
            gameObject.SetActive(true);
            fading = false;
        }
    }

    // ───────────────────────────────
    // RELOAD KEY
    // ───────────────────────────────
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7))
            ApplyBloodStorm();
    }
}
