using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DestroyIt
{
    /// <summary>This script manages all player input.</summary>
    [DisallowMultipleComponent]
    public class InputManager : MonoBehaviour
    {
        public GameObject cannonballPrefab;         // The cannonball prefab to launch.
        public float cannonballVelocity = 75f;      // Launch velocity of the cannonball.
        public GameObject rocketPrefab;			    // The rocket prefab to launch.
        public GameObject bulletPrefab;
        public ParticleSystem muzzleFlash;
        public ParticleSystem cannonFire;
        public ParticleSystem rocketFire;
        public Light muzzleLight;
        public GameObject launcherRocket;       // The rocket gameobject, seated inside the launcher while ready to fire.
        public int bulletDamage = 15;               // the amount of damage the bullet does to its target
        public float bulletForcePerSecond = 25f;    // the amount of force the bullet applies to impacted rigidbodies
        public float bulletForceFrequency = 10f;
        [Range(1, 30)]
        public int gunShotsPerSecond = 8;           // The gun's shots per second (rate of fire) while Fire1 button is depressed.
        public float startDistance = 1.5f; 		    // The distance projectiles/missiles will start in front of the player.
        public WeaponType startingWeapon = WeaponType.Rocket;   // The weapon the player will start with.
        public GameObject nukePrefab;
        public float shockwaveSpeed = 800f;         // How fast the shockwave expands (ie, how much force is applied to the shock walls).
        public GameObject shockWallPrefab;
        public GameObject dustWallPrefab;
        public int dustWallDistance = 120;          // How far in front of the shockwave should the dust effect around a player trigger?
        public GameObject groundChurnPrefab;
        public int nukeDistance = 2500;             // The distance the nuke starts away from the player.
        public int groundChurnDistance = 90;
        [Range(0.1f, .5f)]
        public float timeSlowSpeed = 0.25f;
        public GameObject windZone;
        public WeaponType SelectedWeapon { get; set; }

        private bool timeSlowed;
        private bool timeStopped;
        private float timeBetweenShots;
        private float meleeAttackDelay;
        private float lastShotTime;
        private float lastMeleeTime;
        private int playerPrefShowReticle = -1;
        private int playerPrefShowHud = -1;
        private float rocketTimer;
        private float nukeTimer;
        private CharacterController firstPersonController;
        private Transform gunTransform;             // The transform where the gun will be fired from.
        private Transform cannonTransform;          // The transform where the cannon will be fired from.
        private Transform rocketTransform;          // The transform where the rocket will be fired from.
        private Transform nukeTransform;            // The location of the nuke firing controller.
        private Transform axeTransform;             // The location of the axe.
        private Transform repairWrenchTransform;    // The location of the repair wrench.
        private readonly LoadSceneParameters lsp = new LoadSceneParameters { loadSceneMode = LoadSceneMode.Single, localPhysicsMode = LocalPhysicsMode.None };

        // Hide the default constructor (use InputManager.Instance instead).
        private InputManager() { }

        public static InputManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (Camera.main == null || Camera.main.transform.parent == null) return;
            firstPersonController = Camera.main.transform.parent.GetComponent<CharacterController>();
            if (firstPersonController == null)
                Debug.LogError("InputManager: Could not find Character Controller on Main Camera parent.");
            
            foreach (Transform trans in Camera.main.transform)
            {
                switch (trans.name)
                {
                    case "WeaponPosition-Nuke":
                        nukeTransform = trans;
                        break;
                    case "WeaponPosition-Gun":
                        gunTransform = trans;
                        break;
                    case "WeaponPosition-Axe":
                        axeTransform = trans;
                        break;
                    case "WeaponPosition-Cannon":
                        cannonTransform = trans;
                        break;
                    case "WeaponPosition-Rocket":
                        rocketTransform = trans;
                        break;
                    case "WeaponPosition-RepairWrench":
                        repairWrenchTransform = trans;
                        break;
                    default: // default to gun
                        gunTransform = trans;
                        break;
                }
            }

            if (muzzleLight != null && muzzleLight.enabled)
                muzzleLight.enabled = false;

            timeBetweenShots = 1f / gunShotsPerSecond;
            meleeAttackDelay = 0.6f; // Limit melee attacks to one every 1/2 second.
            lastShotTime = 0f;
            lastMeleeTime = 0f;

            SetTimeScale();
            
            // Set active weapon from player preferences.
            int playerPrefWeapon = PlayerPrefs.GetInt("SelectedWeapon", -1);
            if (playerPrefWeapon == -1)
                SelectedWeapon = startingWeapon;
            else
	            SelectedWeapon = (WeaponType)playerPrefWeapon;
	        
            // Set HUD visibility options from player preferences.
            playerPrefShowHud = PlayerPrefs.GetInt("ShowHud", -1);
            playerPrefShowReticle = PlayerPrefs.GetInt("ShowReticle", -1);

	        #if UNITY_WEBGL
	        if (SelectedWeapon == WeaponType.Nuke)
		        SelectedWeapon = startingWeapon;
	        #endif
	        
            SetActiveWeapon();
        }

        private void Update()
        {
            if (nukeTimer > 0f)
                nukeTimer -= Time.deltaTime;
            if (nukeTimer < 0f)
                nukeTimer = 0f;
            if (rocketTimer > 0f)
                rocketTimer -= Time.deltaTime;
            if (rocketTimer <= 0f)
            {
                if (launcherRocket != null)
                    launcherRocket.SetActive(true);
                rocketTimer = 0f;
            }

            if (Input.GetButtonDown("Fire1"))
            {
                //Cursor.lockState = CursorLockMode.Locked;

                switch (SelectedWeapon)
                {
                    case WeaponType.Cannonball:
                        if (cannonFire != null)
                        {
                            cannonFire.GetComponent<ParticleSystem>().Clear(true);
                            cannonFire.Play(true);
                        }
                        WeaponHelper.Launch(cannonballPrefab, cannonTransform, startDistance, cannonballVelocity, true);
                        break;
                    case WeaponType.Rocket:
                        if (rocketTimer <= 0f)
                        {
                            if (launcherRocket != null)
                                launcherRocket.SetActive(false);
                            if (rocketFire != null)
                            {
                                rocketFire.GetComponent<ParticleSystem>().Clear(true);
                                rocketFire.Play(true);
                            }
                            WeaponHelper.Launch(rocketPrefab, rocketTransform, startDistance + .1f, 6f, false);
                            RocketLoading rocketLoading = launcherRocket.GetComponentInChildren<RocketLoading>();
                            if (rocketLoading != null)
                                rocketLoading.isLoaded = false;

                            rocketTimer = 1f; // one rocket every X seconds.
                        }
                        break;
                    case WeaponType.Nuke: // Nuclear Blast and Rolling Shockwave Damage
                        if (nukeTimer <= 0f)
                        {
                            FadeIn flashEffect = gameObject.AddComponent<FadeIn>();
                            flashEffect.startColor = Color.white;
                            flashEffect.fadeLength = 5f;

                            // position the nuke 2500m in front of where the player is facing.
                            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
                            Vector3 nukeForwardPos = player.position + player.forward * nukeDistance;
                            Vector3 nukePos = new Vector3(nukeForwardPos.x, 0f, nukeForwardPos.z);
                            if (groundChurnPrefab != null)
                            {
                                GameObject groundChurn = Instantiate(groundChurnPrefab, nukePos, Quaternion.identity) as GameObject;
                                Follow followScript = groundChurn.AddComponent<Follow>();
                                followScript.isPositionFixed = true;
                                followScript.objectToFollow = player;
                                followScript.facingDirection = FacingDirection.FixedPosition;
                                followScript.fixedFromPosition = nukePos;
                                followScript.fixedDistance = groundChurnDistance;
                            }
                            GameObject nuke = Instantiate(nukePrefab, nukePos, Quaternion.Euler(Vector3.zero)) as GameObject;
                            nuke.transform.LookAt(player);
                            
                            // Configure Wind Zone
                            if (windZone != null)
                            {
                                windZone.transform.position = nukeForwardPos;
                                windZone.transform.LookAt(player);
                                Invoke("EnableWindZone", 5f);
                                DisableAfter disableAfter = windZone.GetComponent<DisableAfter>() ?? windZone.AddComponent<DisableAfter>();
                                disableAfter.seconds = 25f;
                                disableAfter.removeScript = true;
                            }

                            // Configure Dust Wall
                            if (dustWallPrefab != null)
                            {
                                GameObject dustWall = Instantiate(dustWallPrefab, nukeForwardPos, Quaternion.Euler(Vector3.zero)) as GameObject;
                                dustWall.transform.LookAt(player);
                                dustWall.transform.position += (dustWall.transform.forward * dustWallDistance);
                                dustWall.GetComponent<Rigidbody>().AddForce(dustWall.transform.forward * shockwaveSpeed, ForceMode.Force);
                                DustWall dwScript = dustWall.GetComponent<DustWall>();
                                dwScript.fixedFromPosition = nukePos;
                            }

                            // Configure Shock Wall
                            if (shockWallPrefab != null)
                            {
                                GameObject shockWall = Instantiate(shockWallPrefab, nukeForwardPos, Quaternion.Euler(Vector3.zero)) as GameObject;
                                shockWall.transform.LookAt(player);
                                shockWall.GetComponent<Rigidbody>().AddForce(shockWall.transform.forward * shockwaveSpeed, ForceMode.Force);
                                ShockWall swScript = shockWall.GetComponent<ShockWall>();
                                swScript.origin = nukePos;
                            }
                            
                            Invoke("BroadcastNukeStart", 0.1f);

                            Invoke("BroadcastNukeEnd", 25f);

                            nukeTimer = 30f; // only one nuke every x seconds.
                        }
                        break;
                    case WeaponType.Gun:
                        FireGun();
                        break;
                    case WeaponType.Melee:
                        if (Time.time >= (lastMeleeTime + meleeAttackDelay))
                            MeleeAttack();
                        break;
                    case WeaponType.RepairWrench:
                        if (Time.time >= (lastMeleeTime + meleeAttackDelay))
                            RepairByHand();
                        break;
                }
            }

            // Continuous fire from holding the button down
            if (Input.GetButton("Fire1") && SelectedWeapon == WeaponType.Gun && Time.time >= (lastShotTime + timeBetweenShots))
                FireGun();

            // Continuous melee attack from holding the button down (useful for chopping trees in an MMO/survival game)
            if (Input.GetButton("Fire1") && SelectedWeapon == WeaponType.Melee && Time.time >= (lastMeleeTime + meleeAttackDelay))
                MeleeAttack();

            // Repair continuously while holding the Fire1 button down.
            if (Input.GetButton("Fire1") && SelectedWeapon == WeaponType.RepairWrench && Time.time >= (lastMeleeTime + meleeAttackDelay))
                RepairByHand();
            
            // Time Slow
            if (Input.GetKeyUp("t"))
            {
                timeSlowed = !timeSlowed;
                SetTimeScale();
            }

            // Time Stop
            if (Input.GetKeyUp("y"))
            {
                timeStopped = !timeStopped;
                SetTimeScale();
            }
            
            // Do this every frame for rigidbodies that enter the scene, so they have smooth frame interpolation.
            // TODO: can probably run this more efficiently at a set rate, like a few times per second - not every frame.
            if (timeSlowed)
            {
                foreach (GameObject go in FindObjectsOfType(typeof(GameObject)))
                {
                    foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>())
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }
            
            // Reset the scene
            if (Input.GetKey("r"))
            {
                TreeManager treeManager = TreeManager.Instance;
                if (treeManager != null)
                {
                    //TODO: Black out the main camera first. You can see trees insta-respawn when restored, and it looks weird.
                    treeManager.RestoreTrees();
                }

#if UNITY_EDITOR
                EditorSceneManager.LoadSceneAsyncInPlayMode(SceneManager.GetActiveScene().path, lsp);
#else
                SceneManager.LoadSceneAsync($"Assets/DestroyIt/Demos (safe to delete)/{SceneManager.GetActiveScene().name}.unity", lsp);
#endif
            }

            if (Input.GetKeyUp("q"))
            {
                SelectedWeapon = WeaponHelper.GetPrevious(SelectedWeapon);
                PlayerPrefs.SetInt("SelectedWeapon", (int)SelectedWeapon);
                SetActiveWeapon();
            }
            if (Input.GetKeyUp("e"))
            {
                SelectedWeapon = WeaponHelper.GetNext(SelectedWeapon);
                PlayerPrefs.SetInt("SelectedWeapon", (int)SelectedWeapon);
                SetActiveWeapon();
            }
            if (Input.GetKeyUp("o"))
            {
                if (playerPrefShowReticle == -1)
                    playerPrefShowReticle = 0;
                else
                    playerPrefShowReticle = -1;
                
                PlayerPrefs.SetInt("ShowReticle", playerPrefShowReticle);
            }
            if (Input.GetKeyUp("h"))
            {
                if (playerPrefShowHud == -1)
                    playerPrefShowHud = 0;
                else
                    playerPrefShowHud = -1;

                PlayerPrefs.SetInt("ShowHud", playerPrefShowHud);
            }

            if (Input.GetKeyUp("m"))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                
                // Always restore trees before changing scenes so you don't lose terrain tree data in the Unity editor.
                TreeManager treeManager = TreeManager.Instance;
                if (treeManager != null)
                    treeManager.RestoreTrees();

#if UNITY_EDITOR
                EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DestroyIt/Demos (safe to delete)/Choose Demo.unity", lsp);
#else
                SceneManager.LoadSceneAsync("Assets/DestroyIt/Demos (safe to delete)/Choose Demo.unity", lsp);
#endif
            }

            float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (scrollWheel > 0f) // scroll up
            {
                SelectedWeapon = WeaponHelper.GetNext(SelectedWeapon);
                PlayerPrefs.SetInt("SelectedWeapon", (int)SelectedWeapon);
                SetActiveWeapon();
            }
            if (scrollWheel < 0f) // scroll down
            {
                SelectedWeapon = WeaponHelper.GetPrevious(SelectedWeapon);
                PlayerPrefs.SetInt("SelectedWeapon", (int)SelectedWeapon);
                SetActiveWeapon();
            }
        }

        private void EnableWindZone()
        {
            if (windZone != null)
                windZone.SetActive(true);
        }

        private void SetActiveWeapon()
        {
            gunTransform.gameObject.SetActive(SelectedWeapon == WeaponType.Gun);
            cannonTransform.gameObject.SetActive(SelectedWeapon == WeaponType.Cannonball);
            rocketTransform.gameObject.SetActive(SelectedWeapon == WeaponType.Rocket);
            nukeTransform.gameObject.SetActive(SelectedWeapon == WeaponType.Nuke);
            axeTransform.gameObject.SetActive(SelectedWeapon == WeaponType.Melee);
            repairWrenchTransform.gameObject.SetActive(SelectedWeapon == WeaponType.RepairWrench);
        }

        private void MeleeAttack()
        {
            Animation anim = axeTransform.GetComponentInChildren<Animation>();
            anim.Play("Axe Swinging");
            lastMeleeTime = Time.time;
            Invoke("BroadcastMeleeDamage", .2f);
        }

        private void RepairByHand()
        {
            Animation anim = repairWrenchTransform.GetComponentInChildren<Animation>();
            anim.Play("Wrench Turn");
            lastMeleeTime = Time.time;
            Invoke("BroadcastRepairDamage", .2f);
        }

        private void BroadcastMeleeDamage()
        {
            firstPersonController.BroadcastMessage("OnMeleeDamage");
        }

        private void BroadcastRepairDamage()
        {
            firstPersonController.BroadcastMessage("OnMeleeRepair");
        }

        private void BroadcastNukeStart()
        {
            firstPersonController.BroadcastMessage("OnNukeStart");
        }

        private void BroadcastNukeEnd()
        {
            firstPersonController.BroadcastMessage("OnNukeEnd");
        }

        private void FireGun()
        {
            // Play muzzle flash particle effect
            if (muzzleFlash != null)
                muzzleFlash.Emit(1);
            
            // Turn on muzzle flash point light
            if (muzzleLight != null && !muzzleLight.enabled)
            {
                muzzleLight.enabled = true;
                Invoke("DisableMuzzleLight", 0.1f);
            }

            WeaponHelper.Launch(bulletPrefab, gunTransform, 0f, false); // Launch bullet
            
            lastShotTime = Time.time;
        }

        private void DisableMuzzleLight()
        {
            if (muzzleLight != null && muzzleLight.enabled)
                muzzleLight.enabled = false;
        }

        private void SetTimeScale()
        {
            if (timeStopped)
            {
                Time.timeScale = 0f;
                return;
            }

            if (timeSlowed)
            {
                Time.timeScale = timeSlowSpeed;
                /* DISABLING RIGIDBODY INTERPOLATION TEMPORARILY DUE TO AN ONGOING UNITY BUG:
                   https://issuetracker.unity3d.com/issues/in-order-to-call-gettransforminfoexpectuptodate-dot-dot-dot-error-message-appears-while-using-rigidbody-interpolate-slash-extrapolate
                */
                foreach (GameObject go in FindObjectsOfType(typeof(GameObject)))
                {
                    foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>())
                        rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
            }
            else
            {
                Time.timeScale = 1.0f;
                /* DISABLING RIGIDBODY INTERPOLATION TEMPORARILY DUE TO AN ONGOING UNITY BUG:
                   https://issuetracker.unity3d.com/issues/in-order-to-call-gettransforminfoexpectuptodate-dot-dot-dot-error-message-appears-while-using-rigidbody-interpolate-slash-extrapolate
                */
                foreach (GameObject go in FindObjectsOfType(typeof(GameObject)))
                {
                    foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>())
                        rb.interpolation = RigidbodyInterpolation.None;
                }
            }
        }
    }
}