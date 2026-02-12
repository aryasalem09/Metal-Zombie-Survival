using System.Collections;
using UnityEngine;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    public class WaveManager : MonoBehaviour
    {
        [Header("config")]
        public WaveConfig config;

        [Header("spawn points (optional)")]
        [Tooltip("Optional. If empty, zombies will spawn just outside the camera view instead.")]
        public Transform[] spawnPoints;

        [Header("runtime (read-only)")]
        [SerializeField] private int currentWave = 0;
        [SerializeField] private int aliveZombies = 0;
        [SerializeField] private bool isSpawning = false;

        private PlayerController player;

        private void Start()
        {
            player = FindObjectOfType<PlayerController>();

            if (config == null)
            {
                Debug.LogError("WaveManager: no WaveConfig assigned.");
                enabled = false;
                return;
            }

            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            yield return new WaitForSeconds(config.timeBeforeFirstWave);

            while (true)
            {
                currentWave++;

                // if config is finite and we finished, move to next scene or stop
                if (config.waveCount > 0 && currentWave > config.waveCount)
                {
                    if (!string.IsNullOrWhiteSpace(config.nextSceneName))
                        SceneTransitionTo(config.nextSceneName);
                    yield break;
                }

                int zombiesThisWave = config.zombiesPerWave + (currentWave - 1) * config.zombiesPerWaveIncrease;
                if (config.maxZombiesPerWave > 0) zombiesThisWave = Mathf.Min(zombiesThisWave, config.maxZombiesPerWave);

                isSpawning = true;

                for (int i = 0; i < zombiesThisWave; i++)
                {
                    // don't flood the scene: cap how many can exist at once
                    while (config.maxAliveAtOnce > 0 && aliveZombies >= config.maxAliveAtOnce)
                        yield return null;

                    SpawnOneZombie(currentWave);
                    yield return new WaitForSeconds(config.timeBetweenSpawns);
                }

                isSpawning = false;

                // wait until everything from this wave is dead
                while (aliveZombies > 0)
                    yield return null;

                yield return new WaitForSeconds(config.timeBetweenWaves);
            }
        }

        private void SceneTransitionTo(string sceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        private void SpawnOneZombie(int waveIndex)
        {
            if (config.zombiePrefab == null) return;

            Vector3 pos = GetSpawnPosition();
            GameObject zObj = Instantiate(config.zombiePrefab, pos, Quaternion.identity);

            var z = zObj.GetComponent<ZombieAI>();
            if (z == null)
            {
                Debug.LogWarning("WaveManager: spawned prefab has no ZombieAI.");
                return;
            }

            aliveZombies++;

            z.Died -= OnZombieDied;
            z.Died += OnZombieDied;

            if (player != null)
            {
                z.player = player.transform;
                z.playerController = player;
            }

            // scaling: health + speed ramp
            float hpMult = Mathf.Pow(config.healthMultiplierPerWave, Mathf.Max(0, waveIndex - 1));
            float spdMult = Mathf.Pow(config.speedMultiplierPerWave, Mathf.Max(0, waveIndex - 1));

            z.maxHealth = Mathf.Max(1, Mathf.RoundToInt(z.maxHealth * hpMult));
            z.currentHealth = z.maxHealth;
            z.moveSpeed = z.moveSpeed * spdMult;

            // runners become more common later
            float runnerChance = config.runnerChanceStart + (waveIndex - 1) * config.runnerChanceIncreasePerWave;
            runnerChance = Mathf.Clamp01(runnerChance);

            z.isRunner = Random.value < runnerChance;
            if (z.isRunner)
            {
                z.moveSpeed *= config.runnerSpeedBonus;
                z.detectionRadius *= config.runnerDetectionBonus;
            }

            // mutations
            ApplyMutations(z, waveIndex);
        }

        private Vector3 GetSpawnPosition()
        {
            // if designer provided spawn points, use them
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (sp != null) return sp.position;
            }

            // otherwise: spawn just outside camera view
            Camera cam = Camera.main;
            if (cam == null)
                return transform.position + (Vector3)(Random.insideUnitCircle.normalized * 10f);

            float margin = Mathf.Max(0.1f, config != null ? config.offscreenSpawnMargin : 2f);

            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

            float left = bl.x - margin;
            float right = tr.x + margin;
            float bottom = bl.y - margin;
            float top = tr.y + margin;

            int side = Random.Range(0, 4);
            Vector3 p;
            if (side == 0) p = new Vector3(left, Random.Range(bottom, top), 0f);
            else if (side == 1) p = new Vector3(right, Random.Range(bottom, top), 0f);
            else if (side == 2) p = new Vector3(Random.Range(left, right), bottom, 0f);
            else p = new Vector3(Random.Range(left, right), top, 0f);

            // avoid spawning right on top of player
            if (player != null && config != null && config.minSpawnDistanceFromPlayer > 0f)
            {
                int guard = 0;
                while (Vector2.Distance(p, player.transform.position) < config.minSpawnDistanceFromPlayer && guard < 20)
                {
                    side = Random.Range(0, 4);
                    if (side == 0) p = new Vector3(left, Random.Range(bottom, top), 0f);
                    else if (side == 1) p = new Vector3(right, Random.Range(bottom, top), 0f);
                    else if (side == 2) p = new Vector3(Random.Range(left, right), bottom, 0f);
                    else p = new Vector3(Random.Range(left, right), top, 0f);
                    guard++;
                }
            }

            return p;
        }

        private void ApplyMutations(ZombieAI z, int waveIndex)
        {
            if (z == null || config == null) return;

            // radiated
            if (waveIndex >= config.radiatedWaveStart)
            {
                float c = config.radiatedChanceStart + (waveIndex - config.radiatedWaveStart) * config.radiatedChanceIncreasePerWave;
                c = Mathf.Clamp01(c);
                if (Random.value < c)
                {
                    z.isRadiated = true;
                    z.maxHealth = Mathf.Max(1, Mathf.RoundToInt(z.maxHealth * config.radiatedHealthBonus));
                    z.currentHealth = z.maxHealth;
                    z.moveSpeed = z.moveSpeed * config.radiatedSpeedBonus;

                    if (z.spriteRenderer != null)
                        z.spriteRenderer.color = new Color(0.7f, 1f, 0.7f, 1f);
                }
            }

            // tank
            if (waveIndex >= config.tankWaveStart)
            {
                float c = config.tankChanceStart + (waveIndex - config.tankWaveStart) * config.tankChanceIncreasePerWave;
                c = Mathf.Clamp01(c);
                if (Random.value < c)
                {
                    z.maxHealth = Mathf.Max(1, Mathf.RoundToInt(z.maxHealth * config.tankHealthBonus));
                    z.currentHealth = z.maxHealth;
                    z.moveSpeed = z.moveSpeed * config.tankSpeedMultiplier;
                    z.transform.localScale = z.transform.localScale * config.tankScaleMultiplier;

                    if (z.spriteRenderer != null)
                        z.spriteRenderer.color = new Color(0.85f, 0.85f, 1f, 1f);
                }
            }
        }

        private void OnZombieDied(ZombieAI z)
        {
            aliveZombies = Mathf.Max(0, aliveZombies - 1);

            if (z != null)
                z.Died -= OnZombieDied;
        }
    }
}